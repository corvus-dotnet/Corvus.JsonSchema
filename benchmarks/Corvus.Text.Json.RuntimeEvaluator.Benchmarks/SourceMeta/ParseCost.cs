using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// The parse cost of a corpus's instances: each instance parsed (and the document disposed) <c>loops</c> times after a
/// warm-up, the corpus figure being the sum over instances of the per-instance mean. The counterpart of the warm
/// evaluation figure, for the parse step the warm figure leaves out.
/// </summary>
public static class ParseCost
{
    public static int Run(string[] args)
    {
        int loops = args.Length > 0 && int.TryParse(args[0], out int l) ? l : 100;
        string dir = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
        string[] names = args.Length > 1 ? args[1..] : Directory.GetFiles(dir, "*-instances.jsonl").Select(f => Path.GetFileName(f)[..^"-instances.jsonl".Length]).Order().ToArray();
        Console.WriteLine($"{"corpus",-24} {"instances",9} {"bytes/inst",10} {"parse sum",12} {"ns/inst",10} {"ns/KB",8}");
        foreach (string name in names)
        {
            byte[][] lines = File.ReadAllLines(Path.Combine(dir, name + "-instances.jsonl")).Where(s => s.Length > 0).Select(Encoding.UTF8.GetBytes).ToArray();
            long bytes = lines.Sum(b => (long)b.Length);
            bool maps = Environment.GetEnvironmentVariable("PARSE_MAPS") == "1";

            // Warm for a second, pause past the tiering delay, warm again: the parser reaches its final tier only
            // after sustained work (three passes left yamllint's escapes path at tier 0, twice its true cost).
            WarmParse(lines, maps, 1000);
            Thread.Sleep(400);
            WarmParse(lines, maps, 300);

            double total = 0;
            foreach (byte[] line in lines)
            {
                long best = long.MaxValue;
                for (int batch = 0; batch < 3; batch++)
                {
                    long t0 = Stopwatch.GetTimestamp();
                    for (int i = 0; i < loops; i++)
                    {
                        using ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line);
                        if (maps)
                        {
                            BuildMaps(d);
                        }
                    }

                    best = Math.Min(best, Stopwatch.GetTimestamp() - t0);
                }

                total += best * 1e9 / Stopwatch.Frequency / loops;
            }

            Console.WriteLine($"{name,-24} {lines.Length,9} {(double)bytes / lines.Length,10:F0} {total / 1000,10:F1} us {total / lines.Length,10:F0} {total / (bytes / 1024.0),8:F0}");
        }

