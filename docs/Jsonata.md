# JSONata Query and Transformation Language

> **[Try the JSONata Playground](/playground-jsonata/)** — evaluate JSONata expressions in your browser using the Corvus interpreted runtime.

## Overview

`Corvus.Text.Json.Jsonata` implements [JSONata](https://jsonata.org/) for the Corvus.Text.Json document model — a high-performance query and transformation language that evaluates JSONata expressions against JSON data with zero-allocation evaluation, compiled delegate trees, and pooled workspace memory.

[JSONata](https://jsonata.org/) is an expressive, Turing-complete functional query and transformation language for JSON. It supports path navigation, predicate filtering, higher-order functions (`$map`, `$filter`, `$reduce`, `$sort`), object construction, string manipulation, arithmetic, regular expressions, and user-defined functions. The Corvus implementation passes **all 1,665** tests in the official [JSONata test suite](https://github.com/jsonata-js/jsonata) (100% conformance). Two additional test cases in the upstream suite contain lone UTF-16 surrogates (`\uD800`) that are not representable in .NET's `System.String`; these are detected and gracefully skipped at the test-harness level (see [Conformance](#conformance)).

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Expressions are dynamic, determined at runtime | `Corvus.Text.Json.Jsonata` |
| **Source generator** | Expressions are known at build time, embedded in your project | `Corvus.Text.Json.Jsonata.SourceGenerator` |
| **CLI code generation** | Expressions are known ahead of time, generated outside the build | `Corvus.Json.CodeGenerator` (the `jsonata` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch. Benchmarks show the interpreted evaluator is **up to 4× faster** than [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) (the .NET reference implementation) with **90–100% less memory allocation**, and code-generated evaluators can be **up to 7× faster**.

## Conformance

![JSONata Runtime Conformance](https://img.shields.io/badge/JSONata_Runtime-1665%2F1665_(100%25)-brightgreen)
![JSONata CodeGen Conformance](https://img.shields.io/badge/JSONata_CodeGen-1665%2F1665_(100%25)-brightgreen)

Both the runtime evaluator and the code generation pipeline pass all **1,665** official JSONata test suite cases (100% conformance) on .NET 10.0. The runtime evaluator also passes on .NET Framework 4.8.1.

The code generation pipeline compiles each expression to optimized static C#. Tests that require runtime-only features (variable bindings, custom recursion depth, execution timeout) are validated through the 5-parameter overload which delegates to the runtime evaluator.

> **Note on surrogate edge cases:** The upstream test suite contains two additional cases (`function-encodeUrl/case002` and `function-encodeUrlComponent/case002`) whose expression strings include a lone UTF-16 surrogate (`\uD800`). These are not representable in .NET's `System.String` — the JSON parser cannot load the test data. JavaScript engines (where jsonata-js runs) permit lone surrogates in strings, so these tests are valid on that platform. The test harness detects this at load time and gracefully skips both cases.

## Quick start

Install the packages:

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.Jsonata
```

Evaluate an expression:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

// Parse the input data
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """
    {
      "FirstName": "Fred",
      "Surname": "Smith",
      "Age": 28,
      "Address": { "City": "London" }
    }
    """u8);

// Create a workspace for zero-allocation evaluation
using JsonWorkspace workspace = JsonWorkspace.Create();

// Evaluate a JSONata expression
JsonElement result = JsonataEvaluator.Default.Evaluate(
    "FirstName & ' ' & Surname",
    dataDoc.RootElement,
    workspace);

Console.WriteLine(result); // "Fred Smith"
```

The evaluator compiles the expression into a delegate tree on first use and caches it. Subsequent evaluations of the same expression skip compilation entirely.

The workspace provides pooled memory for the result — **zero GC allocation** per evaluation for most expressions. The result remains valid until the workspace is disposed or reset.

## Evaluation modes

Three evaluation modes are available. All three produce the same results for the same expression; they differ in when compilation happens and what overhead is incurred at evaluation time.

### Interpreted (runtime evaluation)

Use `JsonataEvaluator` when expressions are determined at runtime:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
var evaluator = new JsonataEvaluator();

JsonElement result = evaluator.Evaluate(
    "Account.Order.Product.Price",
    dataDoc.RootElement,
    workspace);
```

Create one `JsonataEvaluator` instance and reuse it — it caches compiled delegate trees per expression string and is thread-safe. For simple cases, `JsonataEvaluator.Default` provides a shared static instance.

### Source generator (build-time code generation)

When expressions are known at build time, the source generator eliminates all runtime compilation overhead.

**1. Install the source generator package:**

```bash
dotnet add package Corvus.Text.Json.Jsonata.SourceGenerator
```

**2. Create a `.jsonata` expression file** (e.g. `Expressions/employee-transform.jsonata`):

```jsonata
{
  'name': FirstName & ' ' & Surname,
  'mobile': Contact.Phone[type = 'mobile'].number
}
```

**3. Declare a partial class with the `[JsonataExpression]` attribute:**

```csharp
using Corvus.Text.Json.Jsonata;

namespace MyApp.Transforms;

[JsonataExpression("Expressions/employee-transform.jsonata")]
internal static partial class EmployeeTransform
{
}
```

**4. Include the expression file and packages in your `.csproj`:**

```xml
<ItemGroup>
  <AdditionalFiles Include="Expressions\employee-transform.jsonata" />
</ItemGroup>

<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.Jsonata.SourceGenerator"
                    PrivateAssets="all"
                    ReferenceOutputAssembly="false"
                    OutputItemType="Analyzer" />
  <PackageReference Include="Corvus.Text.Json" />
  <PackageReference Include="Corvus.Text.Json.Jsonata" />
</ItemGroup>
```

**5. Call the generated `Evaluate` method:**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = EmployeeTransform.Evaluate(dataDoc.RootElement, workspace);
```

The generated method is a static method that directly evaluates the expression without delegate dispatch. For expressions that use external variables (see [Variable bindings](#variable-bindings)), the generator also emits overloads that accept `bindings`, `maxDepth`, and `timeLimitMs` parameters — identical to the interpreted API.

**Diagnostic messages:**

| Code | Severity | Description |
|------|----------|-------------|
| JASG001 | Error | Expression file not found in `AdditionalFiles` |
| JASG002 | Error | Expression file is empty |
| JASG003 | Error | Code generation failed (invalid expression or unsupported feature) |

### CLI code generation

The `generatejsonschematypes` CLI tool includes a `jsonata` subcommand for ahead-of-time code generation outside the MSBuild pipeline:

```bash
dotnet tool install --global Corvus.Json.CodeGenerator
```

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

This produces a self-contained `.cs` file with the same static `Evaluate` methods as the source generator. Use the CLI tool when:

- You need to generate code outside the MSBuild pipeline
- You want to inspect or modify the generated code before committing it
- You are integrating with a non-.NET build system

## How-to guides

### Variable bindings

Pass external variables into an expression using the `bindings` parameter. Variables are referenced in expressions with the `$` prefix.

**Using `JsonataBinding` (recommended):**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

var bindings = new Dictionary<string, JsonataBinding>
{
    ["threshold"] = JsonataBinding.FromValue(100.0),    // double — stored inline, zero allocation
    ["label"]     = JsonataBinding.FromValue("High"),    // string
    ["active"]    = JsonataBinding.FromValue(true),      // bool — pre-cached singleton
};

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "$filter(items, function($v) { $v.price > $threshold })",
    dataDoc.RootElement,
    workspace,
    bindings);
```

`JsonataBinding.FromValue()` has overloads for `JsonElement`, `double`, `string`, and `bool`. The primitive overloads avoid allocating a `JsonElement` until the value is actually consumed by the expression. Implicit conversions from `JsonElement`, `double`, and `bool` are also available:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    ["rate"]  = 0.15,                                     // implicit double → JsonataBinding
    ["debug"] = true,                                     // implicit bool → JsonataBinding
    ["config"] = JsonElement.ParseValue("""{"a":1}"""u8), // implicit JsonElement → JsonataBinding
};
```

**Using `JsonElement` dictionary (legacy):**

```csharp
var bindings = new Dictionary<string, JsonElement>
{
    ["threshold"] = JsonElement.ParseValue("100"u8),
    ["label"] = JsonElement.ParseValue("\"High value\""u8),
};

JsonElement result = JsonataEvaluator.Default.Evaluate(
    "$filter(items, function($v) { $v.price > $threshold })",
    dataDoc.RootElement,
    workspace,
    bindings: bindings);
```

The `IReadOnlyDictionary<string, JsonElement>` overload is retained for backwards compatibility but cannot express function bindings. Prefer the `JsonataBinding` overload for new code.

### Custom functions

JSONata has first-class support for user-defined functions. You can define functions directly in the expression, bind external C# functions from interpreted mode, or provide compiled C# functions for the code generator via `.jfn` files.

#### Functions defined in the expression

Use JSONata's `function` keyword to define functions inline or via variable binding blocks:

**Inline lambda:**

```jsonata
$map(items, function($v) { $v.price * 1.2 })
```

**Named function via variable binding in the expression:**

```jsonata
(
  $discount := function($price, $pct) { $price * (1 - $pct / 100) };
  $map(items, function($v) { $discount($v.price, 10) })
)
```

**Recursive functions:**

```jsonata
(
  $factorial := function($n) { $n <= 1 ? 1 : $n * $factorial($n - 1) };
  $factorial(10)
)
```

**Higher-order functions** — all of the standard JSONata higher-order functions are supported:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// $filter with an inline predicate
JsonElement expensive = JsonataEvaluator.Default.Evaluate(
    "$filter(Account.Order.Product, function($v) { $v.Price > 50 })",
    dataDoc.RootElement,
    workspace);

// $reduce to sum prices
workspace.Reset();
JsonElement total = JsonataEvaluator.Default.Evaluate(
    "$reduce(Account.Order.Product, function($prev, $curr) { $prev + $curr.Price }, 0)",
    dataDoc.RootElement,
    workspace);

// $sort with a custom comparator
workspace.Reset();
JsonElement sorted = JsonataEvaluator.Default.Evaluate(
    "$sort(Account.Order.Product, function($a, $b) { $a.Price > $b.Price })",
    dataDoc.RootElement,
    workspace);
```

#### External C# function bindings (interpreted mode)

Bind C# functions to JSONata variables using `JsonataBinding.FromFunction()`. Three overload families are available:

##### `SequenceFunction` — the recommended approach

The `SequenceFunction` delegate receives arguments as a `ReadOnlySpan<Sequence>` and returns a `Sequence` result. This avoids materializing `JsonElement` values for numeric operations and integrates directly with the JSONata runtime's value proxy system:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

var bindings = new Dictionary<string, JsonataBinding>
{
    // A function that returns the hypotenuse of two numbers
    ["hypot"] = JsonataBinding.FromFunction(
        (ReadOnlySpan<Sequence> args, JsonWorkspace ws) =>
        {
            double a = args[0].AsDouble();
            double b = args[1].AsDouble();
            return Sequence.FromDouble(Math.Sqrt(a * a + b * b), ws);
        },
        parameterCount: 2),

    // A function that returns a string
    ["greet"] = JsonataBinding.FromFunction(
        (ReadOnlySpan<Sequence> args, JsonWorkspace ws) =>
        {
            string name = args[0].AsElement().GetString()!;
            return Sequence.FromString($"Hello, {name}!", ws);
        },
        parameterCount: 1),
};

JsonElement result = evaluator.Evaluate(
    "$hypot(3, 4) & ' from ' & $greet('World')",
    dataDoc.RootElement,
    workspace,
    bindings);
```

Key `Sequence` methods for function bindings:

| Read arguments | Create results |
|---------------|----------------|
| `args[i].AsDouble()` — extract number | `Sequence.FromDouble(value, workspace)` — zero allocation |
| `args[i].TryGetDouble(out double v)` — try extract | `Sequence.FromString(value, workspace)` — materializes immediately |
| `args[i].AsElement()` — materialize to `JsonElement` | `Sequence.FromBool(value)` — pre-cached singleton |
| `args[i].AsElement(workspace)` — materialize with workspace | `new Sequence(in jsonElement)` — wrap existing element |

**Signature validation:** Optionally provide a JSONata type signature string to type-check arguments before the C# function is called:

```csharp
["to_upper"] = JsonataBinding.FromFunction(
    (args, ws) =>
    {
        string s = args[0].AsElement().GetString()!.ToUpperInvariant();
        return Sequence.FromString(s, ws);
    },
    parameterCount: 1,
    signature: "<s:s>"),  // expects a string argument, returns a string
```

##### `Func<double, double>` and `Func<double, double, double>` — numeric convenience

For pure numeric functions, shorthand overloads automatically extract `double` arguments and wrap the result with zero allocation:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    // Unary: double → double (parameter count is inferred as 1)
    ["cosine"]   = JsonataBinding.FromFunction((double v) => Math.Cos(v)),
    ["to_celsius"] = JsonataBinding.FromFunction((double f) => (f - 32) * 5.0 / 9.0),

    // Binary: (double, double) → double (parameter count is inferred as 2)
    ["max"]      = JsonataBinding.FromFunction((double a, double b) => Math.Max(a, b)),
    ["distance"] = JsonataBinding.FromFunction((double x, double y) => Math.Sqrt(x * x + y * y)),
};
```

These are equivalent to writing the full `SequenceFunction` with `args[0].AsDouble()` / `Sequence.FromDouble(...)` boilerplate.

##### `Func<JsonElement[], JsonWorkspace, JsonElement>` — legacy overload

The original function binding API receives `JsonElement[]` arguments and returns a `JsonElement`. It is retained for backwards compatibility:

```csharp
["double_it"] = JsonataBinding.FromFunction(
    (JsonElement[] args, JsonWorkspace ws) =>
    {
        double result = args[0].GetDouble() * 2;
        return JsonElement.ParseValue($"{result}"u8.ToArray());
    },
    parameterCount: 1),
```

Prefer the `SequenceFunction` overload for new code — it avoids the `JsonElement[]` array allocation and the `JsonElement.ParseValue` materialization overhead.

##### Mixing value and function bindings

You can mix value bindings and function bindings in the same dictionary. External functions are available as `$name(...)` in the expression:

```csharp
var bindings = new Dictionary<string, JsonataBinding>
{
    ["tax_rate"]  = 0.2,                                                   // value (implicit double)
    ["round"]     = JsonataBinding.FromFunction((double v) => Math.Round(v, 2)),  // function
};

JsonElement result = evaluator.Evaluate(
    "$round(price * (1 + $tax_rate))",
    dataDoc.RootElement,
    workspace,
    bindings);
```

#### Custom functions for code generation (`.jfn` files)

For code-generated expressions (source generator or CLI tool), define custom functions in `.jfn` files. These contain raw C# code that runs at the same performance level as built-in functions:

**Expression form:**

```jfn
// celsius_to_fahrenheit.jfn
fn celsius_to_fahrenheit(temp) => JsonataCodeGenHelpers.NumberFromDouble(temp.GetDouble() * 9.0 / 5.0 + 32.0, workspace);
```

**Block form:**

```jfn
fn clamp(value, lo, hi)
{
    double v = value.GetDouble();
    double low = lo.GetDouble();
    double high = hi.GetDouble();
    double result = Math.Max(low, Math.Min(high, v));
    return JsonataCodeGenHelpers.NumberFromDouble(result, workspace);
}
```

Each parameter receives a `JsonElement`. The `workspace` variable is implicitly available. Use `JsonataCodeGenHelpers` for helper methods like `NumberFromDouble`, `BooleanElement`, etc.

**Using with the source generator:** Add `.jfn` files as `AdditionalFiles` in your project:

```xml
<ItemGroup>
  <AdditionalFiles Include="MyFunctions.jfn" />
  <AdditionalFiles Include="MyExpression.jsonata" />
</ItemGroup>
```

The source generator automatically discovers `.jfn` files and makes the functions available to all `[JsonataExpression]`-annotated types in the project.

**Using with the CLI tool:** Pass the `--customFunctions` option:

```bash
generatejsonschematypes jsonata MyExpression.jsonata --customFunctions MyFunctions.jfn
```

### Recursion depth and execution timeouts

Guard against runaway expressions with depth and timeout limits:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

try
{
    JsonElement result = JsonataEvaluator.Default.Evaluate(
        myExpression,
        dataDoc.RootElement,
        workspace,
        maxDepth: 200,       // recursion depth limit (default: 500)
        timeLimitMs: 5000);  // timeout in milliseconds (default: 0 = no limit)
}
catch (JsonataException ex) when (ex.Code == "U1001")
{
    // U1001: stack overflow or timeout
    Console.WriteLine($"Expression exceeded limits: {ex.Message}");
}
```

Both limits apply to all evaluation modes (interpreted and code-generated). When a limit is exceeded, a `JsonataException` with code `U1001` is thrown.

### Workspace and memory management

The `JsonWorkspace` provides pooled memory for evaluation results and intermediate values. Using a caller-managed workspace is the recommended pattern for zero-allocation evaluation.

**Single evaluation:**

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Evaluate(expression, data, workspace);
// result is valid until workspace is disposed or reset
```

**Batch evaluation — reset the workspace between iterations:**

```csharp
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

**Without a workspace (convenience, allocates per call):**

```csharp
// Omit the workspace — the evaluator creates one internally and clones the result
JsonElement result = evaluator.Evaluate(expression, data);
```

This overload creates a workspace internally, evaluates the expression, clones the result into a standalone `JsonElement`, and disposes the workspace. It is convenient for one-off evaluations but allocates on every call.

**Clearing the expression cache:**

The evaluator caches compiled delegate trees per expression string. If your application dynamically generates many unique expressions over time, the cache can grow unbounded. Use `ClearCache()` to release all cached compilations:

```csharp
var evaluator = new JsonataEvaluator();
// ... many evaluations with unique expressions ...

evaluator.ClearCache(); // releases all cached delegate trees
```

Subsequent evaluations will recompile their expressions on first use. This is a no-op on `JsonataEvaluator.Default` in practice, since the static instance's cache grows with the application lifetime. Create a separate instance if you need isolated cache management.

### Error handling

All evaluation errors throw `JsonataException` with a standard error code, message, and character position:

```csharp
try
{
    JsonElement result = evaluator.Evaluate(expression, data, workspace);
}
catch (JsonataException ex)
{
    Console.WriteLine($"Error {ex.Code} at position {ex.Position}: {ex.Message}");
    // ex.Token contains the relevant token, if any
}
```

Error codes follow the [jsonata-js](https://github.com/jsonata-js/jsonata) conventions:

| Prefix | Category | Example |
|--------|----------|---------|
| `S0xxx` | Syntax/parse errors | `S0101`: String literal not terminated |
| `T0xxx` | Type errors | `T0410`: Argument type mismatch |
| `T0410` | Binding argument count | External function called with too few arguments |
| `D0xxx` | Runtime/data errors | `D3001`: Cannot convert value to string |
| `T1xxx` | Evaluation errors | `T1005`: Attempted to invoke a non-function |
| `T2xxx` | Operator errors | `T2001`: Left side of arithmetic is not a number |
| `U1001` | Resource limits | Stack overflow or execution timeout |

### String-to-string evaluation

For simple string-in, string-out transformations:

```csharp
string? result = evaluator.EvaluateToString(
    "FirstName & ' ' & Surname",
    """{"FirstName": "Fred", "Surname": "Smith"}""");

// result is the JSON string: "Fred Smith" (with quotes)
// Returns null if the expression produces no result
```

This convenience method parses the input JSON, evaluates the expression, and returns the result as a JSON string via `GetRawText()`. It does not support bindings or resource limits — use the full `Evaluate` overloads for those.

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
| `$random()` | Random number `[0, 1)` |
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
| Custom function bindings | `JsonataBinding.FromFunction()` (interpreted) | Via API |
| Custom functions (code-gen) | Via `.jfn` files | N/A |
| Recursion depth limit | Configurable (default 500) | Not configurable |
| Execution timeout | Configurable (milliseconds) | Not available |

### Benchmark summary

Measured on .NET 10.0 (13th Gen Intel Core i7-13800H) across 50 scenarios covering property navigation, arithmetic, string operations, pattern matching, higher-order functions, predicate filtering, object construction, array functions, and object functions. Each benchmark compares three implementations: `Corvus` (interpreted), `CodeGen` (source-generated), and `Jsonata.Net.Native` (reference .NET implementation, v3.0.0).

All Runtime benchmarks use caller-managed `JsonWorkspace` for zero-allocation evaluation. Jsonata.Net.Native uses pre-compiled `JsonataQuery` objects with pre-parsed `JToken` data. The "Alloc" columns show per-invocation GC allocation.

#### Employee transform (reference benchmark)

This benchmark replicates the [Jsonata.Net.Native benchmark scenario](https://github.com/mikhail-barg/jsonata.net.native) — a multi-step expression with property navigation, string concatenation, and array predicate filtering against a real-world employee dataset:

```jsonata
{
  'name': Employee.FirstName & ' ' & Employee.Surname,
  'mobile': Contact.Phone[type = 'mobile'].number
}
```

**Cached evaluation** (expression pre-compiled):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Runtime (interpreted) | 1,226 ns | 960 B |
| Runtime (code-gen) | 1,123 ns | 240 B |
| Jsonata.Net.Native | 2,297 ns | 9,920 B |

The runtime evaluator is **1.9× faster** with **90% less allocation** than the reference implementation. Code-gen matches interpreted speed with 75% less allocation (240 B vs 960 B).

**Cold start** (fresh compile + evaluate):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Runtime (interpreted) | 4,353 ns | 10,800 B |
| Jsonata.Net.Native | 4,765 ns | 19,296 B |

Cold start is comparable in speed, but Corvus allocates 44% less memory.

#### Detailed results

##### Property navigation

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Deep path | `Account.Order.Product.Price` | 446 ns | 313 ns | 403 ns | 120 B | 120 B | 1,816 B |
| Flatten path | `Account.Order.Product[]` | 489 ns | 328 ns | 274 ns | 120 B | 120 B | 1,520 B |
| Quoted property | `` Account.`Account Name` `` | 45 ns | 32 ns | 167 ns | 0 B | 0 B | 1,024 B |
| Array index | `Account.Order[0].OrderID` | 63 ns | 53 ns | 242 ns | 0 B | 0 B | 1,408 B |
| 5-level path | `a.b.c.d.e.value` | 151 ns | 176 ns | 574 ns | 0 B | 0 B | 1,936 B |
| 10-level path | `a.b.c.d.e.f.g.h.i.j.value` | 151 ns | 456 ns | 946 ns | 0 B | 0 B | 3,328 B |

The runtime evaluator is **3–6× faster** for simple property access and deep navigation, with zero allocation. Flatten path (`Product[]`) through nested arrays is slower due to document model overhead, but uses **92% less memory**. Code-gen is **2–5× faster** for most navigation patterns.

##### Arithmetic

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Sum-product | `$sum(Account.Order.Product.(Price * Quantity))` | 1,247 ns | 426 ns | 1,441 ns | 184 B | 0 B | 6,480 B |
| Map arithmetic | `Account.Order.Product.(Price * Quantity)` | 1,138 ns | 842 ns | 1,199 ns | 152 B | 120 B | 5,536 B |
| Pure arithmetic | `1 + 2 * 3 - 4 / 2 + 10 % 3` | 182 ns | 59 ns | 276 ns | 0 B | 0 B | 1,352 B |

Code-gen achieves **3–5× speedup** over the reference implementation for arithmetic with **zero allocation**. Pure arithmetic shows the largest CG win (4.7× faster) because computation dominates over data traversal.

##### String concatenation

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple concat | `FirstName & ' ' & Surname` | 467 ns | 245 ns | 441 ns | 0 B | 0 B | 1,408 B |
| Concat + number | `FirstName & ' ' & Surname & ', age ' & $string(Age)` | 928 ns | 287 ns | 1,212 ns | 0 B | 0 B | 3,136 B |
| Join array | `$join([Address.Street, Address.City, Address.Postcode], ', ')` | 982 ns | 319 ns | 1,511 ns | 120 B | 0 B | 4,040 B |

Code-gen is **2–5× faster** than the reference implementation for string operations with zero allocation. The interpreted evaluator matches or beats native for multi-part concatenation.

##### String functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $contains | `$contains(Product."Product Name", "Hat")` | 328 ns | 273 ns | 1,214 ns | 0 B | 0 B | 3,136 B |
| $length | `$length(Product."Product Name")` | 375 ns | 279 ns | 993 ns | 0 B | 0 B | 2,864 B |
| $lowercase | `$lowercase(Product."Product Name")` | 334 ns | 261 ns | 1,022 ns | 0 B | 0 B | 2,864 B |
| $uppercase | `$uppercase(Product."Product Name")` | 341 ns | 284 ns | 1,098 ns | 0 B | 0 B | 2,864 B |
| $trim | `$trim(Product."Product Name")` | 463 ns | 319 ns | 1,445 ns | 96 B | 0 B | 2,864 B |
| $substring | `$substring(Product."Product Name", 0, 6)` | 737 ns | 541 ns | 1,301 ns | 0 B | 0 B | 3,416 B |
| $substringBefore | `$substringBefore(Product."Product Name", " ")` | 444 ns | 366 ns | 1,125 ns | 0 B | 0 B | 3,120 B |
| $substringAfter | `$substringAfter(Product."Product Name", " ")` | 403 ns | 367 ns | 1,216 ns | 0 B | 0 B | 3,112 B |

String functions show **2–4× speedup** across the board with **zero allocation** for both runtime and code-gen. The reference implementation allocates 2–3 KB per call due to `JToken` intermediaries.

##### Pattern matching

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $match | `$match(text, /\d{3}-\d{3}-\d{4}/)` | 1,687 ns | 1,378 ns | 1,611 ns | 632 B | 912 B | 8,072 B |
| $replace (regex) | `$replace(text, /\d{3}-\d{3}-\d{4}/, "***")` | 522 ns | 386 ns | 1,855 ns | 0 B | 0 B | 9,312 B |
| $replace (string) | `$replace(text, "555", "XXX")` | 227 ns | 317 ns | 863 ns | 0 B | 0 B | 3,264 B |
| $split (regex) | `$split(text, /\d{3}-\d{3}-\d{4}/)` | 2,431 ns | 1,745 ns | 2,811 ns | 120 B | 120 B | 7,536 B |
| $split (string) | `$split(text, ". ")` | 1,118 ns | 1,147 ns | 1,172 ns | 120 B | 120 B | 2,552 B |

Pattern matching shows **92–100% less allocation**. Regex replacement is **3.6–4.8× faster** than native with zero allocation thanks to UTF-8 span processing. String replacement is **2.7–3.8× faster**. Regex split is **1.3–1.7× faster** while string split approaches parity.

##### Higher-order functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $map | `$map(Account.Order.Product, function($v) { ... })` | 1,051 ns | 765 ns | 1,380 ns | 480 B | 120 B | 6,768 B |
| $filter | `$filter(Account.Order.Product, function($v) { ... })` | 1,268 ns | 838 ns | 1,713 ns | 480 B | 120 B | 7,632 B |
| $reduce | `$reduce(Account.Order.Product, function($prev, $curr) { ... }, 0)` | 1,619 ns | 833 ns | 2,271 ns | 360 B | 120 B | 10,120 B |
| $sort | `$sort(Account.Order.Product, function($a, $b) { ... })` | 5,248 ns | 2,419 ns | 6,862 ns | 600 B | 240 B | 9,992 B |

Higher-order functions show **93–98% less allocation** than the reference implementation. Code-gen eliminates most allocation (120–240 B vs 480–600 B for interpreted, vs 6–10 KB for native).

**Large array (5,000 items):**

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $map | `$map(items, function($v) { $v.value * 2 })` | 2,931 μs | 1,396 μs | 8,092 μs | 200 B | 120 B | ~12 MB |
| $filter | `$filter(items, function($v) { $v.value > 2500 })` | 2,475 μs | 984 μs | 4,090 μs | 200 B | 120 B | ~12 MB |
| $reduce | `$reduce(items, function($prev, $curr) { ... }, 0)` | 2,552 μs | 586 μs | 4,173 μs | 80 B | 0 B | ~11 MB |

At scale (5,000 items), code-gen is **4–7× faster** than native with **near-zero allocation** (0–200 B vs ~12 MB). The interpreted evaluator is **1.6–2.8× faster** with 200 B or less per call.

##### Iteration functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $each | `$each(obj, function($v, $k) { $k & ": " & $string($v) })` | 2,672 ns | 720 ns | 3,501 ns | 200 B | 120 B | 8,096 B |
| $sift | `$sift(obj, function($v) { $type($v) = "number" })` | 1,099 ns | 736 ns | 1,658 ns | 200 B | 120 B | 7,088 B |

Code-gen `$each` is **4.9× faster** than native. Both functions show **97–98% less allocation**.

##### Predicate filtering

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Single predicate | `Contact.Phone[type = 'mobile'].number` | 589 ns | 492 ns | 1,300 ns | 120 B | 120 B | 5,760 B |
| Chained predicate | `Contact[ssn = '496913021'].Phone[0].number` | 286 ns | 239 ns | 981 ns | 0 B | 0 B | 3,072 B |
| Compound predicate | `Contact.Phone[type = 'office' or type = 'mobile'].number` | 1,459 ns | 573 ns | 2,497 ns | 120 B | 120 B | 10,376 B |

Predicate filtering shows **2–4× speedup** over the reference implementation with **97–100% less allocation**.

##### Object construction

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple object | `` {"name": Account.`Account Name`, "total": $sum(...)} `` | 2,717 ns | 1,130 ns | 3,168 ns | 304 B | 120 B | 7,840 B |
| Group-by object | `` Account.Order.Product.{`Product Name`: Price} `` | 3,171 ns | 1,165 ns | 2,268 ns | 600 B | 120 B | 7,104 B |
| Array of objects | `[Account.Order.Product.{"name": ..., "total": ...}]` | 2,238 ns | 2,004 ns | 3,423 ns | 120 B | 120 B | 9,664 B |

Code-gen achieves **2–3× speedup** for object construction with **98–99% less allocation**. Group-by is the only scenario where the runtime evaluator is slower than native (1.4×), due to the overhead of building grouped object keys.

##### Array functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $count | `$count(Account.Order[0].Product)` | 145 ns | 115 ns | 437 ns | 0 B | 0 B | 2,112 B |
| $append | `$append(Order[0].Product, Order[1].Product)` | 470 ns | 449 ns | 709 ns | 120 B | 120 B | 3,280 B |
| $reverse | `$reverse(Account.Order[0].Product)` | 268 ns | 254 ns | 460 ns | 120 B | 120 B | 2,136 B |
| $distinct | `$distinct(Account.Order.Product."Product Name")` | 645 ns | 577 ns | 655 ns | 400 B | 120 B | 2,848 B |

Array functions are **1.5–3× faster** with zero or minimal allocation. `$distinct` uses a zero-copy `UniqueItemsHashSet` that deduplicates via document-internal element references — no intermediate strings are created.

##### Object functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $keys | `$keys(Account.Order[0].Product[0])` | 577 ns | 492 ns | 951 ns | 120 B | 120 B | 2,696 B |
| $merge | `$merge(Account.Order.Product)` | 861 ns | 769 ns | 1,417 ns | 400 B | 120 B | 2,784 B |
| $each (as $values) | `$each(obj, function($v){$v})` | 1,133 ns | 372 ns | 1,448 ns | 200 B | 120 B | 4,232 B |

Object functions are **1.6–2× faster** for runtime, **2–4× faster** for code-gen. `$keys` uses a `Utf8KeyHashSet` for zero-copy deduplication over raw UTF-8 property names. `$merge` uses fused chain navigation to eliminate intermediate document builders. Corvus also provides `$values()` as an extension function (not available in standard JSONata).

> *Benchmarks run with BenchmarkDotNet v0.15.8 on .NET 10.0.5, 13th Gen Intel Core i7-13800H, Windows 11. All Runtime benchmarks use `JsonWorkspace` for pooled evaluation. Jsonata.Net.Native v3.0.0 uses pre-compiled `JsonataQuery` with pre-parsed `JToken` data. OutlierMode=RemoveAll, RunStrategy=Throughput.*
