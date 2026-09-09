using System.Diagnostics;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Interleaved min-of-N timing: alternates the two workloads so that bursty background load hits both,
/// and keeps the fastest observation of each, an estimate of the uncontended time.
/// </summary>
public static class QuickTimer
{
    public static (double ANs, double BNs) Run(int rounds, Func<int> a, Func<int> b)
    {
        // Warm up both.
        for (int i = 0; i < 3; i++)
        {
            a();
            b();
        }

        double minA = double.MaxValue;
        double minB = double.MaxValue;
        int sink = 0;
        for (int round = 0; round < rounds; round++)
        {
            long t0 = Stopwatch.GetTimestamp();
            sink += a();
            long t1 = Stopwatch.GetTimestamp();
            sink += b();
            long t2 = Stopwatch.GetTimestamp();
            minA = Math.Min(minA, ToNs(t1 - t0));
            minB = Math.Min(minB, ToNs(t2 - t1));
        }

        GC.KeepAlive(sink);
        return (minA, minB);
    }

    public static (double ANs, double BNs, double CNs) Run3(int rounds, int inner, Func<bool> a, Func<bool> b, Func<bool> c)
    {
        for (int i = 0; i < 100; i++)
        {
            a();
            b();
            c();
        }

        double minA = double.MaxValue;
        double minB = double.MaxValue;
        double minC = double.MaxValue;
        bool sink = false;
        for (int round = 0; round < rounds; round++)
        {
            long t0 = Stopwatch.GetTimestamp();
            for (int i = 0; i < inner; i++)
            {
                sink ^= a();
            }

            long t1 = Stopwatch.GetTimestamp();
            for (int i = 0; i < inner; i++)
            {
                sink ^= b();
            }

            long t2 = Stopwatch.GetTimestamp();
            for (int i = 0; i < inner; i++)
            {
                sink ^= c();
            }

            long t3 = Stopwatch.GetTimestamp();
            minA = Math.Min(minA, ToNs(t1 - t0) / inner);
            minB = Math.Min(minB, ToNs(t2 - t1) / inner);
            minC = Math.Min(minC, ToNs(t3 - t2) / inner);
        }

        GC.KeepAlive(sink);
        return (minA, minB, minC);
    }

    public static string Format(double ns)
    {
        return ns >= 1_000_000 ? $"{ns / 1_000_000:F2} ms" : ns >= 1_000 ? $"{ns / 1_000:F2} us" : $"{ns:F0} ns";
    }

    private static double ToNs(long ticks) => ticks * 1_000_000_000.0 / Stopwatch.Frequency;
}
