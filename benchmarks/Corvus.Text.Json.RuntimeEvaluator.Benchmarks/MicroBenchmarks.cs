using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using Corvus.MicroModels;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

/// <summary>
/// Keyword-group micro benchmarks: generated typed model vs generated standalone evaluator vs runtime evaluator.
/// Each measures one flag-mode evaluation of a small instance (the schemas live in the MicroModels project).
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class MicroBenchmarks
{
    private const string ObjectJson = """{"id": 7, "name": "Ada", "email": "ada@example.com", "tags": ["a", "b", "c"], "active": true, "score": 42.5, "address": {"city": "London", "zip": "N1"}}""";
    private const string ArrayJson = "[1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16]";
    private const string StringJson = "\"abc-1234\"";
    private const string TreeJson = """{"data": 1, "children": [{"data": 2, "children": [{"data": 3}, {"data": 4}]}, {"data": 5, "children": [{"data": 6}]}]}""";

    private static readonly string SchemaDir = Path.Combine(AppContext.BaseDirectory, "micro-schemas");

    private JsonSchemaEvaluator objectEvaluator = null!;
    private JsonSchemaEvaluator arrayEvaluator = null!;
    private JsonSchemaEvaluator stringEvaluator = null!;
    private JsonSchemaEvaluator unevaluatedEvaluator = null!;
    private JsonSchemaEvaluator dynamicEvaluator = null!;
    private JsonSchemaEvaluator oneOfEvaluator = null!;

    private ParsedJsonDocument<JsonElement> objectDoc = null!;
    private ParsedJsonDocument<JsonElement> arrayDoc = null!;
    private ParsedJsonDocument<JsonElement> stringDoc = null!;
    private ParsedJsonDocument<JsonElement> treeDoc = null!;

    private ParsedJsonDocument<MicroObject> typedObjectDoc = null!;
    private ParsedJsonDocument<MicroArray> typedArrayDoc = null!;
    private ParsedJsonDocument<MicroString> typedStringDoc = null!;
    private ParsedJsonDocument<MicroUnevaluated> typedUnevaluatedDoc = null!;
    private ParsedJsonDocument<MicroDynamic> typedTreeDoc = null!;
    private ParsedJsonDocument<MicroOneOf> typedOneOfDoc = null!;

    [GlobalSetup]
    public void Setup()
    {
        JsonSchemaEvaluator Compile(string name, bool assertFormat = false)
        {
            return JsonSchemaEvaluator.Compile(File.ReadAllBytes(Path.Combine(SchemaDir, name)), new JsonSchemaEvaluatorOptions { AssertFormat = assertFormat ? true : null });
        }

        this.objectEvaluator = Compile("object.json");
        this.arrayEvaluator = Compile("array.json");
        this.stringEvaluator = Compile("string.json", assertFormat: true);
        this.unevaluatedEvaluator = Compile("unevaluated.json");
        this.dynamicEvaluator = Compile("dynamic.json");
        this.oneOfEvaluator = Compile("oneof.json");

        this.objectDoc = ParsedJsonDocument<JsonElement>.Parse(ObjectJson);
        this.arrayDoc = ParsedJsonDocument<JsonElement>.Parse(ArrayJson);
        this.stringDoc = ParsedJsonDocument<JsonElement>.Parse(StringJson);
        this.treeDoc = ParsedJsonDocument<JsonElement>.Parse(TreeJson);

        this.typedObjectDoc = ParsedJsonDocument<MicroObject>.Parse(ObjectJson);
        this.typedArrayDoc = ParsedJsonDocument<MicroArray>.Parse(ArrayJson);
        this.typedStringDoc = ParsedJsonDocument<MicroString>.Parse(StringJson);
        this.typedUnevaluatedDoc = ParsedJsonDocument<MicroUnevaluated>.Parse(ObjectJson);
        this.typedTreeDoc = ParsedJsonDocument<MicroDynamic>.Parse(TreeJson);
        this.typedOneOfDoc = ParsedJsonDocument<MicroOneOf>.Parse(ObjectJson);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.objectDoc.Dispose();
        this.arrayDoc.Dispose();
        this.stringDoc.Dispose();
        this.treeDoc.Dispose();
        this.typedObjectDoc.Dispose();
        this.typedArrayDoc.Dispose();
        this.typedStringDoc.Dispose();
        this.typedUnevaluatedDoc.Dispose();
        this.typedTreeDoc.Dispose();
        this.typedOneOfDoc.Dispose();
        this.objectEvaluator.Dispose();
        this.arrayEvaluator.Dispose();
        this.stringEvaluator.Dispose();
        this.unevaluatedEvaluator.Dispose();
        this.dynamicEvaluator.Dispose();
        this.oneOfEvaluator.Dispose();
    }

    /// <summary>Interleaved min-of-N three-way comparison for every category.</summary>
    public static void RunQuick(int rounds)
    {
        var b = new MicroBenchmarks();
        b.Setup();
        Console.WriteLine($"{"category",-14} {"typed",12} {"standalone",12} {"runtime",12} {"rt/typed",9} {"rt/standalone",14}");
        foreach ((string name, Func<bool> typed, Func<bool> standalone, Func<bool> runtime) in new (string, Func<bool>, Func<bool>, Func<bool>)[]
        {
            ("Object", b.Object_Typed, b.Object_Standalone, b.Object_Runtime),
            ("Array", b.Array_Typed, b.Array_Standalone, b.Array_Runtime),
            ("String", b.String_Typed, b.String_Standalone, b.String_Runtime),
            ("Unevaluated", b.Unevaluated_Typed, b.Unevaluated_Standalone, b.Unevaluated_Runtime),
            ("DynamicRef", b.DynamicRef_Typed, b.DynamicRef_Standalone, b.DynamicRef_Runtime),
            ("OneOf", b.OneOf_Typed, b.OneOf_Standalone, b.OneOf_Runtime),
            ("Verbose", b.Verbose_Standalone, b.Verbose_Standalone, b.Verbose_Runtime),
        })
        {
            (double t, double s, double r) = QuickTimer.Run3(rounds, 2000, typed, standalone, runtime);
            Console.WriteLine($"{name,-14} {QuickTimer.Format(t),12} {QuickTimer.Format(s),12} {QuickTimer.Format(r),12} {r / t,9:F2} {r / s,14:F2}");
        }

        b.Cleanup();
    }

    // ---- object properties ----
    [BenchmarkCategory("Object"), Benchmark(Baseline = true)]
    public bool Object_Typed() => this.typedObjectDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("Object"), Benchmark]
    public bool Object_Standalone() => MicroObjectEvaluator.Evaluate(this.objectDoc.RootElement);

    [BenchmarkCategory("Object"), Benchmark]
    public bool Object_Runtime() => this.objectEvaluator.Evaluate(this.objectDoc.RootElement);

    // ---- array items + uniqueItems ----
    [BenchmarkCategory("Array"), Benchmark(Baseline = true)]
    public bool Array_Typed() => this.typedArrayDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("Array"), Benchmark]
    public bool Array_Standalone() => MicroArrayEvaluator.Evaluate(this.arrayDoc.RootElement);

    [BenchmarkCategory("Array"), Benchmark]
    public bool Array_Runtime() => this.arrayEvaluator.Evaluate(this.arrayDoc.RootElement);

    // ---- string pattern/length/format ----
    [BenchmarkCategory("String"), Benchmark(Baseline = true)]
    public bool String_Typed() => this.typedStringDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("String"), Benchmark]
    public bool String_Standalone() => MicroStringEvaluator.Evaluate(this.stringDoc.RootElement);

    [BenchmarkCategory("String"), Benchmark]
    public bool String_Runtime() => this.stringEvaluator.Evaluate(this.stringDoc.RootElement);

    // ---- unevaluatedProperties across allOf/if/then ----
    [BenchmarkCategory("Unevaluated"), Benchmark(Baseline = true)]
    public bool Unevaluated_Typed() => this.typedUnevaluatedDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("Unevaluated"), Benchmark]
    public bool Unevaluated_Standalone() => MicroUnevaluatedEvaluator.Evaluate(this.objectDoc.RootElement);

    [BenchmarkCategory("Unevaluated"), Benchmark]
    public bool Unevaluated_Runtime() => this.unevaluatedEvaluator.Evaluate(this.objectDoc.RootElement);

    // ---- $dynamicRef strict-tree ----
    [BenchmarkCategory("DynamicRef"), Benchmark(Baseline = true)]
    public bool DynamicRef_Typed() => this.typedTreeDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("DynamicRef"), Benchmark]
    public bool DynamicRef_Standalone() => MicroDynamicEvaluator.Evaluate(this.treeDoc.RootElement);

    [BenchmarkCategory("DynamicRef"), Benchmark]
    public bool DynamicRef_Runtime() => this.dynamicEvaluator.Evaluate(this.treeDoc.RootElement);

    // ---- oneOf fan-out (all branches fail on required) ----
    [BenchmarkCategory("OneOf"), Benchmark(Baseline = true)]
    public bool OneOf_Typed() => this.typedOneOfDoc.RootElement.EvaluateSchema();

    [BenchmarkCategory("OneOf"), Benchmark]
    public bool OneOf_Standalone() => MicroOneOfEvaluator.Evaluate(this.objectDoc.RootElement);

    [BenchmarkCategory("OneOf"), Benchmark]
    public bool OneOf_Runtime() => this.oneOfEvaluator.Evaluate(this.objectDoc.RootElement);

    // ---- verbose results ----
    [BenchmarkCategory("Verbose"), Benchmark(Baseline = true)]
    public bool Verbose_Standalone()
    {
        using JsonSchemaResultsCollector c = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        return MicroObjectEvaluator.Evaluate(this.objectDoc.RootElement, c);
    }

    [BenchmarkCategory("Verbose"), Benchmark]
    public bool Verbose_Runtime()
    {
        using JsonSchemaResultsCollector c = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        return this.objectEvaluator.Evaluate(this.objectDoc.RootElement, c);
    }
}
