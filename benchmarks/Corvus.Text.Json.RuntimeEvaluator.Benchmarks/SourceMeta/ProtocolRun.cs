// <copyright file="ProtocolRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// One run of a corpus by the jsonschema-benchmark protocol, as the other engines' harnesses run it (the Java port's
/// <c>Main</c>): parse every instance, compile the schema, validate once cold, warm up for 2 seconds (at least 100
/// passes) and report the last warm-up pass. Prints <c>cold,warm,compile,parse</c> in nanoseconds
/// (<c>protocol &lt;corpus&gt;</c>). The evaluator's runtime codegen switch applies.
/// </summary>
public static class ProtocolRun
{
    private const int MinWarmUpPasses = 100;

    public static int Run(string[] args)
    {
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        string corpus = args[0];
        JsonSchemaDialect dialect = JsonSchemaDialect.Draft7;
        foreach ((string file, JsonSchemaDialect d) in SourceMetaCases.All)
        {
            if (file == corpus)
            {
                dialect = d;
            }
        }

        byte[] schema = File.ReadAllBytes(Path.Combine(root, corpus + "-schema.json"));
        byte[][] texts = [.. File.ReadAllLines(Path.Combine(root, corpus + "-instances.jsonl")).Where(l => l.Length > 0).Select(System.Text.Encoding.UTF8.GetBytes)];

        long parseStart = Stopwatch.GetTimestamp();
        var docs = new ParsedJsonDocument<JsonElement>[texts.Length];
        for (int i = 0; i < texts.Length; i++)
        {
            docs[i] = ParsedJsonDocument<JsonElement>.Parse(texts[i]);
        }

        long parseEnd = Stopwatch.GetTimestamp();
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect });
        long compileEnd = Stopwatch.GetTimestamp();
        ValidateAll(evaluator, docs);
        long coldEnd = Stopwatch.GetTimestamp();

        // The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it.
        long warmUpEnd = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);
        long warm = 0;
        for (int i = 0; i < MinWarmUpPasses || Stopwatch.GetTimestamp() < warmUpEnd; i++)
        {
            long start = Stopwatch.GetTimestamp();
            ValidateAll(evaluator, docs);
            warm = Stopwatch.GetTimestamp() - start;
        }

        static long Ns(long ticks) => (long)(ticks * 1_000_000_000.0 / Stopwatch.Frequency);
        Console.WriteLine($"{Ns(coldEnd - compileEnd)},{Ns(warm)},{Ns(compileEnd - parseEnd)},{Ns(parseEnd - parseStart)}");
        return 0;
    }

    private static int ValidateAll(JsonSchemaEvaluator evaluator, ParsedJsonDocument<JsonElement>[] docs)
    {
        int valid = 0;
        foreach (ParsedJsonDocument<JsonElement> d in docs)
        {
            valid += evaluator.Evaluate(d.RootElement) ? 1 : 0;
        }

        return valid;
    }
}