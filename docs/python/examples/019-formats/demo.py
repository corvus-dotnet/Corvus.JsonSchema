"""Runnable demo for recipe 019, format validation (branded types).

Run it from ``docs/python/examples`` (see ../README.md). The regenerate.ps1 gate type-checks this with
mypy --strict and pyright, then executes it.
"""

from corvus_json_runtime import FormatError

from generated import (
    Account,
    build_account,
    evaluate,
    from_created,
    from_id,
    from_website,
    parse,
    to_temporal_created,
)

# 1. Each `format` is a branded NewType with a validating factory. A raw str is not assignable; you go
#    through the factory, so a value's format is guaranteed by its type.
account: Account = {
    "id": from_id("6f9619ff-8b86-d011-b42d-00cf4fc964ff"),
    "website": from_website("https://example.com"),
    "created": from_created("2026-06-26T10:00:00Z"),
}
data = build_account(account)
print("1. built:        ", data.decode())
print("2. valid:        ", evaluate(data))  # True

# 3. The factory checks the format and raises FormatError on failure.
try:
    from_id("nope")
except FormatError as e:
    print("3. from_id threw:", e)  # value does not match format 'uuid'

# 4. to_temporal reads the date-time brand back out as a strong whenever.Instant (a pure parse helper,
#    no re-validation, the brand already proves the format).
parsed = parse(data)
created = parsed.get("created")
if created is not None:
    print("4. created:      ", to_temporal_created(created))
