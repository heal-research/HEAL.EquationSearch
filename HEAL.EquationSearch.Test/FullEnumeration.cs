using Newtonsoft.Json.Linq;
using TreesearchLib;

namespace HEAL.EquationSearch.Test {
  [TestClass]
  public class FullEnumeration {
    [TestInitialize]
    public void Init() {
      Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
      System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    }

    [DataTestMethod]
    [DataRow(1, 0)] // an expression that only has an intercept is not evaluated
    [DataRow(2, 0)] 
    [DataRow(3, 0)] 
    [DataRow(4, 0)] 
    [DataRow(5, 1)] // p x * p + 
    [DataRow(6, 1)] 
    [DataRow(7, 2)] // p x x * * p +
    [DataRow(8, 3)] // p p x * exp * p +

    // p x * p x * p + +      // TODO: this should be prevented because of distributivity
    // p x x * x * * p +
    [DataRow(9, 5)] 

    // p p x x * * exp * p +
    // p x p x * exp * * p +
    [DataRow(10, 7)]

    // p p x * 1 + 1 / * p +
    // p p x * 1 + abs log * p +
    // p p p x * exp * exp * p +
    // p x * p x x * * p + +
    // p x x * x * x * * p +
    [DataRow(11, 12)]

    // p x * p p x * exp * p + +
    // p p x x * x * * exp * p +
    // p x p x x * * exp * * p +
    // p x x * p x * exp * * p +
    [DataRow(12, 16)]

    public void OneDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y);
      Evaluator.OptimizedExpressions = 0;
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {Evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, Evaluator.OptimizedExpressions); // 
    }

    [DataTestMethod]
    [DataRow(1, 0)]
    [DataRow(2, 0)]
    [DataRow(3, 0)]
    [DataRow(4, 0)]
    [DataRow(5, 2)] // p (x|y) * p + 
    [DataRow(6, 2)]

    // p x x * * p +
    // p x y * * p +
    // p y y * * p +
    [DataRow(7, 5)]

    // p p x * exp * p +
    // p p y * exp * p +
    [DataRow(8, 7)]

    // p x * p x * p + +      // TODO: this should be prevented because of distributivity
    // p x * p y * p + +
    // p y * p y * p + +     // TODO: this should be prevented because of distributivity

    // p x x * x * * p +
    // p x x * y * * p +
    // p x y * y * * p +
    // p y y * y * * p +
    [DataRow(9, 14)]

    // p p x x * * exp * p +
    // p p x y * * exp * p +
    // p p y y * * exp * p +
    // p p y x * * exp * p +  // TODO should be prevented (commutativity in factors)

    // p x p x * exp * * p +
    // p x p y * exp * * p +
    // p y p y * exp * * p +
    // p y p x * exp * * p + // TODO should be prevented (commutativity in factors)
    [DataRow(10, 20)]

    // p p x * 1 + 1 / * p +
    // p p y * 1 + 1 / * p +
    // p p x * 1 + abs log * p +
    // p p y * 1 + abs log * p +
    // p p p x * exp * exp * p +
    // p p p y * exp * exp * p +

    // p x * p x x * * p + +
    // p x * p x y * * p + +
    // p x * p y y * * p + +
    // p y * p y y * * p + +

    // p x x * x * x * * p +
    // p x x * x * y * * p +
    // p x x * y * y * * p +
    // p x y * y * y * * p +
    // p x y * y * y * * p +
    // p y y * y * y * * p +
    [DataRow(11, 36)]

    // p x * p p x * exp * p + +
    // p x * p p y * exp * p + +
    // p y * p p y * exp * p + +

    // p p x x * x * * exp * p +
    // p p x x * y * * exp * p +
    // p p x y * y * * exp * p +
    // p p y y * y * * exp * p +

    // p x p x x * * exp * * p +
    // p x p x y * * exp * * p +
    // p x p y y * * exp * * p +
    // p y p y y * * exp * * p +


    // p x x * p x * exp * * p +
    // p x x * p y * exp * * p +
    // p x y * p y * exp * * p +
    // p y y * p y * exp * * p +
    [DataRow(12, 51)]

    public void TwoDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x", "y" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y);
      Evaluator.OptimizedExpressions = 0;
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {Evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, Evaluator.OptimizedExpressions); // 
    }
  }
}