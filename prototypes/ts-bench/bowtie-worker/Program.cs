// In-process TypeScript codegen worker for the Bowtie harness. Reads NDJSON requests
//   { "schema": <schema>, "registry"?: { uri: <schema> }, "out": <dir>, "dialect"?: <name> }
// on stdin and, for each, generates the module — registering Bowtie's per-case registry of remote schemas in
// the document resolver via AddDocument (no HTTP/file remote resolution, no server) — then writes
//   { "ok": true } | { "error": <message> }.
//
// `dialect` is OPTIONAL. When absent, the fallback vocabulary is detected from the schema's `$schema`
// (the JSON Schema draft path). When present it forces the fallback vocabulary — "openapi30" / "openapi31"
// select the OpenAPI vocabularies (whose documents carry no `$schema`, so $schema-detection cannot reach
// them), mirroring GenerationDriverV5.GetFallbackVocabulary + RegisterVocabularies.
// Long-running, so there is NO per-schema process spawn (which is what kept the harness in lockstep with
// bowtie). Depends only on the public codegen libraries — nothing in the production CLI, nothing in the
// feasibility spike — so this is the harness's own tooling, bundled into the Bowtie OCI image.
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.TypeScript.CodeGeneration;

// Load the standard metaschema documents once; they are reused (read-only) across every request's resolver.
(string Uri, JsonDocument Doc)[] metaschemas = LoadMetaschemas();

string? line;
while ((line = await Console.In.ReadLineAsync()) is not null)
{
    if (string.IsNullOrWhiteSpace(line))
    {
        continue;
    }

    try
    {
        using JsonDocument req = JsonDocument.Parse(line);
        JsonElement r = req.RootElement;
        string schema = r.GetProperty("schema").GetRawText();
        string outDir = r.GetProperty("out").GetString()!;
        string? dialect = r.TryGetProperty("dialect", out JsonElement d) && d.ValueKind == JsonValueKind.String
            ? d.GetString()
            : null;
        var registry = new Dictionary<string, JsonDocument>();
        if (r.TryGetProperty("registry", out JsonElement reg) && reg.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty p in reg.EnumerateObject())
            {
                registry[p.Name] = JsonDocument.Parse(p.Value.GetRawText());
            }
        }

        await GenerateModule(schema, registry, outDir, metaschemas, dialect);
        await Console.Out.WriteLineAsync("{\"ok\":true}");
    }
    catch (Exception ex)
    {
        await Console.Out.WriteLineAsync(JsonSerializer.Serialize(new { error = ex.Message }));
    }

    await Console.Out.FlushAsync();
}

return 0;

static async Task GenerateModule(string schemaJson, Dictionary<string, JsonDocument> registry, string outDir, (string Uri, JsonDocument Doc)[] metaschemas, string? dialect)
{
    // The fallback vocabulary. An explicit `dialect` request wins (OpenAPI 3.0/3.1 documents carry no
    // `$schema`, so they can only be selected this way); otherwise it is detected from the schema's
    // `$schema` (the JSON Schema draft path the harness injects).
    IVocabulary fallback;
    if (dialect is not null)
    {
        fallback = dialect switch
        {
            "openapi30" => Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            "openapi31" => Corvus.Json.CodeGeneration.OpenApi31.VocabularyAnalyser.DefaultVocabulary,
            "draft4" => Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            "draft6" => Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
            "draft7" => Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
            "draft2019-09" => Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
            "draft2020-12" => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            _ => throw new ArgumentException($"Unknown dialect '{dialect}'."),
        };
    }
    else
    {
        using JsonDocument sd = JsonDocument.Parse(schemaJson);
        string detected = sd.RootElement.TryGetProperty("$schema", out JsonElement ds) && ds.ValueKind == JsonValueKind.String
            ? (ds.GetString() ?? string.Empty).TrimEnd('#')
            : "https://json-schema.org/draft/2020-12/schema";
        fallback = detected switch
        {
            "http://json-schema.org/draft-04/schema" => Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            "http://json-schema.org/draft-06/schema" => Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
            "http://json-schema.org/draft-07/schema" => Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
            "https://json-schema.org/draft/2019-09/schema" => Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
            _ => Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
        };
    }

    CompoundDocumentResolver resolver = new(new FileSystemDocumentResolver());

    // Metaschemas first (so a schema that $refs the metaschema document resolves), then the Bowtie registry.
    foreach ((string uri, JsonDocument doc) in metaschemas)
    {
        resolver.AddDocument(uri, doc);
    }

    foreach ((string uri, JsonDocument doc) in registry)
    {
        resolver.AddDocument(uri, doc);
    }

    VocabularyRegistry vocabularyRegistry = new();
    Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);
    Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);
    Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
    Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
    Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
    Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
    Corvus.Json.CodeGeneration.OpenApi31.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

    JsonSchemaTypeBuilder builder = new(resolver, vocabularyRegistry);

    // Normalize the synthetic main reference so the AddDocument key and the JsonReference lookup agree.
    string mainRef = Path.Combine(outDir, "__corvus_main.json");
    if (SchemaReferenceNormalization.TryNormalizeSchemaReference(mainRef, out string? normalized))
    {
        mainRef = normalized;
    }

    builder.AddDocument(mainRef, JsonDocument.Parse(schemaJson));
    TypeDeclaration root = await builder.AddTypeDeclarationsAsync(new JsonReference(mainRef), fallback, true);

    // CreateDefault = annotation-only format/content (Bowtie's per-dialect suite expects annotations).
    TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.CreateDefault();
    IReadOnlyCollection<GeneratedCodeFile> files = builder.GenerateCodeUsing(provider, CancellationToken.None, root);
    string export = provider.RootEvaluatorExport(root);

    Directory.CreateDirectory(outDir);
    File.WriteAllText(Path.Combine(outDir, "corvus-runtime.ts"), TypeScriptLanguageProvider.RuntimeModuleSource());
    GeneratedCodeFile generated = files.First(f => f.FileName == "generated.ts");
    File.WriteAllText(Path.Combine(outDir, "generated.ts"), generated.FileContent + export);
}

// Canonical metaschema URI -> path under the bundled metaschema/ directory (mirrors what the validator ships).
static (string Uri, JsonDocument Doc)[] LoadMetaschemas()
{
    (string Uri, string Rel)[] map =
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

    string baseDir = Path.Combine(AppContext.BaseDirectory, "metaschema");
    var loaded = new List<(string, JsonDocument)>();
    foreach ((string uri, string rel) in map)
    {
        string path = Path.Combine(baseDir, rel.Replace('/', Path.DirectorySeparatorChar));
        if (File.Exists(path))
        {
            loaded.Add((uri, JsonDocument.Parse(File.ReadAllText(path))));
        }
    }

    return loaded.ToArray();
}
