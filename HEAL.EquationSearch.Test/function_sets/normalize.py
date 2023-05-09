from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import re

param = symbols("p")
line_cnt = 1

regex_remove_plus_one = re.compile(r" \+ 1(?!/)")
regex_remove_discrete_multiplication = re.compile(r"[2-9](?=\*)")
regex_remove_params = re.compile(r"a[0-5]")

def parse_line(line):
    # We also get rid of the Abs function
    line = line.replace("Abs", "")
    line = regex_remove_plus_one.sub("", line)
    line = regex_remove_params.sub(str(param), line) # replace a0, ... a4 with p
    line = regex_remove_discrete_multiplication.sub(str(param), line) # replace 2*x with p*x
    
    f = parse_expr(line)
    
    f = regex_remove_plus_one.sub("", str(f))  # some plus-ones might remain
    return f


def normalize_file(folder, maxComplexity):
    with open(f"{folder}/unique_equations_{maxComplexity}_cumulative.txt") as f_src, open(f"{folder}/unique_equations_{maxComplexity}_cum_normalized.txt", "w") as f_out:
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


print("format core maths:")
for i in range(1, 11):
    print()
    print(f"normalize maxComplexity={i}...")
    normalize_file("core_maths", i)

print("format ext maths:")
for i in range(4, 5):
    print()
    print(f"normalize maxComplexity={i}...")
    normalize_file("ext_maths", i)
