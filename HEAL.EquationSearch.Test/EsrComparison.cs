using TreesearchLib;
using static alglib;

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
    [DataRow(30)]
    public void FullEnumerationUnivariateReducedGrammar(int maxLength) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x1" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseLogExpPowRestrictedRules();


      Enumerate(grammar, maxLength, @"c:\temp\allExpressions_1d.txt");

      // get number of total expressions and uniq expressions
      // mlr --csv --fs ";" --from allexpressions_1d.txt stats1 -a sum,count -f numExprs -g minLen then sort -n minLen
      /*
        minLen;numExprs_sum;numExprs_count
        1;1;1
        5;7;1
        7;4;1
        8;1;1
        9;5;2
        10;5;3
        11;81;4
        12;36;7
        13;100;8
        14;147;16
        15;187;18
        16;397;35
        17;435;37
        18;1135;75
        19;1198;82
        20;2840;157
        21;3382;174
        22;6887;326
        23;8054;380
        24;15728;668
        25;22333;817
        26;35460;1362
        27;52557;1759
        28;84787;2772
        29;108327;3744
        30;65592;5640
       */
    }

    [DataTestMethod]
    [DataRow(30)]
    public void FullEnumerationBivariateReducedGrammar(int maxLength) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x1", "x2" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseLogExpPowRestrictedRules();

      Enumerate(grammar, maxLength, @"c:\temp\allExpressions_2d.txt");

      // get number of total expressions and uniq expressions
      // mlr --csv --fs ";" --from allexpressions_2d.txt stats1 -a sum,count -f numExprs -g minLen then sort -n minLen

    }


    [DataTestMethod]
    [DataRow(10)]
    public void FullEnumerationUnivariateESRCosmicGrammar(int maxLength) {
      // TODO:
      //  - simplification rules in semantics
      //    - constant / parameter folding
      //    - repeated inv or neg 
      //    - ... mul inv(x) (multiplication with inverse = division)
      //    - ... plus neg(x) (addition of negative = subtraction)
      //  - interpretation of '/' and '-' symbols in grammar 
      //  - 
      var inputs = new string[] { "x1" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseUnrestrictedRulesESR();

      Enumerate(grammar, maxLength, $@"c:\temp\allExpressions_esr_cosmic_1d_{maxLength}.txt");
      // len	numExpr	numUniqExpr
      // 1	2	2
      // 2	2	2
      // 3	24	14
      // 4	52	31
      // 5	460	177
      // 6	1556	593
      // 7	11380	3065
      // 8	49200	12374
      // 9	325728	61344
      // 10	1618800	272986

      // reported in ESR paper for complexity 10:
      // 5.2e6 'valid trees'
      // 134234 uniq expressions
      // 119861 expressions have parameters
    }

    public void Enumerate(Grammar g, int maxLength, string outFilename = "") {
      var evaluator = new CollectExpressionsEvaluator();
      var inputs = g.Variables.Select(sy => sy.VariableName).ToArray();
      // we do not require any data
      var trainX = new double[0, inputs.Length];
      var trainY = Array.Empty<double>();
      var trainInvNoiseSigma = Array.Empty<double>();
      var data = new Data(inputs, trainX, trainY, trainInvNoiseSigma);

      // do not use GraphSearchControl here because it already detects duplicate states
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, g, evaluator));

      Algorithms.BreadthSearch(control, control.InitialState, depth: 0, filterWidth: int.MaxValue, depthLimit: int.MaxValue, nodesReached: int.MaxValue);

      evaluator.WriteSummary();
      if (!string.IsNullOrEmpty(outFilename)) {
        evaluator.WriteAllExpressions(outFilename);
      }
    }

    private class CollectExpressionsEvaluator : IEvaluator {
      public CollectExpressionsEvaluator() {
      }

      public long OptimizedExpressions => throw new NotImplementedException();

      public long EvaluatedExpressions => allExpressions.Values.Sum(l => l.Count);

      public Dictionary<ulong, List<Expression>> allExpressions = new();
      public Dictionary<int, int> numExpressions = new();
      public Dictionary<int, int> numUniqExpressions = new();

      public double[] Evaluate(Expression expression, Data data) {
        throw new NotImplementedException();
      }

      public double OptimizeAndEvaluateDL(Expression expr, Data data) {
        if (!expr.IsSentence) throw new NotSupportedException();

        var h = Semantics.GetHashValue(expr);
        var len = expr.Length;
        if (!numExpressions.ContainsKey(len)) numExpressions.Add(len, 0);
        if (!numUniqExpressions.ContainsKey(len)) numUniqExpressions.Add(len, 0);

        numExpressions[len]++;

        if (allExpressions.TryGetValue(h, out var list)) {
          list.Add(expr);
        } else {
          allExpressions.Add(h, new List<Expression>() { expr });
          numUniqExpressions[len]++;
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

      internal void WriteSummary() {
        System.Console.WriteLine("len\tnumExpr\tnumUniqExpr");
        foreach (var len in numExpressions.Keys.Order()) {
          System.Console.WriteLine($"{len}\t{numExpressions[len]}\t{numUniqExpressions[len]}");
        }
      }
    }
  }
}