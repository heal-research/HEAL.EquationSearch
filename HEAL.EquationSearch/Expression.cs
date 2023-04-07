using System.Collections;

namespace HEAL.EquationSearch {
  public class Expression : IEnumerable<Grammar.Symbol> {
    private readonly Grammar.Symbol[] syString;
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

    public int FirstIndexOfNT() {
      return Array.FindIndex(syString, sy => sy.IsNonterminal);
    }

    // returns a new expression with the symbol at pos replaced with syString
    public Expression Replace(int pos, Grammar.Symbol[] replSyString) {

      var newSyString = new Grammar.Symbol[syString.Length - 1 + replSyString.Length];

      // terminalClassSymbols must be cloned
      int j = 0;
      for (int i = 0; i < pos; i++) { newSyString[j++] = syString[i].Clone(); }
      for (int i = 0; i < replSyString.Length; i++) { newSyString[j++] = replSyString[i].Clone(); }
      for (int i = pos + 1; i < syString.Length; i++) { newSyString[j++] = syString[i].Clone(); }
      return new Expression(Grammar, newSyString);
    }

    internal void UpdateCoefficients(int[] idx, double[] coeff) {
      for(int i=0;i<idx.Length;i++) {
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
        subExpressions.Insert(0, ToInfixString(c, lengths));
        c = c - lengths[c];
      }
      if (subExpressions.Any()) {
        if (subExpressions.Count == 1) {
          return rootStr + "(" + subExpressions[0] + ")"; // functions have a single subexpression (TODO: this is only true for now)
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
