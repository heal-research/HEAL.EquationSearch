using HEAL.NativeInterpreter;
using System.Numerics;

namespace HEAL.EquationSearch {
  public class Grammar {
    public Symbol Start => Expr;

    // the order of occurance of symbols in this list is the order how the symbols may apprear in expressions (see Semantics)
    public IEnumerable<Symbol> AllSymbols {
      get {
        return new[] { One, Parameter }
                     .Concat(Variables)
                     .Concat(new[] { Plus, Times, Abs, Log, Exp, Div })
                     .Concat(Nonterminals);
      }
    }

    public IEnumerable<Symbol> Nonterminals {
      get {
        return new[] { Expr, Term, Factor, PolyExpr, PolyTerm, PolyFactor };
      }
    }

    // by convention nonterminal symbols start with an uppercase letter
    public Symbol Expr = new Symbol("Expr");
    public Symbol Term = new Symbol("Term");
    public Symbol Factor = new Symbol("Factor");
    public Symbol PolyExpr = new Symbol("PolyExpr"); // allowed within log() and 1/()
    public Symbol PolyTerm = new Symbol("PolyTerm");
    public Symbol PolyFactor = new Symbol("PolyFactor");

    // terminals
    public Symbol Plus = new Symbol("+", arity: 2);
    public Symbol Times = new Symbol("*", arity: 2);
    public Symbol Div = new Symbol("/", arity: 2);
    public Symbol Exp = new Symbol("exp", arity: 1);
    public Symbol Log = new Symbol("log", arity: 1);
    public Symbol Abs = new Symbol("abs", arity: 1);

    public Symbol One = new ConstantSymbol(1.0);
    public Symbol Parameter = new ParameterSymbol(0.0); // terminal classes have a value

    public VariableSymbol[] Variables;

    public Dictionary<Symbol, List<Symbol[]>> rules = new Dictionary<Symbol, List<Symbol[]>>();

    public Grammar(string[] variableNames) {
      Variables = variableNames.Select(varName => new VariableSymbol(varName)).ToArray();
      UseDefaultRules();
    }

    public void UseDefaultRules() {
      UseFullRules();
    }

    public void UsePolynomialRules() {

      // Expr -> param | param * Term + Expr
      // Term -> Fact | Fact * Term 
      // Fact -> var_1 | ... | var_n

      // evaluator requires a postfix representation 
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
        new Symbol[] { Parameter, Term, Times, Expr, Plus }, // p * Term + Expr
      };

      rules[Term] = new List<Symbol[]>() {
        new Symbol[] { Factor },
        new Symbol[] { Term, Factor, Times },
      };

      // every variable is an alternative
      rules[Factor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();
    }

    public void UseFullRules() {

      // Expr -> param | param * Term + Expr                              // Term in Expr limitiert (Terme lexikographisch sortiert nach Faktoren)
      // Term -> Fact | Fact * Term                                       // Fact in Term limitiert (Faktoren in Term nach Alternative sortiert)
      // Fact -> var_1 | ... | var_n
      //         | 1 / ( PolyExpr )
      //         | log ( abs ( PolyExpr ) )
      //         | exp(param * Term)
      // PolyExpr -> param * PolyTerm + 1 | param * PolyTerm + PolyExpr
      // PolyTerm -> PolyFact | PolyFact * PolyTerm
      // PolyFact -> var_1 | ... | var_n

      // evaluator requires a postfix representation 
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
        new Symbol[] { Parameter, Term, Times, Expr, Plus }, // p * Term + Expr
      };

      rules[Term] = new List<Symbol[]>() {
        new Symbol[] { Factor },
        new Symbol[] { Term, Factor, Times },
      };

      // every variable is an alternative
      rules[Factor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();
      rules[Factor].Add(new[] { PolyExpr, One, Div });
      rules[Factor].Add(new[] { PolyExpr, Abs, Log });
      rules[Factor].Add(new[] { Parameter, Term, Times, Exp });


      rules[PolyExpr] = new List<Symbol[]>() {
        new Symbol[] { Parameter, Term, Times, One, Plus }, // param * Term + 1
        new Symbol[] { Parameter, Term, Times, PolyExpr, Plus }, // param * Term + LogExpr
      };

      rules[PolyTerm] = new List<Symbol[]>() {
        new Symbol[] { PolyFactor },
        new Symbol[] { PolyFactor, PolyTerm, Times}
      };

      // every variable is an alternative
      rules[PolyFactor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();
    }

    internal IEnumerable<Expression> CreateAllDerivations(Expression expression) {

      var idx = expression.FirstIndexOfNT();
      if (idx < 0) yield break;
      foreach (var alternative in rules[expression[idx]]) {
        yield return expression.Replace(idx, alternative);
      }
    }


    // TODO: necessary? Better solution possible?
    // For heuristics we replace all NT-symbols with a T-symbol to allow evaluation.
    // We could replace all NT-symbols with a parameter, but this can lead to over-parameterization
    internal Symbol[] GetDefaultReplacment(Symbol symbol) {
      if (symbol == Expr) return new[] { new ParameterSymbol(0.0) };
      if (symbol == Term) return new[] { One };
      if (symbol == Factor) return new[] { One };
      if (symbol == PolyExpr) return new[] { new ParameterSymbol(0.0) };
      if (symbol == PolyTerm) return new[] { One };
      if (symbol == PolyFactor) return new[] { One };
      return new[] { new ParameterSymbol(0.0) }; // default
    }


    #region symbols
    // TODO Symbol and TerminalSymbol are generic classes.
    // TODO Extract Grammar base class
    // Our grammar is a specific instance of grammar
    public class Symbol {
      public string Name { get; private set; }
      public bool IsNonterminal => char.IsUpper(Name[0]); // by convention nonterminal symbols start with uppercase letter
      public bool IsTerminal => !IsNonterminal;
      public int Arity { get; internal set; }

      public Symbol(string name, int arity = 0) {
        this.Name = name;
        this.Arity = arity;
      }

      public virtual Symbol Clone() {
        return this; // symbols are not cloned;
      }
      public override string ToString() {
        return Name;
      }
    }

    // TODO: this base class is not too useful
    public class TerminalClassSymbol<T> : Symbol where T : notnull {
      protected T value; // e.g. for variables we have multiple instances of the symbol with different values (variable names)

      public TerminalClassSymbol(string symbolName, T value, int arity = 0) : base(symbolName, arity) {
        this.value = value;
      }

      public override string ToString() {
        return value.ToString() ?? string.Empty;
      }
    }

    public class VariableSymbol : TerminalClassSymbol<string> {
      public string VariableName => value;
      public VariableSymbol(string varName) : base("var", varName) { }
    }
    public class ParameterSymbol : TerminalClassSymbol<double> {
      public double Value {
        get { return value; }
        set { this.value = value; }
      }
      public ParameterSymbol(double val) : base("p", val) { }
      public override Symbol Clone() {
        return new ParameterSymbol(value);
      }
      public override string ToString() {
        return string.Format("{0:g4}", value);
      }
    }
    public class ConstantSymbol : TerminalClassSymbol<double> {
      public double Value {
        get { return value; }
      }
      public ConstantSymbol(double val) : base("p", val) { }
      public override Symbol Clone() {
        return new ConstantSymbol(value);
      }
      public override string ToString() {
        return string.Format("{0:g4}", value);
      }
    }
    #endregion
  }
}