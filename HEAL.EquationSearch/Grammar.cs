namespace HEAL.EquationSearch {
  public class Grammar {
    public Symbol Start => Expr;

    public IEnumerable<Symbol> AllSymbols {
      get {
        return new[] { One, Parameter }
                     .Concat(Variables)
                     .Concat(new[] { Plus, Times, Div, Abs, Log, Exp, Cos })
                     .Concat(Nonterminals);
      }
    }

    public IEnumerable<Symbol> Nonterminals {
      get {
        return new[] { Expr, Term, Factor, PolyExpr, PolyExprOne, PolyTerm, PolyFactor };
      }
    }

    // by convention nonterminal symbols start with an uppercase letter
    public Symbol Expr = new Symbol("Expr");
    public Symbol Term = new Symbol("Term");
    public Symbol Factor = new Symbol("Factor");
    public Symbol PolyExpr = new Symbol("PolyExpr"); // allowed within cos()
    public Symbol PolyExprOne = new Symbol("PolyExprOne"); // allowed within log() and 1/()
    public Symbol PolyTerm = new Symbol("PolyTerm"); // allowed within exp()
    public Symbol PolyFactor = new Symbol("PolyFactor");

    // terminals
    public Symbol Plus = new Symbol("+", arity: 2);
    public Symbol Times = new Symbol("*", arity: 2);
    public Symbol Div = new Symbol("/", arity: 2);
    public Symbol Exp = new Symbol("exp", arity: 1);
    public Symbol Log = new Symbol("log", arity: 1);
    public Symbol Abs = new Symbol("abs", arity: 1);
    public Symbol Cos = new Symbol("cos", arity: 1);

    public Symbol One = new ConstantSymbol(1.0);
    public Symbol Parameter = new ParameterSymbol(0.0);

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
      // Original grammar definition:
      // Expr -> param | param * Term + Expr
      // Term -> Fact | Fact * Term 
      // Fact -> var_1 | ... | var_n

      // The following expanded version ensures that each expansion always adds at least one variable. This removes intermediate states.
      // TODO: this expansion could potentially be created automatically from the base grammar definition.

      // Expr -> param 
      //         | param * (var_1 | ... | var_n) + Expr
      //         | param * (var_1 | ... | var_n) * Term + Expr
      // Term -> var_1 | ... | var_n
      //         | (var_1 | ... | var_n) * Term 

      // evaluator requires a postfix representation 
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
      };
      rules[Expr].AddRange(Variables.Select(varSy => new Symbol[] { Parameter, varSy, Times, Expr, Plus }));
      rules[Expr].AddRange(Variables.Select(varSy => new Symbol[] { Parameter, varSy, Term, Times, Times, Expr, Plus }));

      rules[Term] = new List<Symbol[]>();
      rules[Term].AddRange(Variables.Select(varSy => new Symbol[] { varSy }));
      rules[Term].AddRange(Variables.Select(varSy => new Symbol[] { varSy, Term, Times }));
    }

    public void UseFullRules() {

      // Expr -> param | param * Term + Expr                              // Term in Expr limitiert (Terme lexikographisch sortiert nach Faktoren)
      // Term -> Fact | Fact * Term                                       // Fact in Term limitiert (Faktoren in Term nach Alternative sortiert)
      // Fact -> var_1 | ... | var_n
      //         | 1 / ( PolyExprOne )
      //         | log ( abs ( PolyExprOne ) )
      //         | exp(param * PolyTerm)
      //         | cos(PolyExpr)
      // PolyExpr    -> param * PolyTerm + param | param * PolyTerm + PolyExpr  // with intercept param
      // PolyExprOne -> param * PolyTerm + 1 | param * PolyTerm + PolyExprOne   // with constant one intercept
      // PolyTerm -> PolyFact | PolyFact * PolyTerm
      // PolyFact -> var_1 | ... | var_n

      // evaluator requires a postfix representation 
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
        new Symbol[] { Parameter, Term, Times, Expr, Plus }, // p * Term + Expr
      };

      rules[Term] = new List<Symbol[]>() {
        new Symbol[] { Factor },
        new Symbol[] { Factor, Term, Times },
      };

      // every variable is an alternative
      rules[Factor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();
      rules[Factor].Add(new[] { PolyExprOne, One, Div });
      rules[Factor].Add(new[] { PolyExprOne, Abs, Log });
      rules[Factor].Add(new[] { Parameter, PolyTerm, Times, Exp });
      rules[Factor].Add(new[] { PolyExpr, Cos });


      // parametric intercept
      rules[PolyExpr] = new List<Symbol[]>() {
        new Symbol[] { Parameter, PolyTerm, Times, Parameter, Plus }, // param * PolyTerm + param
        new Symbol[] { Parameter, PolyTerm, Times, PolyExpr, Plus }, // param * PolyTerm + PolyExpr
      };

      // constant 1 intercept
      rules[PolyExprOne] = new List<Symbol[]>() {
        new Symbol[] { Parameter, PolyTerm, Times, One, Plus }, // param * PolyTerm + 1
        new Symbol[] { Parameter, PolyTerm, Times, PolyExprOne, Plus }, // param * PolyTerm + PolyExprOne
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


    // For heuristics we replace all NT-symbols with a T-symbol to allow evaluation.
    // TODO: necessary? Better solution possible?
    // We could replace all NT-symbols with a parameter, but this can lead to over-parameterization
    private Symbol[] GetDefaultReplacement(Symbol symbol) {
      if (symbol == Expr) return new[] { new ParameterSymbol(0.0) };
      if (symbol == Term) return new[] { One };
      if (symbol == Factor) return new[] { One };
      if (symbol == PolyExpr) return new[] { new ParameterSymbol(0.0) };
      if (symbol == PolyTerm) return new[] { One };
      if (symbol == PolyFactor) return new[] { One };
      return new[] { new ParameterSymbol(0.0) }; // default
    }

    internal Expression MakeSentence(Expression expr) {
      // Takes an unfinished expression and replaces all NTs with their defaults to make a sentence.
      var ntIdx = expr.FirstIndexOfNT();
      while (ntIdx >= 0) {
        expr = expr.Replace(ntIdx, GetDefaultReplacement(expr[ntIdx]));
        ntIdx = expr.FirstIndexOfNT();
      }
      return expr;
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

      public override int GetHashCode() {
        // all parameter objects are equal regardless of their value (this is necessary for semantic hashing)
        return 5308417; // arbitrary prime number
      }

      public override bool Equals(object? other) {
        return other is ParameterSymbol;
      }
    }
    public class ConstantSymbol : TerminalClassSymbol<double> {
      public double Value {
        get { return value; }
      }
      public ConstantSymbol(double val) : base("p", val) { }

      public override string ToString() {
        return string.Format("{0:g4}", value);
      }
    }
    #endregion
  }
}