        return 0;
    }

    /// <summary>
    /// Parse and evaluate once, per instance: the shape of a service validating each request, timed per instance
    /// with the clock overhead subtracted, the corpus figure being the sum over instances of the per-instance mean
    /// over <c>loops</c>. Output in the columns of the warm measurement (sum of means, sum of stdev, overhead).
    /// </summary>
    public static int RunParseEval(string[] args)
    {
        int loops = args.Length > 0 && int.TryParse(args[0], out int l) ? l : 100;
        string[] names = args[1..];
        double tick = 1_000_000.0 / Stopwatch.Frequency;
        Console.WriteLine($"{"corpus",-24} {"instances",9} {"sum of means",14} {"sum of stdev",14} {"overhead ns",12} {"alloc B/eval",12}");
        foreach (string name in names)
        {
            using var c = new SourceMetaCase(name);
            string dir = Environment.GetEnvironmentVariable("COLD_ROOT") ?? Path.Combine(AppContext.BaseDirectory, "sourcemeta");
            byte[][] lines = File.ReadAllLines(Path.Combine(dir, name + "-instances.jsonl")).Where(s => s.Length > 0).Select(Encoding.UTF8.GetBytes).ToArray();
            JsonSchemaEvaluator evaluator = c.Evaluator;
            // Warm for a second, pause past the tiering delay, warm again, as the warm measurement does: the parser is
            // a large method that reaches its final tier only after sustained work.
            WarmParseEval(lines, evaluator, 1000);
            Thread.Sleep(400);
            WarmParseEval(lines, evaluator, 300);

            bool noEval = Environment.GetEnvironmentVariable("PE_NOEVAL") == "1";
            double empty = BlazeBasis.ClockOverheadUs(loops, tick);
            double total = 0;
            double totalStdev = 0;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            foreach (byte[] line in lines)
            {
                double sum = 0;
                double squares = 0;
                bool sink = false;
                for (int i = 0; i < loops; i++)
                {
                    long a = Stopwatch.GetTimestamp();
                    using (ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line))
                    {
                        sink ^= noEval || evaluator.Evaluate(d.RootElement);
                    }

                    long b = Stopwatch.GetTimestamp();
                    double delay = Math.Max(0, ((b - a) * tick) - empty);
                    sum += delay;
                    squares += delay * delay;
                }

                GC.KeepAlive(sink);
                double mean = sum / loops;
                total += mean;
                totalStdev += Math.Sqrt(Math.Max(0, (squares / loops) - (mean * mean)));
            }

            double allocPerEval = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)(loops * (long)lines.Length);
            Console.WriteLine($"{name,-24} {lines.Length,9} {Format(total),14} {Format(totalStdev),14} {empty * 1000,12:F1} {allocPerEval,12:F2}");
            if (Environment.GetEnvironmentVariable("PE_DIAG") == "1")
            {
                int gen0 = GC.CollectionCount(0);
                int gen1 = GC.CollectionCount(1);
                long t0 = Stopwatch.GetTimestamp();
                Span<double> sample = stackalloc double[8];
                int n = 0;
                foreach (byte[] line in lines)
                {
                    long best = long.MaxValue;
                    long worst = 0;
                    for (int i = 0; i < loops; i++)
                    {
                        long a = Stopwatch.GetTimestamp();
                        using (ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line))
                        {
                            GC.KeepAlive(evaluator.Evaluate(d.RootElement));
                        }

                        long dt = Stopwatch.GetTimestamp() - a;
                        best = Math.Min(best, dt);
                        worst = Math.Max(worst, dt);
                    }

                    if (n < 8)
                    {
                        Console.WriteLine($"    instance {n}: {line.Length} bytes, best {best * tick * 1000:F0} ns, worst {worst * tick:F1} us");
                    }

                    n++;
                }

                long t1 = Stopwatch.GetTimestamp();
                Console.WriteLine($"    pass: {(t1 - t0) * tick / 1000:F2} ms wall for {loops * lines.Length} parse+eval, gen0 GCs {GC.CollectionCount(0) - gen0}, gen1 {GC.CollectionCount(1) - gen1}, GC mode {(System.Runtime.GCSettings.IsServerGC ? "server" : "workstation")}, latency {System.Runtime.GCSettings.LatencyMode}");
            }
        }

        return 0;
    }

    private static void WarmParseEval(byte[][] lines, JsonSchemaEvaluator evaluator, int milliseconds)
    {
        long end = Stopwatch.GetTimestamp() + (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        bool sink = false;
        while (Stopwatch.GetTimestamp() < end)
        {
            foreach (byte[] line in lines)
            {
                using ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line);
                sink ^= evaluator.Evaluate(d.RootElement);
            }
        }

        GC.KeepAlive(sink);
    }

    private static string Format(double us)
    {
        return us >= 1000 ? $"{us / 1000:F2} ms" : $"{us:F2} us";
    }

    private static void WarmParse(byte[][] lines, bool maps, int milliseconds)
    {
        long end = Stopwatch.GetTimestamp() + (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        while (Stopwatch.GetTimestamp() < end)
        {
            foreach (byte[] line in lines)
            {
                using ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line);
                if (maps)
                {
                    BuildMaps(d);
                }
            }
        }
    }

    /// <summary>With PARSE_MAPS=1: the property map of every object built after the parse, to price a parse that hashes names.</summary>
    private static void BuildMaps(ParsedJsonDocument<JsonElement> d)
    {
        if (!d.TryGetRawSpans(out _, out ReadOnlySpan<byte> rows, out _))
        {
            return;
        }

        Corvus.Text.Json.Internal.IJsonDocument doc = d;
        int built = 0;

        // The rows span is the pooled buffer; the document's rows end at the root's end row.
        int used = ((int)(System.Runtime.InteropServices.MemoryMarshal.Read<uint>(rows.Slice(8, 4)) & 0x0FFFFFFF) + 1) * 12;
        for (int index = 0; index + 12 <= Math.Min(used, rows.Length); index += 12)
        {
            uint union = System.Runtime.InteropServices.MemoryMarshal.Read<uint>(rows.Slice(index + 8, 4));
            if ((union >> 28) == (uint)JsonTokenType.StartObject)
            {
                try
                {
                    doc.EnsurePropertyMap(index);
                    built++;
                }
                catch (Exception e) when (Environment.GetEnvironmentVariable("PARSE_MAPS_DIAG") == "1")
                {
                    Console.WriteLine($"map failed at row index {index} (row {index / 12} of {rows.Length / 12}), count {doc.GetPropertyCount(index)}, maps built before {built}: {e.GetType().Name} {e.Message}");
                    Console.WriteLine(d.RootElement.GetRawText());
                    throw;
                }
            }
        }
    }
}
