# The type surface

How each JSON Schema construct maps to generated TypeScript. Each row links to a runnable recipe.

| Construct | Generated TypeScript | Recipe |
|-----------|----------------------|--------|
| `type: object` + `properties` | `readonly interface` | [001](./examples/001-data-object/) |
| required / optional property | `name: T` / `name?: T` | [001](./examples/001-data-object/) |
| `type: string` / `number` / `integer` / `boolean` / `null` | `string` / `number` / `number` / `boolean` / `null` | [001](./examples/001-data-object/) |
| `format` | branded type `Brand<string, "fmt">` + `as{Name}` factory | [019](./examples/019-formats/) |
| validation keywords (`minLength`, `minimum`, `pattern`, `multipleOf`, …) | (no type change; enforced by `evaluateRoot`) | [002](./examples/002-validation/) |
| `$ref` / `$defs` | one shared named type | [003](./examples/003-references/) |
| `additionalProperties` / `unevaluatedProperties: false` | open (extra keys allowed) / closed (rejected) | [004](./examples/004-open-and-closed/) |
| `allOf` (base + extension) | merged `interface` | [005](./examples/005-extending/) |
| `allOf` (constrain a base) | same shape, combined constraints | [006](./examples/006-constraining/) |
| `allOf` (several bases) | merged `interface` | [010](./examples/010-mixins/) |
| `array` + `items` | `readonly T[]` | [007](./examples/007-arrays/) |
| nested `items` | `readonly (readonly T[])[]` | [008](./examples/008-nested-arrays/) |
| `prefixItems` + `items: false` + `minItems` | tuple `readonly [A, B, C]` | [009](./examples/009-tuples/) |
| `oneOf` / `anyOf` | union `A \| B` + `is{Branch}` guards + `match*` | [011](./examples/011-unions/) |
| `oneOf` with `const` discriminant | discriminated union, `match*` keyed off the tag | [012](./examples/012-discriminated-unions/) |
| string `enum` | string-literal union `type X = "a" \| "b"` | [013](./examples/013-string-enums/) |
| numeric `enum` | numeric-literal union `type X = 200 \| 404` | [014](./examples/014-numeric-enums/) |
| `additionalProperties: { … }` (map) | index signature `{ readonly [key: string]: T }` | [015](./examples/015-maps/) |
| `if` / `then` / `else` | base shape; conditional enforced by `evaluateRoot` | [017](./examples/017-conditional/) |
| `default` | property made optional (value not applied — apply at read site) | [018](./examples/018-defaults/) |
| `const` | the literal type of the value | [012](./examples/012-discriminated-unions/) |

## Shapes vs constraints

A recurring split runs through the table: a construct that **changes the shape** of the value appears in the *type* (an object → `interface`, an array → `readonly T[]`, a `oneOf` → a union). A construct that only **constrains** a value that already has a shape (a length, a range, a pattern, a conditional requirement) does *not* appear in the type — TypeScript can't express it — and is enforced by `evaluateRoot` instead. A `format` is the one construct that appears in **both**: a branded type *and* a runtime check.

## Names

Type names come from the schema: a usable `title`, else the property or `$defs` key the type sits under, else the document name; pascal-cased, de-duplicated, and kept clear of TypeScript reserved words and globals. Identical subschemas are de-duplicated to a single type, so referencing one shape many times yields one `interface`.

## What each type comes with

A generated object type carries more than its `interface`:

- `evaluate{Type}(value, ev)` — the per-type evaluator;
- `build{Type}(props)` — construct bytes;
- `patch{Type}` / `produce{Type}` — mutate bytes;
- for a union, `is{Branch}` guards and `match{Type}`;
- for a `format`, an `as{Name}` factory.

Plus, once per module, `evaluateRoot` (the document entry point and `default` export).

## See also

- [getting-started](./getting-started.md), [reading-and-validating](./reading-and-validating.md), [mutation](./mutation.md), [value-types](./value-types.md)
- [examples](./examples/) — every row above, as a runnable recipe.
