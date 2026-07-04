# Python patterns, constraining a base type

This recipe uses `allOf` not to *add* properties but to *tighten* a base type's constraints, and shows how
the Corvus.Text.Json **Python** generator keeps the shape unchanged while the union of every member's
constraints is enforced by `evaluate`.

## The pattern

A member of `allOf` can re-state an existing property with extra keywords, and all members apply together.
Here `Batch` requires `size >= 1`, and `SmallBatch` composes it with `size <= 100`, so the effective
constraint is `1 <= size <= 100`. The generated shape is unchanged (a `TypedDict` with a single `size`
member); the *constraints* are the union of all members and live in `SmallBatch`'s evaluator.

A numeric range cannot be expressed in a Python type, so `size` on `SmallBatch` is typed `Required[Any]`
(the `maximum: 100` member carries no `type`) and the `1..100` bound lives in `evaluate`. This is the same
split between *shape* and *constraint* that plain validation uses. The base `Batch` types `size` as
`Required[int]` on its own.

- `evaluate(value)`. An AOT-compiled boolean evaluator for the root `SmallBatch`. It accepts the wire bytes
  directly (it decodes them) or an already-parsed value, and it enforces both the base minimum and the
  added maximum.
- `build_small_batch(props)` / `build_canonical_small_batch(props)`. Compact / RFC 8785 UTF-8 JSON bytes.
- `parse(data)` / `parse_small_batch(data)`. Decode `bytes | str` into a typed `SmallBatch`.
- `build_batch(props)` / `parse_batch(data)`. The independent surface for the base type.

## The schema

```json
{ "title": "SmallBatch", "type": "object",
  "$defs": { "batch": { "title": "Batch", "type": "object", "required": ["size"],
      "properties": { "size": { "type": "integer", "minimum": 1 } } } },
  "allOf": [ { "$ref": "#/$defs/batch" } ],
  "properties": { "size": { "maximum": 100 } } }
```

## The generated surface

```python
class SmallBatch(TypedDict, total=False):
    size: Required[Any]


class Batch(TypedDict, total=False):
    size: Required[int]
```

## The demo

See [`demo.py`](./demo.py). It builds and validates an in-range value, then shows the two out-of-range cases
rejected by `evaluate`: one past the maximum added by `SmallBatch`, one below the minimum inherited from the
base `Batch`.

```
1. size 50:  True
2. size 200: False
3. size 0:   False
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
