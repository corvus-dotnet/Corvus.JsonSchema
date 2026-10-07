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

    /// <summary>
    /// The harness's choice of code generation, from its environment: <c>CORVUS_RT_CODEGEN=1</c> for after warm-up,
    /// and with <c>CORVUS_RT_CODEGEN_THRESHOLD=0</c> eagerly. Unset, the interpreter.
    /// </summary>
    /// <returns>The option's value.</returns>
    public static JsonSchemaCodeGeneration CodeGeneration()
    {
        if (Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN") != "1")
        {
            return JsonSchemaCodeGeneration.Disabled;
        }

        return Environment.GetEnvironmentVariable("CORVUS_RT_CODEGEN_THRESHOLD") == "0" ? JsonSchemaCodeGeneration.Eager : JsonSchemaCodeGeneration.AfterWarmUp;
    }

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
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = dialect, CodeGeneration = CodeGeneration() });
        long compileEnd = Stopwatch.GetTimestamp();
        ValidateAll(evaluator, docs);
        long coldEnd = Stopwatch.GetTimestamp();

        // The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it.
        // PROTOCOL_WARMUP_MS lengthens the warm-up (for profiling the steady state); the protocol's is 2 seconds.
        long warmUpMs = long.TryParse(Environment.GetEnvironmentVariable("PROTOCOL_WARMUP_MS"), out long ms) ? ms : 2000;
        long warmUpEnd = Stopwatch.GetTimestamp() + (warmUpMs * Stopwatch.Frequency / 1000);
        long warm = 0;
        for (int i = 0; i < MinWarmUpPasses || Stopwatch.GetTimestamp() < warmUpEnd; i++)
        {
            long start = Stopwatch.GetTimestamp();
            ValidateAll(evaluator, docs);
            warm = Stopwatch.GetTimestamp() - start;
        }

        // PROTOCOL_ALLOC=1 reports what a warm pass allocates (nothing, when the evaluation path is clean).
        if (Environment.GetEnvironmentVariable("PROTOCOL_ALLOC") == "1")
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            ValidateAll(evaluator, docs);
            Console.Error.WriteLine($"allocated in one warm pass: {GC.GetAllocatedBytesForCurrentThread() - before} bytes over {docs.Length} documents");
        }

        static long Ns(long ticks) => (long)(ticks * 1_000_000_000.0 / Stopwatch.Frequency);
        Console.WriteLine($"{Ns(coldEnd - compileEnd)},{Ns(warm)},{Ns(compileEnd - parseEnd)},{Ns(parseEnd - parseStart)}");
        return 0;
    }

    /// <summary>
    /// The warm time of parsing a corpus's instances (<c>parsewarm &lt;corpus&gt;</c>), by the protocol's warm-up rule:
    /// passes for 2 seconds (at least 100), each parsing every instance and disposing its document, and the last
    /// pass's time. Prints the time in nanoseconds.
    /// </summary>
    /// <summary>
    /// The first pass, evaluation by evaluation (<c>tiertrace corpus</c>): what the evaluation that starts the
    /// background compilation costs, and what the evaluations after it cost while the compilation runs. Prints the
    /// pass's time, the median evaluation, the 1,000th evaluation (which starts it), the time of the evaluations
    /// after it over what their count would cost at the median before it, and the slowest five (index:microseconds).
    /// </summary>
    public static int RunTierTrace(string[] args)
    {
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        byte[] schema = File.ReadAllBytes(Path.Combine(root, args[0] + "-schema.json"));
        ParsedJsonDocument<JsonElement>[] docs = [.. File.ReadAllLines(Path.Combine(root, args[0] + "-instances.jsonl")).Where(l => l.Length > 0).Select(l => ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(l)))];
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.Draft7, CodeGeneration = CodeGeneration() });
        if (Environment.GetEnvironmentVariable("TIERTRACE_WARM_POOL") == "1")
        {
            // As in an application whose thread pool is already running (a server): one item run and waited for.
            using var ran = new ManualResetEventSlim();
            ThreadPool.QueueUserWorkItem(_ => ran.Set());
            ran.Wait();
        }

        long[] ticks = new long[docs.Length];
        int valid = 0;
        for (int i = 0; i < docs.Length; i++)
        {
            long start = Stopwatch.GetTimestamp();
            valid += evaluator.Evaluate(docs[i].RootElement) ? 1 : 0;
            ticks[i] = Stopwatch.GetTimestamp() - start;
        }

        double Microseconds(long t) => t * 1_000_000.0 / Stopwatch.Frequency;
        long[] before = [.. ticks.Take(Math.Min(999, ticks.Length)).Skip(50).OrderBy(t => t)];
        double median = Microseconds(before[before.Length / 2]);
        double total = Microseconds(ticks.Sum());
        double trigger = ticks.Length > 999 ? Microseconds(ticks[999]) : 0;
        double after = ticks.Length > 1000 ? Microseconds(ticks.Skip(1000).Sum()) : 0;
        int afterCount = Math.Max(0, ticks.Length - 1000);
        string slowest = string.Join(" ", ticks.Select((t, i) => (t, i)).OrderByDescending(x => x.t).Take(5).Select(x => $"{x.i}:{Microseconds(x.t):F0}"));
        Console.WriteLine($"{args[0]},total {total:F0} us,median {median:F2} us,evaluation 1000 {trigger:F0} us,after it {after:F0} us over {afterCount * median:F0} us expected,slowest {slowest},valid {valid}");
        return 0;
    }

    public static int RunParse(string[] args)
    {
        string root = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        byte[][] texts = [.. File.ReadAllLines(Path.Combine(root, args[0] + "-instances.jsonl")).Where(l => l.Length > 0).Select(System.Text.Encoding.UTF8.GetBytes)];
        long warmUpEnd = Stopwatch.GetTimestamp() + (2 * Stopwatch.Frequency);
        long warm = 0;
        int rows = 0;
        for (int i = 0; i < MinWarmUpPasses || Stopwatch.GetTimestamp() < warmUpEnd; i++)
        {
            long start = Stopwatch.GetTimestamp();
            rows += ParseAll(texts);
            warm = Stopwatch.GetTimestamp() - start;
        }

        Console.WriteLine((long)(warm * 1_000_000_000.0 / Stopwatch.Frequency));
        return rows == 0 ? 1 : 0;
    }

    private static int ParseAll(byte[][] texts)
    {
        int kinds = 0;
        foreach (byte[] text in texts)
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(text);
            kinds += (int)document.RootElement.ValueKind;
        }

        return kinds;
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