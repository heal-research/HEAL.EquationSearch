from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import re


param = symbols("p")
zero = symbols("0")
line_cnt = 1

# we can replace a0, a1, ... a_4 with a generic parameter symbol. Given that we substiute other constants, expressions
# like a0+a1 become p+p and further on 2*p 
paramsIndexed = [symbols(f"a{i}") for i in range(5)]
param_substitution = {pi: param for pi in paramsIndexed}

# replace x**p with p subsequent multplications of x for "discretizing" the exponent. This is primarily to match
# the notation of grammar enumeration.
#discrete_power_substitution = {
#    parse_expr(f"x**{i}", evaluate=False): parse_expr(str.join("*", ["x"] * i), evaluate=False) for i in range(2, 10)
#}

# x+x+...+x becomes [1|2|3...]*x. Since we also have p*x in the search space, so can skip such 
# discrete values as factors.
discrete_multiplication_substitutions = {
    parse_expr(str(i), evaluate=False): param for i in range(2, 10)
}

all_subsitutions = {
    **param_substitution,
    #**discrete_power_substitution,
    ** discrete_multiplication_substitutions,
}

regex_remove_plus_one = re.compile(r" \+ 1(?!/)")


def parse_line(line):
    # We also get rid of the Abs function and turn - into + (so -x becomes +)
    line = line.replace("Abs", "").replace("-", "+")
    line = regex_remove_plus_one.sub("", line)

    f = parse_expr(line)
    f = f.subs(all_subsitutions)
    f = f.subs({param: zero}, evaluate=False)

    return f


def normalize_file(maxComplexity):
    with open(f"core_maths/unique_equations_{maxComplexity}.txt") as f_src, open(f"core_maths/unique_equations_{maxComplexity}_normalized.txt", "w") as f_out:
        line_cnt = 1
        
        reductions = {}
        functions = set()
        for line in f_src:
            try:
                line = line.replace('\n', '')
                parsed_line = parse_line(line)
                parsed_line = str(parsed_line)
                
                if parsed_line not in functions:
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


for i in range(10, 11):
    print(f"normalize maxComplexity={i}...")
    normalize_file(i)
