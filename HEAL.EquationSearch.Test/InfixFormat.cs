using TreesearchLib;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class InfixFormat {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [TestMethod]
    public void Division() {
      var g = new Grammar(new[] { "x" });
      g.UseLogExpPowRestrictedRules();
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Parameter.Clone(), g.Variables[0], g.Plus, g.Inv, g.Times });
      Assert.AreEqual("(1 / (x + 0)) * 0", expr.ToInfixString());
    }

    [TestMethod]
    public void Subtraction() {
      var g = new Grammar(new[] { "x" });
      g.UseLogExpPowRestrictedRules();
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Neg, g.Variables[0], g.Plus});
      Assert.AreEqual("x + (- 0)", expr.ToInfixString());
    }

    [TestMethod]
    public void Power() {
      var g = new Grammar(new[] { "x" });
      g.UseLogExpPowRestrictedRules();
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Parameter.Clone(), g.Parameter.Clone(), g.Variables[0], g.Plus, g.Abs, g.Pow, g.Times });
      Assert.AreEqual("((abs( (x + 0) )) ** 0) * 0", expr.ToInfixString());
    }

  }
}