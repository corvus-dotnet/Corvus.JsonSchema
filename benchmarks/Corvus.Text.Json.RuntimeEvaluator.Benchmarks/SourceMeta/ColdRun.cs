// <copyright file="ColdRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// One cold run of a corpus in a fresh process: time from process start to main, schema read and compile (or
/// program image load), the first evaluation, and one pass over every instance (parse and evaluate), in the shape
/// the Blaze CLI's <c>validate --fast</c> does the same work. <c>cold prepare</c> writes the program images.
/// </summary>
public static class ColdRun
{
    public static int Run(string[] args)
    {
        // The harness finds the corpora next to itself; the cold runner (a separate executable) is told where they are.
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        string images = Path.Combine(root, "..", "cold-images");
        if (args.Length > 0 && args[0] == "prepare")
        {
            Directory.CreateDirectory(images);
            foreach ((string file, JsonSchemaDialect dialect) in SourceMetaCases.All)
            {
                using JsonSchemaEvaluator e = JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(root, file + "-schema.json")), new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
                File.WriteAllBytes(Path.Combine(images, file + ".bin"), e.ToProgramImage());
            }

            return 0;
        }

        long startToMain = (long)(DateTime.UtcNow - Process.GetCurrentProcess().StartTime.ToUniversalTime()).TotalMilliseconds;
        string corpus = args[0];
        bool fromImage = args.Length > 1 && args[1] == "image";
        JsonSchemaDialect corpusDialect = JsonSchemaDialect.Draft7;
        foreach ((string file, JsonSchemaDialect dialect) in SourceMetaCases.All)
        {
            if (file == corpus)
            {
                corpusDialect = dialect;
            }
        }

        var options = new JsonSchemaEvaluatorOptions { DefaultDialect = corpusDialect };
        long t0 = Stopwatch.GetTimestamp();
        using JsonSchemaEvaluator evaluator = fromImage
            ? JsonSchemaEvaluator.FromProgramImage(File.ReadAllBytes(Path.Combine(images, corpus + ".bin")), options)
            : JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(root, corpus + "-schema.json")), options);
        long t1 = Stopwatch.GetTimestamp();

        string[] lines = File.ReadAllLines(Path.Combine(root, corpus + "-instances.jsonl"));
        long t2 = Stopwatch.GetTimestamp();
        int valid = 0;
        long firstDone = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(lines[i]);
            if (evaluator.Evaluate(doc.RootElement))
            {
                valid++;
            }

            if (i == 0)
            {
                firstDone = Stopwatch.GetTimestamp();
            }
        }

        long t3 = Stopwatch.GetTimestamp();
        double Ms(long from, long to) => (to - from) * 1000.0 / Stopwatch.Frequency;
        Console.WriteLine($"{corpus} {(fromImage ? "image" : "compile")} start-to-main {startToMain} ms, {(fromImage ? "load" : "compile")} {Ms(t0, t1):F2} ms, read {Ms(t1, t2):F2} ms, first {Ms(t2, firstDone):F2} ms, pass {Ms(t2, t3):F2} ms, valid {valid}/{lines.Length}");
        return 0;
    }
}
