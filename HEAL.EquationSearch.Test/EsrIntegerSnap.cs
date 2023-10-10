﻿using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq.Expressions;
using CommandLine;
using HEAL.Expressions;
using HEAL.NonlinearRegression;
using TreesearchLib;
using static alglib;

namespace HEAL.EquationSearch.Test {

  // This test class processes a file with parameterized expressions for RAR and checks if they can be improved via integer-snap
  [TestClass]
  public class EsrIntegerSnap {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }


    [DataTestMethod]
    [DataRow(@"c:\temp\allExpressions_eqs_logexppow_1d_10_optimized.txt", new[] { "gbar" })]
    [DataRow(@"c:\temp\allExpressions_eqs_logexppow_1d_30_optimized.txt", new[] { "gbar" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_10_optimized.txt", new[] { "gbar" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_5_optimized.txt", new[] { "gbar" })]
    [DataRow(@"c:\temp\allExpressions_esr_cosmic_1d_10_optimized.txt", new[] { "gbar" })]
    public void IntegerSnap(string fileName, string[] variableNames) {
      var expressions = new HashSet<string>();
      using (var reader = new StreamReader(fileName)) {
        reader.ReadLine(); // skip variable names

        var line = reader.ReadLine();
        while (line != null) {
          var toks = line.Split(";");
          // len;nParam;expr;postfixExpr;DL;ms;nll;restarts;restartNumBest
          if (toks[4] == "1.7976931348623157E+308") {
            line = reader.ReadLine();
          } else {
            var parameterizedExpr = toks[3];
            expressions.Add(parameterizedExpr);
            line = reader.ReadLine();
          }
        }
      }


      var options = new HEAL.EquationSearch.Console.Program.RunOptions();
      options.Dataset = "RAR_sigma.csv";
      options.Target = "gobs";
      options.TrainingRange = "0:2695";
      options.NoiseSigma = "";
      options.Seed = 1234;
      GetRARData(options, out var inputVars, out var trainX, out var trainY, out var _, out var e_log_gobs, out var e_log_gbar);

      // var grammar = new Grammar(inputVars, maxLen: int.MaxValue);


      var likelihood = new RARLikelihood(trainX, trainY, modelExpr: null, e_log_gobs, e_log_gbar);
      var evaluator = new Evaluator(likelihood);
      var data = new Data(inputVars, trainX, trainY, null);

      var varSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "x");
      var paramSy = System.Linq.Expressions.Expression.Parameter(typeof(double[]), "p");
      // Parallel.ForEach(expressions.OrderBy(str => str.Length), new ParallelOptions() { MaxDegreeOfParallelism = 12 }, exprStr => {
      foreach (var exprStr in expressions.OrderBy(str => str.Length)) {
        if (!exprStr.Contains("Infinity") && !exprStr.Contains("NaN")) {
          var parser = new HEAL.Expressions.Parser.ExprParser(exprStr, variableNames, varSy, paramSy);
          var expr = parser.Parse();
          var parameters = parser.ParameterValues.ToArray();

          for (int i = 0; i < parameters.Length; i++) {
            if (Math.Floor(parameters[i]) == parameters[i]) {
              var v = new ReplaceParameterWithConstantVisitor(expr.Parameters[0], i, parameters[i]);
              expr = (System.Linq.Expressions.Expression<Expr.ParametricFunction>)v.Visit(expr);
            }
          }
          var exprWithoutConstants = Expr.SimplifyAndRemoveParameters(expr, parameters, out parameters);

          if (parameters.Length > 0) {
            likelihood.ModelExpr = exprWithoutConstants;
            var dl = ModelSelection.DL(parameters, likelihood);
            var integerSnapDl = DLWithIntegerSnap(parameters, likelihood);

            System.Console.WriteLine($"dl: {dl} integer-snap dl: {integerSnapDl} expr: {exprStr}");
            if (dl > integerSnapDl) {
            }

          }
        }
      }
      // );


    }
    public static double DLWithIntegerSnap(double[] paramEst, LikelihoodBase likelihood) {

      // clone parameters and likelihood for pruning and evaluation (caller continues to work with original expression)
      paramEst = (double[])paramEst.Clone();
      likelihood = likelihood.Clone();
      IntegerSnapPruning(ref paramEst, likelihood, ModelSelection.DL);
      //TODO return updated model from integer snap and evaluate DL for updated model
      return ModelSelection.DL(paramEst, likelihood);
    }

