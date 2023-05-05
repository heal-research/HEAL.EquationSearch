using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System;
using System.Linq.Expressions;
namespace HEAL.EquationSearch {

  // as reported in https://arxiv.org/pdf/2301.04368.pdf Eq. 3
  public class RARLikelihood : LikelihoodBase {
    private readonly double[] e_log_gobs;
    private readonly double[] e_log_gbar;

    // public override double Dispersion { get { return sErr; } set { sErr = value; } }    

    internal RARLikelihood(RARLikelihood original) : base(original) {
      this.e_log_gobs = original.e_log_gobs;
      this.e_log_gbar = original.e_log_gbar;
    }
    public RARLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] e_log_gobs, double[] e_log_gbar)
      : base(modelExpr, x, y, numLikelihoodParams: 1) {
      this.e_log_gobs = e_log_gobs;
      this.e_log_gbar = e_log_gbar;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var yJac = new double[m, n];
      var yHess = new double[n, m, n]; // parameters x rows x parameters
      var yHessJ = new double[m, n]; // buffer

      var yPred = Expr.EvaluateFuncJac(ModelExpr, p, x, ref yJac);
      double[,]? yJacX = null;
      Expr.EvaluateFuncJacX(ModelExpr, p, X, ref yJacX); // TODO: calculate once

      // evaluate hessian
      for (int j = 0; j < p.Length; j++) {
        Expr.EvaluateFuncJac(ModelGradient[j], p, x, ref yHessJ);
        Buffer.BlockCopy(yHessJ, 0, yHess, j * m * n * sizeof(double), m * n * sizeof(double));
        Array.Clear(yHessJ, 0, yHessJ.Length);
      }


      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessian = new double[n, n];
      for (int j = 0; j < n; j++) {
        for (int i = 0; i < m; i++) {
          var res = yPred[i] - y[i];
          var stot = (e_log_gobs[i] * e_log_gobs[i] + yJacX[i, 0] * yJac[i, 0] * e_log_gbar[i] * e_log_gbar[i]);
          for (int k = 0; k < n; k++) {
            hessian[j, k] += (yJac[i, j] * yJac[i, k] + res * yHess[j, i, k]) / stot;
          }
        }
      }

      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      int m = y.Length;
      double[,]? jac = null;
      Expr.EvaluateFuncJacX(ModelExpr, p, X, ref jac);
      var nll = 0.0;
      for (int i = 0; i < m; i++) {
        var stot = (e_log_gobs[i] * e_log_gobs[i] + jac[i, 0] * jac[i, 0] * e_log_gbar[i] * e_log_gbar[i]);
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
      double[,]? yJacP = null;
      double[,]? yJacX = null;
      Expr.EvaluateFuncJacX(ModelExpr, p, X, ref yJacX); // TODO: calculate once

      nll = BestNegLogLikelihood(p);

      double[] yPred;
      if (nll_grad == null) {
        yPred = Expr.EvaluateFunc(ModelExpr, p, x);
      } else {
        yPred = Expr.EvaluateFuncJac(ModelExpr, p, x, ref yJacP);
        Array.Clear(nll_grad, 0, n);
      }

      for (int i = 0; i < m; i++) {
        var stot = (e_log_gobs[i] * e_log_gobs[i] + yJacX[i, 0] * yJacX[i, 0] * e_log_gbar[i] * e_log_gbar[i]);
        var res = yPred[i] - y[i];
        nll += 0.5 * res * res / stot;

        if (nll_grad != null) {
          for (int j = 0; j < n; j++) {
            nll_grad[j] += res * yJacP[i, j] / stot;
          }
        }
      }
    }

    public override LikelihoodBase Clone() {
      return new RARLikelihood(this);
    }
  }
}
