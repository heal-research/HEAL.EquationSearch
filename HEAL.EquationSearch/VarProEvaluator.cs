using HEAL.Expressions;
using HEAL.NativeInterpreter;
using HEAL.NonlinearRegression;
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
    public double OptimizeAndEvaluateMSE(Expression expr, Data data) {
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
        for (int i = 0; i < data.Target.Length; i++) {
          var res = data.Target[i] - ym;
          sse += data.InvNoiseSigma[i] * data.InvNoiseSigma[i] * res * res;
        }
        return sse / data.Target.Length; // weighted MSE of mean model
      }

      var result = new double[data.Rows];

      mse = double.MaxValue;

      // TODO: restarts
      // optimize parameters (objective: minimize sum_i 1/2 (w_i^2 (y_i - y_pred_i)^2)  where we use w = 1/sErr)
      var solverOptions = new SolverOptions() { Iterations = 100, Algorithm = 1 }; // Algorithm 1: Krogh Variable Projection
      var coeff = new double[coeffIndexes.Count];
      // Debug.Assert(coeffIndexes.Count == 1 + termIdx.Count); // one coefficient for each term + intercept
      NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.InvNoiseSigma, coeff, solverOptions, result, out var summary);
      Interlocked.Increment(ref optimizedExpressions);

      // update parameters if optimization lead to an improvement
      if (!double.IsNaN(summary.FinalCost) && summary.FinalCost < summary.InitialCost) {
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
    public double OptimizeAndEvaluateDL(Expression expr, Data data) {
      AssertExpressionStructure(expr); // VarProEval requires the last coefficient to be the offset. Otherwise evaluation will fail silently!
      Interlocked.Increment(ref evaluatedExpressions);

      var terms = new List<(int start, int end)>();
      var coeffIndexes = new List<int>();
      GetTerms(expr, terms, coeffIndexes);
      if (terms.Count != coeffIndexes.Count - 1) throw new InvalidProgramException("len(terms) != len(coefficients) - 1");  // the additional coeff is for the constant offset. this must be the 

      // compile all terms individually
      var code = CompileTerms(expr, terms, data, out var termIdx, out var paramIdx);

      // no terms for which to optimize parameters -> return default MDL
      if (termIdx.Count == 0) {
        // This case is not particularly interesting (average model).
        // TODO: we could reuse expr here and set the parameter correctly. 
        // TODO: here we would also need to take a weighted average. 
        // return MDL(new Expression(expr.Grammar, new Grammar.Symbol[] { new Grammar.ParameterSymbol(data.Target.Average()) }), data);
        return double.MaxValue;
      }

      var result = new double[data.Rows];

      var solverOptions = new SolverOptions() { Iterations = 100, Algorithm = 1 };
      var coeff = new double[coeffIndexes.Count];
      var bestCost = double.MaxValue;
      double[]? bestCoeff = null;

      double[]? parameterValues = new double[paramIdx.Count];

      // extract parameter vector from compiled expr
      // (for random restarts)
      var pIdx = 0;
      for (int i = 0; i < code.Length; i++) {
        if (code[i].Optimize == 1) {
          parameterValues[pIdx++] = code[i].Coeff;
        }
      }

      var restartPolicy = new RestartPolicy(parameterValues.Length);
      do {

        // set initial value for nonlinear parameters (for random restarts)
        // (this is not necessary on the first iteration)
        pIdx = 0;
        for (int i = 0; i < code.Length; i++) {
          if (code[i].Optimize == 1) {
            code[i].Coeff = parameterValues[pIdx++];
          }
        }

        // optimize parameters (objective: minimize sum_i 1/2 (w_i^2 (y_i - y_pred_i)^2)  where we use w = 1/sErr)
        NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.InvNoiseSigma, coeff, solverOptions, result, out var summary);
        // NOTE: last coefficient is offset parameter!

        Interlocked.Increment(ref optimizedExpressions);

        // update parameters if optimization lead to an improvement
        if (summary.Success == 1 && summary.FinalCost < bestCost) {
          bestCost = summary.FinalCost;
          bestCoeff = (double[])coeff.Clone();
        }

        if (summary.Success == 1) {
          // extract optimized nonlinear parameters
          pIdx = 0;
          for (int i = 0; i < code.Length; i++) {
            if (code[i].Optimize == 1) {
              parameterValues[pIdx++] = code[i].Coeff;
            }
          }

          restartPolicy.Update(parameterValues, loss: summary.FinalCost);
        }

        // prepare for next iteration
        parameterValues = restartPolicy.Next();
      } while (parameterValues != null);

      // when parameters were improved
      if (bestCost < double.MaxValue) {
        // update linear coefficients in expr
        expr.UpdateCoefficients(coeffIndexes.ToArray(), bestCoeff);
        // update nonlinear parameters in expr
        pIdx = 0;
        for (int i = 0; i < code.Length; i++) {
          if (code[i].Optimize == 1) {
            var paramSy = expr[paramIdx.Find(tup => tup.codePos == i).exprPos] as Grammar.ParameterSymbol;
            paramSy.Value = restartPolicy.BestParameters[pIdx++];
          }
        }
      }

      // generate a tree from the updated expression to evaluate DL
      var exprTree = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var paramValues);
      var likelihood = new GaussianLikelihood(data.X, data.Target, exprTree, data.InvNoiseSigma);
      try {
        // var dl = ModelSelection.DLWithIntegerSnap(paramValues, likelihood);
        var dl = ModelSelection.DL(paramValues, likelihood);

        // for debugging
        // Console.WriteLine($"len: {expr.Length} DL: {dl:e6} nll: {likelihood.NegLogLikelihood(paramValues):e6} {string.Join(" ", paramValues.Select(pi => pi.ToString("e4")))} starts {restartPolicy.Iterations} numBest {restartPolicy.NumBest} {expr.ToInfixString()} ");
        return dl;
      } catch (Exception e) {
        System.Console.Error.WriteLine(e.Message);
        return double.MaxValue;
      }
    }

    private static void AssertExpressionStructure(Expression expr) {
      // expression must end with ... p + 
      if (expr.Length == 1 && expr[0] is Grammar.ParameterSymbol) return;
      else {
        // must end with additions
        int i = expr.Length - 1;
        while (i >= 0 && expr[i] == expr.Grammar.Plus) {
          i--;
        }
        // the last symbol which is not + must be a parameter
        if(i >= 0 && expr[i] is Grammar.ParameterSymbol) return;
      }
      throw new ArgumentException("The expression must end with addition of the offset");
    }

    public double[] Evaluate(Expression expression, Data data) {
      Interlocked.Increment(ref evaluatedExpressions);
      var code = CompileTerms(expression, new[] { (start: 0, end: expression.Length - 1) }, data, out var _, out var _);
      var result = new double[data.Rows];
      NativeWrapper.GetValues(code, data.AllRowIdx, result);
      return result;
    }


    private NativeInstruction[] CompileTerms(Expression expr, IEnumerable<(int start, int end)> terms, Data data,
      out List<int> termIdx, out List<(int codePos, int exprPos)> paramSyIdx) {

      if (cachedDataHandles == null || cachedData != data) {
        InitCache(data);
      }

      // var codeLen = terms.Sum(t => t.end - t.start + 1);
      var code = new List<NativeInstruction>();
      termIdx = new List<int>();

      paramSyIdx = new List<(int, int)>();
      foreach (var (start, end) in terms) {
        var containsVariable = false;
        // requires a postfix representation
        for (int exprIdx = start; exprIdx <= end; exprIdx++) {
          AddInstructions(expr, exprIdx, code, paramSyIdx);
          containsVariable |= expr[exprIdx] is Grammar.VariableSymbol;
        }
        // skip constant terms because VarPro will be unstable otherwise
        if (containsVariable) {
          termIdx.Add(code.Count - 1);
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

      return code.ToArray();
    }

    private void AddInstructions(Expression expr, int exprIdx, List<NativeInstruction> code, List<(int codePos, int exprPos)> paramSyIdx) {
      var grammar = expr.Grammar;
      var curSy = expr[exprIdx];
      if (curSy is Grammar.VariableSymbol varSy) {
        var curInstr = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 1.0 };
        curInstr.Data = cachedDataHandles[varSy.VariableName].AddrOfPinnedObject();
        code.Add(curInstr);
      } else if (curSy is Grammar.ParameterSymbol paramSy) {
        var curInstr = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 1.0 };
        curInstr.Coeff = paramSy.Value;
        curInstr.Optimize = 1;
        paramSyIdx.Add((codePos: code.Count, exprPos: exprIdx));
        code.Add(curInstr);
      } else if (curSy is Grammar.ConstantSymbol constSy) {
        var curInstr = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 1.0 };
        curInstr.Coeff = constSy.Value;
        code.Add(curInstr);
      } else if (curSy == grammar.Inv) {
        // inv(x) => 1/(x)
        var chIdx = code.Count - 1; // child idx;
        var chLen = code[chIdx].Length;

        var instr1 = new NativeInstruction { Arity = 0, OpCode = (int)OpCode.Constant, Length = 1, Optimize = 0, Coeff = 1.0 };
        var instrDiv = new NativeInstruction { Arity = 2, OpCode = (int)OpCode.Div, Length = chLen + 2, Optimize = 0, Coeff = 0.0 };
        code.Add(instr1);
        code.Add(instrDiv);
      } else if (curSy == grammar.Neg) {
        // neg(x) => (-1)*(x)
        var chIdx = code.Count - 1; // child idx;
        var chLen = code[chIdx].Length;
        var instr1 = new NativeInstruction { Arity = 0, OpCode = (int)OpCode.Constant, Length = 1, Optimize = 0, Coeff = -1.0 };
        var instrMul = new NativeInstruction { Arity = 2, OpCode = (int)OpCode.Mul, Length = chLen + 2, Optimize = 0, Coeff = 0.0 };
        code.Add(instr1);
        code.Add(instrMul);
      } else {
        var curInstr = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 0.0 }; // length updated below, coeff irrelevant
        // for all other symbols update the code length
        var c = code.Count - 1; // first child idx;
        for (int j = 0; j < curInstr.Arity; ++j) {
          curInstr.Length += code[c].Length;
          c -= code[c].Length; // next child idx
        }
        code.Add(curInstr);
      }
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
          coeffIndexes.Insert(0, exprIdx);
        } else {
          // Assert that each term has the pattern: <coeff * term> or just <coeff>
          // throw new InvalidProgramException($"Term does not have the structure <coeff * term> in {string.Join(" ", expr.Select(sy => sy.ToString()))} at position {exprIdx}");
        }
      }
    }


    private static int SymbolToOpCode(Grammar grammar, Grammar.Symbol symbol) {
      if (symbol == grammar.Plus) {
        return (int)OpCode.Add;
      } else if (symbol == grammar.Neg) {
        throw new NotSupportedException(); // multiple instructions for one symbol required
      } else if (symbol == grammar.Times) {
        return (int)OpCode.Mul;
      } else if (symbol == grammar.Inv) {
        throw new NotSupportedException(); // multiple instructions for one symbol required
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
      } else if (symbol == grammar.Pow) {
        return (int)OpCode.Power;
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