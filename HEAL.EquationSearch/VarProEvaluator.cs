using HEAL.NativeInterpreter;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HEAL.EquationSearch {
  public class VarProEvaluator : IEvaluator {
    private long optimizedExpressions = 0;
    private long evaluatedExpressions = 0;
    public long OptimizedExpressions => optimizedExpressions;

    public long EvaluatedExpressions => evaluatedExpressions;

    // The caches in GraphSearchControl and Evaluator have different purposes.
    // The cache in GraphSearchControl prevents visiting duplicate states in the state graph.
    // The cache in Evaluator prevents duplicate evaluations. 
    // Currently, they are both necessary because GraphSearchControl calculates
    // semantic hashes for expressions with nonterminal symbols (this is necessary to distinguish terminal states from nonterminal states),
    // while the cache in Evaluator only sees expressions where nonterminal symbols have been replaced by terminal symbols.
    public NonBlocking.ConcurrentDictionary<ulong, double> exprQualities = new();

    // TODO: make iterations configurable
    // This method uses caching for efficiency.
    // IMPORTANT: This method does not update parameter values in expr. Use for heuristic evaluation only.
    public double OptimizeAndEvaluateMSE(Expression expr, Data data, int iterations = 10) {
      var semHash = Semantics.GetHashValue(expr);
      Interlocked.Increment(ref evaluatedExpressions);

      if (exprQualities.TryGetValue(semHash, out double mse)) {
        // NOTE: parameters of expression are not set in this case
        return mse;
      }

      var terms = new List<(int start, int end)>();
      var coeffIndexes = new List<int>();
      GetTerms(expr, terms, coeffIndexes);

      // compile all terms individually
      var code = CompileTerms(expr, terms, data, out var termIdx, out var paramIdx);

      // no terms for which to optimize parameters -> return MSE for (weighted) mean model
      if (termIdx.Count == 0) {
        // This case is not particularly interesting (average model).
        // TODO: we could reuse expr here and set the parameter correctly. 
        var ym = data.Target.Average();
        var sse = 0.0;
        for(int i=0;i<data.Target.Length;i++) {
          var res = data.Target[i] - ym;
          sse += data.InvNoiseVariance[i] * res * res;
        }
        return sse / data.Target.Length; // weighted MSE of mean model
      }

      var result = new double[data.Rows];

      mse = double.MaxValue;

      // optimize parameters (objective: minimize sum_i (w_i^2 (y_i - y_pred_i)^2)  where we use w = 1/sErr²)
      var solverOptions = new SolverOptions() { Iterations = iterations, Algorithm = 1 }; // Algorithm 1: Krogh Variable Projection
      var coeff = new double[coeffIndexes.Count];
      Debug.Assert(coeffIndexes.Count == 1 + termIdx.Count); // one coefficient for each term + intercept
      NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.InvNoiseSigma, coeff, solverOptions, result, out var summary);
      Interlocked.Increment(ref optimizedExpressions);

      // update parameters if optimization lead to an improvement
      if (!double.IsNaN(summary.FinalCost) && summary.FinalCost < summary.InitialCost ) {
        mse = summary.FinalCost * 2 / data.Rows;
      } else {
        mse = summary.InitialCost * 2 / data.Rows;
        if (double.IsNaN(mse)) mse = double.MaxValue;
      }

      exprQualities.GetOrAdd(semHash, mse);
      return mse;
    }

    // TODO: make iterations configurable
    // This method always optimizes parameters in expr but does not use caching to make sure all parameters of the evaluated expressions are set correctly.
    // Use this method to optimize the best solutions (found via MSE)
    public double OptimizeAndEvaluateMDL(Expression expr, Data data, int iterations = 10) {
      Interlocked.Increment(ref evaluatedExpressions);

      var terms = new List<(int start, int end)>();
      var coeffIndexes = new List<int>();
      // compile all terms individually
      var code = CompileTerms(expr, terms, data, out var termIdx, out var paramIdx);

      // no terms for which to optimize parameters -> return default MDL
      if (termIdx.Count == 0) {
        // This case is not particularly interesting (average model).
        // TODO: we could reuse expr here and set the parameter correctly. 
        // TODO: here we would also need to take a weighted average. 
        return MDL(new Expression(expr.Grammar, new Grammar.Symbol[] { new Grammar.ParameterSymbol(data.Target.Average()) }), data);
      }

      var result = new double[data.Rows];

      // optimize parameters
      var solverOptions = new SolverOptions() { Iterations = iterations, Algorithm = 1 };
      var coeff = new double[coeffIndexes.Count];
      // The first coefficient is always the intercept
      NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.InvNoiseSigma, coeff, solverOptions, result, out var summary);
      Interlocked.Increment(ref optimizedExpressions);

      // update parameters if optimization lead to an improvement
      if (summary.Success == 1) {
        expr.UpdateCoefficients(coeffIndexes.ToArray(), coeff);
        
        foreach (var (codePos, exprPos) in paramIdx) {
          var instr = code[codePos];
          if (instr.Optimize != 1) continue;
        
          if (expr[exprPos] is Grammar.ParameterSymbol paramSy)
            paramSy.Value = instr.Coeff;
        }
      }

      var mdl = MDL(expr, data);

      // for debugging and unit tests
      Console.Error.WriteLine($"len: {expr.Length} MDL: {mdl} logLik: {summary.FinalCost} {expr}");
      return mdl;
    }


    public double[] Evaluate(Expression expression, Data data) {
      Interlocked.Increment(ref evaluatedExpressions);
      var code = CompileTerms(expression, new[] { (start: 0, end: expression.Length - 1) }, data, out var _, out var _);
      var result = new double[data.Rows];
      NativeWrapper.GetValues(code, data.AllRowIdx, result);
      return result;
    }


    // as described in https://arxiv.org/abs/2211.11461
    // Deaglan J. Bartlett, Harry Desmond, Pedro G. Ferreira, Exhaustive Symbolic Regression, 2022

    private double MDL(Expression expr, Data data) {
      var code = CompileTerms(expr, new[] { (start: 0, end: expr.Length - 1) }, data, out var _, out var paramSyIdx);

      var result = new double[data.Rows];
      NativeWrapper.GetValues(code, data.AllRowIdx, result);

      // total description length:
      // L(D) = L(D|H) + L(H)

      // c_j are constants
      // theta_i are parameters
      // k is the number of nodes
      // n is the number of different symbols
      // Delta_i is inverse precision of parameter i
      // Delta_i are optimized to find minimum total description length
      // The paper shows that the optima for delta_i are sqrt(12/I_ii)
      // The formula implemented here is Equation (7).

      // L(D) = -log(L(theta)) + k log n - p/2 log 3
      //        + sum_j (1/2 log I_ii + log |theta_i| )

      var logLike = GaussianLogLikelihood(data.InvNoiseVariance, data.Target, result);
      var diagFisherInfo = ApproximateFisherInformation(logLike, code, paramSyIdx, data);
      if (diagFisherInfo.Any(fi => fi <= 0)) return double.MaxValue;

      int numNodes = expr.Length;
      var constants = expr.OfType<Grammar.ConstantSymbol>().ToArray();
      var numSymbols = expr.Select(sy => sy.GetHashCode()).Distinct().Count();
      var parameters = expr.OfType<Grammar.ParameterSymbol>().ToArray();
      int numParam = parameters.Length;

      /*
       * TODO: This does not work reliably yet
      for (int i = 0; i < numParam; i++) {
        // if the parameter estimate is not significantly different from zero
        if (Math.Abs(parameters[i].Value) / Math.Sqrt(12.0 / diagFisherInfo[i]) < 1.0) {

          // set param to zero and calculate MDL for the manipulated expression and code and re-evaluate 
          ((Grammar.ParameterSymbol)expr[paramSyIdx[i].exprPos]).Value = 0.0;
          code[paramSyIdx[i].codePos].Coeff = 0.0;

          Interlocked.Increment(ref EvaluatedExpressions);
          NativeWrapper.GetValues(code, data.AllRowIdx, result);

          // update likelihood for manipulated expression
          // As described in the ESR paper. More accurately we would need to set the value to zero, simplify and re-optimize the expression and recalculate logLik and FIM.
          // Here we are not too concerned about this, because the simplified expression is likely to be visited independently anyway. 
          logLike = GaussianLogLikelihood(data.InvNoiseVariance, data.Target, result);
        }

        // IDEA: a similar manipulation could be performed for multiplicative coefficients that are approximately equal to 1.0
      }
      */

      double paramCodeLength(int paramIdx) {
        // ignore zeros
        if (parameters[paramIdx].Value == 0) return 0.0;
        else return 0.5 * Math.Log(diagFisherInfo[paramIdx]) - 0.5 * Math.Log(3) + Math.Log(Math.Abs(parameters[paramIdx].Value));
      }

      // The grammar does not allow negative or zero constants
      return -logLike
        + numNodes * Math.Log(numSymbols) + constants.Sum(ci => Math.Log(Math.Abs(ci.Value)))
        + Enumerable.Range(0, numParam).Sum(i => paramCodeLength(i));
    }

    private double GaussianLogLikelihood(double[] invNoiseVariance, double[] target, double[] result) {
      var logLik = 0.0;
      for (int i = 0; i < target.Length; i++) {
        var res = target[i] - result[i];
        logLik -= 0.5 * invNoiseVariance[i] * res * res;
      }
      return logLik;
    }

    private double[] ApproximateFisherInformation(double logLik, NativeInstruction[] code, List<(int codePos, int exprPos)> paramSyIdx, Data data) {
      // numeric approximation of fisher information for each parameter (diagonal of fisher information matrix)
      // TODO: it would be much better to extract the Jacobian from hl-native-interpreter instead
      // TODO: this is numerically unstable!
      const double delta = 1e-6;
      var fi = new double[paramSyIdx.Count];
      var paramIdx = 0;
      var tempResult = new double[data.Rows];
      foreach (var (codePos, exprPos) in paramSyIdx) { // paramSyIdx is sorted
        var origCoeff = code[codePos].Coeff;
        code[codePos].Coeff = origCoeff - delta;
        NativeWrapper.GetValues(code, data.AllRowIdx, tempResult);
        var low = GaussianLogLikelihood(data.InvNoiseVariance, data.Target, tempResult);

        code[codePos].Coeff = origCoeff + delta;
        NativeWrapper.GetValues(code, data.AllRowIdx, tempResult);
        var high = GaussianLogLikelihood(data.InvNoiseVariance, data.Target, tempResult);

        // https://math.stackexchange.com/questions/2634825/approximating-second-derivative-from-taylors-theorem
        // factor -1 for the Fisher information
        fi[paramIdx++] = -(1 / delta / delta) * (low + high - 2 * logLik);
        code[codePos].Coeff = origCoeff;
      }
      return fi;
    }

    private NativeInstruction[] CompileTerms(Expression expr, IEnumerable<(int start, int end)> terms, Data data,
      out List<int> termIdx, out List<(int codePos, int exprPos)> paramSyIdx) {

      if (cachedDataHandles == null || cachedData != data) {
        InitCache(data);
      }

      var codeLen = terms.Sum(t => t.end - t.start + 1);
      var code = new NativeInstruction[codeLen];
      termIdx = new List<int>();

      paramSyIdx = new List<(int, int)>();
      int codeIdx = 0;
      foreach (var (start, end) in terms) {
        var containsVariable = false;
        // requires a postfix representation
        for (int exprIdx = start; exprIdx <= end; exprIdx++) {
          var curSy = expr[exprIdx];
          code[codeIdx] = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 1.0 };
          if (curSy is Grammar.VariableSymbol varSy) {
            code[codeIdx].Data = cachedDataHandles[varSy.VariableName].AddrOfPinnedObject();
            containsVariable |= true;
          } else if (curSy is Grammar.ParameterSymbol paramSy) {
            code[codeIdx].Coeff = paramSy.Value;
            code[codeIdx].Optimize = 1;
            paramSyIdx.Add((codePos: codeIdx, exprPos: exprIdx));
          } else if (curSy is Grammar.ConstantSymbol constSy) {
            code[codeIdx].Coeff = constSy.Value;
          } else {
            // for all other symbols update the code length
            var c = codeIdx - 1; // first child idx;
            for (int j = 0; j < code[codeIdx].Arity; ++j) {
              code[codeIdx].Length += code[c].Length;
              c -= code[c].Length; // next child idx
            }
          }
          codeIdx++;
        }
        // remove constant terms because VarPro will be unstable otherwise
        if (containsVariable) {
          termIdx.Add(codeIdx - 1);
        }
      }

