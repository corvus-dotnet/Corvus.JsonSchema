# TypeScript Patterns - Extending a Base Type

This recipe composes a base type with extra properties using `allOf` — the JSON Schema equivalent of inheritance.

## The Pattern

`allOf: [ { "$ref": "#/$defs/person" } ]` plus this schema's own `properties` produces a single `interface` that **merges** the base and the extension. `Employee` carries `name`/`email` from `Person` and adds `employeeId`/`department`; a value must satisfy every constraint of every member.

## The Schema

File: [`employee.json`](./employee.json)

```json
{ "title": "Employee", "type": "object",
  "$defs": { "person": { "title": "Person", "type": "object", "required": ["name"],
      "properties": { "name": { "type": "string" }, "email": { "type": "string", "format": "email" } } } },
  "allOf": [ { "$ref": "#/$defs/person" } ],
  "required": ["employeeId"],
  "properties": { "employeeId": { "type": "string" }, "department": { "type": "string" } } }
```

The generated `interface Employee` has `name`, `email?`, `employeeId`, `department?` — the base and the extension in one shape.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const bytes = Employee.build({ name: "Ada", email: Email.from("ada@example.com"), employeeId: "E-1", department: "R&D" });
const e = Employee.parse(bytes);
e.name;        // from Person (the base)
e.employeeId;  // from Employee (the extension)
Employee.evaluate({ employeeId: "E-2" }); // false — name is required by the base
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/005-extending/demo.js`.

## Related Patterns

- [006-constraining](../006-constraining/) — `allOf` that tightens a base instead of adding to it
- [010-mixins](../010-mixins/) — `allOf` of several bases at once

## Frequently Asked Questions

### Is the base type still available on its own?

Yes. `Person` is generated as its own `interface` with its own `Person.evaluate`/`Person.build`, so you can use the base independently as well as through `Employee`.
