from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import re

param = symbols("p")
line_cnt = 1

# we can replace a0, a1, ... a_4 with a generic parameter symbol. Given that we substiute other constants, expressions
# like a0+a1 become p+p and further on 2*p
paramsIndexed = [symbols(f"a{i}") for i in range(5)]
param_substitution = {pi: param for pi in paramsIndexed}

# replace x**p with p subsequent multplications of x for "discretizing" the exponent. This is primarily to match
# the notation of grammar enumeration.
discrete_power_substitution = {
    parse_expr(f"x**{i}", evaluate=False): UnevaluatedExpr(parse_expr(str.join("*", ["x"] * i), evaluate=False)) for i in range(2, 10)
}

# x+x+...+x becomes [1|2|3...]*x. Since we also have p*x in the search space, so can skip such
# discrete values as factors.
discrete_multiplication_substitutions = {
    parse_expr(str(i)): param for i in range(2, 10)
}

regex_remove_plus_one = re.compile(r" \+ 1(?!/)")


def parse_line(line):
    # We also get rid of the Abs function
    line = line.replace("Abs", "")
    line = regex_remove_plus_one.sub("", line)

    f = parse_expr(line)
    f = f.subs(param_substitution, evaluate=False)
    f = f.subs(discrete_power_substitution, evaluate=False)
    f = f.subs(discrete_multiplication_substitutions, evaluate=False)

    f = regex_remove_plus_one.sub("", str(f))

    return f


def normalize_file(maxComplexity):
    with open(f"core_maths/unique_equations_{maxComplexity}_cumulative.txt") as f_src, open(f"core_maths/unique_equations_{maxComplexity}_cum_normalized.txt", "w") as f_out:
        line_cnt = 1

        reductions = {}
        functions = set()
        for line in f_src:
            try:
                line = line.replace('\n', '')
                parsed_line = parse_line(line)

                if "-" not in parsed_line and parsed_line not in functions:
                    f_out.write(parsed_line + "\n")
                    functions.add(parsed_line)

                  # reductions[parsed_line] = line
                else:
                    pass
                    #line = line
                    #print(f"'{line}' is the same as {reductions[parsed_line]}, which already exists. Both became {parsed_line}")

                if line_cnt % 1000 == 0:
                    print(f"   parsed {line_cnt} lines...")
                line_cnt += 1

            except SyntaxError:
                print(f"   Could not parse line: {line}", end="")


for i in range(1, 11):
    print()
    print(f"normalize maxComplexity={i}...")
    normalize_file(i)
