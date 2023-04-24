
using CommandLine;
using System.IO.Compression;

namespace HEAL.EquationSearch.Console {
  public class Program {

    public static void Main(string[] args) {
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      var parserResult = Parser.Default.ParseArguments<RunOptions>(args)
        .WithParsed<RunOptions>(options => Run(options))
        ;
      ;
    }

    private static void Run(RunOptions options) {
      // if inputs are not specified we use all variables (except for the target variable) from the dataset (assumes that first row are variable names)
      var inputs = options.Inputs?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

      PrepareData(options, ref inputs, out var x, out var y, out var noiseSigma, out var trainStart, out var trainEnd, out var testStart, out var testEnd, out var trainX, out var trainY, out var trainNoiseSigma);
      var alg = new Algorithm();
      alg.Fit(trainX, trainY, trainNoiseSigma, inputs, CancellationToken.None, maxLength: options.MaxLength, randSeed: options.Seed);

      System.Console.WriteLine($"Best expression: {alg.BestExpression.ToInfixString()}");

      // for detailed result analysis we could use HEAL.NLR instead
      System.Console.WriteLine($"RMSE (train): {EvaluateRMSE(alg, trainX, trainY):g5}");

      // get test dataset
      Split(x, y, noiseSigma, trainStart, trainEnd, testStart, testEnd, out _, out _, out _, out var testX, out var testY, out var _);
      System.Console.WriteLine($"RMSE (test): {EvaluateRMSE(alg, testX, testY):g5}");
    }

    private static double EvaluateRMSE(Algorithm alg, double[,] x, double[] y) {
      var predY = alg.Predict(x);

      // output test mse
      var sse = 0.0;
      for (int i = 0; i < predY.Length; i++) {
        var res = y[i] - predY[i];
        sse += res * res;
      }
      return Math.Sqrt(sse / predY.Length);
    }

    private static void Shuffle(double[,] x, double[] y, double[] s, Random rand) {
      var n = y.Length;
      var d = x.GetLength(1);
      // via using sorting
      var idx = Enumerable.Range(0, n).ToArray();
      var r = idx.Select(_ => rand.NextDouble()).ToArray();
      Array.Sort(r, idx);

      var shufX = new double[n, d];
      var shufY = new double[y.Length];
      var shufS = new double[y.Length];
      for (int i = 0; i < n; i++) {
        shufY[idx[i]] = y[i];
        shufS[idx[i]] = s[i];
        Buffer.BlockCopy(x, i * sizeof(double) * d, shufX, idx[i] * sizeof(double) * d, sizeof(double) * d); // copy a row shufX[idx[i], :] = x[i, :]
      }

      // overwrite x,y,s with shuffled data
      Array.Copy(shufY, y, y.Length);
      Array.Copy(shufS, s, s.Length);
      Buffer.BlockCopy(shufX, 0, shufY, 0, shufY.Length * sizeof(double));
    }

    private static void PrepareData(RunOptions options, ref string[] inputs, out double[,] x, out double[] y, out double[] noiseSigma, 
      out int trainStart, out int trainEnd, out int testStart, out int testEnd, 
      out double[,] trainX, out double[] trainY, out double[] trainNoiseSigma) {
      if (double.TryParse(options.NoiseSigma, out var noiseSigmaVal)) {
        ReadData(options.Dataset, options.Target, noiseSigmaVarName: string.Empty, ref inputs, out x, out y, out _);
        noiseSigma = Enumerable.Repeat(noiseSigmaVal, y.Length).ToArray();
      } else {
        ReadData(options.Dataset, options.Target, options.NoiseSigma, ref inputs, out x, out y, out noiseSigma);
      }

      // default split is 66/34%
      var m = x.GetLength(0);
      trainStart = 0;
      trainEnd = (int)Math.Round(m * 2 / 3.0);
      testStart = (int)Math.Round(m * 2 / 3.0) + 1;
      testEnd = m - 1;
      if (options.TrainingRange != null) {
        var toks = options.TrainingRange.Split(":");
        trainStart = int.Parse(toks[0]);
        trainEnd = int.Parse(toks[1]);
        // when a training range is given, the test range is the remaining partition at the end
        testStart = trainEnd + 1;
        testEnd = m - 1;
      }
      if (options.TestingRange != null) {
        var toks = options.TestingRange.Split(":");
        testStart = int.Parse(toks[0]);
        testEnd = int.Parse(toks[1]);
      }

      var randSeed = new Random().Next();
      if (options.Seed != null) {
        randSeed = options.Seed.Value;
      }

      if (options.Shuffle) {
        var rand = new Random(randSeed);
        Shuffle(x, y, noiseSigma, rand);
      }

      Split(x, y, noiseSigma, trainStart, trainEnd, testStart, testEnd, out trainX, out trainY, out trainNoiseSigma, out var _, out var _, out var _);
    }

