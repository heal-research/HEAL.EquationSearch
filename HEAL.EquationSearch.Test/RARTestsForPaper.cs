using System.Diagnostics;
using System.Linq.Expressions;
using CommandLine;
using HEAL.Expressions;
using HEAL.NonlinearRegression;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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
    public void FullEnumerationReducedGrammar(int maxLength, double expectedDL) {
      // use EQS to find RAR model (using the likelihood from the RAR paper)

      GetRARData(out var inputs, out var trainX, out var trainY, out var trainNoiseSigma, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputs);
      grammar.UseFullRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      Assert.AreEqual(expectedDL, alg.BestMDL.Value, Math.Abs(expectedDL * 1e-4));
    }

    [DataTestMethod]
    [DataRow(3, 53477.0)]
    [DataRow(5, -240.91)]
    [DataRow(7, -240.91)]
    [DataRow(9, -1182.4)]
    public void BeamSearchReducedGrammar(int maxLength, double expectedDL) {
      // use EQS to find RAR model (using the likelihood from the RAR paper)

      GetRARData(out var inputs, out var trainX, out var trainY, out var trainNoiseSigma, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputs);
      grammar.UseFullRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.Beam);
      Assert.AreEqual(expectedDL, alg.BestMDL.Value, Math.Abs(expectedDL * 1e-4));
    }

    private static void GetRARData(out string[] inputs, out double[,] trainX, out double[] trainY, out double[] trainNoiseSigma, out double[] e_log_gobs, out double[] e_log_gbar) {
      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry

      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.MaxLength = 20;
      options.Seed = 1234;

      // extract gbar as x
      inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out trainNoiseSigma);
      // extract error variables
      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out _, out _, out _, out _, out _, out _, out _, out var trainErrorX, out _, out _);

      e_log_gobs = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 0]).ToArray();
      e_log_gbar = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 1]).ToArray();
    }


    /*

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
      var evaluator = new Evaluator(new RARLikelihood(x, y, modelExpr: null, e_log_gobs, e_log_gbar));
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, evaluator: evaluator, maxLength: options.MaxLength, randSeed: options.Seed);

      System.Console.WriteLine($"Best expression: {alg.BestExpression.ToInfixString()}");
    }

    */

    [TestMethod]
    public void RARExpr() {
      // check likelihood and DL calculation for top expressions from RAR paper
      GetRARData(out _, out var trainX, out var trainY, out _, out var e_log_gobs, out var e_log_gbar);
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
      options.NoiseSigma = "stot";
      options.TrainingRange = "0:2695";
      GetRARData(out _, out var trainX, out var trainY, out var trainNoiseSigma, out _, out _);

      ParamOptConvergence(new GaussianLikelihood(trainX, trainY, modelExpr: null, trainNoiseSigma.Select(s => 1.0 / s).ToArray()));
    }

    [TestMethod]
    public void ParamOptConvergenceWithRARLikelihood() {
      // Checks the parameter optimization success rate for the best ESR expression reported in the RAR paper
      // Uses RAR likelihood
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "log_gobs";
      options.NoiseSigma = "stot";
      options.TrainingRange = "0:2695";
      GetRARData(out _, out var trainX, out var trainY, out _, out var e_log_gobs, out var e_log_gbar);

      ParamOptConvergence(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
    }

    public void ParamOptConvergence(LikelihoodBase likelihood) {
      // best expression from RAR
      Expression<Expr.ParametricFunction> model1 = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]); // likelihood: -1279.1 DL: -1250.6 p: 0.84 -0.02 0.38 
      Expression<Expr.ParametricFunction> model1a = (p, x) => p[0] + p[1] * x[0] + p[2] * Math.Pow(Math.Abs(p[3] + x[0]), p[4]); // GE version of model 1
      Expression<Expr.ParametricFunction> model7 = (p, x) => Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]; // likelihood: -1250.6 DL: -1228.5 p: 1.87 -0.52 

      var models = new[] { (expr: model1, d: 3), (expr: model1a, d: 5), (expr: model7, d: 2) };
      foreach (var tup in models) {
        var nlr = new NonlinearRegression.NonlinearRegression();
        likelihood.ModelExpr = tup.expr;
        var restartPolicy = new RestartPolicy(length: tup.d, maxSeconds: 10);
        var parameterValues = restartPolicy.Next();
        int numRestarts = -1;
        do {
          numRestarts++;
          if (!double.IsNaN(likelihood.NegLogLikelihood(parameterValues))) {

            nlr.Fit(parameterValues, likelihood, maxIterations: 5000); // as in https://github.com/DeaglanBartlett/ESR/blob/main/esr/fitting/test_all.py
                                                                       // successful?
            if (nlr.ParamEst != null && !nlr.ParamEst.Any(double.IsNaN)) {
              System.Console.Error.WriteLine(nlr.OptReport);
              System.Console.Error.WriteLine($"Evals/s {nlr.OptReport.NumFuncEvals / nlr.OptReport.Runtime.TotalSeconds}");

              if (nlr.LaplaceApproximation == null) {
                System.Console.Error.WriteLine("Hessian not positive semidefinite after fitting");
                continue;
              }
              restartPolicy.Update(nlr.ParamEst, loss: nlr.NegLogLikelihood);
            }
          }

          parameterValues = restartPolicy.Next();
        } while (parameterValues != null);


        System.Console.WriteLine($"{tup.expr} Restarts: {numRestarts} Valid restarts: {restartPolicy.Iterations} Number of best: {restartPolicy.NumBest} ({100 * restartPolicy.NumBest / (double)restartPolicy.Iterations:f1}%) " +
          $"Best loss: {restartPolicy.BestLoss:e2} " +
          $"Best params: {(restartPolicy.BestParameters != null ? string.Join(" ", restartPolicy.BestParameters.Select(pi => pi.ToString("e4"))) : "")}");
      }
    }
  }
}