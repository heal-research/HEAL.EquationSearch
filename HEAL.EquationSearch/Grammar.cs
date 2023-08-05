using System.Text;

namespace HEAL.EquationSearch {
  // TODO:
  // - ruleset that includes trigonometrics
  // - variants of the ruleset that have no grammar restrictions

  public class Grammar {
    public Symbol Start => Expr;

    public IEnumerable<Symbol> AllSymbols {
      get {
        return new[] { One, Parameter }
                     .Concat(Variables)
                     .Concat(new[] { Plus, Minus, Times, Div, Inv, Neg, Abs, Log, Sqrt, Exp, Cos, Pow, PowAbs })
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
    public Symbol PolyExpr = new Symbol("PolyExpr"); // allowed within cos()
    public Symbol PolyTerm = new Symbol("PolyTerm"); // allowed within exp()
    public Symbol PolyFactor = new Symbol("PolyFactor");

    // terminals
    public Symbol Plus = new Symbol("+", arity: 2);
    public Symbol Minus = new Symbol("-", arity: 2);
    public Symbol Times = new Symbol("*", arity: 2);
    public Symbol Div = new Symbol("/", arity: 2);
    public Symbol Inv = new Symbol("inv", arity: 1); // TODO: power(x, -1) and inv(x) are equivalent. Remove?
    public Symbol Neg = new Symbol("neg", arity: 1);
    public Symbol Exp = new Symbol("exp", arity: 1);
    public Symbol Log = new Symbol("log", arity: 1);
    public Symbol Sqrt = new Symbol("sqrt", arity: 1); // TODO: power(x, 1/2) and sqrt(x) are equivalent. Remove?
    public Symbol Abs = new Symbol("abs", arity: 1);
    public Symbol Cos = new Symbol("cos", arity: 1);
    public Symbol Pow = new Symbol("**", arity: 2);
    public Symbol PowAbs = new Symbol("powabs", arity: 2); // the pow symbol implicitly is |expr|^expr in the ESR paper. This is necessary to reproduce the search space of ESR

    public Symbol One = new ConstantSymbol(1.0);
    public Symbol Parameter = new ParameterSymbol(0.0);

    public VariableSymbol[] Variables;

    private Dictionary<Symbol, List<Symbol[]>> rules = new Dictionary<Symbol, List<Symbol[]>>();
    private readonly int maxLength;
    public int MaxLength => maxLength;

    // maxLen required for limiting expansion of rules (a chain can have a maximum of maxLen symbols)
    public Grammar(string[] variableNames, int maxLen) {
      Variables = variableNames.Select(varName => new VariableSymbol(varName)).ToArray();
      maxLength = maxLen;
      UseDefaultRules();
    }

    public void UseDefaultRules() {
      UseLogExpPowRestrictedRules();
      // UsePolynomialRules();
      // UseUnrestrictedRulesESR();
    }

    public void UsePolynomialRestrictedRules() {
      rules.Clear();

      // Original grammar definition:
      // Expr -> param | param * Term + Expr
      // Term -> Fact | Fact * Term 
      // Fact -> var_1 | ... | var_n

      // VarProEvaluator requires a postfix representation and must end with the offset
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
        new Symbol[] { Parameter, Term, Times, Expr, Plus}, //  p * Term + Expr
      };

      rules[Term] = new List<Symbol[]>() {
        new Symbol[] { Factor },
        new Symbol[] { Factor, Term, Times },
      };

      rules[Factor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();

      ExpandRules();
    }

    public void UseLogExpPowRestrictedRules() {
      rules.Clear();
      // Grammar:
      // Expr -> param | param * Term + Expr
      // Term -> Fact | Fact * Term
      // Fact -> var_1 | ... | var_n
      //         | log '(' abs '(' PolyExpr ')' ')'
      //         | exp '(' param * PolyTerm ')'
      //         | pow '(' abs '(' PolyExpr ')' ',' param ')'
      //         | pow '(' abs '(' PolyExpr ')' ',' PolyExpr ')' // more complex exponents
      //
      //         (1/x = x^(-1),  sqrt(x) = x^(1/2)

      // polynomial where the first term has constant coefficient 1 or -1
      // PolyExpr -> param + PolyTerm
      //           | param - PolyTerm
      //           | PolyExpr + param * PolyTerm
      // PolyTerm -> PolyFact
      //           | PolyFact * PolyTerm
      // PolyFact -> var_1 | ... | var_n

      // VarProEvaluator requires a postfix representation
      rules[Expr] = new List<Symbol[]>() {
        new Symbol[] { Parameter }, // p
        new Symbol[] { Parameter, Term, Times, Expr, Plus }, // p * Term + Expr
      };

      rules[Term] = new List<Symbol[]>() {
        new Symbol[] { Factor },
        new Symbol[] { Factor, Term, Times },
      };

      // NOTE: be careful with functions in multiple variables (the order of arguments is reversed)

      // every variable is an alternative
      rules[Factor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();
      rules[Factor].Add(new[] { PolyExpr, Abs, Log });
      rules[Factor].Add(new[] { Parameter, PolyTerm, Times, Exp });
      rules[Factor].Add(new[] { Parameter, PolyExpr, Abs, Pow }); // parameter must be the first child! (= PolyExpr ^ Parameter)
      rules[Factor].Add(new[] { PolyExpr, PolyExpr, Abs, Pow }); // for more complex exponents e.g. x ** x

      // rules[Factor].Add(new[] { PolyExpr, Inv }); // 1 / abs(PolyExpr) == abs(PolyExpr) ** -1
      // rules[Factor].Add(new[] { PolyExpr, Abs, Sqrt }); // == PolyExpr ** 0.5

      // polynomial with one degree of freedom removed (always wrapped within abs() )
      rules[PolyExpr] = new List<Symbol[]>() {
        new Symbol[] { PolyTerm, Parameter, Plus}, // parameter + PolyTerm 
        new Symbol[] { PolyTerm, Neg, Parameter, Plus}, // for e.g. log(p0 + (-x) + p1 x ...). 
        new Symbol[] { PolyExpr, Parameter, PolyTerm, Times, Plus }, // PolyExpr + param * PolyTerm
      };

      rules[PolyTerm] = new List<Symbol[]>() {
        new Symbol[] { PolyFactor },
        new Symbol[] { PolyFactor, PolyTerm, Times}
      };

      // every variable is an alternative
      rules[PolyFactor] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();

      ExpandRules();
    }


    public void UseEsrCoreMaths() {
      // primarily to compare to reported results in ESR paper https://arxiv.org/pdf/2211.11461.pdf
      // ESR operators: x, a, inv, +, −, ×, ÷, pow
      // Cannot be evaluated with VarPro evaluator (which requires a constant offset at the end of each expression) (TODO)

      rules.Clear();

      // this is an inverse regular grammar.
      // each rule introduces at least one terminal symbol
      rules[Expr] = Variables.Select(varSy => new Symbol[] { varSy }).ToList();      // every variable is an alternative
      rules[Expr].AddRange(new List<Symbol[]>() {
        new [] { Parameter },
        new [] { Expr, Inv }, // 1/x
        new [] { Expr, Expr, Plus},
        new [] { Expr, Expr, Minus},
        new [] { Expr, Expr, Times},
        new [] { Expr, Expr, Div },
        new [] { Expr, Expr, PowAbs},
      });
    }


    // TODO:
    //  - use set of rules instead of list of rules
    //  - remove semantic duplicates
    //  - support recursive grammars
    //  - remove maxlen restriction
    private void ExpandRules() {
#if DEBUG
      Console.WriteLine($"Grammar rules before expansion: {this}");
#endif

      // We want to ensure that each derivation introduces a new variables reference (to save intermediate states)
      // For this we expand the rules until each rule contains a variable reference.
      // This also ensures that our rules do not allow endless expansions.
      var changed = true;
      while (changed) {
        changed = false;
        // we iterate the NTs in reverse order (bottom up) for efficiency but the algorithm should work for any order
        foreach (var ntSy in Nonterminals.Reverse()) {
          if (!rules.ContainsKey(ntSy)) continue;
          var alternatives = rules[ntSy];

          // clear directly recursive rules: N -> N
          for (int i = alternatives.Count - 1; i >= 0; i--) {
            if (alternatives[i].Length > maxLength) {
              alternatives.RemoveAt(i);
            }
          }

          // go over all alternatives (including the newly introduced ones)
          var altIdx = 0;
          while (altIdx < alternatives.Count) {
            var alt = alternatives[altIdx];

            var ntIdx = Array.FindIndex(alt, sy => sy.IsNonterminal);
            var varIdx = Array.FindIndex(alt, sy => sy is VariableSymbol);

            // expand if a rule contains an NT but no variable reference
            if (ntIdx >= 0 && varIdx < 0) {
              // replace the existing alternative with all derivations
              alternatives.RemoveAt(altIdx);
              var newAlternatives = CreateAllDerivations(alt, maxLength).ToArray();
              // cloned parameters are initialized with a random value. Reset the value to zero here for clean output
              foreach (var newAlt in newAlternatives) {
                foreach (var sy in newAlt) {
                  if (sy is ParameterSymbol paramSy) paramSy.Value = 0.0;
                }
              }
              alternatives.InsertRange(altIdx, newAlternatives);
              changed |= true;
            } else {
              altIdx++;
            }
          }
        }
      }

#if DEBUG
      Console.WriteLine($"After expansion: {this}");
#endif
    }
    internal int FirstIndexOfNT(Symbol[] syString) => Array.FindIndex(syString, sy => sy.IsNonterminal);
    internal IEnumerable<Symbol[]> CreateAllDerivations(Symbol[] syString, int maxLen) {

      var idx = FirstIndexOfNT(syString);
      if (idx < 0) yield break;

      foreach (var alternative in rules[syString[idx]]) {
        if (alternative.Length + syString.Length - 1 <= maxLen)
          yield return Replace(syString, idx, alternative);
      }
    }
    internal IEnumerable<Expression> CreateAllDerivations(Expression expression, int maxLen) =>
      CreateAllDerivations(expression.SymbolString, maxLen)
      .Select(newStr => new Expression(this, newStr));

    // returns a new string with the symbol at pos replaced with replSyString
    public Symbol[] Replace(Symbol[] syString, int pos, Symbol[] replSyString) {

      var newSyString = new Symbol[syString.Length - 1 + replSyString.Length];

      // terminalClassSymbols must be cloned
      int j = 0;
      for (int i = 0; i < pos; i++) { newSyString[j++] = syString[i].Clone(); }

      // copy replacement
      for (int i = 0; i < replSyString.Length; i++) {
        newSyString[j] = replSyString[i].Clone();

        // initialized parameters in the replacement randomly
        if (newSyString[j] is ParameterSymbol paramSy) {
          paramSy.Value = SharedRandom.NextDouble() * 2 - 1; // uniform(-1,1)
        }
        j++;
      }

      for (int i = pos + 1; i < syString.Length; i++) { newSyString[j++] = syString[i].Clone(); }

      return newSyString;
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
      throw new InvalidProgramException(); // assert that we have handled all NTs
    }

    internal Expression MakeSentence(Expression expr) {
      // Takes an unfinished expression and replaces all NTs with their defaults to make a sentence.
      var syString = expr.SymbolString;
      var ntIdx = FirstIndexOfNT(syString);
      while (ntIdx >= 0) {
        syString = Replace(syString, ntIdx, GetDefaultReplacement(syString[ntIdx]));
        ntIdx = FirstIndexOfNT(syString);
      }
      return new Expression(this, syString);
    }

    // for debugging
    public override string ToString() {
      var sb = new StringBuilder();
      sb.AppendLine("G:");
      foreach (var ntSy in Nonterminals) {
        if (!rules.ContainsKey(ntSy)) continue;
        foreach (var alternative in rules[ntSy]) {
          sb.AppendLine($"{ntSy,-20} -> {string.Join(" ", alternative.Select(sy => sy.ToString()))}");
        }
      }
      return sb.ToString();
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