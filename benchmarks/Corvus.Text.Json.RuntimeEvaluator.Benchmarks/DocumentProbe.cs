using System.Diagnostics;
using System.Text;
using Corvus.MicroModels;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Evaluates one schema over the same instance held in documents of every model type, parsed from a string and
/// from bytes, to see whether evaluation cost depends on the document's element type.
/// </summary>
public static class DocumentProbe
{
    public static int Run(string[] args)
    {
        int rounds = args.Length > 0 && int.TryParse(args[0], out int r) ? r : 30;
        string schemaDir = Path.Combine(AppContext.BaseDirectory, "micro-schemas");
        using JsonSchemaEvaluator objectSchema = JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(schemaDir, "object.json")));
        using JsonSchemaEvaluator oneOfSchema = JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(schemaDir, "oneof.json")));
        byte[] bytes = Encoding.UTF8.GetBytes(MicroBenchmarks.ObjectJsonText);

        var docs = new List<(string Name, IJsonDocument Doc)>
        {
            ("JsonElement/string", ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("JsonElement/bytes", ParsedJsonDocument<JsonElement>.Parse(bytes)),
            ("MicroObject/string", ParsedJsonDocument<MicroObject>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("MicroObject/bytes", ParsedJsonDocument<MicroObject>.Parse(bytes)),
            ("MicroOneOf/string", ParsedJsonDocument<MicroOneOf>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("MicroOneOf/bytes", ParsedJsonDocument<MicroOneOf>.Parse(bytes)),
            ("MicroString/string", ParsedJsonDocument<MicroString>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("MicroArray/string", ParsedJsonDocument<MicroArray>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("MicroDynamic/string", ParsedJsonDocument<MicroDynamic>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("MicroUnevaluated/string", ParsedJsonDocument<MicroUnevaluated>.Parse(MicroBenchmarks.ObjectJsonText)),
            ("JsonElement/string#2", ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.ObjectJsonText)),
        };

        foreach ((string schemaName, JsonSchemaEvaluator schema) in new[] { ("object", objectSchema), ("oneOf", oneOfSchema) })
        {
            Console.WriteLine($"schema {schemaName}: min ns per evaluation, two passes");
            foreach (int pass in new[] { 1, 2 })
            {
                foreach ((string name, IJsonDocument doc) in docs)
                {
                    double best = double.MaxValue;
                    for (int round = 0; round < rounds; round++)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        bool sink = false;
                        for (int i = 0; i < 2000; i++)
                        {
                            sink ^= schema.Evaluate(doc, 0);
                        }

                        long t1 = Stopwatch.GetTimestamp();
                        GC.KeepAlive(sink);
                        best = Math.Min(best, (t1 - t0) * (1_000_000_000.0 / Stopwatch.Frequency) / 2000);
                    }

                    Console.WriteLine($"  pass {pass} {name,-26} {best,8:F0} ns");
                }
            }
        }

        foreach ((_, IJsonDocument doc) in docs)
        {
            (doc as IDisposable)?.Dispose();
        }

        // Twelve identical documents parsed in sequence, once now (parse path already tiered up) and once more
        // after a collection, evaluated in parse order and in reverse: does cost follow the instance?
        foreach (string set in new[] { "set A", "set B (after GC)" })
        {
            if (set.StartsWith("set B"))
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }

            var same = new List<ParsedJsonDocument<JsonElement>>();
            for (int i = 0; i < 12; i++)
            {
                same.Add(ParsedJsonDocument<JsonElement>.Parse(MicroBenchmarks.ObjectJsonText));
            }

            Console.WriteLine($"{set}: object schema, min ns, forward then reverse");
            foreach (bool reverse in new[] { false, true })
            {
                var line = new StringBuilder(reverse ? "  reverse:" : "  forward:");
                for (int k = 0; k < same.Count; k++)
                {
                    ParsedJsonDocument<JsonElement> doc = same[reverse ? same.Count - 1 - k : k];
                    double best = double.MaxValue;
                    for (int round = 0; round < rounds; round++)
                    {
                        long t0 = Stopwatch.GetTimestamp();
                        bool sink = false;
                        for (int i = 0; i < 2000; i++)
                        {
                            sink ^= objectSchema.Evaluate(doc, 0);
                        }

                        long t1 = Stopwatch.GetTimestamp();
                        GC.KeepAlive(sink);
                        best = Math.Min(best, (t1 - t0) * (1_000_000_000.0 / Stopwatch.Frequency) / 2000);
                    }

                    line.Append($" {best,5:F0}");
                }

                Console.WriteLine(line);
            }

            foreach (ParsedJsonDocument<JsonElement> doc in same)
            {
                doc.Dispose();
            }
        }

        return 0;
    }
}
