﻿using System.Diagnostics;
using System.Linq.Expressions;
using HEAL.Expressions;
using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class RARTestsForPaper {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [DataTestMethod]
    [DataRow(3, 53477.0)]
    [DataRow(5, -240.91)]
    [DataRow(7, -240.91)]
    [DataRow(9, -1182.4)]
    [DataRow(11, -1182.4)]
    [DataRow(13, -1182.4)]
    [DataRow(15, -1182.4)]
    [DataRow(17, -1182.4)]
    [DataRow(19, -1182.4)]
    [DataRow(21, -1182.4)]
    public void FullEnumerationReducedGrammarGaussianLikelihood(int maxLength, double expectedDL) {
      // use EQS to find RAR model (using approximate likelihood)

      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "stot";
      options.Seed = 1234;

      GetRARData(options, out var inputs, out var trainX, out var trainY, out var trainNoiseSigma, out _, out _);

      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var evaluator = new VarProEvaluator();
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);

      // Assert.AreEqual(expectedDL, alg.BestMDL.Value, Math.Abs(expectedDL * 1e-4));
    }


    [DataTestMethod]
    [DataRow(3, 53477.0)]
    [DataRow(5, -240.91)]
    [DataRow(7, -240.91)]
    [DataRow(9, -1182.4)]
    [DataRow(11, -1182.4)]
    [DataRow(13, -1182.4)]
    [DataRow(15, -1182.4)]
    public void FullEnumerationReducedGrammarRAR(int maxLength, double expectedDL) {
      // use EQS to find RAR model (using the likelihood from the RAR paper)
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;

      GetRARData(options, out var inputs, out var trainX, out var trainY, out _, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, noiseSigma: 1.0, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      // Assert.AreEqual(expectedDL, alg.BestMDL.Value, Math.Abs(expectedDL * 1e-4));
    }

    [DataTestMethod]
    [DataRow(3, 53477.0)]
    [DataRow(5, -240.91)]
    [DataRow(7, -240.91)]
    [DataRow(9, -1182.4)]
    [DataRow(11, -1182.4)]
    [DataRow(13, -1182.4)]
    [DataRow(15, -1182.4)]
    public void FullEnumerationReducedGrammarRARApproximation(int maxLength, double expectedDL) {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;

      GetRARData(options, out var inputs, out var trainX, out var trainY, out var sigma_tot, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new RARLikelihoodApprox(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar, sigma_tot));
      alg.Fit(trainX, trainY, noiseSigma: 1.0, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      // Assert.AreEqual(expectedDL, alg.BestMDL.Value, Math.Abs(expectedDL * 1e-4));
    }

    [DataTestMethod]
    [DataRow(3, 53477.0)]
    [DataRow(5, -240.91)]
    [DataRow(7, -240.91)]
    [DataRow(9, -1182.4)]
    public void BeamSearchReducedGrammar(int maxLength, double expectedDL) {
      // use EQS to find RAR model (using the likelihood from the RAR paper)
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;

      GetRARData(options, out var inputs, out var trainX, out var trainY, out _, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, noiseSigma: 1.0, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.Beam);
      Assert.AreEqual(expectedDL, alg.BestDescriptionLength.Value, Math.Abs(expectedDL * 1e-4));
    }

    private static void GetRARData(Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY, out double[] trainNoiseSigma, out double[] e_log_gobs, out double[] e_log_gbar) {
      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry


      // extract gbar as x
      inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out trainNoiseSigma);
      // extract error variables
      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out _, out _, out _, out _, out _, out _, out _, out var trainErrorX, out _, out _);

      e_log_gobs = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 0]).ToArray();
      e_log_gbar = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 1]).ToArray();
    }




    [TestMethod]
    public void RARExpr() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "stot";
      options.Seed = 1234;

      // check likelihood and DL calculation for top expressions from RAR paper
      GetRARData(options, out _, out var trainX, out var trainY, out var sigma_tot, out var e_log_gobs, out var e_log_gbar);
      var likelihood = new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar);


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
        // model 1 in RAR paper
        likelihood.ModelExpr = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]);
        var nlr = new NonlinearRegression.NonlinearRegression();
        var theta = new double[] { 0.84, -0.02, 0.38 };
        nlr.Fit(theta, likelihood); // fit parameters
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(-1244.66, dl, 1e-1); // reference result: 1250.6, which omits abs() and has DL(func) Math.Log(5)*9 = 14.5
                                             // we use DL(func) = Math.Log(6)*10 = 17.9 (+3.4 nats more)
        Assert.AreEqual(-1276.98, nlr.NegLogLikelihood, 1e-1); // reference result -1279.1
      }
      //  {
      //    // model 1 in RAR paper Gaussian likelihood
      //    var approxLikelihood = new GaussianLikelihood(likelihood.X, likelihood.Y.Select(Math.Log10).ToArray(), null, sigma_tot.Select(si => 1.0 / si).ToArray());
      //    approxLikelihood.ModelExpr = (p, x) => Math.Log(p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0])) / Math.Log(10);
      //    var nlr = new NonlinearRegression.NonlinearRegression();
      //    var theta = new double[] { 0.84, -0.02, 0.38 };
      //    nlr.Fit(theta, approxLikelihood); // fit parameters
      //    var dl = ModelSelection.DL(theta, approxLikelihood);
      //  }

      {
        // model 1 in RAR paper with approximate RAR likelihood
        var approxLikelihood = new RARLikelihoodApprox(likelihood.X, likelihood.Y, null, e_log_gobs, e_log_gbar, sigma_tot);
        approxLikelihood.ModelExpr = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]);
        var nlr = new NonlinearRegression.NonlinearRegression();
        var theta = new double[] { 0.84, -0.02, 0.38 };
        nlr.Fit(theta, approxLikelihood); // fit parameters
        var dl = ModelSelection.DL(theta, approxLikelihood);
        // Assert.AreEqual(-1244.66, dl, 1e-1); // reference result: 1250.6, which omits abs() and has DL(func) Math.Log(5)*9 = 14.5
        //                                      // we use DL(func) = Math.Log(6)*10 = 17.9 (+3.4 nats more)
        // Assert.AreEqual(-1276.98, nlr.NegLogLikelihood, 1e-1); // reference result -1279.1
      }

      {
        // model 7 in RAR paper
        likelihood.ModelExpr = (p, x) => Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0];
        var nlr = new NonlinearRegression.NonlinearRegression();
        var theta = new double[] { 1.87, -0.52 };
        nlr.Fit(theta, likelihood); // fit parameters
        var dl = ModelSelection.DL(theta, likelihood);
        Assert.AreEqual(-1228.5, dl, 1e-1); // reference result: DL -1228.5, Lik: -1250.6
        Assert.AreEqual(-1250.56, nlr.NegLogLikelihood, 1e-1);
      }
    }

    [TestMethod]
    public void ParamOptConvergenceWithGaussianLikelihood() {

      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      // Uses approximate likelihood (Gaussian)
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "stot";
      options.Seed = 1234;

      GetRARData(options, out _, out var trainX, out var trainY, out var trainNoiseSigma, out _, out _);

      // best expression from RAR
      Expression<Expr.ParametricFunction> model1 = (p, x) => Math.Log(p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0])) * (1.0 / Math.Log(10));
      Expression<Expr.ParametricFunction> model1a = (p, x) => Math.Log(p[0] + p[1] * x[0] + p[2] * Math.Pow(Math.Abs(p[3] + x[0]), p[4])) * (1.0 / Math.Log(10)); // GE version of model 1
      Expression<Expr.ParametricFunction> model7 = (p, x) => Math.Log(Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]) * (1.0 / Math.Log(10));

      var models = new[] { (expr: model1, d: 3), (expr: model1a, d: 5), (expr: model7, d: 2) };

      ParamOptConvergence(new GaussianLikelihood(trainX, trainY, modelExpr: null, trainNoiseSigma.Select(s => 1.0 / s).ToArray()), models, options.Seed.Value);
    }


    [TestMethod]
    public void ParamOptConvergenceWithRARLikelihood() {
      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      // Uses RAR likelihood
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;

      GetRARData(options, out _, out var trainX, out var trainY, out _, out var e_log_gobs, out var e_log_gbar);

      Expression<Expr.ParametricFunction> model0 = (p, x) => 0.84 * (Math.Pow(Math.Abs(p[0] + x[0]), p[1]) + x[0]); // version of model1 with scale fixed
      // best expression from RAR
      Expression<Expr.ParametricFunction> model1 = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]); // likelihood: -1279.1 DL: -1250.6 p: 0.84 -0.02 0.38 
      Expression<Expr.ParametricFunction> model1a = (p, x) => p[0] + p[1] * x[0] + p[2] * Math.Pow(Math.Abs(p[3] + x[0]), p[4]); // GE version of model 1
      Expression<Expr.ParametricFunction> model7 = (p, x) => Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]; // likelihood: -1250.6 DL: -1228.5 p: 1.87 -0.52 

      var models = new[] { /* (expr: model0, d: 2), */ (expr: model1, d: 3) /*,(expr: model1a, d: 5), (expr: model7, d: 2) */};
      ParamOptConvergence(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar), models, options.Seed.Value);
    }

    [TestMethod]
    public void ParamOptConvergenceWithApproxRARLikelihood() {
      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      // Uses approximate RAR likelihood (ignores partial derivatives of sigma_tot)
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "stot";
      options.Seed = 1234;

      GetRARData(options, out _, out var trainX, out var trainY, out var trainNoiseSigma, out var e_log_gobs, out var e_log_gbar);

      // best expression from RAR
      Expression<Expr.ParametricFunction> model1 = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]); // likelihood: -1279.1 DL: -1250.6 p: 0.84 -0.02 0.38 
      Expression<Expr.ParametricFunction> model1a = (p, x) => p[0] + p[1] * x[0] + p[2] * Math.Pow(Math.Abs(p[3] + x[0]), p[4]); // GE version of model 1
      Expression<Expr.ParametricFunction> model7 = (p, x) => Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]; // likelihood: -1250.6 DL: -1228.5 p: 1.87 -0.52 

      var models = new[] { (expr: model1, d: 3), (expr: model1a, d: 5), (expr: model7, d: 2) };
      ParamOptConvergence(new RARLikelihoodApprox(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar, trainNoiseSigma), models, options.Seed.Value);
    }

    public void ParamOptConvergence(LikelihoodBase likelihood, IEnumerable<(Expression<Expr.ParametricFunction> expr, int d)> models, int seed) {
      SharedRandom.SetSeed(seed);
      foreach (var tup in models) {
        var nlr = new NonlinearRegression.NonlinearRegression();
        var sw = new Stopwatch();
        sw.Start();
        likelihood.ModelExpr = tup.expr;
        System.Console.Error.WriteLine($"Total time for preparing likelihood: {sw.ElapsedMilliseconds}ms");
        var restartPolicy = new RestartPolicy(numParam: tup.d);
        var parameterValues = restartPolicy.Next();
        int numRestarts = -1;
        do {
          likelihood = likelihood.Clone(); // reset likelihood
          likelihood.ModelExpr = tup.expr;
          numRestarts++;
          if (!double.IsNaN(likelihood.NegLogLikelihood(parameterValues))) {
            nlr.Fit(parameterValues, likelihood, maxIterations: 5000, epsF: 1e-3); // as in https://github.com/DeaglanBartlett/ESR/blob/main/esr/fitting/test_all.py
                                                                       // successful?
            if (nlr.ParamEst != null && !nlr.ParamEst.Any(double.IsNaN)) {
              System.Console.Error.WriteLine($"{nlr.NegLogLikelihood} {nlr.OptReport} evals/s {nlr.OptReport.NumFuncEvals / nlr.OptReport.Runtime.TotalSeconds}");

              if (nlr.LaplaceApproximation == null) {
                System.Console.Error.WriteLine("Hessian not positive semidefinite after fitting");
                continue;
              }
              restartPolicy.Update(nlr.ParamEst, loss: nlr.NegLogLikelihood);
            }
          }

          parameterValues = restartPolicy.Next();
        } while (parameterValues != null);


        System.Console.WriteLine($"{tup.expr} Restarts: {numRestarts} valid parameters: {restartPolicy.Iterations} number of best: {restartPolicy.NumBest} ({100 * restartPolicy.NumBest / (double)restartPolicy.Iterations:f1}%) " +
          $"Best loss: {restartPolicy.BestLoss:f2} " +
          $"Best params: {(restartPolicy.BestParameters != null ? string.Join(" ", restartPolicy.BestParameters.Select(pi => pi.ToString("e4"))) : "")}");
      }
    }
  }
}