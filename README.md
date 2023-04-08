# HEAL.EquationSearch
Equation learning with tree search using an expression grammar.

Search is implemented via https://github.com/heal-research/TreesearchLib.

We use separable nonlinear least squares (variable projection) for parameter optimization. The linear parameters are solved via ordinary least squares (via QR decomposition) and the nonlinear parameters are optimized via an iterative algorithm (see https://iopscience.iop.org/article/10.1088/0266-5611/19/2/201). 

The algorithm can be used via breath-first search to generate all expressions up to a given length from the grammar.

## Grammars
Several grammars for expressions can be used. 
The grammars are restricted to limit the number of possible expressions visited by the algorithm.

Polynomial grammar:
```
Expr -> param | param * Term + Expr
Term -> Fact | Fact * Term 
Fact -> var_1 | ... | var_n
```

Default (full) grammar:
```
Expr -> param | param * Term + Expr    
Term -> Fact | Fact * Term             
Fact -> var_1 | ... | var_n
        | 1 / ( PolyExpr )
        | log ( abs ( PolyExpr ) )
        | exp(param * Term)
PolyExpr -> param * PolyTerm + 1 | param * PolyTerm + PolyExpr
PolyTerm -> PolyFact | PolyFact * PolyTerm
PolyFact -> var_1 | ... | var_n
```

## Building
```sh
dotnet build -c Release HEAL.EquationSearch
```

Run the unit tests with
```sh
dotnet test
```

## Native interpreter
The repository includes a binary of a native library (for Windows and Ubuntu 20.04) that is used for automatic differentiation and parameter optimization for efficiency. The C++ code for the native interpreter is not published yet.

## Citation

This algorithm is based on [Kammerer et al, 2023](https://link.springer.com/chapter/10.1007/978-3-030-39958-0_5) ([preprint](https://arxiv.org/abs/2109.13895)) with several improvements developed after the publication.

Full citation:
```
Kammerer, L., Kronberger, G., Burlacu, B., Winkler, S.M., Kommenda, M., Affenzeller, M. (2020).
Symbolic Regression by Exhaustive Search: Reducing the Search Space Using Syntactical Constraints 
and Efficient Semantic Structure Deduplication. In: Banzhaf, W., Goodman, E., Sheneman, L., 
Trujillo, L., Worzel, B. (eds) Genetic Programming Theory and Practice XVII. Genetic and 
Evolutionary Computation. Springer, Cham. https://doi.org/10.1007/978-3-030-39958-0_5
```

## License
MIT License


