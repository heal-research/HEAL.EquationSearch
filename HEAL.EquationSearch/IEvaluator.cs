namespace HEAL.EquationSearch {
  public interface IEvaluator {
    long OptimizedExpressions { get; }
    long EvaluatedExpressions { get; }

    double OptimizeAndEvaluateMSE(Expression expr, Data data, int iterations = 10);

    double OptimizeAndEvaluateMDL(Expression expr, Data data, int iterations = 10);

    double[] Evaluate(Expression expression, Data data);
  }
}