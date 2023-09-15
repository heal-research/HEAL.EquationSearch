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

  public class RARLikelihood : LikelihoodBase {
    private readonly double[] e_log_gobs;
    private readonly double[] e_log_gbar;
    private readonly double[][] extendedXCol;
    private readonly double[,] extendedX;
    private readonly int y_idx;
    private readonly int e_log_gbar_idx;
    private readonly int e_log_gobs_idx;

    private readonly static MethodInfo log = typeof(Math).GetMethod("Log", new Type[] { typeof(double) });
    private readonly static MethodInfo pow = typeof(Math).GetMethod("Pow", new Type[] { typeof(double), typeof(double) });


    private Expression<Expr.ParametricFunction> origExpr;
    private ExpressionInterpreter likelihoodInterpreter = null;
    private ExpressionInterpreter bestLikelihoodInterpreter = null;
    private ExpressionInterpreter[] likelihoodGradInterpreter = null;
    public ExpressionInterpreter sigmaTotInterpreter = null;
    public ExpressionInterpreter derivativeInterpreter = null;

    private double[] nllArr; // buffer for observation likelihoods
    private double[,] jacP; // buffer for jacobian

    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => origExpr;
      set {
        origExpr = value;
        this.nllArr = null;
        this.jacP = null;
        if (value != null && extendedXCol != null && extendedX != null) {
          // We create an expression for the likelihood which wraps the model expression as well as it's partial derivative over log(bar).
          // This allows us to use the autodiff interpreter for the full likelihood and simplifies the likelihood code.

          var numParam = Expr.NumberOfParameters(origExpr);

          var pParam = value.Parameters[0];
          var xParam = value.Parameters[1];

          this.nllArr = new double[y.Length];
          this.jacP = new double[y.Length, numParam];
          // this.f = new double[y.Length];
          // wrap log(f(x))
          value = value.Update(LinqExpr.Multiply(
            // LinqExpr.Call(log, LinqExpr.Call(abs, value.Body)),
            LinqExpr.Call(log, value.Body),
            LinqExpr.Constant(1.0 / Math.Log(10))), value.Parameters);

          var d_log_f_dgbar = Expr.Derive(value, value.Parameters[1], 0); // d/dx0

          var gbar_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(0));
          var e_log_gobs_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(e_log_gobs_idx));
          var e_log_gbar_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(e_log_gbar_idx));
          var y_expr = LinqExpr.ArrayIndex(xParam, LinqExpr.Constant(y_idx));
          var sTotExpr = LinqExpr.Add(
            LinqExpr.Multiply(e_log_gobs_expr, e_log_gobs_expr),
            LinqExpr.Call(pow, LinqExpr.Multiply(LinqExpr.Multiply(LinqExpr.Multiply(d_log_f_dgbar.Body, gbar_expr),
                                                                   LinqExpr.Constant(Math.Log(10))),
                                                 e_log_gbar_expr),
                               LinqExpr.Constant(2.0))
            );

          // for debugging
          // sigmaTotInterpreter = new ExpressionInterpreter(LinqExpr.Lambda<Expr.ParametricFunction>(sTotExpr, pParam, xParam), extendedXCol, y.Length);
          // derivativeInterpreter = new ExpressionInterpreter(LinqExpr.Lambda<Expr.ParametricFunction>(
          //   LinqExpr.Multiply(LinqExpr.Multiply(d_log_f_dgbar.Body, gbar_expr),
          //                                                          LinqExpr.Constant(Math.Log(10))), pParam, xParam), extendedXCol);

          var baseExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Call(log, LinqExpr.Multiply(LinqExpr.Constant(2.0 * Math.PI), sTotExpr))); //  0.5 * Math.Log(2.0 * Math.PI * stot)
          var residualExpr = LinqExpr.Multiply(LinqExpr.Constant(0.5), LinqExpr.Divide(LinqExpr.Call(pow, LinqExpr.Subtract(value.Body, y_expr), LinqExpr.Constant(2.0)), sTotExpr)); //0.5 * res * res / stot;

          Expression<Expr.ParametricFunction> likelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: LinqExpr.Add(residualExpr, baseExpr),
            pParam, xParam);

          Expression<Expr.ParametricFunction> baseLikelihoodExpr = LinqExpr.Lambda<Expr.ParametricFunction>(
            body: baseExpr,
            pParam, xParam);


          likelihoodInterpreter = new ExpressionInterpreter(likelihoodExpr, extendedXCol, y.Length);
          bestLikelihoodInterpreter = new ExpressionInterpreter(baseLikelihoodExpr, extendedXCol, y.Length);


          likelihoodGradInterpreter = new ExpressionInterpreter[numParam];
          for (int i = 0; i < numParam; i++) {
            var dLikeExpr = Expr.Derive(likelihoodExpr, i);
            likelihoodGradInterpreter[i] = new ExpressionInterpreter(dLikeExpr, extendedXCol, y.Length);

            // for debugging
            System.Console.Error.WriteLine($"df/dp_{i} number of nodes: {Expr.NumberOfNodes(dLikeExpr)}");
          }
        }

        base.ModelExpr = value;
      }
    }


    internal RARLikelihood(RARLikelihood original) : base(original) {
      this.e_log_gobs = original.e_log_gobs;
      this.e_log_gbar = original.e_log_gbar;
      this.extendedXCol = original.extendedXCol;
      this.extendedX = original.extendedX;
      this.y_idx = original.y_idx;
      this.e_log_gbar_idx = original.e_log_gbar_idx;
      this.e_log_gobs_idx = original.e_log_gobs_idx;
      ModelExpr = original.origExpr; // initializes all interpreters and gradients
    }

    public RARLikelihood(double[,] x, double[] y, Expression<Expr.ParametricFunction> modelExpr, double[] e_log_gobs, double[] e_log_gbar)
      : base(modelExpr, x, y, 0) {
      this.e_log_gobs = e_log_gobs;
      this.e_log_gbar = e_log_gbar;


      // the interpreter for the likelihood has additional variables e_log_gbar, e_log_gobs, and y
      this.extendedXCol = new double[xCol.Length + 3][];
      this.extendedX = new double[x.GetLength(0), x.GetLength(1) + 3];
      for (int i = 0; i < xCol.Length; i++) {
        extendedXCol[i] = xCol[i];
      }

      extendedXCol[xCol.Length] = e_log_gobs; this.e_log_gobs_idx = xCol.Length;
      extendedXCol[xCol.Length + 1] = e_log_gbar; this.e_log_gbar_idx = xCol.Length + 1;
      extendedXCol[xCol.Length + 2] = y.Select(Math.Log10).ToArray(); this.y_idx = xCol.Length + 2;

      for (int i = 0; i < x.GetLength(0); i++) {
        for (int j = 0; j < x.GetLength(1); j++) {
          extendedX[i, j] = x[i, j];
        }
        extendedX[i, xCol.Length] = e_log_gobs[i];
        extendedX[i, xCol.Length + 1] = e_log_gbar[i];
        extendedX[i, xCol.Length + 2] = Math.Log10(y[i]);
      }
    }

    public override double[,] FisherInformation(double[] p) {
      var m = y.Length;
      var n = p.Length;
      var tmp = new double[m];

      // FIM is the negative of the second derivative (Hessian) of the log-likelihood
      // -> FIM is the Hessian of the negative log-likelihood
      var hessian = new double[n, n];
      for (int j = 0; j < n; j++) {
        likelihoodGradInterpreter[j].EvaluateWithJac(p, tmp, null, jacP);
        // likelihoodGradFunc[j](p, extendedX, f, jacP);
        for (int i = 0; i < m; i++) {
          for (int k = 0; k < n; k++) {
            hessian[j, k] += jacP[i, k];
          }
        }
      }

      return hessian;
    }

    // for the calculation of deviance
    public override double BestNegLogLikelihood(double[] p) {
      bestLikelihoodInterpreter.Evaluate(p, nllArr);
      return nllArr.Sum();
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

        NegLogLikelihoodJacobian(p, nllArr, jacP);
        nll = nllArr.Sum();
        for (int i = 0; i < m; i++) {
          for (int j = 0; j < n; j++) {
            nll_grad[j] += jacP[i, j];
          }
        }
      } else {
        NegLogLikelihoodJacobian(p, nllArr, null);
        nll = nllArr.Sum();
      }

      // for debugging
      // if (!double.IsNaN(nll))
      //   System.IO.File.AppendAllLines(@"c:\temp\convergence_log.txt", new string[] { $"{nEvals++},{nll},{string.Join(",", p.Select(pi => pi.ToString()))},{(nll_grad != null ? string.Join(",", nll_grad.Select(xi => xi.ToString())) : "")}" });
    }

    public void NegLogLikelihoodJacobian(double[] p, double[] nll, double[,]? jac) {
      likelihoodInterpreter.EvaluateWithJac(p, nll, null, jac);
      // var f = new double[y.Length];
      // if (jac != null) {
      //   likelihoodFuncAndJac(p, extendedX, f, jac);
      // } else {
      //   likelihoodFunc(p, extendedX, f);
      // }
      // return f;
    }

    public override LikelihoodBase Clone() {
      return new RARLikelihood(this);
    }
  }
}
