using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System.Linq.Expressions;
using System.Reflection;

namespace HEAL.EquationSearch.Test {

  // likelihood for cosmic chonometer
  public class CCLikelihood : LikelihoodBase {
    private readonly double[] invNoiseSigma;

    private static readonly MethodInfo sqrt = typeof(Math).GetMethod("Sqrt", new Type[] { typeof(double) });
    private static readonly MethodInfo abs = typeof(Math).GetMethod("Abs", new Type[] { typeof(double) });
    private Expression<Expr.ParametricFunction> h2Expr;

    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => h2Expr;
      set {
        h2Expr = value;
        if (value != null) {
          // wrap H=sqrt(abs(model))
          value = value.Update(System.Linq.Expressions.Expression.Call(null, sqrt, System.Linq.Expressions.Expression.Call(null, abs, value.Body)), value.Parameters);
        } 
        base.ModelExpr = value;
      }
    }

    internal CCLikelihood(CCLikelihood original) : base(original) {
      this.invNoiseSigma = original.invNoiseSigma;
      this.h2Expr = original.h2Expr;
    }
    public CCLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] invNoiseSigma)
      : base(modelExpr, x, y, numLikelihoodParams: 0) {
      this.invNoiseSigma = invNoiseSigma;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var yPred = new double[m];
      var tmp = new double[m];
      var yJac = new double[m, n];
      var yHess = new double[n, m, n]; // parameters x rows x parameters
      var yHessJ = new double[m, n]; // buffer

      Interpreter.EvaluateWithJac(p, yPred, null, yJac);

      // evaluate hessian
      for (int j = 0; j < p.Length; j++) {
        GradInterpreter[j].EvaluateWithJac(p, tmp, null, yHessJ);
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
    public override double BestNegLogLikelihood(double[] p) {
      return 0.0; // in the ESR paper the constant term of the likelihood is not reported
      // int m = y.Length;
      // return Enumerable.Range(0, m).Sum(i => 0.5 * Math.Log(2.0 * Math.PI / invNoiseSigma[i] / invNoiseSigma[i]));
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[]? nll_grad) {
      var m = y.Length;
      var n = p.Length;
      var yPred = new double[m];
      double[,]? yJac = null;

      nll = BestNegLogLikelihood(p);

      if (nll_grad == null) {
        Interpreter.Evaluate(p, yPred);
      } else {
        yJac = new double[m, n];
        Interpreter.EvaluateWithJac(p, yPred, null, yJac);
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
      return new CCLikelihood(this);
    }
  }
}
