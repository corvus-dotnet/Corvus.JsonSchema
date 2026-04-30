# JSON Schema Patterns in .NET - JMESPath Query Evaluation

This recipe demonstrates how to evaluate [JMESPath](https://jmespath.org/) expressions against JSON data using the `Corvus.Text.Json.JMESPath` library. It covers basic identifiers, sub-expressions, index expressions, projections (list, object, flatten, filter), slicing, pipe expressions, multiselect (lists and hashes), built-in functions, and build-time expression compilation via the source generator.

## The Pattern

JMESPath is a query language for JSON that lets you extract and transform elements from a JSON document. Expressions are compiled to delegate trees on first use and cached. The evaluator is thread-safe and designed for high-throughput, zero-allocation evaluation.

The Corvus implementation passes all tests in the official [JMESPath compliance test suite](https://github.com/jmespath/jmespath.test).

## The Schema

File: `order.json`

```json
{
    "title": "Order",
    "type": "object",
    "required": ["customer", "items"],
    "properties": {
        "customer": { "type": "string" },
        "items": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["name", "price", "quantity"],
                "properties": {
                    "name": { "type": "string" },
                    "price": { "type": "number" },
                    "quantity": { "type": "integer", "format": "int32" }
                }
            }
        }
    }
}
```

## Evaluating Expressions

### Search — the core API

JMESPath expressions are evaluated with `JMESPathEvaluator.Search()`:

```csharp
var evaluator = JMESPathEvaluator.Default;

// Simple search — result is cloned and safe to use
JsonElement customer = evaluator.Search("customer", dataDoc.RootElement);
// "Alice"

// With a workspace for zero-allocation evaluation
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Search("items[0].name", dataDoc.RootElement, workspace);
// "Widget"
```

## Basic Expressions

```csharp
// Identifier — select a key
evaluator.Search("customer", data);
// "Alice"

// Sub-expression — nested navigation
evaluator.Search("a.b.c.d", data);

// Index expression — select by position (0-based)
evaluator.Search("items[0].name", data);
// "Widget"

// Negative index — from end of array
evaluator.Search("items[-1].name", data);
// "Doohickey"
```

## Projections

### List projections (`[*]`)

Apply an expression to each element in an array. `null` results are omitted.

```csharp
evaluator.Search("people[*].first", peopleData);
// ["James", "Jacob", "Jayden"]
```

### Object projections (`*`)

Apply an expression to each value in an object:

```csharp
evaluator.Search("ops.*.numArgs", opsData);
// [2, 3]
```

### Flatten projections (`[]`)

Flatten nested arrays into a single list:

```csharp
evaluator.Search("reservations[].instances[].state", reservationsData);
// ["running", "stopped", "terminated", "running"]
```

### Filter projections (`[? ... ]`)

Filter array elements by a predicate:

```csharp
evaluator.Search("machines[?state=='running'].name", machinesData);
// ["a", "c"]
```

## Slicing

Select contiguous subsets of an array using `[start:stop:step]`:

```csharp
evaluator.Search("[0:5]", numbersData);   // [0, 1, 2, 3, 4]
evaluator.Search("[::2]", numbersData);   // [0, 2, 4, 6, 8]
evaluator.Search("[::-1]", numbersData);  // [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
```

## Pipe Expressions

Stop a projection and operate on its result:

```csharp
evaluator.Search("people[*].first | [0]", peopleData);
// "James"
```

Without the pipe, `people[*].first[0]` would try to index each string (which is not valid), returning an empty array. The pipe collects the projection result first, then applies `[0]` to the collected array.

## MultiSelect

### MultiSelect lists

Create arrays from selected values:

```csharp
evaluator.Search("people[*].[name, state.name]", data);
// [["James", "Virginia"], ["Jacob", "Texas"], ["Jayden", "Florida"]]
```

### MultiSelect hashes

Create objects with renamed keys:

```csharp
evaluator.Search("people[*].{Name: name, State: state.name}", data);
// [{"Name": "James", "State": "Virginia"}, ...]
```

## Built-in Functions

JMESPath provides functions for aggregation, type checking, string operations, and more:

| Category | Functions |
|---|---|
| Aggregation | `sum`, `avg`, `min`, `max`, `length` |
| String | `starts_with`, `ends_with`, `contains`, `join`, `reverse` |
| Type | `type`, `to_number`, `to_string`, `to_array`, `not_null` |
| Math | `abs`, `ceil`, `floor` |
| Sorting | `sort`, `sort_by`, `min_by`, `max_by` |
| Object | `keys`, `values`, `merge` |
| Higher-order | `map` |

```csharp
evaluator.Search("length(items)", data);                  // 3
evaluator.Search("sum(items[*].price)", data);             // 129.49
evaluator.Search("max_by(items, &price).name", data);      // "Gadget"
evaluator.Search("sort_by(items, &price)[*].name", data);  // ["Doohickey", "Widget", "Gadget"]

// Filter with contains
evaluator.Search("myarray[?contains(@, 'foo')]", data);
// ["foo", "foobar", "barfoo", "barfoobaz"]
```

Note the `&` prefix in `sort_by` and `max_by` — this creates an expression reference (similar to a lambda) that the function evaluates against each array element.

## Source-Generated Expressions

For maximum performance, compile expressions at build time using the `[JMESPathExpression]` attribute. Add the expression file as an `AdditionalFiles` item and reference the source generator:

File: `total-price.jmespath`

```
sum(items[*].price)
```

```csharp
[JMESPathExpression("total-price.jmespath")]
public static partial class TotalPrice;
```

The generator compiles the expression to optimized C# at build time:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = TotalPrice.Evaluate(dataDoc.RootElement, workspace);
// 129.49
```

## Running the Example

```bash
cd docs/ExampleRecipes/026-JMESPath
dotnet run
```

## Related Patterns

- [025-JSONata](../025-JSONata/) — JSONata expression evaluation (a more expressive query language with object construction, variable bindings, and higher-order functions)
- [024-JsonLogic](../024-JsonLogic/) — JsonLogic rule evaluation (a declarative rules engine)
- [023-JsonPatch](../023-JsonPatch/) — RFC 6902 JSON Patch operations
