from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import itertools
import sys


param = symbols("p")
line_cnt = 1


def parse_line(line):
    f = parse_expr(line)

    for func in itertools.chain(f.atoms(Add), f.atoms(Mul), f.atoms(Pow)):
        is_sqrt = func.is_Pow and func.exp == S.Half
        is_discrete_exp = func.is_Pow and func.exp.is_Integer

        if not is_sqrt and not is_discrete_exp:
            for arg in func.args:
                if arg.is_Integer or arg.is_Rational:
                    f = f.subs(arg, param)


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
                  
                if line_cnt % 100_000 == 0:
                    print(f"   parsed {line_cnt} lines...")
                line_cnt += 1

            except SyntaxError:
                print(f"   Could not parse line: {line}", end="")


# parse file

print(sys.argv)
source = sys.argv[1]
target = sys.argv[2]

normalize_file(source, target)
