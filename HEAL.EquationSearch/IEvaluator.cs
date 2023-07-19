namespace HEAL.EquationSearch {
  public interface IEvaluator {
    long OptimizedExpressions { get; }
    long EvaluatedExpressions { get; }

    double OptimizeAndEvaluateMSE(Expression expr, Data data);

    double OptimizeAndEvaluateDL(Expression expr, Data data);

    double[] Evaluate(Expression expression, Data data);
  }
}