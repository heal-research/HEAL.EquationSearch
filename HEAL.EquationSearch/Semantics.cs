using HEAL.Expressions;

namespace HEAL.EquationSearch {
  public class Semantics {
    public static int[] GetLengths(Expression expr) {
      var lengths = new int[expr.Length]; // length of each part
      for (int i = 0; i < expr.Length; i++) {
        var c = i - 1; // first child index
        lengths[i] = 1;
        for (int cIdx = 0; cIdx < expr[i].Arity; cIdx++) {
          lengths[i] += lengths[c];
          c = c - lengths[c];
        }
      }
      return lengths;
    }


    // TODO: refactoring to use either use int hash values or implement custom hash function for expressions

    internal static ulong GetHashValue(Expression expr, out Expression simplifiedExpr) {
      // System.IO.File.AppendAllLines(@"c:\temp\log.txt", new[] { $"{expr.ToInfixString()}" });
      var variableNames = expr.Grammar.Variables.Select(v => v.VariableName).ToArray();
      var tree = HEALExpressionBridge.ConvertToExpressionTree(expr, variableNames, out var parameterValues);
      tree = Expr.SimplifyRepeated(tree, parameterValues, out var treeParamValues);
      simplifiedExpr = HEALExpressionBridge.ConvertToPostfixExpression(tree, treeParamValues, expr.Grammar);
      
      return (ulong)simplifiedExpr.ToInfixString(includeParamValues: false).GetHashCode();
    }

    // public static string GetDebugView(System.Linq.Expressions.Expression expr) {
    //   if (expr == null)
    //     return null;
    // 
    //   var propertyInfo = typeof(System.Linq.Expressions.Expression).GetProperty("DebugView", BindingFlags.Instance | BindingFlags.NonPublic);
    //   return propertyInfo.GetValue(expr) as string;
    // }
  }
}
