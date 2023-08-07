namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class SemanticDuplicatesTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    // TODO: better readable when converted to Expressions (p, x) => p[0] * x[0]
    [DataTestMethod]
    [DataRow("2 + 3*x + 4*x", "2 + 3*x", true)] // Semantic hashing assumes that parameters are fit. The values are used only as initial values.
    [DataRow("2*x + 3*x + 4", "2*x + 3", true)]
    [DataRow("x*y*2 + 3", "y*x*2 + 3", true)]
    [DataRow("2*x + 3*y + 4", "2*y + 3*x + 4", true)]
    [DataRow("log(2*x) + log(3*y) + 4", "log(2*y) + log(3*x) + 4", true)]
    [DataRow("2*x*(1f/(2*y)) * 3 + 4", "2*y*(1f/2*x)*3 + 4", false)]
    [DataRow("2*x*(1f/(3*y))*4 + 5", "2*x*(1f/(3*y)) + 4", true)]
    [DataRow("-(2 * x) + 3", "2*x + 3", true)]
    [DataRow("-(2*x) + -(3*x) + 4", "2*x + 3", true)]
    public void SemanticHashing(string exprStr1, string exprStr2, bool equal) {
      var g = new Grammar(new string[] { "x", "y" }, maxLen: 100);

      var expr1 = StringToExpression(g, exprStr1);
      var expr2 = StringToExpression(g, exprStr2);
      Assert.AreEqual(equal, Semantics.GetHashValue(expr1) == Semantics.GetHashValue(expr2));
    }


    [DataTestMethod]
    [DataRow("x")]
    [DataRow("2 * x")]
    [DataRow("2 + x")]
    [DataRow("2 / x")]
    [DataRow("2 - x")]
    [DataRow("pow(x, 2)")]
    [DataRow("pow(abs(x), 2)")]
    [DataRow("cos(x)")]
    [DataRow("x / (x + 2)")]
    [DataRow("2 * x + 3 * y + 4")]
    public void HEALExpressionBridge(string exprStr) {
      // Check conversion of Expression (postfix) to expression trees.
      // And check if Expr -> Tree -> Expr produces the same result
      var varNames = new string[] { "x", "y" };
      var g = new Grammar(varNames, maxLen: 100);
      var expr1 = StringToExpression(g, exprStr);
      var tree = EquationSearch.HEALExpressionBridge.ConvertToExpressionTree(expr1, varNames, out var p);
      var expr2 = EquationSearch.HEALExpressionBridge.ConvertToPostfixExpression(tree, p, g);
      Assert.AreEqual(expr1.ToInfixString(), expr2.ToInfixString());
    }

    private Expression StringToExpression(Grammar g, string str) {
      var varNames = g.Variables.Select(varSy => varSy.VariableName).ToArray();
      var x = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "x");
      var p = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "p");
      var parser = new HEAL.Expressions.Parser.ExprParser(str, varNames, x, p);
      var tree = parser.Parse();
      var treeParamValues = parser.ParameterValues;
      return EquationSearch.HEALExpressionBridge.ConvertToPostfixExpression(tree, treeParamValues, g);
    }
  }
}