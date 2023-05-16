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
        return Symbol == expr.Grammar.Exp || Symbol == expr.Grammar.Log || Symbol == expr.Grammar.Div 
              || Symbol == expr.Grammar.Cos || Symbol == expr.Grammar.Pow
              || children.Any(c => c.HasNonlinearParameters());
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

      // for debugging
      public override string ToString() {
        return string.Join(" ", expr.Skip(start).Take(end - start + 1).Select(sy => sy.ToString()));
      }
    }
  }


}
