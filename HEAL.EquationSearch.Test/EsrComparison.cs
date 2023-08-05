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
      //    - repeated inv
      //    - ... mul inv(x) (multiplication with inverse = division)
      //  - 
      var inputs = new string[] { "x1" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseEsrCoreMaths();

      Enumerate(grammar, maxLength, $@"c:\temp\allExpressions_esr_cosmic_1d_{maxLength}.txt");



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
          // writer.WriteLine("minLen;numExprs;shortestExpr;expressions");
          // foreach (var kvp in allExpressions) {
          //   var shortestExpr = kvp.Value.OrderBy(e => e.Length).First();
          //   writer.WriteLine($"{shortestExpr.Length};" +
          //     $"{kvp.Value.Count};" +
          //     $"{shortestExpr.ToInfixString(includeParamValues: false)};" +
          //     $"{string.Join("\t", kvp.Value.Select(expr => expr.ToInfixString(includeParamValues: false)))}");
          // }
          writer.WriteLine("len;nParam;expr");
          foreach (var group in allExpressions.Values.SelectMany(e => e).GroupBy(e => e.Length)) {
            var len = group.FirstOrDefault()?.Length;
            foreach (var expr in group) {
              var numParam = expr.Count(sy => sy is Grammar.ParameterSymbol);
              writer.WriteLine($"{len};{numParam};{expr.ToInfixString(includeParamValues: false)}");
            }
          }
        }
      }

      internal void WriteSummary() {
        System.Console.WriteLine("len\tnumExpr\tnumUniqExpr\tnParam_0\tnParam_1\tnParam_2\tnParam_3\tnParam_4");
        foreach (var len in numExpressions.Keys.Order()) {
          // TODO: efficiency
          var exprs = allExpressions.Values.SelectMany(exprs => exprs.Where(e => e.Length == len)).ToArray();
          var nParam0 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 0);
          var nParam1 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 1);
          var nParam2 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 2);
          var nParam3 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 3);
          var nParam4 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 4);
          var nParam5 = exprs.Count(expr => expr.Count(sy => sy is Grammar.ParameterSymbol) == 5);

          System.Console.WriteLine($"{len}\t{numExpressions[len]}\t{numUniqExpressions[len]}\t{nParam0}\t{nParam1}\t{nParam2}\t{nParam3}\t{nParam4}\t{nParam5}");
        }
      }
    }
  }
}