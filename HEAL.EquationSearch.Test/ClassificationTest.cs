using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class ClassificationTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [DataTestMethod]
    [DataRow(10)]
    public void BeamSearchReducedGrammar(int maxLength) {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "bankruptcy.csv";
      options.Target = "Bankrupt";
      options.TrainingRange = "0:48";
      options.Seed = 1234;

      GetData(options, out var inputs, out var trainX, out var trainY);

      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var evaluator = new Evaluator(new BernoulliLikelihood(trainX, trainY, modelExpr: null));
      alg.Fit(trainX, trainY, noiseSigma: 1.0, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);
      // Assert.AreEqual(expectedDL, alg.BestDescriptionLength.Value, Math.Abs(expectedDL * 1e-4));
    }

    private static void GetData(Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY) {
      inputs = new string[] { "WC/TA","RE/TA","EBIT/TA","S/TA","BVE/BVL" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out _);
    }
  }
}