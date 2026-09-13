using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;

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
            for (int w = 0; w < 3; w++)
            {
                foreach (byte[] line in lines)
                {
                    using ParsedJsonDocument<JsonElement> d = ParsedJsonDocument<JsonElement>.Parse(line);
                }
            }

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
                    }

                    best = Math.Min(best, Stopwatch.GetTimestamp() - t0);
                }

                total += best * 1e9 / Stopwatch.Frequency / loops;
            }

            Console.WriteLine($"{name,-24} {lines.Length,9} {(double)bytes / lines.Length,10:F0} {total / 1000,10:F1} us {total / lines.Length,10:F0} {total / (bytes / 1024.0),8:F0}");
        }

        return 0;
    }
}
