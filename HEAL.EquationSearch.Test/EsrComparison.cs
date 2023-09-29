using System.Collections.Concurrent;
using System.Diagnostics;
using CommandLine;
using HEAL.Expressions;
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
    [DataRow(10, null)]
    [DataRow(20, null)]
    [DataRow(30, null)]
    public void FullEnumerationUnivariateReducedGrammar(int maxLength, int? expectedTotalExpressions) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseLogExpPowRestrictedRules();


      Enumerate(grammar, maxLength, @"c:\temp\allExpressions_eqs_logexppow_1d_" + maxLength + ".txt", expectedTotalExpressions);

    }

    [DataTestMethod]
    [DataRow(30)]
    public void FullEnumerationBivariateReducedGrammar(int maxLength) {
      // Generates all expressions and unique expressions using the reduced grammar.
      // The code uses a custom evaluator that collects all expressions, finds semantic duplicates and at the end produces files with all expressions in infix form.

      var inputs = new string[] { "x0", "x1" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseLogExpPowRestrictedRules();

      Enumerate(grammar, maxLength, @"c:\temp\allExpressions_eqs_logexppow_2d_" + maxLength + ".txt");

    }


    [DataTestMethod]
    [DataRow(4, 88)]
    [DataRow(5, 610)]
    [DataRow(6, 2812)]
    [DataRow(7, 19114)]
    [DataRow(8, 103536)]
    [DataRow(9, 692098)]
    [DataRow(10, 4103220)]
    public void FullEnumerationUnivariateESRCosmicGrammar(int maxLength, int? expectedTotalExpressions) {
      var inputs = new string[] { "x" };
      var grammar = new Grammar(inputs, maxLength);
      grammar.UseEsrCoreMaths();

      Enumerate(grammar, maxLength, $@"c:\temp\allExpressions_esr_cosmic_1d_{maxLength}.txt", expectedTotalExpressions);

      // reported in ESR core_maths.zip:
      // original_trees:
      // len #
      // 1   2
      // 2   2
      // 3   20
      // 4   62
      // 5   522
      // 6   2202
      // 7   16302
      // 8   84422
      // 9   588562
      // 10  3411122
      // sum 4103218

      // reported in ESR paper for complexity 10:
      // 5.2e6 'valid trees'
      // 134234 uniq expressions
      // 119861 expressions have parameters
    }

    public void Enumerate(Grammar g, int maxLength, string outFilename = "", int? expectedTotalExpressions = null, int? expectedUniqExpressions = null) {
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

      if (expectedTotalExpressions.HasValue) {
        Assert.AreEqual(expectedTotalExpressions.Value, evaluator.EvaluatedExpressions);
      }
    }

    [DataTestMethod]
    [DataRow(@"c:\temp\allExpressions_eqs_logexppow_1d_10.txt", new[] { "x" })]
    [DataRow(@"c:\temp\allExpressions_eqs_logexppow_1d_30.txt", new[] { "x" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_10.txt", new[] { "x" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_5.txt", new[] { "x" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_7.txt", new[] { "x" })]
    public void OptimizeAndEvaluateAll(string fileName, string[] variableNames) {
      // the files must be produced with Enumerate()

      var uniqExprStr = new HashSet<string>();
      using (var reader = new StreamReader(fileName)) {
        reader.ReadLine(); // skip variable names

        var line = reader.ReadLine();
        while (line != null) {
          var toks = line.Split(";");
          // h;len;nParam;expr;simplified;simplifiedLen;noConstHash;simplifiedNoConst;simplifiedNoConstLen
          var noConstExpr = toks[4];
          uniqExprStr.Add(noConstExpr);
          line = reader.ReadLine();
        }
      }


      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;
      GetRARData(options, out var inputVars, out var trainX, out var trainY, out var _, out var e_log_gobs, out var e_log_gbar);

      var grammar = new Grammar(inputVars, maxLen: int.MaxValue);
      // maxLen and grammar rules do not matter here since we are not generating expressions from the grammar


      var likelihood = new RARLikelihoodNumeric(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar);
      var evaluator = new Evaluator(likelihood);
      var data = new Data(inputVars, trainX, trainY, null);

      var varSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "x");
      var paramSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "p");
      using (var writer = new StreamWriter(fileName.Replace(".txt", "_optimized.txt"), append: false)) {
        writer.WriteLine($"len;nParam;expr;postfixExpr;DL;ms;nll;restarts;restartNumBest");
        Parallel.ForEach(uniqExprStr.OrderBy(str => str.Length), new ParallelOptions() { MaxDegreeOfParallelism = 12 }, exprStr => {
          if (!exprStr.Contains("Infinity") && !exprStr.Contains("NaN")) {
            // parse the expression with additional variable p
            // lock (writer) {
            //   writer.WriteLine($"{exprStr}");
            // }
            var parser = new HEAL.Expressions.Parser.ExprParser(exprStr, variableNames.Concat(new string[] { "p" }).ToArray(), varSy, paramSy);
            var expr = parser.Parse();
            // replace all parameters with constants (numbers in the exprStr are constants)
            expr = Expr.ReplaceParameterWithValues<Expr.ParametricFunction>(expr, expr.Parameters[0], parser.ParameterValues);
            // replace all usages of p with parameter value 0.01 (variable p in exprStr are parameters)
            var replaceVarWithParVisitor = new ReplaceVariableWithParameterVisitor(paramSy, new double[0], varSy, 1, 0.01);
            expr = (System.Linq.Expressions.Expression<Expr.ParametricFunction>)replaceVarWithParVisitor.Visit(expr);
            var newParamValues = replaceVarWithParVisitor.NewThetaValues.ToArray();

            // System.Console.WriteLine(Expr.ToString(expr, variableNames, newParamValues));

            var postfixExpr = HEALExpressionBridge.ConvertToPostfixExpression(expr, newParamValues, grammar);

            var sw = new Stopwatch();
            sw.Start();
            var dl = evaluator.OptimizeAndEvaluateDL(postfixExpr, data, out var restarts); // TODO: remove conversion to postfix expression and back to expression tree
            sw.Stop();

            var len = postfixExpr.Length;
            var nParam = postfixExpr.Count(sy => sy is Grammar.ParameterSymbol);
            lock (writer) {
              writer.WriteLine($"{len};{nParam};{exprStr};{postfixExpr.ToInfixString()};{dl};{sw.ElapsedMilliseconds};{restarts.BestLoss};{restarts.Iterations};{restarts.NumBest}");
              writer.Flush();
            }
          }
        });
      }


    }

    private static void GetRARData(Console.Program.RunOptions options, out string[] inputs, out double[,] trainX, out double[] trainY, out double[] trainNoiseSigma, out double[] e_log_gobs, out double[] e_log_gbar) {
      // https://arxiv.org/abs/2301.04368
      // File RAR.dat recieved from Harry


      // extract gbar as x
      inputs = new string[] { "gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref inputs, out _, out _, out _, out _, out _, out _, out _, out trainX, out trainY, out trainNoiseSigma);
      // extract error variables
      string[] errors = new string[] { "e_log_gobs", "e_log_gbar" };
      HEAL.EquationSearch.Console.Program.PrepareData(options, ref errors, out _, out _, out _, out _, out _, out _, out _, out var trainErrorX, out _, out _);

      e_log_gobs = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 0]).ToArray();
      e_log_gbar = Enumerable.Range(0, trainErrorX.GetLength(0)).Select(i => trainErrorX[i, 1]).ToArray();
    }

    private class CollectExpressionsEvaluator : IEvaluator {
      public CollectExpressionsEvaluator() {
      }

      public long OptimizedExpressions => throw new NotImplementedException();

      public long EvaluatedExpressions => allExpressions.Count;

      public List<Expression> allExpressions = new();

      public double[] Evaluate(Expression expression, Data data) {
        throw new NotImplementedException();
      }

      public double OptimizeAndEvaluateDL(Expression expr, Data data) {
        if (!expr.IsSentence) throw new NotSupportedException();

        allExpressions.Add(expr);

        return 0.0;
      }

      public double OptimizeAndEvaluateMSE(Expression expr, Data data) {
        throw new NotImplementedException();
      }



      internal void WriteAllExpressions(string filename) {
        using (var writer = new StreamWriter(filename, false)) {
          writer.WriteLine("h;len;nParam;expr;simplified;simplifiedLen;noConstHash;simplifiedNoConst;simplifiedNoConstLen");
          foreach (var group in allExpressions.GroupBy(e => e.Length)) {
            var len = group.Key;
            Parallel.ForEach(group, (expr) => {
              var numParam = expr.Count(sy => sy is Grammar.ParameterSymbol);
              var semHash = Semantics.GetHashValue(expr, out var simplifiedExpression);
              var simplifiedLen = simplifiedExpression.Length;

              var noConstExpr = new Expression(simplifiedExpression.Grammar, simplifiedExpression.Select(sy => sy is Grammar.ConstantSymbol constSy ? new Grammar.ParameterSymbol(constSy.Value) : sy).ToArray());
              var noConstSemHash = Semantics.GetHashValue(noConstExpr, out var noConstSimplifiedExpr);

              lock (writer)
                writer.WriteLine($"{semHash};{len};{numParam};{expr.ToInfixString(includeParamValues: false)};" +
                  $"{simplifiedExpression.ToInfixString(includeParamValues: false)};{simplifiedExpression.Length};" +
                  $"{noConstSemHash};{noConstSimplifiedExpr.ToInfixString(includeParamValues: false)};{noConstSimplifiedExpr.Length}");
            });
          }
        }
      }

      internal void WriteSummary() {
        System.Console.WriteLine("len\tnumExpr\tnumUniqExpr\tnParam_0\tnParam_1\tnParam_2\tnParam_3\tnParam_4");
        foreach (var group in allExpressions.GroupBy(e => e.Length).OrderBy(g => g.Key)) {
          var len = group.Key;
          var semHashCount = new Dictionary<ulong, int>();
          var paramCount = new Dictionary<int, int>();
          foreach (var expr in group) {
            var numParam = expr.Count(sy => sy is Grammar.ParameterSymbol);
            var semHash = Semantics.GetHashValue(expr, out var simplifiedExpression);
            if (semHashCount.TryGetValue(semHash, out var curCount)) {
              semHashCount[semHash] = curCount + 1;
            } else {
              semHashCount.Add(semHash, 1);
            }
            if (paramCount.TryGetValue(numParam, out curCount)) {
              paramCount[numParam] = curCount + 1;
            } else {
              paramCount.Add(numParam, 1);
            }
          }
          var maxParam = paramCount.Keys.Max();
          System.Console.WriteLine($"{len}\t{semHashCount.Values.Sum()}\t{semHashCount.Count}\t{string.Join("\t", Enumerable.Range(0, maxParam).Select(i => paramCount.ContainsKey(i) ? paramCount[i].ToString() : "0"))}");
        }
      }
    }
  }
}