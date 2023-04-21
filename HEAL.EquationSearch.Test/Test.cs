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


      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      // estimate sigma and use it and standard error in gaussian likelihood. sigma = sqrt(noiseRange / 2); weight_i = 1/simga²
      var weights = Enumerable.Repeat(1 / (noiseRange / 12), y.Length).ToArray();
      alg.Fit(x, y, varNames, CancellationToken.None, maxLength: maxLength, depthLimit: int.MaxValue, weights: weights);
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


      var varNames = new string[] { "x1", "x2", "x3", "x4", "x5", "x6", "x7", "x8", "x9", "x10" };

      var alg = new Algorithm();
      var g = new Grammar(varNames);
      g.UsePolynomialRules();
      var weights = Enumerable.Repeat(1 / (noiseRange / 12), y.Length).ToArray();
      alg.Fit(x, y, varNames, CancellationToken.None, grammar: g, maxLength: maxLength, depthLimit: int.MaxValue, weights: weights);
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
      alg.Fit(x, y, varNames, CancellationToken.None, maxLength: 30, weights: Enumerable.Repeat(1/Math.Pow(1e-5, 2), y.Length).ToArray());
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
      alg.Fit(x, y, varNames, CancellationToken.None, maxLength: 20, weights: Enumerable.Repeat(1/1e-6, y.Length).ToArray());
    }
  }
}