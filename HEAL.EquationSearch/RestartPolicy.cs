namespace HEAL.EquationSearch {
  // controls random restarts for parameter optimization
  internal class RestartPolicy {
    public int length { get; private set; }
    public int MaxIterations => (int)(70.71 * Math.Exp(0.381 * length)); // 104, 152, 222, 325 ...
    
    // for early stopping (when finding NConv times the best parameters)
    public int NConv => 15 + (length - 1) * 20; // 15, 35, 55, 75 ... 

    public int MaxSeconds { get; private set; }
    public DateTime StartTime { get; private set; }
    public int Iterations { get; private set; } = 0;

    public double BestLoss { get; private set; } = double.MaxValue;
    public double[] BestParameters { get; private set; } = null;
    public int NumBest { get; private set; } = 0; // how often the best loss was found
    public List<(double[] p, double loss)> Parameters { get; private set; } = new(); // for debugging

    public RestartPolicy(int length, int maxSeconds = 3600) {
      this.length = length;      
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


      var p = new double[length];
      for (int i = 0; i < length; i++) p[i] = SharedRandom.NextDouble() * 6 - 3; // in ESR code only unif(0, 3), TODO
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