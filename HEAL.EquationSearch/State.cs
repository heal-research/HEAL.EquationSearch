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
    private readonly Evaluator evaluator;
    internal Data Data => data;
    public Grammar Grammar => grammar;
    public Expression Expression => expression;
    internal Evaluator Evaluator => evaluator;

    public State(Data data, int maxLength, Grammar grammar, Evaluator evaluator) {
      this.data = data;
      this.maxLength = maxLength;
      this.grammar = grammar;
      this.evaluator = evaluator;
      this.expression = new Expression(grammar, new[] { grammar.Start });
    }

    public State(Data data, int maxLength, Grammar grammar, Expression expression, Evaluator evaluator) {
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

    public MinimizeDouble Bound => new MinimizeDouble(0.0);

    private MinimizeDouble? quality = null; // cache quality to prevent duplicate evaluation (TODO: useful?)
    public MinimizeDouble? Quality {
      get {
        if (quality.HasValue) return quality;

        if (expression.IsSentence) {
          quality = new MinimizeDouble(evaluator.OptimizeAndEvaluate(expression, data));
          return quality;
        }

        return null;
      }
      set {
        // Heuristics are allowed to set the quality of the state (to prevent duplicate evaluation)
        this.quality = value;
      }
    }


    public object Clone() {
      return new State(this);
    }

    public IEnumerable<State> GetBranches() {
      return Grammar.CreateAllDerivations(expression)
        .Where(expr => expr.Length <= maxLength)
        .Select(expr => {
          var newState = new State(data, maxLength, grammar, expr, evaluator);

          return newState;
        });
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
