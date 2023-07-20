using CommandLine;

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
      var evaluator = new AutoDiffEvaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
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
      var evaluator = new AutoDiffEvaluator(new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar));
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
  }
}