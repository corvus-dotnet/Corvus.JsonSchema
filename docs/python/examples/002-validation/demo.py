"""Runnable demo for recipe 002, validation.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from corvus_json_runtime import FormatError

from generated import Registration, build_registration, evaluate, from_email

# 1. Build a valid Registration. Constraints (minLength / pattern / minimum / multipleOf / format) are enforced
#    by evaluate, not by the type: the TypedDict carries the SHAPE, evaluate carries the CONSTRAINT.
reg: Registration = {
    "username": "ada_lovelace",
    "age": 36,
    "email": from_email("ada@example.com"),  # a validating branded factory for `format: email`
    "score": 4.5,
}
data = build_registration(reg)
print("1. built:         ", data.decode())

# 2. evaluate returns a bool (no exceptions, no error graph). It accepts the JSON bytes or a parsed value.
print("2. valid:         ", evaluate(data))  # True

# 3. Each constraint rejection is just False.
print("3. short username:", evaluate({"username": "ab", "age": 36, "email": "a@b.com"}))  # minLength 3
print("   bad pattern:   ", evaluate({"username": "Ada", "age": 36, "email": "a@b.com"}))  # ^[a-z][a-z0-9_]*$
print("   under age:     ", evaluate({"username": "ada", "age": 17, "email": "a@b.com"}))  # minimum 18
print("   score step:    ", evaluate({"username": "ada", "age": 36, "email": "a@b.com", "score": 0.3}))  # multipleOf 0.5

# 4. A format brand validates eagerly at construction (it raises), distinct from whole-document evaluation.
try:
    from_email("not-an-email")
except FormatError as e:
    print("4. from_email raised:", e)
