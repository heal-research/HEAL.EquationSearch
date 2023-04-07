using TreesearchLib;

namespace HEAL.EquationSearch {

  // TreesearchLib only has a Minimize type for integers.
  // This type is used for qualities and provides the comparison of qualities.
  public struct MinimizeDouble : IQuality<MinimizeDouble> {
    public double Value { get; private set; }
    public MinimizeDouble(double value) {
      Value = value;
    }

    public bool IsBetter(MinimizeDouble other) => Value < other.Value;

    public override string ToString() => $"min( {Value:g5} )";


    public int CompareTo(MinimizeDouble other) {
      return Value.CompareTo(other.Value);
    }
  }
}