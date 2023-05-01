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
  [DataRow(6)]
  [DataRow(7)]
  [DataRow(8)]
  [DataRow(9)]
  [DataRow(10)]
  public void OneDimensionalSpace(int maxLength) {
    var varNames = new [] { "x" };

    var grammar = new Grammar(varNames);
    grammar.UseCoreMathGrammar();
    EnumerateAll(grammar, maxLength);
  }

  private void EnumerateAll(Grammar grammar, int maxComplexity) {
    
    var allFunctionsFile = $"generated_complexity_{maxComplexity}.txt";
    GenerateAllToFile(grammar, maxComplexity, allFunctionsFile);
    System.Console.WriteLine($"All functions generated in file {Path.GetFullPath(allFunctionsFile)}.\n\n");

    var uniqueFunctionsFile = $"generated_complexity_{maxComplexity}_normalized.txt";
    NormalizeFilesWithSymPy(allFunctionsFile, uniqueFunctionsFile);
    System.Console.WriteLine($"Unique functions written to {Path.GetFullPath(uniqueFunctionsFile)}.\n\n");

    var esrFunctions = ReadEsrFunctionSet(maxComplexity);
    var generatedFunctions = ReadGeneratedFunctions(uniqueFunctionsFile);
    
    CompareFunctionSets(esrFunctions, generatedFunctions);
    
    System.Console.WriteLine($"{esrFunctions.Length} ESR functions\n{generatedFunctions.Length} generated functions\n");
  }

  private void GenerateAllToFile(Grammar grammar, int maxComplexity, string filename) {
    var visitedSentences = new HashSet<ulong>();
    var openPhrases = new Queue<Expression>();
    openPhrases.Enqueue(new Expression(grammar, new[] { grammar.Start }));
    
    using StreamWriter resultWriter = new StreamWriter(filename);

    while (openPhrases.Any()) {
      var peek = openPhrases.Dequeue();
      foreach (var child in grammar.CreateAllDerivations(peek)) {
        if (GetComplexity(child) > maxComplexity) continue;

        if (child.IsSentence) {
          var childHash = Semantics.GetHashValue(child);

          if (!visitedSentences.Contains(childHash)) {
            visitedSentences.Add(childHash);

            // Format and output child            
            SetCoefficientsToZero(child);

            var childString = child.ToInfixString();

            // prepare parameters
            childString = childString.Replace("0", "p");
            resultWriter.WriteLine(childString);
          }
        } else {
          openPhrases.Enqueue(child);
        }
      }
    }
  }

  private void NormalizeFilesWithSymPy(string srcFile, string targetFile) {
    // Simplify the remaining function with SymPy --> REQUIRES PYTHON AND SYMPY!!!
    var process = System.Diagnostics.Process.Start("python3", 
      arguments: $"function_sets/normalize_generated.py {srcFile} {targetFile}");
    process.WaitForExit();
  }

  private void CompareFunctionSets(string[] esrFunctions, string[] generatedFunctions) {
    System.Console.WriteLine("   ESR Functions that are not generated:");
    int notGeneratedCount = 0;
    foreach (var esrFunc in esrFunctions) {
      if (!generatedFunctions.Contains(esrFunc)) {
        System.Console.WriteLine(esrFunc);
        notGeneratedCount++;
      }
    }

    int notInEsrCount = 0;
    System.Console.WriteLine("\n   Generated Function that are not in the ESR function set.");
    foreach (var generatedFunc in generatedFunctions) {
      if (!esrFunctions.Contains(generatedFunc)) {
        System.Console.WriteLine(generatedFunc);
        notInEsrCount++;
      }
    }
    
    System.Console.WriteLine($"   {notGeneratedCount} ESR functions found that are not generated ");
    System.Console.WriteLine($"   {notInEsrCount} generated functions that are not in ESR");
  }

  private void SetCoefficientsToZero(Expression expr) {
    foreach (var sym in expr.OfType<Grammar.ParameterSymbol>()) {
      sym.Value = 0.0;
    }
  }

  private string[] ReadEsrFunctionSet(int complexity) {
    string file = $"function_sets/core_maths/unique_equations_{complexity}_normalized.txt";
    return File.ReadAllLines(file);
  }

  private string[] ReadGeneratedFunctions(string file) {
    return File.ReadAllLines(file);
  }

  private int GetComplexity(Expression expr) {
    return expr.Count(symbol => symbol != expr.Grammar.One);
  }
}
