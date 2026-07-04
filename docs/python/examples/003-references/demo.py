"""Runnable demo for recipe 003, references ($ref / $defs).

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Address, Order, build_order, evaluate, parse, patch_order

# 1. `shipTo` and `billTo` both `$ref` the same `#/$defs/address`, so both are the ONE shared `Address` type.
#    Define an address once and reuse it.
home: Address = {"line1": "1 Mill Rd", "city": "Cambridge", "postcode": "CB1 2AB"}
data = build_order({"id": "ord-1", "shipTo": home, "billTo": home})
print("1. order:     ", data.decode())

# 2. evaluate the whole document.
print("2. valid:     ", evaluate(data))  # True

# 3. Read through the reference: shipTo IS an Address, so it subscripts like one.
order = parse(data)
print("3. ship city: ", order["shipTo"]["city"])  # Cambridge

# 4. Patch a referenced sub-object. Only shipTo's value span is rewritten; billTo and the rest copy through.
moved = patch_order(data, {"shipTo": {"line1": "5 King's Parade", "city": "Cambridge", "postcode": "CB2 1ST"}})
print("4. moved:     ", moved.decode())
