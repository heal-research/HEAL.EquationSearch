using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch {
  public class AutoDiffEvaluator : IEvaluator {
    private long optimizedExpressions = 0;
    private long evaluatedExpressions = 0;
    public long OptimizedExpressions => optimizedExpressions;

    public long EvaluatedExpressions => evaluatedExpressions;

    // The caches in GraphSearchControl and Evaluator have different purposes.
    // The cache in GraphSearchControl prevents visiting duplicate states in the state graph.
    // The cache in Evaluator prevents duplicate evaluations. 
    // Currently, they are both necessary because GraphSearchControl calculates
    // semantic hashes for expressions with nonterminal symbols (this is necessary to distinguish terminal states from nonterminal states),
    // while the cache in Evaluator only sees expressions where nonterminal symbols have been replaced by terminal symbols.
    public NonBlocking.ConcurrentDictionary<ulong, double> exprQualities = new();
    private readonly LikelihoodBase likelihood;

    public AutoDiffEvaluator(LikelihoodBase likelihood) {
      this.likelihood = likelihood;
    }

    // TODO: make iterations configurable
    // This method uses caching for efficiency.
    // IMPORTANT: This method does not update parameter values in expr. Use for heuristic evaluation only.
    public double OptimizeAndEvaluateMSE(Expression expr, Data data, int iterations = 100) {
      var semHash = Semantics.GetHashValue(expr);
      Interlocked.Increment(ref evaluatedExpressions);

      if (exprQualities.TryGetValue(semHash, out double nll)) {
        // NOTE: parameters of expression are not set in this case
        return nll;
      }


      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;


      var nlr = new NonlinearRegression.NonlinearRegression();

      var bestNLL = double.MaxValue;

      // default scale and precision is one
      // var scale = Enumerable.Repeat(1.0, parameterValues.Length).ToArray();
      // var prec = Enumerable.Repeat(1.0, parameterValues.Length).ToArray(); 
      for (int restart = 0; restart < 10; restart++) {
        nlr.Fit(parameterValues, modelLikelihood, maxIterations: 0 /* , scale: scale, diagHess: prec*/);
        // successful?
        if (nlr.ParamEst != null && nlr.NegLogLikelihood < bestNLL) {
          bestNLL = nlr.NegLogLikelihood;

          // use scale and precision based on this estimation to hopefully speed up next iteration
          // prec = (double[])nlr.LaplaceApproximation.diagH.Clone();
          // scale = nlr.ParamEst.Select((pi,i) => pi / Math.Sqrt(prec[i])).ToArray();

          HEALExpressionBridge.UpdateParameters(expr, nlr.ParamEst);
        }

        // try random restart
        for (int i = 0; i < parameterValues.Length; i++) {
          parameterValues[i] = Random.Shared.NextDouble() * 2 - 1;  // TODO: shared random
        }
      }



      exprQualities.GetOrAdd(semHash, bestNLL);
      return bestNLL;
    }

    // TODO: make iterations configurable
    // This method always optimizes parameters in expr but does not use caching to make sure all parameters of the evaluated expressions are set correctly.
    // Use this method to optimize the best solutions (found via MSE)
    public double OptimizeAndEvaluateMDL(Expression expr, Data data, int iterations = 100) {
      Interlocked.Increment(ref evaluatedExpressions);

      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;

      var bestNLL = double.MaxValue;
      double[] bestParam = (double[])parameterValues.Clone();

      // default scale and precision is one
      // var scale = Enumerable.Repeat(1.0, parameterValues.Length).ToArray();
      // var prec = Enumerable.Repeat(1.0, parameterValues.Length).ToArray(); 
      var nlr = new NonlinearRegression.NonlinearRegression();
      for (int restart = 0; restart < 10; restart++) {
        nlr.Fit(parameterValues, modelLikelihood, maxIterations: 0 /* , scale: scale, diagHess: prec*/);
        // successful?
        if (nlr.ParamEst != null && nlr.NegLogLikelihood < bestNLL) {
          bestNLL = nlr.NegLogLikelihood;
          bestParam = (double[])nlr.ParamEst.Clone();

        }
        for (int i = 0; i < parameterValues.Length; i++) {
          parameterValues[i] = Random.Shared.NextDouble() * 2 - 1;  // TODO: shared random
        }
      }

      HEALExpressionBridge.UpdateParameters(expr, bestParam);
      // try random restart
      var mdl = ModelSelection.MDL(bestParam, modelLikelihood);

      if (double.IsNaN(mdl)) return double.MaxValue;
      // TODO: MDL potentially simplifies the expression tree and re-fits the parameters.
      // We must therefore update our postfix representation of the expression to report the correct (simplified) result.
      Console.WriteLine($"{mdl};{-bestNLL};{expr.ToInfixString()}");
      return mdl;

      // use scale and precision based on this estimation to hopefully speed up next iteration
      // prec = (double[])nlr.LaplaceApproximation.diagH.Clone();
      // scale = nlr.ParamEst.Select((pi,i) => pi / Math.Sqrt(prec[i])).ToArray();


    }


    public double[] Evaluate(Expression expr, Data data) {
      Interlocked.Increment(ref evaluatedExpressions);

      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;


      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.SetModel(parameterValues, modelLikelihood);
      return nlr.Predict(data.X);
    }


  }
}