    // Algorithm for Pruning (replacing parameters by integers):
    // Replace parameters by constants greedily.
    // 1. Calc parameter statistics and select parameters with the largest zScore (least certain)
    // 2. Generate integer constant values towards zero by repeatedly halving the rounded parameter value
    // 3. For each constant value calculate the DL (ignoring the DL of the function because it is unchanged)
    // 4. Use the value with best DL as replacement, simplify and re-fit the function.
    // 5. If the simplified function has better DL accept as new model and goto 1.
    //
    // The description length function is not convex over theta (sum of negLogLik (assumed to be convex around the MLE) and logarithms of theta (concave)).
    // -> multiple local optima in DL
    // This method modifies the parameter vector and the likelihood
    private static void IntegerSnapPruning(ref double[] paramEst, LikelihoodBase likelihood, Func<double[], LikelihoodBase, double> DL) {
      var fisherInfo = likelihood.FisherInformation(paramEst);

      var n = paramEst.Length;
      var precision = new double[n];
      for (int i = 0; i < n; i++) {
        precision[i] = Math.Abs(paramEst[i]) * Math.Sqrt(fisherInfo[i, i]);
      }

      var idx = Enumerable.Range(0, n).ToArray();
      Array.Sort(precision, idx); // order paramIdx by zScore (smallest first)

      var paramIdx = idx[0];
      // generate integer alternatives for the parameter by repeatedly halving the value (TODO: other schemes useful / better here?)
      var constValues = new List<int> {
          (int)Math.Round(paramEst[paramIdx])
        };
      while (constValues.Last() > 0) {
        constValues.Add(constValues.Last() / 2); // integer divison
      }

      var origDL = DL(paramEst, likelihood);

      var origParamValue = paramEst[paramIdx];
      var origExpr = likelihood.ModelExpr;
      var bestDL = double.MaxValue; // likelihood and DL of constant for best replacement
      var bestConstValue = double.NaN;

      // try all constant values and find best 
      foreach (var constValue in constValues) {
        paramEst[paramIdx] = constValue;
        double curDL= DL(paramEst, likelihood);
        // System.Console.WriteLine($"param: {paramIdx} const:{constValue} DL:{curDL} negLL:{likelihood.NegLogLikelihood(paramEst)} DL(const):{ConstCodeLength(constValue)}");
        if (curDL < bestDL) {
          bestDL = curDL;
          bestConstValue = constValue;
        }
      }

      if (!double.IsNaN(bestConstValue)) {

        // replace parameter with best constant value, simplify and re-fit
        paramEst[paramIdx] = bestConstValue;

        var v = new ReplaceParameterWithConstantVisitor(origExpr.Parameters[0], paramIdx, bestConstValue);
        var reducedExpr = (Expression<Expr.ParametricFunction>)v.Visit(origExpr);
        var simplifiedExpr = Expr.SimplifyAndRemoveParameters(reducedExpr, paramEst, out var simplifiedParamEst);
        if (simplifiedParamEst.Length > 0) {
          likelihood.ModelExpr = simplifiedExpr;
          var nlr = new NonlinearRegression.NonlinearRegression();
          nlr.Fit(simplifiedParamEst, likelihood); // TODO: here we could use FisherDiag for the scale for improved perf
          if (nlr.ParamEst == null) {
            System.Console.Error.WriteLine("Problem while re-fitting pruned expression in DL calculation.");
            likelihood.ModelExpr = origExpr;
            paramEst[paramIdx] = origParamValue;
          } else {
            var newDL = DL(nlr.ParamEst, likelihood);
            // if the new DL is shorter then continue with next parameter
            if (newDL < origDL) {
              System.Console.WriteLine("######################################");
              System.Console.WriteLine($"In DL: replaced parameter[{paramIdx}]={origParamValue} by constant {bestConstValue}:");
              System.Console.WriteLine($"Pruned model: {likelihood.ModelExpr}");

              likelihood.LaplaceApproximation(nlr.ParamEst).WriteStatistics(System.Console.Out);

              paramEst = nlr.ParamEst;
              IntegerSnapPruning(ref paramEst, likelihood, DL);
            } else {
              // no improvement by replacing the parameter with a constant -> restore original expression and return
              likelihood.ModelExpr = origExpr;
              paramEst[paramIdx] = origParamValue;
            }
          }
        }
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