from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import sys

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
    parse_expr(str(i), evaluate=False): param for i in range(2, 10)
}




def parse_line(line):
    f = parse_expr(line)
    f = f.subs(param_substitution, evaluate=False)
    f = f.subs(discrete_power_substitution, evaluate=False)
    f = f.subs(discrete_multiplication_substitutions, evaluate=False)

    return str(f)


def normalize_file(src_filename, target_filename):
    with open(src_filename) as f_src, open(target_filename, "w") as f_out:
        line_cnt = 1
        
        functions = set()
        for line in f_src:
            try:
                parsed_line = parse_line(line)

                if "-" not in parsed_line and parsed_line not in functions:
                  f_out.write(str(parsed_line) + "\n")
                  functions.add(parsed_line)
                  
                if line_cnt % 1000 == 0:
                    print(f"   parsed {line_cnt} lines...")
                line_cnt += 1

            except SyntaxError:
                print(f"   Could not parse line: {line}", end="")


# parse file

print(sys.argv)
source = sys.argv[1]
target = sys.argv[2]

normalize_file(source, target)
