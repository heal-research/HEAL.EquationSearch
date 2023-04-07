namespace HEAL.EquationSearch {
  
  // TODO: This is not used yet
  // Potentially can be removed if the code in Semantics is sufficient to remove semantic duplicates.
  // So far we do not need to calculate hash values for expressions.
  internal class SemanticHash {

    public static ulong GetHashValue(Expression expression) => GetHashValues(expression).Last();
    public static ulong[] GetHashValues(Expression expr) {

      var lengths = Semantics.GetLengths(expr);
      var nodeHashValues = new ulong[expr.Length];
      var exprHashValues = new ulong[expr.Length];

      // give each parameter a uniq hash-value based on their order
      var paramHash = new System.Random(1234);

      for (int i = 0; i < expr.Length; i++) {
        if (expr[i] is Grammar.ParameterSymbol) {
          nodeHashValues[i] = (ulong)paramHash.Next();
        } else if (expr[i] is Grammar.VariableSymbol varSy) {
          nodeHashValues[i] = (ulong)varSy.VariableName.GetHashCode();
        } else {
          nodeHashValues[i] = (ulong)expr[i].Name.GetHashCode();
        }

        exprHashValues[i] = ComputeHash(expr, lengths, exprHashValues, nodeHashValues, i);
      }

      return exprHashValues;
    }

    // A bitwise hash function written by Justin Sobel. hash functions adapted from http://partow.net/programming/hashfunctions/index.html#AvailableHashFunctions 
    private static ulong JSHash(byte[] input) {
      ulong hash = 1315423911;
      for (int i = 0; i < input.Length; ++i)
        hash ^= (hash << 5) + input[i] + (hash >> 2);
      return hash;
    }

    public static ulong ComputeHash(Expression expr, int[] lengths, ulong[] exprHashValues, ulong[] nodeHashValues, int i) {
      var sy = expr[i];
      const int size = sizeof(ulong);
      var childHashes = new ulong[sy.Arity + 1];
      var bytes = new byte[(sy.Arity + 1) * size];

      for (int j = i - 1, k = 0; k < sy.Arity; ++k, j -= lengths[j]) {
        childHashes[k] = exprHashValues[j];
      }
      childHashes[sy.Arity] = nodeHashValues[i];
      if (IsCommutative(expr.Grammar, sy)) Array.Sort(childHashes, 0, sy.Arity);
      Buffer.BlockCopy(childHashes, 0, bytes, 0, bytes.Length);
      return JSHash(bytes);
    }

    private static bool IsCommutative(Grammar grammar, Grammar.Symbol sy) {
      return sy == grammar.Plus || sy == grammar.Times;
    }

  }
}
