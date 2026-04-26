# JSONata Query and Transformation Language

> **[Try the JSONata Playground](/playground-jsonata/)** — evaluate JSONata expressions in your browser using the Corvus interpreted runtime.

## Overview

`Corvus.Text.Json.Jsonata` implements [JSONata](https://jsonata.org/) for the Corvus.Text.Json document model — a high-performance query and transformation language that evaluates JSONata expressions against JSON data with zero-allocation evaluation, compiled delegate trees, and pooled workspace memory.

[JSONata](https://jsonata.org/) is an expressive, Turing-complete functional query and transformation language for JSON. It supports path navigation, predicate filtering, higher-order functions (`$map`, `$filter`, `$reduce`, `$sort`), object construction, string manipulation, arithmetic, regular expressions, and user-defined functions. The Corvus implementation passes **all 1,665** tests in the official [JSONata test suite](https://github.com/jsonata-js/jsonata) (100% conformance). Two upstream test cases involve lone UTF-16 surrogates (`\uD800`) that cannot be threaded through .NET's string-based lexer; these are verified by dedicated unit tests that exercise the D3140 validation directly (see [Conformance](#conformance)).

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Expressions are dynamic, determined at runtime | `Corvus.Text.Json.Jsonata` |
| **Source generator** | Expressions are known at build time, embedded in your project | `Corvus.Text.Json.Jsonata.SourceGenerator` |
| **CLI code generation** | Expressions are known ahead of time, generated outside the build | `Corvus.Json.Cli` (the `jsonata` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch. Benchmarks show the interpreted evaluator is **up to 9× faster** than [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) (the .NET reference implementation) with **90–100% less memory allocation**, and code-generated evaluators can be **up to 13× faster**.

> **Choosing between JSONata and JsonLogic:** JSONata is ideal for **querying, reshaping, and transforming** JSON data — path navigation, filtering, object construction, and string manipulation. If you need **declarative business rules** — branching logic, predicates, and simple calculations expressed as JSON — see [JsonLogic](JsonLogic.md) instead.

**Requirements:** The runtime packages target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`. The source generator is an analyzer package and does not impose additional runtime requirements.

## Conformance

![JSONata Runtime Conformance](https://img.shields.io/badge/JSONata_Runtime-1665%2F1665_(100%25)-brightgreen)
![JSONata CodeGen Conformance](https://img.shields.io/badge/JSONata_CodeGen-1665%2F1665_(100%25)-brightgreen)

Both the runtime evaluator and the code generation pipeline pass all **1,665** official JSONata test suite cases (100% conformance) on .NET 10.0. The runtime evaluator also passes on .NET Framework 4.8.1.

The code generation pipeline compiles each expression to optimized static C#. Tests that require runtime-only features (variable bindings, custom recursion depth, execution timeout) are validated through the 5-parameter overload which delegates to the runtime evaluator.

> **Note on surrogate edge cases:** The upstream test suite contains two cases (`function-encodeUrl/case002` and `function-encodeUrlComponent/case002`) whose expression strings include a lone UTF-16 surrogate (`\uD800`). JavaScript engines permit lone surrogates in strings, so these tests work natively on the jsonata-js reference implementation. In .NET, the JSONata lexer materialises string tokens via `Encoding.UTF8.GetString`, which rejects the WTF-8 byte sequences that lone surrogates produce. The conformance test runner therefore cannot load these two expressions. However, the underlying D3140 error validation is fully covered by dedicated unit tests (`EncodeUrlSurrogateTests`) that construct the invalid data directly and verify both the runtime and code-generation paths throw the correct error.

## Quick start

Install the packages:

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.Jsonata
```

**Simplest approach — string in, string out:**

```csharp
using Corvus.Text.Json.Jsonata;

string? result = JsonataEvaluator.Default.EvaluateToString(
    "FirstName & ' ' & Surname",
    """{"FirstName": "Fred", "Surname": "Smith", "Age": 28}""");

Console.WriteLine(result); // "\"Fred Smith\""
```

`EvaluateToString` parses the input JSON, evaluates the expression, and returns the result as a JSON string via `GetRawText()`. It is the simplest way to get started — no document parsing, workspace management, or disposal needed.

**Full API — zero-allocation evaluation:**

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.Jsonata;

// Parse the input data (using statement ensures pooled memory is returned)
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

The evaluator compiles the expression into a delegate tree on first use and caches it. Subsequent evaluations of the same expression skip compilation entirely. Create one `JsonataEvaluator` instance and reuse it — `JsonataEvaluator.Default` provides a shared static instance.

The workspace provides pooled memory for the result — **zero GC allocation** per evaluation for most expressions. The result remains valid until the workspace is disposed or reset.

## Evaluation modes

Three evaluation modes are available. All three produce the same results for the same expression; they differ in when compilation happens and what overhead is incurred at evaluation time.

### Interpreted (runtime evaluation)

Use `JsonataEvaluator` when expressions are determined at runtime:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
var evaluator = JsonataEvaluator.Default;

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

The `corvusjson` CLI tool includes a `jsonata` subcommand for ahead-of-time code generation outside the MSBuild pipeline:

```bash
dotnet tool install --global Corvus.Json.Cli
```

```bash
corvusjson jsonata <expressionFile> \
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
corvusjson jsonata expressions/employee-transform.jsonata \
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
corvusjson jsonata MyExpression.jsonata --customFunctions MyFunctions.jfn
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
var evaluator = JsonataEvaluator.Default;
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

For simple string-in, string-out transformations (also shown in [Quick start](#quick-start)):

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

## Common pitfalls

### Always dispose `ParsedJsonDocument`

`ParsedJsonDocument<T>` rents memory from `ArrayPool<byte>`. Forgetting to dispose it leaks pooled memory:

```csharp
// ❌ BAD — leaks pooled memory
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Evaluate(expression, doc.RootElement, workspace);

// ✅ GOOD — using statement returns memory to the pool
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Evaluate(expression, doc.RootElement, workspace);
```

### Reset the workspace in loops

Without `Reset()`, workspace memory grows with each evaluation. The workspace remains valid, but old results consume pooled memory unnecessarily:

```csharp
// ❌ BAD — workspace grows unboundedly
foreach (var item in items)
{
    var result = evaluator.Evaluate(expression, item, workspace);
    ProcessResult(result);
}

// ✅ GOOD — reset frees previous results
foreach (var item in items)
{
    workspace.Reset();
    var result = evaluator.Evaluate(expression, item, workspace);
    ProcessResult(result);
}
```

### Don't forget `AdditionalFiles` for the source generator

The source generator reads expression files from `AdditionalFiles`. Without the MSBuild item, the generator can't find the expression and produces diagnostic `JASG001`:

```xml
<!-- ❌ Missing — generator produces JASG001 -->
<ItemGroup>
  <None Include="Expressions\transform.jsonata" />
</ItemGroup>

<!-- ✅ Correct -->
<ItemGroup>
  <AdditionalFiles Include="Expressions\transform.jsonata" />
</ItemGroup>
```

### Result lifetime is tied to the workspace

When you pass a workspace, the returned `JsonElement` is backed by that workspace's memory. Using the result after the workspace is disposed or reset produces undefined behavior:

```csharp
// ❌ BAD — result is invalid after workspace disposal
JsonElement result;
using (JsonWorkspace workspace = JsonWorkspace.Create())
{
    result = evaluator.Evaluate(expression, data, workspace);
}
Console.WriteLine(result.GetRawText()); // undefined behavior

// ✅ GOOD — use result before workspace is disposed
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Evaluate(expression, data, workspace);
Console.WriteLine(result.GetRawText()); // safe
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
| Custom function bindings | `JsonataBinding.FromFunction()` (interpreted) | Via API |
| Custom functions (code-gen) | Via `.jfn` files | N/A |
| Recursion depth limit | Configurable (default 500) | Not configurable |
| Execution timeout | Configurable (milliseconds) | Not available |

### Benchmark summary

Across 95 scenarios, the Corvus implementation outperforms [Jsonata.Net.Native](https://github.com/mikhail-barg/jsonata.net.native) (the reference .NET implementation) in all but one scenario (`$millis`, where evaluator framework overhead exceeds the trivial function cost):

- **Interpreted runtime:** 1.1–9× faster with 90–100% less memory allocation
- **Code-generated:** 1.1–13× faster with near-zero allocation (0–216 B vs 1–12 KB)
- **Zero allocation** in 70+ of 95 scenarios for the interpreted evaluator
- **Constant folding** by the source generator reduces pure arithmetic and constant `$zip` to sub-nanosecond evaluation

The detailed results below are measured on .NET 10.0 (13th Gen Intel Core i7-13800H). Each benchmark compares three implementations: `Corvus` (interpreted), `CodeGen` (source-generated), and `Jsonata.Net.Native` (v3.0.0).

All Runtime benchmarks use caller-managed `JsonWorkspace` with the UTF-8 byte[] evaluation API and expression caching for zero-allocation evaluation. Jsonata.Net.Native uses pre-compiled `JsonataQuery` objects with pre-parsed `JToken` data. The "Alloc" columns show per-invocation GC allocation.

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
| Runtime (interpreted) | 2,113 ns | 976 B |
| Runtime (code-gen) | 1,947 ns | 272 B |
| Jsonata.Net.Native | 4,463 ns | 9,920 B |

The runtime evaluator is **2.1× faster** with **90% less allocation** than the reference implementation. Code-gen is **2.3× faster** with 97% less allocation (272 B vs 9,920 B).

**Cold start** (fresh compile + evaluate):

| Method | Mean | Allocated |
|--------|-----:|----------:|
| Runtime (interpreted) | 5,747 ns | 8,104 B |
| Jsonata.Net.Native | 9,174 ns | 19,296 B |

Cold start runtime is 1.6× faster than native, and Corvus allocates 58% less memory.

#### Detailed results

##### Property navigation

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Deep path | `Account.Order.Product.Price` | 772 ns | 559 ns | 840 ns | 136 B | 136 B | 1,816 B |
| Flatten path | `Account.Order.Product[]` | 439 ns | 515 ns | 560 ns | 136 B | 136 B | 1,520 B |
| Quoted property | `` Account.`Account Name` `` | 89 ns | 69 ns | 327 ns | 0 B | 0 B | 1,024 B |
| Array index | `Account.Order[0].OrderID` | 130 ns | 125 ns | 517 ns | 0 B | 0 B | 1,408 B |
| 5-level path | `a.b.c.d.e.value` | 158 ns | 189 ns | 668 ns | 0 B | 0 B | 1,936 B |
| 10-level path | `a.b.c.d.e.f.g.h.i.j.value` | 298 ns | 312 ns | 1,095 ns | 0 B | 0 B | 3,328 B |

The runtime evaluator is **3.7–4.0× faster** for simple property access (quoted names, array indices) and **3.7–4.2× faster** for deep multi-level navigation, with zero allocation for paths of 4+ levels. Nested-array traversal (deep path, flatten) is **1.1–1.3× faster** with **91% less memory**. Code-gen is **3.5–4.7× faster** for simple and deep navigation patterns.

##### Arithmetic

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Sum-product | `$sum(Account.Order.Product.(Price * Quantity))` | 1,234 ns | 417 ns | 1,603 ns | 32 B | 0 B | 6,480 B |
| Map arithmetic | `Account.Order.Product.(Price * Quantity)` | 1,101 ns | 810 ns | 1,324 ns | 168 B | 136 B | 5,536 B |
| Pure arithmetic | `1 + 2 * 3 - 4 / 2 + 10 % 3` | 185 ns | 0.6 ns | 300 ns | 0 B | 0 B | 1,352 B |

Code-gen achieves **1.6–3.8× speedup** over the reference implementation for arithmetic with **zero or minimal allocation**. Pure arithmetic is **constant-folded at compile time** by the source generator (0.6 ns), achieving effectively infinite speedup. The runtime evaluator is **1.2–1.3× faster** than native for both sum-product and map arithmetic, using **97% less memory**.

##### Math functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $abs | `$abs(Account.Order[0].Product[0].Price - 50)` | 834 ns | 736 ns | 1,200 ns | 0 B | 0 B | 3,040 B |
| $ceil | `$ceil(Account.Order[0].Product[0].Price)` | 364 ns | 315 ns | 1,132 ns | 0 B | 0 B | 2,888 B |
| $floor | `$floor(Account.Order[0].Product[0].Price)` | 368 ns | 317 ns | 1,039 ns | 0 B | 0 B | 2,888 B |
| $power | `$power(Account.Order[0].Product[0].Price, 2)` | 641 ns | 477 ns | 1,361 ns | 0 B | 0 B | 3,200 B |
| $round | `$round(Account.Order[0].Product[0].Price, 1)` | 610 ns | 428 ns | 1,318 ns | 0 B | 0 B | 3,224 B |
| $sqrt | `$sqrt(Account.Order[0].Product[0].Price)` | 455 ns | 401 ns | 1,134 ns | 0 B | 0 B | 2,888 B |

Math functions achieve **1.4–3.1× speedup** with **zero allocation** across the board. Code-gen is **1.6–3.6× faster** than native for all six functions. `$round` code-gen is **3.1× faster** than native; `$sqrt` code-gen is **2.8× faster**.

##### String concatenation

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple concat | `FirstName & ' ' & Surname` | 462 ns | 243 ns | 492 ns | 0 B | 0 B | 1,408 B |
| Concat + number | `FirstName & ' ' & Surname & ', age ' & $string(Age)` | 478 ns | 162 ns | 702 ns | 0 B | 0 B | 3,136 B |
| Join array | `$join([Address.Street, Address.City, Address.Postcode], ', ')` | 893 ns | 307 ns | 1,720 ns | 136 B | 0 B | 4,040 B |

Code-gen is **2–6× faster** than the reference implementation for string operations with zero allocation. The interpreted evaluator matches or beats native for multi-part concatenation.

##### String functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $contains | `$contains(Product."Product Name", "Hat")` | 353 ns | 210 ns | 1,338 ns | 0 B | 0 B | 3,136 B |
| $length | `$length(Product."Product Name")` | 402 ns | 302 ns | 1,131 ns | 0 B | 0 B | 2,864 B |
| $lowercase | `$lowercase(Product."Product Name")` | 337 ns | 319 ns | 1,158 ns | 0 B | 0 B | 2,864 B |
| $uppercase | `$uppercase(Product."Product Name")` | 352 ns | 309 ns | 1,196 ns | 0 B | 0 B | 2,864 B |
| $trim | `$trim(Product."Product Name")` | 455 ns | 323 ns | 1,300 ns | 96 B | 0 B | 2,864 B |
| $substring | `$substring(Product."Product Name", 0, 6)` | 746 ns | 372 ns | 1,480 ns | 0 B | 0 B | 3,416 B |
| $substringBefore | `$substringBefore(Product."Product Name", " ")` | 418 ns | 281 ns | 1,353 ns | 0 B | 0 B | 3,120 B |
| $substringAfter | `$substringAfter(Product."Product Name", " ")` | 417 ns | 280 ns | 1,418 ns | 0 B | 0 B | 3,112 B |
| $pad | `$pad(Account.Order[0].Product[0]."Product Name", 20)` | 586 ns | 430 ns | 1,484 ns | 360 B | 0 B | 3,808 B |

String functions show **2–6× speedup** with **zero or minimal allocation** for both runtime and code-gen. Code-gen achieves **3.5–6.4× faster** across all nine functions; the runtime evaluator ranges from **2–3.8× faster**. Code-gen `$pad` achieves zero allocation by using `stackalloc` for code-point indexing. The reference implementation allocates 2–4 KB per call due to `JToken` intermediaries.

##### Pattern matching

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $match | `$match(text, /\d{3}-\d{3}-\d{4}/)` | 1,344 ns | 1,041 ns | 3,355 ns | 168 B | 168 B | 8,072 B |
| $replace (regex) | `$replace(text, /\d{3}-\d{3}-\d{4}/, "***")` | 891 ns | 632 ns | 3,758 ns | 0 B | 0 B | 9,312 B |
| $replace (string) | `$replace(text, "555", "XXX")` | 221 ns | 178 ns | 837 ns | 0 B | 0 B | 3,264 B |
| $split (regex) | `$split(text, /\d{3}-\d{3}-\d{4}/)` | 1,166 ns | 847 ns | 1,618 ns | 136 B | 136 B | 7,536 B |
| $split (string) | `$split(text, ". ")` | 354 ns | 274 ns | 680 ns | 136 B | 136 B | 2,552 B |

Pattern matching shows **95–100% less allocation**. Regex replacement is **4.2–5.9× faster** than native with zero allocation thanks to UTF-8 span processing. String replacement is **3.8–4.7× faster**. `$match` is **2.5–3.2× faster** with 98% less allocation.

##### Higher-order functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $map | `$map(Account.Order.Product, function($v) { ... })` | 1,487 ns | 1,290 ns | 2,713 ns | 216 B | 136 B | 6,768 B |
| $filter | `$filter(Account.Order.Product, function($v) { ... })` | 1,993 ns | 1,445 ns | 3,175 ns | 216 B | 136 B | 7,632 B |
| $reduce | `$reduce(Account.Order.Product, function($prev, $curr) { ... }, 0)` | 3,017 ns | 1,506 ns | 4,531 ns | 360 B | 136 B | 10,120 B |
| $sort | `$sort(Account.Order.Product, function($a, $b) { ... })` | 3,210 ns | 1,482 ns | 4,275 ns | 216 B | 136 B | 9,992 B |

Higher-order functions show **98–99% less allocation** than the reference implementation. Code-gen is **2.1–3.0× faster** with 136 B allocation vs 6–10 KB for native.

**Large array (5,000 items):**

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $map | `$map(items, function($v) { $v.value * 2 })` | 3,297 µs | 1,403 µs | 9,512 µs | 216 B | 136 B | ~12 MB |
| $filter | `$filter(items, function($v) { $v.value > 2500 })` | 2,641 µs | 869 µs | 4,659 µs | 216 B | 136 B | ~12 MB |
| $reduce | `$reduce(items, function($prev, $curr) { ... }, 0)` | 3,092 µs | 621 µs | 5,031 µs | 80 B | 0 B | ~11 MB |

At scale (5,000 items), code-gen is **5–8× faster** than native with **near-zero allocation** (0–136 B vs ~12 MB). The interpreted evaluator is **1.6–2.9× faster** with 216 B or less per call.

##### Iteration functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $each | `$each(obj, function($v, $k) { $k & ": " & $string($v) })` | 2,629 ns | 1,042 ns | 4,233 ns | 216 B | 136 B | 8,096 B |
| $sift | `$sift(obj, function($v) { $type($v) = "number" })` | 1,527 ns | 534 ns | 3,307 ns | 216 B | 136 B | 7,088 B |

Code-gen `$each` is **4.1× faster** than native and `$sift` is **6.2× faster**. Both functions show **98% less allocation**.

##### Predicate filtering

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Single predicate | `Contact.Phone[type = 'mobile'].number` | 1,016 ns | 887 ns | 2,654 ns | 136 B | 136 B | 5,760 B |
| Chained predicate | `Contact[ssn = '496913021'].Phone[0].number` | 303 ns | 247 ns | 1,186 ns | 0 B | 0 B | 3,072 B |
| Compound predicate | `Contact.Phone[type = 'office' or type = 'mobile'].number` | 1,231 ns | 1,004 ns | 4,370 ns | 136 B | 136 B | 10,376 B |

Predicate filtering shows **2.6–4.8× speedup** over the reference implementation with **98–100% less allocation**.

##### Object construction

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Simple object | `` {"name": Account.`Account Name`, "total": $sum(...)} `` | 2,736 ns | 1,073 ns | 3,691 ns | 168 B | 136 B | 7,840 B |
| Group-by object | `` Account.Order.Product.{`Product Name`: Price} `` | 3,138 ns | 1,106 ns | 2,655 ns | 680 B | 136 B | 7,104 B |
| Array of objects | `[Account.Order.Product.{"name": ..., "total": ...}]` | 2,307 ns | 2,142 ns | 3,838 ns | 136 B | 136 B | 9,664 B |

Code-gen achieves **1.8–3.4× speedup** for object construction with **98–99% less allocation**. Group-by is the only scenario where the runtime evaluator is slower than native (1.2×), due to the overhead of building grouped object keys.

##### Array functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $count | `$count(Account.Order[0].Product)` | 142 ns | 115 ns | 460 ns | 0 B | 0 B | 2,112 B |
| $append | `$append(Order[0].Product, Order[1].Product)` | 422 ns | 416 ns | 793 ns | 136 B | 136 B | 3,280 B |
| $reverse | `$reverse(Account.Order[0].Product)` | 421 ns | 375 ns | 870 ns | 136 B | 136 B | 2,136 B |
| $distinct | `$distinct(Account.Order.Product."Product Name")` | 659 ns | 558 ns | 768 ns | 136 B | 136 B | 2,848 B |
| $flatten | `Account.Order.Product[]` | 439 ns | 515 ns | 560 ns | 136 B | 136 B | 1,520 B |
| $single | `$single(Account.Order[0].Product, function($v) { $v.Price > 30 })` | 739 ns | 441 ns | 2,147 ns | 80 B | 0 B | 4,960 B |

Array functions are **1.1–5× faster** with zero or minimal allocation. `$count` is **3.2× faster** with zero allocation. `$single` achieves **zero allocation** in code-gen by inlining the predicate as a direct comparison. `$distinct` uses a zero-copy `UniqueItemsHashSet` for deduplication via document-internal element references.

##### Object functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $keys | `$keys(Account.Order[0].Product[0])` | 540 ns | 384 ns | 978 ns | 136 B | 136 B | 2,696 B |
| $merge | `$merge(Account.Order.Product)` | 827 ns | 680 ns | 1,510 ns | 136 B | 136 B | 2,784 B |
| $each (as $values) | `$each(obj, function($v){$v})` | 1,182 ns | 391 ns | 1,796 ns | 216 B | 136 B | 4,232 B |
| $lookup | `$lookup(Account.Order[0].Product[0], "Price")` | 278 ns | 229 ns | 1,238 ns | 0 B | 120 B | 2,752 B |
| $spread | `$spread(Account.Order[0].Product[0])` | 786 ns | 735 ns | 1,234 ns | 136 B | 136 B | 3,400 B |
| $values | `$values(obj)` | 336 ns | 350 ns | — | 136 B | 136 B | — |

Object functions are **1.5–4.5× faster** for runtime, **1.7–5.4× faster** for code-gen. `$lookup` achieves **5.4× speedup** over native by resolving directly from the document backing store. `$keys` uses a `Utf8KeyHashSet` for zero-copy deduplication over raw UTF-8 property names. Corvus also provides `$values()` as an extension function (not available in standard JSONata).

##### Language operators

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Conditional | `Account.Order[0].Product[0].Price > 30 ? "expensive" : "cheap"` | 252 ns | 217 ns | 850 ns | 0 B | 0 B | 2,280 B |
| Descendant | `**."Product Name"` | 1,070 ns | 703 ns | 1,538 ns | 136 B | 136 B | 2,432 B |
| Transform pipe | `Account.Order.Product.Price ~> $sum` | 716 ns | 636 ns | 1,401 ns | 0 B | 0 B | 2,792 B |

Conditional expressions are **3.4–3.9× faster** than native with **zero allocation**. The descendant operator (`**`) uses a fused single-pass traversal that collects only matching properties during recursive descent, achieving **1.4–2.2× speedup** with 136 B allocation (vs 2,432 B).

##### Variable bindings

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| Let binding | `($total := $sum(...); $total > 100 ? "high" : "low")` | 2,253 ns | 851 ns | 3,182 ns | 152 B | 0 B | 6,992 B |
| Binding in HOF | `$map(Products, function($v) { ($p := $v.Price * $v.Quantity; ...) })` | 3,436 ns | 1,974 ns | 5,263 ns | 648 B | 136 B | 11,792 B |

Code-gen eliminates all allocation for simple let-bindings by keeping intermediate values in local variables (0 B vs 6,992 B for native). For bindings inside higher-order functions, code-gen emits a direct `double > constant` comparison shortcut that avoids element round-trips, achieving **2.7× speedup** with 136 B allocation (vs 11,792 B).

##### Date/time functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $millis | `$millis()` | 260 ns | 201 ns | 181 ns | 0 B | 0 B | 560 B |
| $now | `$now()` | 306 ns | 260 ns | 405 ns | 0 B | 0 B | 600 B |
| $toMillis | `$toMillis("2021-04-07T22:00:00.000Z")` | 280 ns | 214 ns | 763 ns | 0 B | 0 B | 1,376 B |
| $fromMillis | `$fromMillis(1617836400000)` | 535 ns | 322 ns | 826 ns | 0 B | 0 B | 1,568 B |

`$toMillis` uses a zero-alloc fast path via `TryGetDateTimeOffset` that parses directly from the UTF-8 backing — **2.7× faster** for runtime, **3.6× faster** for code-gen with zero allocation (vs 1,376 B for native). `$millis` is the only scenario where native is slightly faster (due to evaluator framework overhead for a trivial function), but Corvus achieves zero allocation. `$now` and `$fromMillis` now achieve **zero allocation** for both runtime and code-gen.

##### Aggregation functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $average | `$average(Account.Order.Product.Price)` | 1,044 ns | 588 ns | 1,371 ns | 0 B | 0 B | 2,760 B |
| $max | `$max(Account.Order.Product.Price)` | 751 ns | 588 ns | 1,376 ns | 0 B | 0 B | 2,760 B |
| $min | `$min(Account.Order.Product.Price)` | 521 ns | 308 ns | 734 ns | 0 B | 0 B | 2,760 B |

Aggregation functions are **1.3–2.4× faster** with **zero allocation** in both runtime and code-gen. Code-gen fuses chain navigation with aggregation to eliminate intermediate array construction.

##### Formatting functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $formatBase | `$formatBase(255, 16)` | 544 ns | 202 ns | 677 ns | 0 B | 0 B | 1,592 B |
| $formatNumber | `$formatNumber(1234.5678, "#,##0.00")` | 682 ns | 612 ns | 1,208 ns | 0 B | 0 B | 1,728 B |
| $formatInteger | `$formatInteger(1234, "w")` | 387 ns | 191 ns | — | 0 B | 0 B | — |
| $parseInteger | `$parseInteger("one hundred and twenty three", "w")` | 427 ns | 334 ns | — | 0 B | 0 B | — |

`$formatNumber` uses a zero-alloc implementation — **1.8× faster** than native with zero allocation. `$formatBase` code-gen is **3.4× faster** and now achieves **zero allocation**. `$formatInteger` and `$parseInteger` are Corvus extensions not available in Jsonata.Net.Native.

##### Encoding functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $base64encode | `$base64encode(text)` | 171 ns | 116 ns | 568 ns | 0 B | 0 B | 1,336 B |
| $base64decode | `$base64decode(encoded)` | 157 ns | 104 ns | 572 ns | 0 B | 0 B | 1,320 B |
| $encodeUrl | `$encodeUrl(url)` | 264 ns | 207 ns | 521 ns | 0 B | 0 B | 1,312 B |
| $encodeUrlComponent | `$encodeUrlComponent(component)` | 218 ns | 147 ns | 726 ns | 0 B | 0 B | 1,288 B |
| $decodeUrl | `$decodeUrl(encoded)` | 258 ns | 211 ns | 535 ns | 0 B | 0 B | 1,312 B |
| $decodeUrlComponent | `$decodeUrlComponent(encoded)` | 212 ns | 157 ns | 587 ns | 0 B | 0 B | 1,272 B |

Encoding functions are **2–5× faster** with **zero allocation** across the board, eliminating all per-call allocation previously required for intermediate buffers.

##### Type conversion functions

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $boolean | `$boolean(Account.Order[0].Product[0].Price)` | 219 ns | 235 ns | 1,194 ns | 0 B | 0 B | 2,840 B |
| $exists | `$exists(Account.Order.Product)` | 113 ns | 76 ns | 969 ns | 0 B | 0 B | 2,352 B |
| $not | `$not(Account."Account Name" = "Firefly")` | 162 ns | 130 ns | 825 ns | 0 B | 0 B | 1,960 B |
| $number | `$number("34.45")` | 298 ns | 198 ns | 733 ns | 0 B | 0 B | 1,232 B |
| $string | `$string(Account.Order[0].Product[0].Price)` | 547 ns | 481 ns | 1,339 ns | 0 B | 0 B | 3,112 B |
| $type | `$type(Account.Order[0].Product[0].Price)` | 183 ns | 183 ns | 1,196 ns | 0 B | 0 B | 2,816 B |

`$not` is **5.1–6.3× faster** with zero allocation. `$boolean` code-gen is **5.1× faster** with zero allocation (vs 2,840 B for native). `$type` is **6.5× faster** with zero allocation. `$string` is **2.4–2.8× faster** with zero allocation.

##### Zip and shuffle

| Scenario | Expression | Runtime | CodeGen | Jsonata.Net.Native | Runtime Alloc | CodeGen Alloc | Native Alloc |
|----------|-----------|-------:|--------:|-------------------:|-------------:|--------------:|-------------:|
| $zip (constant) | `$zip([1,2,3],[4,5,6])` | 39 ns | 1.3 ns | 1,431 ns | 0 B | 0 B | 3,696 B |
| $zip (data) | `$zip(Account.Order.Product.Price, Account.Order.Product.Quantity)` | 1,209 ns | 1,184 ns | 2,295 ns | 136 B | 136 B | 4,784 B |
| $zip (mixed) | `$zip(keys, Order[0].Product.Price)` | 866 ns | 772 ns | 1,830 ns | 136 B | 136 B | 4,240 B |
| $shuffle | `$shuffle(Account.Order[0].Product)` | 774 ns | 723 ns | 1,612 ns | 136 B | 136 B | 2,824 B |

`$zip` with constant arrays is **constant-folded** by the source generator (1.3 ns). Data-driven zip is **1.9× faster** with **97% less allocation**. `$shuffle` is **2.1× faster** using a Fisher–Yates in-place shuffle over the mutable document model.

> *Benchmarks run with BenchmarkDotNet v0.15.8 on .NET 10.0.7, 13th Gen Intel Core i7-13800H, Windows 11. All Runtime benchmarks use the UTF-8 byte[] evaluation API with expression caching. Jsonata.Net.Native v3.0.0 uses pre-compiled `JsonataQuery` with pre-parsed `JToken` data. OutlierMode=RemoveAll, WarmupCount=5, IterationCount=15, RunStrategy=Throughput.*
