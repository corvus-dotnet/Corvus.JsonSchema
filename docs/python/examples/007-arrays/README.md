# Python patterns, strongly typed arrays

This recipe shows a homogeneous array of objects: `items` types every element, and the Corvus.Text.Json
**Python** generator reads the array as a `Sequence[T]` where `T` is a generated element type.

## The pattern

An `array` with an `items` schema generates a `Sequence[T]` alias (here `Items = Sequence[LineItem]`, where
`LineItem` is its own generated `TypedDict`). The array is read with ordinary indexing and iteration,
evaluated as a whole (including `minItems`), and appended to at the byte level with `patch`.

- `evaluate(value)`. An AOT-compiled boolean evaluator for the root `Cart`. It accepts the wire bytes
  directly (it decodes them) or an already-parsed value, and it enforces `minItems` across the whole array
  plus every element's own constraints.
- `build_cart(props)` / `build_canonical_cart(props)`. Compact / RFC 8785 UTF-8 JSON bytes.
- `parse(data)` / `parse_cart(data)`. Decode `bytes | str` into a typed `Cart`.
- `patch_cart(source, changes, removals, arrays)`. A byte-native partial update. The `arrays` argument
  applies element edits to a named array member; the ops are a `corvus_json_runtime.RmwArrayOps` with
  `set` / `insert` / `remove_at` / `append`, spliced in place while every other byte is copied verbatim.
- `build_line_item(props)` / `parse_line_item(data)`. The independent surface for the element type.

## The schema

```json
{ "title": "Cart", "type": "object", "required": ["items"],
  "properties": { "items": { "type": "array", "minItems": 1,
      "items": { "title": "LineItem", "type": "object", "required": ["sku", "qty"],
        "properties": { "sku": { "type": "string" }, "qty": { "type": "integer", "minimum": 1 } } } } } }
```

## The generated surface

```python
class Cart(TypedDict, total=False):
    items: Required[Items]


type Items = Sequence[LineItem]


class LineItem(TypedDict, total=False):
    qty: Required[int]
    sku: Required[str]
```

## The demo

See [`demo.py`](./demo.py). It builds a `Cart` of `LineItem` objects, validates it, reads elements with
indexing and iteration, shows `minItems 1` rejecting an empty array, and appends one more element with a
byte-native array patch.

```
1. valid:     True
2. first sku: A1
   total qty: 3
3. empty:     False
4. appended:  {"items":[{"sku":"A1","qty":2},{"sku":"B2","qty":1},{"sku":"C3","qty":5}]}
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
