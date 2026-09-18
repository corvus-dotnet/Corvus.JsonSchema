using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Exact allocation accounting for warm evaluation: bytes allocated on the current thread over N passes.
/// </summary>
public static class AllocationCheck
{
    public static int Run(string[] filters)
    {
        using var listener = new TypeListener();
        int nonZero = RunCore(filters);
        listener.Report();
        return nonZero;
    }

    private sealed class TypeListener : System.Diagnostics.Tracing.EventListener
    {
        private readonly Dictionary<string, int> counts = [];

        protected override void OnEventSourceCreated(System.Diagnostics.Tracing.EventSource source)
        {
            if (source.Name == "Microsoft-Windows-DotNETRuntime")
            {
                this.EnableEvents(source, System.Diagnostics.Tracing.EventLevel.Verbose, (System.Diagnostics.Tracing.EventKeywords)0x1);
            }
        }

        protected override void OnEventWritten(System.Diagnostics.Tracing.EventWrittenEventArgs e)
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

        public void Report()
        {
            Console.WriteLine("Allocation ticks by type (whole run, ~100 KB per tick):");
            foreach ((string type, int count) in this.counts.OrderByDescending(kv => kv.Value).Take(12))
            {
                Console.WriteLine($"  {count,6}  {type}");
            }
        }
    }

