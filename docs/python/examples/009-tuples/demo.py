"""Runnable demo for recipe 009, tuples.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Point3D, build_point3_d, evaluate, parse

# 1. Build a Point3D. `coord` is a fixed 3-tuple (prefixItems + items:false + minItems:3). build accepts a
#    real tuple; on the wire it becomes a JSON array (JSON has no tuple type).
point: Point3D = {"coord": (1, 2, 3)}
data = build_point3_d(point)
print("1. built:    ", data.decode())

# 2. Validate. evaluate returns a bool (no exceptions) and accepts the JSON bytes directly or a parsed value.
print("2. valid:    ", evaluate(data))  # True

# 3. Parse back to a typed Point3D and destructure the tuple positionally. Each element is typed float.
p = parse(data)
x, y, z = p["coord"]
print("3. x,y,z:    ", x, y, z)

# 4. The length is exact: minItems 3 rejects a short array, items:false rejects a fourth element.
print("4. too few:  ", evaluate({"coord": [1, 2]}))  # False, minItems 3
print("   too many: ", evaluate({"coord": [1, 2, 3, 4]}))  # False, items: false
