# Python patterns, tuples

This recipe defines a fixed-length positional array (`prefixItems` with `items: false`) and shows how the
Corvus.Text.Json **Python** generator projects it as a precise `tuple[...]` type.

## The pattern

`prefixItems` types each position, `items: false` forbids anything beyond them, and `minItems` requires them
all to be present. Together they generate a tuple. Here `coord` becomes `tuple[float, float, float]`, which
destructures and indexes positionally with full types.

The generator emits, for the root schema, a `TypedDict` (`Point3D`) whose `coord` member is the tuple alias
`Coord = tuple[float, float, float]`. A JSON array is a `list` on the wire and in memory, so `parse` hands the
tuple back as a list typed as the tuple. You read it by positional destructuring.

- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions). It accepts the wire bytes directly (it
  decodes them) or an already-parsed value. It enforces the exact length: `minItems: 3` rejects a short array,
  `items: false` rejects a fourth element.
- `parse(data)`. Decode `bytes | str` into a typed `Point3D`.
- `build_point3_d(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `build_canonical_point3_d(props)`. The same, with RFC 8785 sorted keys.
- `patch_point3_d(source, changes, removals, arrays)`. Change only the named fields, spliced at the byte level.

## The schema

```json
{ "title": "Point3D", "type": "object", "required": ["coord"],
  "properties": { "coord": { "type": "array", "minItems": 3,
      "prefixItems": [ { "type": "number" }, { "type": "number" }, { "type": "number" } ], "items": false } } }
```

## The generated surface

```python
class Point3D(TypedDict, total=False):
    coord: Required[Coord]


type Coord = tuple[float, float, float]
```

## The demo

See [`demo.py`](./demo.py). It builds a `Point3D`, validates it, parses the bytes back to a typed value and
destructures the tuple positionally, then shows the exact-length rule rejecting a short and an over-long array.
Because a JSON array is a `list` at runtime, the plain value is a list cast to the `Coord` tuple type at the
build boundary.

```
1. built:     {"coord":[1,2,3]}
2. valid:     True
3. x,y,z:     1 2 3
4. too few:   False
   too many:  False
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
