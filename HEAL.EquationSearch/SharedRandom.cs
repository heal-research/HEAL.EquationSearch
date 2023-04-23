
namespace HEAL.EquationSearch {
  // Crude implementation of shared thread-safe random number generator for which we can set the seed.
  // Required to make the algorithm deterministic by setting a random seed.
  internal class SharedRandom {

    private static System.Random random = new System.Random();

    public static void SetSeed(int seed) {
      lock (random) random = new System.Random(seed);
    }

    public static double NextDouble() {
      lock (random) return random.NextDouble();
    }
  }
}
