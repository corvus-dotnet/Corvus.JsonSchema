# Python patterns, format validation

This recipe shows `format` keywords generating **branded** `NewType` types with validating factories, and the
accessor that converts a branded value into a strong runtime type.

## The pattern

A `format` (e.g. `uuid`, `uri`, `date-time`) generates a branded type (`Id = NewType("Id", str)`) and a factory
`from_id(value) -> Id` that checks the format and raises `FormatError` on failure. The brand means a raw `str`
is *not* assignable where an `Id` is expected. You go through the factory, so a value's format is guaranteed by
its type. `evaluate` also checks formats as part of whole-document evaluation.

A branded value is still a `str` (or number) at runtime, so it serializes and prints as-is. When you need a
*strong* runtime value, a calendar or instant type for a date, the companion carries a conversion accessor.

- `from_id(value)` / `from_website(value)` / `from_created(value)`. Validating factories, one per formatted
  field. Each checks the format and raises `corvus_json_runtime.FormatError` on failure.
- `to_temporal_created(value)`. Read the `date-time` brand back out as a strong `whenever.Instant`. This is a
  pure parse helper. It never re-validates (the brand already proves the format) and never mutates the value.
- `evaluate(value)`. The AOT-compiled boolean evaluator, which checks every format as part of the whole
  document.
- `build_account(props)`. Construct compact UTF-8 JSON bytes from plain (branded) values.
- `parse(data)`. Decode `bytes | str` into a typed `Account`.

The four temporal formats (`date`, `date-time`, `time`, `duration`) get a `to_temporal_<t>` accessor returning
the matching `whenever` type. Arbitrary-precision numeric formats (`decimal`) get `to_exact_<t>` returning the
exact decimal digits. This recipe's `created` (`date-time`) maps to `to_temporal_created(value) -> Instant`.

## The schema

```json
{ "title": "Account", "type": "object", "required": ["id"],
  "properties": { "id": { "type": "string", "format": "uuid" }, "website": { "type": "string", "format": "uri" }, "created": { "type": "string", "format": "date-time" } } }
```

## The generated surface

```python
class Account(TypedDict, total=False):
    created: Created
    id: Required[Id]
    website: Website


Id = NewType("Id", str)
Website = NewType("Website", str)
Created = NewType("Created", str)


def from_id(value: str) -> Id: ...
def to_temporal_created(value: Created) -> Instant: ...
```

## The demo

See [`demo.py`](./demo.py). It builds an `Account` through the format factories, validates it, shows the factory
rejecting a bad `uuid`, and reads the `date-time` brand back out as a `whenever.Instant`.

```
1. built:         {"id":"6f9619ff-8b86-d011-b42d-00cf4fc964ff","website":"https://example.com","created":"2026-06-26T10:00:00Z"}
2. valid:         True
3. from_id threw: value does not match format 'uuid'
4. created:       2026-06-26T10:00:00Z
```

Line 3 is the factory refusing to brand an invalid value. Line 4 is the branded `date-time` read out as a
strong temporal value, not the raw string.

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
