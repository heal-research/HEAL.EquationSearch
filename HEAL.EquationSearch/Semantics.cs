using System.Diagnostics;

namespace HEAL.EquationSearch {
  public class Semantics {
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

    private static bool IsCommutative(Grammar grammar, Grammar.Symbol sy) {
      return sy == grammar.Plus || sy == grammar.Times;
    }

    private static bool IsAssociative(Grammar grammar, Grammar.Symbol sy) {
      return sy == grammar.Plus || sy == grammar.Times;
    }

    internal static ulong GetHashValue(Expression expr) {
      return new HashNode(expr).HashValue;
    }



    // This class is used for nodes within a tree that is used for calculating semantic hashes for expressions.
    // The tree is build recursively when calling the constructor for the expression.
    // The semantic hash function has the following capabilities:
    //   - flatten out the sub-trees for associative expressions (x1 ° x2) ° x3 => x1 ° x2 ° x3 (a single node with three children)
    //   - order sub-expressions of commutative expressions x2 ° x3 ° x1 => x1 ° x2 ° x3. The ordering is deterministic and based on the hash values of subexpressions.
    // The steps make sure that most of the semantically equivalent expressions have the same hash value.
    // We do not yet handle distributive operators p1 * x1 + p2 * x1 => p3 * x1. This is necessary to prevent duplicate terms.
    public class HashNode {
      public readonly Expression expr;
      private readonly int[] lengths;
      public readonly int start;
      public readonly int end;

      private readonly List<HashNode> children;
      public IEnumerable<HashNode> Children { get { return children; } }
      public Grammar.Symbol Symbol => expr[end];

      public ulong HashValue { get; private set; }

      // constructor for the root nodes
      public HashNode(Expression expr) : this(expr, GetLengths(expr), 0, expr.Length - 1) { }

      // constructor for sub-expressions (sub-trees)
      // The full tree of HashNodes for this expression is build bottom-up in the constructor.
      private HashNode(Expression expr, int[] lengths, int start, int end) {
        this.expr = expr;
        this.lengths = lengths;
        this.start = start;
        this.end = end;
        this.children = new List<HashNode>();
        if (expr[end].Arity > 0) {
          GetChildrenRec(children, end); // collect all children of this node
          if (IsCommutative(expr.Grammar, Symbol)) children.Sort(HashValueComparer);
          Simplify();
        }

        var parameterHash = CalculateHashValue(expr.Grammar.Parameter);

        if (IsAssociative(expr.Grammar, Symbol)) { // remove multiple occurrences of parameters in "flattened" children 
          while (children.Count(c => c.HashValue == parameterHash) > 1) {
            children.Remove(children.First(c => c.HashValue == parameterHash));
          }
        }
        
        if (children.Any() && children.TrueForAll(c => c.HashValue == parameterHash || c.Symbol == expr.Grammar.One)) { // reduce subtrees only with parameters to one single parameter (for ESR only).
          HashValue = parameterHash;
          return;
        }
        if (expr[end].Arity == 2 && children.Count == 1) { // "Remove" binary operators that are reduced to a single argument
          HashValue = children[0].HashValue;
          return;
        }

        if (expr[end] == expr.Grammar.Div) {
          if (children[0].HashValue == children[1].HashValue) {// remove "x / x"
            HashValue = parameterHash;
            return;
          }

          // make sure, x/0 == x*0
          if (children[0].Symbol != this.expr.Grammar.One && children[1].HashValue == parameterHash) { // denominator is on index 0
            expr.SymbolString[end] = expr.Grammar.Times;
            children.Sort(HashValueComparer); // multiplication is commutative.
            HashValue = CalculateHashValue();
            return;
          }

          var isNestedDiv = children[1].Symbol == expr.Grammar.Div;
          var isNominatorOne = children[0].Symbol == expr.Grammar.One ||
                               children[0].HashValue == parameterHash;
          if (isNestedDiv && isNominatorOne) { // is nested div?
            var childDiv = children[1];
            var isChildNominatorOne = childDiv.children[0].Symbol == expr.Grammar.One ||
                                      childDiv.children[0].HashValue == parameterHash;
            if (isChildNominatorOne) {
              HashValue = childDiv.HashValue;
              return;
            }
          }
        }
        
        HashValue = CalculateHashValue();
      }

      // for sorting children by hashvalue above
      private int HashValueComparer(HashNode x, HashNode y) {
        if (x.HashValue < y.HashValue) return -1;
        else if (x.HashValue == y.HashValue) return 0;
        else return 1;
      }

      private void Simplify() {
        // remove all multiplications by one
        if (Symbol == expr.Grammar.Times) {
          for (int i = children.Count - 1; i >= 0; i--) {
            if (children[i].Symbol == expr.Grammar.One) children.RemoveAt(i);
          }
        }

        // remove duplicate terms (which have the same hash value)
        // Children are already sorted by hash value
        if (Symbol == expr.Grammar.Plus) {
          var c = 0;
          while (c < children.Count - 1) {
            Debug.Assert(children[c].HashValue <= children[c + 1].HashValue); // ASSERT: list sorted

            // If the terms contain non-linear parameters we allow duplicates.
            // e.g. we keep "p log(p + p x) + p log(p + p x)" or "p exp(p x) + p exp(p x)
            if (children[c].HashValue == children[c + 1].HashValue && !children[c].HasNonlinearParameters()) {
              children.RemoveAt(c + 1);
            } else {
              c++;
            }
          }
        }
      }

      private bool HasNonlinearParameters() {
        return 
          (Symbol == expr.Grammar.Exp || 
           Symbol == expr.Grammar.Log || 
           Symbol == expr.Grammar.Div || 
           Symbol == expr.Grammar.Cos || 
           Symbol == expr.Grammar.Pow)
              && children.Any(c => c.HasParameter());
      }

      private bool HasParameter() {
        if (Symbol == expr.Grammar.Parameter) return true;
        return children.Any(c => c.HasParameter());
      }

      private void GetChildrenRec(List<HashNode> children, int parentIndex) {
        var c = parentIndex - 1; // first child idx
        for (int cIdx = 0; cIdx < expr[parentIndex].Arity; cIdx++) {
          // collect all children recursively for associative operations
          if (IsAssociative(expr.Grammar, expr[parentIndex]) && expr[parentIndex] == expr[c]) {
            GetChildrenRec(children, c);
          } else {
            children.Add(new HashNode(expr, lengths, start: c - lengths[c] + 1, end: c));
          }
          c = c - lengths[c];
        }
        if (expr[parentIndex].Arity == 0) {
          children.Add(new HashNode(expr, lengths, start: parentIndex - lengths[parentIndex] + 1, end: parentIndex));
        }
      }

      internal ulong CalculateHashValue() {
        var hashValues = new ulong[children.Count];
        for (int i = 0; i < children.Count; i++) {
          hashValues[i] = children[i].HashValue;
        }
        return Hash.JSHash(hashValues, (ulong)expr[end].GetHashCode()); // hash values of children, followed by hash value of current symbol
      }

      internal ulong CalculateHashValue(Grammar.Symbol symbol) {
        return Hash.JSHash(Array.Empty<ulong>(), (ulong)symbol.GetHashCode());
      }

      // for debugging
      public override string ToString() {
        return string.Join(" ", expr.Skip(start).Take(end - start + 1).Select(sy => sy.ToString()));
      }
    }
  }


}
