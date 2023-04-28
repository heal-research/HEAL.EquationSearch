from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import sys

param = symbols("p")
zero = symbols("0")
line_cnt = 1

discrete_power_substitution = {
    parse_expr(f"x**{i}", evaluate=False): parse_expr(str.join("*", ["x"] * i), evaluate=False) for i in range(2, 10)
}

discrete_multiplication_substitutions = {
    parse_expr(str(i), evaluate=False): param for i in range(2, 10)
}

all_subsitutions = {
    **discrete_power_substitution,
    ** discrete_multiplication_substitutions,
}

def parse_line(line):
    f = parse_expr(line)
    f = f.subs(all_subsitutions)
    f = f.subs({param: zero}, evaluate=False)

    return f


def normalize_file(src_filename, target_filename):
    with open(src_filename) as f_src, open(target_filename, "w") as f_out:
        line_cnt = 1
        
        functions = set()
        for line in f_src:
            try:
                parsed_line = parse_line(line)
                parsed_line = str(parsed_line)

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
