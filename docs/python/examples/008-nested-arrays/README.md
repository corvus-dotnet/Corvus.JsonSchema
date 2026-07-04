# Python patterns, arrays of higher rank

This recipe shows nested arrays (an array whose `items` are themselves arrays), and how the
Corvus.Text.Json **Python** generator projects them as a nested `Sequence[...]` matrix type.

## The pattern

`items` can itself be an `array`, recursively. A rank-2 array generates `Sequence[Sequence[float]]` through
a pair of aliases (`Cells = Sequence[Items]`, `Items = Sequence[float]`); the nesting continues for rank-N
(tensors). Element access is ordinary double-indexing, and every level is typed.

- `evaluate(value)`. An AOT-compiled boolean evaluator for the root `Grid`. It accepts the wire bytes
  directly (it decodes them) or an already-parsed value, and it validates the array at every level.
- `build_grid(props)` / `build_canonical_grid(props)`. Compact / RFC 8785 UTF-8 JSON bytes.
- `parse(data)` / `parse_grid(data)`. Decode `bytes | str` into a typed `Grid`.

## The schema

```json
{ "title": "Grid", "type": "object", "required": ["cells"],
  "properties": { "cells": { "type": "array", "items": { "type": "array", "items": { "type": "number" } } } } }
```

## The generated surface

```python
class Grid(TypedDict, total=False):
    cells: Required[Cells]


type Cells = Sequence[Items]


type Items = Sequence[float]
```

## The demo

See [`demo.py`](./demo.py). It builds a rank-2 `Grid`, validates it, and reads a cell with ordinary
double-indexing.

```
1. valid:    True
2. cell 1,2: 6
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
