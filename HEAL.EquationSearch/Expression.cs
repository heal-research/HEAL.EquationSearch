﻿using System.Collections;
using HEAL.Expressions;
using static HEAL.EquationSearch.Grammar;

namespace HEAL.EquationSearch {
  public class Expression : IEnumerable<Grammar.Symbol> {
    private readonly Grammar.Symbol[] syString;
    public Grammar.Symbol[] SymbolString => syString;
    public Grammar.Symbol this[int pos] {
      get => syString[pos];
    }
    public int Length => syString.Length;
    public bool IsSentence => FirstIndexOfNT() < 0; // no NT found

    public Grammar Grammar { get; internal set; }

    public Expression(Grammar grammar, Grammar.Symbol[] syString) {
      this.Grammar = grammar;
      this.syString = syString;
    }

    private int FirstIndexOfNT() {
      return Array.FindIndex(syString, sy => sy.IsNonterminal);
    }

    internal void UpdateCoefficients(int[] idx, double[] coeff) {
      for (int i = 0; i < idx.Length; i++) {
        ((Grammar.ParameterSymbol)syString[idx[i]]).Value = coeff[i];
      }
    }

    public string ToString(bool includeParamValues) {
      if (includeParamValues)
        return string.Join(" ", syString.Select(sy => sy.ToString()));
      else
        return string.Join(" ", syString.Select(sy => sy is ParameterSymbol ? "p" : sy.ToString()));
    }

    public override string ToString() {
      return ToString(includeParamValues: true);
    }

    #region infix string output
    public string ToInfixString(bool includeParamValues = true) {
      // postfix to infix representation to make it more readable
      // for all operations we know the arity 

      var lengths = Semantics.GetLengths(this);
      return ToInfixString(Length - 1, lengths, includeParamValues);
    }

    private string ToInfixString(int rootIdx, int[] lengths, bool includeParamValues) {
      string rootStr;
      if (!includeParamValues && syString[rootIdx] is Grammar.ParameterSymbol) {
        rootStr = "p";
      } else {
        rootStr = syString[rootIdx].ToString();
      }
      var numC = syString[rootIdx].Arity;

      var subExpressions = new List<string>();
      var c = rootIdx - 1;
      for (int cIdx = 0; cIdx < numC; cIdx++) {
        if (lengths[c] == 1) {
          // no need to use ( ... ) for terminal symbols
          subExpressions.Add(ToInfixString(c, lengths, includeParamValues));
        } else {
          subExpressions.Add("(" + ToInfixString(c, lengths, includeParamValues) + ")");
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
        } else if (syString[rootIdx] == Grammar.PowAbs) {
          return $"pow(abs ({subExpressions[0]}), {subExpressions[1]})";
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

    internal Expression Clone() {
      return new Expression(Grammar, (Symbol[])syString.Clone());
    }
    #endregion
  }
}
