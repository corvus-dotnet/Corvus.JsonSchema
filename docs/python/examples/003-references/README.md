# Python patterns, references

This recipe shows how a schema reuses a common type with `$ref` and `$defs`, and how that becomes one shared
`TypedDict` with its own generated API.

## The pattern

A subschema defined under `$defs` and referenced with `$ref` is generated **once**, as a named `TypedDict`, and
every reference to it resolves to that same type. Here `shipTo` and `billTo` both `$ref` `#/$defs/address`, so
both properties are typed `Address`. Define an address value once and use it for either.

A referenced type is first-class. `Address` gets its own `build_address` / `parse_address` / `patch_address` /
`evaluate` alongside `Order`'s, so you can construct and evaluate it independently.

- `build_order(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `evaluate(value)`. An AOT-compiled boolean evaluator over the whole document. It accepts the wire bytes
  directly (it decodes them) or an already-parsed value.
- `parse(data)`. Decode `bytes | str` into a typed `Order`; `order["shipTo"]` reads back as an `Address`.
- `patch_order(source, changes, removals)`. Change only the named members, spliced at the byte level. Patching
  `shipTo` replaces its whole member value while every other byte is copied through verbatim.

The reference graph is resolved during generation. `$ref`, `$dynamicRef`, anchors and remote documents are all
flattened to plain named types before the Python is emitted, so there is no runtime resolution.

## The schema

```json
{
  "title": "Order",
  "type": "object",
  "$defs": {
    "address": {
      "title": "Address",
      "type": "object",
      "required": ["line1", "city", "postcode"],
      "properties": {
        "line1": { "type": "string" },
        "city": { "type": "string" },
        "postcode": { "type": "string" }
      }
    }
  },
  "required": ["id", "shipTo"],
  "properties": {
    "id": { "type": "string" },
    "shipTo": { "$ref": "#/$defs/address" },
    "billTo": { "$ref": "#/$defs/address" }
  }
}
```

## The generated surface

```python
class Order(TypedDict, total=False):
    billTo: Address
    id: Required[str]
    shipTo: Required[Address]


class Address(TypedDict, total=False):
    city: Required[str]
    line1: Required[str]
    postcode: Required[str]
```

## The demo

See [`demo.py`](./demo.py). It defines one `Address` and reuses it for both `shipTo` and `billTo`, evaluates the
order, reads a city through the reference, then patches the referenced `shipTo` sub-object at the byte level.

```
1. order:      {"id":"ord-1","shipTo":{"line1":"1 Mill Rd","city":"Cambridge","postcode":"CB1 2AB"},"billTo":{"line1":"1 Mill Rd","city":"Cambridge","postcode":"CB1 2AB"}}
2. valid:      True
3. ship city:  Cambridge
4. moved:      {"id":"ord-1","shipTo":{"line1":"5 King's Parade","city":"Cambridge","postcode":"CB2 1ST"},"billTo":{"line1":"1 Mill Rd","city":"Cambridge","postcode":"CB1 2AB"}}
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
