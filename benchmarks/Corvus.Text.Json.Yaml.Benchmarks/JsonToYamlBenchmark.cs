// <copyright file="JsonToYamlBenchmark.cs" company="Endjin Limited">
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
using StjYamlWriterOptions = Corvus.Yaml.YamlWriterOptions;
using CtjYamlWriterOptions = Corvus.Text.Json.Yaml.YamlWriterOptions;
using YamlDotNet.Serialization;

namespace Corvus.Text.Json.Yaml.Benchmarks;

/// <summary>
/// Benchmarks comparing Corvus.Text.Json.Yaml against YamlDotNet for JSON→YAML conversion.
/// </summary>
/// <remarks>
/// <para>
/// Tests four conversion paths per scenario:
/// </para>
/// <list type="bullet">
/// <item><b>YamlDotNet</b> — parse JSON string via YAML deserializer (JSON ⊂ YAML), serialize to YAML.</item>
/// <item><b>Corvus_Utf8</b> — raw UTF-8 bytes through <c>Utf8JsonReader</c> (converter Path 1).</item>
/// <item><b>Corvus_CtjElement</b> — pre-parsed CTJ <see cref="JsonElement"/> tree walk (converter Path 3).</item>
/// <item><b>CorvusStj_Element</b> — pre-parsed STJ <see cref="System.Text.Json.JsonElement"/> tree walk (converter Path 2).</item>
/// </list>
/// <para>
/// JSON inputs are generated from the existing YAML scenario files via YAML→JSON conversion
/// in <see cref="GlobalSetup"/>, guaranteeing content parity with the <see cref="YamlToJsonBenchmark"/>.
/// </para>
/// </remarks>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class JsonToYamlBenchmark
{
    private static readonly IDeserializer YamlDotNetDeserializer = new DeserializerBuilder()
        .Build();

    private static readonly ISerializer YamlDotNetSerializer = new SerializerBuilder()
        .Build();

    private static readonly StjYamlReaderOptions StjMultiDocOptions = new()
    {
        DocumentMode = StjYamlDocumentMode.MultiAsArray,
    };

    private static readonly YamlReaderOptions MultiDocOptions = new()
    {
        DocumentMode = YamlDocumentMode.MultiAsArray,
    };

    // Per-scenario: raw JSON bytes, JSON string, pre-parsed STJ/CTJ documents.
    private byte[] simpleScalarJsonBytes = null!;
    private byte[] nestedMappingJsonBytes = null!;
    private byte[] blockScalarJsonBytes = null!;
    private byte[] flowStyleJsonBytes = null!;
    private byte[] anchorAliasJsonBytes = null!;
    private byte[] smallConfigJsonBytes = null!;
    private byte[] largeConfigJsonBytes = null!;
    private byte[] complexMixedJsonBytes = null!;
    private byte[] multiDocumentJsonBytes = null!;

    private string simpleScalarJsonString = null!;
    private string nestedMappingJsonString = null!;
    private string blockScalarJsonString = null!;
    private string flowStyleJsonString = null!;
    private string anchorAliasJsonString = null!;
    private string smallConfigJsonString = null!;
    private string largeConfigJsonString = null!;
    private string complexMixedJsonString = null!;
    private string multiDocumentJsonString = null!;

    private JsonDocument simpleScalarStjDoc = null!;
    private JsonDocument nestedMappingStjDoc = null!;
    private JsonDocument blockScalarStjDoc = null!;
    private JsonDocument flowStyleStjDoc = null!;
    private JsonDocument anchorAliasStjDoc = null!;
    private JsonDocument smallConfigStjDoc = null!;
    private JsonDocument largeConfigStjDoc = null!;
    private JsonDocument complexMixedStjDoc = null!;
    private JsonDocument multiDocumentStjDoc = null!;

    private ParsedJsonDocument<JsonElement> simpleScalarCtjDoc;
    private ParsedJsonDocument<JsonElement> nestedMappingCtjDoc;
    private ParsedJsonDocument<JsonElement> blockScalarCtjDoc;
    private ParsedJsonDocument<JsonElement> flowStyleCtjDoc;
    private ParsedJsonDocument<JsonElement> anchorAliasCtjDoc;
    private ParsedJsonDocument<JsonElement> smallConfigCtjDoc;
    private ParsedJsonDocument<JsonElement> largeConfigCtjDoc;
    private ParsedJsonDocument<JsonElement> complexMixedCtjDoc;
    private ParsedJsonDocument<JsonElement> multiDocumentCtjDoc;

    [GlobalSetup]
    public void GlobalSetup()
    {
        // Convert each YAML scenario to JSON to produce our benchmark inputs.
        (this.simpleScalarJsonBytes, this.simpleScalarJsonString) = YamlToJson("SimpleScalar.yaml");
        (this.nestedMappingJsonBytes, this.nestedMappingJsonString) = YamlToJson("NestedMapping.yaml");
        (this.blockScalarJsonBytes, this.blockScalarJsonString) = YamlToJson("BlockScalar.yaml");
        (this.flowStyleJsonBytes, this.flowStyleJsonString) = YamlToJson("FlowStyle.yaml");
        (this.anchorAliasJsonBytes, this.anchorAliasJsonString) = YamlToJson("AnchorAlias.yaml");
        (this.smallConfigJsonBytes, this.smallConfigJsonString) = YamlToJson("SmallConfig.yaml");
        (this.largeConfigJsonBytes, this.largeConfigJsonString) = YamlToJson("LargeConfig.yaml");
        (this.complexMixedJsonBytes, this.complexMixedJsonString) = YamlToJson("ComplexMixed.yaml");
        (this.multiDocumentJsonBytes, this.multiDocumentJsonString) = YamlToJson("MultiDocument.yaml", multi: true);

        // Pre-parse STJ documents (kept alive for element-walk benchmarks).
        this.simpleScalarStjDoc = JsonDocument.Parse(this.simpleScalarJsonBytes);
        this.nestedMappingStjDoc = JsonDocument.Parse(this.nestedMappingJsonBytes);
        this.blockScalarStjDoc = JsonDocument.Parse(this.blockScalarJsonBytes);
        this.flowStyleStjDoc = JsonDocument.Parse(this.flowStyleJsonBytes);
        this.anchorAliasStjDoc = JsonDocument.Parse(this.anchorAliasJsonBytes);
        this.smallConfigStjDoc = JsonDocument.Parse(this.smallConfigJsonBytes);
        this.largeConfigStjDoc = JsonDocument.Parse(this.largeConfigJsonBytes);
        this.complexMixedStjDoc = JsonDocument.Parse(this.complexMixedJsonBytes);
        this.multiDocumentStjDoc = JsonDocument.Parse(this.multiDocumentJsonBytes);

        // Pre-parse CTJ documents (kept alive for element-walk benchmarks).
        this.simpleScalarCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.simpleScalarJsonBytes);
        this.nestedMappingCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.nestedMappingJsonBytes);
        this.blockScalarCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.blockScalarJsonBytes);
        this.flowStyleCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.flowStyleJsonBytes);
        this.anchorAliasCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.anchorAliasJsonBytes);
        this.smallConfigCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.smallConfigJsonBytes);
        this.largeConfigCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.largeConfigJsonBytes);
        this.complexMixedCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.complexMixedJsonBytes);
        this.multiDocumentCtjDoc = ParsedJsonDocument<JsonElement>.Parse(this.multiDocumentJsonBytes);
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.simpleScalarStjDoc?.Dispose();
        this.nestedMappingStjDoc?.Dispose();
        this.blockScalarStjDoc?.Dispose();
        this.flowStyleStjDoc?.Dispose();
        this.anchorAliasStjDoc?.Dispose();
        this.smallConfigStjDoc?.Dispose();
        this.largeConfigStjDoc?.Dispose();
        this.complexMixedStjDoc?.Dispose();
        this.multiDocumentStjDoc?.Dispose();

        this.simpleScalarCtjDoc.Dispose();
        this.nestedMappingCtjDoc.Dispose();
        this.blockScalarCtjDoc.Dispose();
        this.flowStyleCtjDoc.Dispose();
        this.anchorAliasCtjDoc.Dispose();
        this.smallConfigCtjDoc.Dispose();
        this.largeConfigCtjDoc.Dispose();
        this.complexMixedCtjDoc.Dispose();
        this.multiDocumentCtjDoc.Dispose();
    }

    // ---- SimpleScalar ----

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_SimpleScalar()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.simpleScalarJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public string Corvus_Utf8_SimpleScalar()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.simpleScalarJsonBytes);
    }

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public string Corvus_CtjElement_SimpleScalar()
    {
        JsonElement element = this.simpleScalarCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("SimpleScalar")]
    [Benchmark]
    public string CorvusStj_Element_SimpleScalar()
    {
        return StjYamlDocument.ConvertToYamlString(this.simpleScalarStjDoc.RootElement);
    }

    // ---- NestedMapping ----

    [BenchmarkCategory("NestedMapping")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_NestedMapping()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.nestedMappingJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public string Corvus_Utf8_NestedMapping()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.nestedMappingJsonBytes);
    }

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public string Corvus_CtjElement_NestedMapping()
    {
        JsonElement element = this.nestedMappingCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("NestedMapping")]
    [Benchmark]
    public string CorvusStj_Element_NestedMapping()
    {
        return StjYamlDocument.ConvertToYamlString(this.nestedMappingStjDoc.RootElement);
    }

    // ---- BlockScalar ----

    [BenchmarkCategory("BlockScalar")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_BlockScalar()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.blockScalarJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public string Corvus_Utf8_BlockScalar()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.blockScalarJsonBytes);
    }

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public string Corvus_CtjElement_BlockScalar()
    {
        JsonElement element = this.blockScalarCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("BlockScalar")]
    [Benchmark]
    public string CorvusStj_Element_BlockScalar()
    {
        return StjYamlDocument.ConvertToYamlString(this.blockScalarStjDoc.RootElement);
    }

    // ---- FlowStyle ----

    [BenchmarkCategory("FlowStyle")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_FlowStyle()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.flowStyleJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public string Corvus_Utf8_FlowStyle()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.flowStyleJsonBytes);
    }

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public string Corvus_CtjElement_FlowStyle()
    {
        JsonElement element = this.flowStyleCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("FlowStyle")]
    [Benchmark]
    public string CorvusStj_Element_FlowStyle()
    {
        return StjYamlDocument.ConvertToYamlString(this.flowStyleStjDoc.RootElement);
    }

    // ---- AnchorAlias ----

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_AnchorAlias()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.anchorAliasJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public string Corvus_Utf8_AnchorAlias()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.anchorAliasJsonBytes);
    }

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public string Corvus_CtjElement_AnchorAlias()
    {
        JsonElement element = this.anchorAliasCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("AnchorAlias")]
    [Benchmark]
    public string CorvusStj_Element_AnchorAlias()
    {
        return StjYamlDocument.ConvertToYamlString(this.anchorAliasStjDoc.RootElement);
    }

    // ---- SmallConfig ----

    [BenchmarkCategory("SmallConfig")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_SmallConfig()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.smallConfigJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public string Corvus_Utf8_SmallConfig()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.smallConfigJsonBytes);
    }

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public string Corvus_CtjElement_SmallConfig()
    {
        JsonElement element = this.smallConfigCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("SmallConfig")]
    [Benchmark]
    public string CorvusStj_Element_SmallConfig()
    {
        return StjYamlDocument.ConvertToYamlString(this.smallConfigStjDoc.RootElement);
    }

    // ---- LargeConfig ----

    [BenchmarkCategory("LargeConfig")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_LargeConfig()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.largeConfigJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public string Corvus_Utf8_LargeConfig()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.largeConfigJsonBytes);
    }

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public string Corvus_CtjElement_LargeConfig()
    {
        JsonElement element = this.largeConfigCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("LargeConfig")]
    [Benchmark]
    public string CorvusStj_Element_LargeConfig()
    {
        return StjYamlDocument.ConvertToYamlString(this.largeConfigStjDoc.RootElement);
    }

    // ---- ComplexMixed ----

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_ComplexMixed()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.complexMixedJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public string Corvus_Utf8_ComplexMixed()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.complexMixedJsonBytes);
    }

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public string Corvus_CtjElement_ComplexMixed()
    {
        JsonElement element = this.complexMixedCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("ComplexMixed")]
    [Benchmark]
    public string CorvusStj_Element_ComplexMixed()
    {
        return StjYamlDocument.ConvertToYamlString(this.complexMixedStjDoc.RootElement);
    }

    // ---- MultiDocument ----

    [BenchmarkCategory("MultiDocument")]
    [Benchmark(Baseline = true)]
    public string YamlDotNet_MultiDocument()
    {
        object? obj = YamlDotNetDeserializer.Deserialize(this.multiDocumentJsonString);
        return YamlDotNetSerializer.Serialize(obj!);
    }

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public string Corvus_Utf8_MultiDocument()
    {
        return YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)this.multiDocumentJsonBytes);
    }

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public string Corvus_CtjElement_MultiDocument()
    {
        JsonElement element = this.multiDocumentCtjDoc.RootElement;
        return YamlDocument.ConvertToYamlString(in element);
    }

    [BenchmarkCategory("MultiDocument")]
    [Benchmark]
    public string CorvusStj_Element_MultiDocument()
    {
        return StjYamlDocument.ConvertToYamlString(this.multiDocumentStjDoc.RootElement);
    }

    private static (byte[] Bytes, string String) YamlToJson(string scenarioFile, bool multi = false)
    {
        byte[] yamlBytes = File.ReadAllBytes(Path.Combine("Scenarios", scenarioFile));

        using JsonDocument doc = multi
            ? StjYamlDocument.Parse(yamlBytes, StjMultiDocOptions)
            : StjYamlDocument.Parse(yamlBytes);

        byte[] jsonBytes = JsonSerializer.SerializeToUtf8Bytes(doc.RootElement);
        string jsonString = JsonSerializer.Serialize(doc.RootElement);
        return (jsonBytes, jsonString);
    }
}
