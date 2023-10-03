namespace HEAL.EquationSearch.Test {
  // for checking the number of sentences in full enumeration
  internal class CountUniqueExpressionsEvaluator : IEvaluator {
    // only counts the number of evaluations
    public CountUniqueExpressionsEvaluator()  { }

    public long OptimizedExpressions => throw new NotSupportedException();

    public long EvaluatedExpressions => evaluatedExpressions.Count;

    private HashSet<ulong> evaluatedExpressions = new HashSet<ulong>();


    public double[] Evaluate(Expression expression, Data data) {
      throw new NotSupportedException();
    }

    public double OptimizeAndEvaluateDL(Expression expr, Data data) {
      if(evaluatedExpressions.Add(Semantics.GetHashValue(expr, out var simplifiedExpr))) {
        System.Console.WriteLine(simplifiedExpr.ToInfixString());
      }
      return 0.0;
    }

    public double OptimizeAndEvaluateMSE(Expression expr, Data data) {
      throw new NotSupportedException();
    }
  }
}