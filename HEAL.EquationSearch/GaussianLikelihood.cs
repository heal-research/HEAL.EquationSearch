using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System;
using System.Linq.Expressions;
namespace HEAL.EquationSearch {

  // errors are iid N(0, noise_sigma)
  public class GaussianLikelihood : LikelihoodBase {
    private readonly double[] invNoiseSigma;

    // public override double Dispersion { get { return sErr; } set { sErr = value; } }

    internal GaussianLikelihood(GaussianLikelihood original) : base(original) {
      this.invNoiseSigma = original.invNoiseSigma;
    }
    public GaussianLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] invNoiseSigma)
      : base(modelExpr, x, y, numLikelihoodParams: 1) {
      this.invNoiseSigma = invNoiseSigma;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var yJac = new double[m, n];
      var yHess = new double[n, m, n]; // parameters x rows x parameters
      var yHessJ = new double[m, n]; // buffer

      var yPred = Expr.EvaluateFuncJac(ModelExpr, p, x, ref yJac);

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
          for (int k = 0; k < n; k++) {
            hessian[j, k] += (yJac[i, j] * yJac[i, k] + res * yHess[j, i, k]) * invNoiseSigma[i] * invNoiseSigma[i];
          }
        }
      }
      
      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood {
      get {
        int m = y.Length;
        return Enumerable.Range(0, m).Sum(i => 0.5 * Math.Log(2.0 * Math.PI  / invNoiseSigma[i] / invNoiseSigma[i])); // residuals are zero
      }
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[]? nll_grad) {
      var m = y.Length;
      var n = p.Length;
      double[,]? yJac = null;

      nll = BestNegLogLikelihood;

      double[] yPred;
      if (nll_grad == null) {
        yPred = Expr.EvaluateFunc(ModelExpr, p, x);
      } else {
        yPred = Expr.EvaluateFuncJac(ModelExpr, p, x, ref yJac);
        Array.Clear(nll_grad, 0, n);
      }

      for (int i = 0; i < m; i++) {
        var res = yPred[i] - y[i];
        nll += 0.5 * res * res * invNoiseSigma[i] * invNoiseSigma[i];

        if (nll_grad != null) {
          for (int j = 0; j < n; j++) {
            nll_grad[j] += res * yJac[i, j] * invNoiseSigma[i] * invNoiseSigma[i];
          }
        }
      }
    }

    public override LikelihoodBase Clone() {
      return new GaussianLikelihood(this);
    }
  }
}
