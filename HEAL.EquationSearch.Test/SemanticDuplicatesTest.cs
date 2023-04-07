namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class SemanticDuplicatesTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    [DataTestMethod]
    [DataRow("x y +", "y x +", true)]
    [DataRow("x y *", "y x *", true)]
    [DataRow("0 x * 0 y * +", "0 y * 0 x * +", true)] // 0 stands for parameters
    [DataRow("0 x * log 0 y * log +", "0 y * log 0 x * log +", true)]
    [DataRow("x y /", "y x /", false)]
    public void SemanticHashing(string exprStr1, string exprStr2, bool equal) {
      var g = new Grammar(new string[] { "x", "y" });

      var expr1 = StringToExpression(g, exprStr1);
      var expr2 = StringToExpression(g, exprStr2);
      Assert.AreEqual(equal, SemanticHash.GetHashValue(expr1) == SemanticHash.GetHashValue(expr2));
    }

    [DataTestMethod]
    // x before y in terms
    [DataRow("0 x * 0 y * +", true)] // 0 is param
    [DataRow("0 y * 0 x * +", false)]

    // x before y in factors
    [DataRow("0 x y * * 0 + ", true)]
    [DataRow("0 y x * * 0 + ", false)]

    // x x before x y in terms
    [DataRow("0 x x * * 0 x y * * 0 + +", true)]
    [DataRow("0 x y * * 0 x x * * 0 + +", false)]

    // x before log in factors
    [DataRow("0 x x log * * 0 + ", true)]
    [DataRow("0 x log x * * 0 + ", false)]

    // we must also consider the arguments of non-linear functions in factors
    [DataRow("0 x log y log * * 0 +", true)]
    [DataRow("0 y log x log * * 0 +", false)] 
    public void CanonincalOrderForCommutative(string exprStr1, bool isCanonicalOrder) {
      var g = new Grammar(new string[] { "x", "y" });

      var expr1 = StringToExpression(g, exprStr1);
      Assert.AreEqual(isCanonicalOrder, Semantics.IsCanonicForCommutative(expr1));
    }



    private Expression StringToExpression(Grammar g, string str) {
      // hacky, only to allow writing expressions as strings
      return new Expression(g, str.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(tok => g.AllSymbols.First(sy => sy.ToString() == tok)).ToArray());
    }
  }
}