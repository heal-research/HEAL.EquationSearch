namespace HEAL.EquationSearch.Test; 

[TestClass]
public class EsrBaseFunctionGeneration {
  public void Init() {
    Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
    System.Globalization.CultureInfo.DefaultThreadCurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
  }
  
  [DataTestMethod]
  [DataRow(1)]
  [DataRow(2)]
  [DataRow(3)]
  [DataRow(4)]
  [DataRow(5)]
  public void OneDimensionalSpace(int maxLength) {
    var varNames = new [] { "x" };

    var grammar = new Grammar(varNames);
    grammar.UseCoreMathGrammar();
    EnumerateAll(grammar, maxLength);
  }

  private void EnumerateAll(Grammar grammar, int maxComplexity) {
    var openPhrases = new Queue<Expression>();
    openPhrases.Enqueue(new Expression(grammar, new[] { grammar.Start }));

    var visitedSentences = new HashSet<ulong>();

    var linearModelHash = Semantics.GetHashValue(new Expression(grammar,
      new[] { grammar.Variables[0], grammar.Parameter, grammar.Times, grammar.Parameter, grammar.Plus}));

    List<string> generatedFunctions = new List<string>();
    
    while(openPhrases.Any()) {
      var peek = openPhrases.Dequeue();
      foreach(var child in grammar.CreateAllDerivations(peek)) {
        if(GetComplexity(child) > maxComplexity) continue;
        
        if (child.IsSentence) {
          var childHash = Semantics.GetHashValue(child);
          
          if (!visitedSentences.Contains(childHash)) {
            visitedSentences.Add(childHash);

            // Format and output child            
            SetCoefficientsToZero(child);

            var childString = child.ToInfixString();
            //childString = string.Join(" + ", childString.Split(" + ").OrderBy(t => t));
            
            generatedFunctions.Add(childString);
            System.Console.WriteLine(childString);
          }
        } else {
          openPhrases.Enqueue(child);
        }
      }
    }
    
    System.Console.WriteLine($"{generatedFunctions.Count} functions generated");
    
    var esrFunctions = ReadEsrFunctionSet(maxComplexity);
    CompareFunctionSets(esrFunctions, generatedFunctions.ToArray());
  }

  private void CompareFunctionSets(string[] esrFunctions, string[] generatedFunctions) {
    System.Console.WriteLine("\nESR Functions that are not generated:");
    foreach (var esrFunction in esrFunctions) {
      if (!generatedFunctions.Contains(esrFunction)) {
        System.Console.WriteLine(esrFunction);
      }
    }
    
    System.Console.WriteLine("\nGenerated Function that are not in the ESR function set.");
    foreach (var generatedFunction in generatedFunctions) {
      if (!esrFunctions.Contains(generatedFunction)) {
        System.Console.WriteLine(generatedFunction);
      }
    }
  }

  private void SetCoefficientsToZero(Expression expr) {
    foreach (var sym in expr) {
      if (sym is Grammar.ParameterSymbol paramSym) {
        paramSym.Value = 0.0;
      }
    }
  }

  private string[] ReadEsrFunctionSet(int complexity) {
    string file = $"function_sets/core_maths/unique_equations_{complexity}_normalized.txt";
    
    return File.ReadLines(file)
      //.Select(line => string.Join(" + ", line.Split(" + ").OrderBy(t => t)))
      .ToArray();
  }

  private int GetComplexity(Expression expr) {
    return expr.Count(symbol => symbol != expr.Grammar.One);
  }
}
