# JSONata Query and Transformation Language

## Overview

`Corvus.Text.Json.Jsonata` implements [JSONata](https://jsonata.org/) for the Corvus.Text.Json document model — a high-performance query and transformation language that evaluates JSONata expressions against JSON data with zero-allocation evaluation, compiled delegate trees, and pooled workspace memory.

[JSONata](https://jsonata.org/) is an expressive, Turing-complete functional query and transformation language for JSON. It supports path navigation, predicate filtering, higher-order functions (`$map`, `$filter`, `$reduce`, `$sort`), object construction, string manipulation, arithmetic, regular expressions, and user-defined functions. The Corvus implementation passes **all 1,665** tests in the official [JSONata test suite](https://github.com/jsonata-org/jsonata) (100% conformance). Two additional test cases in the upstream suite contain lone UTF-16 surrogates (`\uD800`) that are not representable in .NET's `System.String`; these are detected and gracefully skipped at the test-harness level (see [Conformance](#conformance)).

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Expressions are dynamic, determined at runtime | `Corvus.Text.Json.Jsonata` |
| **Source generator** | Expressions are known at build time, embedded in your project | `Corvus.Text.Json.Jsonata.SourceGenerator` |
| **CLI code generation** | Expressions are known ahead of time, generated outside the build | `Corvus.Json.CodeGenerator` (the `jsonata` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch. Benchmarks show the interpreted evaluator is **up to 3.5× faster** than [Jsonata.Net.Native](https://github.com/nicoleaudia/jsonata.net.native) (the .NET reference implementation) with **90–100% less memory allocation**, and code-generated evaluators are faster still.

## Conformance

![JSONata Runtime Conformance](https://img.shields.io/badge/JSONata_Runtime-1665%2F1665_(100%25)-brightgreen)
![JSONata CodeGen Conformance](https://img.shields.io/badge/JSONata_CodeGen-1665%2F1665_(100%25)-brightgreen)

Both the runtime evaluator and the code generation pipeline pass all **1,665** official JSONata test suite cases (100% conformance) on .NET 10.0. The runtime evaluator also passes on .NET Framework 4.8.1.

The code generation pipeline compiles each expression to optimized static C#. Tests that require runtime-only features (variable bindings, custom recursion depth, execution timeout) are validated through the 5-parameter overload which delegates to the runtime evaluator.

> **Note on surrogate edge cases:** The upstream test suite contains two additional cases (`function-encodeUrl/case002` and `function-encodeUrlComponent/case002`) whose expression strings include a lone UTF-16 surrogate (`\uD800`). These are not representable in .NET's `System.String` — the JSON parser cannot load the test data. JavaScript engines (where jsonata-js runs) permit lone surrogates in strings, so these tests are valid on that platform. The test harness detects this at load time and gracefully skips both cases.

## Installation

### Interpreted mode (runtime evaluation)

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.Jsonata
```

### Source generator (build-time code generation)

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.Jsonata
dotnet add package Corvus.Text.Json.Jsonata.SourceGenerator
```

The source generator package is a development dependency — it runs at build time and produces C# source; the generated code depends on `Corvus.Text.Json` and `Corvus.Text.Json.Jsonata` at runtime.

### CLI tool

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

The `generatejsonschematypes` tool includes a `jsonata` subcommand. See [CLI code generation](#cli-code-generation) below.

## Interpreted evaluation

Create a `JsonataEvaluator`, pass it an expression string, JSON data, and a `JsonWorkspace`:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

// Parse the input data
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "FirstName": "Fred",
      "Surname": "Smith",
      "Age": 28
    }
    """u8);

// Evaluate a JSONata expression
using JsonWorkspace workspace = JsonWorkspace.Create();
var evaluator = new JsonataEvaluator();
JsonElement result = evaluator.Evaluate(
    "FirstName & ' ' & Surname",
    dataDoc.RootElement,
    workspace);

// result is a string element: "Fred Smith"
Console.WriteLine(result); // "Fred Smith"
```

The evaluator compiles the expression into a delegate tree on first use and caches it. Subsequent evaluations of the same expression skip compilation entirely.

When you provide a workspace, the result element is backed by the workspace's pooled memory — **zero GC allocation** per evaluation for most expressions. The result remains valid until the workspace is disposed or reset.

> You can omit the workspace parameter; the evaluator will create one internally and return a cloned, self-contained result. This is convenient but allocates on every call.

### Variable bindings, recursion depth, and timeouts

The 5-parameter overload supports external variable bindings, custom recursion depth, and execution timeouts:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// External variable bindings
var bindings = new Dictionary<string, JsonElement>
{
    ["threshold"] = JsonElement.ParseValue("100"u8),
};

JsonElement result = evaluator.Evaluate(
    "$filter(items, function($v) { $v.price > $threshold })",
    dataDoc.RootElement,
    workspace,
    bindings: bindings,
    maxDepth: 500,       // recursion depth limit (default: 500)
    timeLimitMs: 5000);  // timeout in milliseconds (default: 0 = no limit)
```

## Source generator

For expressions known at build time, the source generator eliminates all runtime compilation overhead. Declare a partial class annotated with `[JsonataExpression]`, pointing at a `.jsonata` expression file:

```csharp
using Corvus.Text.Json.Jsonata;

namespace MyApp.Transforms;

[JsonataExpression("Expressions/employee-transform.jsonata")]
internal static partial class EmployeeTransform
{
}
```

Create the expression file (e.g. `Expressions/employee-transform.jsonata`):

```jsonata
{
  'name': FirstName & ' ' & Surname,
  'mobile': Contact.Phone[type = 'mobile'].number
}
```

Include the expression file as an `AdditionalFiles` item in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Expressions\employee-transform.jsonata" />
</ItemGroup>
```

And reference the source generator:

```xml
<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.Jsonata.SourceGenerator"
                    PrivateAssets="all"
                    ReferenceOutputAssembly="false"
                    OutputItemType="Analyzer" />
  <PackageReference Include="Corvus.Text.Json" />
  <PackageReference Include="Corvus.Text.Json.Jsonata" />
</ItemGroup>
```

At build time, the generator reads the `.jsonata` file and emits an `Evaluate` method on the partial class:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "FirstName": "Fred",
      "Surname": "Smith",
      "Contact": {
        "Phone": [
          {"type": "home", "number": "0203 544 1234"},
          {"type": "mobile", "number": "077 7700 1234"}
        ]
      }
    }
    """u8);

using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = EmployeeTransform.Evaluate(dataDoc.RootElement, workspace);
```

The generated `Evaluate` method is a static method that directly evaluates the expression without delegate dispatch.

### Diagnostic messages

| Code | Severity | Description |
|------|----------|-------------|
| JASG001 | Error | Expression file not found in `AdditionalFiles` |
| JASG002 | Error | Expression file is empty |
| JASG003 | Error | Code generation failed (invalid expression or unsupported feature) |

## CLI code generation

The `generatejsonschematypes` CLI tool includes a `jsonata` subcommand for ahead-of-time code generation:

```bash
generatejsonschematypes jsonata <expressionFile> \
    --className <ClassName> \
    --namespace <Namespace> \
    [--outputPath <output.cs>]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `<expressionFile>` | Yes | Path to the `.jsonata` expression file |
| `--className` | Yes | Name of the generated static class |
| `--namespace` | Yes | Namespace for the generated class |
| `--outputPath` | No | Output file path (defaults to `<ClassName>.cs`) |

Example:

```bash
generatejsonschematypes jsonata expressions/employee-transform.jsonata \
    --className EmployeeTransform \
    --namespace MyApp.Transforms \
    --outputPath Generated/EmployeeTransform.cs
```

This produces a self-contained `.cs` file with a static `Evaluate` method, identical to what the source generator produces. Use the CLI tool when:

- You need to generate code outside the MSBuild pipeline
- You want to inspect or modify the generated code before committing it
- You are integrating with a non-.NET build system

## Supported functions

The implementation supports the full set of [JSONata built-in functions](https://docs.jsonata.org/overview):

### String functions

| Function | Description |
|----------|-------------|
| `$string(arg)` | Convert to string |
| `$length(str)` | String length |
| `$substring(str, start[, length])` | Extract substring |
| `$substringBefore(str, chars)` | Substring before first occurrence |
| `$substringAfter(str, chars)` | Substring after first occurrence |
| `$uppercase(str)` | Convert to uppercase |
| `$lowercase(str)` | Convert to lowercase |
| `$trim(str)` | Trim whitespace |
| `$pad(str, width[, char])` | Pad string |
| `$contains(str, pattern)` | Test for substring/regex match |
| `$split(str, separator[, limit])` | Split string |
| `$join(array[, separator])` | Join array elements |
| `$match(str, pattern[, limit])` | Regex match |
| `$replace(str, pattern, replacement[, limit])` | Regex/string replace |
| `$eval(expr[, context])` | Evaluate string as JSONata |
| `$base64encode(str)` | Base64 encode |
| `$base64decode(str)` | Base64 decode |
| `$encodeUrlComponent(str)` | URL-encode |
| `$encodeUrl(str)` | URL-encode (full URL) |
| `$decodeUrlComponent(str)` | URL-decode |
| `$decodeUrl(str)` | URL-decode (full URL) |

### Numeric functions

| Function | Description |
|----------|-------------|
| `$number(arg)` | Convert to number |
| `$abs(n)` | Absolute value |
| `$floor(n)` | Floor |
| `$ceil(n)` | Ceiling |
| `$round(n[, precision])` | Round |
| `$power(base, exp)` | Exponentiation |
| `$sqrt(n)` | Square root |
| `$random()` | Random number [0, 1) |
| `$formatNumber(n, picture[, options])` | Format number with picture string |
| `$formatBase(n, radix)` | Format number in given radix |
| `$formatInteger(n, picture)` | Format integer with picture string |
| `$parseInteger(str, picture)` | Parse formatted integer |

### Aggregate functions

| Function | Description |
|----------|-------------|
| `$sum(array)` | Sum of numeric values |
| `$max(array)` | Maximum value |
| `$min(array)` | Minimum value |
| `$average(array)` | Average value |

### Boolean functions

| Function | Description |
|----------|-------------|
| `$boolean(arg)` | Convert to boolean |
| `$not(arg)` | Logical NOT |
| `$exists(arg)` | Test if value exists |

### Array functions

| Function | Description |
|----------|-------------|
| `$count(array)` | Array length |
| `$append(arr1, arr2)` | Concatenate arrays |
| `$sort(array[, comparator])` | Sort array |
| `$reverse(array)` | Reverse array |
| `$shuffle(array)` | Randomly shuffle array |
| `$distinct(array)` | Remove duplicates |
| `$zip(arr1, arr2, ...)` | Zip arrays |

### Object functions

| Function | Description |
|----------|-------------|
| `$keys(obj)` | Object keys |
| `$values(obj)` | Object values |
| `$spread(obj)` | Spread object into array of single-key objects |
| `$merge(arr)` | Merge array of objects |
| `$sift(obj, func)` | Filter object properties |
| `$each(obj, func)` | Apply function to each property |
| `$type(value)` | Type of value |
| `$lookup(obj, key)` | Property lookup by key |

### Higher-order functions

| Function | Description |
|----------|-------------|
| `$map(array, func)` | Transform each element |
| `$filter(array, func)` | Filter elements |
| `$reduce(array, func[, init])` | Reduce to single value |
| `$sort(array, func)` | Sort with comparator |
| `$sift(obj, func)` | Filter object properties |
| `$each(obj, func)` | Apply to each property |

### Date/time functions

| Function | Description |
|----------|-------------|
| `$now([picture[, timezone]])` | Current timestamp |
| `$millis()` | Current time in milliseconds |
| `$fromMillis(ms[, picture[, timezone]])` | Convert milliseconds to timestamp |
| `$toMillis(str[, picture])` | Convert timestamp to milliseconds |

### Language features

In addition to built-in functions, JSONata supports:

| Feature | Example | Description |
|---------|---------|-------------|
| Path navigation | `Account.Order.Product.Price` | Dot-separated property access with automatic array flattening |
| Predicate filtering | `Phone[type = 'mobile']` | Filter arrays by condition |
| Wildcards | `Address.*` | All properties of an object |
| Descendant operator | `Account..Price` | Recursive descent |
| Parent operator | `Account.Order.Product.%.OrderID` | Navigate to parent context |
| Array constructors | `[1, 2, 3]` | Construct arrays |
| Object constructors | `{"name": FirstName}` | Construct objects |
| String concatenation | `FirstName & ' ' & Surname` | `&` operator |
| Conditional | `x > 0 ? "positive" : "non-positive"` | Ternary expression |
| Lambda functions | `function($x) { $x * 2 }` | User-defined functions |
| Variable binding | `($x := 5; $x * 2)` | Block-scoped variables |
| Regular expressions | `/pattern/flags` | Regex literals in `$match`, `$replace`, `$split`, `$contains` |
| Range operator | `[1..10]` | Numeric ranges |
| Order-by | `Account.Order^(OrderID)` | Sort expression results |

## Workspace and memory management

The `JsonataEvaluator` is designed for high-throughput scenarios. Key performance patterns:

### Evaluator reuse

Create one evaluator and reuse it across evaluations. The evaluator caches compiled delegate trees per expression string. Provide a workspace and reset it between iterations for zero-allocation evaluation:

```csharp
// Create once, reuse many times
var evaluator = new JsonataEvaluator();
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (var item in items)
{
    workspace.Reset();
    JsonElement result = evaluator.Evaluate(expression, item, workspace);
    ProcessResult(result);
}
```

This pattern achieves **zero GC allocation** per evaluation for most expressions. The workspace pools memory internally via `ArrayPool<byte>` and reuses it across evaluations.

### Code-generated evaluation

For expressions known at compile time, the source generator or CLI tool produces a static `Evaluate` method that bypasses the evaluator entirely:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (var item in items)
{
    workspace.Reset();
    JsonElement result = MyExpression.Evaluate(item, workspace);
    ProcessResult(result);
}
```

## Comparison with other libraries

The Corvus JSONata implementation is designed for high-throughput scenarios where expressions are evaluated millions of times. Key differences from the existing .NET implementation:

| Feature | Corvus.Text.Json.Jsonata | Jsonata.Net.Native |
|---------|--------------------------|-------------------|
| Evaluation model | Compiled delegate tree (cached) | Direct interpretation |
| Code generation | Source generator + CLI tool | Not available |
| JSON document model | `Corvus.Text.Json` (zero-copy, pooled) | Custom `JToken`, `System.Text.Json`, Newtonsoft |
| Memory model | Pooled (`JsonWorkspace`, `ArrayPool`) | GC-allocated |
| Zero-allocation hot path | Yes (with workspace) | No |
| Conformance (official suite) | 1,665 / 1,665 (100%) | 1,370 passed, 287 skipped ([badge](https://github.com/mikhail-barg/jsonata.net.native)) |
| .NET Framework support | net9.0+, netstandard2.0/2.1 | net6.0+ |
| Variable bindings | Supported | Supported |
| Custom functions | Via bindings | Via API |
| Recursion depth limit | Configurable (default 500) | Not configurable |
| Execution timeout | Configurable (milliseconds) | Not available |

### Benchmark summary

Measured on .NET 10.0 (13th Gen Intel Core i7-13800H) across 20 representative scenarios covering property navigation, arithmetic, string operations, higher-order functions, predicate filtering, and object construction. Each benchmark compares three implementations: `Corvus` (interpreted), `CodeGen` (source-generated), and `Jsonata.Net.Native` (reference .NET implementation, v3.0.0).

All Runtime benchmarks use caller-managed `JsonWorkspace` for zero-allocation evaluation. Jsonata.Net.Native uses pre-compiled `JsonataQuery` objects with pre-parsed `JToken` data. The "Alloc" columns show per-invocation GC allocation.

#### Employee transform (reference benchmark)

This benchmark replicates the [Jsonata.Net.Native benchmark scenario](https://github.com/nicoleaudia/jsonata.net.native) — a multi-step expression with property navigation, string concatenation, and array predicate filtering against a real-world employee dataset:

```jsonata
{
  'name': Employee.FirstName & ' ' & Employee.Surname,
  'mobile': Contact.Phone[type = 'mobile'].number
}
```

**Cached evaluation** (expression pre-compiled):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Runtime (interpreted) | 1,180 ns | 960 B |
| Runtime (code-gen) | 1,183 ns | 240 B |
| Jsonata.Net.Native | 2,067 ns | 9,920 B |

The runtime evaluator is **1.8× faster** with **90% less allocation** than the reference implementation. Code-gen reduces allocation to 240 B (98% reduction).

**Cold start** (fresh compile + evaluate):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Runtime (interpreted) | 6,964 ns | 10,800 B |
| Jsonata.Net.Native | 6,703 ns | 19,264 B |

Cold start is comparable in speed, but Corvus allocates 44% less memory.

#### Detailed results

##### Property navigation

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Deep path | `Account.Order.Product.Price` | 764 ns | 736 ns | 654 ns | 120 B | 120 B | 1,816 B |
| Quoted property | `` Account.`Account Name` `` | 76 ns | 59 ns | 258 ns | 0 B | 0 B | 1,024 B |
| Array index | `Account.Order[0].OrderID` | 103 ns | 94 ns | 370 ns | 0 B | 0 B | 1,408 B |

The runtime evaluator is **3–4× faster** for simple property access and array indexing, with zero allocation. Deep path traversal through nested arrays is comparable in speed but uses **93% less memory**.

##### Arithmetic

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Sum-product | `$sum(Account.Order.Product.(Price * Quantity))` | 1,261 ns | 424 ns | 1,424 ns | 216 B | 0 B | 6,480 B |
| Map arithmetic | `Account.Order.Product.(Price * Quantity)` | 1,149 ns | 817 ns | 1,174 ns | 184 B | 120 B | 5,536 B |
| Pure arithmetic | `1 + 2 * 3 - 4 / 2 + 10 % 3` | 186 ns | 58 ns | 250 ns | 0 B | 0 B | 1,352 B |

Code-gen achieves **3–4× speedup** over the reference implementation for arithmetic with **zero allocation**. Pure arithmetic shows the largest CG win (4.3× faster than interpreted) because computation dominates over data traversal.

##### String operations

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple concat | `FirstName & ' ' & Surname` | 416 ns | 220 ns | 382 ns | 0 B | 0 B | 1,408 B |
| Concat + number | `FirstName & ' ' & Surname & ', age ' & $string(Age)` | 1,139 ns | 428 ns | 1,069 ns | 32 B | 0 B | 3,136 B |
| Join array | `$join([Address.Street, Address.City, Address.Postcode], ', ')` | 860 ns | 281 ns | 1,518 ns | 120 B | 0 B | 4,040 B |

Code-gen is **2–5× faster** than the reference implementation for string operations with zero allocation.

##### Higher-order functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $map | `$map(Account.Order.Product, function($v) { ... })` | 2,069 ns | 1,362 ns | 2,069 ns | 1,184 B | 120 B | 6,768 B |
| $filter | `$filter(Account.Order.Product, function($v) { ... })` | 2,197 ns | 1,477 ns | 2,647 ns | 1,184 B | 120 B | 7,632 B |
| $reduce | `$reduce(Account.Order.Product, function($prev, $curr) { ... }, 0)` | 3,158 ns | 2,985 ns | 3,623 ns | 2,528 B | 120 B | 10,120 B |
| $sort | `$sort(Account.Order.Product, function($a, $b) { ... })` | 3,646 ns | 2,395 ns | 3,517 ns | 1,576 B | 240 B | 9,992 B |

Higher-order functions show **75–98% less allocation** than the reference implementation. Code-gen eliminates most allocation (120–240 B vs 1,184–2,528 B for interpreted).

##### Predicate filtering

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Single predicate | `Contact.Phone[type = 'mobile'].number` | 980 ns | 976 ns | 1,888 ns | 120 B | 120 B | 5,760 B |
| Chained predicate | `Contact[ssn = '496913021'].Phone[0].number` | 259 ns | 218 ns | 903 ns | 0 B | 0 B | 3,072 B |
| Compound predicate | `Contact.Phone[type = 'office' or type = 'mobile'].number` | 1,253 ns | 1,153 ns | 3,554 ns | 120 B | 120 B | 10,376 B |

Predicate filtering shows the largest speedup: the runtime evaluator is **2–3.5× faster** than the reference implementation with **97–100% less allocation**.

##### Object construction

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple object | `{"name": Account.`Account Name`, "total": $sum(...)}` | 2,439 ns | 990 ns | 2,926 ns | 336 B | 120 B | 7,840 B |
| Group-by object | `` Account.Order.Product.{`Product Name`: Price} `` | 3,095 ns | 1,054 ns | 2,051 ns | 632 B | 120 B | 7,104 B |
| Array of objects | `[Account.Order.Product.{"name": ..., "total": ...}]` | 2,277 ns | 1,990 ns | 3,104 ns | 120 B | 120 B | 9,664 B |

Code-gen achieves **2–3× speedup** for object construction with **98–99% less allocation**.

> *Benchmarks run with BenchmarkDotNet v0.15.8 on .NET 10.0.5, 13th Gen Intel Core i7-13800H, Windows 11. All Runtime benchmarks use `JsonWorkspace` for pooled evaluation. Jsonata.Net.Native v3.0.0 uses pre-compiled `JsonataQuery` with pre-parsed `JToken` data. OutlierMode=RemoveAll, RunStrategy=Throughput.*
