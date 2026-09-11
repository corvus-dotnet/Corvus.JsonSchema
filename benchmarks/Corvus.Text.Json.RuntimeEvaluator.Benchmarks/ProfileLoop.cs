using System.Diagnostics;
using Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>Runs one workload for N seconds so a sampling profiler can be attached.</summary>
public static class ProfileLoop
{
    public static int Run(string[] args)
    {
        string what = args.Length > 0 ? args[0] : "dynamicref";
        int seconds = args.Length > 1 && int.TryParse(args[1], out int s) ? s : 8;
        var sw = Stopwatch.StartNew();
        long iterations = 0;
        if (what == "dynamicref" || what == "object" || what == "unevaluated" || what == "verbose" || what == "oneof-typed" || what == "oneof-runtime")
        {
            var micro = new MicroBenchmarks();
            micro.Setup();
            Func<bool> f = what switch { "dynamicref" => micro.DynamicRef_Runtime, "object" => micro.Object_Runtime, "verbose" => micro.Verbose_Runtime, "oneof-typed" => micro.OneOf_Typed, "oneof-runtime" => micro.OneOf_Runtime, _ => micro.Unevaluated_Runtime };
            bool sink = false;
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                for (int i = 0; i < 1000; i++)
                {
                    sink ^= f();
                }

                iterations += 1000;
            }

            micro.Cleanup();
            GC.KeepAlive(sink);
        }
        else
        {
            using var c = new SourceMetaCase(what);
            int sink = 0;
            while (sw.Elapsed.TotalSeconds < seconds)
            {
                sink += c.EvaluateAll();
                iterations++;
            }

            GC.KeepAlive(sink);
        }

        Console.WriteLine($"{what}: {iterations} iterations in {sw.Elapsed.TotalSeconds:F1}s");
        return 0;
    }
}
