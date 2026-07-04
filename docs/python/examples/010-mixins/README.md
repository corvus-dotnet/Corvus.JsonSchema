# Python patterns, mix-in types

This recipe composes several independent base types into one with a multi-member `allOf`, and shows how the
Corvus.Text.Json **Python** generator merges them into a single `TypedDict`.

## The pattern

`allOf` can reference more than one base. `Widget` mixes in `Named` (a `name`) and `Timestamped` (a
`createdAt`), and adds its own `id`. This is the "combine orthogonal capabilities" pattern.

The generator emits a `TypedDict` for each base (`Named`, `Timestamped`) and one for the root (`Widget`) whose
members are the union of all of them. A value must satisfy every mixed-in base for `evaluate` to accept it. The
`createdAt` member came from `Timestamped`, and because it declares `format: date-time` it is a **branded**
`NewType` with a validating factory.

- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions). It accepts the wire bytes directly (it
  decodes them) or an already-parsed value, and requires each mixed-in base to hold.
- `parse(data)`. Decode `bytes | str` into a typed `Widget`.
- `build_widget(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `build_canonical_widget(props)`. The same, with RFC 8785 sorted keys.
- `from_created_at(value)`. A validating factory for the `date-time`-formatted branded type.
- `to_temporal_created_at(value)`. Read the brand as a `whenever.Instant`.

## The schema

```json
{ "title": "Widget", "type": "object",
  "$defs": {
    "named": { "title": "Named", "type": "object", "required": ["name"], "properties": { "name": { "type": "string" } } },
    "timestamped": { "title": "Timestamped", "type": "object", "properties": { "createdAt": { "type": "string", "format": "date-time" } } } },
  "allOf": [ { "$ref": "#/$defs/named" }, { "$ref": "#/$defs/timestamped" } ],
  "properties": { "id": { "type": "string" } } }
```

## The generated surface

```python
class Widget(TypedDict, total=False):
    createdAt: CreatedAt
    id: str
    name: Required[str]


CreatedAt = NewType("CreatedAt", str)
```

## The demo

See [`demo.py`](./demo.py). It builds a `Widget` from all three merged sources, validates it, parses the bytes
back to a typed value, reads the merged members, and reads the `date-time` brand contributed by `Timestamped`
as its temporal value.

```
1. built:   {"name":"gauge","createdAt":"2026-06-26T10:00:00Z","id":"w-1"}
2. valid:   True
3. name:    gauge | id: w-1
4. created: 2026-06-26T10:00:00Z
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
