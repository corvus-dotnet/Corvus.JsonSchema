# Python patterns, open and closed objects

This recipe shows the difference between an **open** object (unknown properties allowed) and a **closed** one
(`unevaluatedProperties: false`), and how each is generated.

## The pattern

By default a JSON Schema object is **open**. Properties beyond those declared are allowed and ignored. Adding
`unevaluatedProperties: false` makes it **closed**, so any property not accounted for by the schema is rejected.
The generated `TypedDict` carries the declared properties either way. The open/closed distinction is enforced by
`evaluate`, because a "no unknown keys" rule cannot be expressed in a `TypedDict` type.

- `build_strict_point(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `evaluate(value)`. An AOT-compiled boolean evaluator. It accepts the wire bytes directly (it decodes them) or
  an already-parsed value. For this closed type it returns `False` for any property beyond `x` and `y`.

`additionalProperties` constrains keys not named in *this* schema's `properties`. `unevaluatedProperties`
constrains keys not accounted for by *any* applicable subschema, including those pulled in by `allOf`/`if`/`$ref`,
so it is the right tool for "closed, even across composition".

## The schema

```json
{ "title": "StrictPoint", "type": "object", "required": ["x", "y"],
  "properties": { "x": { "type": "number" }, "y": { "type": "number" } },
  "unevaluatedProperties": false }
```

## The generated surface

```python
class StrictPoint(TypedDict, total=False):
    x: Required[float]
    y: Required[float]
```

## The demo

See [`demo.py`](./demo.py). It builds a `StrictPoint`, evaluates the declared shape, then shows the closed type
rejecting an unknown property and a missing required member.

```
1. built:       {"x":1,"y":2}
2. valid:       True
3. extra prop:  False
4. missing y:   False
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
