using System.Collections;

namespace HEAL.EquationSearch {
  public class Expression : IEnumerable<Grammar.Symbol> {
    private readonly Grammar.Symbol[] syString;
    public Grammar.Symbol[] SymbolString => syString;
    public Grammar.Symbol this[int pos] {
      get => syString[pos];
    }
    public Expression(Grammar grammar, Grammar.Symbol[] syString) {
      this.Grammar = grammar;
      this.syString = syString;
    }

    public int Length => syString.Length;
    public bool IsSentence => FirstIndexOfNT() < 0; // no NT found

    public Grammar Grammar { get; internal set; }

    private int FirstIndexOfNT() {
      return Array.FindIndex(syString, sy => sy.IsNonterminal);
    }

    internal void UpdateCoefficients(int[] idx, double[] coeff) {
      for (int i = 0; i < idx.Length; i++) {
        ((Grammar.ParameterSymbol)syString[idx[i]]).Value = coeff[i];
      }
    }

    public override string ToString() {
      return string.Join(" ", syString.Select(sy => sy.ToString()));
    }

    #region infix string output
    public string ToInfixString() {
      // postfix to infix representation to make it more readable
      // for all operations we know the arity 

      var lengths = Semantics.GetLengths(this);
      return ToInfixString(Length - 1, lengths);
    }

    private string ToInfixString(int rootIdx, int[] lengths) {
      var rootStr = syString[rootIdx].ToString();
      var numC = syString[rootIdx].Arity;

      var subExpressions = new List<string>();
      var c = rootIdx - 1;
      for (int cIdx = 0; cIdx < numC; cIdx++) {
        if (lengths[c] == 1) {
          // no need to use ( ... ) for terminal symbols
          subExpressions.Add(ToInfixString(c, lengths));
        } else {
          subExpressions.Add("(" + ToInfixString(c, lengths) + ")");
        }
        c = c - lengths[c];
      }
      if (subExpressions.Any()) {
        // functions
        if (subExpressions.Count == 1) {
          if (syString[rootIdx] == Grammar.Neg) {
            return $"- {subExpressions[0]}";
          } else if (syString[rootIdx] == Grammar.Inv) {
            return $"1 / {subExpressions[0]}";
          } else {
            return rootStr + "( " + subExpressions[0] + " )";
          }
        } else {
          return string.Join(" " + rootStr + " ", subExpressions);
        }
      } else return rootStr;
    }

    public IEnumerator<Grammar.Symbol> GetEnumerator() {
      return syString.OfType<Grammar.Symbol>().GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() {
      return this.GetEnumerator();
    }
    #endregion
  }
}
