"""Runnable demo for recipe 018, default values.

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from generated import Settings, build_settings, evaluate, parse, with_defaults_settings

# 1. `default` makes a property optional. An empty object omits both defaulted properties and is still valid.
empty: Settings = {}
data = build_settings(empty)
print("1. built:        ", data.decode())  # {}
print("2. empty valid:  ", evaluate(data))  # True

# 3. The parsed value is never mutated. An omitted property is simply absent (reads None via .get).
s = parse(data)
print("3. theme absent: ", s.get("theme"))  # None

# 4. with_defaults returns a copy with every absent default filled (theme -> "light", fontSize -> 14).
filled = with_defaults_settings(s)
print("4. with_defaults:", filled)

# 5. Present properties are left as-is; only the absent defaults are added.
partial: Settings = {"theme": "dark"}
print("5. partial:      ", with_defaults_settings(partial))
