﻿using TreesearchLib;
namespace HEAL.EquationSearch {

  // TODO:
  // - persistence
  // - Improve performance of semantic hashing (main bottleneck besides evaluation).
  // - stop training anytime (returning best expression so far), and allow to continue running later
  // - Best-first search implementation in Treesearchlib
  // - Model evaluation results, AIC, BIC, MDL, ...
  // - Model archive or model selection based on MDL
  // - Connection to HEAL.NLR for simplification, prediction intervals, MDL, ...

  public class Algorithm {
    public Expression? BestExpression { get; private set; }
    public double? BestMDL { get; private set; }

    public string[]? VariableNames { get; private set; }

    public void Fit(double[,] x, double[] y, double noiseSigma, string[] varNames, CancellationToken token, Grammar? grammar = null, IEvaluator? evaluator = null,
      int maxLength = 20, int depthLimit = 20, double earlyStopQuality = double.NegativeInfinity, int? randSeed = null) {
      Fit(x, y, Enumerable.Repeat(noiseSigma, y.Length).ToArray(), varNames, token, grammar, evaluator, maxLength, depthLimit, earlyStopQuality, randSeed);
    }

    public void Fit(double[,] x, double[] y, double[] noiseSigma, string[] varNames, CancellationToken token, Grammar? grammar = null, IEvaluator? evaluator = null,
      int maxLength = 20, int depthLimit = 20, double earlyStopQuality = double.NegativeInfinity, int? randSeed = null) {
      if (randSeed.HasValue) SharedRandom.SetSeed(randSeed.Value);
      if (x.GetLength(1) != varNames.Length) throw new ArgumentException("number of variables does not match number of columns in x");
      grammar ??= new Grammar(varNames); // default grammar if none supplied by user

      this.VariableNames = (string[])varNames.Clone();
      var data = new Data(varNames, x, y, invNoiseVariance: noiseSigma.Select(si => 1.0 / (si * si)).ToArray());
      evaluator ??= new AutoDiffEvaluator(new GaussianLikelihood(x, y, modelExpr: null, data.InvNoiseSigma));

      var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
      var control = GraphSearchControl.Start(new State(data, maxLength, grammar, evaluator))
        .WithCancellationToken(cts.Token)
        .WithImprovementCallback((ctrl, state, quality) => {
          var mse = CalculateMSE(state.Expression, data);
          Console.WriteLine($"Found new best solution with {quality} rmse: {Math.Sqrt(mse):g5} after {ctrl.Elapsed} {ctrl.BestQualityState}");
          if (quality.Value < earlyStopQuality) cts.Cancel(); // early stopping
        });

      // Algorithms.BreadthSearch(control, control.InitialState, depth: 0, filterWidth: int.MaxValue, depthLimit: int.MaxValue, nodesReached: int.MaxValue);
      // ConcurrentAlgorithms.ParallelBreadthSearch(control, control.InitialState, depth: 0, filterWidth: int.MaxValue, depthLimit: int.MaxValue, maxDegreeOfParallelism: 16 /*, nodesReached: int.MaxValue*/);
      TreesearchLib.Heuristics.BeamSearch(control, new PriorityBiLevelFIFOCollection<State>(control.InitialState), depth: 0, beamWidth: 1000, Heuristics.PartialMSE, filterWidth: int.MaxValue, depthLimit: int.MaxValue);


      if (control.BestQuality != null) {
        Console.WriteLine($"Quality: {control.BestQuality} nodes: {control.VisitedNodes} ({(control.VisitedNodes / control.Elapsed.TotalSeconds):F2} nodes/sec)\n" +
          $"Evaluations (including cached): {evaluator.EvaluatedExpressions}\n" +
          // $"VarPro evals: {evaluator.OptimizedExpressions} ({(evaluator.OptimizedExpressions / control.Elapsed.TotalSeconds):F2} expr/sec)\n" +
          // $"Evaluator best cached value (MSE): {(evaluator.exprQualities.Values.Any() ? evaluator.exprQualities.Values.Min().ToString("g4") : "no heuristic evaluations")}",
          $"runtime: {control.Elapsed}");

        BestExpression = control.BestQualityState.Expression;
        BestMDL = control.BestQuality.Value.Value;
      }
    }

    private static double CalculateMSE(Expression expression, Data data) {
      var evaluator = new VarProEvaluator();
      var yPred = evaluator.Evaluate(expression, data);
      var ssr = 0.0;
      for (int i = 0; i < yPred.Length; i++) {
        var res = yPred[i] - data.Target[i];
        ssr += res * res;
      }
      return ssr / yPred.Length;
    }

    public double[] Predict(double[,] x) {
      if (BestExpression == null) throw new InvalidOperationException("Call fit() first.");
      if (x.GetLength(1) != VariableNames.Length) throw new ArgumentException("x has different number of columns than the training dataset");
      var evaluator = new VarProEvaluator();
      var data = new Data(VariableNames, x, new double[x.GetLength(0)], new double[x.GetLength(0)]); // no target, or noise variance
      return evaluator.Evaluate(BestExpression, data);
    }
  }
}