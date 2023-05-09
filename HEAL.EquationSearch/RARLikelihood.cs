using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System;
using System.Linq.Expressions;
using System.Reflection;
using LinqExpr = System.Linq.Expressions.Expression;

namespace HEAL.EquationSearch {

  // as reported in https://arxiv.org/pdf/2301.04368.pdf Eq. 3
  public class RARLikelihood : LikelihoodBase {
    private readonly double[] e_log_gobs;
    private readonly double[] e_log_gbar;

    private readonly static MethodInfo log = typeof(Math).GetMethod("Log", new Type[] { typeof(double) });
    private readonly static MethodInfo abs = typeof(Math).GetMethod("Abs", new Type[] { typeof(double) });
    private readonly static MethodInfo pow = typeof(Math).GetMethod("Pow", new Type[] { typeof(double), typeof(double) });


    private Expression<Expr.ParametricFunction> origExpr;
    // private ExpressionInterpreter df_dgbar_Interpreter;
    // private Expression<Func<double[], double[], double, double, double>> likelihoodExpr;
    private ExpressionInterpreter likelihoodInterpreter = null;
    private ExpressionInterpreter bestLikelihoodInterpreter = null;
    private ExpressionInterpreter[] likelihoodGradInterpreter = null;

    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => origExpr;
      set {
        origExpr = value;

        // to simplify calculation of Jac and Hess we create a parametric expression for the likelihood of one observation by wrapping the model
        // df_dgbar_Interpreter = null;
        // likelihoodExpr = null;

        if (value != null) {
          var pParam = value.Parameters[0];
          var xParam = value.Parameters[1];

          // the interpreter for the likelihood has additional variables e_log_gbar, e_log_gobs, and y
          var extendedXCol = new double[xCol.Length + 3][];
          for (int i = 0; i < xCol.Length; i++) {
            extendedXCol[i] = xCol[i];
          }

          extendedXCol[xCol.Length] = e_log_gobs; var e_log_gobs_idx = xCol.Length;
          extendedXCol[xCol.Length + 1] = e_log_gbar; var e_log_gbar_idx = xCol.Length + 1;
          extendedXCol[xCol.Length + 2] = y.Select(Math.Log10).ToArray(); var y_idx = xCol.Length + 2;

          // wrap log(f(x))
          value = value.Update(LinqExpr.Divide(
            LinqExpr.Call(log, LinqExpr.Call(abs, value.Body)),
            LinqExpr.Constant(Math.Log(10))), value.Parameters);

          var d_log_f_dgbar = Expr.Derive(value, value.Parameters[1], 0);

          var gbar_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(0));
          var e_log_gobs_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(e_log_gobs_idx));
          var e_log_gbar_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(e_log_gbar_idx));
          var y_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(y_idx));
          var sTotExpr = LinqExpr.Add(
            LinqExpr.Multiply(e_log_gobs_expr, e_log_gobs_expr),
            LinqExpr.Call(pow, LinqExpr.Multiply(LinqExpr.Multiply(LinqExpr.Multiply(d_log_f_dgbar.Body, gbar_expr), LinqExpr.Constant(Math.Log(10))), e_log_gbar_expr), LinqExpr.Constant(2.0))
            );
          // df_dgbar_Interpreter = new ExpressionInterpreter(d_log_f_dgbar, xCol);

          var baseExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Call(log, LinqExpr.Multiply(LinqExpr.Constant(2.0 * Math.PI), sTotExpr))); //  0.5 * Math.Log(2.0 * Math.PI * stot)
          var residualExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Divide(LinqExpr.Call(pow, LinqExpr.Subtract(value.Body, y_expr), LinqExpr.Constant(2.0)), sTotExpr)); //0.5 * res * res / stot;

          Expression<Expr.ParametricFunction> likelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: LinqExpr.Add(residualExpr, baseExpr), 
            pParam, xParam);
          
          Expression<Expr.ParametricFunction> baseLikelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: baseExpr,
            pParam, xParam);

          likelihoodInterpreter = new ExpressionInterpreter(likelihoodExpr, extendedXCol);
          bestLikelihoodInterpreter = new ExpressionInterpreter(baseLikelihoodExpr, extendedXCol);

          var numParam = Expr.NumberOfParameters(origExpr);
          likelihoodGradInterpreter = new ExpressionInterpreter[numParam];
          for(int i=0;i< numParam; i++) {
            var dLikeExpr = Expr.Derive(likelihoodExpr, i);
            likelihoodGradInterpreter[i] = new ExpressionInterpreter(dLikeExpr, extendedXCol);
          }
        }
        base.ModelExpr = value;
      }
    }


    internal RARLikelihood(RARLikelihood original) : base(original) {
      this.e_log_gobs = original.e_log_gobs;
      this.e_log_gbar = original.e_log_gbar;
      this.likelihoodInterpreter = original.likelihoodInterpreter;
      this.bestLikelihoodInterpreter = original.bestLikelihoodInterpreter;
      this.likelihoodGradInterpreter = original.likelihoodGradInterpreter;

    }
    public RARLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] e_log_gobs, double[] e_log_gbar)
      : base(modelExpr, x, y, 0) {
      this.e_log_gobs = e_log_gobs;
      this.e_log_gbar = e_log_gbar;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var likeHessPart = new double[m, n]; // buffer

      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessian = new double[n, n];
      for (int j = 0; j < n; j++) {
        likelihoodGradInterpreter[j].EvaluateWithJac(p, null, likeHessPart);
        for (int i = 0; i < m; i++) {
          for (int k = 0; k < n; k++) {
            hessian[j, k] += likeHessPart[i, k];
          }
        }
      }

      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      return bestLikelihoodInterpreter.Evaluate(p).Sum();
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[]? nll_grad) {
      var m = y.Length;
      var n = p.Length;
      double[,]? jacP = null;


      if (nll_grad != null) {
        jacP = new double[m, n];
        Array.Clear(nll_grad, 0, n);

        var nllArr = likelihoodInterpreter.EvaluateWithJac(p, null, jacP);
        nll = nllArr.Sum();
        for (int i = 0; i < m; i++) {
          for (int j = 0; j < n; j++) {
            nll_grad[j] += jacP[i, j];
          }
        }
      } else {
        nll = likelihoodInterpreter.Evaluate(p).Sum();
      }
    }

    public override LikelihoodBase Clone() {
      return new RARLikelihood(this);
    }
  }
}