#if DEBUG
      // ASSERT that parameter indexes are sorted ascending
      for (int i = 0; i < paramSyIdx.Count - 1; i++) {
        if (paramSyIdx[i].codePos >= paramSyIdx[i + 1].codePos ||
            paramSyIdx[i].exprPos >= paramSyIdx[i + 1].exprPos)
          throw new InvalidProgramException("assertion failed");
      }
#endif

      return code;
    }

    private static void GetTerms(Expression expr, List<(int start, int end)> terms, List<int> coeffIndexes) {
      var lengths = Semantics.GetLengths(expr);
      GetTermsRec(expr, expr.Length - 1, lengths, terms, coeffIndexes);
    }

    private static void GetTermsRec(Expression expr, int exprIdx, int[] lengths, List<(int start, int end)> terms, List<int> coeffIndexes) {
      // get terms
      if (expr[exprIdx] == expr.Grammar.Plus) {
        var c = exprIdx - 1; // first child idx
        for (int cIdx = 0; cIdx < expr[exprIdx].Arity; cIdx++) {
          GetTermsRec(expr, c, lengths, terms, coeffIndexes);
          c = c - lengths[c];
        }
      } else {
        // here we accept only "<coeff> <term> *" or "<coeff>"
        if (expr[exprIdx] == expr.Grammar.Times) {
          // calculate index of coefficient
          var end = exprIdx - 1;
          var start = end - lengths[end] + 1;
          terms.Insert(0, (start, end)); // TODO: perf
          var coeffIdx = start - 1;
          if (expr[coeffIdx] is not Grammar.ParameterSymbol) throw new NotSupportedException("Invalid expression form. Expected: <coeff> <term> *");
          coeffIndexes.Insert(0, coeffIdx); // TODO: perf
        } else if (expr[exprIdx] is Grammar.ParameterSymbol) {
          coeffIndexes.Insert(0, exprIdx); // TODO: perf
        } else {
          // Assert that each term has the pattern: <coeff * term> or just <coeff>
          // throw new InvalidProgramException($"Term does not have the structure <coeff * term> in {string.Join(" ", expr.Select(sy => sy.ToString()))} at position {exprIdx}");
        }
      }
    }


    private static int SymbolToOpCode(Grammar grammar, Grammar.Symbol symbol) {
      if (symbol == grammar.Plus) {
        return (int)OpCode.Add;
      } else if (symbol == grammar.Times) {
        return (int)OpCode.Mul;
      } else if (symbol == grammar.Div) {
        return (int)OpCode.Div;
      } else if (symbol == grammar.Exp) {
        return (int)OpCode.Exp;
      } else if (symbol == grammar.Log) {
        return (int)OpCode.Log;
      } else if (symbol == grammar.Sqrt) {
        return (int)OpCode.Sqrt;
      } else if (symbol == grammar.Abs) {
        return (int)OpCode.Abs;
      } else if (symbol == grammar.Cos) {
        return (int)OpCode.Cos;
      } else if (symbol is Grammar.VariableSymbol) {
        return (int)OpCode.Variable;
      } else if (symbol is Grammar.ParameterSymbol) {
        return (int)OpCode.Number;
      } else if (symbol is Grammar.ConstantSymbol) {
        return (int)OpCode.Constant;
      } else throw new NotSupportedException($"Evaluator: unknown symbol {symbol}");
    }

    [ThreadStatic]
    private Data? cachedData;
    [ThreadStatic]
    private Dictionary<string, GCHandle>? cachedDataHandles;

    private void InitCache(Data data) {
      cachedData = data;
      // cache new data (but free old data first)
      if (cachedDataHandles != null) {
        foreach (var gch in cachedDataHandles.Values) {
          gch.Free();
        }
      }
      cachedDataHandles = new Dictionary<string, GCHandle>();
      foreach (var varName in data.VarNames) {
        var values = data.GetValues(varName);
        var gch = GCHandle.Alloc(values, GCHandleType.Pinned);
        cachedDataHandles[varName] = gch;
      }
    }
  }
}