    // start and end are inclusive
    private static void Split(double[,] x, double[] y, double[] s, int trainStart, int trainEnd, int testStart, int testEnd,
      out double[,] trainX, out double[] trainY, out double[] trainS,
      out double[,] testX, out double[] testY, out double[] testS) {
      if (trainStart < 0) throw new ArgumentException("Negative index for training start");
      if (trainEnd >= y.Length) throw new ArgumentException($"End of training range: {trainEnd} but dataset has only {x.GetLength(0)} rows. Training range is inclusive.");
      if (testStart < 0) throw new ArgumentException("Negative index for training start");
      if (testEnd >= y.Length) throw new ArgumentException($"End of testing range: {testEnd} but dataset has only {x.GetLength(0)} rows. Testing range is inclusive.");

      var dim = x.GetLength(1);
      var trainRows = trainEnd - trainStart + 1;
      var testRows = testEnd - testStart + 1;
      trainX = new double[trainRows, dim]; trainY = new double[trainRows]; trainS = new double[trainRows];
      testX = new double[testRows, dim]; testY = new double[testRows]; testS = new double[testRows];
      Buffer.BlockCopy(x, trainStart * dim * sizeof(double), trainX, 0, trainRows * dim * sizeof(double));
      Array.Copy(y, trainStart, trainY, 0, trainRows);
      Array.Copy(s, trainStart, trainS, 0, trainRows);
      Buffer.BlockCopy(x, testStart * dim * sizeof(double), testX, 0, testRows * dim * sizeof(double));
      Array.Copy(y, testStart, testY, 0, testRows);
      Array.Copy(s, testStart, testS, 0, testRows);
    }


    public static void ReadData(string filename, string target, string noiseSigmaVarName, ref string[] inputs, out double[,] x, out double[] y, out double[] s) {
      List<string> lines = new List<string>();
      if (filename.EndsWith(".gz")) {
        using (var reader = new StreamReader(new GZipStream(new FileStream(filename, FileMode.Open, FileAccess.Read), CompressionMode.Decompress))) {
          while (!reader.EndOfStream) {
            lines.Add(reader.ReadLine());
          }
        }
      } else {
        lines.AddRange(File.ReadAllLines(filename));
      }
      var varNames = lines[0].Split(',');
      var targetVarIndex = Array.IndexOf(varNames, target);
      if (targetVarIndex < 0) {
        throw new ArgumentException($"Target variable {target} not found in the dataset.");
      }

      var noiseSigmaVarIndex = Array.IndexOf(varNames, noiseSigmaVarName);
      if (!string.IsNullOrEmpty(noiseSigmaVarName) && noiseSigmaVarIndex < 0) {
        throw new ArgumentException($"Noise sigma variable {noiseSigmaVarName} not found in the dataset.");
      }

      int[] inputIndex = null;
      if (inputs == null) inputs = varNames.Except(new[] { target }).ToArray(); // use all variables except for target as default

      var missingVarNames = inputs.Where(v => !varNames.Contains(v));
      if (missingVarNames.Any()) {
        throw new ArgumentException($"Variable names {string.Join(", ", missingVarNames)} not found in the dataset");
      }

      inputIndex = inputs.Select(iv => Array.IndexOf(varNames, iv)).ToArray();

      y = new double[lines.Count - 1];
      s = new double[lines.Count - 1];
      x = new double[y.Length, inputs.Length];
      for (int i = 0; i < lines.Count - 1; i++) {
        var toks = lines[i + 1].Split(',');
        for (int j = 0; j < inputs.Length; j++) {
          x[i, j] = double.Parse(toks[inputIndex[j]]);
        }
        y[i] = double.Parse(toks[targetVarIndex]);
        s[i] = noiseSigmaVarIndex >= 0 ? double.Parse(toks[noiseSigmaVarIndex]) : 1.0;
      }
    }

    // TODO: options for grammar rules, options for search parameters (type, beam-width, ...)
    private class RunOptions {
      [Option('d', "dataset", Required = true, HelpText = "Filename with dataset in csv format.")]
      public string Dataset { get; set; }

      [Option('t', "target", Required = true, HelpText = "Target variable name.")]
      public string Target { get; set; }

      [Option("noise-sigma", Required = false, Default = "1.0", HelpText = "Variable name in the dataset for the standard error for each observation. " +
        "Specify a real value if the same error should be assumed for all observations. The square of this value is used for weighting in least squares " +
        "parameter optimization and for the Gaussian likelihood in model selection.")]
      public string NoiseSigma { get; set; }

      [Option('i', "inputs", Required = false, HelpText = "Comma separated list of input variables. If not specified all variables from the dataset are used.")]
      public string Inputs { get; set; }

      [Option("train", Required = false, HelpText = "The training range <firstRow>:<lastRow> in the dataset (inclusive).")]
      public string TrainingRange { get; set; }

      [Option("test", Required = false, HelpText = "The testing range <firstRow>:<lastRow> in the dataset (inclusive).")]
      public string TestingRange { get; set; }

      [Option("max-length", Required = false, Default = 20, HelpText = "The maximum length limit for the expression.")]
      public int MaxLength { get; set; }

      [Option("shuffle", Required = false, Default = false, HelpText = "Switch to shuffle the dataset before fitting.")]
      public bool Shuffle { get; set; }

      [Option("seed", Required = false, HelpText = "Random seed.")]
      public int? Seed { get; set; }
    }
  }
}