using HEAL.NativeInterpreter;
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
      this.evaluator = evaluator;
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
              quality = new MinimizeDouble(evaluator.Variance(data.Target));
            } else {
              quality = new MinimizeDouble(evaluator.OptimizeAndEvaluate(expression, data));
            }
          }
          return quality;
        } else {
          return null;
        }
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
        .Where(Semantics.IsCanonicForm) // we only accept expressions in canonic form to prevent visiting duplicate expressions
        .Select(expr => {
          var newState = new State(data, maxLength, grammar, expr, evaluator);
          // TODO: this is true only when the quality solely depends on the error (and does not include length of the expression)
          // if the number of parameters in the original expression and the new expression is the same
          // then the quality of the state is the same.
          // We could actually link the quality (use the same object). If one of the states is evaluated, all equivalent states are evaluated.
          // TODO: in general we could have a shared hashtable of qualities that is used for all expressions that are semantically the same (same semantic hash).
          if(this.Quality != null && expression.Count(sy => sy is Grammar.ParameterSymbol) == expr.Count(sy => sy is Grammar.ParameterSymbol)) {
            newState.Quality = this.Quality;
          }
          return newState;
        });
    }

    public override string ToString() {
      if (expression.IsSentence) return expression.ToInfixString();
      else return expression.ToString() ?? "<empty>";
    }
  }
}
