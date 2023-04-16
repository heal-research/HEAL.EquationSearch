internal class Util {
  internal static double[,] GenerateRandom(Random rand, int rows, int cols, double low = 0.0, double high = 1.0) {
    var a = new double[rows, cols];
    for (int i = 0; i < rows; i++) {
      for (int j = 0; j < cols; j++) {
        a[i, j] = rand.NextDouble() * (high - low) + low;
      }
    }
    return a;
  }

  internal static double[] GenerateRandom(Random rand, int rows, double low = 0.0, double high = 1.0) {
    var a = new double[rows];
    for (int i = 0; i < rows; i++) {
      a[i] = rand.NextDouble() * (high - low) + low;
    }
    return a;
  }
}