using System.Linq.Expressions;
using System.Reflection;
using HEAL.Expressions;
using HEAL.NonlinearRegression;
using Microsoft.VisualStudio.TestPlatform.TestHost;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class Test {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    [DataTestMethod]
    // multiple max lengths to see scaling
    [DataRow(10)]
    [DataRow(12)]
    [DataRow(14)]
    [DataRow(16)]
    [DataRow(18)]
    [DataRow(20)]
    [DataRow(25)]
    [DataRow(30)]
    public void Poly10DefaultGrammar(int maxLength) {
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

      var noiseSigma = noiseRange  / Math.Sqrt(12);
      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: maxLength, depthLimit: int.MaxValue);
    }

    [DataTestMethod]
    [DataRow(10)]
    [DataRow(15)]
    [DataRow(20)]
    [DataRow(25)]
    [DataRow(30)]
    [DataRow(50)]
    public void Poly10PolynomialGrammar(int maxLength) {
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
      g.UsePolynomialRules();
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, grammar: g, maxLength: maxLength, depthLimit: int.MaxValue);
    }

    [TestMethod]
    public void Log() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 1.0 * Math.Log(2 * x[i, 0] + 2) +
               2.0 * Math.Log(2 * x[i, 1] + 2);
      }

      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma: 1e-8, varNames, CancellationToken.None, maxLength: 30); // use a small sigma (we actually have zero noise)
    }


    [TestMethod]
    public void Cos() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2, -2, 2);
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 1.0 * Math.Cos(2 * x[i, 0] + 2) +
               2.0 * Math.Cos(2 * x[i, 1] + 2);
      }
      

      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma: 1e-8, varNames, CancellationToken.None, maxLength: 40); // use a small sigma (we actually have zero noise)
    }

    [TestMethod]
    public void Linear() {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2, -2, 2);
      var y = new double[100];
      for (int i = 0; i < y.Length; i++) {
        y[i] = 2.0 * x[i, 0] + 3.0 * x[i, 1] + 4;
      }


      var varNames = new string[] { "x1", "x2" };

      var alg = new Algorithm();
      alg.Fit(x, y, noiseSigma: 1e-8, varNames, CancellationToken.None, maxLength: 20); // use a small sigma (we actually have zero noise)
    }

    [TestMethod]
    public void LinearWeighted() {
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
      alg.Fit(x, y, noiseSigma, varNames, CancellationToken.None, maxLength: 20); // use a small sigma (we actually have zero noise)
    }

    [TestMethod]
    public void CosmicChronometerX() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "CC_Hubble.csv";
      options.Target = "H";
      options.TrainingRange = "0:31";
      options.NoiseSigma = "H_err";
      options.MaxLength = 30;
      options.Seed = 1234;
      string[] inputs = new [] { "x" }; // x = z+1
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var _, out var _, out var _, out var _, out var _, out var _, out var _);

      var likelihood = new CCLikelihood(x, y, modelExpr: null, noiseSigma.Select(s => 1.0 / s).ToArray());
      var evaluator = new AutoDiffEvaluator(likelihood);
      var alg = new Algorithm();
      var grammar = new Grammar(inputs);
      alg.Fit(x, y, noiseSigma, inputs, CancellationToken.None, evaluator: evaluator, grammar: grammar);
    }

    [TestMethod]
    public void CosmicChronometerExpr() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "CC_Hubble.csv";
      options.Target = "H";
      options.TrainingRange = "0:31";
      options.NoiseSigma = "H_err";
      options.MaxLength = 30;
      options.Seed = 1234;
      string[] inputs = new[] { "x" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var _, out var _, out var _, out var _, out var _, out var _, out var _);

      var likelihood = new CCLikelihood(x, y, modelExpr: null, noiseSigma.Select(s => 1.0 / s).ToArray());
      var evaluator = new AutoDiffEvaluator(likelihood);

      {
        likelihood.ModelExpr = (p, x) => p[0] * x[0] * x[0];
        var theta = new double[] { 3883.44 };
        var mdl = ModelSelection.MDL(theta, likelihood);
        Assert.AreEqual(16.39, mdl, 1e-2);
      }
      {        
        likelihood.ModelExpr = (p, x) => Math.Pow(p[0], Math.Pow(x[0], p[1]));
        var theta = new double[] { 3982.43, 0.22 };
        var mdl = ModelSelection.MDL(theta, likelihood);
        Assert.AreEqual(18.72, mdl, 1e-2); // does not match exactly
      }
    }

    [TestMethod]
    public void RAR() {
      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry (Slack)

      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out var errorX, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      var e_log_gobs = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 0]).ToArray();
      var e_log_gbar = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 1]).ToArray();
      var alg = new Algorithm();
      var evaluator = new AutoDiffEvaluator(new RARLikelihood(x, y, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, evaluator: evaluator, maxLength: options.MaxLength, randSeed: options.Seed);

      System.Console.WriteLine($"Best expression: {alg.BestExpression.ToInfixString()}");
    }

    [TestMethod]
    public void RARExpr() {
      // test a problematic expression
      Expression<Expressions.Expr.ParametricFunction> expr =(p, x) => (p[0] + (Math.Sqrt(Math.Abs((1 + ((x[0] * x[0]) * p[1])))) * p[2]));
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out var errorX, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      var e_log_gobs = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 0]).ToArray();
      var e_log_gbar = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 1]).ToArray();
      
      var likelihood = new RARLikelihood(x, y, modelExpr: expr, e_log_gobs, e_log_gbar);

      var nlr = new NonlinearRegression.NonlinearRegression();
      var theta = new double[] { 0.0, 1.0, 1.0 };
      nlr.Fit(theta, likelihood);
    }
  }
}