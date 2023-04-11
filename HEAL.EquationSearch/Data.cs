namespace HEAL.EquationSearch {
  /// <summary>
  /// Input and target vectors in column-oriented format. Vector for each variable can be accessed via its name.
  /// </summary>
  public class Data {
    private readonly Dictionary<string, double[]> values = new Dictionary<string, double[]>();
    public int Rows { get; }
    public int[] AllRowIdx { get; }
    public double[] Weights { get; }
    public double[] Target { get; }
    public IEnumerable<string> VarNames { get; internal set; }

    public Data(string[] variableNames, double[,] x, double[] y) {
      Rows = x.GetLength(0);
      this.AllRowIdx = Enumerable.Range(0, Rows).ToArray();
      this.Weights = Enumerable.Repeat(1.0, Rows).ToArray(); // TODO support weights for alg
      this.Target = y;
      this.VarNames = variableNames;
      var cols = x.GetLength(1);

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