    private static int RunCore(string[] filters)
    {
        int nonZero = 0;
        Console.WriteLine($"{"case",-34} {"passes",7} {"bytes",10} {"per eval",10}");

        // Every format on a valid value, asserting.
        foreach ((string format, string value) in new[]
        {
            ("date", "\"2024-02-29\""), ("date-time", "\"2024-02-29T12:34:56.789+01:00\""), ("time", "\"12:34:56Z\""),
            ("duration", "\"P1Y2M3DT4H5M6S\""), ("email", "\"joe.bloggs@example.com\""), ("idn-email", "\"실례@실례.테스트\""),
            ("hostname", "\"www.example.com\""), ("idn-hostname", "\"실례.테스트\""), ("ipv4", "\"192.168.0.1\""),
            ("ipv6", "\"::ffff:192.168.0.1\""), ("uri", "\"https://example.com/a?b=c#d\""), ("uri-reference", "\"/a/b?c\""),
            ("iri", "\"https://ƒøø.ßår/?∂éœ=πîx#πîüx\""), ("iri-reference", "\"/ƒøø\""), ("uuid", "\"2eb8aa08-aa98-11ea-b4aa-73b441d16380\""),
            ("uri-template", "\"http://example.com/dictionary/{term:1}/{term}\""), ("json-pointer", "\"/foo/bar~0/baz~1/%a\""),
            ("relative-json-pointer", "\"1/0\""), ("regex", "\"([abc])+\\\\s+$\""),
        })
        {
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile($$"""{"format": "{{format}}"}""", new JsonSchemaEvaluatorOptions { AssertFormat = true });
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(value);
            if (!evaluator.Evaluate(doc.RootElement))
            {
                Console.WriteLine($"format {format}: value unexpectedly invalid");
            }

            nonZero += Report("format:" + format, 1000, () => evaluator.Evaluate(doc.RootElement));
        }

        // Per-keyword probes.
        foreach ((string name, string schema, string instance) in new[]
        {
            ("uniqueItems:empty", """{"uniqueItems": true}""", "[]"),
            ("uniqueItems:one", """{"uniqueItems": true}""", "[1]"),
            ("uniqueItems:notarray", """{"uniqueItems": true}""", "1"),
            ("uniqueItems:withType", """{"type": "array", "uniqueItems": true}""", "[1, 2]"),
            ("uniqueItems:numbers", """{"uniqueItems": true}""", "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]"),
            ("uniqueItems:strings", """{"uniqueItems": true}""", """["a", "b", "c", "d", "e", "f", "g", "h"]"""),
            ("uniqueItems:objects", """{"uniqueItems": true}""", """[{"a": 1}, {"a": 2}, {"b": [1, 2]}, {"b": [2, 1]}]"""),
            ("uniqueItems:300", """{"uniqueItems": true}""", "[" + string.Join(",", Enumerable.Range(0, 300)) + "]"),
            ("enum:objects", """{"enum": [{"a": 1}, {"b": 2}, [1, 2]]}""", """{"b": 2}"""),
            ("const:object", """{"const": {"a": [1, {"b": "c"}]}}""", """{"a": [1, {"b": "c"}]}"""),
            ("const:number", """{"const": 1.50}""", "1.5"),
            ("patternProperties", """{"patternProperties": {"^x-": {"type": "string"}, "[0-9]+$": true}}""", """{"x-a": "1", "b12": 2, "c": 3, "x-d": "4"}"""),
            ("propertyNames", """{"propertyNames": {"pattern": "^[a-z]+$", "maxLength": 5}}""", """{"abc": 1, "de": 2, "fgh": 3}"""),
            ("propertyNames:nested", """{"propertyNames": {"pattern": "^[a-z]+$"}, "additionalProperties": {"propertyNames": {"maxLength": 3}}}""", """{"abc": {"de": 1}, "fgh": {"ij": 2}}"""),
            ("contains", """{"contains": {"type": "string"}, "minContains": 2}""", """[1, "a", 2, "b"]"""),
            ("escaped:names", """{"properties": {"a\"b": {"type": "integer"}}, "required": ["a\"b"]}""", """{"a\"b": 1, "c\nd": 2}"""),
            ("escaped:string", """{"minLength": 2, "pattern": "^a"}""", "\"a\\nb\\u0041\""),
            ("longstring", """{"pattern": "^a.*z$"}""", "\"" + new string('a', 300) + "z\""),
            ("dependentSchemas", """{"dependentSchemas": {"a": {"required": ["b"]}}}""", """{"a": 1, "b": 2}"""),
            ("ifthen", """{"if": {"properties": {"a": {"const": 1}}}, "then": {"required": ["b"]}, "else": {"required": ["c"]}}""", """{"a": 1, "b": 2}"""),
            ("bigobject:required", """{"required": ["p0", "p150", "p299"]}""", "{" + string.Join(",", Enumerable.Range(0, 300).Select(i => $"\"p{i}\": {i}")) + "}"),
            ("bigobject:unevaluated", """{"properties": {"p0": true}, "unevaluatedProperties": {"type": "integer"}}""", "{" + string.Join(",", Enumerable.Range(0, 300).Select(i => $"\"p{i}\": {i}")) + "}"),
            ("multipleOf", """{"multipleOf": 0.01}""", "12345.67"),
            ("bignum", """{"maximum": 1e400, "minimum": -1e400, "multipleOf": 1e-8}""", "12391239123.00000001"),
        })
        {
            using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(schema);
            using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(instance);
            nonZero += Report("probe:" + name, 20000, () => evaluator.Evaluate(doc.RootElement));
        }

        // Keyword groups in flag and verbose mode.
        var micro = new MicroBenchmarks();
        micro.Setup();
        nonZero += Report("micro:object", 1000, () => micro.Object_Runtime());
        nonZero += Report("micro:array", 1000, () => micro.Array_Runtime());
        nonZero += Report("micro:string(regex)", 1000, () => micro.String_Runtime());
        nonZero += Report("micro:unevaluated", 1000, () => micro.Unevaluated_Runtime());
        nonZero += Report("micro:dynamicRef", 1000, () => micro.DynamicRef_Runtime());
        nonZero += Report("micro:oneOf", 1000, () => micro.OneOf_Runtime());
        nonZero += Report("micro:verbose", 1000, () => micro.Verbose_Runtime());
        micro.Cleanup();

        // Whole Sourcemeta corpora.
        foreach ((string file, _) in SourceMetaCases.All)
        {
            if (filters.Length > 0 && !Array.Exists(filters, f => file.Contains(f, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            using var c = new SourceMetaCase(file);
            int count = c.Documents.Length;
            nonZero += Report("sourcemeta:" + file, 5, () => c.EvaluateAll(), count);
        }

        Console.WriteLine(nonZero == 0 ? "All warm evaluations allocated zero bytes." : $"{nonZero} case(s) allocated.");
        return nonZero;
    }

    private static int Report(string name, int passes, Func<bool> action, int evaluationsPerPass = 1)
    {
        Func<int> wrapped = () => action() ? 1 : 0;
        return Report(name, passes, wrapped, evaluationsPerPass);
    }

    private static int Report(string name, int passes, Func<int> action, int evaluationsPerPass = 1)
    {
        int sink = 0;
        for (int i = 0; i < 50; i++)
        {
            sink += action();
        }

        // Warm-up may allocate lazily-created runners (regex, pooled documents); measure steady state only.
        if (Environment.GetEnvironmentVariable("CORVUS_RT_ALLOC_GC") == "1")
        {
            GC.Collect();
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < passes; i++)
        {
            sink += action();
        }


        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Console.WriteLine($"{name,-34} {passes,7} {bytes,10} {(double)bytes / (passes * evaluationsPerPass),10:F2}{(sink == int.MinValue ? "!" : string.Empty)}");
        return bytes == 0 ? 0 : 1;
    }
}
