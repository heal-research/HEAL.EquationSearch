using System.Linq.Expressions;
using System.Reflection;
using HEAL.Expressions;
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
      #pragma warning disable format
      if (expr[i] is Grammar.ParameterSymbol paramSy) {
        paramValues.Add(paramSy.Value);
        return LinqExpr.ArrayIndex(p, LinqExpr.Constant(paramValues.Count - 1));
      } else if (expr[i] is Grammar.VariableSymbol varSy) {
        var varIdx = Array.IndexOf(variableNames, varSy.VariableName); // TODO: perf
        if (varIdx < 0) throw new InvalidProgramException("var name not found");
        return LinqExpr.ArrayIndex(x, LinqExpr.Constant(varIdx));
      } else if (expr[i] == expr.Grammar.Plus) { return LinqExpr.Add(children[0], children[1]); } 
        else if (expr[i] == expr.Grammar.Minus) { return LinqExpr.Subtract(children[0], children[1]); } 
        else if (expr[i] == expr.Grammar.Neg) { return LinqExpr.Negate(children[0]); } 
        else if (expr[i] == expr.Grammar.Times) { return LinqExpr.Multiply(children[0], children[1]); } 
        else if (expr[i] == expr.Grammar.Div) { return LinqExpr.Divide(children[0], children[1]); } 
        else if (expr[i] == expr.Grammar.Inv) { return LinqExpr.Divide(LinqExpr.Constant(1.0), children[0]); }
        else if (expr[i] == expr.Grammar.Abs) { return LinqExpr.Call(abs, children[0]); } 
        else if (expr[i] == expr.Grammar.Cos) { return LinqExpr.Call(cos, children[0]); } 
        else if (expr[i] == expr.Grammar.Exp) { return LinqExpr.Call(exp, children[0]); } 
        else if (expr[i] == expr.Grammar.Log) { return LinqExpr.Call(log, children[0]); } 
        else if (expr[i] == expr.Grammar.Sqrt) { return LinqExpr.Call(sqrt, children[0]); } 
        else if (expr[i] == expr.Grammar.Pow) { return LinqExpr.Call(pow, children[0], children[1]); } 
        else if (expr[i] == expr.Grammar.PowAbs) { return LinqExpr.Call(pow, LinqExpr.Call(abs, children[0]), children[1]); }
        else if (expr[i] is Grammar.ConstantSymbol constSy) { return LinqExpr.Constant(constSy.Value); } 
        else throw new NotSupportedException("unsupported symbol");
        #pragma warning restore format
    }


    // NOTE: order of parameter is reversed in expr
    internal static void UpdateParameters(Expression expr, double[] parameterValues) {
      var paramIdx = parameterValues.Length - 1;
      for (int i = 0; i < expr.Length; i++) {
        if (expr[i] is Grammar.ParameterSymbol paramSy) {
          paramSy.Value = parameterValues[paramIdx--];
        }
      }
    }



    internal static Expression ConvertToPostfixExpression(Expression<Expr.ParametricFunction> tree, double[] parameterValues, Grammar g) {
      var syList = new List<Grammar.Symbol>();
      ConvertToPostfixExpressionRec(tree.Body, tree.Parameters[0], tree.Parameters[1], parameterValues, g, syList);
      return new Expression(g, syList.ToArray());
    }

    private static void ConvertToPostfixExpressionRec(LinqExpr tree, ParameterExpression theta, ParameterExpression x, double[] parameterValues, Grammar g, List<Grammar.Symbol> syList) {
      if (tree is BinaryExpression binExpr) {
        if (binExpr.NodeType == ExpressionType.ArrayIndex) {
          var paramIdx = (int)((ConstantExpression)binExpr.Right).Value;
          if (binExpr.Left == theta) {
            syList.Add(new Grammar.ParameterSymbol(parameterValues[paramIdx]));
          } else if (binExpr.Left == x) {
            syList.Add(g.Variables.ElementAt(paramIdx));
          }
        } else {
          // right argument before left argument
          ConvertToPostfixExpressionRec(binExpr.Right, theta, x, parameterValues, g, syList);
          ConvertToPostfixExpressionRec(binExpr.Left, theta, x, parameterValues, g, syList);
          if (binExpr.NodeType == ExpressionType.Add) {
            syList.Add(g.Plus);
          } else if (binExpr.NodeType == ExpressionType.Subtract) {
            syList.Add(g.Minus);
          } else if (binExpr.NodeType == ExpressionType.Multiply) {
            syList.Add(g.Times);
          } else if (binExpr.NodeType == ExpressionType.Divide) {
            syList.Add(g.Div);
          } else throw new InvalidProgramException();
        }
      } else if (tree is UnaryExpression unaryExpr) {
        ConvertToPostfixExpressionRec(unaryExpr.Operand, theta, x, parameterValues, g, syList);
        if (unaryExpr.NodeType == ExpressionType.Negate) {
          syList.Add(g.Neg);
        } // ignore UnaryPlus
      } else if (tree is MethodCallExpression callExpr) {
        if (callExpr.Method == abs) {
          ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
          syList.Add(g.Abs);
        } else if (callExpr.Method == sin) {
          throw new NotSupportedException();
        } else if (callExpr.Method == cos) {
          ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
          syList.Add(g.Cos);
        } else if (callExpr.Method == exp) {
          ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
          syList.Add(g.Exp);
        } else if (callExpr.Method == log) {
          ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
          syList.Add(g.Log);
        } else if (callExpr.Method == tanh) {
          throw new NotSupportedException();
        } else if (callExpr.Method == cosh) {
          throw new NotSupportedException();
        } else if (callExpr.Method == sqrt) {
          ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
          syList.Add(g.Sqrt);
        } else if (callExpr.Method == pow) {
          if (callExpr.Arguments[0] is MethodCallExpression arg0Expr && arg0Expr.Method == abs) {
            // special handling of pow(abs(..), ..)
            // produce sub-expressions for arguments in reverse order
            ConvertToPostfixExpressionRec(callExpr.Arguments[1], theta, x, parameterValues, g, syList);
            ConvertToPostfixExpressionRec(arg0Expr.Arguments[0], theta, x, parameterValues, g, syList);
            syList.Add(g.PowAbs);
          } else {
            // produce sub-expressions for arguments in reverse order
            ConvertToPostfixExpressionRec(callExpr.Arguments[1], theta, x, parameterValues, g, syList);
            ConvertToPostfixExpressionRec(callExpr.Arguments[0], theta, x, parameterValues, g, syList);
            syList.Add(g.Pow);
          }
        }
      } else if (tree is ConstantExpression constExpr) {
        syList.Add(new Grammar.ConstantSymbol((double)constExpr.Value));
      } else throw new NotSupportedException();
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
  }
}
