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

      var childRoot = rootIdx - 1;
      var childExpressions = new List<string>();
      for (int cIdx = 0; cIdx < numC; cIdx++) {
        var childExpression = ToInfixString(childRoot, lengths);
        if (syString[rootIdx].Arity == 2 && syString[childRoot].Arity == 2 && syString[childRoot] != Grammar.Div) {
          childExpression = $"({childExpression})";
        } else if (syString[rootIdx] == Grammar.Div && syString[childRoot].Arity == 2) {
          childExpression = $"({childExpression})";
        } else if (syString[rootIdx] == Grammar.Pow && syString[childRoot] == Grammar.Div) {
          // Div within pow is in parenthesis. I don't know why...
          childExpression = $"({childExpression})";
        }

        childExpressions.Add(childExpression);
        childRoot -= lengths[childRoot];
      }

      var operatorPadding = syString[rootIdx] == Grammar.Plus ? " " : string.Empty;
      if (syString[rootIdx].Arity == 2) {
        return string.Join(operatorPadding + rootStr + operatorPadding, childExpressions);
      }

      if (syString[rootIdx].Arity == 1) {
        return $"{rootStr}({childExpressions[0]})";
      }
      return rootStr;
      

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
