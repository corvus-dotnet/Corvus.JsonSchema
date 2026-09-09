using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Loads a Sourcemeta benchmark schema and its instances.
/// </summary>
public sealed class SourceMetaCase : IDisposable
{
    public SourceMetaCase(string name)
    {
        string dir = Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        this.SchemaBytes = File.ReadAllBytes(Path.Combine(dir, name + "-schema.json"));
        string[] lines = File.ReadAllLines(Path.Combine(dir, name + "-instances.jsonl"));
        this.Documents = new ParsedJsonDocument<JsonElement>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            this.Documents[i] = ParsedJsonDocument<JsonElement>.Parse(lines[i]);
        }

        JsonSchemaDialect dialect = JsonSchemaDialect.Draft7;
        foreach ((string file, JsonSchemaDialect d) in SourceMetaCases.All)
        {
            if (file == name)
            {
                dialect = d;
            }
        }

        this.Evaluator = JsonSchemaEvaluator.Compile(this.SchemaBytes, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
    }

    public byte[] SchemaBytes { get; }

    public ParsedJsonDocument<JsonElement>[] Documents { get; }

    public JsonSchemaEvaluator Evaluator { get; }

    public int EvaluateAll()
    {
        int valid = 0;
        JsonSchemaEvaluator evaluator = this.Evaluator;
        ParsedJsonDocument<JsonElement>[] docs = this.Documents;
        for (int i = 0; i < docs.Length; i++)
        {
            if (evaluator.Evaluate(docs[i].RootElement))
            {
                valid++;
            }
        }

        return valid;
    }

    public void Dispose()
    {
        foreach (ParsedJsonDocument<JsonElement> d in this.Documents)
        {
            d.Dispose();
        }

        this.Evaluator.Dispose();
    }
}
