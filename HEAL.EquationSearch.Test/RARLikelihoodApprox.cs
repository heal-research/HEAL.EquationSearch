using HEAL.Expressions;
using HEAL.NonlinearRegression;
using System.Linq.Expressions;
using System.Reflection;
using LinqExpr = System.Linq.Expressions.Expression;

namespace HEAL.EquationSearch.Test {

  // as reported in https://arxiv.org/pdf/2301.04368.pdf Eq. 3
  // additional info from Harry:
  // e_loggbar = e_gbar / (gbar * np.log(10.))
  // e_loggobs = e_gobs / (gobs * np.log(10.))
  
  // sigma2_tot = e_loggobs**2 + (gobs1_diff*e_loggbar)**2
  // negloglike = 0.5 * np.sum((np.log10(gobs) - np.log10(gobs1))**2 ./ sigma2_tot + np.log(2.* np.pi * sigma2_tot))

  // partial derivatives of sigma_tot are ignored

  public class RARLikelihoodApprox : LikelihoodBase {
    private readonly double[] e_log_gobs;
    private readonly double[] e_log_gbar;
    private readonly double[] sigma_tot;
    private readonly double[][] extendedXCol;
    private readonly int y_idx;
    private readonly int e_log_gbar_idx;
    private readonly int e_log_gobs_idx;
    private readonly int sigma_tot_idx;

    private readonly static MethodInfo log = typeof(Math).GetMethod("Log", new Type[] { typeof(double) });
    private readonly static MethodInfo abs = typeof(Math).GetMethod("Abs", new Type[] { typeof(double) });
    private readonly static MethodInfo pow = typeof(Math).GetMethod("Pow", new Type[] { typeof(double), typeof(double) });


    private Expression<Expr.ParametricFunction> origExpr;
    private ExpressionInterpreter likelihoodInterpreter = null;
    private ExpressionInterpreter bestLikelihoodInterpreter = null;
    private ExpressionInterpreter[] likelihoodGradInterpreter = null;
    public ExpressionInterpreter sigmaTotInterpreter = null;
    public ExpressionInterpreter derivativeInterpreter = null;

    private double[,] jacP; // buffer for jacobian
    // private double[] f;
    // private Expr.ParametricVectorFunction likelihoodFunc;
    // private Expr.ParametricJacobianFunction likelihoodFuncAndJac;
    // private Expr.ParametricJacobianFunction bestLikelihoodFunc;
    // private Expr.ParametricJacobianFunction[] likelihoodGradFunc;

    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => origExpr;
      set {
        origExpr = value;
        this.jacP = null;
        if (value != null && extendedXCol != null) {
          // We create an expression for the likelihood which wraps the model expression as well as it's partial derivative over log(bar).
          // This allows us to use the autodiff interpreter for the full likelihood and simplifies the likelihood code.

          var numParam = Expr.NumberOfParameters(origExpr);

          var pParam = value.Parameters[0];
          var xParam = value.Parameters[1];

          this.jacP = new double[y.Length, numParam];
          // this.f = new double[y.Length];

          // wrap log(f(x))
          value = value.Update(LinqExpr.Multiply(
            // LinqExpr.Call(log, LinqExpr.Call(abs, value.Body)),
            LinqExpr.Call(log, value.Body),
            LinqExpr.Constant(1.0 / Math.Log(10))), value.Parameters);

          var d_log_f_dgbar = Expr.Derive(value, value.Parameters[1], 0); // d/dx0
          derivativeInterpreter = new ExpressionInterpreter(d_log_f_dgbar, extendedXCol, y.Length);

          var sTotExpr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(sigma_tot_idx));
          var y_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(y_idx));

