using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System;
using System.Linq.Expressions;
using System.Reflection;

namespace HEAL.EquationSearch {

  // as reported in https://arxiv.org/pdf/2301.04368.pdf Eq. 3
  public class RARLikelihood : LikelihoodBase {
    private readonly double[] e_log_gobs;
    private readonly double[] e_log_gbar;

    private readonly static MethodInfo log = typeof(Math).GetMethod("Log", new Type[] { typeof(double) });


    private Expression<Expr.ParametricFunction> origExpr;
    private ExpressionInterpreter df_dgbar_Interpreter;
    // private Expression<Func<double[], double[], double, double, double>> likelihoodExpr;
    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => origExpr;
      set {
        origExpr = value;

        // to simplify calculation of Jac and Hess we create a parametric expression for the likelihood of one observation by wrapping the model
        df_dgbar_Interpreter = null;
        // likelihoodExpr = null;

        if (value != null) {
          // wrap log(f(x))
          value = value.Update(System.Linq.Expressions.Expression.Divide(
            System.Linq.Expressions.Expression.Call(null, log, value.Body),
            System.Linq.Expressions.Expression.Constant(Math.Log(10))), value.Parameters);
          
          var d_log_f_dgbar = Expr.Derive(value, value.Parameters[1], 0);
          df_dgbar_Interpreter = new ExpressionInterpreter(d_log_f_dgbar, xCol);
          
        }
        base.ModelExpr = value;
      }
    }


    internal RARLikelihood(RARLikelihood original) : base(original) {
      this.e_log_gobs = original.e_log_gobs;
      this.e_log_gbar = original.e_log_gbar;
      this.df_dgbar_Interpreter = original.df_dgbar_Interpreter;
    }
    public RARLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] e_log_gobs, double[] e_log_gbar)
      : base(modelExpr, x, y, 0) {
      this.e_log_gobs = e_log_gobs;
      this.e_log_gbar = e_log_gbar;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var fJacX = new double[m, xCol.Length];
      var fJacP = new double[m, n];
      var fHess = new double[n, m, n]; // parameters x rows x parameters
      var fHessPart = new double[m, n]; // buffer

      var yPred = interpreter.EvaluateWithJac(p, fJacX, fJacP);

      // evaluate hessian
      for (int j = 0; j < p.Length; j++) {
        // Expr.EvaluateFuncJac(ModelGradient[j], p, x, ref yHessJ);
        gradInterpreter[j].EvaluateWithJac(p, null, fHessPart);
        Buffer.BlockCopy(fHessPart, 0, fHess, j * m * n * sizeof(double), m * n * sizeof(double));
        Array.Clear(fHessPart, 0, fHessPart.Length);
      }


      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessian = new double[n, n];
      for (int j = 0; j < n; j++) {
        for (int i = 0; i < m; i++) {
          var res = yPred[i] - Math.Log10(y[i]); // y = g_obs
          // stot = e_log_gobs^2 + (d log(f) / d log(gbar))^2 * e_log_gbar^2 
          // fJacX = (d / d gbar)  log f (gbar) 
          var gbar = x[i, 0];
          var stot = e_log_gobs[i] * e_log_gobs[i] + Math.Pow(fJacX[i, 0] * gbar * Math.Log(10) * e_log_gbar[i], 2);// yJacX[., 0] is gradient for e_log_gbar
          for (int k = 0; k < n; k++) {
            hessian[j, k] += (fJacP[i, j] * fJacP[i, k] + res * fHess[j, i, k]) / stot; // TODO
          }
        }
      }

      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      int m = y.Length;

      var df_dgbar = df_dgbar_Interpreter.Evaluate(p);

      var nll = 0.0;
      for (int i = 0; i < m; i++) {
        var gbar = x[i, 0];
        var stot = e_log_gobs[i] * e_log_gobs[i] + Math.Pow(df_dgbar[i] * gbar  * Math.Log(10) * e_log_gbar[i], 2);// yJacX[., 0] is gradient for e_log_gbar
        nll += 0.5 * Math.Log(2.0 * Math.PI * stot);
      }
      return nll;
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[]? nll_grad) {
      var m = y.Length;
      var n = p.Length;
      double[,]? fJacP = null;
      double[,]? df_dgbarJacP = null;


      if (nll_grad != null) {
        fJacP = new double[m, n];
        df_dgbarJacP = new double[m, n];
        Array.Clear(nll_grad, 0, n);
      }

      var yPred = interpreter.EvaluateWithJac(p, null, fJacP);
      var df_dgbar = df_dgbar_Interpreter.EvaluateWithJac(p, null, df_dgbarJacP);


      nll = 0.0;

      for (int i = 0; i < m; i++) {
        var gbar = x[i, 0];
        var stot = e_log_gobs[i] * e_log_gobs[i] + Math.Pow(df_dgbar[i] * gbar * Math.Log(10) * e_log_gbar[i], 2);// yJacX[., 0] is gradient for e_log_gbar

        var res = yPred[i] - Math.Log10(y[i]);
        nll +=  0.5 * Math.Log(2.0 * Math.PI * stot) + 0.5 * res * res / stot;

        if (nll_grad != null) {
          for (int j = 0; j < n; j++) {
            var dStot = 2 * (df_dgbar[i] * gbar * Math.Log(10) * e_log_gbar[i]) * gbar * Math.Log(10) * e_log_gbar[i] * df_dgbarJacP[i, j];
            nll_grad[j] += 0.5 * (stot * (2 * res * fJacP[i, j] + dStot) - res * res * dStot) / (stot * stot);
          }
        }
      }
    }

    public override LikelihoodBase Clone() {
      return new RARLikelihood(this);
    }
  }
}
