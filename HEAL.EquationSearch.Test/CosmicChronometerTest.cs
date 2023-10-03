using System.Diagnostics;
using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class CosmicChronometerTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }
    [TestMethod]
    public void CosmicChronometerX() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "CC_Hubble.csv";
      options.Target = "H";
      options.TrainingRange = "0:31";
      options.NoiseSigma = "H_err";
      string[] inputs = new[] { "x" }; // x = z+1
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      int maxLen = 10;

      var likelihood = new CCLikelihood(x, y, modelExpr: null, noiseSigma.Select(s => 1.0 / s).ToArray());
      var evaluator = new Evaluator(likelihood);
      var alg = new Algorithm();
      var grammar = new Grammar(inputs, maxLen);
      alg.Fit(x, y, noiseSigma, inputs, CancellationToken.None, evaluator: evaluator, grammar: grammar, maxLength: maxLen, randSeed: 1234);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
      // Assert.AreEqual(16.39, alg.BestDescriptionLength.Value, 1.0e-2);

      var bestExpr = HEALExpressionBridge.ConvertToExpressionTree(alg.BestExpression, inputs, out var bestParamValues);
      likelihood.ModelExpr = bestExpr;
      Assert.AreEqual(16.39, ModelSelection.DLWithIntegerSnap(bestParamValues, likelihood), 1e-2);
    }

    [TestMethod]
    public void CosmicChronometerExprDL() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "CC_Hubble.csv";
      options.Target = "H";
      options.TrainingRange = "0:31";
      options.NoiseSigma = "H_err";
      options.Seed = 1234;
      string[] inputs = new[] { "x" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var _, out var _, out var _, out var _, out var _, out var _, out var _);

      var likelihood = new CCLikelihood(x, y, modelExpr: null, noiseSigma.Select(s => 1.0 / s).ToArray());
      var evaluator = new Evaluator(likelihood);

      // top two expressions from ESR Paper
      {
        likelihood.ModelExpr = (p, x) => p[0] * x[0] * x[0];
        var theta = new double[] { 3883.44 };
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(16.39, dl, 1e-2);
      }
      {
        likelihood.ModelExpr = (p, x) => Math.Pow(p[0], Math.Pow(x[0], p[1]));
        var theta = new double[] { 3982.43, 0.22 };
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(18.72, dl, 1e-2);  // does not match exactly the value in the paper
      }
      // an expr found with EQS
      {
        likelihood.ModelExpr = (p, x) => p[0] * Math.Pow(x[0], 4.0) + p[1] * x[0];
        var theta = new double[] { 416.93, 4055.4 };
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(27.74, dl, 1e-2);
      }      
    }

  }
}