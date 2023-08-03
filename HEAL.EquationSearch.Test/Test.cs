using HEAL.NonlinearRegression;
using static alglib;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class Test {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    [DataTestMethod]
    // multiple max lengths to analyse runtime growth
    [DataRow(10, 285270.88)]
    [DataRow(12, 277235.58)]
    [DataRow(14, 183420.72)]
    [DataRow(16, 180863.4)]
    [DataRow(18, null)] // expected DL not known yet
    [DataRow(20, null)] // expected DL not known yet
    [DataRow(25, null)] // expected DL not known yet
    [DataRow(30, null)] // expected DL not known yet
    public void BeamSearchDefaultGrammarPoly10(int maxLength, double? expectedDl) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 10, -1, 1);
      var y = new double[100];
      var noiseRange = 0.02;
      for (int i = 0; i < y.Length; i++) {
        y[i] = x[i, 0] * x[i, 1]
          + x[i, 2] * x[i, 3]
          + x[i, 4] * x[i, 5]
          + x[i, 0] * x[i, 6] * x[i, 8]
          + x[i, 2] * x[i, 5] * x[i, 9]
                    + rand.NextDouble() * noiseRange - noiseRange / 2;
        ;
      }

      var noiseSigma = noiseRange / Math.Sqrt(12);
      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: maxLength, randSeed: 1234);
      if (expectedDl != null) Assert.AreEqual(expectedDl.Value, alg.BestDescriptionLength.Value, 1e-1);
    }

    [DataTestMethod]
    [DataRow(10, 2.8948e+05)]
    [DataRow(15, 1.8342e+05)]
    [DataRow(20, 69994)]
    [DataRow(25, 61664)]
    [DataRow(30, 29770)]
    [DataRow(50, -266.75)]
    public void BeamSearchPolynomialGrammarPoly10(int maxLength, double minDL) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 10, -1, 1);
      var y = new double[100];
      var noiseRange = 0.02;
      for (int i = 0; i < y.Length; i++) {
        y[i] = x[i, 0] * x[i, 1]
          + x[i, 2] * x[i, 3]
          + x[i, 4] * x[i, 5]
          + x[i, 0] * x[i, 6] * x[i, 8]
          + x[i, 2] * x[i, 5] * x[i, 9]
          + rand.NextDouble() * noiseRange - noiseRange / 2;
        ;
      }
      var noiseSigma = noiseRange / Math.Sqrt(12);

      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      var g = new Grammar(varNames);
      g.UsePolynomialRestrictedRules();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, grammar: g, maxLength: maxLength, randSeed: 1234);
      Assert.AreEqual(minDL, alg.BestDescriptionLength.Value, Math.Abs(minDL * 1e-4));
    }

    [TestMethod]
    public void BestExprDLPoly10() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 10, -1, 1);
      var y = new double[100];
      var noiseRange = 0.02;
      for (int i = 0; i < y.Length; i++) {
        y[i] = x[i, 0] * x[i, 1]
          + x[i, 2] * x[i, 3]
          + x[i, 4] * x[i, 5]
          + x[i, 0] * x[i, 6] * x[i, 8]
          + x[i, 2] * x[i, 5] * x[i, 9]
          + rand.NextDouble() * noiseRange - noiseRange / 2;
        ;
      }
      var noiseSigma = noiseRange / Math.Sqrt(12);

      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      var likelihood = new SimpleGaussianLikelihood(x, y,
        (p, x) => p[5] + x[0] * x[1] * p[0] + x[2] * x[3] * p[1] + x[4] * x[5] * p[2] + x[0] * x[6] * x[8] * p[3] + x[2] * x[5] * x[9] * p[4], noiseSigma);
      var nlr = new HEAL.NonlinearRegression.NonlinearRegression();
      var param = new double[] { 1, 1, 1, 1, 1, 0 };
      nlr.Fit(param, likelihood);
      Assert.AreEqual(-381.81, nlr.NegLogLikelihood, 1e-2);
      Assert.AreEqual(-266.75, ModelSelection.DL(nlr.ParamEst, likelihood), 1e-2);
    }

    [DataTestMethod]
    [DataRow(10, 2.8948e+05)]
    [DataRow(15, 1.8342e+05)]
    // search space too large
    // [DataRow(20, 69994)]
    // [DataRow(25, 61664)]
    // [DataRow(30, 29770)]
    // [DataRow(50, -310.52)]
    public void FullEnumerationPolyGrammarPoly10(int maxLength, double minDL) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 10, -1, 1);
      var y = new double[100];
      var noiseRange = 0.02;
      for (int i = 0; i < y.Length; i++) {
        y[i] = x[i, 0] * x[i, 1]
          + x[i, 2] * x[i, 3]
          + x[i, 4] * x[i, 5]
          + x[i, 0] * x[i, 6] * x[i, 8]
          + x[i, 2] * x[i, 5] * x[i, 9]
          + rand.NextDouble() * noiseRange - noiseRange / 2;
        ;
      }
      var noiseSigma = noiseRange / Math.Sqrt(12);

      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      var g = new Grammar(varNames);
      g.UsePolynomialRestrictedRules();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, grammar: g, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      Assert.AreEqual(minDL, alg.BestDescriptionLength.Value, Math.Abs(minDL * 1e-4));
    }

    [TestMethod]
    public void BeamSearchLog() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 1.0 * Math.Log(2 * x[i, 0] + 2) +
               2.0 * Math.Log(2 * x[i, 1] + 2);
      }

      double noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)
      // get nll and DL for generating expression (in restricted grammar form)
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] + p[1] * Math.Log(Math.Abs(x[0] + p[2])) + p[3] * Math.Log(Math.Abs(x[1] + p[4])), noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 3 * Math.Log(2), 1.0, 1.0, 2.0, 1.0 }); // factor 2 extracted out of log into offset
      var bestDL = ModelSelection.DL(new double[] { 2 * Math.Log(2), 1.0, 1.0, 2.0, 1.0 }, likelihood);
      System.Console.WriteLine($"Generating expression negLogLik: {bestNll} DL {bestDL}");

      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 20, randSeed: 1234);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
      Assert.AreEqual(bestDL, alg.BestDescriptionLength.Value, 1e-1);
    }


    [TestMethod]
    public void BeamSearchPower() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * Math.Pow(x[i, 0], -1.5);
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)

      // get nll and DL for generating expression (in restricted grammar form)
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] + p[1] * Math.Pow(Math.Abs(x[0] + p[2]), p[3]), noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 0.0, 2.0, 0.0, -1.5 });
      var bestDL = ModelSelection.DL(new double[] { 0.0, 2.0, 0.0, -1.5 }, likelihood);
      System.Console.WriteLine($"Generating expression negLogLik: {bestNll} DL {bestDL}");

      var varNames = new string[] { "x1" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 15, randSeed: 1234);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
      Assert.AreEqual(bestDL, alg.BestDescriptionLength.Value, 1e-1);
    }

    [TestMethod]
    public void FullEnumerationVarProPower() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * Math.Pow(x[i, 0], -1.5);
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)

      // get nll and DL for generating expression
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0]), p[1]), noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 2.0, -1.5 });
      var bestDL = ModelSelection.DL(new double[] { 2.0, -1.5 }, likelihood);
      System.Console.WriteLine($"Generating expression negLogLik: {bestNll} DL {bestDL}");

      var varNames = new string[] { "x1" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 15, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);

      var bestExpr = HEALExpressionBridge.ConvertToExpressionTree(alg.BestExpression, varNames, out var bestParamValues);
      likelihood.ModelExpr = bestExpr;
      Assert.AreEqual(bestDL, ModelSelection.DLWithIntegerSnap(bestParamValues, likelihood), 1e-1);
    }

    [TestMethod]
    public void FullEnumerationNlrPower() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * Math.Pow(x[i, 0], -1.5);
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)

      // get nll and DL for generating expression
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0]), p[1]), noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 2.0, -1.5 });
      var bestDL = ModelSelection.DL(new double[] { 2.0, -1.5 }, likelihood);
      System.Console.WriteLine($"Generating expression negLogLik: {bestNll} DL {bestDL}");

      var varNames = new string[] { "x1" };

      var alg = new Algorithm();
      var evaluator = new Evaluator(likelihood);
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, evaluator: evaluator, maxLength: 15, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());

      var bestExpr = HEALExpressionBridge.ConvertToExpressionTree(alg.BestExpression, varNames, out var bestParamValues);
      likelihood.ModelExpr = bestExpr;
      Assert.AreEqual(bestDL, ModelSelection.DLWithIntegerSnap(bestParamValues, likelihood), 1e-1);
    }

    [TestMethod]
    public void ParameterOptConvergenceNlrPower() {
      // check if the correct parameters can be found if the (generalized) structure is known 

      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * Math.Pow(x[i, 0], -1.5);
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)

      // get nll and DL for generating expression
      // var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0]), p[1]), noiseSigma);
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0] + p[1]), p[2]), noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 2.0, 0.0, -1.5 });

      var varNames = new string[] { "x1" };

      var nlr = new NonlinearRegression.NonlinearRegression();
      var restartPolicy = new RestartPolicy(3);
      // var p = new double[] { 2.0, -1.5 };
      var p = restartPolicy.Next();
      do {
        nlr.Fit(p, likelihood);
        if (nlr.ParamEst != null) {
          restartPolicy.Update(nlr.ParamEst, nlr.NegLogLikelihood);
        }
        p = restartPolicy.Next();
      } while (p != null);
      Assert.AreEqual(bestNll, restartPolicy.BestLoss, 1e-3);
    }

    [TestMethod]
    public void ParameterOptConvergenceVarProPower() {
      // check if the correct parameters can be found if the (generalized) structure is known 

      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * Math.Pow(x[i, 0], -1.5);
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)

      var varNames = new string[] { "x1" };

      var varProEval = new VarProEvaluator();
      var data = new Data(varNames, x, y, y.Select(_ => 1.0 / noiseSigma ).ToArray());
      var g = new Grammar(varNames);
      g.UseLogExpPowRestrictedRules();
      var x0 = g.Variables.Single();
      var expr = new Expression(g, new[] { g.Parameter.Clone(), g.Parameter.Clone(), g.Parameter.Clone(), x0, g.Plus, g.Abs, g.Pow, g.Times, g.Parameter.Clone(), g.Plus });
      var dl = varProEval.OptimizeAndEvaluateDL(expr, data);

      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0] + p[1]), p[2]) + p[3], noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 2.0, 0.0, -1.5, 0.0 });
      var bestDl = ModelSelection.DL(new double[] { 2.0, 0.0, -1.5, 0.0 }, likelihood);
      Assert.AreEqual(bestDl, dl, 1e-2);
    }

    [TestMethod]
    public void BeamSearchInverse() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1, 1, 5); // unif(1, 5)
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 / x[i, 0];
      }

      var noiseSigma = 1e-8; // use a small sigma (we actually have zero noise)
      // get nll and DL for generating expression (in restricted grammar form)
      var likelihood = new SimpleGaussianLikelihood(x, y, (p, x) => p[0] * Math.Pow(Math.Abs(x[0] + p[1]), p[2]) + p[3], noiseSigma);
      var bestNll = likelihood.NegLogLikelihood(new double[] { 2.0, 0.0, -1, 0.0 });
      var bestDL = ModelSelection.DL(new double[] { 2.0, 0.0, -1, 0.0 }, likelihood);
      System.Console.WriteLine($"Generating expression negLogLik: {bestNll} DL {bestDL}");

      var varNames = new string[] { "x1" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 15, randSeed: 1234);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
      Assert.AreEqual(bestDL, alg.BestDescriptionLength.Value, 1e-1);
    }

    [TestMethod]
    public void BeamSearchLinear() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2, -2, 2);
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * x[i, 0] + 3.0 * x[i, 1] + 4;
      }


      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma: 1e-8, varNames, CancellationToken.None, maxLength: 15, randSeed: 1234); // use a small sigma (we actually have zero noise)
      Assert.AreEqual(-1671.79, alg.BestDescriptionLength.Value, 1e-1);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
    }

    [TestMethod]
    public void BeamSearchLinearWithNoise() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2, -2, 2);
      var y = new double[100];
      var noiseRange = 0.4;
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * x[i, 0] + 3.0 * x[i, 1] + 4
          + rand.NextDouble() * noiseRange - noiseRange / 2;

      }


      var noiseSigma = noiseRange / Math.Sqrt(12);

      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 15, randSeed: 1234);
      Assert.AreEqual(-43.124, alg.BestDescriptionLength.Value, 1e-2);
      System.Console.WriteLine(alg.BestExpression.ToInfixString());
    }
  }
}