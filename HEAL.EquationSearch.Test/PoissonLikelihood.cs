using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System.Linq.Expressions;

namespace HEAL.EquationSearch.Test {

  public class PoissonLikelihood : LikelihoodBase {
    private double[] yPred; // buffer for model response
    private double[] nllArr; // buffer for observation likelihoods
    private double[,] jacP; // buffer for jacobian
    private double[] logGamma;


    internal PoissonLikelihood(PoissonLikelihood original) : base(original) {
      this.logGamma = (double[])original.logGamma.Clone();
      yPred = (double[])original.yPred.Clone();
      nllArr = (double[])original.nllArr.Clone();
      if (jacP != null)
        jacP = (double[,])original.jacP.Clone();
    }

    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => base.ModelExpr;
      set {
        base.ModelExpr = value;
        if (value != null) {

          var numParam = Expr.NumberOfParameters(value);
          if (jacP == null || jacP.GetLength(1) != numParam)
            jacP = new double[NumberOfObservations, numParam];
        }
      }
    }

    public PoissonLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr)
      : base(modelExpr, x, y, 0) {
      this.logGamma = new double[y.Length];
      for (int i = 0; i < y.Length; i++) {
        // prepare logGamma for the evaluation of likelihoods
        // TODO: lngamma(y+1) ?
        logGamma[i] = alglib.lngamma(y[i] + 1, out var sgngam); // we ignore sgngam because y is positive
      }

      // allocate buffers
      yPred = new double[y.Length];
      nllArr = new double[y.Length];
      if (modelExpr != null) {
        jacP = new double[y.Length, Expr.NumberOfParameters(modelExpr)];
      }
    }

    // numeric approximation via the gradient
    public override double[,] FisherInformation(double[] p) {
      var n = p.Length;
      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessianNum = new double[n, n];
      var eps = 1e-8;
      var nll_grad_low = new double[n];
      var nll_grad_high = new double[n];
      
      for (int i = 0; i < n; i++) {
        // numeric approximation (2-point) via gradient
        var relEps = Math.Max(eps, p[i] * eps);
        p[i] -= relEps;
        NegLogLikelihoodGradient(p, out _, nll_grad_low);
        p[i] += 2 * relEps;
        NegLogLikelihoodGradient(p, out _, nll_grad_high);
      
        p[i] -= relEps; // restore
      
        for (int j = 0; j < n; j++) {
          hessianNum[i, j] = (nll_grad_high[j] - nll_grad_low[j]) / (2 * relEps);
        }
      }
      
      // enforce symmetry / smooth out numeric errors
      for (int i = 0; i < n; i++) {
        for (int j = i + 1; j < n; j++) {
          hessianNum[i, j] = (hessianNum[i, j] + hessianNum[j, i]) / 2;
          hessianNum[j, i] = hessianNum[i, j];
        }
      }
      
      // return hessian;

      // d/dx1(d/(dx2)(-y f(x1, x2) + exp(f(x1, x2))))
      // = f^(1, 1)(x1, x2) (e^f(x1, x2) - y) + e^f(x1, x2) f^(0, 1)(x1, x2) f^(1, 0)(x1, x2)
      // = f'' (e^f + y) + e^f f' * f'
      var h = new double[y.Length][,];
      for (int i = 0; i < h.Length;i++) h[i] = new double[n, n];
      for (int i = 0; i < p.Length; i++) {
        GradInterpreter[i].EvaluateWithJac(p, nllArr, null, jacP); // jac for d y / d p_i
        for (int k = 0; k < jacP.GetLength(0); k++) {
          for (int j = 0; j < p.Length; j++) {
            h[k][i, j] = jacP[k, j];
          }
        }
      }

      Interpreter.EvaluateWithJac(p, yPred, null, jacP);
      var hessian = new double[n, n];
      for (int i = 0; i < y.Length; i++) {
        for (int j = 0; j < n; j++) {
          for (int k = 0; k < n; k++) {
            hessian[j, k] += h[i][j, k] * (Math.Exp(yPred[i]) - y[i]) + Math.Exp(yPred[i]) * jacP[i, j] * jacP[i, k];
          }
        }
      }
      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      var nllSum = 0.0;
      for (int i = 0; i < y.Length; i++) {
        nllSum += -y[i] * Math.Log(y[i] + 1) + (y[i] + 1) + logGamma[i];      // TODO: can be precalculated
      }
      return nllSum;
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[] nll_grad = null) {
      if (nll_grad == null) {
        NegLogLikelihoodJacobian(p, nllArr, null);
      } else {
        NegLogLikelihoodJacobian(p, nllArr, jacP);

        Array.Clear(nll_grad);
        for (int i = 0; i < jacP.GetLength(0); i++) {
          for (int j = 0; j < nll_grad.Length; j++) {
            nll_grad[j] += jacP[i, j];
          }
        }
      }
      nll = nllArr.Sum();
    }

    public void NegLogLikelihoodJacobian(double[] p, double[] nll, double[,]? jac) {
      base.Interpreter.EvaluateWithJac(p, yPred, null, jac); // yPred is log(lambda)
      for (int i = 0; i < y.Length; i++) {
        nll[i] = -y[i] * yPred[i] + Math.Exp(yPred[i]) + logGamma[i];
        if (jac != null) {
          for (int j = 0; j < p.Length; j++) {
            jac[i, j] = -y[i] * jac[i, j] + Math.Exp(yPred[i]) * jac[i, j];
          }
        }
      }
    }

    public override LikelihoodBase Clone() {
      return new PoissonLikelihood(this);
    }
  }
}
