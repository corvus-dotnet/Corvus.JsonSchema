# Corvus.Text.Json.JsonPath.SourceGenerator

Roslyn incremental source generator that produces optimized C# evaluation code from JSONPath (RFC 9535) expression files at build time.

## Usage

1. Add a `.jsonpath` file to your project with the expression:
   ```
   $.store.book[*].author
   ```

2. Include it as an `AdditionalFiles` item:
   ```xml
   <AdditionalFiles Include="Expressions\*.jsonpath" />
   ```

3. Annotate a partial type:
   ```csharp
   [JsonPathExpression("Expressions/book-authors.jsonpath")]
   internal static partial class BookAuthors;
   ```

4. Use the generated `Evaluate` method:
   ```csharp
   using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
   using var workspace = JsonWorkspace.Create();
   JsonElement result = BookAuthors.Evaluate(doc.RootElement, workspace);
   ```
