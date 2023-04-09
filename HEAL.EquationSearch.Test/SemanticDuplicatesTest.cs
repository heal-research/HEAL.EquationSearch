namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class SemanticDuplicatesTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    [DataTestMethod]
    [DataRow("0 0 x * 0 x * + +", "0 0 x * + ", true)] // 0 stands for parameters
    [DataRow("0 x * 0 x * + 0 +", "0 x * 0 +", true)]
    [DataRow("0 0 x * 0 y * + +", "0 0 y * 0 x * + + ", true)]
    [DataRow("0 0 x y * * + ", "0 0 y x * * + ", true)]
    [DataRow("0 0 x * 0 y * + +", "0 0 y * 0 x * + +", true)] 
    [DataRow("0 0 x * log 0 y * log + +", "0 0 y * log 0 x * log + +", true)]
    [DataRow("0 0 0 x * 0 y * / * +", "0 0 0 y * 0 x * / * +", false)]  // would need to check within division / division not commutative
    public void SemanticHashing(string exprStr1, string exprStr2, bool equal) {
      var g = new Grammar(new string[] { "x", "y" });

      var expr1 = StringToExpression(g, exprStr1);
      var expr2 = StringToExpression(g, exprStr2);
      Assert.AreEqual(equal, Semantics.GetHashValue(expr1) == Semantics.GetHashValue(expr2));
    }

    private Expression StringToExpression(Grammar g, string str) {
      // hacky, only to allow writing expressions as strings
      return new Expression(g, str.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(tok => g.AllSymbols.First(sy => sy.ToString() == tok)).ToArray());
      
    }
  }
}