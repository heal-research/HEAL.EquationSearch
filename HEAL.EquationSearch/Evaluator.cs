using HEAL.NativeInterpreter;
using System.Runtime.InteropServices;

namespace HEAL.EquationSearch {
  // TODO: non-static
  internal class Evaluator {
    public long OptimizedExpressions;
    // TODO: make iterations configurable
    internal double OptimizeAndEvaluate(Expression expr, Data data, int iterations = 10) {
      Interlocked.Increment(ref OptimizedExpressions);

      // Semantics.IndexOfTerms(expr, out var termRanges, out var coeffIdx);
      var semExpr = new Semantics.Expr(expr);
      var terms = semExpr.Terms;
      var coeffIdx = semExpr.CoeffIdx.ToArray();

      // compile all terms individually
      var code = CompileTerms(expr, terms, data, out var termIdx, out var paramIdx);

      var result = new double[data.Rows];

      var MSE = double.MaxValue;
      // optimize parameters

      var solverOptions = new SolverOptions() { Iterations = iterations, Algorithm = 1 };
      // NativeWrapper.Optimize(code, data.AllRowIdx, data.Target, data.Weights, solverOptions, result, out SolverSummary summary);

      var coeff = new double[coeffIdx.Length];
      NativeWrapper.OptimizeVarPro(code, termIdx.ToArray(), data.AllRowIdx, data.Target, data.Weights, coeff, solverOptions, result, out var summary);

      // update parameters if successful
      if (summary.Success == 1 && !double.IsNaN(summary.FinalCost)) {
        expr.UpdateCoefficients(coeffIdx, coeff);

        foreach (var i in paramIdx) {
          var instr = code[i.codePos];
          if (instr.Optimize != 1) continue;

          if (expr[i.exprPos] is Grammar.ParameterSymbol paramSy)
            paramSy.Value = instr.Coeff;
        }

        MSE = summary.FinalCost * 2 / data.Rows;


#if DEBUG
        // double check result
        var evalMSE = CalculateMSE(data.Target, Evaluate(expr, data));
        var relAbsErr = Math.Abs((MSE - evalMSE) / MSE);
        // notify user if there is an error larger than 0.1%
        if (relAbsErr > 1e-3)
          System.Console.Error.WriteLine($"Evaluation of optimized expression returns {100 * relAbsErr}% different result {evalMSE} than VarPro {MSE}.");
#endif

      } else {
        return double.MaxValue; // optimization failed
      }
      return MSE;
    }

    internal double[] Evaluate(Expression expression, Data data) {
      var semExpr = new Semantics.Term(expression, Semantics.GetLengths(expression), 0, expression.Length - 1); // treat the whole expression as a single term
      var code = CompileTerms(expression, new[] { semExpr }, data, out var _, out var _);
      var result = new double[data.Rows];
      NativeWrapper.GetValues(code, data.AllRowIdx, result);
      return result;
    }

    // TODO: can be removed (only used in State to handle the special case of a constant expression)
    internal double Variance(double[] y) {
      var ym = y.Average();
      var variance = 0.0;
      for (int i = 0; i < y.Length; i++) {
        var res = y[i] - ym;
        variance += res * res;
      }
      return variance / y.Length;
    }


    // TODO: for debugging only
    private double CalculateMSE(double[] target, double[] pred) {
      var mse = 0.0;
      for (int i = 0; i < target.Length; i++) {
        var res = target[i] - pred[i];
        mse += res * res;
      }
      mse /= target.Length;
      return mse;
    }

    private NativeInstruction[] CompileTerms(Expression expr, IEnumerable<Semantics.Term> terms, Data data,
      out List<int> termIdx, out List<(int codePos, int exprPos)> paramSyIdx) {

      if (cachedDataHandles == null || cachedData != data) {
        InitCache(data);
      }

      var codeLen = terms.Sum(t => t.end - t.start + 1);
      var code = new NativeInstruction[codeLen];
      termIdx = new List<int>();

      paramSyIdx = new List<(int, int)>();
      int codeIdx = 0;
      foreach (var t in terms) {
        // requires a postfix representation
        for (int exprIdx = t.start; exprIdx <= t.end; exprIdx++) {
          var curSy = expr[exprIdx];
          code[codeIdx] = new NativeInstruction { Arity = curSy.Arity, OpCode = SymbolToOpCode(expr.Grammar, curSy), Length = 1, Optimize = 0, Coeff = 1.0 };
          if (curSy is Grammar.VariableSymbol varSy) {
            code[codeIdx].Data = cachedDataHandles[varSy.VariableName].AddrOfPinnedObject();
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
        termIdx.Add(codeIdx - 1);
      }

      return code;
    }


    private int SymbolToOpCode(Grammar grammar, Grammar.Symbol symbol) {
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