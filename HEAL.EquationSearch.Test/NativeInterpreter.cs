
using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class Evaluators {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [TestMethod]
    public void Division() {
      // check the order of arguments for the VarPro evaluator.
      // we have to use the same order in the Grammar
      var g = new Grammar(new[] { "x" });
      g.UseFullRules();
      
      // this is equivalent to 2 / 1
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Parameter.Clone(), g.Div });
      ((Grammar.ParameterSymbol)expr[0]).Value = 1.0;
      ((Grammar.ParameterSymbol)expr[1]).Value = 2.0;

      // create dataset of one row
      var data = new Data(new[] { "x" }, new double[1, 1], new double[1], new double[] { 1.0 });

      var eval = new HEAL.EquationSearch.VarProEvaluator();
      var res = eval.Evaluate(expr, data);
      Assert.AreEqual(2.0, res[0]);


      var eval2 = new AutoDiffEvaluator(new SimpleGaussianLikelihood(data.X, data.Target, modelExpr: null, noiseSigma: 1.0));
      res = eval2.Evaluate(expr, data);
      Assert.AreEqual(2.0, res[0]);
    }

    [TestMethod]
    public void Power() {
      // check the order of arguments for the VarPro evaluator.
      // we have to use the same order in the Grammar
      var g = new Grammar(new[] { "x" });
      g.UseFullRules();

      // this is equivalent to 3^2
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Parameter.Clone(), g.Pow });
      ((Grammar.ParameterSymbol)expr[0]).Value = 2.0;
      ((Grammar.ParameterSymbol)expr[1]).Value = 3.0;

      // create dataset of one row
      var data = new Data(new[] { "x" }, new double[1, 1], new double[1], new double[] { 1.0 });

      var eval = new HEAL.EquationSearch.VarProEvaluator();
      var res = eval.Evaluate(expr, data);
      Assert.AreEqual(9.0, res[0]);

      var eval2 = new AutoDiffEvaluator(new SimpleGaussianLikelihood(data.X, data.Target, modelExpr: null, noiseSigma: 1.0));
      res = eval2.Evaluate(expr, data);
      Assert.AreEqual(9.0, res[0]);

    }

  }
}