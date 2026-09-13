// <copyright file="GeneratedRun.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// The generated models (the shipping generator's validation code, one type per corpus) measured the way the runtime
/// evaluator is: <c>generated warm &lt;loops&gt; &lt;corpus&gt;</c> is the per-evaluation measurement of
/// <see cref="BlazeBasis"/>, with allocations per evaluation; <c>generated cold &lt;corpus&gt;</c> is one cold
/// pass in a fresh process (read the instances, parse and evaluate each once), the first evaluation timed apart,
/// which stands in for compile since generated code has no compile step at run time.
/// </summary>
public static class GeneratedRun
{
    /// <summary>Set by <c>generated parseeval</c>: each timed evaluation parses its instance first (see ParseCost.RunParseEval).</summary>
    private static bool ParseEval;

    public static int Main(string[] args)
    {
        bool cold = args.Length > 0 && args[0] == "cold";
        ParseEval = args.Length > 0 && args[0] == "parseeval";
        int loop = !cold && args.Length > 1 && int.TryParse(args[1], out int l) ? l : 200;
        string[] names = cold ? args[1..] : args[2..];
        string dir = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        if (!cold)
        {
            Console.WriteLine($"{"corpus",-24} {"instances",9} {"sum of means",14} {"sum of stdev",14} {"overhead ns",12} {"alloc B/eval",12}");
        }

        foreach (string name in names)
        {
            string[] lines = File.ReadAllLines(Path.Combine(dir, name + "-instances.jsonl"));
            foreach ((string file, Action<string[], int, bool> run) in GeneratedCases.All)
            {
                if (file == name)
                {
                    run(lines, loop, cold);
                }
            }
        }

        return 0;
    }

    public static void Run<T>(string name, string[] lines, int loop, bool cold)
        where T : struct, IJsonElement<T>
    {
        double tick = 1_000_000.0 / Stopwatch.Frequency;
        if (cold)
        {
            long t0 = Stopwatch.GetTimestamp();
            int valid = 0;
            long firstDone = 0;
            for (int i = 0; i < lines.Length; i++)
            {
                using ParsedJsonDocument<T> doc = ParsedJsonDocument<T>.Parse(lines[i]);
                if (doc.RootElement.EvaluateSchema())
                {
                    valid++;
                }

                if (i == 0)
                {
                    firstDone = Stopwatch.GetTimestamp();
                }
            }

            long t1 = Stopwatch.GetTimestamp();
            Console.WriteLine($"{name} generated first {(firstDone - t0) * tick / 1000:F2} ms, pass {(t1 - t0) * tick / 1000:F2} ms, valid {valid}/{lines.Length}");
            return;
        }

        if (ParseEval)
        {
            RunParseEval<T>(name, lines, loop, tick);
            return;
        }

        var docs = new ParsedJsonDocument<T>[lines.Length];
        for (int i = 0; i < lines.Length; i++)
        {
            docs[i] = ParsedJsonDocument<T>.Parse(lines[i]);
        }

        // Warm for a second, pause past the tiering delay, warm again, as the runtime measurement does.
        Warm(docs, 1000);
        Thread.Sleep(400);
        Warm(docs, 300);

        double empty = BlazeBasis.ClockOverheadUs(loop, tick);
        double total = 0;
        double totalStdev = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        foreach (ParsedJsonDocument<T> doc in docs)
        {
            double sum = 0;
            double squares = 0;
            bool sink = false;
            for (int i = 0; i < loop; i++)
            {
                long a = Stopwatch.GetTimestamp();
                sink ^= doc.RootElement.EvaluateSchema();
                long b = Stopwatch.GetTimestamp();
                double delay = Math.Max(0, ((b - a) * tick) - empty);
                sum += delay;
                squares += delay * delay;
            }

            GC.KeepAlive(sink);
            double mean = sum / loop;
            total += mean;
            totalStdev += loop == 1 ? 0 : Math.Sqrt(Math.Max(0, (squares / loop) - (mean * mean)));
        }

        double allocPerEval = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)(loop * (long)docs.Length);
        Console.WriteLine($"{name,-24} {docs.Length,9} {Format(total),14} {Format(totalStdev),14} {empty * 1000,12:F1} {allocPerEval,12:F2}");
        foreach (ParsedJsonDocument<T> doc in docs)
        {
            doc.Dispose();
        }
    }

    private static void RunParseEval<T>(string name, string[] lines, int loop, double tick)
        where T : struct, IJsonElement<T>
    {
        byte[][] bytes = lines.Where(s => s.Length > 0).Select(System.Text.Encoding.UTF8.GetBytes).ToArray();
        WarmParseEval<T>(bytes, 1000);
        Thread.Sleep(400);
        WarmParseEval<T>(bytes, 300);

        double empty = BlazeBasis.ClockOverheadUs(loop, tick);
        double total = 0;
        double totalStdev = 0;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        foreach (byte[] line in bytes)
        {
            double sum = 0;
            double squares = 0;
            bool sink = false;
            for (int i = 0; i < loop; i++)
            {
                long a = Stopwatch.GetTimestamp();
                using (ParsedJsonDocument<T> d = ParsedJsonDocument<T>.Parse(line))
                {
                    sink ^= d.RootElement.EvaluateSchema();
                }

                long b = Stopwatch.GetTimestamp();
                double delay = Math.Max(0, ((b - a) * tick) - empty);
                sum += delay;
                squares += delay * delay;
            }

            GC.KeepAlive(sink);
            double mean = sum / loop;
            total += mean;
            totalStdev += Math.Sqrt(Math.Max(0, (squares / loop) - (mean * mean)));
        }

        double allocPerEval = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)(loop * (long)bytes.Length);
        Console.WriteLine($"{name,-24} {bytes.Length,9} {Format(total),14} {Format(totalStdev),14} {empty * 1000,12:F1} {allocPerEval,12:F2}");
    }

    private static void WarmParseEval<T>(byte[][] lines, int milliseconds)
        where T : struct, IJsonElement<T>
    {
        long end = Stopwatch.GetTimestamp() + (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        bool sink = false;
        while (Stopwatch.GetTimestamp() < end)
        {
            foreach (byte[] line in lines)
            {
                using ParsedJsonDocument<T> d = ParsedJsonDocument<T>.Parse(line);
                sink ^= d.RootElement.EvaluateSchema();
            }
        }

        GC.KeepAlive(sink);
    }

    private static void Warm<T>(ParsedJsonDocument<T>[] docs, int milliseconds)
        where T : struct, IJsonElement<T>
    {
        long end = Stopwatch.GetTimestamp() + (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        int sink = 0;
        while (Stopwatch.GetTimestamp() < end)
        {
            foreach (ParsedJsonDocument<T> doc in docs)
            {
                sink += doc.RootElement.EvaluateSchema() ? 1 : 0;
            }
        }

        GC.KeepAlive(sink);
    }

    private static string Format(double us)
    {
        return us >= 1000 ? $"{us / 1000:F2} ms" : $"{us:F2} us";
    }
}
