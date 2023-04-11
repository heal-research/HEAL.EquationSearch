namespace HEAL.EquationSearch {
  internal class Heuristics {
    public static float PartialMSE(State state) {
      var origExpr = state.Expression;
      Expression expr;
      if (origExpr.IsSentence) {
        expr = origExpr;
      } else {
        expr = state.Grammar.MakeSentence(origExpr);
      }

      if (expr.Length <= 1) return (float)Evaluator.Variance(state.Data.Target); // TODO: remove special case
      else {
        var quality = (float)state.Evaluator.OptimizeAndEvaluate(expr, state.Data);
        if (expr != origExpr) {
          // write back optimized parameters
          // Here we assume that there are no parameters after non-terminals (Assertion in ReplaceAllNtWithParameter)
          for (int i = 0; i < origExpr.Length; i++) {
            if (origExpr[i] is Grammar.ParameterSymbol origParamSy) {
              origParamSy.Value = ((Grammar.ParameterSymbol)expr[i]).Value;
            }
          }
        }
        state.Quality = new MinimizeDouble(quality);
        return quality;
      }
    }
  }
}