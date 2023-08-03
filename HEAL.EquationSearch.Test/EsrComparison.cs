using TreesearchLib;

namespace HEAL.EquationSearch.Test {

  // Tests for comparison of the size of the hypothesis space and the quality of expressions of EQS to Exhaustive Symbolic Regression (ESR)
  [TestClass]
  public class EsrComparison {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [DataTestMethod]
    [DataRow(50)]
    public void FullEnumerationUnivariateReducedGrammar(int maxLength) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x1" };
      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var evaluator = new CollectExpressionsEvaluator();

      // we do not require any data
      var trainX = new double[0, inputs.Length];
      var trainY = new double[0];
      var trainInvNoiseSigma = new double[0];
      var data = new Data(inputs, trainX, trainY, trainInvNoiseSigma);

      // do not use GraphSearchControl here because it already detects duplicate states
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator));

      Algorithms.BreadthSearch(control, control.InitialState, depth: 0, filterWidth: int.MaxValue, depthLimit: int.MaxValue, nodesReached: int.MaxValue);
      evaluator.WriteAllExpressions(@"c:\temp\allexpressions_1d.txt");

      // get number of total expressions and uniq expressions
      // mlr --csv --fs ";" --from allexpressions_1d.txt stats1 -a sum,count -f numExprs -g minLen then sort -n minLen
      /*

       */
    }

    [DataTestMethod]
    [DataRow(20)]
    public void FullEnumerationBivariateReducedGrammar(int maxLength) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x1", "x2" };
      var grammar = new Grammar(inputs);
      grammar.UseLogExpPowRestrictedRules();

      var evaluator = new CollectExpressionsEvaluator();

      // we do not require any data
      var trainX = new double[0, inputs.Length];
      var trainY = new double[0];
      var trainInvNoiseSigma = new double[0];
      var data = new Data(inputs, trainX, trainY, trainInvNoiseSigma);

      // do not use GraphSearchControl here because it already detects duplicate states
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator));

      Algorithms.BreadthSearch(control, control.InitialState, depth: 0, filterWidth: int.MaxValue, depthLimit: int.MaxValue, nodesReached: int.MaxValue);
      evaluator.WriteAllExpressions(@"c:\temp\allexpressions_2d.txt");
    }

    private class CollectExpressionsEvaluator : IEvaluator {
      public CollectExpressionsEvaluator() {
      }

      public long OptimizedExpressions => throw new NotImplementedException();

      public long EvaluatedExpressions => allExpressions.Values.Sum(l => l.Count);

      public Dictionary<ulong, List<Expression>> allExpressions = new();

      public double[] Evaluate(Expression expression, Data data) {
        throw new NotImplementedException();
      }

      public double OptimizeAndEvaluateDL(Expression expr, Data data) {
        var h = Semantics.GetHashValue(expr);
        if (allExpressions.TryGetValue(h, out var list)) {
          list.Add(expr);
        } else {
          allExpressions.Add(h, new List<Expression>() { expr });
        }
        return 0.0;
      }

      public double OptimizeAndEvaluateMSE(Expression expr, Data data) {
        throw new NotImplementedException();
      }

      internal void WriteAllExpressions(string filename) {
        using (var writer = new StreamWriter(filename, false)) {
          writer.WriteLine("minLen;numExprs;shortestExpr;expressions");
          foreach (var kvp in allExpressions) {
            var shortestExpr = kvp.Value.OrderBy(e => e.Length).First();
            writer.WriteLine($"{shortestExpr.Length};" +
              $"{kvp.Value.Count};" +
              $"{shortestExpr.ToInfixString(includeParamValues: false)};" +
              $"{string.Join("\t", kvp.Value.Select(expr => expr.ToInfixString(includeParamValues: false)))}");
          }
        }
      }
    }
  }
}