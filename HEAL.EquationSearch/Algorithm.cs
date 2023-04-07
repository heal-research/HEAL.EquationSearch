using TreesearchLib;
namespace HEAL.EquationSearch {

  // TODO:
  // - persistence
  // - Heuristics to allow usage of heuristic search algorithms (PILOT, Beam, ...) in Treesearchlib
  // - Model evaluation results, AIC, BIC, MDL, ...
  // - Model archive or model selection based on MDL
  // - Store best model(s) in fit.
  // - Use best model in Predict.
  // - Connection to HEAL.NLR for simplification, prediction intervals, MDL, ...
  // - Evaluator non-static

  public class Algorithm {

    public void Fit(double[,] x, double[] y, string[] varNames, CancellationToken token, Grammar? grammar = null, int maxLength = 20, int depthLimit = 20, double earlyStopQuality = 1e-12) {
      grammar ??= new Grammar(varNames); // default grammar if none supplied by user
      var data = new Data(varNames, x, y);
      var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar))
        .WithCancellationToken(cts.Token)
        .WithImprovementCallback((ctrl, state, quality) => {
          Console.WriteLine($"Found new best solution with {quality} after {ctrl.Elapsed} {ctrl.BestQualityState}");
          if (quality.Value < earlyStopQuality) cts.Cancel(); // early stopping
        })
        .BreadthFirst(depthlimit: depthLimit);

      Console.WriteLine($"Quality: {control.BestQuality} nodes: {control.VisitedNodes} ({(control.VisitedNodes / control.Elapsed.TotalSeconds):F2} nodes/sec) runtime: {control.Elapsed}");
    }

    public double[] Predict(double[,] x) {
      // TODO
      throw new NotImplementedException();
    }
  }
}