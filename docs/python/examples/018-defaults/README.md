# Python patterns, default values

This recipe shows the `default` annotation and how the Corvus.Text.Json **Python** generator surfaces it: a
property with a `default` becomes optional, and a `with_defaults_<t>` function fills every absent default.

## The pattern

`default` documents a fallback for a property and, in practice, makes that property optional. The generated
`TypedDict` is `total=False`, so a value may omit the property and still be valid. The parsed value is never
mutated: an omitted property is simply absent (it reads `None` through `.get`).

- `with_defaults_settings(value)`. Return a shallow copy of `value` with every absent property that declares a
  `default` filled in. Present properties are left as-is, and nested object types recurse through their own
  `with_defaults`. It is emitted only for types that have at least one direct default.
- `evaluate(value)`. The AOT-compiled boolean evaluator. An empty object is valid because both defaulted
  properties are optional.
- `build_settings(props)` / `build_canonical_settings(props)`. Plain values to compact / RFC 8785 bytes.
- `parse(data)`. Decode `bytes | str` into a typed `Settings`.

`with_defaults` returns a copy, so the original value is untouched. Use it to get a defaulted shape in one call
before handing the value to code that expects every documented default present.

## The schema

```json
{ "title": "Settings", "type": "object", "additionalProperties": false,
  "properties": { "theme": { "type": "string", "default": "light" }, "fontSize": { "type": "integer", "default": 14 } } }
```

## The generated surface

```python
class Settings(TypedDict, total=False):
    fontSize: int
    theme: str


def with_defaults_settings(value: Settings) -> Settings: ...
```

## The demo

See [`demo.py`](./demo.py). It builds and validates an empty object, shows that an omitted property stays
absent on the parsed value, and fills the absent defaults with `with_defaults_settings`.

```
1. built:         {}
2. empty valid:   True
3. theme absent:  None
4. with_defaults: {'fontSize': 14, 'theme': 'light'}
5. partial:       {'theme': 'dark', 'fontSize': 14}
```

Line 4 fills both absent defaults. Line 5 keeps the present `theme` (`"dark"`) and adds only the absent
`fontSize`.

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
