namespace HEAL.EquationSearch {
  internal class Heuristics {
    public static float PartialMSE(State state) {
      var origExpr = state.Expression;
      Expression expr;
      if (origExpr.IsSentence) {
        return 0.0f; // sentences should always be visited (evaluated) first
      } else {
        expr = state.Grammar.ReplaceAllNTWithDefaults(origExpr);
      }

      var mse = (float)state.Evaluator.OptimizeAndEvaluateMSE(expr, state.Data);

      // write back optimized parameters into original expression (to improve starting points for the derived expressions)
      if (expr != origExpr) {
        for (int i = 0; i < origExpr.Length; i++) {
          if (origExpr[i] is Grammar.ParameterSymbol origParamSy) {
            origParamSy.Value = ((Grammar.ParameterSymbol)expr[i]).Value;
          }
        }
      }

      return mse;
    }
  }
}