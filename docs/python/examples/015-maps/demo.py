"""Runnable demo for recipe 015, open maps.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Scores, build_scores, evaluate, parse

# 1. An open map (additionalProperties) is a plain Mapping[str, float] whose keys are not known ahead of
#    time. Build it directly from a dict.
table: Scores = {"ada": 9.5, "alan": 8}
data = build_scores(table)
print("1. built:    ", data.decode())

# 2. Validate. evaluate checks every value against the value schema (number, minimum 0).
print("2. valid:    ", evaluate(data))  # True
print("   negative: ", evaluate({"ada": -1}))  # False, minimum 0 on the values

# 3. Parse bytes back to a typed map. Read it by key and iterate with .items().
scores = parse(data)
print("3. ada:      ", scores["ada"])
for name, score in scores.items():
    print(f"   {name} = {score}")
