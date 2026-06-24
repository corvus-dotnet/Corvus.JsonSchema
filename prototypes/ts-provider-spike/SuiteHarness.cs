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
    ];

    public static async Task Run(string suiteDir, string outDir)
    {
        string schemasDir = Path.Combine(outDir, "schemas");
        Directory.CreateDirectory(schemasDir);
        File.WriteAllText(Path.Combine(outDir, "package.json"), "{ \"type\": \"module\" }\n");

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
                    string schemaPath = Path.GetFullPath(Path.Combine(schemasDir, moduleName + ".json"));
                    File.WriteAllText(schemaPath, schema.GetRawText());

                    var resolver = new CompoundDocumentResolver(new FileSystemDocumentResolver());
                    var registry = new VocabularyRegistry();
                    Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, registry);
                    IVocabulary fallback = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
                    var builder = new JsonSchemaTypeBuilder(resolver, registry);
                    TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(schemaPath), fallback, false);
                    TypeScriptLanguageProviderSpike provider = TypeScriptLanguageProviderSpike.CreateDefault();
                    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
                    string rootName = root.TryGetMetadata<string>("Ts_FinalName", out string? rn) && !string.IsNullOrEmpty(rn) ? rn! : "Entity";
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
