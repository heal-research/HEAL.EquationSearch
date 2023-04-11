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
    [DataRow(5, 1)] // p * x + p 
    [DataRow(7, 2)] // p * x * x + p

    // p * x * x * x + p
    [DataRow(9, 3)]

    // p * x + p * x * x + p 
    // p * x * x * x * x + p
    [DataRow(11, 5)]

    public void OneDimensionalPolynomial(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x" };

      var grammar = new Grammar(varNames);
      grammar.UsePolynomialRules();
      var data = new Data(varNames, x, y);
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
    }

    [DataTestMethod]
    [DataRow(5, 2)] // p (x|y) * p + 

    // 7 p * x * x + p
    // 7 p * x * y + p
    // 7 p * y * y + p
    [DataRow(7, 5)]

    // p * x + p * y + p 
    // p * x * x * x + p 
    // p * x * x * y + p
    // p * x * y * y + p
    // p * y * y * y + p
    [DataRow(9, 10)]

    // p * x + p * x * x + p
    // p * x + p * x * y + p
    // p * x + p * y * y + p
    // p * y + p * x * x + p 
    // p * y + p * x * y + p
    // p * y + p * y * y + p 
    // p * x * x * x * x + p
    // p * x * x * x * y + p
    // p * x * x * y * y + p
    // p * x * y * y * y + p
    // p * y * y * y * y + p
    [DataRow(11, 21)]


    public void TwoDimensionalPolynomial(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x", "y" };

      var grammar = new Grammar(varNames); grammar.UsePolynomialRules();
      var data = new Data(varNames, x, y);
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
    }

    [DataTestMethod]
    [DataRow(1, 0)] // an expression that only has an intercept is not evaluated
    [DataRow(2, 0)] 
    [DataRow(3, 0)] 
    [DataRow(4, 0)] 
    [DataRow(5, 1)] // 0.009979 x * 0.4848 +
    [DataRow(6, 1)] 
    [DataRow(7, 2)] // -0.03922 x x * * 0.5021 +
    [DataRow(8, 3)] // len: 8 -0.1587 -8.429 x * exp * 0.5075 +

    // len: 9 -0.07303 x x * x * * 0.5064 +
    [DataRow(9, 4)]

    // len: 10 -2.445e-05 9.84 x x * * exp * 0.5049 +
    // len: 10 -2.935e-08 x 16.48 x * exp * * 0.5049 +
    [DataRow(10, 6)]

     // len: 11 -0.0001243 -1.123 x * 1 + 1 / * 0.4938 +
     // len: 11 0.01543 -1.123 x * 1 + abs log * 0.5099 +
     // len: 11 0.7447 x * -0.7458 x x * * 0.3601 + +
     // len: 11 -0.1003 x x * x * x * * 0.5075 +
    [DataRow(11, 10)]


    // len: 12  0.4674 x * -0.01764 3.499 x * exp * 0.4112 + +
    // len: 12 -0.0003475 7.263 x x x * * * exp * 0.5052 +
    // len: 12  0.7164 x -2.483 x x * * exp * * 0.3524 +
    // len: 12 -1.028e-07 x x * 15.21 x * exp * * 0.505 +
    [DataRow(12, 14)]

    public void OneDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y);
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
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

    // p x * p y * p + +
    // p x x * x * * p +
    // p x x * y * * p +
    // p x y * y * * p +
    // p y y * y * * p +
    [DataRow(9, 12)]

    // p p x x * * exp * p +
    // p p x y * * exp * p +
    // p p y y * * exp * p +

    // p x p x * exp * * p +
    // p x p y * exp * * p +
    // p y p x * exp * * p +
    // p y p y * exp * * p +
    [DataRow(10, 19)]

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
    [DataRow(11, 34)]

    // p x * p p x * exp * p + +
    // p x * p p y * exp * p + +
    // p y * p p x * exp * p + +
    // p y * p p y * exp * p + +
    // p p x x x * * * exp * p +
    // p p x x y * * * exp * p +
    // p p x y y * * * exp * p +
    // p p y y y * * * exp * p +
    // p x p x x * * exp * * p +
    // p x p x y * * exp * * p +
    // p x p y y * * exp * * p +
    // p y p x x * * exp * * p +
    // p y p x y * * exp * * p +
    // p y p y y * * exp * * p +
    // p x x * p x * exp * * p +
    // p x x * p y * exp * * p +
    // p x y * p x * exp * * p +
    // p x y * p y * exp * * p +
    // p y y * p x * exp * * p +
    // p y y * p y * exp * * p +
    [DataRow(12, 54)]

    public void TwoDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x", "y" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y);
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
    }
  }
}