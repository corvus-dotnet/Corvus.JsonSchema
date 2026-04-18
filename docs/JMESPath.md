# JMESPath Query Language

> **[Try the JMESPath Playground](/playground-jmespath/)** — evaluate JMESPath expressions in your browser using the Corvus runtime.

## Overview

`Corvus.Text.Json.JMESPath` implements [JMESPath](https://jmespath.org/) for the Corvus.Text.Json document model — a high-performance query language that evaluates JMESPath expressions against JSON data with compiled delegate trees, pipe fusion, and pooled workspace memory.

[JMESPath](https://jmespath.org/) is a query language for JSON. It supports path navigation, sub-expressions, index access, slicing, list and object projections, flatten, filter expressions, multiselect lists and hashes, pipe expressions, comparisons, and a full set of built-in functions. The Corvus implementation passes **all 892** conformance test cases in the official [JMESPath Compliance Test Suite](https://github.com/jmespath/jmespath.test) (100% conformance). The test suite also contains 21 benchmark-only entries that are implemented as BenchmarkDotNet benchmarks with baseline comparison to JmesPath.Net; these are used for performance measurement, not conformance, and are excluded from the count.

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Expressions are dynamic, determined at runtime | `Corvus.Text.Json.JMESPath` |
| **Source generator** | Expressions are known at build time, embedded in your project | `Corvus.Text.Json.JMESPath.SourceGenerator` |
| **CLI code generation** | Expressions are known ahead of time, generated outside the build | `Corvus.Json.CodeGenerator` (the `jmespath` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch.

> **Choosing between JMESPath and JSONata:** JMESPath is ideal for **querying and extracting** data from JSON — projections, filtering, slicing, and reshaping with a concise, standardized syntax. If you need **arithmetic, string concatenation, user-defined functions, or complex transformations**, see [JSONata](Jsonata.md) instead. JMESPath is a pure query language with no side effects and no arithmetic operators.

**Requirements:** The runtime packages target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`. The source generator is an analyzer package and does not impose additional runtime requirements.

## Conformance

![JMESPath Runtime Conformance](https://img.shields.io/badge/JMESPath_Runtime-892%2F892_(100%25)-brightgreen)
![JMESPath CodeGen Conformance](https://img.shields.io/badge/JMESPath_CodeGen-892%2F892_(100%25)-brightgreen)

Both the runtime evaluator and the code generation pipeline pass all **892** official JMESPath compliance test cases (100% conformance) on .NET 10.0.

## Performance

All benchmarks use [BenchmarkDotNet](https://benchmarkdotnet.org/) with [JmesPath.Net](https://github.com/jdevillard/JmesPath.Net) as the baseline. The **RT** column is the interpreted runtime evaluator; the **CG** column is the source-generated code. Measurements are from a single-threaded run on .NET 10.0.

| Benchmark | Expression | JmesPath.Net | RT | CG | RT Alloc | CG Alloc |
|-----------|-----------|-------------:|---:|---:|---------:|---------:|
| SimpleField | `a` | 6,446 ns | 44 ns | 35 ns | 0 B | 0 B |
| SimpleSubexpr | `a.b.c` | 6,659 ns | 51 ns | 49 ns | 0 B | 0 B |
| SimpleOr | `a \|\| b` | 6,948 ns | 83 ns | 69 ns | 0 B | 0 B |
| LongString | `foo` (1 KB value) | 3,874 ns | 54 ns | 11 ns | 0 B | 0 B |
| ChainedFilter | `a[?b==\`1\`][?c==\`2\`]` | 2,697 ns | 18 ns | 11 ns | 0 B | 0 B |
| Field50 | 50 chained fields | 21,692 ns | 563 ns | 80 ns | 0 B | 0 B |
| Index50 | 50 chained indices | 21,668 ns | 588 ns | 79 ns | 0 B | 0 B |
| Pipe50 | 50 chained pipes | 24,274 ns | 384 ns | 92 ns | 0 B | 0 B |
| DeepAnds | Nested `&&` | 12,399 ns | 2,192 ns | 2,073 ns | 0 B | 0 B |
| DeepOrs | Nested `\|\|` | 19,717 ns | 248 ns | 171 ns | 0 B | 0 B |
| DeepMatch | Deep wildcard match | 8,305 ns | 287 ns | 439 ns | 0 B | 0 B |
| DeepNoMatch | Deep wildcard miss | 13,838 ns | 498 ns | 386 ns | 0 B | 0 B |
| DeepProjection | `[*].[*].[*]` | 82,276 ns | 148 ns | 18 ns | 0 B | 0 B |
| MultiList | Multi-select list | 19,178 ns | 3,856 ns | 3,996 ns | 120 B | 120 B |
| SumArray | `sum(values(@))` | 20,576 ns | 4,030 ns | 4,036 ns | 0 B | 0 B |
| NestedSum | Nested `sum(...)` | 53,033 ns | 3,857 ns | 3,846 ns | 0 B | 0 B |
| AvgArray | `avg(values(@))` | 6,764 ns | 1,155 ns | 1,119 ns | 240 B | 240 B |
| MinArray | `min(values(@))` | 10,588 ns | 1,863 ns | 1,830 ns | 120 B | 120 B |
| MaxArray | `max(values(@))` | 10,970 ns | 2,274 ns | 2,125 ns | 120 B | 120 B |
| MinBy | `min_by(people, &age)` | 18,015 ns | 1,591 ns | 1,588 ns | 0 B | 0 B |
| MaxBy | `max_by(people, &age)` | 18,107 ns | 1,866 ns | 2,305 ns | 0 B | 0 B |

All allocations in the RT and CG columns come from `JsonDocumentBuilder` results returned from aggregate functions (`values()`, `avg()`). Navigation, projection, filtering, and arithmetic benchmarks are zero-allocation.

## Quick start

Install the packages:

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.JMESPath
```

**Simplest approach — cloned result, no workspace management:**

```csharp
using Corvus.Text.Json.JMESPath;

JsonElement result = JMESPathEvaluator.Default.Search(
    "locations[?state == 'WA'].name | sort(@) | {WashingtonCities: join(', ', @)}",
    JsonElement.ParseValue("""
    {
      "locations": [
        {"name": "Seattle", "state": "WA"},
        {"name": "New York", "state": "NY"},
        {"name": "Bellevue", "state": "WA"},
        {"name": "Olympia", "state": "WA"}
      ]
    }
    """u8));

Console.WriteLine(result); // {"WashingtonCities":"Bellevue, Olympia, Seattle"}
```

`Search(expression, data)` parses the input, evaluates the expression, and returns a cloned result that you own — no workspace management or disposal needed.

**Full API — zero-allocation evaluation:**

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JMESPath;

// Parse the input data (using statement ensures pooled memory is returned)
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "people": [
        {"first": "James", "last": "d"},
        {"first": "Jacob", "last": "e"},
        {"first": "Jayden", "last": "f"},
        {"missing": "different"}
      ],
      "foo": {"bar": "baz"}
    }
    """u8);

// Create a workspace for zero-allocation evaluation
using JsonWorkspace workspace = JsonWorkspace.Create();

// Evaluate a JMESPath expression
JsonElement result = JMESPathEvaluator.Default.Search(
    "people[*].first",
    dataDoc.RootElement,
    workspace);

Console.WriteLine(result); // ["James","Jacob","Jayden"]
```

The evaluator compiles the expression into a delegate tree on first use and caches it. Subsequent evaluations of the same expression skip compilation entirely. Create one `JMESPathEvaluator` instance and reuse it — `JMESPathEvaluator.Default` provides a shared static instance.

The workspace provides pooled memory for the result — **zero GC allocation** per evaluation for most expressions. The result remains valid until the workspace is disposed or reset.

## Evaluation modes

Three evaluation modes are available. All three produce the same results for the same expression; they differ in when compilation happens and what overhead is incurred at evaluation time.

### Interpreted (runtime evaluation)

Use `JMESPathEvaluator` when expressions are determined at runtime:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
var evaluator = new JMESPathEvaluator();

JsonElement result = evaluator.Search(
    "people[*].first",
    dataDoc.RootElement,
    workspace);
```

Create one `JMESPathEvaluator` instance and reuse it — it caches compiled delegate trees per expression string and is thread-safe. For simple cases, `JMESPathEvaluator.Default` provides a shared static instance.

### Source generator (build-time code generation)

When expressions are known at build time, the source generator eliminates all runtime compilation overhead.

**1. Install the source generator package:**

```bash
dotnet add package Corvus.Text.Json.JMESPath.SourceGenerator
```

**2. Create a `.jmespath` expression file** (e.g. `Expressions/total-price.jmespath`):

```
sum(items[*].price)
```

**3. Declare a partial class with the `[JMESPathExpression]` attribute:**

```csharp
using Corvus.Text.Json.JMESPath;

namespace MyApp.Queries;

[JMESPathExpression("Expressions/total-price.jmespath")]
internal static partial class TotalPrice
{
}
```

**4. Include the expression file and packages in your `.csproj`:**

```xml
<ItemGroup>
  <AdditionalFiles Include="Expressions\total-price.jmespath" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.JMESPath.SourceGenerator"
                    PrivateAssets="all"
                    ReferenceOutputAssembly="false"
                    OutputItemType="Analyzer" />
  <PackageReference Include="Corvus.Text.Json" />
  <PackageReference Include="Corvus.Text.Json.JMESPath" />
</ItemGroup>
```

**5. Call the generated `Evaluate` method:**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = TotalPrice.Evaluate(dataDoc.RootElement, workspace);
```

The generated method is a static method that directly evaluates the expression without delegate dispatch.

**Diagnostic messages:**

| Code | Severity | Description |
|------|----------|-------------|
| JPSG001 | Error | Expression file not found in `AdditionalFiles` |
| JPSG002 | Error | Expression file is empty |
| JPSG003 | Error | Code generation failed (invalid expression or unsupported feature) |

### CLI code generation

The `generatejsonschematypes` CLI tool includes a `jmespath` subcommand for ahead-of-time code generation outside the MSBuild pipeline:

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

```bash
generatejsonschematypes jmespath <expressionFile> \
    --className <ClassName> \
    --namespace <Namespace> \
    [--outputPath <output.cs>]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `<expressionFile>` | Yes | Path to the `.jmespath` expression file |
| `--className` | Yes | Name of the generated static class |
| `--namespace` | Yes | Namespace for the generated class |
| `--outputPath` | No | Output file path (defaults to `<ClassName>.cs`) |

Example:

```bash
generatejsonschematypes jmespath expressions/total-price.jmespath \
    --className TotalPrice \
    --namespace MyApp.Queries \
    --outputPath Generated/TotalPrice.cs
```

This produces a self-contained `.cs` file with the same static `Evaluate` method as the source generator. Use the CLI tool when:

- You need to generate code outside the MSBuild pipeline
- You want to inspect or modify the generated code before committing it
- You are integrating with a non-.NET build system

## How-to guides

### Workspace and memory management

The `JsonWorkspace` provides pooled memory for evaluation results and intermediate values. Using a caller-managed workspace is the recommended pattern for zero-allocation evaluation.

**Single evaluation:**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Search(expression, data, workspace);
// result is valid until workspace is disposed or reset
```

**Batch evaluation — reset the workspace between iterations:**

```csharp
var evaluator = new JMESPathEvaluator();
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (var item in items)
{
    workspace.Reset();
    JsonElement result = evaluator.Search(expression, item, workspace);
    ProcessResult(result);
}
```

This pattern achieves **zero GC allocation** per evaluation for most expressions. The workspace pools memory internally via `ArrayPool<byte>` and reuses it across evaluations.

**Without a workspace (convenience, allocates per call):**

```csharp
// Omit the workspace — the evaluator creates one internally and clones the result
JsonElement result = evaluator.Search(expression, data);
```

This overload creates a workspace internally, evaluates the expression, clones the result into a standalone `JsonElement`, and disposes the workspace. It is convenient for one-off evaluations but allocates on every call.

### Error handling

All evaluation errors throw `JMESPathException` with a descriptive message:

```csharp
try
{
    JsonElement result = evaluator.Search(expression, data, workspace);
}
catch (JMESPathException ex)
{
    Console.WriteLine($"JMESPath error: {ex.Message}");
}
```

Common error scenarios:

| Error | Example | Description |
|-------|---------|-------------|
| Syntax error | `people[*` | Unterminated bracket or invalid syntax |
| Type error | `abs('string')` | Function called with wrong argument type |
| Arity error | `sum(a, b)` | Function called with wrong number of arguments |
| Unknown function | `$custom(x)` | Unrecognised function name |

## Expression reference

JMESPath expressions follow the [JMESPath specification](https://jmespath.org/specification.html). This section summarises the key features with examples.

### Identifiers

Access a property by name:

```
// Expression: a
// Data:       {"a": "foo", "b": "bar", "c": "baz"}
// Result:     "foo"
```

Quoted identifiers allow any string as a property name:

```
// Expression: "with space"
// Data:       {"with space": "value"}
// Result:     "value"
```

### Sub-expressions

Chain property access with `.`:

```
// Expression: a.b.c.d
// Data:       {"a": {"b": {"c": {"d": "value"}}}}
// Result:     "value"
```

If any key along the path does not exist, the result is `null`:

```
// Expression: a.b.c
// Data:       {"a": {"b": {"notc": "value"}}}
// Result:     null
```

### Index expressions

Access array elements by zero-based index. Negative indices count from the end:

```
// Expression: [0]
// Data:       ["a", "b", "c", "d", "e", "f"]
// Result:     "a"

// Expression: [-1]
// Data:       ["a", "b", "c", "d", "e", "f"]
// Result:     "f"
```

### Slicing

Extract a sub-array with `[start:stop:step]`:

```
// Expression: [0:5]
// Data:       [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
// Result:     [0, 1, 2, 3, 4]

// Expression: [5:10]
// Data:       [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
// Result:     [5, 6, 7, 8, 9]

// Expression: [::2]
// Data:       [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
// Result:     [0, 2, 4, 6, 8]

// Expression: [::-1]
// Data:       [0, 1, 2, 3, 4, 5, 6, 7, 8, 9]
// Result:     [9, 8, 7, 6, 5, 4, 3, 2, 1, 0]
```

### Projections

Projections evaluate an expression against each element in a collection.

**List projections** (`[*]`) — project over an array:

```
// Expression: people[*].first
// Data:       {"people": [{"first": "James", "last": "d"},
//                         {"first": "Jacob", "last": "e"},
//                         {"first": "Jayden", "last": "f"},
//                         {"missing": "different"}]}
// Result:     ["James", "Jacob", "Jayden"]
```

Note: `null` values (from missing keys) are excluded from projection results.

**Object projections** (`*`) — project over an object's values:

```
// Expression: ops.*.numArgs
// Data:       {"ops": {"functionA": {"numArgs": 2},
//                      "functionB": {"numArgs": 3},
//                      "functionC": {"variadic": true}}}
// Result:     [2, 3]
```

**Flatten projections** (`[]`) — flatten nested arrays by one level and project:

```
// Expression: reservations[].instances[].state
// Data:       {"reservations": [
//               {"instances": [{"state": "running"}, {"state": "stopped"}]},
//               {"instances": [{"state": "terminated"}, {"state": "running"}]}
//             ]}
// Result:     ["running", "stopped", "terminated", "running"]
```

### Filter expressions

Filter arrays with `[? <expression>]`:

```
// Expression: machines[?state == 'running'].name
// Data:       {"machines": [
//               {"name": "a", "state": "running"},
//               {"name": "b", "state": "stopped"},
//               {"name": "c", "state": "running"}
//             ]}
// Result:     ["a", "c"]
```

Comparison operators: `==`, `!=`, `<`, `<=`, `>`, `>=`.

Logical operators: `&&`, `||`, `!`.

### Pipe expressions

The pipe operator (`|`) stops the current projection and passes the full result to the right-hand side:

```
// Expression: people[*].first | [0]
// Data:       {"people": [{"first": "James"}, {"first": "Jacob"}, {"first": "Jayden"}]}
// Result:     "James"
```

Without the pipe, `[0]` would be applied to each element of the projection (always returning the first character of each name). The pipe collapses the projection first, then applies `[0]` to the resulting array.

### Multiselect

**Multiselect list** — create an array from multiple expressions:

```
// Expression: people[].[name, state.name]
// Data:       {"people": [
//               {"name": "a", "state": {"name": "WA"}},
//               {"name": "b", "state": {"name": "NY"}},
//               {"name": "c", "state": {"name": "WA"}}
//             ]}
// Result:     [["a","WA"],["b","NY"],["c","WA"]]
```

**Multiselect hash** — create an object from multiple expressions:

```
// Expression: people[*].{Name: name, State: state.name}
// Data:       {"people": [
//               {"name": "a", "state": {"name": "WA"}},
//               {"name": "b", "state": {"name": "NY"}}
//             ]}
// Result:     [{"Name":"a","State":"WA"},{"Name":"b","State":"NY"}]
```

### Literal expressions

Use backtick-quoted JSON for literal values in expressions:

```
// Expression: people[*].name | contains(@, `James`)
// Data:       {"people": [{"name": "James"}, {"name": "Jacob"}]}
// Result:     true
```

### Current node (`@`)

The `@` token refers to the current node — useful inside functions and filter expressions:

```
// Expression: people[*].name | sort(@)
// Data:       {"people": [{"name": "b"}, {"name": "a"}, {"name": "c"}]}
// Result:     ["a", "b", "c"]
```

## Built-in functions

The implementation supports the full set of [JMESPath built-in functions](https://jmespath.org/specification.html#built-in-functions):

### Numeric functions

| Function | Description |
|----------|-------------|
| `abs(n)` | Absolute value |
| `ceil(n)` | Ceiling (round up) |
| `floor(n)` | Floor (round down) |

### Aggregate functions

| Function | Description |
|----------|-------------|
| `avg(array)` | Average of numeric values |
| `max(array)` | Maximum value (numbers or strings) |
| `min(array)` | Minimum value (numbers or strings) |
| `sum(array)` | Sum of numeric values |

### String functions

| Function | Description |
|----------|-------------|
| `contains(subject, search)` | Test if string contains substring, or array contains element |
| `ends_with(str, suffix)` | Test if string ends with suffix |
| `join(separator, array)` | Join array of strings with separator |
| `length(subject)` | Length of string, array, or object |
| `starts_with(str, prefix)` | Test if string starts with prefix |

### Type functions

| Function | Description |
|----------|-------------|
| `to_array(arg)` | Convert to array (wraps non-arrays in single-element array) |
| `to_number(arg)` | Convert to number |
| `to_string(arg)` | Convert to string |
| `type(arg)` | Returns the type name (`"string"`, `"number"`, `"boolean"`, `"array"`, `"object"`, `"null"`) |

### Collection functions

| Function | Description |
|----------|-------------|
| `keys(obj)` | Object keys as an array |
| `values(obj)` | Object values as an array |
| `merge(obj1, obj2, ...)` | Merge objects (later keys win) |
| `not_null(arg1, arg2, ...)` | Returns the first non-null argument |
| `reverse(array_or_string)` | Reverse an array or string |
| `sort(array)` | Sort an array of numbers or strings |

### Expression-argument functions

These functions take an expression reference (`&expr`) as an argument:

| Function | Description |
|----------|-------------|
| `map(&expr, array)` | Apply expression to each element |
| `max_by(array, &expr)` | Maximum element by expression key |
| `min_by(array, &expr)` | Minimum element by expression key |
| `sort_by(array, &expr)` | Sort array by expression key |

Example:

```
// Expression: max_by(people, &age).name
// Data:       {"people": [{"name": "a", "age": 20}, {"name": "b", "age": 30}, {"name": "c", "age": 10}]}
// Result:     "b"
```

## Common pitfalls

### Always dispose `ParsedJsonDocument`

`ParsedJsonDocument<T>` rents memory from `ArrayPool<byte>`. Forgetting to dispose it leaks pooled memory:

```csharp
// ❌ BAD — leaks pooled memory
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Search(expression, doc.RootElement, workspace);

// ✅ GOOD — using statement returns memory to the pool
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Search(expression, doc.RootElement, workspace);
```

### Reset the workspace in loops

Without `Reset()`, workspace memory grows with each evaluation. The workspace remains valid, but old results consume pooled memory unnecessarily:

```csharp
// ❌ BAD — workspace grows unboundedly
foreach (var item in items)
{
    var result = evaluator.Search(expression, item, workspace);
    ProcessResult(result);
}

// ✅ GOOD — reset frees previous results
foreach (var item in items)
{
    workspace.Reset();
    var result = evaluator.Search(expression, item, workspace);
    ProcessResult(result);
}
```

### Don't forget `AdditionalFiles` for the source generator

The source generator reads expression files from `AdditionalFiles`. Without the MSBuild item, the generator can't find the expression and produces diagnostic `JPSG001`:

```xml
<!-- ❌ Missing — generator produces JPSG001 -->
<ItemGroup>
  <None Include="Expressions\query.jmespath" />
</ItemGroup>

<!-- ✅ Correct -->
<ItemGroup>
  <AdditionalFiles Include="Expressions\query.jmespath" />
</ItemGroup>
```

### Result lifetime is tied to the workspace

When you pass a workspace, the returned `JsonElement` is backed by that workspace's memory. Using the result after the workspace is disposed or reset produces undefined behavior:

```csharp
// ❌ BAD — result is invalid after workspace disposal
JsonElement result;
using (JsonWorkspace workspace = JsonWorkspace.Create())
{
    result = evaluator.Search(expression, data, workspace);
}
Console.WriteLine(result.GetRawText()); // undefined behavior

// ✅ GOOD — use result before workspace is disposed
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Search(expression, data, workspace);
Console.WriteLine(result.GetRawText()); // safe
```

## Comparison with other libraries

| Feature | Corvus.Text.Json.JMESPath | JmesPath.Net |
|---------|--------------------------|--------------|
| Evaluation model | Compiled delegate tree (cached) | Direct interpretation |
| Code generation | Source generator + CLI tool | Not available |
| JSON document model | `Corvus.Text.Json` (pooled, zero-copy, over `System.Text.Json`) | `Newtonsoft.Json` |
| Memory model | Pooled (`JsonWorkspace`, `ArrayPool`) | GC-allocated |
| Zero-allocation hot path | Yes (with workspace) | No |
| Conformance (official suite) | 892 / 892 (100%) | 100% |
| .NET Framework support | net9.0+, netstandard2.0/2.1 | net6.0+ |
| Pipe fusion optimization | Yes (multi-stage pipe chains fused into single pass) | No |
