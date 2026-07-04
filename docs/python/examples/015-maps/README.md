# Python patterns, open maps

This recipe defines an open map in JSON Schema (`additionalProperties` with a value schema) and shows how the
Corvus.Text.Json **Python** generator projects it as a `Mapping[str, V]`.

## The pattern

An object whose keys are not known ahead of time, but whose *values* share a schema, is a map.
`additionalProperties: { "type": "number", "minimum": 0 }` generates `type Scores = Mapping[str, float]`. A map
is a plain dict. Read it by key, iterate with `.items()`, and build it directly; `evaluate` checks every value
against the value schema.

The generator emits, for the root schema, the `Scores` alias plus the module-level API.

- `Scores`. A `Mapping[str, float]` alias. There is nothing to wrap; a parsed value *is* a `Scores`, read with
  ordinary subscripting.
- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions). It accepts the wire bytes directly (it
  decodes them) or an already-parsed value, and fails when any value breaks the value schema (here `minimum: 0`).
- `parse(data)`. Decode `bytes | str` into a typed `Scores`.
- `build_scores(value)`. Construct compact UTF-8 JSON bytes from a plain dict.
- `build_canonical_scores(value)`. The same, with RFC 8785 sorted keys.
- `apply_merge_patch_scores(doc, merge_patch)` / `create_merge_patch_scores(source, target)`. RFC 7396 merge
  patch over the map (an open map has no per-member `patch`, so members are edited by merge patch).

## The schema

```json
{ "title": "Scores", "type": "object", "additionalProperties": { "type": "number", "minimum": 0 } }
```

## The generated surface

```python
type Scores = Mapping[str, float]
type AdditionalProperties = float
```

## The demo

See [`demo.py`](./demo.py). It builds a `Scores` map, validates trusted and untrusted input, parses bytes back
to a typed map, and reads it by key and by iteration.

```
1. built:     {"ada":9.5,"alan":8}
2. valid:     True
   negative:  False
3. ada:       9.5
   ada = 9.5
   alan = 8
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
