---
name: corvus-query-languages
description: >
  Work with JSONata, JMESPath, and JsonLogic query and transformation languages.
  Each has runtime (interpreted) and code-generated evaluation modes plus Roslyn source
  generators. Covers the JSONata evaluator API, JMESPath Search(), JsonLogic rule engine,
  conformance test suites, code generation for all three, and performance characteristics.
  USE FOR: evaluating queries and transforms, generating optimized evaluators, running
  conformance tests, understanding the dual (runtime + codegen) architecture.
  DO NOT USE FOR: JSON Schema validation (use corvus-keywords-and-validation or
  corvus-standalone-evaluator).
---

# Query Languages: JSONata, JMESPath, JsonLogic

## Architecture

Each language follows the same three-package pattern:

| Package tier | JSONata | JMESPath | JsonLogic |
|-------------|---------|----------|-----------|
| **Runtime** (interpreted) | `Corvus.Text.Json.Jsonata` | `Corvus.Text.Json.JMESPath` | `Corvus.Text.Json.JsonLogic` |
| **Code generation library** | `Corvus.Text.Json.Jsonata.CodeGeneration` | `Corvus.Text.Json.JMESPath.CodeGeneration` | `Corvus.Text.Json.JsonLogic.CodeGeneration` |
| **Source generator** | `Corvus.Text.Json.Jsonata.SourceGenerator` | `Corvus.Text.Json.JMESPath.SourceGenerator` | `Corvus.Text.Json.JsonLogic.SourceGenerator` |

## JSONata

**Conformance:** 100% (1,665 tests)
**Performance:** Up to 8× faster than Jsonata.Net.Native (runtime), up to 12× (code-generated)

### Runtime Evaluation
```csharp
using var doc = ParsedJsonDocument<JsonElement>.Parse(data);
JsonElement result = JsonataEvaluator.Default.Evaluate(expression, doc.RootElement);
```

### Code-Generated Evaluation
```csharp
// Source generator takes a .jsonata FILE path, not an inline expression
[JsonataExpression("expressions/total-price.jsonata")]
internal static partial class TotalPrice;

// Usage — code-gen produces a static Evaluate(in JsonElement, JsonWorkspace) method:
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = TotalPrice.Evaluate(doc.RootElement, workspace);
```

### Key Notes
- User-defined functions may shadow built-ins; compilation preserves runtime fallback
- Individual test cases have 10-second timeout for runaway recursion
- Conformance tests: `dotnet test tests\Corvus.Text.Json.Jsonata.Tests --filter "category!=failing&category!=outerloop"`
- Code-gen tests tagged: `codegen-conformance` and `codegen-edge`

## JMESPath

**Conformance:** 100% (892 tests)
**Performance:** Up to 150× faster than JmesPath.Net

### Runtime Evaluation
```csharp
using var doc = ParsedJsonDocument<JsonElement>.Parse(data);
JsonElement result = JMESPathEvaluator.Default.Search(expression, doc.RootElement);
```

### Code-Generated Evaluation
```csharp
// Source generator takes a .jmespath FILE path, not an inline expression
[JMESPathExpression("expressions/wa-locations.jmespath")]
internal static partial class WashingtonLocations;

// Usage — code-gen produces a static Evaluate(in JsonElement, JsonWorkspace) method:
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = WashingtonLocations.Evaluate(doc.RootElement, workspace);
```

## JsonLogic

Complete rule engine with all standard operations.

### Runtime Evaluation
```csharp
using var ruleDoc = ParsedJsonDocument<JsonElement>.Parse(ruleJson);
JsonLogicRule rule = new(ruleDoc.RootElement);
using var dataDoc = ParsedJsonDocument<JsonElement>.Parse(dataJson);
JsonElement result = JsonLogicEvaluator.Default.Evaluate(rule, dataDoc.RootElement);
```

### Code-Generated Evaluation
```csharp
// Source generator takes a .json FILE path containing the rule, not an inline expression
[JsonLogicRule("rules/conditional.json")]
internal static partial class ConditionalRule;

// Usage — code-gen produces a static Evaluate(in JsonElement, JsonWorkspace) method:
using JsonWorkspace workspace = JsonWorkspace.Create();
JsonElement result = ConditionalRule.Evaluate(doc.RootElement, workspace);
```

### Thread Safety Warning
`JsonLogicEvaluator` (including the `Default` singleton) has mutable last-rule cache fields (`_lastCompiled`, `_lastRuleIdentity`) that are not synchronized. The `ConcurrentDictionary` cache is thread-safe, but the fast-path identity check is not. Concurrent calls on the same instance may produce correct results but with degraded cache performance (benign races). If strict single-evaluation-at-a-time fast-path caching matters, use separate instances or external synchronization.

## Running Tests

```powershell
# JSONata conformance
dotnet test tests\Corvus.Text.Json.Jsonata.Tests --filter "category!=failing&category!=outerloop"

# JSONata code-gen
dotnet test tests\Corvus.Text.Json.Jsonata.CodeGeneration.Tests --filter "category!=failing&category!=outerloop"

# JMESPath conformance
dotnet test tests\Corvus.Text.Json.JMESPath.Tests --filter "category!=failing&category!=outerloop"

# JsonLogic conformance
dotnet test tests\Corvus.Text.Json.JsonLogic.Tests --filter "category!=failing&category!=outerloop"
```

## Common Pitfalls

- **JsonLogic thread safety**: The `Default` singleton's fast-path cache fields (`_lastCompiled`, `_lastRuleIdentity`) are not atomic. Concurrent use is functionally safe (falls back to `ConcurrentDictionary`) but the fast-path may thrash. Use separate instances if this matters.
- **JSONata timeout**: Complex expressions may hit the 10-second test timeout.
- **Code-gen vs runtime**: Code-generated evaluators are pre-compiled and faster, but less flexible for dynamic expressions.

## Cross-References
- For benchmarking query languages, see `corvus-benchmarks`
- For building/testing, see `corvus-build-and-test`
- Full guides: `docs/Jsonata.md`, `docs/JMESPath.md`, `docs/JsonLogic.md`
