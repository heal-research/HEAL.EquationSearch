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

      var quality = (float)state.Evaluator.OptimizeAndEvaluate(expr, state.Data);
      if (expr != origExpr) {
        // write back optimized parameters
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