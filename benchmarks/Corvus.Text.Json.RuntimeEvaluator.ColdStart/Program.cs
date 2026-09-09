using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using CorvusValidator = Corvus.Text.Json.Validator.JsonSchema;

// Cold-start comparison: runtime evaluator compilation vs the Roslyn-based Corvus.Text.Json.Validator.
// Usage: coldstart [schema-name ...]   (defaults to a representative subset)

string dir = Path.Combine(AppContext.BaseDirectory, "sourcemeta");
string[] names = args.Length > 0 ? args : ["aws-cdk", "cql2", "geojson", "cmake-presets", "ansible-meta", "openapi", "pulumi"];

static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

static JsonSchemaDialect DialectOf(string json) => json.Contains("draft-04") ? JsonSchemaDialect.Draft4
    : json.Contains("draft-06") ? JsonSchemaDialect.Draft6
    : json.Contains("2019-09") ? JsonSchemaDialect.Draft201909
    : json.Contains("2020-12") ? JsonSchemaDialect.Draft202012
    : JsonSchemaDialect.Draft7;

Console.WriteLine($"{"schema",-16} {"bytes",8} {"rt first",10} {"rt warm",10} {"rt alloc",10} {"roslyn first",13} {"roslyn 2nd",11}");
bool roslynWarm = false;
foreach (string name in names)
{
    string path = Path.Combine(dir, name + "-schema.json");
    byte[] bytes = File.ReadAllBytes(path);
    string text = Encoding.UTF8.GetString(bytes);
    var options = new JsonSchemaEvaluatorOptions { DefaultDialect = DialectOf(text) };

    // Runtime evaluator: first (cold, includes JIT of the compiler for the first schema) and warm minimum.
    long a0 = GC.GetAllocatedBytesForCurrentThread();
    long t0 = Stopwatch.GetTimestamp();
    using (JsonSchemaEvaluator.Compile(bytes, options))
    {
    }

    double first = Ms(Stopwatch.GetTimestamp() - t0);
    long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
    double warm = double.MaxValue;
    for (int i = 0; i < 20; i++)
    {
        long w0 = Stopwatch.GetTimestamp();
        using (JsonSchemaEvaluator.Compile(bytes, options))
        {
        }

        warm = Math.Min(warm, Ms(Stopwatch.GetTimestamp() - w0));
    }

    // Roslyn validator: the first schema in the process also pays the metadata-reference discovery cost;
    // the cache is keyed by URI, so use a fresh canonical URI per call.
    double roslynFirst = double.NaN;
    double roslynSecond = double.NaN;
    try
    {
        var vopts = new CorvusValidator.Options(alwaysAssertFormat: false);
        long r0 = Stopwatch.GetTimestamp();
        CorvusValidator.FromText(text, $"https://coldstart.example/{name}/1.json", vopts);
        roslynFirst = Ms(Stopwatch.GetTimestamp() - r0);
        long r1 = Stopwatch.GetTimestamp();
        CorvusValidator.FromText(text, $"https://coldstart.example/{name}/2.json", vopts);
        roslynSecond = Ms(Stopwatch.GetTimestamp() - r1);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"{name}: roslyn validator failed: {ex.GetType().Name}: {ex.Message.Split('\n')[0]}");
    }

    Console.WriteLine($"{name,-16} {bytes.Length,8} {first,8:F2}ms {warm,8:F2}ms {alloc / 1024.0,7:F0}KB {roslynFirst,11:F1}ms {roslynSecond,9:F1}ms{(roslynWarm ? string.Empty : "  (first Roslyn use in process)")}");
    roslynWarm = true;
}
