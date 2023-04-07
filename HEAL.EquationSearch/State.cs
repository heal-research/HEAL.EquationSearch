using TreesearchLib;

namespace HEAL.EquationSearch {

  /// <summary>
  /// Most important class for Treesearchlib. This class defines the search tree (via the grammar)
  /// </summary>
  internal class State : IState<State, MinimizeDouble> {
    private readonly Data data;
    private readonly int maxLength;
    private readonly Grammar grammar;
    private readonly Expression expression;

    public State(Data data, int maxLength, Grammar grammar) {
      this.data = data;
      this.maxLength = maxLength;
      this.grammar = grammar;
      this.expression = new Expression(grammar, new[] { grammar.Start });
    }

    public State(Data data, int maxLength, Grammar grammar, Expression expression) {
      this.data = data;
      this.maxLength = maxLength;
      this.grammar = grammar;
      this.expression = expression;
    }

    public State(State original) {
      this.data = original.data;
      this.quality = original.quality;
      this.maxLength = original.maxLength;
      this.grammar = original.grammar;
      this.expression = original.expression;
    }

    public bool IsTerminal => expression.Length >= maxLength || expression.IsSentence;

    public MinimizeDouble Bound => new MinimizeDouble(0.0);

    private MinimizeDouble? quality = null; // cache quality to prevent duplicate evaluation (TODO: useful?)
    public MinimizeDouble? Quality {
      get {
        if (expression.IsSentence) {
           if (!quality.HasValue) {
            if (expression.Length == 1) {
              // the expression is a constant. TODO: remove special case and handle in Evaluator
              quality = new MinimizeDouble(Evaluator.Variance(data.Target));
            } else {
              quality = new MinimizeDouble(Evaluator.OptimizeAndEvaluate(expression, data));
            }
          }
          return quality;
        } else {
          return null;
        }
      }
    }

    public object Clone() {
      return new State(this);
    }

    public IEnumerable<State> GetBranches() {
      return grammar.CreateAllDerivations(expression)
        .Where(expr => expr.Length <= maxLength)
        .Where(Semantics.IsCanonicForm) // we only accept expressions in canonic form to prevent visiting duplicate expressions
        .Select(expr => new State(data, maxLength, grammar, expr));
    }

    public override string ToString() {
      if (expression.IsSentence) return expression.ToInfixString();
      else return expression.ToString() ?? "<empty>";
    }
  }
}
