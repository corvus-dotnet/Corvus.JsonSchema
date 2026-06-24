using Corvus.Json;
using Corvus.Json.CodeGeneration;
using TsProviderSpike;

if (args.Length > 0 && args[0] == "--suite")
{
    string suiteDir = args.Length > 1 ? args[1] : "../../JSON-Schema-Test-Suite/tests/draft2020-12";
    await SuiteHarness.Run(suiteDir, "out-suite");
    return;
}

string schemaPath = args.Length > 0 ? args[0] : "person.json";
string outDir = args.Length > 1 ? args[1] : "out";

var resolver = new CompoundDocumentResolver(new FileSystemDocumentResolver());
var registry = new VocabularyRegistry();
Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
IVocabulary fallback = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
var builder = new JsonSchemaTypeBuilder(resolver, registry);
var reference = new JsonReference(Path.GetFullPath(schemaPath));
TypeDeclaration root = await builder.AddTypeDeclarationsAsync(reference, fallback, false);

Directory.CreateDirectory(outDir);

void Emit(TypeScriptLanguageProviderSpike provider, string fileName)
{
    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
    string rootName = root.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "GeneratedType";
    foreach (GeneratedCodeFile file in files)
    {
        File.WriteAllText(Path.Combine(outDir, fileName), file.FileContent + $"\nexport const evaluateRoot = evaluate{rootName};\n");
    }

    Console.WriteLine($"wrote {fileName} (root = evaluate{rootName})");
}

// Base provider: the default handler set (no multipleOf).
Emit(TypeScriptLanguageProviderSpike.CreateDefault(), "generated.ts");

// Extension: register a custom handler at runtime. The core registry dispatches to it for the
// `multipleOf` keyword — no change to the provider. This is the user/third-party extension seam.
TypeScriptLanguageProviderSpike ext = TypeScriptLanguageProviderSpike.CreateDefault();
ext.RegisterValidationHandlers(new TsMultipleOfHandler());
Emit(ext, "generated-ext.ts");

Console.WriteLine("generated.ts = base handlers; generated-ext.ts = base + registered multipleOf extension.");
