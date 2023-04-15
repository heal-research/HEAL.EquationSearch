using HEAL.NativeInterpreter;
using System.Runtime.InteropServices;

namespace HEAL.EquationSearch {
  public class Evaluator {
    public long OptimizedExpressions => exprQualities.Count;
    public long EvaluatedExpressions = 0;

    // TODO: this should not be necessary
    public NonBlocking.ConcurrentDictionary<ulong, double> exprQualities = new();

    // TODO: make iterations configurable
    internal double OptimizeAndEvaluate(Expression expr, Data data, int iterations = 10) {

      var semHash = Semantics.GetHashValue(expr);
      Interlocked.Increment(ref EvaluatedExpressions);

      if (exprQualities.TryGetValue(semHash, out double quality)) {
        // TODO: parameters of expression are not set in this case
        return quality;
      }

      var terms = new List<(int start, int end)>();
      var coeffIndexes = new List<int>();
      GetTerms(expr, terms, coeffIndexes);

      // compile all terms individually
      var code = CompileTerms(expr, terms, data, out var termIdx, out var paramIdx);

      // no terms for which to optimize parameters -> return MSE of constant model
      if (termIdx.Count == 0) { return Variance(data.Target); }

      var result = new double[data.Rows];

      var mse = double.MaxValue;
      // optimize parameters

      var solverOptions = new SolverOptions() { Iterations = iterations, Algorithm = 1 };

      var coeff = new double[coeffIndexes.Count];
      NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.Weights, coeff, solverOptions, result, out var summary);

      // update parameters if successful
      if (summary.Success == 1 && !double.IsNaN(summary.FinalCost)) {
        expr.UpdateCoefficients(coeffIndexes.ToArray(), coeff);

        foreach (var (codePos, exprPos) in paramIdx) {
          var instr = code[codePos];
          if (instr.Optimize != 1) continue;

          if (expr[exprPos] is Grammar.ParameterSymbol paramSy)
            paramSy.Value = instr.Coeff;
        }

        mse = summary.FinalCost * 2 / data.Rows;


      } else {
        return double.MaxValue; // optimization failed
      }

      // for debugging and unit tests
      // Console.Error.WriteLine($"len: {expr.Length} hash: {semHash} {expr}");

      exprQualities.GetOrAdd(semHash, mse);
      return mse;
    }

    internal double[] Evaluate(Expression expression, Data data) {
      var code = CompileTerms(expression, new[] { (start: 0, end: expression.Length - 1) }, data, out var _, out var _);
      var result = new double[data.Rows];
      NativeWrapper.GetValues(code, data.AllRowIdx, result);
      return result;
    }

    // TODO: can be removed (only used in State to handle the special case of a constant expression)
    internal static double Variance(double[] y) {
      var ym = y.Average();
      var variance = 0.0;
      for (int i = 0; i < y.Length; i++) {
        var res = y[i] - ym;
        variance += res * res;
      }
      return variance / y.Length;
    }


    // TODO: for debugging only
    private static double CalculateMSE(double[] target, double[] pred) {
      var mse = 0.0;
      for (int i = 0; i < target.Length; i++) {
        var res = target[i] - pred[i];
        mse += res * res;
      }
      mse /= target.Length;
      return mse;
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