using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using CorvusValidator = Corvus.Text.Json.Validator.JsonSchema;

// Cold-start measurement: compiling a schema with the runtime evaluator directly and through the
// Corvus.Text.Json.Validator wrapper (which adds URI/cache handling and document resolution).
// Usage: coldstart [schema-name ...]   (defaults to a representative subset)

string dir = Path.Combine(AppContext.BaseDirectory, "sourcemeta");
string imageDir = Path.Combine(AppContext.BaseDirectory, "images");
string[] defaults = ["aws-cdk", "cql2", "geojson", "cmake-presets", "ansible-meta", "openapi", "pulumi"];

// coldstart write [names]: compile each schema and write its program image next to the binary.
// coldstart image [names]: load the images written earlier and time the first (process-cold) and warm loads,
//                          in a process that has never run the compiler.
if (args.Length > 0 && args[0] == "write")
{
    Directory.CreateDirectory(imageDir);
    foreach (string name in args.Length > 1 ? args[1..] : defaults)
    {
        byte[] schema = File.ReadAllBytes(Path.Combine(dir, name + "-schema.json"));
        var o = new JsonSchemaEvaluatorOptions { DefaultDialect = DialectOf(Encoding.UTF8.GetString(schema)) };
        using JsonSchemaEvaluator e = JsonSchemaEvaluator.Compile(schema, o);
        File.WriteAllBytes(Path.Combine(imageDir, name + ".cjsp"), e.ToProgramImage());
    }

    return;
}

if (args.Length > 0 && args[0] == "image")
{
    Console.WriteLine($"{"schema",-16} {"image",8} {"img first",10} {"img warm",10} {"img alloc",10}");
    foreach (string name in args.Length > 1 ? args[1..] : defaults)
    {
        byte[] image = File.ReadAllBytes(Path.Combine(imageDir, name + ".cjsp"));
        var o = new JsonSchemaEvaluatorOptions();
        long a0 = GC.GetAllocatedBytesForCurrentThread();
        long t0 = Stopwatch.GetTimestamp();
        using (JsonSchemaEvaluator.FromProgramImage(image, o))
        {
        }

        double first = Ms(Stopwatch.GetTimestamp() - t0);
        long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
        double warm = double.MaxValue;
        for (int i = 0; i < 20; i++)
        {
            long w0 = Stopwatch.GetTimestamp();
            using (JsonSchemaEvaluator.FromProgramImage(image, o))
            {
            }

            warm = Math.Min(warm, Ms(Stopwatch.GetTimestamp() - w0));
        }

        Console.WriteLine($"{name,-16} {image.Length,8} {first,8:F2}ms {warm,8:F2}ms {alloc / 1024.0,7:F0}KB");
    }

    return;
}

string[] names = args.Length > 0 ? args : defaults;

static double Ms(long ticks) => ticks * 1000.0 / Stopwatch.Frequency;

static JsonSchemaDialect DialectOf(string json) => json.Contains("draft-04") ? JsonSchemaDialect.Draft4
    : json.Contains("draft-06") ? JsonSchemaDialect.Draft6
    : json.Contains("2019-09") ? JsonSchemaDialect.Draft201909
    : json.Contains("2020-12") ? JsonSchemaDialect.Draft202012
    : JsonSchemaDialect.Draft7;

Console.WriteLine($"{"schema",-16} {"bytes",8} {"rt first",10} {"rt warm",10} {"rt alloc",10} {"validator 1st",14} {"validator 2nd",14}");
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

    // Validator wrapper: the cache is keyed by URI, so use a fresh canonical URI per call.
    var vopts = new CorvusValidator.Options(allowFileSystemAndHttpResolution: false, alwaysAssertFormat: false, defaultDialect: DialectOf(text));
    long r0 = Stopwatch.GetTimestamp();
    CorvusValidator.FromText(text, $"https://coldstart.example/{name}/1.json", vopts);
    double validatorFirst = Ms(Stopwatch.GetTimestamp() - r0);
    long r1 = Stopwatch.GetTimestamp();
    CorvusValidator.FromText(text, $"https://coldstart.example/{name}/2.json", vopts);
    double validatorSecond = Ms(Stopwatch.GetTimestamp() - r1);

    Console.WriteLine($"{name,-16} {bytes.Length,8} {first,8:F2}ms {warm,8:F2}ms {alloc / 1024.0,7:F0}KB {validatorFirst,12:F2}ms {validatorSecond,12:F2}ms");
}
