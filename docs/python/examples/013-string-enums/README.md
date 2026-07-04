# Python patterns, string enumerations

This recipe defines an `enum` of strings in JSON Schema and shows how the Corvus.Text.Json **Python** generator
projects it as a `Literal` union, the idiomatic Python enumeration.

## The pattern

A string `enum` becomes a `Literal` type alias of the member strings (here `Status = Literal["todo",
"in_progress", "done"]`), not a runtime `enum.Enum`. That gives an exhaustive, statically checked set with no
runtime construct and full editor completion. A plain JSON string is a member directly, and `evaluate` rejects
any value outside the set.

The generator emits, for the root schema, a `TypedDict` (`Task`) that types each enum property with its
`Literal` alias, plus the module-level API to evaluate, build, parse and mutate the value.

- `Status` / `Priority`. `Literal[...]` aliases, one per enum. Because a `Literal` is exhaustive, it keys a
  total lookup: every member must be handled or the type does not check.
- `evaluate(value)`. An AOT-compiled boolean evaluator (no exceptions). It accepts the wire bytes directly (it
  decodes them) or an already-parsed value, and it fails any value outside the enum.
- `parse(data)`. Decode `bytes | str` into a typed `Task`; the enum properties narrow to their `Literal` alias.
- `build_task(props)`. Construct compact UTF-8 JSON bytes from plain values.
- `build_canonical_task(props)`. The same, with RFC 8785 sorted keys.
- `patch_task(source, changes, removals)`. Change only the named fields, spliced at the byte level.

## The schema

```json
{ "title": "Task", "type": "object", "required": ["status"],
  "properties": { "status": { "enum": ["todo", "in_progress", "done"] }, "priority": { "enum": ["low", "medium", "high"] } } }
```

## The generated surface

```python
class Task(TypedDict, total=False):
    priority: Priority
    status: Required[Status]


type Priority = Literal["low", "medium", "high"]
type Status = Literal["todo", "in_progress", "done"]
```

## The demo

See [`demo.py`](./demo.py). It builds a `Task`, validates trusted and untrusted input, parses bytes back to a
typed value, and uses the `Status` `Literal` to key an exhaustive label lookup.

```
1. built:      {"status":"in_progress","priority":"high"}
2. valid:      True
   bad value:  False
3. status:     in_progress
4. label:      In progress
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
