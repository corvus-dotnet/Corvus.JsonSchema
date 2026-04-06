# JsonLogic Rule Engine

## Overview

`Corvus.Text.Json.JsonLogic` implements [JsonLogic](https://jsonlogic.com/) for the Corvus.Text.Json document model — a safe, side-effect-free rule engine that evaluates JSON-encoded logic rules against JSON data.

[JsonLogic](https://jsonlogic.com/) is a standard for expressing business rules as JSON. Rules are portable, storable in databases, and safely evaluated without allowing arbitrary code execution. The Corvus implementation passes the full [official test suite](https://jsonlogic.com/tests.json) and adds support for extended numeric types (`BigNumber`) via custom operators.

Three evaluation modes are available:

| Mode | When to use | Package |
|------|-------------|---------|
| **Interpreted** | Rules are dynamic, determined at runtime | `Corvus.Text.Json.JsonLogic` |
| **Source generator** | Rules are known at build time, embedded in your project | `Corvus.Text.Json.JsonLogic.SourceGenerator` |
| **CLI code generation** | Rules are known ahead of time, generated outside the build | `Corvus.Json.CodeGenerator` (the `jsonlogic` command) |

The source generator and CLI tool produce optimized static C# that eliminates delegate dispatch and can constant-fold literal expressions. Benchmarks show generated code is **60–95% faster** than interpreted evaluation and **60–95% faster** than JsonEverything.

## Table of Contents

- [Installation](#installation)
- [Interpreted evaluation](#interpreted-evaluation)
- [Source generator](#source-generator)
- [CLI code generation](#cli-code-generation)
- [Supported operators](#supported-operators)
- [Extended operators](#extended-operators)
- [Custom operator templates (.jlops)](#custom-operator-templates-jlops)
- [Workspace and memory management](#workspace-and-memory-management)
- [Comparison with other libraries](#comparison-with-other-libraries)

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
dotnet tool install --global Corvus.Json.CodeGenerator
```

The `generatejsonschematypes` tool includes a `jsonlogic` subcommand. See [CLI code generation](#cli-code-generation) below.

## Interpreted evaluation

Parse a rule, wrap it in a `JsonLogicRule`, and evaluate it against data:

```csharp
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

// Parse the rule and data
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"+":[{"var":"a"},{"var":"b"}]}""");
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(
    """{"a":3,"b":4}""");

// Evaluate
JsonLogicRule rule = new(ruleDoc.RootElement);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(rule, dataDoc.RootElement);

// result is a number element with value 7
Console.WriteLine(result.GetRawText()); // "7"
```

The evaluator compiles the rule into a delegate tree on first use and caches it. Subsequent evaluations of the same rule skip compilation entirely.

### Caller-managed workspace

For zero-allocation evaluation on the hot path, provide your own `JsonWorkspace`:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

JsonElement result = JsonLogicEvaluator.Default.Evaluate(
    rule, dataDoc.RootElement, workspace);
```

When you provide a workspace, the result element is backed by the workspace's memory. It remains valid until the workspace is disposed.

When you omit the workspace, the evaluator creates one internally and returns a cloned, self-contained result.

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

The `generatejsonschematypes` CLI tool includes a `jsonlogic` subcommand for ahead-of-time code generation:

```bash
generatejsonschematypes jsonlogic <ruleFile> \
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
generatejsonschematypes jsonlogic rules/pricing.json \
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
generatejsonschematypes jsonlogic rules/pricing.json \
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

For best performance in tight loops or request-processing pipelines, reuse a workspace:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

foreach (var request in requests)
{
    using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(request.Json);
    JsonElement result = JsonLogicEvaluator.Default.Evaluate(
        rule, dataDoc.RootElement, workspace);
    ProcessResult(result);
}
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

### Benchmark summary

Measured on .NET 10.0 (13th Gen Intel Core i7-13800H) across 9 representative scenarios:

| Scenario | Corvus (interpreted) | Corvus (code-gen) | JsonEverything | Corvus alloc | JE alloc |
|----------|---------------------|-------------------|----------------|-------------|----------|
| Simple var access | 115 ns | 62 ns | 189 ns | 0 B | 248 B |
| Arithmetic | 870 ns | 269 ns | 970 ns | 0 B | 656 B |
| Comparison | 571 ns | 75 ns | 629 ns | 0 B | 512 B |
| String concatenation | 499 ns | 205 ns | 560 ns | 0 B | 728 B |
| Logic short-circuit | 1,493 ns | 292 ns | 1,666 ns | 0 B | 1,304 B |
| Missing data | 1,909 ns | 803 ns | 2,183 ns | 120 B | 2,184 B |
| Complex rule | 2,939 ns | 481 ns | 2,296 ns | 0 B | 2,040 B |
| Array filter | 2,390 ns | 1,873 ns | 5,189 ns | 120 B | 4,032 B |
| Array map/reduce | 2,616 ns | 725 ns | 14,124 ns | 0 B | 12,856 B |

Code-generated evaluators are **60–95% faster** than JsonEverything across all scenarios, with **zero or near-zero allocations**. The interpreted evaluator is comparable to or faster than JsonEverything in most scenarios, with consistently lower memory usage. JsonEverything allocates 248 B–12,856 B per evaluation, while both Corvus modes allocate 0 B in 7 of 9 scenarios (the two that allocate 120 B involve array construction in `missing` and `filter` results).