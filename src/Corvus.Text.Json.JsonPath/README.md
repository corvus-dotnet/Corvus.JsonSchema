# Corvus.Text.Json.JsonPath

JSONPath (RFC 9535) query language evaluator for Corvus.Text.Json.

Provides a high-performance, zero-allocation compiled-delegate evaluator for
JSONPath expressions. Compiled expressions are cached and reused automatically.

## Usage

```csharp
using Corvus.Text.Json.JsonPath;

JsonElement data = JsonElement.ParseValue("""{"store":{"book":[{"title":"A"},{"title":"B"}]}}"""u8);
JsonElement result = JsonPathEvaluator.Default.Query("$.store.book[*].title", data);
// result is ["A","B"]
```

## Packages

| Package | Description |
|---------|-------------|
| `Corvus.Text.Json.JsonPath` | Runtime evaluator (this package) |
| `Corvus.Text.Json.JsonPath.CodeGeneration` | Expression → C# source code |
| `Corvus.Text.Json.JsonPath.SourceGenerator` | Roslyn incremental source generator |
