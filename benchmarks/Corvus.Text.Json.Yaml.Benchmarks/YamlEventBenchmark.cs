// <copyright file="YamlEventBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Corvus.Text.Json.Yaml;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace Corvus.Text.Json.Yaml.Benchmarks;

/// <summary>
/// Benchmarks comparing Corvus.Text.Json.Yaml event parsing against YamlDotNet
/// low-level event parsing. Both parsers iterate through all YAML events without
/// constructing a document — this measures pure parsing throughput.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class YamlEventBenchmark
{
    private byte[] simpleScalarBytes = null!;
    private byte[] nestedMappingBytes = null!;
    private byte[] blockScalarBytes = null!;
    private byte[] flowStyleBytes = null!;
    private byte[] anchorAliasBytes = null!;
    private byte[] smallConfigBytes = null!;
    private byte[] largeConfigBytes = null!;
    private byte[] complexMixedBytes = null!;
    private byte[] multiDocumentBytes = null!;

    private string simpleScalarString = null!;
    private string nestedMappingString = null!;
    private string blockScalarString = null!;
    private string flowStyleString = null!;
    private string anchorAliasString = null!;
    private string smallConfigString = null!;
    private string largeConfigString = null!;
    private string complexMixedString = null!;
    private string multiDocumentString = null!;

    [GlobalSetup]
    public void GlobalSetup()
    {
        this.simpleScalarBytes = LoadScenarioBytes("SimpleScalar.yaml");
        this.nestedMappingBytes = LoadScenarioBytes("NestedMapping.yaml");
        this.blockScalarBytes = LoadScenarioBytes("BlockScalar.yaml");
        this.flowStyleBytes = LoadScenarioBytes("FlowStyle.yaml");
        this.anchorAliasBytes = LoadScenarioBytes("AnchorAlias.yaml");
        this.smallConfigBytes = LoadScenarioBytes("SmallConfig.yaml");
        this.largeConfigBytes = LoadScenarioBytes("LargeConfig.yaml");
        this.complexMixedBytes = LoadScenarioBytes("ComplexMixed.yaml");
        this.multiDocumentBytes = LoadScenarioBytes("MultiDocument.yaml");

        this.simpleScalarString = Encoding.UTF8.GetString(this.simpleScalarBytes);
        this.nestedMappingString = Encoding.UTF8.GetString(this.nestedMappingBytes);
        this.blockScalarString = Encoding.UTF8.GetString(this.blockScalarBytes);
        this.flowStyleString = Encoding.UTF8.GetString(this.flowStyleBytes);
        this.anchorAliasString = Encoding.UTF8.GetString(this.anchorAliasBytes);
        this.smallConfigString = Encoding.UTF8.GetString(this.smallConfigBytes);
        this.largeConfigString = Encoding.UTF8.GetString(this.largeConfigBytes);
        this.complexMixedString = Encoding.UTF8.GetString(this.complexMixedBytes);
        this.multiDocumentString = Encoding.UTF8.GetString(this.multiDocumentBytes);
    }

    // ---- SimpleScalar (24 bytes) ----

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_SimpleScalar() => CountYamlDotNetEvents(this.simpleScalarString);

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public int Corvus_SimpleScalar() => CountCorvusEvents(this.simpleScalarBytes);

    // ---- NestedMapping (540 bytes) ----

    [BenchmarkCategory("NestedMapping")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_NestedMapping() => CountYamlDotNetEvents(this.nestedMappingString);

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public int Corvus_NestedMapping() => CountCorvusEvents(this.nestedMappingBytes);

    // ---- BlockScalar (642 bytes) ----

    [BenchmarkCategory("BlockScalar")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_BlockScalar() => CountYamlDotNetEvents(this.blockScalarString);

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public int Corvus_BlockScalar() => CountCorvusEvents(this.blockScalarBytes);

    // ---- FlowStyle (374 bytes) ----

    [BenchmarkCategory("FlowStyle")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_FlowStyle() => CountYamlDotNetEvents(this.flowStyleString);

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public int Corvus_FlowStyle() => CountCorvusEvents(this.flowStyleBytes);

    // ---- AnchorAlias (604 bytes) ----

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_AnchorAlias() => CountYamlDotNetEvents(this.anchorAliasString);

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public int Corvus_AnchorAlias() => CountCorvusEvents(this.anchorAliasBytes);

    // ---- SmallConfig (339 bytes) ----

    [BenchmarkCategory("SmallConfig")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_SmallConfig() => CountYamlDotNetEvents(this.smallConfigString);

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public int Corvus_SmallConfig() => CountCorvusEvents(this.smallConfigBytes);

    // ---- LargeConfig (~6KB) ----

    [BenchmarkCategory("LargeConfig")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_LargeConfig() => CountYamlDotNetEvents(this.largeConfigString);

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public int Corvus_LargeConfig() => CountCorvusEvents(this.largeConfigBytes);

    // ---- ComplexMixed (~2.4KB) ----

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_ComplexMixed() => CountYamlDotNetEvents(this.complexMixedString);

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public int Corvus_ComplexMixed() => CountCorvusEvents(this.complexMixedBytes);

    // ---- MultiDocument (235 bytes, 3 documents) ----

    [BenchmarkCategory("MultiDocument")]
    [Benchmark(Baseline = true)]
    public int YamlDotNet_MultiDocument() => CountYamlDotNetEvents(this.multiDocumentString);

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public int Corvus_MultiDocument() => CountCorvusEvents(this.multiDocumentBytes);

    // ---- Helpers ----

    private static int CountYamlDotNetEvents(string yaml)
    {
        int count = 0;
        using StringReader reader = new(yaml);
        Parser parser = new(reader);

        while (parser.MoveNext())
        {
            count++;
        }

        return count;
    }

    private static int CountCorvusEvents(byte[] yaml)
    {
        int count = 0;

        YamlDocument.EnumerateEvents(
            yaml,
            (in YamlEvent _) =>
            {
                count++;
                return true;
            });

        return count;
    }

    private static byte[] LoadScenarioBytes(string filename)
    {
        string path = Path.Combine("Scenarios", filename);
        return File.ReadAllBytes(path);
    }
}
