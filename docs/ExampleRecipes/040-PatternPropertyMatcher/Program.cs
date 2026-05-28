using Corvus.Text.Json;
using PatternPropertyMatcher.Models;

string json = """
    {
      "name": "server-01",
      "S_region": "eu-west",
      "S_status": "green",
      "I_cpu": 42,
      "I_memory": 73
    }
    """;

using ParsedJsonDocument<MetricBag> parsed = ParsedJsonDocument<MetricBag>.Parse(json);
MetricBag metrics = parsed.RootElement;

Console.WriteLine("Pattern property helper checks:");
Console.WriteLine($"  S_region is a string metric: {MetricBag.MatchesPatternJsonString("S_region"u8)}");
Console.WriteLine($"  I_cpu is an integer metric: {MetricBag.MatchesPatternJsonInt32("I_cpu"u8)}");
Console.WriteLine();

if (metrics.TryGetProperty("S_status"u8, out JsonElement statusValue) &&
    MetricBag.TryAsPatternJsonString("S_status"u8, in statusValue, out JsonString status))
{
    Console.WriteLine($"Status metric: {status}");
    Console.WriteLine();
}

MetricSummary summary = default;
metrics.MatchPatternProperties(ref summary, MetricVisitor.Instance);

Console.WriteLine("Matched metrics:");
Console.WriteLine($"  Name: {summary.Name}");
Console.WriteLine($"  String metrics: {summary.StringMetricCount}");
Console.WriteLine($"  Integer metrics: {summary.IntegerMetricCount}");
Console.WriteLine($"  CPU: {summary.Cpu}");
Console.WriteLine($"  Memory: {summary.Memory}");

readonly struct MetricVisitor : MetricBag.IPatternPropertyVisitor<MetricSummary>
{
    public static readonly MetricVisitor Instance = new();

    public bool VisitPatternJsonString(ReadOnlySpan<byte> name, in JsonString value, ref MetricSummary state)
    {
        state.StringMetricCount++;
        return true;
    }

    public bool VisitPatternJsonInt32(ReadOnlySpan<byte> name, in JsonInt32 value, ref MetricSummary state)
    {
        state.IntegerMetricCount++;

        if (name.SequenceEqual("I_cpu"u8))
        {
            state.Cpu = (int)value;
        }
        else if (name.SequenceEqual("I_memory"u8))
        {
            state.Memory = (int)value;
        }

        return true;
    }

    public bool VisitUnmatched(ReadOnlySpan<byte> name, in JsonElement value, ref MetricSummary state)
    {
        if (name.SequenceEqual("name"u8))
        {
            state.Name = value.GetString();
        }

        return true;
    }
}

struct MetricSummary
{
    public string? Name;

    public int StringMetricCount;

    public int IntegerMetricCount;

    public int Cpu;

    public int Memory;
}
