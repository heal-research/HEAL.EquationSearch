using HEAL.Expressions;
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


    [TestMethod]
    public void EvaluateModel() {
      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "SeoulBikeData.csv";
      options.Target = "RentedBikeCount";
      options.TrainingRange = "0:8000";
      options.Seed = 1234;

      GetBikeRentalData(options, out var inputs, out var trainX, out var trainY);

      var varSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "x");
      var paramSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "p");
      var variablesNames = inputs;
      //var exprStr = "Exp(Temperature * 0.14197412) * -28.169514 " +
      //  "+ Temperature * 177.32869 " +
      //  "+ Humidity * -35.455525 " +
      //  "+ Exp(FunctioningDay * 8.8266581 + Rainfall * -1.3207561) * 1e-6 " +
      //  "+ FunctioningDay * 4.0104302 " +
      //  "+ Holiday * -0.36177268 " +
      //  "+ date_dec_yearly * 0.39734289";
      // var exprStr = "1.0 * Temperature + 1.0";
      // var exprStr = "1.0 * RentedBikeCount + 1.0";
      var exprStr = "0.01 * FunctioningDay + 0.01 * Holiday + 0.01 * date_dec_yearly + 0.01 * Hour + 0.01 * Seasons_Winter + 0.01 * Temperature + 0.01 * Humidity +0.01 * Rainfall";
      var parser = new HEAL.Expressions.Parser.ExprParser(exprStr, inputs, varSy, paramSy);
      var expr = parser.Parse();
      var likelihood = new PoissonLikelihood(trainX, trainY, expr);
      var paramValues = parser.ParameterValues.ToArray();
      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.Fit(paramValues, likelihood, maxIterations: 1000);

      var laplaceIntervals = nlr.PredictWithIntervals(trainX, IntervalEnum.LaplaceApproximation);
      using (var writer = new StreamWriter(@"c:\temp\poissonprediction_laplace.csv")) {
        for (int i = 0; i < laplaceIntervals.GetLength(0); i++) {
          writer.Write(trainY[i] + ";");
          for (int j = 0; j < laplaceIntervals.GetLength(1); j++) {
            writer.Write(laplaceIntervals[i, j] + ";");
          }
          writer.WriteLine();
        }
      }

      var profileIntervals = nlr.PredictWithIntervals(trainX, IntervalEnum.TProfile);
      using (var writer = new StreamWriter(@"c:\temp\poissonprediction_profile.csv")) {
        for (int i = 0; i < profileIntervals.GetLength(0); i++) {
          writer.Write(trainY[i] + ";");
          for (int j = 0; j < profileIntervals.GetLength(1); j++) {
            writer.Write(profileIntervals[i, j] + ";");
          }
          writer.WriteLine();
        }
      }
      nlr.WriteStatistics();
    }

    private static void GetBikeRentalData(Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY) {
      inputs = new string[] { "Date_dec", "date_dec_yearly", "date_dec_weekly", "Hour", "Temperature", "Humidity", "WindSpeed", "Visibility",
        "Visibility_2000_or_more", "DewPointTemperature", "SolarRadiation", "Rainfall", "Snowfall", "Seasons_Winter", "Seasons_Summer", "Seasons_Autumn",
        "Seasons_Spring", "Holiday", "FunctioningDay" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out _);
    }
  }
}