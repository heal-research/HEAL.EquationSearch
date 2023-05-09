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

      if (exprQualities.TryGetValue(semHash, out double mse)) {
        // NOTE: parameters of expression are not set in this case
        return mse;
      }


      var model = HEALExpressionBridge.ConvertToExpressionTree(expr, data.VarNames, out var parameterValues);
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;
      
      
      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.Fit(parameterValues, modelLikelihood, maxIterations: 0);
      // successfull?
      if (nlr.ParamEst != null) {
        HEALExpressionBridge.UpdateParameters(expr, nlr.ParamEst);
        mse = nlr.Dispersion * nlr.Dispersion;
        if (double.IsNaN(mse)) mse = double.MaxValue;
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
      var modelLikelihood = this.likelihood.Clone();
      modelLikelihood.ModelExpr = model;

      var nlr = new NonlinearRegression.NonlinearRegression();
      nlr.Fit(parameterValues, modelLikelihood, maxIterations: 0);

      // successful
      if (nlr.ParamEst != null) {
        HEALExpressionBridge.UpdateParameters(expr, nlr.ParamEst);
        var mdl = ModelSelection.MDL(nlr.ParamEst, nlr.Likelihood);
        if (double.IsNaN(mdl)) return double.MaxValue;
        // TODO: MDL potentially simplifies the expression tree and re-fits the parameters.
        // We must therefore update our postfix representation of the expression to report the correct (simplified) result.
        Console.WriteLine($"{-nlr.NegLogLikelihood};{nlr.NegLogLikelihood - nlr.Likelihood.BestNegLogLikelihood(nlr.ParamEst)};{mdl - nlr.Likelihood.BestNegLogLikelihood(nlr.ParamEst)};{nlr.OptReport.Iterations};{nlr.OptReport.NumJacEvals};{nlr.Likelihood.ModelExpr};{expr}");
        return mdl;
      } else {
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