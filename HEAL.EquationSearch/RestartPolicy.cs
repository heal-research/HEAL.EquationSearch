namespace HEAL.EquationSearch {
  // controls random restarts for parameter optimization
  public class RestartPolicy {
    public int NumParam { get; private set; }

    // more than in ESR
    public int MaxIterations => NumParam == 0 ? 0 : (int)(70.71 * Math.Exp(0.381 * NumParam)); // 0, 104, 152, 222, 325 ... 
    // public int MaxIterations => 40 + 60 * NumParam; // same as in ESR https://github.com/DeaglanBartlett/ESR/blob/main/esr/fitting/test_all.py line 105

    // for early stopping (when finding NConv times the best parameters)
    public int NConv => NumParam * 20 - 5; // 15, 35, 55, 75 ...  same as in ESR (URL above)

    public int MaxSeconds { get; private set; }
    public DateTime StartTime { get; private set; }
    public int Iterations { get; private set; } = 0;

    public double BestLoss { get; private set; } = double.MaxValue;
    public double[] BestParameters { get; private set; } = null;
    public int NumBest { get; private set; } = 0; // how often the best loss was found
    public List<(double[] p, double loss)> Parameters { get; private set; } = new(); // for debugging

    public RestartPolicy(int numParam, int maxSeconds = 3600) {
      this.NumParam = numParam;
      this.MaxSeconds = maxSeconds;
      this.StartTime = DateTime.Now;
    }

    // null means to stop restarts
    internal double[] Next() {
      if (Iterations >= MaxIterations
          || NumBest >= NConv
          || BestLoss == double.MaxValue && Iterations > 50 // no valid solution in 50 iterations
          || (DateTime.Now - StartTime).TotalSeconds > MaxSeconds
          ) return null;


      var p = new double[NumParam];
      for (int i = 0; i < NumParam; i++) p[i] = SharedRandom.NextDouble() * 6 - 3; // NOTE in ESR code only unif(0, 3) is used
      return p;
    }

    internal void Update(double[] p, double loss) {
      // we ignore p

      Iterations++;
      Parameters.Add(((double[])p.Clone(), loss));

      if (loss < BestLoss - 2) {
        // much better -> reset convergence counter
        NumBest = 1;
      }

      // close to best 
      if (Math.Abs(loss - BestLoss) < 0.5) NumBest++;

      if (loss < BestLoss) {
        BestLoss = loss;
        BestParameters = (double[])p.Clone();
      }
    }
  }
}