"""Runnable demo for recipe 001, simple data objects.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import (
    Person,
    build_person,
    evaluate,
    from_birth_date,
    parse,
    patch_person,
    to_temporal_birth_date,
)

# 1. Build a Person from plain values. The result is compact UTF-8 JSON bytes, the wire / persistence shape.
person: Person = {
    "family_name": "Bronte",
    "given_name": "Anne",
    "birth_date": from_birth_date("1820-01-17"),  # a validating branded factory for `format: date`
    "height": 1.52,
    "tags": ["author"],
}
data = build_person(person)
print("1. built:        ", data.decode())

# 2. Validate untrusted input. evaluate returns a bool (no exceptions) and accepts the JSON bytes directly
#    (it decodes them) or an already-parsed value.
print("2. valid:        ", evaluate(data))  # True
print("   missing reqd: ", evaluate({"given_name": "Anne"}))  # False, family_name is required

# 3. Parse bytes back to a typed Person and read it with ordinary subscripting.
parsed = parse(data)
print("3. given_name:   ", parsed["given_name"])

# 4. Read a formatted brand as its whenever temporal value.
birth_date = parsed.get("birth_date")
if birth_date is not None:
    print("4. birth date:   ", to_temporal_birth_date(birth_date))  # Date("1820-01-17")

# 5. Byte-native partial update: change height, remove tags. Only the changed spans are rewritten; every
#    other byte is copied through verbatim.
patched = patch_person(data, {"height": 1.53}, ["tags"])
print("5. patched:      ", patched.decode())
