using Corvus.Json;
using Corvus.Json.CodeGeneration;
using TsProviderSpike;

string schemaPath = args.Length > 0 ? args[0] : "person.json";
string outDir = args.Length > 1 ? args[1] : "out";

// --- bootstrap the existing language-neutral core (mirrors GenerationDriverV5) ---
var resolver = new CompoundDocumentResolver(new FileSystemDocumentResolver());

// (No AddMetaschema(): it's an internal per-consumer helper. The 2020-12 analyser matches
// $schema by URI string without fetching the metaschema, so a self-contained schema works.)
var registry = new VocabularyRegistry();
Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
IVocabulary fallback = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

var builder = new JsonSchemaTypeBuilder(resolver, registry);

// --- build the (language-neutral) type graph from the schema ---
var reference = new JsonReference(Path.GetFullPath(schemaPath));
TypeDeclaration root = await builder.AddTypeDeclarationsAsync(reference, fallback, false);

// --- generate TypeScript via our provider ---
var provider = new TypeScriptLanguageProviderSpike();
IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);

Directory.CreateDirectory(outDir);
foreach (GeneratedCodeFile file in files)
{
    File.WriteAllText(Path.Combine(outDir, file.FileName), file.FileContent);
    Console.WriteLine($"--- {file.FileName} ---");
    Console.Write(file.FileContent);
}

Console.WriteLine($"\nGenerated {files.Count} TypeScript file(s) into '{outDir}'.");
