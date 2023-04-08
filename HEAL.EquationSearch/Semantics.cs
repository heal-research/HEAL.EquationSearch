using static HEAL.EquationSearch.Semantics;

namespace HEAL.EquationSearch {
  public class Semantics {
    public static bool IsCanonicForm(Expression expr) {
      // we only allow expressions in canonical form
      // 1. for commutative operators (x ° y = y ° x) we only allow the form such that order(x) <= order(y)
      // 2. for (param * x + param * y) is not allowed if (order(x) == order(y)) the same term

      var ok = IsCanonicForCommutative(expr) && IsCanonicForDistributive(expr);
      
      // for debugging and unit tests
      // Console.Error.WriteLine($"{ok} {expr}");
      return ok;
    }

    public static bool IsCanonicForCommutative(Expression expr) {
      var terms = new Expr(expr).Terms;
      return terms.All(t => IsOrdered(t.Factors)) && IsOrdered(terms);
    }

    private static bool IsOrdered<T>(IEnumerable<T> factors) where T : IComparable<T> {
      // the factors must be ordered
      // for the ordering we use the symbol index in the grammar
      // terminal symbols with smaller index must occur before those with larger indexes
      // Order: param, var_1, ..., var_n,     

      var enumerator = factors.GetEnumerator();

      // assumes at least one factor
      enumerator.MoveNext();
      var prevFactor = enumerator.Current;
      while (enumerator.MoveNext()) {
        var curFactor = enumerator.Current;
        if (curFactor.CompareTo(prevFactor) < 0) return false;
        prevFactor = curFactor;
      }
      // all factors ordered
      return true;
    }

    public static bool IsCanonicForDistributive(Expression expr) {
      // we do not allow p1 * x + p2 * x because it can be represented as p3 * x
      // where x are the same and have no nonlinear parameters

      // TODO: detection of duplicate terms is not implemented yet
      return true;
    }

    public static int[] GetLengths(Expression expr) {
      var lengths = new int[expr.Length]; // length of each part
      for (int i = 0; i < expr.Length; i++) {
        var c = i - 1; // first child index
        lengths[i] = 1;
        for (int cIdx = 0; cIdx < expr[i].Arity; cIdx++) {
          lengths[i] += lengths[c];
          c = c - lengths[c];
        }
      }
      return lengths;
    }

    public class Expr {
      private readonly Expression expr;
      public IEnumerable<Term> Terms { get; }
      /// <summary>
      /// Indexes of all term coefficients + the intercept
      /// </summary>
      public IEnumerable<int> CoeffIdx { get; }
      public Expr(Expression expr) {
        this.expr = expr;
        var terms = new List<Term>();
        var coeffIdx = new List<int>();

        GetTermsRec(expr, expr.Length - 1, GetLengths(expr), terms, coeffIdx);
        this.Terms = terms;
        this.CoeffIdx = coeffIdx;
      }

      private static void GetTermsRec(Expression expr, int exprIdx, int[] lengths, List<Term> terms, List<int> coeffIndexes) {
        // get terms
        if (expr[exprIdx] == expr.Grammar.Plus) {
          var c = exprIdx - 1; // first child idx
          for (int cIdx = 0; cIdx < expr[exprIdx].Arity; cIdx++) {
            GetTermsRec(expr, c, lengths, terms, coeffIndexes);
            c = c - lengths[c];
          }
        } else {
          // here we accept only "<coeff> <term> *" or "<coeff>"
          if (expr[exprIdx] == expr.Grammar.Times) {
            // calculate index of coefficient
            var end = exprIdx - 1;
            var start = end - lengths[end] + 1;
            terms.Insert(0, new Term(expr, lengths, start, end)); // TODO: perf
            var coeffIdx = start - 1;
            if (expr[coeffIdx] is not Grammar.ParameterSymbol) throw new NotSupportedException("Invalid expression form. Expected: <coeff> <term> *");
            coeffIndexes.Insert(0, coeffIdx); // TODO: perf
          } else if (expr[exprIdx] is Grammar.ParameterSymbol) {
            coeffIndexes.Insert(0, exprIdx); // TODO: perf
          } else {
            // Assert that each term has the pattern: <coeff * term> or just <coeff>
            // throw new InvalidProgramException($"Term does not have the structure <coeff * term> in {string.Join(" ", expr.Select(sy => sy.ToString()))} at position {exprIdx}");
          }
        }
      }
    }
    // A term represents a term within an expression. 
    // Internally it is represented as a span of the expression.
    // The span does not include the multiplication with the coefficient
    // 
    // Objects of Term and Factor are only used for comparisons / ordering
    public class Term : IComparable<Term> {
      public readonly Expression expr;
      public readonly int start;
      public readonly int end;
      private readonly int[] lengths;

      public IEnumerable<Factor> Factors { get; }

      public Term(Expression expr, int[] lengths, int start, int end) {
        this.expr = expr;
        this.start = start;
        this.end = end;
        this.lengths = lengths;

        var factors = new List<Factor>();
        GetFactorsRec(expr, end, factors, lengths);
        this.Factors = factors;
      }

      private static void GetFactorsRec(Expression expr, int factorIdx, List<Factor> factors, int[] lengths) {
        if (expr[factorIdx] == expr.Grammar.Times) {
          var c = factorIdx - 1; // first child idx
          for (int cIdx = 0; cIdx < expr[factorIdx].Arity; cIdx++) {
            GetFactorsRec(expr, c, factors, lengths);
            c = c - lengths[c];
          }
        } else {
          factors.Insert(0, new Factor(expr, lengths, factorIdx - lengths[factorIdx] + 1, factorIdx)); // TODO: perf
        }
      }

      public int CompareTo(Term? other) {
        if (other == null) throw new ArgumentNullException(nameof(other));

        // lexicographic ordering over factors
        var thisFactorEnum = Factors.GetEnumerator();
        var otherFactorEnum = other.Factors.GetEnumerator();
        while (thisFactorEnum.MoveNext() & otherFactorEnum.MoveNext()) {
          var comp = thisFactorEnum.Current.CompareTo(otherFactorEnum.Current);
          if (comp != 0) return comp;
        }
        // both have the same number of factors
        if (thisFactorEnum.Current == null && otherFactorEnum.Current == null) return 0; // all factors equal and the same number of factors
        else if (thisFactorEnum.Current == null) return -1; // other has more factors
        else if (otherFactorEnum.Current == null) return 1; // this has more factors
        else throw new InvalidProgramException(); // cannot happen
      }

      // for debugging
      public override string ToString() {
        return string.Join(" ", expr.Skip(start).Take(end - start + 1).Select(sy => sy.ToString()));
      }
    }

    public class Factor : IComparable<Factor> {
      public readonly Expression expr;
      private readonly int[] lengths;
      public readonly int start;
      public readonly int end;

      public Factor(Expression expr, int[] lengths, int start, int end) {
        this.expr = expr;
        this.start = start;
        this.lengths = lengths;
        this.end = end;
      }

      public int CompareTo(Factor? other) {
        if (other == null) throw new ArgumentNullException(nameof(other));

        var allSymbols = expr.Grammar.AllSymbols.ToArray();
        var thisOrdNum = Array.IndexOf(allSymbols, expr[end]); // root symbol of factor
        var otherOrdNum = Array.IndexOf(allSymbols, other.expr[other.end]); // root symbol of factor
        return thisOrdNum - otherOrdNum;
      }

      // for debugging
      public override string ToString() {
        return string.Join(" ", expr.Skip(start).Take(end - start + 1).Select(sy => sy.ToString()));
      }
    }
  }


}
