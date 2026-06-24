using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Codegen-aware compliance harness (design §8): for each JSON-Schema-Test-Suite schema-group in the
// targeted keyword files, run the provider -> emit a TS module (one evaluateRoot per group) -> and
// write a manifest of {module, tests[]}. A Node runner then compiles + runs every case and asserts
// the boolean expectation. Bounded to the keywords the handler set currently supports.
internal static class SuiteHarness
{
    private static readonly string[] Targets =
    [
        "type", "required", "properties", "minLength", "maxLength", "minimum", "maximum", "enum", "pattern",
        "const", "exclusiveMinimum", "exclusiveMaximum", "minProperties", "maxProperties", "uniqueItems",
        "allOf", "anyOf", "oneOf", "items", "prefixItems", "propertyNames", "dependentRequired",
        "if-then-else", "ref", "defs", "unevaluatedProperties", "unevaluatedItems",
        "contains", "dependentSchemas", "minContains", "maxContains",
        "dynamicRef", "recursiveRef",
    ];

    // The 2020-12 metaschema documents (canonical URI -> path relative to the metaschema dir), mirroring
    // the C# AddMetaschema. Registering these lets schemas that $ref the metaschema build offline.
    private static readonly (string Uri, string Rel)[] MetaschemaMap =
    [
        ("https://json-schema.org/draft/2020-12/schema", "schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/applicator", "meta/applicator.json"),
        ("https://json-schema.org/draft/2020-12/meta/content", "meta/content.json"),
        ("https://json-schema.org/draft/2020-12/meta/core", "meta/core.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-annotation", "meta/format-annotation.json"),
        ("https://json-schema.org/draft/2020-12/meta/format-assertion", "meta/format-assertion.json"),
        ("https://json-schema.org/draft/2020-12/meta/hyper-schema", "meta/hyper-schema.json"),
        ("https://json-schema.org/draft/2020-12/meta/meta-data", "meta/meta-data.json"),
        ("https://json-schema.org/draft/2020-12/meta/unevaluated", "meta/unevaluated.json"),
        ("https://json-schema.org/draft/2020-12/meta/validation", "meta/validation.json"),
    ];

    public static async Task Run(string suiteDir, string outDir)
    {
        string schemasDir = Path.Combine(outDir, "schemas");
        Directory.CreateDirectory(schemasDir);
        File.WriteAllText(Path.Combine(outDir, "package.json"), "{ \"type\": \"module\" }\n");

        // remotes/ is a sibling of tests/ in the suite; the metaschema ships with Corvus.Json.Cli.Core.
        string remotesDir = Path.GetFullPath(Path.Combine(suiteDir, "..", "..", "remotes"));
        string metaschemaDir = Path.GetFullPath(Path.Combine(suiteDir, "..", "..", "..", "src", "Corvus.Json.Cli.Core", "metaschema", "draft2020-12"));
        Console.WriteLine($"remotes={remotesDir} (exists={Directory.Exists(remotesDir)}); metaschema={metaschemaDir} (exists={File.Exists(Path.Combine(metaschemaDir, "schema.json"))})");

        var manifest = new List<object>();
        var keepAlive = new List<JsonDocument>();
        int built = 0, errored = 0, totalTests = 0;

        foreach (string file in Targets)
        {
            string path = Path.Combine(suiteDir, file + ".json");
            if (!File.Exists(path))
            {
                Console.WriteLine($"(skip missing {file}.json)");
                continue;
            }

            JsonDocument doc = JsonDocument.Parse(File.ReadAllText(path));
            keepAlive.Add(doc);

            int i = 0;
            foreach (JsonElement group in doc.RootElement.EnumerateArray())
            {
                JsonElement schema = group.GetProperty("schema");
                string groupDesc = group.TryGetProperty("description", out JsonElement gd) ? gd.GetString() ?? string.Empty : string.Empty;
                string moduleName = $"g_{file}_{i}";
                i++;

                var tests = new List<object>();
                foreach (JsonElement test in group.GetProperty("tests").EnumerateArray())
                {
                    tests.Add(new
                    {
                        data = test.GetProperty("data"),
                        valid = test.GetProperty("valid").GetBoolean(),
                        desc = test.TryGetProperty("description", out JsonElement td) ? td.GetString() : string.Empty,
                    });
                    totalTests++;
                }

                if (schema.ValueKind is JsonValueKind.True or JsonValueKind.False)
                {
                    manifest.Add(new { module = (string?)null, file, group = groupDesc, error = "boolean schema (not in spike scope)", tests });
                    errored++;
                    continue;
                }

                try
                {
                    File.WriteAllText(Path.GetFullPath(Path.Combine(schemasDir, moduleName + ".json")), schema.GetRawText());

                    // Set up the generator EXACTLY as the C# test runner (TestJsonSchemaCodeGenerator):
                    // CompoundDocumentResolver(FakeWeb(remotes), FileSystem) + the metaschema; register the
                    // schema on the builder under a normalized path INSIDE the remotes namespace (so its
                    // relative $refs resolve as siblings of the remote schemas); build with rebaseAsRoot:true.
                    var resolver = new CompoundDocumentResolver(new FakeWebDocumentResolver(remotesDir), new FileSystemDocumentResolver());
                    foreach ((string uri, string rel) in MetaschemaMap)
                    {
                        string mp = Path.Combine(metaschemaDir, rel);
                        if (File.Exists(mp)) { resolver.AddDocument(uri, JsonDocument.Parse(File.ReadAllText(mp))); }
                    }

                    var registry = new VocabularyRegistry();
                    Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                    IVocabulary fallback = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
                    var builder = new JsonSchemaTypeBuilder(resolver, registry);

                    string schemaRef = Path.Combine(remotesDir, moduleName + ".json");
                    if (Corvus.Json.CodeGeneration.DocumentResolvers.SchemaReferenceNormalization.TryNormalizeSchemaReference(schemaRef, out string? normalized)) { schemaRef = normalized; }
                    builder.AddDocument(schemaRef, JsonDocument.Parse(schema.GetRawText()));
                    TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(schemaRef), fallback, rebaseAsRoot: true);
                    TypeScriptLanguageProviderSpike provider = TypeScriptLanguageProviderSpike.CreateDefault();
                    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
                    TypeDeclaration reducedRoot = root.ReducedTypeDeclaration().ReducedType;
                    string rootName = reducedRoot.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";
                    File.WriteAllText(Path.Combine(outDir, moduleName + ".ts"), files.First().FileContent + $"\nexport const evaluateRoot = (v: unknown): boolean => evaluate{rootName}(v, fresh());\n");
                    manifest.Add(new { module = moduleName, file, group = groupDesc, error = (string?)null, tests });
                    built++;
                }
                catch (Exception ex)
                {
                    manifest.Add(new { module = (string?)null, file, group = groupDesc, error = ex.Message, tests });
                    errored++;
                }
            }
        }

        File.WriteAllText(Path.Combine(outDir, "manifest.json"), JsonSerializer.Serialize(manifest));
        foreach (JsonDocument d in keepAlive)
        {
            d.Dispose();
        }

        Console.WriteLine($"Built {built} modules, {errored} errored, {totalTests} cases across {Targets.Length} keyword files -> {outDir}/manifest.json");
    }
}
