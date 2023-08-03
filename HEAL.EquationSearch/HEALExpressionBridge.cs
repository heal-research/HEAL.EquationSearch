using System.Linq.Expressions;
using System.Reflection;
using LinqExpr = System.Linq.Expressions.Expression;

namespace HEAL.EquationSearch {
  // class with utility methods to use HEAL.NLR and HEAL.Expressions
  public class HEALExpressionBridge {

    // convert to an expression tree that is compatible with HEAL.NLR
    internal static Expression<HEAL.Expressions.Expr.ParametricFunction> ConvertToExpressionTree(Expression expr, string[] variableNames, out double[] parameterValues) {
      var lengths = Semantics.GetLengths(expr);
      var p = LinqExpr.Parameter(typeof(double[]), "p");
      var x = LinqExpr.Parameter(typeof(double[]), "x");
      var parameterValuesList = new List<double>();
      var body = ConvertToExpressionTreeRec(expr, p, x, variableNames, lengths, expr.Length - 1, parameterValuesList);
      parameterValues = parameterValuesList.ToArray();
      return LinqExpr.Lambda<Expressions.Expr.ParametricFunction>(body, p, x);
    }

    

    private static LinqExpr ConvertToExpressionTreeRec(Expression expr, ParameterExpression p, ParameterExpression x, string[] variableNames, int[] lengths, int i, List<double> paramValues) {
      var c = i - 1;
      var children = new List<LinqExpr>();
      for (int chIdx = 0; chIdx < expr[i].Arity; chIdx++) {
        children.Add(ConvertToExpressionTreeRec(expr, p, x, variableNames, lengths, c, paramValues));
        c = c - lengths[c];
      }

      if (children.Count > 2) throw new InvalidProgramException("the following code assumes binary operations");

      if (expr[i] is Grammar.ParameterSymbol paramSy) {
        paramValues.Add(paramSy.Value);
        return LinqExpr.ArrayIndex(p, LinqExpr.Constant(paramValues.Count - 1));
      } else if (expr[i] is Grammar.VariableSymbol varSy) {
        var varIdx = Array.IndexOf(variableNames, varSy.VariableName); // TODO: perf
        if (varIdx < 0) throw new InvalidProgramException("var name not found");
        return LinqExpr.ArrayIndex(x, LinqExpr.Constant(varIdx));
      } else if (expr[i] == expr.Grammar.One) { return LinqExpr.Constant(1.0); } 
      else if (expr[i] == expr.Grammar.Plus) { return LinqExpr.Add(children[0], children[1]); }    
      else if (expr[i] == expr.Grammar.Neg) { return LinqExpr.Negate(children[0]); }
      else if (expr[i] == expr.Grammar.Times) { return LinqExpr.Multiply(children[0], children[1]); } 
      else if (expr[i] == expr.Grammar.Inv) { return LinqExpr.Divide(LinqExpr.Constant(1.0), children[0]); }
      else if (expr[i] == expr.Grammar.Abs) { return LinqExpr.Call(abs, children[0]); }
      else if (expr[i] == expr.Grammar.Cos) { return LinqExpr.Call(cos, children[0]); }
      else if (expr[i] == expr.Grammar.Exp) { return LinqExpr.Call(exp, children[0]); }
      else if (expr[i] == expr.Grammar.Log) { return LinqExpr.Call(log, children[0]); }
      else if (expr[i] == expr.Grammar.Sqrt) { return LinqExpr.Call(sqrt, children[0]); } 
      else if (expr[i] == expr.Grammar.Pow) { return LinqExpr.Call(pow, children[0], children[1]); } 
      else throw new NotSupportedException("unsupported symbol");
    }

    internal static void UpdateParameters(Expression expr, double[] parameterValues) {
      var paramIdx = parameterValues.Length - 1;
      for(int i=0;i<expr.Length;i++) {
        if (expr[i] is Grammar.ParameterSymbol paramSy) {
          paramSy.Value = parameterValues[paramIdx--];
        }
      }
    }

    private static readonly MethodInfo abs = typeof(Math).GetMethod("Abs", new[] { typeof(double) });
    private static readonly MethodInfo sin = typeof(Math).GetMethod("Sin", new[] { typeof(double) });
    private static readonly MethodInfo cos = typeof(Math).GetMethod("Cos", new[] { typeof(double) });
    private static readonly MethodInfo exp = typeof(Math).GetMethod("Exp", new[] { typeof(double) });
    private static readonly MethodInfo log = typeof(Math).GetMethod("Log", new[] { typeof(double) });
    private static readonly MethodInfo tanh = typeof(Math).GetMethod("Tanh", new[] { typeof(double) });
    private static readonly MethodInfo cosh = typeof(Math).GetMethod("Cosh", new[] { typeof(double) });
    private static readonly MethodInfo sqrt = typeof(Math).GetMethod("Sqrt", new[] { typeof(double) });
    private static readonly MethodInfo pow = typeof(Math).GetMethod("Pow", new[] { typeof(double), typeof(double) });
    private static readonly MethodInfo sign = typeof(Math).GetMethod("Sign", new[] { typeof(double) }); // for deriv abs(x)
  }
}
