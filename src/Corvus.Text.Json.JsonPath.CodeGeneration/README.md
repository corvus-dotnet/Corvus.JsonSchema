# Corvus.Text.Json.JsonPath.CodeGeneration

Code generation engine for Corvus.Text.Json.JsonPath — parses JSONPath (RFC 9535) expressions and emits optimized C# evaluation code.

## Usage

```csharp
string source = JsonPathCodeGenerator.Generate(
    "$.store.book[*].author",
    "BookAuthors",
    "MyNamespace");

// source is a complete C# file with:
// public static class BookAuthors
// {
//     public static JsonElement Evaluate(in JsonElement data, JsonWorkspace workspace) { ... }
// }
```

This package is used by the `Corvus.Text.Json.JsonPath.SourceGenerator` Roslyn source generator to produce build-time-compiled JSONPath evaluators.
