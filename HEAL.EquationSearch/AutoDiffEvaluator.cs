using System.ComponentModel.Design;
using System.Linq.Expressions;
using HEAL.Expressions;
using HEAL.NonlinearRegression;
using HEAL.NonlinearRegression.Likelihoods;

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

    // TODO: make iterations configurable
    // This method uses caching for efficiency.
    // IMPORTANT: This method does not update parameter values in expr. Use for heuristic evaluation only.
    public double OptimizeAndEvaluateMSE(Expression expr, Data data, int iterations = 100) {
      var semHash = Semantics.GetHashValue(expr);
      Interlocked.Increment(ref evaluatedExpressions);

      if (exprQualities.TryGetValue(semHash, out double mse)) {
        // NOTE: parameters of expression are not set in this case
        return mse;
      }

      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);

      var likelihood = new SimpleGaussianLikelihood(data.X, data.Target, model, noiseSigma: 1.0);

      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.Fit(parameterValues, likelihood, maxIterations: iterations);
      // successfull?
      if (nlr.ParamEst != null) {
        HEALExpressionBridge.UpdateParameters(expr, nlr.ParamEst);
        mse = nlr.Dispersion * nlr.Dispersion;
      } else {
        mse = double.MaxValue;
      }

      exprQualities.GetOrAdd(semHash, mse);
      return mse;
    }

    // TODO: make iterations configurable
    // This method always optimizes parameters in expr but does not use caching to make sure all parameters of the evaluated expressions are set correctly.
    // Use this method to optimize the best solutions (found via MSE)
    public double OptimizeAndEvaluateMDL(Expression expr, Data data, int iterations = 100) {
      Interlocked.Increment(ref evaluatedExpressions);

      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var likelihood = new SimpleGaussianLikelihood(data.X, data.Target, model, noiseSigma: 1.0);

      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.Fit(parameterValues, likelihood, maxIterations: 0);
      
      // successful
      if (nlr.ParamEst != null) {
        HEALExpressionBridge.UpdateParameters(expr, nlr.ParamEst);
        var mdl = ModelSelection.MDL(nlr.Likelihood.ModelExpr, nlr.ParamEst, -nlr.NegLogLikelihood, nlr.LaplaceApproximation.diagH);
        // Console.WriteLine($"{-nlr.NegLogLikelihood} {mdl} {nlr.Likelihood.ModelExpr} {expr}");
        return mdl;
      } else {
        return double.MaxValue;
      }
    }


    public double[] Evaluate(Expression expr, Data data) {
      Interlocked.Increment(ref evaluatedExpressions);

      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var likelihood = new SimpleGaussianLikelihood(data.X, data.Target, model, noiseSigma: 1.0);

      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.SetModel(parameterValues, likelihood);
      return nlr.Predict(data.X);
    }

  }
}