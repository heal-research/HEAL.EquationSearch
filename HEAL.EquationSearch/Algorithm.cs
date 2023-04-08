using TreesearchLib;
namespace HEAL.EquationSearch {

  // TODO:
  // - persistence
  // - stop training anytime (returning best expression so far), and allow to continue running later
  // - Model evaluation results, AIC, BIC, MDL, ...
  // - Model archive or model selection based on MDL
  // - Connection to HEAL.NLR for simplification, prediction intervals, MDL, ...

  public class Algorithm {
    public Expression? BestExpression { get; private set; }
    public double? BestMSE { get; private set; }

    public string[] VariableNames { get; private set; }

    public void Fit(double[,] x, double[] y, string[] varNames, CancellationToken token, Grammar? grammar = null, int maxLength = 20, int depthLimit = 20, double earlyStopQuality = 1e-12) {
      if (x.GetLength(1) != varNames.Length) throw new ArgumentException("number of variables does not match number of columns in x");
      grammar ??= new Grammar(varNames); // default grammar if none supplied by user
      this.VariableNames = (string[])varNames.Clone();
      var data = new Data(varNames, x, y);
      var evaluator = new Evaluator();
      var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .WithCancellationToken(cts.Token)
        .WithImprovementCallback((ctrl, state, quality) => {
          Console.WriteLine($"Found new best solution with {quality} after {ctrl.Elapsed} {ctrl.BestQualityState}");
          if (quality.Value < earlyStopQuality) cts.Cancel(); // early stopping
        })
        .BeamSearch(beamWidth: 10000, Heuristics.PartialMSE /*, depthLimit: depthLimit*/);
        // .BreadthFirst();

      Console.WriteLine($"Quality: {control.BestQuality} nodes: {control.VisitedNodes} ({(control.VisitedNodes / control.Elapsed.TotalSeconds):F2} nodes/sec) " +
        $"VarPro evals: {evaluator.OptimizedExpressions} ({(evaluator.OptimizedExpressions / control.Elapsed.TotalSeconds):F2} nodes/sec) runtime: {control.Elapsed}");

      BestExpression = control.BestQualityState.Expression;
      BestMSE = control.BestQuality.Value.Value;
    }

    public double[] Predict(double[,] x) {
      if (BestExpression == null) throw new InvalidOperationException("Call fit() first.");
      if (x.GetLength(1) != VariableNames.Length) throw new ArgumentException("x has different number of columns than the training dataset");
      var evaluator = new Evaluator();
      var data = new Data(VariableNames, x, new double[x.GetLength(0)]); // no target
      return evaluator.Evaluate(BestExpression, data);
    }
  }
}