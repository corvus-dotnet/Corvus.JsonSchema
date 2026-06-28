using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Gap A1 (§7.4): with <see cref="TypeScriptLanguageProvider.Options.ModulePerType"/> the TypeScript engine
/// emits one <c>.ts</c> module per generated type plus a barrel <c>index.ts</c> that re-exports them, instead
/// of a single <c>generated.ts</c>. Each module imports the sibling-module identifiers it references (a
/// validator calls its children's <c>evaluate{Type}</c>; an interface references its children's type names),
/// and the barrel carries the <c>evaluateRoot</c> entry point. The default (single-file) output is unchanged.
/// </summary>
[TestClass]
public class TypeScriptModulePerTypeTests
{
    // An object (-> Widget.ts interface) with a string property (-> Id.ts, validator only) and an inline enum
    // property (-> Kind.ts, `type Kind` alias). Widget's interface references Kind and its validator calls
    // evaluateKind / evaluateId, so the module-per-type output must wire cross-module imports.
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
    public async Task ModulePerType_EmitsOneModulePerTypePlusBarrelAndNoSingleFile()
    {
        IReadOnlyDictionary<string, string> files = await GenerateModulePerType(WidgetSchema);

        // One module per generated type + the barrel + the self-contained runtime; no single generated.ts.
        Assert.IsTrue(files.ContainsKey("index.ts"), "Expected a barrel index.ts. Files: " + string.Join(", ", files.Keys));
        Assert.IsTrue(files.ContainsKey("Widget.ts"), "Expected a per-type module Widget.ts. Files: " + string.Join(", ", files.Keys));
        Assert.IsTrue(files.ContainsKey("Kind.ts"), "Expected a per-type module Kind.ts. Files: " + string.Join(", ", files.Keys));
        Assert.IsTrue(files.ContainsKey("corvus-runtime.ts"), "Expected the self-contained runtime module.");
        Assert.IsFalse(files.ContainsKey("generated.ts"), "Module-per-type must not emit a single generated.ts.");
    }

    [TestMethod]
    public async Task ModulePerType_SiblingModuleImportsReferencedIdentifiers()
    {
        IReadOnlyDictionary<string, string> files = await GenerateModulePerType(WidgetSchema);
        string widget = files["Widget.ts"];

        // Widget references Kind (its enum property's type + validator), so it imports both from Kind's module:
        // the validator as a value and the type with the `type` modifier.
        StringAssert.Contains(widget, "from \"./Kind.js\"", "Widget.ts must import from the Kind module.");
        StringAssert.Contains(widget, "evaluateKind", "Widget.ts must import the Kind validator it calls.");
        StringAssert.Contains(widget, "type Kind", "Widget.ts must import the Kind type it references.");

        // Each module imports the shared runtime, and the cross-module import is a relative `.js` specifier.
        StringAssert.Contains(widget, "corvus-runtime.js", "Each module imports the shared runtime.");

        // The interface itself lives in Widget's own module (the surface is co-located with the validator).
        StringAssert.Contains(widget, "export interface Widget", "Widget's interface lives in its own module.");
        StringAssert.Contains(widget, "export function evaluateWidget", "Widget's validator lives in its own module.");
    }

    [TestMethod]
    public async Task ModulePerType_BarrelReExportsEveryModuleAndDeclaresEvaluateRoot()
    {
        IReadOnlyDictionary<string, string> files = await GenerateModulePerType(WidgetSchema);
        string index = files["index.ts"];

        // The barrel re-exports every generated module...
        StringAssert.Contains(index, "export * from \"./Widget.js\"");
        StringAssert.Contains(index, "export * from \"./Kind.js\"");

        // ...and carries the entry point, which `export *` cannot bring into scope, so it imports the root
        // validator from its own module.
        StringAssert.Contains(index, "import { evaluateWidget } from \"./Widget.js\"");
        StringAssert.Contains(index, "export const evaluateRoot");
        StringAssert.Contains(index, "export default evaluateRoot");
    }

    // Build the schema's type declarations and emit TypeScript in module-per-type mode, returning a
    // filename -> content map with the barrel's evaluateRoot entry point appended (mirroring the two steps the
    // CLI driver performs: GenerateCodeFor + RootEvaluatorBarrelExport).
    private static async Task<IReadOnlyDictionary<string, string>> GenerateModulePerType(string schemaContent)
    {
        string tempFile = Path.Combine(Path.GetTempPath(), $"ts-a1-{Guid.NewGuid():N}.json");
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
                new TypeScriptLanguageProvider.Options(ModulePerType: true));

            IReadOnlyCollection<GeneratedCodeFile> files = typeBuilder.GenerateCodeUsing(
                provider,
                [rootType],
                CancellationToken.None);

            Dictionary<string, string> map = files.ToDictionary(f => f.FileName, f => f.FileContent);
            string barrelExport = provider.RootEvaluatorBarrelExport(rootType);
            map["index.ts"] += barrelExport;
            return map;
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
