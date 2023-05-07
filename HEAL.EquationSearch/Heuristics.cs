namespace HEAL.EquationSearch {
  internal class Heuristics {
    // private static IEvaluator evaluator = new VarProEvaluator();
    public static float PartialMSE(State state) {
      var origExpr = state.Expression;
      Expression expr;
      if (origExpr.IsSentence) {
        expr = origExpr;
      } else {
        expr = state.Grammar.MakeSentence(origExpr);
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