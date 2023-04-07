internal class Util {
  internal static double[,] GenerateRandom(Random rand, int rows, int cols) {
    var a = new double[rows, cols];
    for (int i = 0; i < rows; i++) {
      for (int j = 0; j < cols; j++) {
        a[i, j] = rand.NextDouble();
      }
    }
    return a;
  }

  internal static double[] GenerateRandom(Random rand, int rows) {
    var a = new double[rows];
    for (int i = 0; i < rows; i++) {
      a[i] = rand.NextDouble();
    }
    return a;
  }
}