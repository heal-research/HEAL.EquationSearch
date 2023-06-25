namespace HEAL.EquationSearch {
  public interface IEvaluator {
    long OptimizedExpressions { get; }
    long EvaluatedExpressions { get; }

    double OptimizeAndEvaluateMSE(Expression expr, Data data, int iterations = 100);

    double OptimizeAndEvaluateDL(Expression expr, Data data, int iterations = 100);

    double[] Evaluate(Expression expression, Data data);
  }
}