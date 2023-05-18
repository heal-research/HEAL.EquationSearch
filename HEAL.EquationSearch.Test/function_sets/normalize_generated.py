from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import sys
import re

param = symbols("p")
line_cnt = 1


def parse_line(line):
    f = parse_expr(line)

    return str(f)


def normalize_file(src_filename, target_filename):
    regex_discrete_multiplication = re.compile(r"\d+\*(?!\*)")

    with open(src_filename) as f_src, open(target_filename, "w") as f_out:
        line_cnt = 1
        
        functions = set()
        for line in f_src:
            try:
                parsed_line = parse_line(line)
                parsed_line = regex_discrete_multiplication.sub("p*", parsed_line)

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
