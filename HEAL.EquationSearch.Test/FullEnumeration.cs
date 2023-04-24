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
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
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
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
    }

    [DataTestMethod]
    [DataRow(1, 0)] // an expression that only has an intercept is not evaluated
    [DataRow(2, 0)] 
    [DataRow(3, 0)] 
    [DataRow(4, 0)] 
    [DataRow(5, 1)] // p x * p +
    [DataRow(6, 1)] 
    [DataRow(7, 2)] // p x x * * p +
    [DataRow(8, 3)] // len: 8 p p x * exp * p +

    // len: 9 p x x * x * * p +
    [DataRow(9, 4)]

    // len: 10 p p x x * * exp * p +
    // len: 10 p x p x * exp * * p +
    // len: 10 p x p x * exp * * p +
    // len: 10 p p x * p + cos * p +
    [DataRow(10, 7)]

     // len: 11 p p x * 1 + 1 / * p +
     // len: 11 p p x * 1 + abs log * p +
     // len: 11 p x * p x x * * p + +
     // len: 11 p x x * x * x * * p +
    [DataRow(11, 11)]


    // len: 12  p x * p p x * exp * p + +
    // len: 12  p p x x x * * * exp * p +
    // len: 12  p x p x x * * exp * * p +
    // len: 12  p x x * p x * exp * * p +
    // len: 12  p x p x * p + cos * * p +
    // len: 12  p p x x * * p + cos * p +
    [DataRow(12, 17)]

    public void OneDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
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

    // p p x * p + cos * p +
    // p p y * p + cos * p +
    [DataRow(10, 21)]

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

    // p p x x * * p + cos * p +    p*cos(p*x*x + p) + p
    // p p x y * * p + cos * p +    p*cos(p*x*y + p) + p
    // p p y y * * p + cos * p +    p*cos(p*y*y + p) + p
    // p x p x * p + cos * * p +    p*x*cos(p*x + p) + p
    // p x p y * p + cos * * p +    p*x*cos(p*y + p) + p
    // p y p x * p + cos * * p +    p*y*cos(p*x + p) + p
    // p y p y * p + cos * * p +    p*y*cos(p*y + p) + p
    [DataRow(12, 63)]

    public void TwoDimensional(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x", "y" };

      var grammar = new Grammar(varNames);
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new Evaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.OptimizedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.OptimizedExpressions); // 
    }
  }
}