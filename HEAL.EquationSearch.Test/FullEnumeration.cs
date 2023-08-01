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
    [DataRow(1, 1)] // p
    [DataRow(5, 2)] // p * x + p 
    [DataRow(7, 3)] // p * x * x + p

    // p * x * x * x + p
    [DataRow(9, 4)]

    // p * x + p * x * x + p 
    // p * x * x * x * x + p
    [DataRow(11, 6)]

    public void OneDimensionalPolynomial(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 1);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x" };

      var grammar = new Grammar(varNames);
      grammar.UsePolynomialRestrictedRules();
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new CountUniqueExpressionsEvaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.EvaluatedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.EvaluatedExpressions); // 
    }

    [DataTestMethod]
    [DataRow(1, 1)] // p
    [DataRow(5, 3)] // p (x|y) * p + 

    // 7 p * x * x + p
    // 7 p * x * y + p
    // 7 p * y * y + p
    [DataRow(7, 6)]

    // p * x + p * y + p 
    // p * x * x * x + p 
    // p * x * x * y + p
    // p * x * y * y + p
    // p * y * y * y + p
    [DataRow(9, 11)]

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
    [DataRow(11, 22)]


    public void TwoDimensionalPolynomial(int maxLength, int expectedEvaluations) {
      var rand = new Random(1234);
      var x = Util.GenerateRandom(rand, 100, 2);
      var y = Util.GenerateRandom(rand, 100);

      var varNames = new string[] { "x", "y" };

      var grammar = new Grammar(varNames); grammar.UsePolynomialRestrictedRules();
      var data = new Data(varNames, x, y, invNoiseVariance: Enumerable.Repeat(1.0, y.Length).ToArray());
      var evaluator = new CountUniqueExpressionsEvaluator();
      var control = SearchControl<State, MinimizeDouble>.Start(new State(data, maxLength, grammar, evaluator))
        .BreadthFirst();
      System.Console.WriteLine($"Visited nodes: {control.VisitedNodes} evaluations: {evaluator.EvaluatedExpressions}");
      Assert.AreEqual(expectedEvaluations, evaluator.EvaluatedExpressions); // 
    }    
  }
}