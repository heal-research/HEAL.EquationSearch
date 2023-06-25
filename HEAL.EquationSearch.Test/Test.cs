using System.Diagnostics;
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

      var noiseSigma = noiseRange / Math.Sqrt(12);
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
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new[] { "x" }; // x = z+1
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


    [TestMethod]
    public void RARApproximateLikelihood() {
      // use EQS to find RAR model (using approximate likelihood and default evaluator (VarPro))

      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry

      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.NoiseSigma = "stot";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      var alg = new Algorithm();
      var evaluator = new VarProEvaluator();
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, evaluator: evaluator, maxLength: options.MaxLength, randSeed: options.Seed);

      System.Console.WriteLine($"Best expression: {alg.BestExpression.ToInfixString()}");
    }

    [TestMethod]
    public void RAR() {
      // use EQS to find RAR model (using the likelihood from the RAR paper)

      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry

      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "gbar" };
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
      // top expressions from RAR paper
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.MaxLength = 30;
      options.Seed = 1234;
      string[] inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out var errorX, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      var e_log_gobs = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 0]).ToArray();
      var e_log_gbar = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 1]).ToArray();
      var likelihood = new RARLikelihood(x, y, modelExpr: null, e_log_gobs, e_log_gbar);


      {
        // RAR IF: gbar/(1 − exp(−√(gbar/g0))) with g0 = 1.127
        // re-parameterized RAR IF: x / (1 - exp(x/p0))
        likelihood.ModelExpr = (p, x) => x[0] / (1.0 - Math.Exp(Math.Sqrt(x[0]) / p[0])); // using x / p[0]  instead of x * p[0] to reduce distinct symbols?
        var theta = new double[] { -Math.Sqrt(1.127) };
        Assert.AreEqual(-1212.77, likelihood.NegLogLikelihood(theta), 1e-1);
        var dl = ModelSelection.DL(theta, likelihood);

        Assert.AreEqual(-1191.3, dl, 1);  // reference result -1192.7 but I count log(2) (=0.7) nats extra for the constant sign
      }

      {
        // simple IF: gbar/2 + sqrt(gbar^2 / 4 + gbar g0)
        // re-parameterized simple IF: p0 ( x + sqrt(x (x + p1))
        likelihood.ModelExpr = (p, x) => (x[0] + Math.Sqrt(x[0] * (x[0] + p[0]))) / 2.0;
        var theta = new double[] { 4 * 1.11 };
        Assert.AreEqual(-1217.3, likelihood.NegLogLikelihood(theta), 1e-1);
        var dl = ModelSelection.DL(theta, likelihood);

        Assert.AreEqual(-1194.05, dl, 1);   // reference result -1194.8 but I count log(2) (=0.7) nats extra for the constant sign
      }

      {
        likelihood.ModelExpr = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]);
        var nlr = new NonlinearRegression.NonlinearRegression();
        var theta = new double[] { 0.84, -0.02, 0.38 };
        nlr.Fit(theta, likelihood); // fit parameters
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(-1246.92, dl, 1e-1); // reference result: 1250.6, which omits abs() and has DL(func) Math.Log(5)*9 = 14.5
                                              // we use DL(func) = Math.Log(6)*10 = 17.9 (+3.4 nats more)
      }
    }

    [TestMethod]
    public void RARParamOptRARLikelihood() {
      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.NoiseSigma = "stot";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out var errorX, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      var e_log_gobs = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 0]).ToArray();
      var e_log_gbar = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 1]).ToArray();
      var likelihood = new RARLikelihood(x, y, modelExpr: null, e_log_gobs, e_log_gbar);

      int REPS = 30;
      var rand = new Random(1234);
      // fit with the full RAR likelihood
      // best expression from RAR
      likelihood.ModelExpr = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]); // model 1
      // likelihood.ModelExpr = (p, x) => Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0];   // model 7
      // likelihood.ModelExpr = (p, x) => p[0] + p[1] * x[0] + p[2] * (Math.Pow(Math.Abs(p[3] + x[0]), p[4])); // GE version of model 1
      var nlr = new NonlinearRegression.NonlinearRegression();
      var nll = new List<double>();
      int nFails = 0;
      for (int iter = 0; iter < REPS; iter++) {
        // var theta = new double[] { 0.84, -0.02, 0.38 }; // parameter estimate from RAR
        var theta = Enumerable.Range(0, 3).Select(_ => rand.NextDouble() * 10 - 5).ToArray(); // ~unif(-5, 5)
        var sw = new Stopwatch();
        sw.Start();
        nlr.Fit(theta, likelihood, epsF: 0.0);
        sw.Stop();
        if (nlr.ParamEst != null) {
          try {
            System.Console.WriteLine($"{nlr.NegLogLikelihood:f2} " +
              $"DL: {ModelSelection.DL(nlr.ParamEst, likelihood)} " +
              // $"MDL with snap: {ModelSelection.DLLatticeWithIntegerSnap(nlr.ParamEst, likelihood)} " +
              // $"MDL (lattice): {ModelSelection.MDLLattice(nlr.ParamEst, likelihood)} " +
              // $"Evidence: {ModelSelection.Evidence(nlr.ParamEst, likelihood)} " +
              $"iters: {nlr.OptReport.Iterations} " +
              $"time: {sw.ElapsedMilliseconds} " +
              $"fEvals: {nlr.OptReport.NumFuncEvals} " +
              $"param: {string.Join(" ", nlr.ParamEst.Select(pi => pi.ToString("g4")))} "
              // $"diagH: {string.Join(" ", nlr.LaplaceApproximation.diagH.Select(di => di.ToString("g4")))} "
              // $"grad: {string.Join(" ", grad.Select(pi => pi.ToString("g4")))} "
              );
            nll.Add(nlr.NegLogLikelihood);

          } catch {
            System.Console.WriteLine("fail");
            nFails++;
          }
        } else {
          System.Console.WriteLine("fail");
          nFails++;
        }


      }
      var bestNll = nll.Min();
      var threshold = nll.Min() - 0.001 * nll.Min();
      System.Console.WriteLine($"failure rate: {nFails / (double)REPS:f2} best nll: {bestNll:f2}, success rate (within 0.1% of best) : {nll.Count(nlli => nlli < threshold) / (double)nll.Count:f2}");

    }

    [TestMethod]
    public void RARParamOptRARApproximateLikelihood() {
      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      // Uses approximate likelihood (Gaussian)
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.NoiseSigma = "stot";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;
      string[] inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);

      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out var errorX, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _, out var _);
      var e_log_gobs = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 0]).ToArray();
      var e_log_gbar = Enumerable.Range(0, errorX.GetLength(0)).Select(i => errorX[i, 1]).ToArray();

      var likelihood = new GaussianLikelihood(x, y, modelExpr: null, noiseSigma.Select(s => 1.0 / s).ToArray());

      var rand = new Random(1234);
      int REPS = 30;
      {
        // best expression from RAR
        likelihood.ModelExpr = (p, x) => Math.Log(p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0])) / Math.Log(10); // model 1
        // likelihood.ModelExpr = (p, x) => Math.Log(Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]) / Math.Log(10);   // model 7
        // likelihood.ModelExpr = (p, x) => Math.Log(p[0] + p[1] * x[0] + p[2] * (Math.Pow(Math.Abs(p[3] + x[0]), p[4]))) / Math.Log(10); // GE version of model 1


        var nlr = new NonlinearRegression.NonlinearRegression();
        int nFails = 0;
        var nll = new List<double>();
        for (int iter = 0; iter < REPS; iter++) {
          // var theta = new double[] { 0.84, -0.02, 0.38 }; // parameter estimate from RAR
          var theta = Enumerable.Range(0, 3).Select(_ => rand.NextDouble() * 10 - 5).ToArray(); // ~unif(-5, 5)

          nlr.Fit(theta, likelihood);
          if (nlr.ParamEst != null && !nlr.ParamEst.Any(double.IsNaN)) {
            nll.Add(nlr.NegLogLikelihood);
            System.Console.WriteLine($"{nlr.NegLogLikelihood:f2} " +
              $"DL: {ModelSelection.DL(nlr.ParamEst, likelihood)} " +
              // $"MDL: {ModelSelection.DLLattice(nlr.ParamEst, likelihood)} " +
              // $"MDL with snap: {ModelSelection.DLLatticeWithIntegerSnap(nlr.ParamEst, likelihood)} " +
              $"iters: {nlr.OptReport.Iterations} " +
              $"fEvals: {nlr.OptReport.NumFuncEvals} " +
              $"param: {string.Join(" ", nlr.ParamEst.Select(pi => pi.ToString("g4")))} "
              // $"diagH: {string.Join(" ", nlr.LaplaceApproximation.diagH.Select(di => di.ToString("g4")))} "
              );

          } else {
            System.Console.WriteLine("fail");
            nFails++;
          }
        }
        var bestNll = nll.Min();
        var threshold = nll.Min() - 0.001 * nll.Min();
        System.Console.WriteLine($"failure rate: {nFails / (double)REPS:f2} best nll: {bestNll:f2}, success rate (within 0.1% of best) : {nll.Count(nlli => nlli < threshold) / (double)nll.Count:f2}");


      }
    }
  }
}