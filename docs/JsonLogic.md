# JsonLogic Rule Engine

## Overview

`Corvus.Text.Json.JsonLogic` implements [JsonLogic](https://jsonlogic.com/) for the Corvus.Text.Json document model — a safe, side-effect-free rule engine that evaluates JSON-encoded logic rules against JSON data.

[JsonLogic](https://jsonlogic.com/) is a standard for expressing business rules as JSON. Rules are portable, storable in databases, and safely evaluated without allowing arbitrary code execution. The Corvus implementation passes the full [official test suite](https://jsonlogic.com/tests.json) and adds support for extended numeric types (`BigNumber`) via custom operators.

> **Choosing between JsonLogic and JSONata:** JsonLogic is ideal for **declarative business rules** — branching logic, predicates, and simple calculations expressed as JSON. If you need to **query, reshape, or transform** JSON data (path navigation, filtering, object construction), see [JSONata](Jsonata.md) instead.

> **Try it now:** The [JSON Logic Playground](/playground-jsonlogic/) lets you build rules visually and evaluate them against live data — no setup required.

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Rules are dynamic, determined at runtime | `Corvus.Text.Json.JsonLogic` |
| **Source generator** | Rules are known at build time, embedded in your project | `Corvus.Text.Json.JsonLogic.SourceGenerator` |
| **CLI code generation** | Rules are known ahead of time, generated outside the build | `Corvus.Json.Cli` (the `jsonlogic` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch and can constant-fold literal expressions. Benchmarks show generated code is typically **70–98% faster** than JsonEverything across 19 scenarios, with zero or near-zero allocations (see [benchmark summary](#benchmark-summary)).

**Requirements:** The runtime packages target `net9.0`, `net10.0`, `netstandard2.0`, and `netstandard2.1`. The source generator is an analyzer package and does not impose additional runtime requirements.

## Quick start

Install the packages:

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.JsonLogic
```

**Simplest approach — string in, string out:**

```csharp
using Corvus.Text.Json.JsonLogic;

string? result = JsonLogicEvaluator.Default.EvaluateToString(
    """{"+":[{"var":"a"},{"var":"b"}]}""",
    """{"a":3,"b":4}""");

Console.WriteLine(result); // "7"
```

`EvaluateToString` parses the rule and data, evaluates the rule, and returns the result as a JSON string. It is the simplest way to get started — no document parsing, workspace management, or disposal needed.

**Full API — zero-allocation evaluation:**

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

// Parse the rule and data (using statements ensure pooled memory is returned)
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"+":[{"var":"a"},{"var":"b"}]}"""u8);
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"a":3,"b":4}"""u8);

// Create a workspace for zero-allocation evaluation
using JsonWorkspace workspace = JsonWorkspace.Create();

// Evaluate
JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(
    rule, dataDoc.RootElement, workspace);

Console.WriteLine(result.GetRawText()); // "7"
```

The evaluator compiles the rule into a delegate tree on first use and caches it. Subsequent evaluations of the same rule skip compilation entirely. Create one `JsonLogicEvaluator` instance and reuse it — `JsonLogicEvaluator.Default` provides a shared static instance.

The workspace provides pooled memory for the result — **zero GC allocation** per evaluation for most rules. The result remains valid until the workspace is disposed or reset.

## Installation

### Interpreted mode (runtime evaluation)

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.JsonLogic
```

### Source generator (build-time code generation)

```bash
dotnet add package Corvus.Text.Json
dotnet add package Corvus.Text.Json.JsonLogic
dotnet add package Corvus.Text.Json.JsonLogic.SourceGenerator
```

The source generator package is a development dependency — it runs at build time and produces C# source; the generated code depends on `Corvus.Text.Json` and `Corvus.Text.Json.JsonLogic` at runtime.

### CLI tool

```bash
dotnet tool install --global Corvus.Json.Cli
```

The `corvusjson` tool includes a `jsonlogic` subcommand. See [CLI code generation](#cli-code-generation) below.

## Interpreted evaluation

### Basic evaluation

Parse a rule, wrap it in a `JsonLogicRule`, and evaluate it against data:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

// Parse the rule and data
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"+":[{"var":"a"},{"var":"b"}]}"""u8);
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"a":3,"b":4}"""u8);

// Evaluate
JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(rule, dataDoc.RootElement);

// result is a number element with value 7
Console.WriteLine(result.GetRawText()); // "7"
```

When you omit the workspace, the evaluator creates one internally and returns a cloned, self-contained result.

### Caller-managed workspace

For zero-allocation evaluation on the hot path, provide your own `JsonWorkspace`:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

JsonElement result = JsonLogicEvaluator.Default.Evaluate(
    rule, dataDoc.RootElement, workspace);
```

When you provide a workspace, the result element is backed by the workspace's memory. It remains valid until the workspace is disposed or reset.

## Source generator

For rules known at build time, the source generator eliminates all runtime compilation overhead. Declare a partial class annotated with `[JsonLogicRule]`, pointing at a JSON rule file:

```csharp
using Corvus.Text.Json.JsonLogic;

namespace MyApp.Rules;

[JsonLogicRule("Rules/discount-rule.json")]
internal static partial class DiscountRule
{
}
```

Include the JSON rule file as an `AdditionalFiles` item in your `.csproj`:

```xml
<ItemGroup>
  <AdditionalFiles Include="Rules\discount-rule.json" />
</ItemGroup>
```

And reference the source generator:

```xml
<ItemGroup>
  <PackageReference Include="Corvus.Text.Json.JsonLogic.SourceGenerator"
                    PrivateAssets="all"
                    ReferenceOutputAssembly="false"
                    OutputItemType="Analyzer" />
  <PackageReference Include="Corvus.Text.Json" />
  <PackageReference Include="Corvus.Text.Json.JsonLogic" />
</ItemGroup>
```

At build time, the generator reads `discount-rule.json` and emits an `Evaluate` method on the partial class:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"price":100,"memberLevel":"gold"}""");

using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = DiscountRule.Evaluate(dataDoc.RootElement, workspace);
```

The generated `Evaluate` method is a static method that directly evaluates the rule without delegate dispatch. Literal sub-expressions are constant-folded at build time using `BigNumber` arithmetic.

### Diagnostic messages

| Code | Severity | Description |
|------|----------|-------------|
| JLSG001 | Error | Rule file not found in `AdditionalFiles` |
| JLSG002 | Error | Rule file is empty |
| JLSG003 | Error | Code generation failed (invalid JSON or unsupported operator) |

## CLI code generation

The `corvusjson` CLI tool includes a `jsonlogic` subcommand for ahead-of-time code generation:

```bash
corvusjson jsonlogic <ruleFile> \
    --className <ClassName> \
    --namespace <Namespace> \
    [--outputPath <output.cs>]
```

| Argument | Required | Description |
|----------|----------|-------------|
| `<ruleFile>` | Yes | Path to the JSON rule file |
| `--className` | Yes | Name of the generated static class |
| `--namespace` | Yes | Namespace for the generated class |
| `--outputPath` | No | Output file path (defaults to `<ClassName>.cs`) |

Example:

```bash
corvusjson jsonlogic rules/pricing.json \
    --className PricingRule \
    --namespace MyApp.Rules \
    --outputPath Generated/PricingRule.cs
```

This produces a self-contained `.cs` file with a static `Evaluate` method, identical to what the source generator produces. Use the CLI tool when:

- You need to generate code outside the MSBuild pipeline
- You want to inspect or modify the generated code before committing it
- You are integrating with a non-.NET build system

## Supported operators

The implementation supports all standard [JsonLogic operators](https://jsonlogic.com/operations.html):

### Data access

| Operator | Description | Example |
|----------|-------------|---------|
| `var` | Access a data value by path | `{"var":"user.name"}` |
| `missing` | Return array of missing keys | `{"missing":["a","b"]}` |
| `missing_some` | Require at least N of the listed keys | `{"missing_some":[1,["a","b","c"]]}` |

### Logic

| Operator | Description | Example |
|----------|-------------|---------|
| `if` / `?:` | Conditional (if/then/else chain) | `{"if":[cond, then, else]}` |
| `and` | Short-circuit logical AND | `{"and":[true, {"var":"x"}]}` |
| `or` | Short-circuit logical OR | `{"or":[false, {"var":"x"}]}` |
| `!` | Logical NOT | `{"!":[true]}` |
| `!!` | Double NOT (coerce to boolean) | `{"!!":[""]}` |

### Comparison

| Operator | Description | Example |
|----------|-------------|---------|
| `==` | Loose equality (with type coercion) | `{"==":[1,"1"]}` |
| `===` | Strict equality (no coercion) | `{"===":[1,1]}` |
| `!=` | Loose inequality | `{"!=":[1,2]}` |
| `!==` | Strict inequality | `{"!==":[1,"1"]}` |
| `<` | Less than (supports 3-arg between) | `{"<":[1,2,3]}` |
| `<=` | Less than or equal | `{"<=":[1,1]}` |
| `>` | Greater than | `{">":[2,1]}` |
| `>=` | Greater than or equal | `{">=":[2,2]}` |

### Arithmetic

| Operator | Description | Example |
|----------|-------------|---------|
| `+` | Addition (n-ary) | `{"+":[1,2,3]}` |
| `-` | Subtraction or unary negation | `{"-":[10,3]}` |
| `*` | Multiplication (n-ary) | `{"*":[2,3,4]}` |
| `/` | Division | `{"/":[10,3]}` |
| `%` | Modulo | `{"%":[10,3]}` |
| `min` | Minimum of values | `{"min":[1,2,3]}` |
| `max` | Maximum of values | `{"max":[1,2,3]}` |

### String

| Operator | Description | Example |
|----------|-------------|---------|
| `cat` | Concatenation | `{"cat":["hello"," ","world"]}` |
| `in` | Substring test or array membership | `{"in":["a","abc"]}` |
| `substr` | Substring extraction | `{"substr":["hello",1,3]}` |

### Array

| Operator | Description | Example |
|----------|-------------|---------|
| `filter` | Filter array by predicate | `{"filter":[data, test]}` |
| `map` | Transform each element | `{"map":[data, transform]}` |
| `reduce` | Fold array to single value | `{"reduce":[data, reducer, initial]}` |
| `merge` | Flatten/concatenate arrays | `{"merge":[[1],[2],[3]]}` |
| `all` | All elements satisfy predicate | `{"all":[data, test]}` |
| `some` | At least one element matches | `{"some":[data, test]}` |
| `none` | No elements match | `{"none":[data, test]}` |

### Miscellaneous

| Operator | Description | Example |
|----------|-------------|---------|
| `log` | Pass-through (for debugging) | `{"log":{"var":"x"}}` |

## Extended operators

The Corvus implementation adds operators for explicit numeric type conversion, useful when working with `BigNumber` precision:

| Operator | Description |
|----------|-------------|
| `asDouble` | Coerce a value to a double-precision number |
| `asLong` | Coerce a value to a 64-bit integer |
| `asBigNumber` | Coerce a value to arbitrary-precision `BigNumber` |
| `asBigInteger` | Coerce a value to arbitrary-precision `BigInteger` (truncated) |

These operators are not part of the standard [JsonLogic](https://jsonlogic.com/) specification but are safe to use with any evaluation mode (interpreted, source generator, or CLI).

## Runtime custom operators (IOperatorCompiler)

The interpreted evaluator supports user-defined operators at runtime via the `IOperatorCompiler` interface. This lets you extend the operator set — or override built-in operators — without code generation.

### Overview

When the evaluator compiles a rule, it walks the JSON tree and produces a delegate tree. For each operator it encounters, it looks up the operator name in a dispatch table. Custom operators are checked **before** the built-in operators, so you can override standard behaviour such as `+`, `var`, or `if`.

### Implementing an operator

Implement `IOperatorCompiler`. The `Compile` method receives the pre-compiled operand delegates and returns a single `RuleEvaluator` delegate that applies the operator:

```csharp
using Corvus.Text.Json.JsonLogic;

public sealed class DoubleItCompiler : IOperatorCompiler
{
    public RuleEvaluator Compile(RuleEvaluator[] operands)
    {
        // Capture the single operand
        RuleEvaluator operand = operands[0];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult val = operand(data, workspace);
            if (val.TryGetDouble(out double d))
            {
                return EvalResult.FromDouble(d * 2);
            }

            return EvalResult.FromDouble(0);
        };
    }
}
```

### Registering operators

Pass a dictionary of operator name → compiler to the `JsonLogicEvaluator` constructor:

```csharp
var customOps = new Dictionary<string, IOperatorCompiler>
{
    ["double_it"] = new DoubleItCompiler(),
};

JsonLogicEvaluator evaluator = new(customOps);
```

Then use the evaluator as normal. Rules can reference the custom operator by name:

```csharp
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"double_it":[{"var":"x"}]}""");
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"x":5}""");

JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = evaluator.Evaluate(rule, dataDoc.RootElement);
// result = 10
```

Custom operators compose freely with built-in operators:

```csharp
// {"+":[{"double_it":[{"var":"x"}]}, {"var":"y"}]}
// → double_it(3) + 4 = 6 + 4 = 10
```

### Overriding built-in operators

Because custom operators are dispatched first, you can replace any standard operator:

```csharp
var customOps = new Dictionary<string, IOperatorCompiler>
{
    ["+"] = new AlwaysFortyTwoCompiler(),
};

JsonLogicEvaluator evaluator = new(customOps);
// All "+" rules now return 42, regardless of operands
```

### Key types

| Type | Description |
|------|-------------|
| `IOperatorCompiler` | Interface to implement. Single method: `RuleEvaluator Compile(RuleEvaluator[] operands)` |
| `RuleEvaluator` | Delegate: `EvalResult RuleEvaluator(in JsonElement data, JsonWorkspace workspace)` |
| `EvalResult` | Discriminated union — holds either a native `double` or a `JsonElement` |
| `JsonLogicEvaluator` | Evaluator. Use the constructor overload accepting `IReadOnlyDictionary<string, IOperatorCompiler>` |

### Working with EvalResult

`EvalResult` is a dual-track value type that avoids the double → JSON → double round-trip for arithmetic:

| Method | Description |
|--------|-------------|
| `EvalResult.FromDouble(double)` | Create a result wrapping a native double |
| `EvalResult.FromElement(in JsonElement)` | Create a result wrapping a JSON element |
| `TryGetDouble(out double)` | Attempt to extract a double (coercing from JSON number/bool/string if needed) |
| `AsElement(JsonWorkspace)` | Materialize the result as a `JsonElement` (writing the double to the workspace if necessary) |
| `IsTruthy()` | [JsonLogic truthiness](https://jsonlogic.com/truthy.html) test |
| `IsNullOrUndefined()` | True for JSON `null` or `Undefined` |
| `ValueKind` | `JsonValueKind.Number` for doubles; the element's kind otherwise |

When implementing operators that do arithmetic, prefer the double fast path and fall back to `BigNumber` for arbitrary-precision values:

```csharp
using Corvus.Numerics;

public sealed class AddCompiler : IOperatorCompiler
{
    public RuleEvaluator Compile(RuleEvaluator[] operands)
    {
        if (operands.Length < 2)
        {
            // Too few arguments — return zero (see error handling below)
            return static (in JsonElement data, JsonWorkspace workspace) =>
                EvalResult.FromDouble(0);
        }

        RuleEvaluator left = operands[0];
        RuleEvaluator right = operands[1];

        return (in JsonElement data, JsonWorkspace workspace) =>
        {
            EvalResult l = left(data, workspace);
            EvalResult r = right(data, workspace);

            // Fast path: both operands fit in a double
            if (l.TryGetDouble(out double a) && r.TryGetDouble(out double b))
            {
                return EvalResult.FromDouble(a + b);
            }

            // Slow path: fall back to BigNumber for full precision
            BigNumber lb = CoerceToBigNumber(l.AsElement(workspace));
            BigNumber rb = CoerceToBigNumber(r.AsElement(workspace));
            return EvalResult.FromElement(
                BigNumberToElement(lb + rb, workspace));
        };
    }

    private static BigNumber CoerceToBigNumber(in JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number)
        {
            using var raw = JsonMarshal.GetRawUtf8Value(element);
            if (BigNumber.TryParse(raw.Span, out BigNumber result))
            {
                return result;
            }
        }

        if (element.ValueKind == JsonValueKind.True) return BigNumber.One;
        if (element.ValueKind == JsonValueKind.False
            || element.ValueKind == JsonValueKind.Null) return BigNumber.Zero;

        return BigNumber.Zero;
    }

    private static JsonElement BigNumberToElement(
        BigNumber value, JsonWorkspace workspace)
    {
        Span<byte> buffer = stackalloc byte[64];
        if (value.TryFormat(buffer, out int bytesWritten))
        {
            return JsonLogicHelpers.NumberFromSpan(
                buffer.Slice(0, bytesWritten), workspace);
        }

        return JsonLogicHelpers.Zero();
    }
}
```

### Error handling

[JsonLogic](https://jsonlogic.com/) follows a philosophy of **graceful degradation** — operators never throw exceptions at evaluation time. All built-in operators in the Corvus implementation follow this convention, and custom operators should do the same:

| Situation | Convention | Example |
|-----------|-----------|---------|
| Too few arguments | Return `null` or `0` | `{"/":[]}` → `null` |
| Wrong type (non-numeric for arithmetic) | Coerce if possible, fall back to `0` | `{"+":[true, "3"]}` → `4` |
| Division / modulo by zero | Return `null` | `{"/":[1, 0]}` → `null` |
| Missing or undefined data | Return `null` | `{"var":"no.such.path"}` → `null` |

Use `JsonLogicHelpers.NullElement()` to return a JSON `null`, and `JsonLogicHelpers.Zero()` to return the number `0`. Both are allocation-free.

```csharp
// Too few arguments — return null
if (operands.Length < 2)
{
    return static (in JsonElement data, JsonWorkspace workspace) =>
        EvalResult.FromElement(JsonLogicHelpers.NullElement());
}

// Non-numeric operand — coerce or fall back to zero
if (!val.TryGetDouble(out double d))
{
    d = 0;
}
```

This means rules with mistakes produce deterministic fallback values rather than aborting evaluation. If your use case requires strict validation, you can throw from `Compile` (which runs once at compilation time) to reject rules with a known-wrong number of operands — but the evaluation delegate itself should not throw.

When your operator produces a non-numeric result, use `AsElement` to get operand values and `FromElement` to return a `JsonElement`:

```csharp
return (in JsonElement data, JsonWorkspace workspace) =>
{
    JsonElement value = operands[0](data, workspace).AsElement(workspace);
    // ... produce a JsonElement result ...
    return EvalResult.FromElement(result);
};
```

### Zero-argument operators

Operators that take no arguments are supported — the `operands` array will be empty:

```csharp
public sealed class PiCompiler : IOperatorCompiler
{
    public RuleEvaluator Compile(RuleEvaluator[] operands)
    {
        return static (in JsonElement data, JsonWorkspace workspace) =>
            EvalResult.FromDouble(3.14159265358979);
    }
}
```

Rule: `{"pi":[]}` — the empty array is required by the [JsonLogic](https://jsonlogic.com/) format.

### Caching

Compiled rules are cached by the evaluator. A custom operator's `Compile` method is called once per unique rule text, not once per evaluation. The returned `RuleEvaluator` delegate is invoked on every evaluation. This means:

- Expensive setup in `Compile` is acceptable — it runs at compilation time.
- The returned delegate should be allocation-free on the hot path.
- Any data captured by the delegate's closure — including the `operands` array and any values derived from it — is also cached for the lifetime of the evaluator (or until `ClearCache()` is called). This is by design: the operand delegates form the compiled expression tree and are reused across evaluations with different data.

## Custom operator templates (.jlops)

The code generator and source generator support user-defined operators via `.jlops` template files. Custom operators extend the standard operator set with C# expression or block bodies that are emitted directly into the generated code.

### Format

A `.jlops` file contains one or more operator definitions. Two forms are supported:

**Expression form** (single line):

```
op discount(price, percent) => BigNumberToElement(CoerceToBigNumber(price) * (BigNumber.One - CoerceToBigNumber(percent) / (BigNumber)100), workspace);
```

**Block form** (multi-line):

```
op clamp(value, lo, hi)
{
    BigNumber v = CoerceToBigNumber(value);
    BigNumber low = CoerceToBigNumber(lo);
    BigNumber high = CoerceToBigNumber(hi);
    BigNumber clamped = v < low ? low : v > high ? high : v;
    return BigNumberToElement(clamped, workspace);
}
```

Lines starting with `//` are comments. Blank lines are ignored.

Each parameter receives a `JsonElement` value at runtime. The implicit `workspace` parameter is always available. The body has access to all helper methods in the generated class, including `CoerceToBigNumber()`, `BigNumberToElement()`, and `JsonLogicHelpers.*`.

### Using with the source generator

Include `.jlops` files as `AdditionalFiles` alongside your JSON rule files:

```xml
<ItemGroup>
  <AdditionalFiles Include="Rules\*.json" />
  <AdditionalFiles Include="Operators\*.jlops" />
</ItemGroup>
```

All custom operators from all `.jlops` files are available to all `[JsonLogicRule]`-annotated types in the project. Use the custom operator in a rule just like a built-in one:

```json
{"+":[{"discount":[{"var":"price"}, 20]}, {"var":"tax"}]}
```

### Using with the CLI tool

Pass the `--operators` flag pointing to a `.jlops` file:

```bash
corvusjson jsonlogic rules/pricing.json \
    --className PricingRule \
    --namespace MyApp.Rules \
    --operators operators/custom-ops.jlops
```

### Argument count validation

The code generator validates that each use of a custom operator passes the exact number of arguments declared in the `.jlops` definition. A mismatch produces a build error (source generator) or an exception (CLI tool).

### Diagnostic messages

| Code | Severity | Description |
|------|----------|-------------|
| JLSG004 | Error | Failed to parse a `.jlops` file (invalid syntax) |

## Workspace and memory management

`JsonWorkspace` is the pooled memory container used throughout Corvus.Text.Json. When evaluating JsonLogic rules:

- **With workspace**: Results are allocated from the workspace. No heap allocations on the hot path. The caller is responsible for disposing the workspace.
- **Without workspace**: The evaluator creates a temporary workspace internally and clones the result before disposing it.

For best performance in tight loops or request-processing pipelines, reuse a workspace and call `Reset()` between iterations:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (var request in requests)
{
    workspace.Reset();
    using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(request.Json);
    JsonElement result = JsonLogicEvaluator.Default.Evaluate(
        rule, dataDoc.RootElement, workspace);
    ProcessResult(result);
}
```

This pattern achieves **zero GC allocation** per evaluation for most rules. The workspace pools memory internally via `ArrayPool<byte>` and reuses it across evaluations.

**Clearing the expression cache:**

The evaluator caches compiled delegate trees per rule. If your application dynamically generates many unique rules over time, the cache can grow unbounded. Use `ClearCache()` to release all cached compilations:

```csharp
var evaluator = new JsonLogicEvaluator();
// ... many evaluations with unique rules ...

evaluator.ClearCache(); // releases all cached delegate trees
```

## Common pitfalls

### Always dispose `ParsedJsonDocument`

`ParsedJsonDocument<T>` rents memory from `ArrayPool<byte>`. Forgetting to dispose it leaks pooled memory:

```csharp
// ❌ BAD — leaks pooled memory
var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Evaluate(rule, doc.RootElement);

// ✅ GOOD — using statement returns memory to the pool
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
var result = evaluator.Evaluate(rule, doc.RootElement);
```

### Reset the workspace in loops

Without `Reset()`, workspace memory grows with each evaluation. The workspace remains valid, but old results consume pooled memory unnecessarily:

```csharp
// ❌ BAD — workspace grows unboundedly
foreach (var item in items)
{
    var result = evaluator.Evaluate(rule, item, workspace);
    ProcessResult(result);
}

// ✅ GOOD — reset frees previous results
foreach (var item in items)
{
    workspace.Reset();
    var result = evaluator.Evaluate(rule, item, workspace);
    ProcessResult(result);
}
```

### Don't forget `AdditionalFiles` for the source generator

The source generator reads rule files from `AdditionalFiles`. Without the MSBuild item, the generator can't find the rule and produces diagnostic `JLSG001`:

```xml
<!-- ❌ Missing — generator produces JLSG001 -->
<ItemGroup>
  <None Include="Rules\discount.json" />
</ItemGroup>

<!-- ✅ Correct -->
<ItemGroup>
  <AdditionalFiles Include="Rules\discount.json" />
</ItemGroup>
```

### Result lifetime is tied to the workspace

When you pass a workspace, the returned `JsonElement` is backed by that workspace's memory. Using the result after the workspace is disposed or reset produces undefined behavior:

```csharp
// ❌ BAD — result is invalid after workspace disposal
JsonElement result;
using (JsonWorkspace workspace = JsonWorkspace.Create())
{
    result = evaluator.Evaluate(rule, data, workspace);
}
Console.WriteLine(result.GetRawText()); // undefined behavior

// ✅ GOOD — use result before workspace is disposed
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = evaluator.Evaluate(rule, data, workspace);
Console.WriteLine(result.GetRawText()); // safe
```

## Comparison with other libraries

The Corvus JsonLogic implementation is designed for high-throughput scenarios where rules are evaluated millions of times. Key differences from other .NET implementations:

| Feature | Corvus.Text.Json.JsonLogic | JsonEverything |
|---------|---------------------------|----------------|
| Evaluation model | Compiled delegate tree (cached) | Direct interpretation |
| Code generation | Source generator + CLI tool | Not available |
| Numeric precision | `BigNumber` (arbitrary precision) | `decimal` |
| Memory model | Pooled (`JsonWorkspace`) | GC-allocated |
| Zero-allocation hot path | Yes (with workspace) | No |
| Extended operators | `asDouble`, `asLong`, `asBigNumber`, `asBigInteger` | Custom operator API |
| Runtime extensibility | `IOperatorCompiler` (can override built-ins) | Custom operator API |
| Code-gen extensibility | `.jlops` custom operator templates | Not available |

### Benchmark summary

Measured on .NET 10.0 (13th Gen Intel Core i7-13800H) across 19 scenarios. **RT** = Corvus runtime (interpreted), **CG** = Corvus code-gen (source generator), **JE** = JsonEverything. Ratios < 1 mean faster than JE; lower is better.

#### Time comparison

| Scenario | JE (ns) | RT (ns) | CG (ns) | RT/JE | CG/JE |
|---|---:|---:|---:|---:|---:|
| Simple var | 64 | 18 | 16 | 0.29 | 0.25 |
| Comparison | 223 | 65 | 34 | 0.29 | 0.15 |
| Arithmetic | 332 | 113 | 98 | 0.34 | 0.30 |
| String cat | 244 | 129 | 115 | 0.53 | 0.47 |
| Substr | 305 | 94 | 93 | 0.31 | 0.30 |
| Min/max | 48 | 668 | 405 | **14.0** | **8.5** |
| In (array) | 457 | 115 | 106 | 0.25 | 0.23 |
| Logic short-circuit | 620 | 306 | 226 | 0.49 | 0.36 |
| Quantifier (all) | 1,179 | 227 | 190 | 0.19 | 0.16 |
| Complex rule | 828 | 219 | 160 | 0.26 | 0.19 |
| Deep nested | 4,936 | 110 | 103 | 0.02 | 0.02 |
| Missing data | 1,361 | 218 | 184 | 0.16 | 0.14 |
| Merge (constant) | 1,725 | 390 | 12 | 0.23 | **0.007** |
| Merge (mixed) | 1,604 | 462 | 509 | 0.29 | 0.32 |
| Reduce (strings) | 2,640 | 2,058 | 2,811 | 0.78 | 1.06 |
| Array filter | 1,839 | 758 | 386 | 0.41 | 0.21 |
| Object filter | 2,359 | 644 | 543 | 0.27 | 0.23 |
| Map (strings) | 1,726 | 735 | 710 | 0.43 | 0.41 |
| Array map/reduce | 5,227 | 563 | 278 | 0.11 | 0.05 |

#### Memory comparison

| Scenario | JE (B) | RT (B) | CG (B) |
|---|---:|---:|---:|
| Simple var | 248 | 0 | 0 |
| Comparison | 512 | 0 | 0 |
| Arithmetic | 656 | 0 | 0 |
| String cat | 728 | 0 | 0 |
| Substr | 560 | 0 | 0 |
| Min/max | 136 | 0 | 0 |
| In (array) | 1,472 | 0 | 0 |
| Logic short-circuit | 1,304 | 0 | 0 |
| Quantifier (all) | 2,776 | 0 | 0 |
| Complex rule | 2,040 | 0 | 0 |
| Deep nested | 10,120 | 0 | 0 |
| Missing data | 2,184 | 0 | 0 |
| Merge (constant) | 4,376 | 120 | 0 |
| Merge (mixed) | 3,800 | 120 | 120 |
| Reduce (strings) | 9,368 | 120 | 120 |
| Array filter | 4,032 | 120 | 120 |
| Object filter | 6,280 | 120 | 120 |
| Map (strings) | 2,912 | 120 | 120 |
| Array map/reduce | 12,856 | 0 | 0 |

#### Summary

- **RT is faster than JE in 18/19 scenarios** (0.02×–0.78× JE), with a geometric mean of **0.22× JE** across those 18.
- **CG is faster than JE in 17/19 scenarios** (0.007×–0.47× JE), with a geometric mean of **0.16× JE** across those 17.
- **Min/max** is the notable outlier where JE is faster: JE's `JsonNode` stores pre-parsed `double` values, while Corvus's `JsonElement` re-parses UTF-8 bytes to `double` on each call. Both RT and CG still allocate 0 B vs JE's 136 B.
- **Reduce (strings)** is at parity for CG (1.06× JE); RT is faster at 0.78×.
- **Memory**: RT allocates 0 B in 13/19 scenarios, CG in 14/19. The remaining scenarios allocate exactly 120 B (a single `ElementBuffer` array). JE allocates 136–12,856 B in every scenario.
- **Merge (constant)** showcases compile-time constant folding: CG evaluates at 12 ns (0.007× JE) by pre-computing the entire merged array as a static field.