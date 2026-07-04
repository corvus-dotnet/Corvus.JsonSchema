"""Runnable demo for recipe 014, numeric enumerations.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Response, build_response, evaluate, parse

# 1. Build a Response. status is the numeric-literal union 200 | 404 | 500, so only those values type-check.
resp: Response = {"status": 404}
data = build_response(resp)
print("1. built:     ", data.decode())

# 2. Validate. evaluate compares membership by mathematical value and rejects any other number.
print("2. valid:     ", evaluate(data))  # True
print("   bad code:  ", evaluate({"status": 418}))  # False, not 200 | 404 | 500

# 3. Parse bytes back to a typed Response. status is narrowed to the literal union.
parsed = parse(data)
print("3. status:    ", parsed["status"])
print("   ok?:       ", parsed["status"] == 200)  # False
