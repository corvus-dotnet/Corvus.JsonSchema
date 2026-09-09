using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.RuntimeEvaluator;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>
/// Measures schema compilation (cold start) for representative Sourcemeta schemas.
/// </summary>
[MemoryDiagnoser]
public class ColdStartBenchmarks
{
    private byte[] schema = [];

    [Params("cql2", "geojson", "openapi", "cmake-presets", "ansible-meta", "aws-cdk")]
    public string Schema { get; set; } = "cql2";

    [GlobalSetup]
    public void Setup()
    {
        this.schema = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "sourcemeta", this.Schema + "-schema.json"));
    }

    [Benchmark]
    public int Compile()
    {
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile(this.schema, new JsonSchemaEvaluatorOptions { DefaultDialect = JsonSchemaDialect.Draft7 });
        return evaluator.NodeCount;
    }
}