          var baseExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Call(log, LinqExpr.Multiply(LinqExpr.Constant(2.0 * Math.PI), sTotExpr))); //  0.5 * Math.Log(2.0 * Math.PI * stot)
          var residualExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Divide(LinqExpr.Call(pow, LinqExpr.Subtract(value.Body, y_expr), LinqExpr.Constant(2.0)), sTotExpr)); //0.5 * res * res / stot;

          Expression<Expr.ParametricFunction> likelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: LinqExpr.Add(residualExpr, baseExpr),
            pParam, xParam);

          Expression<Expr.ParametricFunction> baseLikelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: baseExpr,
            pParam, xParam);


          // for debugging
          // sigmaTotInterpreter = new ExpressionInterpreter(LinqExpr.Lambda<Expr.ParametricFunction>(sTotExpr, pParam, xParam), extendedXCol);
          // derivativeInterpreter = new ExpressionInterpreter(LinqExpr.Lambda<Expr.ParametricFunction>(
          //   LinqExpr.Multiply(LinqExpr.Multiply(d_log_f_dgbar.Body, gbar_expr),
          //                                                          LinqExpr.Constant(Math.Log(10))), pParam, xParam), extendedXCol);


          likelihoodInterpreter = new ExpressionInterpreter(likelihoodExpr, extendedXCol, y.Length);
          bestLikelihoodInterpreter = new ExpressionInterpreter(baseLikelihoodExpr, extendedXCol, y.Length);
          // likelihoodFunc = Expr.Broadcast(likelihoodExpr).Compile();
          // likelihoodFuncAndJac = Expr.Jacobian(likelihoodExpr, numParam).Compile();
          // bestLikelihoodFunc = Expr.Jacobian(baseLikelihoodExpr, numParam).Compile();

          likelihoodGradInterpreter = new ExpressionInterpreter[numParam];
          // likelihoodGradFunc = new Expr.ParametricJacobianFunction[numParam];
          for (int i = 0; i < numParam; i++) {
            var dLikeExpr = Expr.Derive(likelihoodExpr, i);
            likelihoodGradInterpreter[i] = new ExpressionInterpreter(dLikeExpr, extendedXCol, y.Length);
            // likelihoodGradFunc[i] = Expr.Jacobian(dLikeExpr, numParam).Compile();

            // for debugging
            System.Console.Error.WriteLine($"df/dp_{i} number of nodes: {Expr.NumberOfNodes(dLikeExpr)}");
          }
        }

        base.ModelExpr = value;
      }
    }


    internal RARLikelihoodApprox(RARLikelihoodApprox original) : base(original) {
      this.e_log_gobs = original.e_log_gobs;
      this.e_log_gbar = original.e_log_gbar;
      this.sigma_tot = (double[])original.sigma_tot.Clone();
      this.extendedXCol = original.extendedXCol.Select(xi => (double[])xi.Clone()).ToArray();
      this.y_idx = original.y_idx;
      this.e_log_gbar_idx = original.e_log_gbar_idx;
      this.e_log_gobs_idx = original.e_log_gobs_idx;
      this.sigma_tot_idx = original.sigma_tot_idx;
      ModelExpr = original.origExpr; // initializes all interpreters and gradients
    }

    public RARLikelihoodApprox(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] e_log_gobs, double[] e_log_gbar, double[] sigma_tot)
      : base(modelExpr, x, y, 0) {
      this.e_log_gobs = e_log_gobs;
      this.e_log_gbar = e_log_gbar;
      this.sigma_tot = (double[])sigma_tot.Clone();


      // the interpreter for the likelihood has additional variables e_log_gbar, e_log_gobs, and y
      this.extendedXCol = new double[xCol.Length + 4][];
      for (int i = 0; i < xCol.Length; i++) {
        extendedXCol[i] = xCol[i];
      }

      extendedXCol[xCol.Length] = e_log_gobs; this.e_log_gobs_idx = xCol.Length;
      extendedXCol[xCol.Length + 1] = e_log_gbar; this.e_log_gbar_idx = xCol.Length + 1;
      extendedXCol[xCol.Length + 2] = sigma_tot; this.sigma_tot_idx = xCol.Length + 2;
      extendedXCol[xCol.Length + 3] = y.Select(Math.Log10).ToArray(); this.y_idx = xCol.Length + 3;

      ModelExpr = modelExpr;
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;

      UpdateSigmaTot(p);

      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessian = new double[n, n];
      for (int j = 0; j < n; j++) {
        likelihoodGradInterpreter[j].EvaluateWithJac(p, null, jacP);
        // likelihoodGradFunc[j](p, extendedX, f, jacP);
        for (int i = 0; i < m; i++) {
          for (int k = 0; k < n; k++) {
            hessian[j, k] += jacP[i, k];
          }
        }
      }

      return hessian;
    }

    private void UpdateSigmaTot(double[] p) {
      int m = y.Length;
      var df_dgbar = derivativeInterpreter.Evaluate(p);
      for (int i = 0; i < m; i++) {
        extendedXCol[sigma_tot_idx][i] = e_log_gobs[i] * e_log_gobs[i] + Math.Pow(df_dgbar[i] * xCol[0][i] * Math.Log(10) * e_log_gbar[i], 2);
      }
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      UpdateSigmaTot(p);
      return bestLikelihoodInterpreter.Evaluate(p).Sum();
      // var f = new double[y.Length];
      // bestLikelihoodFunc(p, extendedX, f, null);
      // return f.Sum();
    }

    public override double NegLogLikelihood(double[] p) {
      NegLogLikelihoodGradient(p, out var nll, nll_grad: null);
      return nll;
    }

    public override void NegLogLikelihoodGradient(double[] p, out double nll, double[]? nll_grad) {
      var m = y.Length;
      var n = p.Length;

      if (nll_grad != null) {
        Array.Clear(nll_grad);

        var nllArr = NegLogLikelihoodJacobian(p, jacP);
        nll = nllArr.Sum();
        for (int i = 0; i < m; i++) {
          for (int j = 0; j < n; j++) {
            nll_grad[j] += jacP[i, j];
          }
        }
      } else {
        nll = NegLogLikelihoodJacobian(p, null).Sum();
      }
    }

    public double[] NegLogLikelihoodJacobian(double[] p, double[,]? jac) {
      UpdateSigmaTot(p);

      return likelihoodInterpreter.EvaluateWithJac(p, null, jac);
      // var f = new double[y.Length];
      // if (jac != null) {
      //   likelihoodFuncAndJac(p, extendedX, f, jac);
      // } else {
      //   likelihoodFunc(p, extendedX, f);
      // }
      // return f;
    }

    public override LikelihoodBase Clone() {
      return new RARLikelihoodApprox(this);
    }
  }
}
