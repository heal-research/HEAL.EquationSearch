using TreesearchLib;

namespace HEAL.EquationSearch {

  /// <summary>
  /// Most important class for Treesearchlib. This class defines the search tree (via the grammar)
  /// </summary>
  public class State : IState<State, MinimizeDouble> {
    private readonly Data data;
    private readonly int maxLength;
    private readonly Grammar grammar;
    private readonly Expression expression;
    private readonly IEvaluator evaluator;
    internal Data Data => data;
    public Grammar Grammar => grammar;
    public Expression Expression => expression;
    internal IEvaluator Evaluator => evaluator;

    public State(Data data, int maxLength, Grammar grammar, IEvaluator evaluator) {
      this.data = data;
      this.maxLength = maxLength;
      this.grammar = grammar;
      this.evaluator = evaluator;
      this.expression = new Expression(grammar, new[] { grammar.Start });
    }

    public State(Data data, int maxLength, Grammar grammar, Expression expression, IEvaluator evaluator) {
      this.data = data;
      this.maxLength = maxLength;
      this.grammar = grammar;
      this.expression = expression;
      this.evaluator = evaluator;
    }

    public State(State original) {
      this.data = original.data;
      this.quality = original.quality;
      this.maxLength = original.maxLength;
      this.grammar = original.grammar;
      this.expression = original.expression;
      this.evaluator = original.evaluator;
    }

    public bool IsTerminal => expression.Length >= maxLength || expression.IsSentence;

    public MinimizeDouble Bound => new MinimizeDouble(double.NegativeInfinity);

    private MinimizeDouble? quality = null; // cache quality to prevent duplicate evaluation (TODO: necessary?)
    public MinimizeDouble? Quality {
      get {
        if (quality.HasValue) return quality;

        if (expression.IsSentence) {
          quality = new MinimizeDouble(evaluator.OptimizeAndEvaluateDL(expression, data)); // we use MSE as heuristic but MDL for the quality of states
          return quality;
        } 

        return null;
      }      
    }


    public object Clone() {
      return new State(this);
    }

    public IEnumerable<State> GetBranches() {
      return Grammar.CreateAllDerivations(expression)
        .Where(expr => expr.Length <= maxLength)
        .Select(expr => new State(data, maxLength, grammar, expr, evaluator));

      // TODO: order by heuristic value?
    }

    public override string ToString() {
      if (expression.IsSentence) return expression.ToInfixString();
      else return expression.ToString() ?? "<empty>";
    }

    internal ulong GetHashValue() {
      return Semantics.GetHashValue(expression);
    }
  }
}
