"""Runnable demo for recipe 007, strongly typed arrays.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from corvus_json_runtime import RmwArrayOps

from generated import Cart, build_cart, evaluate, parse, patch_cart

# 1. Build a Cart whose items are typed LineItem objects. minItems 1 is a whole-array constraint.
cart_in: Cart = {"items": [{"sku": "A1", "qty": 2}, {"sku": "B2", "qty": 1}]}
data = build_cart(cart_in)
print("1. valid:    ", evaluate(data))  # True

# 2. Parse and read elements with ordinary indexing and iteration; each element is a LineItem.
cart = parse(data)
print("2. first sku:", cart["items"][0]["sku"])  # A1
print("   total qty:", sum(item["qty"] for item in cart["items"]))  # 3

# 3. minItems 1 is enforced as a whole-array constraint by evaluate.
print("3. empty:    ", evaluate({"items": []}))  # False, minItems 1

# 4. Byte-native append: splice one more LineItem into the items array in place, copying the rest through.
ops: RmwArrayOps = {"append": [{"sku": "C3", "qty": 5}]}
more = patch_cart(data, {}, None, {"items": ops})
print("4. appended: ", more.decode())
