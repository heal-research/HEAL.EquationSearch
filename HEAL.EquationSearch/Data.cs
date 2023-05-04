namespace HEAL.EquationSearch {
  /// <summary>
  /// Input and target vectors in column-oriented format. Vector for each variable can be accessed via its name.
  /// </summary>
  public class Data {
    private readonly Dictionary<string, double[]> values = new Dictionary<string, double[]>();
    public int Rows { get; }
    public int[] AllRowIdx { get; }
    public double[] InvNoiseVariance { get; } // 1/sErr²
    public double[] InvNoiseSigma { get; } // 1/sErr
    public double[] Target { get; }
    public string[] VarNames { get; internal set; }
    public double[,] X { get; }

    public Data(string[] variableNames, double[,] x, double[] y, double[] invNoiseVariance) {
      Rows = x.GetLength(0);
      this.AllRowIdx = Enumerable.Range(0, Rows).ToArray();
      this.InvNoiseVariance= invNoiseVariance;
      this.InvNoiseSigma = invNoiseVariance.Select(si => Math.Sqrt(si)).ToArray(); // TODO use only one of var and sigma
      this.Target = y;
      this.VarNames = variableNames;

      this.X = x; // row-oriented representation

      var cols = x.GetLength(1);

      // prepare column-oriented representation
      for (int j = 0; j < cols; j++) {
        var values = new double[Rows];
        for (int i = 0; i < Rows; i++) {
          values[i] = x[i, j];
        }
        this.values[variableNames[j]] = values;
      }
    }

    internal double[] GetValues(string varName) {
      return values[varName];
    }
  }
}