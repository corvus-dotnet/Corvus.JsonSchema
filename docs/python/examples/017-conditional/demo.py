"""Runnable demo for recipe 017, conditional schemas (if / then / else).

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Payment, build_payment, evaluate, parse

# 1. Build a cash payment. `cardNumber` is only required when `method` is "card", so this is valid.
cash: Payment = {"method": "cash"}
data = build_payment(cash)
print("1. built:        ", data.decode())

# 2. The rule lives in the evaluator, not the type. A card payment that carries its number is valid.
print("2. cash valid:   ", evaluate(data))  # True
print("3. card + number:", evaluate({"method": "card", "cardNumber": "4111 1111 1111 1111"}))  # True

# 4. A card payment with no number fails: the then-clause makes `cardNumber` required when method is "card".
print("4. card, no num: ", evaluate({"method": "card"}))  # False

# 5. Parse bytes back to a typed Payment. `method` is a Literal["card", "cash"].
parsed = parse(data)
print("5. method:       ", parsed["method"])
