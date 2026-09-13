using System.Diagnostics.Tracing;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Attributes steady-state allocations to types using the runtime's GCAllocationTick events
/// (one event per ~100 KB allocated, naming the type that crossed the threshold).
/// </summary>
public static class AllocationTypes
{
    public static int Run(string[] args)
    {
        (string schema, string instance, bool assertFormat) = args.Length > 0 && args[0] == "idn"
            ? ("""{"format": "idn-email"}""", "\"실례@실례.테스트\"", true)
            : args.Length > 0 && args[0] == "unique300"
            ? ("""{"uniqueItems": true}""", "[" + string.Join(",", Enumerable.Range(0, 300)) + "]", false)
            : ("""{"uniqueItems": true}""", "[1, 2, 3]", false);

        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema, new JsonSchemaEvaluatorOptions { AssertFormat = assertFormat ? true : null });
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
        for (int i = 0; i < 1000; i++)
        {
            evaluator.Evaluate(doc.RootElement);
        }

        // Let tiered compilation settle so the measurement reflects steady-state code.
        Thread.Sleep(1000);
        for (int i = 0; i < 1000; i++)
        {
            evaluator.Evaluate(doc.RootElement);
        }

        var counts = new Dictionary<string, int>();
        using (var listener = new TickListener(counts))
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 200_000; i++)
            {
                evaluator.Evaluate(doc.RootElement);
            }

            Console.WriteLine($"allocated {GC.GetAllocatedBytesForCurrentThread() - before} bytes over 200000 evaluations");
        }

        foreach ((string type, int count) in counts.OrderByDescending(kv => kv.Value))
        {
            Console.WriteLine($"  {count,6} ticks  {type}");
        }

        return 0;
    }

    private sealed class TickListener : EventListener
    {
        private readonly Dictionary<string, int> counts;

        public TickListener(Dictionary<string, int> counts)
        {
            this.counts = counts;
        }

        protected override void OnEventSourceCreated(EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
            {
                this.EnableEvents(source, EventLevel.Verbose, (EventKeywords)0x1); // GC keyword
            }
        }

        protected override void OnEventWritten(EventWrittenEventArgs e)
        {
            if (e.EventName is not null && e.EventName.StartsWith("GCAllocationTick", StringComparison.Ordinal) && e.Payload is not null && e.PayloadNames is not null)
            {
                int i = e.PayloadNames.IndexOf("TypeName");
                string type = i >= 0 ? e.Payload[i]?.ToString() ?? "?" : "?";
                lock (this.counts)
                {
                    this.counts[type] = this.counts.GetValueOrDefault(type) + 1;
                }
            }
        }
    }
}
