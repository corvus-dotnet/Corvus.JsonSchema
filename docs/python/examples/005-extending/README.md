# Python patterns, extending a base type

This recipe composes a base type with extra properties using `allOf`, the JSON Schema equivalent of
inheritance, and shows how the Corvus.Text.Json **Python** generator merges the base and the extension
into one `TypedDict`.

## The pattern

`allOf: [ { "$ref": "#/$defs/person" } ]` plus this schema's own `properties` produces a single
`TypedDict` that **merges** the base and the extension. `Employee` carries `name` / `email` from `Person`
and adds `employeeId` / `department`, and a value must satisfy every constraint of every member. Required
members are typed `Required[...]`, so `name` (from the base) and `employeeId` (from the extension) are both
required while `email` and `department` are optional.

The base type is generated on its own too. `Person` is emitted as its own `TypedDict` with its own
`build_person` / `parse_person`, so you can use it independently as well as through `Employee`. The root
`Employee` is the one wired to the module-level `evaluate`.

- `evaluate(value)`. An AOT-compiled boolean evaluator for the root `Employee`. It accepts the wire bytes
  directly (it decodes them) or an already-parsed value, and it enforces every member of the `allOf`.
- `build_employee(props)` / `build_canonical_employee(props)`. Compact / RFC 8785 UTF-8 JSON bytes.
- `parse(data)` / `parse_employee(data)`. Decode `bytes | str` into a typed `Employee`.
- `build_person(props)` / `parse_person(data)`. The independent surface for the base type.
- `from_email(value)`. A validating factory for the `email`-formatted branded type on the base.

## The schema

```json
{ "title": "Employee", "type": "object",
  "$defs": { "person": { "title": "Person", "type": "object", "required": ["name"],
      "properties": { "name": { "type": "string" }, "email": { "type": "string", "format": "email" } } } },
  "allOf": [ { "$ref": "#/$defs/person" } ],
  "required": ["employeeId"],
  "properties": { "employeeId": { "type": "string" }, "department": { "type": "string" } } }
```

## The generated surface

```python
class Employee(TypedDict, total=False):
    department: str
    email: Email
    employeeId: Required[str]
    name: Required[str]


class Person(TypedDict, total=False):
    email: Email
    name: Required[str]


Email = NewType("Email", str)
```

## The demo

See [`demo.py`](./demo.py). It builds an `Employee` (a validating `Email` brand for the base's
`format: email`), validates trusted and untrusted input, parses bytes back to a typed value reading both a
base member and an extension member, and builds the base `Person` on its own.

```
1. built:         {"name":"Ada","email":"ada@example.com","employeeId":"E-1","department":"R&D"}
2. valid:         True
   missing name:  False
3. name (base):   Ada
   id (own):      E-1
4. base person:   {"name":"Grace"}
```

## Running it

From `docs/python/examples`, regenerate every recipe and run its checks (mypy `--strict`, pyright, and the
demo) with:

```bash
pwsh ./regenerate.ps1 -Check
```
