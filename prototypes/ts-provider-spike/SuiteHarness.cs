using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.TypeScript.CodeGeneration;

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

    // Benchmark generation: emit one validator module per Sourcemeta-dataset schema (schemas/<name>/
    // schema.json), dialect-detected from $schema, plus the shared runtime once. Used to benchmark our
    // generated validators against Ajv/Hyperjump/schemasafe over the real-world Sourcemeta dataset.
    public static async Task GenerateForBench(string schemasDir, string outDir, string metaschemaBase)
    {
        Directory.CreateDirectory(outDir);
        File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());
        string remotesDir = metaschemaBase; // bench schemas are self-contained; FakeWeb just needs a dir

        var dialectFallback = new (string Uri, Func<IVocabulary> F)[]
        {
            ("http://json-schema.org/draft-04/schema", static () => Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary),
            ("http://json-schema.org/draft-06/schema", static () => Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary),
            ("http://json-schema.org/draft-07/schema", static () => Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary),
            ("https://json-schema.org/draft/2019-09/schema", static () => Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary),
            ("https://json-schema.org/draft/2020-12/schema", static () => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary),
        };

        int ok = 0, failed = 0;
        var names = new List<string>();
        foreach (string dir in Directory.GetDirectories(schemasDir).OrderBy(d => d, StringComparer.Ordinal))
        {
            string schemaPath = Path.Combine(dir, "schema.json");
            if (!File.Exists(schemaPath)) { continue; }
            string name = Path.GetFileName(dir);

            try
            {
                string schemaText = File.ReadAllText(schemaPath);
                using JsonDocument sd = JsonDocument.Parse(schemaText);
                string dialect = sd.RootElement.TryGetProperty("$schema", out JsonElement ds) && ds.ValueKind == JsonValueKind.String
                    ? (ds.GetString() ?? string.Empty).TrimEnd('#')
                    : "https://json-schema.org/draft/2020-12/schema";
                IVocabulary fallback = (dialectFallback.FirstOrDefault(d => d.Uri == dialect).F
                    ?? (static () => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary))();

                var resolver = new CompoundDocumentResolver(new FakeWebDocumentResolver(remotesDir), new FileSystemDocumentResolver());
                foreach ((string uri, string rel) in MetaschemaMap)
                {
                    string mp = Path.Combine(metaschemaBase, rel);
                    if (File.Exists(mp)) { resolver.AddDocument(uri, JsonDocument.Parse(File.ReadAllText(mp))); }
                }

                var registry = new VocabularyRegistry();
                Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(registry);
                Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(registry);
                Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(registry);
                var builder = new JsonSchemaTypeBuilder(resolver, registry);

                string schemaRef = Path.Combine(remotesDir, name + "__bench.json");
                if (Corvus.Json.CodeGeneration.DocumentResolvers.SchemaReferenceNormalization.TryNormalizeSchemaReference(schemaRef, out string? norm)) { schemaRef = norm; }
                builder.AddDocument(schemaRef, JsonDocument.Parse(schemaText));
                TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(schemaRef), fallback, rebaseAsRoot: true);
                IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(TypeScriptLanguageProvider.CreateDefault(), CancellationToken.None, root);
                TypeDeclaration reducedRoot = root.ReducedTypeDeclaration().ReducedType;
                string rootName = reducedRoot.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";
                File.WriteAllText(Path.Combine(outDir, name + ".ts"), files.First().FileContent + $"\nexport const evaluateRoot = (v: unknown): boolean => evaluate{rootName}(v, fresh());\n");
                names.Add(name);
                ok++;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  FAILED {name}: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
                failed++;
            }
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonSerializer.Serialize(names));
        Console.WriteLine($"bench-gen: {ok} generated, {failed} failed -> {outDir}");
    }

    // Diagnostic: build ONE schema exactly as the C# runner does and walk the resolved type graph,
    // printing each type's location + raw schema. Used to see how $dynamicRef resolves (string vs integer).
    public static async Task DynDebug(string schemaText, string testsBaseDir)
    {
        string remotesDir = Path.GetFullPath(Path.Combine(testsBaseDir, "..", "remotes"));
        string metaschemaBase = Path.GetFullPath(Path.Combine(testsBaseDir, "..", "..", "src", "Corvus.Json.Cli.Core", "metaschema"));
        var resolver = new CompoundDocumentResolver(new FakeWebDocumentResolver(remotesDir), new FileSystemDocumentResolver());
        foreach ((string uri, string rel) in MetaschemaMap)
        {
            string mp = Path.Combine(metaschemaBase, rel);
            if (File.Exists(mp)) { resolver.AddDocument(uri, JsonDocument.Parse(File.ReadAllText(mp))); }
        }

        var registry = new VocabularyRegistry();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
        var builder = new JsonSchemaTypeBuilder(resolver, registry);
        string schemaRef = Path.Combine(remotesDir, "dyndbg.json");
        if (Corvus.Json.CodeGeneration.DocumentResolvers.SchemaReferenceNormalization.TryNormalizeSchemaReference(schemaRef, out string? n)) { schemaRef = n; }
        builder.AddDocument(schemaRef, JsonDocument.Parse(schemaText));
        TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(schemaRef), Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary, rebaseAsRoot: true);

        var seen = new HashSet<TypeDeclaration>();
        void Walk(TypeDeclaration t, string path, int depth)
        {
            TypeDeclaration r = t.ReducedTypeDeclaration().ReducedType;
            string raw = r.LocatedSchema.Schema.ValueKind == JsonValueKind.Object || r.LocatedSchema.Schema.ValueKind == JsonValueKind.Array ? r.LocatedSchema.Schema.GetRawText() : r.LocatedSchema.Schema.ToString();
            if (raw.Length > 90) { raw = raw[..90]; }
            bool dyn = r.TryGetDynamicSource(out TypeDeclaration? ds);
            Console.WriteLine($"{new string(' ', depth * 2)}{path}: loc={r.LocatedSchema.Location} dynSrc={(dyn ? ds!.LocatedSchema.Location.ToString() : "-")}");
            Console.WriteLine($"{new string(' ', depth * 2)}  schema={raw}");
            if (!seen.Add(r) || depth > 6) { return; }
            foreach (PropertyDeclaration p in r.PropertyDeclarations)
            {
                Walk(p.ReducedPropertyType, $".{p.JsonPropertyName}", depth + 1);
            }
        }

        Walk(root, "root", 0);
    }

    public static async Task Run(string testsBaseDir, string outDir)
    {
        string schemasDir = Path.Combine(outDir, "schemas");
        Directory.CreateDirectory(schemasDir);
        File.WriteAllText(Path.Combine(outDir, "package.json"), "{ \"type\": \"module\" }\n");
        File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());

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
