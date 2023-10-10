using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class PoissonTest {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [DataTestMethod]
    [DataRow(10)]
    public void BikesharingBreadthFirstSearchReducedGrammar(int maxLength) {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "SeoulBikeData.csv";
      options.Target = "RentedBikeCount";
      options.TrainingRange = "0:5000";
      options.Seed = 1234;

      GetBikeRentalData(options, out var inputs, out var trainX, out var trainY);

      var grammar = new Grammar(inputs, maxLength);
      grammar.UseLogExpPowRestrictedRules();

      var alg = new Algorithm();
      var likelihood = new PoissonLikelihood(trainX, trainY, modelExpr: null);
      var evaluator = new Evaluator(likelihood);
      alg.Fit(trainX, trainY, noiseSigma: 1.0, inputs, CancellationToken.None, grammar: grammar, evaluator: evaluator, maxLength: maxLength, randSeed: 1234, algorithmType: AlgorithmTypeEnum.BreadthFirst);

      likelihood.ModelExpr = HEALExpressionBridge.ConvertToExpressionTree(alg.BestExpression, alg.VariableNames, out var bestParam);
      System.Console.WriteLine($"Parameters {string.Join(" ", bestParam.Select(p => p.ToString("e4")))}");
      System.Console.WriteLine($"{alg.BestExpression.ToInfixString()} DL: {alg.BestDescriptionLength:f2} logL {likelihood.NegLogLikelihood(bestParam):f2}");
      // Assert.AreEqual(expectedDL, alg.BestDescriptionLength.Value, Math.Abs(expectedDL * 1e-4));
    }

    private static void GetBikeRentalData(Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY) {
      inputs = new string[] { "Date_dec", "date_dec_yearly", "date_dec_weekly", "Hour", "Temperature", "Humidity", "WindSpeed", "Visibility", 
        "Visibility_2000_or_more", "DewPointTemperature", "SolarRadiation", "Rainfall", "Snowfall", "Seasons_Winter", "Seasons_Summer", "Seasons_Autumn", 
        "Seasons_Spring", "Holiday", "FunctioningDay" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out _);
    }
  }
}