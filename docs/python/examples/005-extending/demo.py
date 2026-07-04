"""Runnable demo for recipe 005, extending a base type with allOf.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Employee, build_employee, build_person, evaluate, from_email, parse

# 1. allOf merges the base Person (name, email) into Employee, which adds employeeId / department. The
#    result is compact UTF-8 JSON bytes, the wire / persistence shape.
employee: Employee = {
    "name": "Ada",
    "email": from_email("ada@example.com"),  # a validating branded factory for `format: email`
    "employeeId": "E-1",
    "department": "R&D",
}
data = build_employee(employee)
print("1. built:        ", data.decode())

# 2. Validate. evaluate accepts the JSON bytes directly (it decodes them) or an already-parsed value.
print("2. valid:        ", evaluate(data))  # True
print("   missing name: ", evaluate({"employeeId": "E-2"}))  # False, name is required by the base

# 3. Parse and read a base member and an extension member with ordinary subscripting.
e = parse(data)
print("3. name (base):  ", e["name"])
print("   id (own):     ", e["employeeId"])

# 4. The base type is generated on its own too, with its own build_person / parse_person.
person = build_person({"name": "Grace"})
print("4. base person:  ", person.decode())
