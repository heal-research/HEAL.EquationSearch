from sympy import *
from sympy.parsing.sympy_parser import parse_expr

import re


param = symbols("p")
zero = symbols("0")
paramsIndexed = [symbols(f"a{i}") for i in range(5)]
line_cnt = 1

discrete_power_substitution = {
    parse_expr(f"x**{i}", evaluate=False): parse_expr(str.join("*", ["x"] * i), evaluate=False) for i in range(2, 10)
}

discrete_multiplication_substitutions = {
    parse_expr(str(i), evaluate=False): param for i in range(2, 10)
}

all_subsitutions = {
    **{pi: param for pi in paramsIndexed},
    **discrete_power_substitution,
    ** discrete_multiplication_substitutions,
}

regex_remove_plus_one = re.compile(r" \+ 1(?!/)")


def parse_line(line):
    line = line.replace("Abs", "").replace("-", "+")
    line = regex_remove_plus_one.sub("", line)

    f = parse_expr(line)
    f = f.subs(all_subsitutions)
    f = f.subs({param: zero}, evaluate=False)

    return f


def normalize_file(maxComplexity):
    with open(f"core_maths/unique_equations_{maxComplexity}.txt") as f_src, open(f"core_maths/unique_equations_{maxComplexity}_normalized.txt", "w") as f_out:
        line_cnt = 1
        for line in f_src:
            try:
                parsed_line = parse_line(line)
                f_out.write(str(parsed_line) + "\n")

                if line_cnt % 1000 == 0:
                    print(f"   parsed {line_cnt} lines...")
                line_cnt += 1

            except SyntaxError:
                print(f"   Could not parse line: {line}", end="")


for i in range(1, 6):
    print(f"normalize maxComplexity={i}...")
    normalize_file(i)
