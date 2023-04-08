namespace HEAL.EquationSearch {
  internal class Heuristics {
    public static float PartialMSE(State state) {
      var origExpr = state.Expression;
      var expr = ReplaceAllNtWithParameter(origExpr);
      if (expr.Length <= 1) return (float)state.Evaluator.Variance(state.Data.Target); // TODO: remove special case
      else {
        var quality = (float)state.Evaluator.OptimizeAndEvaluate(expr, state.Data);
        // write back optimized parameters
        // Here we assume that there are no parameters after non-terminals (Assertion in ReplaceAllNtWithParameter)
        for (int i = 0; i < origExpr.Length; i++) {
          if (origExpr[i] is Grammar.ParameterSymbol origParamSy) {
            origParamSy.Value = ((Grammar.ParameterSymbol)expr[i]).Value;
          }
        }
        state.Quality = new MinimizeDouble(quality);
        return quality;
      }
    }

    private static Expression ReplaceAllNtWithParameter(Expression expr) {
      var ntIdx = expr.FirstIndexOfNT();
#if DEBUG
      // ASSERT: no parameter after the first NT
      if (ntIdx >= 0) {
        for (int i = ntIdx; i < expr.Length; i++) {
          if (expr[i] is Grammar.ParameterSymbol) throw new InvalidProgramException();
        }
      }
#endif
      while (ntIdx >= 0) {
        expr = expr.Replace(ntIdx, expr.Grammar.GetDefaultReplacment(expr[ntIdx]));
        ntIdx = expr.FirstIndexOfNT();
      }
      return expr;
    }
  }
}