using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Codegen-aware compliance harness (design §8), now MULTI-DIALECT: because handlers match on capability
// interfaces (not keyword text), the same handler set serves every draft. For each dialect's test suite,
// for each schema-group in the targeted keyword files, run the provider -> emit a TS module (one
// evaluateRoot per group) + a manifest. A Node runner compiles + runs every case and tallies per dialect.
// The generator is set up exactly as the C# test runner (FakeWebDocumentResolver + metaschema + all
// analysers + rebaseAsRoot).
internal static class SuiteHarness
{
    private static readonly (string Name, Func<IVocabulary> Fallback)[] Dialects =
    [
        ("draft4", static () => Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary),
        ("draft6", static () => Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary),
        ("draft7", static () => Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary),
        ("draft2019-09", static () => Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary),
        ("draft2020-12", static () => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary),
    ];

    // Canonical metaschema URI -> path relative to the metaschema base dir, mirroring C# AddMetaschema.
    private static readonly (string Uri, string Rel)[] MetaschemaMap =
    [
        ("http://json-schema.org/draft-04/schema", "draft4/schema.json"),
        ("http://json-schema.org/draft-06/schema", "draft6/schema.json"),
        ("http://json-schema.org/draft-07/schema", "draft7/schema.json"),
        ("https://json-schema.org/draft/2019-09/schema", "draft2019-09/schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/applicator", "draft2019-09/meta/applicator.json"),
        ("https://json-schema.org/draft/2019-09/meta/content", "draft2019-09/meta/content.json"),
        ("https://json-schema.org/draft/2019-09/meta/core", "draft2019-09/meta/core.json"),
        ("https://json-schema.org/draft/2019-09/meta/format", "draft2019-09/meta/format.json"),
        ("https://json-schema.org/draft/2019-09/meta/hyper-schema", "draft2019-09/meta/hyper-schema.json"),
        ("https://json-schema.org/draft/2019-09/meta/meta-data", "draft2019-09/meta/meta-data.json"),
        ("https://json-schema.org/draft/2019-09/meta/validation", "draft2019-09/meta/validation.json"),
        ("https://json-schema.org/draft/2020-12/schema", "draft2020-12/schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/applicator", "draft2020-12/meta/applicator.json"),
        ("https://json-schema.org/draft/2020-12/meta/content", "draft2020-12/meta/content.json"),
        ("https://json-schema.org/draft/2020-12/meta/core", "draft2020-12/meta/core.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-annotation", "draft2020-12/meta/format-annotation.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-assertion", "draft2020-12/meta/format-assertion.json"),
        ("https://json-schema.org/draft/2020-12/meta/hyper-schema", "draft2020-12/meta/hyper-schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/meta-data", "draft2020-12/meta/meta-data.json"),
        ("https://json-schema.org/draft/2020-12/meta/unevaluated", "draft2020-12/meta/unevaluated.json"),
        ("https://json-schema.org/draft/2020-12/meta/validation", "draft2020-12/meta/validation.json"),
    ];

    private static readonly Dictionary<string, string> MetaschemaText = [];

    public static async Task Run(string testsBaseDir, string outDir)
    {
        string schemasDir = Path.Combine(outDir, "schemas");
        Directory.CreateDirectory(schemasDir);
        File.WriteAllText(Path.Combine(outDir, "package.json"), "{ \"type\": \"module\" }\n");
        File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProviderSpike.RuntimeModuleSource());

        string remotesDir = Path.GetFullPath(Path.Combine(testsBaseDir, "..", "remotes"));
        string metaschemaBase = Path.GetFullPath(Path.Combine(testsBaseDir, "..", "..", "src", "Corvus.Json.Cli.Core", "metaschema"));
        foreach ((string uri, string rel) in MetaschemaMap)
        {
            string mp = Path.Combine(metaschemaBase, rel);
            if (File.Exists(mp)) { MetaschemaText[uri] = File.ReadAllText(mp); }
        }

        Console.WriteLine($"remotes exists={Directory.Exists(remotesDir)}; metaschema docs loaded={MetaschemaText.Count}/{MetaschemaMap.Length}");

        var manifest = new List<object>();
        var keepAlive = new List<JsonDocument>();
        int built = 0, errored = 0, totalTests = 0;

