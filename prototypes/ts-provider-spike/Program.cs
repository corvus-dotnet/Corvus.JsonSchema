using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using TsProviderSpike;

if (args.Length > 0 && args[0] == "--suite")
{
    string testsBaseDir = args.Length > 1 ? args[1] : "../../JSON-Schema-Test-Suite/tests";
    await SuiteHarness.Run(testsBaseDir, "out-suite");
    return;
}

if (args.Length > 0 && args[0] == "--dyndbg")
{
    string schemaText = File.ReadAllText(args[1]);
    await SuiteHarness.DynDebug(schemaText, args.Length > 2 ? args[2] : "../../JSON-Schema-Test-Suite/tests");
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
File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());

void Emit(TypeScriptLanguageProvider provider, string fileName)
{
    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
    TypeDeclaration reducedRoot = root.ReducedTypeDeclaration().ReducedType;
    string rootName = reducedRoot.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "GeneratedType";

    // The provider returns [types module, shared runtime]; write the types module (first) to the
    // requested name (corvus-runtime.ts is written once by the caller).
    GeneratedCodeFile typesFile = files.First();
    File.WriteAllText(Path.Combine(outDir, fileName), typesFile.FileContent + $"\nexport const evaluateRoot = (v: unknown): boolean => evaluate{rootName}(v, fresh());\n");

    Console.WriteLine($"wrote {fileName} (root = evaluate{rootName})");
}

// Base provider: the default handler set (no multipleOf).
Emit(TypeScriptLanguageProvider.CreateDefault(), "generated.ts");

// Extension: register a custom handler at runtime. The core registry dispatches to it for the
// `multipleOf` keyword — no change to the provider. This is the user/third-party extension seam.
TypeScriptLanguageProvider ext = TypeScriptLanguageProvider.CreateDefault();
ext.RegisterValidationHandlers(new TsFormatExtensionHandler());
Emit(ext, "generated-ext.ts");

Console.WriteLine("generated.ts = base handlers; generated-ext.ts = base + registered format extension.");
