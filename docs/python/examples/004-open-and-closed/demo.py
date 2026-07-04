"""Runnable demo for recipe 004, open and closed objects.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import StrictPoint, build_strict_point, evaluate

# 1. Build a StrictPoint from plain values.
point: StrictPoint = {"x": 1, "y": 2}
data = build_strict_point(point)
print("1. built:      ", data.decode())

# 2. evaluate accepts the declared shape.
print("2. valid:      ", evaluate(data))  # True

# 3. unevaluatedProperties:false makes this a CLOSED type, so an unknown property is rejected.
print("3. extra prop: ", evaluate({"x": 1, "y": 2, "z": 3}))  # False, z is unevaluated

# 4. A required member is still required.
print("4. missing y:  ", evaluate({"x": 1}))  # False, y is required
