// <copyright file="YamlToJsonBenchmark.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using Corvus.Text.Json.Yaml;
using StjYamlDocument = Corvus.Yaml.YamlDocument;
using StjYamlDocumentMode = Corvus.Yaml.YamlDocumentMode;
using StjYamlReaderOptions = Corvus.Yaml.YamlReaderOptions;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Corvus.Text.Json.Yaml.Benchmarks;

/// <summary>
/// Benchmarks comparing Corvus.Text.Json.Yaml against YamlDotNet for YAML→JSON conversion.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class YamlToJsonBenchmark
{
    private static readonly ISerializer YamlDotNetJsonSerializer = new SerializerBuilder()
        .JsonCompatible()
        .Build();

    private static readonly IDeserializer YamlDotNetDeserializer = new DeserializerBuilder()
        .Build();

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

    private static readonly YamlReaderOptions MultiDocOptions = new()
    {
        DocumentMode = YamlDocumentMode.MultiAsArray,
    };

    private static readonly StjYamlReaderOptions StjMultiDocOptions = new()
    {
        DocumentMode = StjYamlDocumentMode.MultiAsArray,
    };

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
    public string YamlDotNet_SimpleScalar()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.simpleScalarString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public JsonValueKind Corvus_SimpleScalar()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.simpleScalarBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_SimpleScalar()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.simpleScalarBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- NestedMapping (540 bytes) ----

    [BenchmarkCategory("NestedMapping")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_NestedMapping()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.nestedMappingString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public JsonValueKind Corvus_NestedMapping()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.nestedMappingBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_NestedMapping()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.nestedMappingBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- BlockScalar (642 bytes) ----

    [BenchmarkCategory("BlockScalar")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_BlockScalar()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.blockScalarString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public JsonValueKind Corvus_BlockScalar()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.blockScalarBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_BlockScalar()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.blockScalarBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- FlowStyle (374 bytes) ----

    [BenchmarkCategory("FlowStyle")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_FlowStyle()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.flowStyleString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public JsonValueKind Corvus_FlowStyle()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.flowStyleBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_FlowStyle()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.flowStyleBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- AnchorAlias (604 bytes) ----

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_AnchorAlias()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.anchorAliasString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public JsonValueKind Corvus_AnchorAlias()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.anchorAliasBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_AnchorAlias()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.anchorAliasBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- SmallConfig (339 bytes) ----

    [BenchmarkCategory("SmallConfig")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_SmallConfig()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.smallConfigString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public JsonValueKind Corvus_SmallConfig()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.smallConfigBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_SmallConfig()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.smallConfigBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- LargeConfig (~6KB) ----

    [BenchmarkCategory("LargeConfig")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_LargeConfig()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.largeConfigString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public JsonValueKind Corvus_LargeConfig()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.largeConfigBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_LargeConfig()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.largeConfigBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- ComplexMixed (~2.4KB) ----

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_ComplexMixed()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.complexMixedString);
        return YamlDotNetJsonSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public JsonValueKind Corvus_ComplexMixed()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.complexMixedBytes);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_ComplexMixed()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.complexMixedBytes);
        return doc.RootElement.ValueKind;
    }

    // ---- MultiDocument (235 bytes, 3 documents) ----

    [BenchmarkCategory("MultiDocument")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_MultiDocument()
    {
        // YamlDotNet requires manual stream iteration for multi-doc.
        StringBuilder sb = new("[");
        bool first = true;
        using StringReader reader = new(this.multiDocumentString);
        YamlDotNet.Core.Parser parser = new(reader);
        parser.Consume<YamlDotNet.Core.Events.StreamStart>();
        while (parser.Accept<YamlDotNet.Core.Events.DocumentStart>(out _))
        {
            object? doc = YamlDotNetDeserializer.Deserialize(parser);
            if (doc is not null)
            {
                if (!first)
                {
                    sb.Append(',');
                }

                sb.Append(YamlDotNetJsonSerializer.Serialize(doc).TrimEnd('\r', '\n'));
                first = false;
            }
        }

        sb.Append(']');
        return sb.ToString();
    }

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public JsonValueKind Corvus_MultiDocument()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(this.multiDocumentBytes, MultiDocOptions);
        return doc.RootElement.ValueKind;
    }

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public System.Text.Json.JsonValueKind CorvusStj_MultiDocument()
    {
        using JsonDocument doc = StjYamlDocument.Parse(this.multiDocumentBytes, StjMultiDocOptions);
        return doc.RootElement.ValueKind;
    }

    private static byte[] LoadScenarioBytes(string filename)
    {
        string path = Path.Combine("Scenarios", filename);
        return File.ReadAllBytes(path);
    }
}
