using BenchmarkDotNet.Attributes;
using System.Linq.Expressions;
using HEAL.Expressions;
using System.Linq.Expressions;
using HEAL.EquationSearch;
using HEAL.NonlinearRegression;
using HEAL.EquationSearch.Test;

public class RARLikelihoodEvaluation {
  public double[,] data;
  public double[][] dataCols;

  // the test expression with a mix of functions
  // Expression<Expr.ParametricFunction> expr = (p, x) => p[0] * x[0]
  //                                                      + Math.Log(Math.Abs(p[1] * x[1] + p[2] * x[2])) * Math.Sqrt(Math.Abs(p[3] * x[3]))
  //                                                      + Math.Pow(x[4], 3.0)
  //                                                      + Math.Sin(p[4] * x[4]);

  // the likelihood expression for a RAR model
  static Expression<Expr.ParametricFunction> model = (p, x) => p[0] * (Math.Pow(Math.Abs(p[1] + x[0]), p[2]) + x[0]);
  // df/dgbar
  static Expression<Expr.ParametricFunction> dModel = Expr.Derive(model, model.Parameters[1], 0); // ((2.302585092994046 * (((Pow((Pow(p[0], x[0]) / x[0]), (p[1] - 1)) * (p[1] * (((x[0] * (Pow(p[0], (x[0] - 1)) * (p[0] * Log(p[0])))) - Pow(p[0], x[0])) / Pow(x[0], 2)))) + 1) / (Pow((Pow(p[0], x[0]) / x[0]), p[1]) + x[0]))) / 5.301898110478399)

  // df/d log gbar
  // 5.301898110478399 * x[0] * ((2.302585092994046 * (((Pow((Pow(p[0], x[0]) / x[0]), (p[1] - 1)) * (p[1] * (((x[0] * (Pow(p[0], (x[0] - 1)) * (p[0] * Log(p[0])))) - Pow(p[0], x[0])) / Pow(x[0], 2)))) + 1) / (Pow((Pow(p[0], x[0]) / x[0]), p[1]) + x[0]))) / 5.301898110478399)

  // sigma2_tot = e_loggobs**2 + (gobs1_diff*e_loggbar)**2
  static Expression<Expr.ParametricFunction> sigma_tot = (p, x) => (x[1] * x[1] + Math.Pow(
    5.301898110478399 * x[0] * ((2.302585092994046 * (((Math.Pow((Math.Pow(p[0], x[0]) / x[0]), (p[1] - 1)) * (p[1] * (((x[0] * (Math.Pow(p[0], (x[0] - 1)) * (p[0] * Math.Log(p[0])))) - Math.Pow(p[0], x[0])) / Math.Pow(x[0], 2)))) + 1) / (Math.Pow((Math.Pow(p[0], x[0]) / x[0]), p[1]) + x[0]))) / 5.301898110478399)
    * x[2], 2.0));

  // the full (expanded) model
  //  0.5 * np.sum((np.log10(gobs) - np.log10(gobs1))**2 ./ sigma2_tot + np.log(2.* np.pi * sigma2_tot))
  static Expression<Expr.ParametricFunction> expr = (p, x) => 0.5 * Math.Pow(x[0] - Math.Log(Math.Pow(Math.Pow(p[0], x[0]) / x[0], p[1]) + x[0]) / Math.Log(10), 2.0) / (x[1] * x[1] + Math.Pow(
    5.301898110478399 * x[0] * ((2.302585092994046 * (((Math.Pow((Math.Pow(p[0], x[0]) / x[0]), (p[1] - 1)) * (p[1] * (((x[0] * (Math.Pow(p[0], (x[0] - 1)) * (p[0] * Math.Log(p[0])))) - Math.Pow(p[0], x[0])) / Math.Pow(x[0], 2)))) + 1) / (Math.Pow((Math.Pow(p[0], x[0]) / x[0]), p[1]) + x[0]))) / 5.301898110478399)
    * x[2], 2.0)) + Math.Log(2.0 * Math.PI * (x[1] * x[1] + Math.Pow(
    5.301898110478399 * x[0] * ((2.302585092994046 * (((Math.Pow((Math.Pow(p[0], x[0]) / x[0]), (p[1] - 1)) * (p[1] * (((x[0] * (Math.Pow(p[0], (x[0] - 1)) * (p[0] * Math.Log(p[0])))) - Math.Pow(p[0], x[0])) / Math.Pow(x[0], 2)))) + 1) / (Math.Pow((Math.Pow(p[0], x[0]) / x[0]), p[1]) + x[0]))) / 5.301898110478399)
    * x[2], 2.0)));

  private RARLikelihoodNumeric lik;

  [GlobalSetup]
  public void Setup() {
    Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

    var options = new HEAL.EquationSearch.Console.Program.RunOptions();
    options.Dataset = "RAR_sigma.csv";
    options.Target = "gobs";
    options.TrainingRange = "0:2695";
    options.NoiseSigma = "stot";
    options.Seed = 1234;

    GetRARData(options, out var inputs, out var trainX, out var trainY, out var trainNoiseSigma, out var e_log_gobs, out var e_log_gbar);

    this.lik = new RARLikelihoodNumeric(trainX, trainY, model, e_log_gobs, e_log_gbar);
  }

  [Benchmark]
  public void Optimize() {
    var nlr = new NonlinearRegression();
    var p = GenerateRandomPoint(3);
    for (int i = 0; i < 100; i++) {
      nlr.Fit(p, lik, epsF: 1e-2);
      System.Console.WriteLine($"{nlr.NegLogLikelihood} {nlr.OptReport}");
      p = GenerateRandomPoint(3);
    }
  }

  private double[] GenerateRandomPoint(int n) {
    var p = new double[n];
    for (int i = 0; i < n; i++)
      p[i] = System.Random.Shared.NextDouble() * 4;
    return p;
  }

  private static void GetRARData(HEAL.EquationSearch.Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY, out double[] trainNoiseSigma, out double[] e_log_gobs, out double[] e_log_gbar) {
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


}