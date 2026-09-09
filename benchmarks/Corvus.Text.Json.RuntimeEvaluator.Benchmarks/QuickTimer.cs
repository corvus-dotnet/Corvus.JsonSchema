using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Interleaved min-of-N timing: alternates the two workloads so that bursty background load hits both,
/// and keeps the fastest observation of each, an estimate of the uncontended time.
/// </summary>
public static class QuickTimer
{
    /// <summary>
    /// With <c>CORVUS_RT_QUICK_CPUTIME=1</c> (Linux only) rounds are timed by the calling thread's CPU time rather
    /// than wall clock, which removes the effect of being descheduled on a loaded host; cache and frequency
    /// effects remain. Pair with <c>DOTNET_TieredCompilation=0</c> for A/B runs of the same binary so that
    /// neither side depends on when the background tier-1 compilation gets scheduled.
    /// </summary>
    private static readonly bool UseThreadCpuTime = Environment.GetEnvironmentVariable("CORVUS_RT_QUICK_CPUTIME") == "1" && OperatingSystem.IsLinux();

    private const int ClockThreadCpuTimeId = 3;

    /// <summary>Gets the current time in nanoseconds on the selected clock.</summary>
    public static long NowNs()
    {
        if (UseThreadCpuTime)
        {
            ClockGetTime(ClockThreadCpuTimeId, out Timespec ts);
            return (ts.Seconds * 1_000_000_000L) + ts.Nanoseconds;
        }

        return (long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));
    }

    [DllImport("libc", EntryPoint = "clock_gettime")]
    private static extern int ClockGetTime(int clockId, out Timespec timespec);

    [StructLayout(LayoutKind.Sequential)]
    private struct Timespec
    {
        public long Seconds;
        public long Nanoseconds;
    }

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
            long t0 = NowNs();
            sink += a();
            long t1 = NowNs();
            sink += b();
            long t2 = NowNs();
            minA = Math.Min(minA, t1 - t0);
            minB = Math.Min(minB, t2 - t1);
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
            long t0 = NowNs();
            for (int i = 0; i < inner; i++)
            {
                sink ^= a();
            }

            long t1 = NowNs();
            for (int i = 0; i < inner; i++)
            {
                sink ^= b();
            }

            long t2 = NowNs();
            for (int i = 0; i < inner; i++)
            {
                sink ^= c();
            }

            long t3 = NowNs();
            minA = Math.Min(minA, (double)(t1 - t0) / inner);
            minB = Math.Min(minB, (double)(t2 - t1) / inner);
            minC = Math.Min(minC, (double)(t3 - t2) / inner);
        }

        GC.KeepAlive(sink);
        return (minA, minB, minC);
    }

    public static string Format(double ns)
    {
        return ns >= 1_000_000 ? $"{ns / 1_000_000:F2} ms" : ns >= 1_000 ? $"{ns / 1_000:F2} us" : $"{ns:F0} ns";
    }

}
