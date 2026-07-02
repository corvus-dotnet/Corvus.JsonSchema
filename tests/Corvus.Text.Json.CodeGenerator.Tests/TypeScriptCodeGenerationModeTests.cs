using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Gap D3: the TypeScript engine honours the CLI <c>--codeGenerationMode</c>. The default
/// (<c>TypeGeneration</c>/<c>Both</c>) emits the idiomatic type surface (interfaces / aliases + the
/// build/patch/accessor helpers) alongside the <c>evaluate{Type}</c> validators; <c>SchemaEvaluationOnly</c>
/// emits a validators-only module — the same evaluators (and the <c>evaluateRoot</c> entry point) with NO
/// type surface. The validators never reference the type surface, so they stand alone in either mode.
/// </summary>
[TestClass]
public class TypeScriptCodeGenerationModeTests
{
    // An object (-> interface) carrying an inline enum property (-> a `type X = "a" | "b"` alias), so BOTH
    // type-surface forms are present in type-generation mode and their absence is meaningful in eval-only mode.
    private const string WidgetSchema = """
        {
          "$schema": "https://json-schema.org/draft/2020-12/schema",
          "title": "Widget",
          "type": "object",
          "required": [ "id", "kind" ],
          "properties": {
            "id": { "type": "string" },
            "kind": { "type": "string", "enum": [ "a", "b" ] }
          }
        }
        """;

    [TestMethod]
    public async Task TypeScript_TypeGeneration_EmitsTypeSurfaceAndValidators()
    {
        string content = await GenerateTypeScript(WidgetSchema, emitTypeSurface: true);

        // The default mode emits the full idiomatic surface...
        StringAssert.Contains(content, "export interface ", "TypeGeneration must emit the object interface.");
        StringAssert.Contains(content, "export type ", "TypeGeneration must emit the inline enum's type alias.");

        // ...plus the (module-internal) validators, the companion objects, and the default-export entry point.
        StringAssert.Contains(content, "function evaluate");
        StringAssert.Contains(content, "export const ");
        StringAssert.Contains(content, "export default ");
    }

    [TestMethod]
    public async Task TypeScript_SchemaEvaluationOnly_EmitsValidatorsWithoutTypeSurface()
    {
        string content = await GenerateTypeScript(WidgetSchema, emitTypeSurface: false);

        // SchemaEvaluationOnly keeps every (module-internal) validator, the companions, and the entry point...
        StringAssert.Contains(content, "function evaluate");
        StringAssert.Contains(content, "export default ");

        // ...but suppresses the type surface entirely (no interface, no enum/union/brand/map/array alias
        // *declaration*). Runtime plumbing that carries type names is allowed: the import line has
        // `import { ... type Draft ... }`, and the module re-exports the JSON Patch types with
        // `export type { JsonPatch, JsonPatchOp };` — a type-only re-export, not a type alias. So match an
        // alias *declaration* (`export type <Name> = ...`) rather than the bare `export type ` substring.
        Assert.IsFalse(
            content.Contains("export interface", StringComparison.Ordinal),
            "SchemaEvaluationOnly must not emit an interface. Generated:\n" + content);
        Assert.IsFalse(
            System.Text.RegularExpressions.Regex.IsMatch(content, "export type [A-Za-z_$]"),
            "SchemaEvaluationOnly must not emit a type-alias declaration. Generated:\n" + content);
    }

    // Build the schema's type declarations and emit TypeScript via the provider in the requested mode,
    // returning the generated module's source with the `evaluateRoot` entry point appended (mirroring the
    // two-step the CLI driver performs: GenerateCodeFor + RootEvaluatorExport).
    private static async Task<string> GenerateTypeScript(string schemaContent, bool emitTypeSurface)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ts-mode-{Guid.NewGuid():N}.json");
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
                new TypeScriptLanguageProvider.Options(EmitTypeSurface: emitTypeSurface));

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