        foreach ((string dialect, Func<IVocabulary> fallbackFactory) in Dialects)
        {
            string suiteDir = Path.Combine(testsBaseDir, dialect);
            if (!Directory.Exists(suiteDir))
            {
                Console.WriteLine($"(skip missing dialect {dialect})");
                continue;
            }

            IVocabulary fallback = fallbackFactory();

            // Entire suite: all top-level keyword files + optional/*.json (default = format-as-annotation)
            // + optional/format/*.json (format asserted).
            foreach (string f in Directory.GetFiles(suiteDir, "*.json"))
            {
                await ProcessFile(f, dialect, fallback, Path.GetFileNameWithoutExtension(f), assertFormat: false);
            }

            string optDir = Path.Combine(suiteDir, "optional");
            if (Directory.Exists(optDir))
            {
                foreach (string f in Directory.GetFiles(optDir, "*.json"))
                {
                    string name = Path.GetFileNameWithoutExtension(f);
                    // optional/content (contentEncoding/contentMediaType) and optional/format-assertion
                    // (which expects format asserted in both the true and false vocab cases) use the
                    // assertion provider, like optional/format.
                    await ProcessFile(f, dialect, fallback, "optional_" + name, assertFormat: name is "content" or "format-assertion");
                }

                string fmtDir = Path.Combine(optDir, "format");
                if (Directory.Exists(fmtDir))
                {
                    foreach (string f in Directory.GetFiles(fmtDir, "*.json"))
                    {
                        await ProcessFile(f, dialect, fallback, "format_" + Path.GetFileNameWithoutExtension(f), assertFormat: true);
                    }
                }
            }
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonSerializer.Serialize(manifest));
        foreach (JsonDocument d in keepAlive)
        {
            d.Dispose();
        }

        Console.WriteLine($"Built {built} modules, {errored} errored, {totalTests} cases across {Dialects.Length} dialects -> {outDir}/manifest.json");
        return;

        async Task ProcessFile(string filePath, string dialect, IVocabulary fallback, string label, bool assertFormat)
        {
            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(filePath));
            keepAlive.Add(doc);

            int i = 0;
            foreach (JsonElement group in doc.RootElement.EnumerateArray())
            {
                JsonElement schema = group.GetProperty("schema");
                string groupDesc = group.TryGetProperty("description", out JsonElement gd) ? gd.GetString() ?? string.Empty : string.Empty;
                string moduleName = $"g_{dialect}_{label}_{i}";
                i++;

                var tests = new List<object>();
                foreach (JsonElement test in group.GetProperty("tests").EnumerateArray())
                {
                    tests.Add(new
                    {
                        // Store the instance's exact SOURCE TEXT (the raw JSON token), so the runner parses
                        // each instance ONCE with a source-preserving parser — no lossy double-parse.
                        data = test.GetProperty("data").GetRawText(),
                        valid = test.GetProperty("valid").GetBoolean(),
                        desc = test.TryGetProperty("description", out JsonElement td) ? td.GetString() : string.Empty,
                    });
                    totalTests++;
                }

                // A boolean root schema is a trivial validator (true accepts all, false rejects all).
                if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    File.WriteAllText(Path.Combine(outDir, moduleName + ".ts"), $"export const evaluateRoot = (v: unknown): boolean => {(schema.ValueKind == JsonValueKind.True ? "true" : "false")};\n");
                    manifest.Add(new { module = moduleName, dialect, file = label, group = groupDesc, error = (string?)null, tests });
                    built++;
                    continue;
                }

                try
                {
                    File.WriteAllText(Path.GetFullPath(Path.Combine(schemasDir, moduleName + ".json")), schema.GetRawText());

                    var resolver = new CompoundDocumentResolver(new FakeWebDocumentResolver(remotesDir), new FileSystemDocumentResolver());
                    foreach (KeyValuePair<string, string> ms in MetaschemaText)
                    {
                        resolver.AddDocument(ms.Key, JsonDocument.Parse(ms.Value));
                    }

                    var registry = new VocabularyRegistry();
                    Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                    Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                    Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(registry);
                    Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(registry);
                    Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(registry);
                    var builder = new JsonSchemaTypeBuilder(resolver, registry);

                    string schemaRef = Path.Combine(remotesDir, moduleName + ".json");
                    if (Corvus.Json.CodeGeneration.DocumentResolvers.SchemaReferenceNormalization.TryNormalizeSchemaReference(schemaRef, out string? normalized)) { schemaRef = normalized; }
                    builder.AddDocument(schemaRef, JsonDocument.Parse(schema.GetRawText()));
                    TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(schemaRef), fallback, rebaseAsRoot: true);
                    TypeScriptLanguageProviderSpike provider = assertFormat ? TypeScriptLanguageProviderSpike.CreateWithFormatAssertion() : TypeScriptLanguageProviderSpike.CreateDefault();
                    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
                    TypeDeclaration reducedRoot = root.ReducedTypeDeclaration().ReducedType;
                    string rootName = reducedRoot.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";
                    File.WriteAllText(Path.Combine(outDir, moduleName + ".ts"), files.First().FileContent + $"\nexport const evaluateRoot = (v: unknown): boolean => evaluate{rootName}(v, fresh());\n");
                    manifest.Add(new { module = moduleName, dialect, file = label, group = groupDesc, error = (string?)null, tests });
                    built++;
                }
                catch (Exception ex)
                {
                    manifest.Add(new { module = (string?)null, dialect, file = label, group = groupDesc, error = ex.Message, tests });
                    errored++;
                }
            }
        }
    }
}
