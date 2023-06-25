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
      for (int restart = 0; restart < 20; restart++) {
        nlr.Fit(parameterValues, modelLikelihood, maxIterations: iterations /* , scale: scale, diagHess: prec*/);
        // successful?
        if (nlr.ParamEst != null && !nlr.ParamEst.Any(double.IsNaN) && nlr.NegLogLikelihood < bestNLL) {
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
    public double OptimizeAndEvaluateDL(Expression expr, Data data, int iterations = 1000) {
      Interlocked.Increment(ref evaluatedExpressions);
      System.Console.WriteLine(expr);
      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;

      var bestNLL = double.MaxValue;
      double[] bestParam = (double[])parameterValues.Clone();

      var nlr = new NonlinearRegression.NonlinearRegression();

      // TODO: Harrys restart method
      for (int restart = 0; restart < 20; restart++) {
        nlr.Fit(parameterValues, modelLikelihood, maxIterations: iterations);
        // successful?
        if (nlr.ParamEst != null && !nlr.ParamEst.Any(double.IsNaN) && nlr.NegLogLikelihood < bestNLL) {
          if (nlr.LaplaceApproximation == null) {
            System.Console.Error.WriteLine("Hessian not positive semidefinite after fitting");
            continue;
          }
          bestNLL = nlr.NegLogLikelihood;
          bestParam = (double[])nlr.ParamEst.Clone();
          // bestLaplaceApproximation = nlr.LaplaceApproximation;
          // nlr.WriteStatistics();
        }
        for (int i = 0; i < parameterValues.Length; i++) {
          parameterValues[i] = Random.Shared.NextDouble() * 10 - 5;  // TODO: shared random
        }
      }

      if (bestNLL == double.MaxValue) return double.MaxValue;

      HEALExpressionBridge.UpdateParameters(expr, bestParam);

      try {
        var dl = ModelSelection.DLWithIntegerSnap(bestParam, modelLikelihood);
        // bestLaplaceApproximation.GetParameterIntervals(0.01, out var low, out var high);
        // for(int i=0;i<low.Length;i++) {
        //   System.Console.WriteLine($"{bestParamEst[i]:g4} {low[i]:g4} {high[i]:g4}");
        // }
        if (double.IsNaN(dl)) return double.MaxValue;

        // modelLikelihood.ModelExpr = bestExpr;
        // bestNLL = modelLikelihood.NegLogLikelihood(bestParamEst);

        // TODO: MDL potentially simplifies the expression tree and re-fits the parameters.
        // We must therefore update our postfix representation of the expression to report the correct (simplified) result.
        // Console.WriteLine($"{mdl};{-bestNLL};{bestExpr};{string.Join(";", bestParamEst.Select(pi => pi.ToString("g4")))}");
        return dl;
      } catch (Exception e) {
        System.Console.Error.WriteLine(e.Message);
        return double.MaxValue;
      }
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