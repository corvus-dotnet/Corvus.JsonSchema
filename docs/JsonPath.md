# JSONPath Query Language

> **[Try the JSONPath Playground](/playground-jsonpath/)** — evaluate JSONPath expressions in your browser using the Corvus runtime.

## Overview

`Corvus.Text.Json.JsonPath` implements [JSONPath (RFC 9535)](https://www.rfc-editor.org/rfc/rfc9535) for the Corvus.Text.Json document model — a high-performance query language that evaluates JSONPath expressions against JSON data with a plan-based interpreter, fused execution steps, and zero-allocation result buffers.

[JSONPath](https://www.rfc-editor.org/rfc/rfc9535) is a query language for extracting values from JSON documents. It supports property access, wildcards, array index and slice selectors, filter expressions with comparisons and logical operators, recursive descent, and function extensions (`length`, `count`, `value`, `match`, `search`). The Corvus implementation passes **all 723** conformance test cases in the official [JSONPath Compliance Test Suite](https://github.com/jsonpath-standard/jsonpath-compliance-test-suite) (100% conformance).

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Expressions are dynamic, determined at runtime | `Corvus.Text.Json.JsonPath` |
| **Source generator** | Expressions are known at build time, embedded in your project | `Corvus.Text.Json.JsonPath.SourceGenerator` |
| **CLI code generation** | Expressions are known ahead of time, generated outside the build | `Corvus.Json.Cli` (the `jsonpath` command) |

The source generator and CLI tool produce optimized static C# that eliminates plan interpretation overhead and uses direct metadata DB traversal for descendant queries.

> **Choosing between JSONPath and JMESPath:** JSONPath follows the IETF-standardized [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535) syntax (`$.store.book[*].author`) and focuses on **node selection** — it returns the actual matched nodes from the document. JMESPath uses its own syntax and focuses on **data extraction and reshaping** — projections, multiselect, and pipe expressions. If you need an IETF standard or `$..` recursive descent, use JSONPath. If you need projections, aggregations, or result reshaping, see [JMESPath](JMESPath.md) or [JSONata](Jsonata.md).

**Requirements:** The runtime packages target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`. The source generator is an analyzer package and does not impose additional runtime requirements.

## Conformance

![JSONPath Runtime Conformance](https://img.shields.io/badge/JSONPath_Runtime-723%2F723_(100%25)-brightgreen)
![JSONPath CodeGen Conformance](https://img.shields.io/badge/JSONPath_CodeGen-723%2F723_(100%25)-brightgreen)

Both the runtime evaluator and the code generation pipeline pass all **723** official JSONPath compliance test cases (100% conformance) on .NET 10.0. The test suite covers the full RFC 9535 specification including edge cases for Unicode property names, escaped characters, deeply nested structures, and all selector types.

## Performance

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with [JsonEverything (json-everything)](https://json-everything.net/) as the baseline. The **RT** column is the interpreted runtime evaluator; the **CG** column is the source-generated code. Measurements are end-to-end including JSON parsing and include a single-threaded run on .NET 10.0.

| Scenario | Expression | JE | RT | CG | JE Alloc | RT Alloc | CG Alloc |
|----------|-----------|---:|---:|---:|---:|---:|---:|
| SimpleProperty | `$.store.book[0].title` | 1.31 μs | 1.06 μs | 1.08 μs | 2,448 B | 152 B | 152 B |
| ArraySlice | `$.store.book[0:2]` | 1.34 μs | 1.10 μs | 1.08 μs | 2,288 B | 152 B | 152 B |
| Wildcard | `$.store.book[*].author` | 1.31 μs | 1.17 μs | 1.22 μs | 2,448 B | 152 B | 152 B |
| Filter | `$.store.book[?@.price<10]` | 1.30 μs | 1.29 μs | 1.21 μs | 2,288 B | 152 B | 152 B |
| FilterFunction | `$.store.book[?length(@.title)>10]` | 1.30 μs | 1.31 μs | 1.23 μs | 2,288 B | 152 B | 152 B |
| RecursiveDescent | `$..author` | 1.25 μs | 1.08 μs | 1.14 μs | 1,968 B | 152 B | 152 B |

Corvus allocates a fixed 152 bytes per evaluation (the `ParsedJsonDocument` allocation). JsonEverything allocates 1,968–2,448 bytes — 13–16× more. The RT evaluator is faster than JsonEverything on 5 of 6 scenarios (0.81–0.99×) and at parity on the sixth. The CG evaluator is faster on all 6 scenarios (0.81–0.95×).

## Quick start

Install the packages:

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.JsonPath
```

**Simplest approach — cloned result array:**

```csharp
using Corvus.Text.Json.JsonPath;

JsonElement result = JsonPathEvaluator.Default.Query(
    "$.store.book[*].author",
    JsonElement.ParseValue("""
    {
      "store": {
        "book": [
          {"category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95},
          {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
          {"category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "price": 8.99},
          {"category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "price": 22.99}
        ]
      }
    }
    """u8));

Console.WriteLine(result);
// ["Nigel Rees","Evelyn Waugh","Herman Melville","J. R. R. Tolkien"]
```

`Query(expression, data)` evaluates the expression and returns a cloned JSON array containing all matched nodes. It manages memory internally — no workspace or disposal needed.

**Zero-allocation evaluation with `QueryNodes`:**

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonPath;

// Parse the input data (using statement ensures pooled memory is returned)
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"store":{"book":[{"title":"A","price":10},{"title":"B","price":5}]}}""");

// Evaluate — internally pools the result buffer
using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
    "$.store.book[?@.price < 10].title",
    dataDoc.RootElement);

// Access matched nodes directly
foreach (JsonElement node in result.Nodes)
{
    Console.WriteLine(node); // "B"
}
```

`QueryNodes` manages its own buffer internally via `ArrayPool<JsonElement>`. The result is a `ref struct` — always use a `using` statement. For the rare case where you want to supply your own buffer (e.g. to avoid even the pool rent on a very hot path), see [Use `QueryNodes` with a rented buffer](#use-querynodes-for-performance-sensitive-code).

## Evaluation modes

Three evaluation modes are available. All three produce the same results for the same expression; they differ in when compilation happens and what overhead is incurred at evaluation time.

### Interpreted (runtime evaluation)

Use `JsonPathEvaluator` when expressions are determined at runtime:

```csharp
var evaluator = new JsonPathEvaluator();

Span<JsonElement> buf = stackalloc JsonElement[16];
using JsonPathResult result = evaluator.QueryNodes(
    "$.store.book[*].title",
    dataDoc.RootElement,
    buf);
```

Create one `JsonPathEvaluator` instance and reuse it — it caches compiled execution plans per expression string and is thread-safe. For simple cases, `JsonPathEvaluator.Default` provides a shared static instance.

### Source generator (build-time code generation)

When expressions are known at build time, the source generator eliminates all runtime compilation overhead.

**1. Install the source generator package:**

```bash
dotnet add package Corvus.Text.Json.JsonPath.SourceGenerator
```

**2. Create a `.jsonpath` expression file** (e.g. `Expressions/all-authors.jsonpath`):

```
$.store.book[*].author
```

**3. Declare a partial class with the `[JsonPathExpression]` attribute:**

```csharp
using Corvus.Text.Json.JsonPath;

namespace MyApp.Queries;

[JsonPathExpression("all-authors.jsonpath")]
public static partial class AllAuthors;
```

**4. Include the expression file and packages in your `.csproj`:**

```xml
<ItemGroup>
  <AdditionalFiles Include="Expressions\all-authors.jsonpath" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.JsonPath.SourceGenerator"
                    PrivateAssets="all"
                    ReferenceOutputAssembly="false"
                    OutputItemType="Analyzer" />
  <PackageReference Include="Corvus.Text.Json" />
  <PackageReference Include="Corvus.Text.Json.JsonPath" />
</ItemGroup>
```

**5. Call the generated `QueryNodes` method:**

```csharp
Span<JsonElement> buf = stackalloc JsonElement[16];
using JsonPathResult result = AllAuthors.QueryNodes(dataDoc.RootElement, buf);
```

The generated method directly evaluates the expression without plan interpretation or delegate dispatch.

**Diagnostic messages:**

| Code | Severity | Description |
|------|----------|-------------|
| JPTHSG001 | Error | Expression file not found in `AdditionalFiles` |
| JPTHSG002 | Error | Expression file is empty |
| JPTHSG003 | Error | Code generation failed (invalid expression or unsupported feature) |

### CLI code generation

The `corvusjson` CLI tool includes a `jsonpath` subcommand for ahead-of-time code generation outside the MSBuild pipeline:

```bash
dotnet tool install --global Corvus.Json.Cli
```

```bash
corvusjson jsonpath <expressionFile> \
    --className <ClassName> \
    --namespace <Namespace> \
    [--outputPath <output.cs>]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `<expressionFile>` | Yes | Path to the `.jsonpath` expression file |
| `--className` | Yes | Name of the generated static class |
| `--namespace` | Yes | Namespace for the generated class |
| `--outputPath` | No | Output file path (defaults to `<ClassName>.cs`) |

Example:

```bash
corvusjson jsonpath expressions/all-authors.jsonpath \
    --className AllAuthors \
    --namespace MyApp.Queries \
    --outputPath Generated/AllAuthors.cs
```

This produces a self-contained `.cs` file with the same static `QueryNodes` method as the source generator. Use the CLI tool when:

- You need to generate code outside the MSBuild pipeline
- You want to inspect or modify the generated code before committing it
- You are integrating with a non-.NET build system

## How-to guides

### Result handling

JSONPath queries return a **node list** — zero or more `JsonElement` values matched in document order.

**Quick result as JSON array:**

```csharp
JsonElement array = JsonPathEvaluator.Default.Query("$.store.book[*].title", data);
// Returns a JSON array: ["Sayings of the Century", "Sword of Honour", ...]
// Returns [] if no matches
```

**Zero-allocation node access:**

```csharp
Span<JsonElement> buf = stackalloc JsonElement[16];
using JsonPathResult result = JsonPathEvaluator.Default.QueryNodes(
    "$.store.book[*].title", data, buf);

ReadOnlySpan<JsonElement> nodes = result.Nodes;
int count = result.Count;

// Access individual nodes
if (count > 0)
{
    JsonElement first = result.First;
}
```

### Error handling

All evaluation errors throw `JsonPathException` with a descriptive message:

```csharp
try
{
    JsonElement result = evaluator.Query(expression, data);
}
catch (JsonPathException ex)
{
    Console.WriteLine($"JSONPath error: {ex.Message}");
}
```

Common error scenarios:

| Error | Example | Description |
|-------|---------|-------------|
| Syntax error | `$.store.book[` | Unterminated bracket or invalid syntax |
| Invalid filter | `$[?@.price >>]` | Malformed filter expression |
| Bad slice | `$[1:2:0]` | Slice step cannot be zero |

## Expression reference

JSONPath expressions follow [RFC 9535](https://www.rfc-editor.org/rfc/rfc9535). This section summarises the key features with examples. All examples use this bookstore document:

```json
{
  "store": {
    "book": [
      {"category": "reference", "author": "Nigel Rees", "title": "Sayings of the Century", "price": 8.95},
      {"category": "fiction", "author": "Evelyn Waugh", "title": "Sword of Honour", "price": 12.99},
      {"category": "fiction", "author": "Herman Melville", "title": "Moby Dick", "price": 8.99},
      {"category": "fiction", "author": "J. R. R. Tolkien", "title": "The Lord of the Rings", "price": 22.99}
    ],
    "bicycle": {"color": "red", "price": 399.99}
  }
}
```

### Root identifier (`$`)

Every JSONPath expression starts with `$`, which refers to the root of the input document:

```
// Expression: $
// Result:     (the entire document)
```

### Property access (name selector)

Access a property by name using dot notation or bracket notation:

```
// Expression: $.store.bicycle.color
// Result:     "red"

// Expression: $['store']['bicycle']['color']
// Result:     "red"
```

If the property does not exist, the result is empty (no match).

### Recursive descent (`..`)

The descendant operator `..` searches the entire subtree for matching selectors:

```
// Expression: $..author
// Result:     ["Nigel Rees", "Evelyn Waugh", "Herman Melville", "J. R. R. Tolkien"]

// Expression: $..price
// Result:     [8.95, 12.99, 8.99, 22.99, 399.99]
```

The Corvus implementation uses a flat metadata DB scan for descendant property queries — a linear walk of the internal token array without enumerator construction or call recursion.

### Wildcard (`*`)

Select all elements of an array or all values of an object:

```
// Expression: $.store.book[*].author
// Result:     ["Nigel Rees", "Evelyn Waugh", "Herman Melville", "J. R. R. Tolkien"]

// Expression: $.store.*
// Result:     [(the book array), (the bicycle object)]
```

### Index selector

Access array elements by zero-based index. Negative indices count from the end:

```
// Expression: $.store.book[0].title
// Result:     "Sayings of the Century"

// Expression: $.store.book[-1].title
// Result:     "The Lord of the Rings"
```

### Array slice (`start:end:step`)

Extract a sub-array with `[start:end:step]`:

```
// Expression: $.store.book[0:2]
// Result:     [(first two books)]

// Expression: $.store.book[::-1]
// Result:     [(all books in reverse order)]

// Expression: $.store.book[::2]
// Result:     [(every other book)]
```

### Filter selector (`?`)

Filter elements with a boolean expression:

```
// Expression: $.store.book[?@.price < 10]
// Result:     [(books with price < 10)]

// Expression: $.store.book[?@.category == 'fiction']
// Result:     [(fiction books)]

// Expression: $.store.book[?@.price < 10 && @.category == 'reference']
// Result:     [(reference books under 10)]
```

Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`.

Logical operators: `&&`, `||`, `!`.

The `@` token refers to the current element being tested. `$` can also be used in filter expressions to reference the root document.

### Union selector

Select multiple properties or indices in a single segment:

```
// Expression: $.store.book[0,1]
// Result:     [(first two books)]

// Expression: $.store.book[0].['title','author']
// Result:     ["Sayings of the Century", "Nigel Rees"]
```

### Function extensions

RFC 9535 defines a set of function extensions for use in filter expressions:

| Function | Description | Example |
|----------|-------------|---------|
| `length(@.field)` | Length of a string, array, or object | `$[?length(@.name) > 5]` |
| `count(@.items)` | Number of nodes in a node list | `$[?count(@.tags) > 0]` |
| `value(@.field)` | Extract a single value from a node list | `$[?value(@.score) > 90]` |
| `match(@.field, 'regex')` | Full regex match against a string | `$[?match(@.name, '^J.*')]` |
| `search(@.field, 'regex')` | Partial regex match against a string | `$[?search(@.name, 'ohn')]` |

## Common pitfalls

### Always dispose `ParsedJsonDocument`

`ParsedJsonDocument<T>` rents memory from `ArrayPool<byte>`. Forgetting to dispose it leaks pooled memory:

```csharp
// ❌ BAD — leaks pooled memory
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
using var result = evaluator.QueryNodes(expression, doc.RootElement);

// ✅ GOOD — using statement returns memory to the pool
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
using var result = evaluator.QueryNodes(expression, doc.RootElement);
```

### Always dispose `JsonPathResult`

`JsonPathResult` may rent memory from `ArrayPool<JsonElement>`. Always use a `using` statement:

```csharp
// ❌ BAD — may leak pooled array
JsonPathResult result = evaluator.QueryNodes(expression, data);
// ... use result but forget to dispose

// ✅ GOOD
using JsonPathResult result = evaluator.QueryNodes(expression, data);
```

### Don't forget `AdditionalFiles` for the source generator

The source generator reads expression files from `AdditionalFiles`. Without the MSBuild item, the generator can't find the expression and produces diagnostic `JPTHSG001`:

```xml
<!-- ❌ Missing — generator produces JPTHSG001 -->
<ItemGroup>
  <None Include="Expressions\query.jsonpath" />
</ItemGroup>

<!-- ✅ Correct -->
<ItemGroup>
  <AdditionalFiles Include="Expressions\query.jsonpath" />
</ItemGroup>
```

### Use `QueryNodes` for performance-sensitive code

`Query()` is convenient but creates a workspace, builds a JSON array, and clones it. In hot paths, use `QueryNodes()` with a stack-allocated buffer:

```csharp
// ❌ Allocates per call
JsonElement result = evaluator.Query(expression, data);

// ✅ Zero allocation when results fit in buffer
Span<JsonElement> buf = stackalloc JsonElement[16];
using JsonPathResult result = evaluator.QueryNodes(expression, data, buf);
```

## Comparison with other libraries

| Feature | Corvus.Text.Json.JsonPath | JsonEverything |
|---------|--------------------------|----------------|
| Specification | RFC 9535 (100% conformance) | RFC 9535 |
| Evaluation model | Plan-based interpreter (cached) | Direct interpretation |
| Code generation | Source generator + CLI tool | Not available |
| JSON document model | `Corvus.Text.Json` (pooled, zero-copy) | `System.Text.Json.Nodes` |
| Memory model | Pooled (`ArrayPool`, stack-allocated buffers) | GC-allocated |
| Zero-allocation hot path | Yes (with `QueryNodes` + stack buffer) | No |
| Recursive descent | Flat metadata DB scan (no recursion) | Recursive tree walk |
| Supported frameworks | net9.0+, netstandard2.0/2.1 | net8.0+ |
