"""Runnable demo for recipe 010, mix-in types.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import (
    Widget,
    build_widget,
    evaluate,
    from_created_at,
    parse,
    to_temporal_created_at,
)

# 1. allOf of MULTIPLE bases: Widget merges Named (name) and Timestamped (createdAt), plus its own id.
#    createdAt is a `date-time` brand reached through its validating factory.
widget: Widget = {
    "name": "gauge",
    "createdAt": from_created_at("2026-06-26T10:00:00Z"),
    "id": "w-1",
}
data = build_widget(widget)
print("1. built:  ", data.decode())

# 2. A value must satisfy every mixed-in base. evaluate accepts the JSON bytes directly or a parsed value.
print("2. valid:  ", evaluate(data))  # True

# 3. Parse back to a typed Widget and read the merged members.
w = parse(data)
print("3. name:   ", w["name"], "| id:", w.get("id"))

# 4. createdAt came from Timestamped; read the date-time brand as its whenever temporal value.
created = w.get("createdAt")
if created is not None:
    print("4. created:", to_temporal_created_at(created))
