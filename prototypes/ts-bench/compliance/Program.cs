// Self-contained, CI-runnable codegen-aware compliance harness for the corvus-ts TypeScript JSON Schema
// engine. This is a faithful relocation of the (now-deleted) feasibility spike's SuiteHarness `Run(...)` out
// of the spike: it walks every dialect (draft4/6/7/2019-09/2020-12) of the JSON-Schema-Test-Suite, and
// for EACH test-group emits one TS module (`<name>.ts` with `export const evaluateRoot`) plus a manifest.json
// recording { module, dialect, file, group, error, tests:[{ data, valid, desc }] } (data = the instance's raw
// JSON source text). Errored groups record `error` + a null module. A Node runner (suite-runner.mjs)
// transpiles each module with esbuild, imports its `evaluateRoot`, and tallies pass/fail per dialect.
//
// The generator is set up exactly as the C# test runner: FakeWebDocumentResolver (offline remotes) +
// metaschema docs + all analysers + rebaseAsRoot. CreateDefault normally; CreateWithFormatAssertion for
// optional/format/*, optional/content, and optional/format-assertion. Depends ONLY on the public codegen
// libraries — nothing in the production CLI, nothing in the feasibility spike.
//
// Usage:  dotnet run --project ComplianceGenerator.csproj -c Release -- <testsBaseDir> <outDir>
//   <testsBaseDir>  the JSON-Schema-Test-Suite `tests` directory (remotes are read from <testsBaseDir>/../remotes)
//   <outDir>        where the TS modules + manifest.json are written (default: out-suite)
using System.Text.Json;
using ComplianceGenerator;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;

string testsBaseDir = args.Length > 0 ? args[0] : "../../JSON-Schema-Test-Suite/tests";
string outDir = args.Length > 1 ? args[1] : "out-suite";

await SuiteHarness.Run(Path.GetFullPath(testsBaseDir), Path.GetFullPath(outDir));

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
        File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());

        string remotesDir = Path.GetFullPath(Path.Combine(testsBaseDir, "..", "remotes"));

        // Load the bundled metaschema documents (Content-copied next to the binary, same set the validator
        // ships and the spike loads from src/Corvus.Json.Cli.Core/metaschema).
        string metaschemaBase = Path.Combine(AppContext.BaseDirectory, "metaschema");
        foreach ((string uri, string rel) in MetaschemaMap)
        {
            string mp = Path.Combine(metaschemaBase, rel.Replace('/', Path.DirectorySeparatorChar));
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
                    TypeScriptLanguageProvider provider = assertFormat ? TypeScriptLanguageProvider.CreateWithFormatAssertion() : TypeScriptLanguageProvider.CreateDefault();
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
