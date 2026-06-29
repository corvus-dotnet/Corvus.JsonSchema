# TypeScript Patterns - String Enumerations

This recipe shows `enum` of strings generating a string-literal union — the idiomatic TypeScript enum.

## The Pattern

A string `enum` becomes a `type` alias of string literals (`type Status = "todo" | "in_progress" | "done"`), not a TypeScript `enum`. That gives exhaustive `switch`/lookup with no runtime cost and full editor completion; `Task.evaluate` rejects any value outside the set.

## The Schema

File: [`task.json`](./task.json)

```json
{ "title": "Task", "type": "object", "required": ["status"],
  "properties": { "status": { "enum": ["todo", "in_progress", "done"] }, "priority": { "enum": ["low", "medium", "high"] } } }
```

The generated `type Status = "todo" | "in_progress" | "done"` (and `Priority`), with `Task` using them.

## Generated Code Usage

[Example code](./demo.ts)

```typescript
const label: Record<Status, string> = { todo: "To do", in_progress: "In progress", done: "Done" };
label[task.status];                        // exhaustive — every member must be handled
Task.evaluate({ status: "archived" });      // false — not a member
```

## Running the Example

From `docs/typescript/examples/` (`npm install` once): `npm run build` then `node dist/013-string-enums/demo.js`.

## Related Patterns

- [014-numeric-enums](../014-numeric-enums/) — numeric `enum`
- [012-discriminated-unions](../012-discriminated-unions/) — enums as union discriminants

## Frequently Asked Questions

### Why a string-literal union instead of a TypeScript `enum`?

A literal union is structural, erases at compile time (no runtime object), accepts plain JSON string values directly, and is exhaustively checkable with `Record<Status, …>` or a `switch`. A TypeScript `enum` would add a runtime construct and force callers to import and use enum members instead of plain strings.
