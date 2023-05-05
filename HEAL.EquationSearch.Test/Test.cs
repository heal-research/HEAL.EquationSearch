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
      var w = new double[100];
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
    public void CosmicChronometerZ() {
      // Run for Cosmic Chronometer dataset from Exhaustive Symbolic Regression
      // https://github.com/DeaglanBartlett/ESR/blob/main/esr/data/CC_Hubble.dat
      // https://arxiv.org/pdf/2211.11461.pdf
      var parameters = "--dataset CC_Hubble.csv --target H --inputs z --train 0:31 --max-length 20 --noise-sigma H_err --seed 1234";
      HEAL.EquationSearch.Console.Program.Main(parameters.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }

    [TestMethod]
    public void CosmicChronometerX() {
      // as above but use x = z+1 and search for H(x)
      var parameters = "--dataset CC_Hubble.csv --target H --inputs x --train 0:31 --max-length 20 --noise-sigma H_err --seed 1234";
      HEAL.EquationSearch.Console.Program.Main(parameters.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }

    [TestMethod]
    public void RAR() {
      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry (Slack)

      // RAR_sigma.csv is created via:
      // mlr --csv --from RAR.csv put '$log_gbar = log10($gbar);
      //                               $log_gobs = log10($gobs);
      //                               $e_log_gbar = $e_gbar/($gbar*log(10));
      //                               $e_log_gobs = $e_gobs/($gobs*log(10));
      //                               $sigma_tot = sqrt($e_log_gobs**2 + (0.6725 * $e_log_gbar)**2);
      //                              ' \
      //                              > RAR_sigma.csv
      var parameters = "--dataset RAR_sigma.csv --target log_gobs --inputs log_gbar --train 0:2695 --max-length 30 --noise-sigma sigma_tot --seed 1234";

      HEAL.EquationSearch.Console.Program.Main(parameters.Split(" ", StringSplitOptions.RemoveEmptyEntries));
    }

  }
}