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
    
    var allFunctionsFile = $"function_sets/core_maths/generated_complexity_{maxComplexity}.txt";
    GenerateAllToFile(grammar, maxComplexity, allFunctionsFile);
    System.Console.WriteLine($"All functions generated in file {Path.GetFullPath(allFunctionsFile)}.\n\n");

    var uniqueFunctionsFile = $"function_sets/core_maths/generated_complexity_{maxComplexity}_normalized.txt";
    NormalizeFilesWithSymPy(allFunctionsFile, uniqueFunctionsFile);
    System.Console.WriteLine($"Unique functions written to {Path.GetFullPath(uniqueFunctionsFile)}.\n\n");

    var esrFunctions = ReadEsrFunctionSet(maxComplexity);
    var generatedFunctions = ReadGeneratedFunctions(uniqueFunctionsFile);

    var total = esrFunctions.Union(generatedFunctions).ToList();
    var intersect = esrFunctions.Intersect(generatedFunctions).ToList();
    var notGenerated = esrFunctions.Except(generatedFunctions).ToList();
    var notInEsr = generatedFunctions.Except(esrFunctions).ToList();
    
    System.Console.WriteLine($"{esrFunctions.Length} ESR functions\n" +
                             $"{generatedFunctions.Length} generated functions\n" +
                             $"\n" +
                             $"{total.Count} distinct functions in total\n" +
                             $"{intersect.Count} functions overlap\n" +
                             $"{notGenerated.Count} ESR functions that are not generated\n" +
                             $"{notInEsr.Count} generated function that are not in ESR\n");

    System.Console.WriteLine($"intersecting:\n\n{string.Join("  ,  ", intersect)}\n\n");

    System.Console.WriteLine($"not generated:\n\n{string.Join("  ,  ", notGenerated)}\n\n");
    System.Console.WriteLine($"not in ESR:\n\n{string.Join("  ,  ", notInEsr)}");
  }

  private void GenerateAllToFile(Grammar grammar, int maxComplexity, string filename) {
    var visitedSentences = new HashSet<ulong>();
    var openPhrases = new Queue<Expression>();
    openPhrases.Enqueue(new Expression(grammar, new[] { grammar.Start }));
    
    using StreamWriter resultWriter = new StreamWriter(filename);

    while (openPhrases.Any()) {
      var peek = openPhrases.Dequeue();
      foreach (var child in grammar.CreateAllDerivations(peek)) {
        // Since ESR uses a different logic for Abs symbols, we filter them out for now.
        if (GetComplexity(child) > maxComplexity) continue;

        if (child.IsSentence) {
          var childNormalized = new Expression(grammar, child.SymbolString.Where(s => s != grammar.Abs).ToArray());

          var childHash = Semantics.GetHashValue(childNormalized);

          if (!visitedSentences.Contains(childHash)) {
            visitedSentences.Add(childHash);

            // Format and output child            
            SetCoefficientsToZero(childNormalized);

            var childString = childNormalized.ToInfixString();

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

  private void SetCoefficientsToZero(Expression expr) {
    foreach (var sym in expr.OfType<Grammar.ParameterSymbol>()) {
      sym.Value = 0.0;
    }
  }

  private string[] ReadEsrFunctionSet(int complexity) {
    string file = $"function_sets/core_maths/unique_equations_{complexity}_cum_normalized.txt";
    return File.ReadAllLines(file);
  }

  private string[] ReadGeneratedFunctions(string file) {
    return File.ReadAllLines(file);
  }

  private int GetComplexity(Expression expr) {
    return expr.Count(symbol => symbol != expr.Grammar.One && symbol != expr.Grammar.Abs);
  }
}
