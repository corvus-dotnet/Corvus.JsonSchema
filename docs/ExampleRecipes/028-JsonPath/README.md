# JSON Schema Patterns in .NET — JSONPath Query Language

This recipe demonstrates how to query JSON data using [JSONPath (RFC 9535)](https://www.rfc-editor.org/rfc/rfc9535) expressions with the `Corvus.Text.Json.JsonPath` library. It covers property access, wildcards, recursive descent, slicing, filter expressions with logical operators and functions, zero-allocation evaluation, build-time compiled expressions, and custom function extensions.

## The Pattern

JSONPath is a standardised query language for selecting nodes within a JSON document. Given a root document `$`, expressions navigate objects (`.name`), arrays (`[0]`, `[*]`, `[0:2]`), and filter nodes (`[?expr]`). The Corvus implementation passes 100% of the JSONPath Compliance Test Suite (723/723 tests).

Expressions are compiled to an execution plan on first use and cached per evaluator instance. The evaluator is thread-safe and supports zero-allocation queries via `QueryNodes` with a caller-provided buffer.

## The Data

File: `bookstore.json`

```json
{
  "store": {
    "book": [
      {"category": "reference", "author": "Sandi Toksvig", "title": "Between the Stops", "price": 8.95},
      {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
      {"category": "fiction", "author": "Jane Austen", "title": "Pride and Prejudice", "price": 8.99},
      {"category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "price": 22.99}
    ],
    "bicycle": {"color": "red", "price": 399.99}
  }
}
```

## Basic Queries

### Property access

Navigate to a specific property using dot notation:

```csharp
evaluator.Query("$.store.bicycle.color", data);
// ["red"]
```

### Wildcards

Select all elements of an array or object:

```csharp
evaluator.Query("$.store.book[*].author", data);
// ["Sandi Toksvig","Evelyn Waugh","Jane Austen","J. R. R. Tolkien"]
```

### Recursive descent

Find matching keys at any depth in the document tree:

```csharp
evaluator.Query("$..author", data);
// ["Sandi Toksvig","Evelyn Waugh","Jane Austen","J. R. R. Tolkien"]
```

### Index access

Select array elements by position. Negative indices count from the end:

```csharp
evaluator.Query("$.store.book[0].title", data);   // ["Between the Stops"]
evaluator.Query("$.store.book[-1].title", data);   // ["The Lord of the Rings"]
```

### Array slicing

Select a contiguous range of elements (`start:end`):

```csharp
evaluator.Query("$.store.book[0:2].title", data);
// ["Between the Stops","Sword of Honour"]
```

## Filter Expressions

### Simple comparison

```csharp
evaluator.Query("$.store.book[?@.price<10].title", data);
// ["Between the Stops","Pride and Prejudice"]
```

### Logical operators

Combine conditions with `&&` (and) and `||` (or):

```csharp
evaluator.Query("$.store.book[?@.price<10 && @.category=='fiction'].title", data);
// ["Pride and Prejudice"]
```

### Built-in functions

RFC 9535 defines five built-in functions: `length`, `count`, `value`, `match`, and `search`.

```csharp
evaluator.Query("$.store.book[?length(@.title)>15].title", data);
// ["Between the Stops","Pride and Prejudice","The Lord of the Rings"]
```

## Zero-Allocation QueryNodes

For high-throughput scenarios, `QueryNodes` avoids the overhead of constructing a result array. Pass a caller-provided buffer (which may be stack-allocated) and iterate over the matching nodes directly:

```csharp
Span<JsonElement> buf = stackalloc JsonElement[16];
using JsonPathResult result = evaluator.QueryNodes("$.store.book[*].title", data, buf);

foreach (JsonElement node in result.Nodes)
{
    Console.WriteLine(node);
}
```

If the result exceeds the buffer, the evaluator transparently rents from `ArrayPool<JsonElement>`. The `JsonPathResult` must be disposed to return any rented memory.

## Source-Generated Expressions

For maximum performance, compile expressions at build time using the `[JsonPathExpression]` attribute. Add the expression file as an `AdditionalFiles` item and reference the source generator package.

File: `all-authors.jsonpath`

```
$..author
```

```csharp
[JsonPathExpression("all-authors.jsonpath")]
public static partial class AllAuthors;
```

The generator produces optimised C# code that evaluates the expression without parsing or planning at runtime:

```csharp
using JsonPathResult result = AllAuthors.QueryNodes(data);
foreach (JsonElement node in result.Nodes)
{
    Console.WriteLine(node);
}
```

## Custom Function Extensions

The JSONPath evaluator supports custom function extensions. Implement `IJsonPathFunction` to define a function with typed parameters and return type. Custom functions participate in RFC 9535 well-typedness checking at parse time.

### ceil — ValueType → ValueType

A function that returns the ceiling of a numeric value:

```csharp
sealed class CeilFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        JsonElement value = arguments[0].Value;
        if (value.ValueKind != JsonValueKind.Number)
            return JsonPathFunctionResult.Nothing;

        int ceiled = (int)Math.Ceiling(value.GetDouble());
        return JsonPathFunctionResult.FromValue(JsonPathCodeGenHelpers.IntToElement(ceiled));
    }
}
```

Register it on a new evaluator instance:

```csharp
var evaluator = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction> { ["ceil"] = new CeilFunction() });

evaluator.Query("$.store.book[?ceil(@.price)==9].title", data);
// ["Between the Stops","Pride and Prejudice"]
```

### is_fiction — ValueType → LogicalType

A function that returns a logical (boolean) result for use in filter expressions:

```csharp
sealed class IsFictionFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.ValueType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.LogicalType;
    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        JsonElement value = arguments[0].Value;
        return JsonPathFunctionResult.FromLogical(
            value.ValueKind == JsonValueKind.String && value.ValueEquals("fiction"u8));
    }
}
```

```csharp
var evaluator = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction> { ["is_fiction"] = new IsFictionFunction() });

evaluator.Query("$.store.book[?is_fiction(@.category)].title", data);
// ["Sword of Honour","Pride and Prejudice","The Lord of the Rings"]
```

### node_count — NodesType → ValueType

A function that receives a node list and returns the count. The `NodesType` parameter gives the function access to all nodes matched by a filter-query argument:

```csharp
sealed class NodeCountFunction : IJsonPathFunction
{
    private static readonly JsonPathFunctionType[] ParamTypes = [JsonPathFunctionType.NodesType];

    public JsonPathFunctionType ReturnType => JsonPathFunctionType.ValueType;
    public ReadOnlySpan<JsonPathFunctionType> ParameterTypes => ParamTypes;

    public JsonPathFunctionResult Evaluate(ReadOnlySpan<JsonPathFunctionArgument> arguments)
    {
        return JsonPathFunctionResult.FromValue(
            JsonPathCodeGenHelpers.IntToElement(arguments[0].NodeCount));
    }
}
```

```csharp
var evaluator = new JsonPathEvaluator(
    new Dictionary<string, IJsonPathFunction> { ["node_count"] = new NodeCountFunction() });

evaluator.Query("$[?node_count(@.book[*])>3].bicycle.color", data);
// ["red"]
```

## Running the Example

```bash
cd docs/ExampleRecipes/028-JsonPath
dotnet run
```

## Related Patterns

- [025-Jsonata](../025-Jsonata/) — JSONata expression evaluation and transformation
- [026-JMESPath](../026-JMESPath/) — JMESPath query language
- [024-JsonLogic](../024-JsonLogic/) — JsonLogic rule evaluation
- [023-JsonPatch](../023-JsonPatch/) — RFC 6902 JSON Patch operations
