using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Gap A2: an <c>if</c>/<c>then</c>/<c>else</c> subschema is a validator-only construct (applied in place to
/// the parent value), so it must NOT be reified as a named interface on the consumer's type surface — only its
/// <c>evaluate{Type}</c> is emitted (the ternary-if validator calls it). The parent interface, which carries the
/// merged conditional properties, is unchanged.
/// </summary>
[TestClass]
public class TypeScriptConditionalSurfaceTests
{
    private const string CondSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Cond",
          "type": "object",
          "properties": { "kind": { "type": "string" } },
          "if": { "properties": { "kind": { "const": "a" } } },
          "then": { "properties": { "extra": { "type": "string" } } },
          "else": { "properties": { "other": { "type": "number" } } }
        }
        """;

    [TestMethod]
    public async Task IfThenElse_SubschemasAreValidatorOnly_NotReifiedAsInterfaces()
    {
        string content = await GenerateTypeScript(CondSchema);

        // The parent interface IS emitted (it surfaces the merged conditional properties)...
        StringAssert.Contains(content, "export interface Cond");

        // ...and it is the ONLY interface: the if/then/else object subschemas (each an object that would
        // otherwise reify to If/Then/Else) are suppressed, not added to the type surface.
        int interfaceCount = content.Split("export interface ").Length - 1;
        Assert.AreEqual(1, interfaceCount, "Only the parent Cond interface should be emitted; conditionals must be validator-only.\n" + content);

        // The conditional logic is still validated — the parent validator and the entry point are present.
        StringAssert.Contains(content, "export function evaluateCond");
        StringAssert.Contains(content, "export const evaluateRoot");
    }

    // Build the schema's type declarations and emit TypeScript (single-file, default options), returning the
    // generated module with the evaluateRoot entry point appended (the two steps the CLI driver performs).
    private static async Task<string> GenerateTypeScript(string schemaContent)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ts-a2-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(tempFile, schemaContent);

            CompoundDocumentResolver documentResolver = new(
                new FileSystemDocumentResolver(),
                new HttpClientDocumentResolver(new HttpClient()));

            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

            JsonReference reference = new(tempFile);
            TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(
                reference,
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary);

            TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.DefaultWithOptions(
                new TypeScriptLanguageProvider.Options());

            IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
                provider,
                [rootType],
                CancellationToken.None);

            GeneratedCodeFile generated = files.First(f => f.FileName == "generated.ts");
            return generated.FileContent + provider.RootEvaluatorExport(rootType);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }
}
