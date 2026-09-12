using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Times the corpora the way the Sourcemeta CLI's <c>validate --benchmark --loop N</c> does, so the two can be
/// compared like for like: every instance is evaluated N times, each evaluation timed on its own with the clock's
/// own overhead measured and subtracted, and the corpus figure is the sum over its instances of the mean. The
/// process is warmed first so that the JIT's tiering, which a native evaluator has no equivalent of, does not
/// appear in the means.
/// </summary>
public static class BlazeBasis
{
    public static int Run(string[] args)
    {
        int loop = args.Length > 0 && int.TryParse(args[0], out int l) ? l : 200;
        string[] names = args.Length > 1 ? args[1..] : SourceMetaCases.All.Select(c => c.File).ToArray();
        double tick = 1_000_000.0 / Stopwatch.Frequency;

        var cases = new List<SourceMetaCase>();
        foreach (string name in names)
        {
            cases.Add(new SourceMetaCase(name));
        }

        // Warm every corpus for a second, pause past the tiering delay, warm again.
        Warm(cases, 1000);
        Thread.Sleep(400);
        Warm(cases, 300);

        Console.WriteLine($"{"corpus",-24} {"instances",9} {"sum of means",14} {"sum of stdev",14} {"overhead ns",12} {"alloc B/eval",12}");
        foreach (SourceMetaCase c in cases)
        {
            double empty = ClockOverheadUs(loop, tick);

            double total = 0;
            double totalStdev = 0;
            JsonSchemaEvaluator evaluator = c.Evaluator;
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            foreach (ParsedJsonDocument<JsonElement> doc in c.Documents)
            {
                double sum = 0;
                double squares = 0;
                bool sink = false;
                for (int i = 0; i < loop; i++)
                {
                    long a = Stopwatch.GetTimestamp();
                    sink ^= evaluator.Evaluate(doc.RootElement);
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

            double allocPerEval = (GC.GetAllocatedBytesForCurrentThread() - allocated) / (double)(loop * (long)c.Documents.Length);
            Console.WriteLine($"{c.Name,-24} {c.Documents.Length,9} {Format(total),14} {Format(totalStdev),14} {empty * 1000,12:F1} {allocPerEval,12:F2}");
        }

        foreach (SourceMetaCase c in cases)
        {
            c.Dispose();
        }

        return 0;
    }

    /// <summary>
    /// The clock's own cost per timed evaluation: a warmed, non-generic loop of timestamp pairs, the minimum mean over
    /// several batches, so that tier-0 code or a stray interruption cannot inflate what is subtracted.
    /// </summary>
    public static double ClockOverheadUs(int loop, double tick)
    {
        double best = double.MaxValue;
        for (int batch = 0; batch < 7; batch++)
        {
            best = Math.Min(best, EmptyLoopUs(loop, tick) / loop);
        }

        return best;
    }

    private static double EmptyLoopUs(int loop, double tick)
    {
        double empty = 0;
        for (int i = 0; i < loop; i++)
        {
            long a = Stopwatch.GetTimestamp();
            long b = Stopwatch.GetTimestamp();
            empty += (b - a) * tick;
        }

        return empty;
    }

    private static void Warm(List<SourceMetaCase> cases, int milliseconds)
    {
        long end = Stopwatch.GetTimestamp() + (long)(milliseconds / 1000.0 * Stopwatch.Frequency);
        int sink = 0;
        while (Stopwatch.GetTimestamp() < end)
        {
            foreach (SourceMetaCase c in cases)
            {
                sink += c.EvaluateAll();
            }
        }

        GC.KeepAlive(sink);
    }

    private static string Format(double us)
    {
        return us >= 1000 ? $"{us / 1000:F2} ms" : $"{us:F2} us";
    }
}