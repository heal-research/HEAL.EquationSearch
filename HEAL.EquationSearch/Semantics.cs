using System.Diagnostics;
using HEAL.Expressions;
using static HEAL.EquationSearch.Grammar;

namespace HEAL.EquationSearch {
  // TODO: refactoring
  // - HashNode should not reference expr, lengths, start or end
  // - move all simplification / transformation of HashNode tree out of ctor into Simplify
  // - closer connection to grammar (to ensure all nonlinear functions are handled, ...). Changes of grammar almost always imply changes in Semantics
  // - implement semantic hashing and simplification generally (without asserting restricted grammar)
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

    // x1 ° x2 = x2 ° x1
    private static bool IsCommutative(Grammar grammar, Symbol sy) {
      return sy == grammar.Plus || sy == grammar.Times;
    }

    // (x1 ° x2) ° x3 = x1 ° (x2 ° x3)
    private static bool IsAssociative(Grammar grammar, Symbol sy) {
      return sy == grammar.Plus || sy == grammar.Times;
    }

    internal static ulong GetHashValue(Expression expr) {
      var tree = Simplify(new HashNode(expr));
      return tree.HashValue;
    }

    internal static HashNode Simplify(HashNode tree) {
      // simplify children recursively
      var originalChildren = tree.children.ToArray();
      tree.children.Clear();
      foreach (var ch in originalChildren) {
        tree.children.Add(Simplify(ch));
      }
      if (IsCommutative(tree.expr.Grammar, tree.Symbol)) tree.children.Sort(tree.HashValueComparer);

      // fold parameters
      if (tree.children.Count > 0 && tree.children.All(ch => ch.Symbol is ParameterSymbol)) {
        return tree.children.First(); // use first parameter as replacement
      }

      // x inv inv -> inv
      if (tree.Symbol == tree.expr.Grammar.Inv && tree.children[0].Symbol == tree.expr.Grammar.Inv) {
        return tree.children[0].children[0];
      }

      // remove all multiplications by one
      if (tree.Symbol == tree.expr.Grammar.Times) {
        for (int i = tree.children.Count - 1; i >= 0; i--) {
          if (tree.children[i].Symbol == tree.expr.Grammar.One) tree.children.RemoveAt(i);
        }
        return tree;
      }

      // remove duplicate terms (which have the same hash value)
      // Children are already sorted by hash value
      if (tree.Symbol == tree.expr.Grammar.Plus) {
        var c = 0;
        while (c < tree.children.Count - 1) {
          Debug.Assert(tree.children[c].HashValue <= tree.children[c + 1].HashValue); // ASSERT: list sorted

          // If the terms contain non-linear parameters we allow duplicates.
          // e.g. we keep "p log(p + p x) + p log(p + p x)" or "p exp(p x) + p exp(p x)
          if (tree.children[c].HashValue == tree.children[c + 1].HashValue && !tree.children[c].HasNonlinearParameters()) {
            tree.children.RemoveAt(c + 1);
          } else {
            c++;
          }
        }
        return tree;
      }

      return tree; // no changes
    }

    // This class is used for nodes within a tree that is used for calculating semantic hashes for expressions.
    // The tree is build recursively when calling the constructor for the expression.
    // The semantic hash function has the following capabilities:
    //   - flatten out the sub-trees for associative expressions (x1 ° x2) ° x3 => x1 ° x2 ° x3 (a single node with three children)
    //   - order sub-expressions of commutative expressions x2 ° x3 ° x1 => x1 ° x2 ° x3. The ordering is deterministic and based on the hash values of subexpressions.
    //   - ignore negation operator (x * (-1))
    // The steps make sure that most of the semantically equivalent expressions have the same hash value.
    internal class HashNode {
      public readonly Expression expr;
      internal readonly int[] lengths;
      public readonly int start;
      public readonly int end;

      internal readonly List<HashNode> children;
      public IEnumerable<HashNode> Children { get { return children; } }
      public Symbol Symbol => expr[end];

      private ulong? hashValue;
      public ulong HashValue {
        get { if (!hashValue.HasValue) { hashValue = CalculateHashValue(); } return hashValue.Value; }
        private set { hashValue = value; }
      }

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
        }
      }

      // for sorting children by hashvalue above
      internal int HashValueComparer(HashNode x, HashNode y) {
        if (x.HashValue < y.HashValue) return -1;
        else if (x.HashValue == y.HashValue) return 0;
        else return 1;
      }



      internal bool HasNonlinearParameters() {
        return Symbol == expr.Grammar.Exp || Symbol == expr.Grammar.Log || Symbol == expr.Grammar.Inv
              || Symbol == expr.Grammar.Cos || Symbol == expr.Grammar.Pow || Symbol == expr.Grammar.Sqrt
              || Symbol == expr.Grammar.PowAbs
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
        if (expr[end] == expr.Grammar.Neg)
          return hashValues[0]; // neg is a linear operation and can be ignored
        else
          return Hash.JSHash(hashValues, (ulong)expr[end].GetHashCode()); // hash values of children, followed by hash value of current symbol
      }

      // for debugging
      public override string ToString() {
        return string.Join(" ", expr.Skip(start).Take(end - start + 1).Select(sy => sy.ToString()));
      }
    }
  }


}
