using System.Linq.Expressions;
using System.Reflection;
using HEAL.Expressions;
using HEAL.NonlinearRegression;

namespace HEAL.EquationSearch.Test {
  internal class BernoulliLikelihoodWithLogisticLink : BernoulliLikelihood {
    private static MethodInfo invLogistic = typeof(Functions).GetMethod("Logistic", new[] { typeof(double) });
    private Expression<Expr.ParametricFunction> innerModelExpr;


    public override Expression<Expr.ParametricFunction> ModelExpr {
      get => innerModelExpr;
      set {
        innerModelExpr = value;
        if (value == null) base.ModelExpr = null;
        else base.ModelExpr = System.Linq.Expressions.Expression.Lambda<Expr.ParametricFunction>(
          System.Linq.Expressions.Expression.Call(invLogistic, new[] { value.Body }),
          value.Parameters);
      }
    }

    public BernoulliLikelihoodWithLogisticLink(double[,] trainX, double[] trainY, Expression<Expr.ParametricFunction> modelExpr) : base(trainX, trainY, modelExpr) {
      innerModelExpr = modelExpr;
    }

    public BernoulliLikelihoodWithLogisticLink(BernoulliLikelihoodWithLogisticLink orig) : base(orig) {
      this.innerModelExpr = orig.innerModelExpr;
    }

    public override LikelihoodBase Clone() {
      return new BernoulliLikelihoodWithLogisticLink(this);
    }
  }
}