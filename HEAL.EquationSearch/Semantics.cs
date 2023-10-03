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

      // replace all NT with placeholder variables
      var placeholderVars = expr.Grammar.Nonterminals.Select(ntSy => new Grammar.VariableSymbol(ntSy.Name)).ToArray();
      var syString = expr.SymbolString;
      var ntIdx = expr.Grammar.FirstIndexOfNT(syString);
      while(ntIdx >= 0) {
        syString = expr.Grammar.Replace(syString, ntIdx, new[] { placeholderVars.Single(v => v.VariableName == syString[ntIdx].Name) });
        ntIdx = expr.Grammar.FirstIndexOfNT(syString);
      }
      expr = new Expression(expr.Grammar, syString);


      var variableNames = expr.Grammar.Variables.Select(v => v.VariableName)
        .Concat(placeholderVars.Select(v => v.VariableName))
        .ToArray();

      var tree = HEALExpressionBridge.ConvertToExpressionTree(expr, variableNames, out var parameterValues);
      tree = Expr.SimplifyRepeated(tree, parameterValues, out var treeParamValues);
      simplifiedExpr = HEALExpressionBridge.ConvertToPostfixExpression(tree, treeParamValues, expr.Grammar, variableNames);
      // 
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
