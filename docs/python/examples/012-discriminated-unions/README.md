# Python patterns, discriminated unions

This recipe shows a tagged union (a `oneOf` whose branches share a discriminant property) and the exhaustive
dispatch the Corvus.Text.Json **Python** generator produces for it.

## The pattern

When every branch of a `oneOf` carries a `const` discriminant (here `type` is `"click"`, `"keypress"` or
`"scroll"`), the generator projects the union and a matcher that keys off it.

- `type Event = Click | KeyPress | Scroll`. A PEP 695 type alias of the branch `TypedDict`s.
- `is_click(value)`, `is_key_press(value)`, `is_scroll(value)`. One `typing.TypeGuard` per branch, backed by
  that branch's validator. Each narrows a value on its own.
- `match_event[R](value, *, click, key_press, scroll) -> R`. A generic dispatcher that tries each branch guard
  in order and calls the matching keyword-only handler, raising if nothing matched. It is exhaustive: every
  branch needs a handler, so adding a branch to the schema turns a missing case into a type error. Because it is
  generic in the return type, every branch produces the same type.

The discriminant is a convenience for *your* dispatch. `evaluate` is correct with or without one, distinguishing
branches structurally, but a shared `type` const lets it short-circuit to the one branch that can match.

## The schema

```json
{ "title": "Event",
  "oneOf": [
    { "title": "Click",    "type": "object", "required": ["type", "x", "y"],   "properties": { "type": { "const": "click" },    "x": { "type": "number" }, "y": { "type": "number" } } },
    { "title": "KeyPress", "type": "object", "required": ["type", "key"],       "properties": { "type": { "const": "keypress" }, "key": { "type": "string" } } },
    { "title": "Scroll",   "type": "object", "required": ["type", "delta"],     "properties": { "type": { "const": "scroll" },   "delta": { "type": "number" } } } ] }
```

## The generated surface

```python
type Event = Click | KeyPress | Scroll


def is_click(value: object) -> TypeGuard[Click]: ...


def match_event[R](value: Event, *, click: Callable[[Click], R], key_press: Callable[[KeyPress], R], scroll: Callable[[Scroll], R]) -> R: ...
```

with `Click`, `KeyPress` and `Scroll` `TypedDict`s and their own `build_click` / `build_key_press` /
`build_scroll` factories.

## The demo

See [`demo.py`](./demo.py). It builds each branch by its own type, parses the bytes to the `Event` union,
validates it, and dispatches with `match_event` to a per-branch handler.

```
valid:    True
click at 10,20
key Enter
```

## Running it

From `docs/python/examples`:

```bash
pwsh ./regenerate.ps1 -Check
```
