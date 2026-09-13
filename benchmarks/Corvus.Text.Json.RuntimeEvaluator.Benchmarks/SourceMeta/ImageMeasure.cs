// <copyright file="ImageMeasure.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Measures, per Sourcemeta corpus, the cost of compiling the schema against the cost of loading a program image
/// of it, with regular expressions interpreted and compiled, and checks that the two programs agree on every instance.
/// </summary>
public static class ImageMeasure
{
    public static int Run(string[] filters)
    {
        int rounds = 15;
        Console.WriteLine($"{"schema",-18} {"bytes",8} {"image",8} {"nodes",6} {"cmp/int",9} {"load/int",9} {"cmp/rx",9} {"load/rx",9} {"cmp KB",8} {"load KB",8} agree");
        int disagreements = 0;
        double sumCompile = 0, sumLoad = 0, sumCompileRx = 0, sumLoadRx = 0;
        long sumBytes = 0, sumImage = 0;
        int count = 0;
        foreach ((string file, JsonSchemaDialect dialect) in SourceMetaCases.All)
        {
            if (filters.Length > 0 && !Array.Exists(filters, f => file.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string dir = Path.Combine(AppContext.BaseDirectory, "sourcemeta");
            byte[] schema = File.ReadAllBytes(Path.Combine(dir, file + "-schema.json"));
            string[] lines = File.ReadAllLines(Path.Combine(dir, file + "-instances.jsonl"));
            var interpreted = new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, CompileRegularExpressions = false };
            var compiledRx = new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, CompileRegularExpressions = true };

            byte[] image;
            int nodes;
            using (JsonSchemaEvaluator e = JsonSchemaEvaluator.Compile(schema, interpreted))
            {
                image = e.ToProgramImage();
                nodes = e.NodeCount;
            }

            (double compile, long compileAlloc) = Measure(rounds, () => JsonSchemaEvaluator.Compile(schema, interpreted));
            (double load, long loadAlloc) = Measure(rounds, () => JsonSchemaEvaluator.FromProgramImage(image, interpreted));
            (double compileRx, _) = Measure(rounds, () => JsonSchemaEvaluator.Compile(schema, compiledRx));
            (double loadRx, _) = Measure(rounds, () => JsonSchemaEvaluator.FromProgramImage(image, compiledRx));

            int mismatches = 0;
            using (JsonSchemaEvaluator a = JsonSchemaEvaluator.Compile(schema, interpreted))
            using (JsonSchemaEvaluator b = JsonSchemaEvaluator.FromProgramImage(image, interpreted))
            {
                foreach (string line in lines)
                {
                    using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(line);
                    if (a.Evaluate(doc.RootElement) != b.Evaluate(doc.RootElement))
                    {
                        mismatches++;
                    }
                }
            }

            disagreements += mismatches;
            sumCompile += compile;
            sumLoad += load;
            sumCompileRx += compileRx;
            sumLoadRx += loadRx;
            sumBytes += schema.Length;
            sumImage += image.Length;
            count++;
            Console.WriteLine($"{file,-18} {schema.Length,8} {image.Length,8} {nodes,6} {compile,7:F2}ms {load,7:F2}ms {compileRx,7:F2}ms {loadRx,7:F2}ms {compileAlloc / 1024.0,7:F0} {loadAlloc / 1024.0,7:F0} {(mismatches == 0 ? "yes" : mismatches + " differ")}");
        }

        Console.WriteLine($"{"total",-18} {sumBytes,8} {sumImage,8} {string.Empty,6} {sumCompile,7:F2}ms {sumLoad,7:F2}ms {sumCompileRx,7:F2}ms {sumLoadRx,7:F2}ms  ({count} corpora, {disagreements} disagreements)");
        return disagreements;
    }

    private static (double Ms, long Alloc) Measure(int rounds, Func<JsonSchemaEvaluator> create)
    {
        double best = double.MaxValue;
        long alloc = 0;
        for (int i = 0; i < rounds; i++)
        {
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            long t0 = Stopwatch.GetTimestamp();
            using (create())
            {
            }

            double ms = (Stopwatch.GetTimestamp() - t0) * 1000.0 / Stopwatch.Frequency;
            if (ms < best)
            {
                best = ms;
                alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
            }
        }

        return (best, alloc);
    }
}
