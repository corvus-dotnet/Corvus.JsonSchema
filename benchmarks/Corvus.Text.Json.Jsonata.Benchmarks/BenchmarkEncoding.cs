// <copyright file="BenchmarkEncoding.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
#if !NETFRAMEWORK
using Jsonata.Net.Native;
using Jsonata.Net.Native.Json;
#endif

namespace Corvus.Text.Json.Jsonata.Benchmarks;

/// <summary>
/// Benchmarks for encoding functions: $base64encode, $base64decode, $encodeUrlComponent,
/// $decodeUrlComponent, $encodeUrl, $decodeUrl.
/// </summary>
[MemoryDiagnoser]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[CategoriesColumn]
public class BenchmarkEncoding : JsonataBenchmarkBase
{
    private const string DataJson = "{}";

    private const string ExprBase64Encode = """$base64encode("hello world/test&value=1")""";
    private const string ExprBase64Decode = """$base64decode("aGVsbG8gd29ybGQvdGVzdCZ2YWx1ZT0x")""";
    private const string ExprEncodeUrlComponent = """$encodeUrlComponent("hello world/test&value=1")""";
    private const string ExprDecodeUrlComponent = """$decodeUrlComponent("hello%20world%2Ftest%26value%3D1")""";
    private const string ExprEncodeUrl = """$encodeUrl("https://example.com/path?q=hello world&x=1")""";
    private const string ExprDecodeUrl = """$decodeUrl("https://example.com/path?q=hello%20world&x=1")""";

    private JsonataEvaluator evaluator = null!;
    private ParsedJsonDocument<JsonElement>? doc;
    private JsonElement data;
    private JsonWorkspace workspace = null!;

#if !NETFRAMEWORK
    private JsonataQuery nativeBase64Encode = null!;
    private JsonataQuery nativeBase64Decode = null!;
    private JsonataQuery nativeEncodeUrlComponent = null!;
    private JsonataQuery nativeDecodeUrlComponent = null!;
    private JsonataQuery nativeEncodeUrl = null!;
    private JsonataQuery nativeDecodeUrl = null!;
    private JToken nativeData = null!;
#endif

    /// <summary>
    /// Global setup.
    /// </summary>
    [GlobalSetup]
    public void GlobalSetup()
    {
        this.doc = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(DataJson));
        this.data = this.doc.RootElement;
        this.evaluator = new JsonataEvaluator();
        this.workspace = JsonWorkspace.Create();

        this.evaluator.Evaluate(ExprBase64Encode, this.data);
        this.evaluator.Evaluate(ExprBase64Decode, this.data);
        this.evaluator.Evaluate(ExprEncodeUrlComponent, this.data);
        this.evaluator.Evaluate(ExprDecodeUrlComponent, this.data);
        this.evaluator.Evaluate(ExprEncodeUrl, this.data);
        this.evaluator.Evaluate(ExprDecodeUrl, this.data);

        Base64EncodeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        Base64DecodeCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        EncodeUrlComponentCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        DecodeUrlComponentCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        EncodeUrlCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();
        DecodeUrlCodeGen.Evaluate(this.data, this.workspace); this.workspace.Reset();

#if !NETFRAMEWORK
        this.nativeData = JToken.Parse(DataJson);
        this.nativeBase64Encode = new JsonataQuery(ExprBase64Encode);
        this.nativeBase64Decode = new JsonataQuery(ExprBase64Decode);
        this.nativeEncodeUrlComponent = new JsonataQuery(ExprEncodeUrlComponent);
        this.nativeDecodeUrlComponent = new JsonataQuery(ExprDecodeUrlComponent);
        this.nativeEncodeUrl = new JsonataQuery(ExprEncodeUrl);
        this.nativeDecodeUrl = new JsonataQuery(ExprDecodeUrl);
#endif
    }

    /// <summary>
    /// Global cleanup.
    /// </summary>
    [GlobalCleanup]
    public void GlobalCleanup()
    {
        this.workspace.Dispose();
        this.doc?.Dispose();
    }

    // ── $base64encode ────────────────────────────────────────

    /// <summary>Corvus RT: $base64encode.</summary>
    [BenchmarkCategory("Base64Encode")]
    [Benchmark]
    public JsonElement Corvus_Base64Encode()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprBase64Encode, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $base64encode.</summary>
    [BenchmarkCategory("Base64Encode")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Base64Encode() => this.nativeBase64Encode.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $base64encode.</summary>
    [BenchmarkCategory("Base64Encode")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Base64Encode()
    {
        this.workspace.Reset();
        return Base64EncodeCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $base64decode ────────────────────────────────────────

    /// <summary>Corvus RT: $base64decode.</summary>
    [BenchmarkCategory("Base64Decode")]
    [Benchmark]
    public JsonElement Corvus_Base64Decode()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprBase64Decode, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $base64decode.</summary>
    [BenchmarkCategory("Base64Decode")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_Base64Decode() => this.nativeBase64Decode.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $base64decode.</summary>
    [BenchmarkCategory("Base64Decode")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_Base64Decode()
    {
        this.workspace.Reset();
        return Base64DecodeCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $encodeUrlComponent ──────────────────────────────────

    /// <summary>Corvus RT: $encodeUrlComponent.</summary>
    [BenchmarkCategory("EncodeUrlComponent")]
    [Benchmark]
    public JsonElement Corvus_EncodeUrlComponent()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprEncodeUrlComponent, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $encodeUrlComponent.</summary>
    [BenchmarkCategory("EncodeUrlComponent")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_EncodeUrlComponent() => this.nativeEncodeUrlComponent.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $encodeUrlComponent.</summary>
    [BenchmarkCategory("EncodeUrlComponent")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_EncodeUrlComponent()
    {
        this.workspace.Reset();
        return EncodeUrlComponentCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $decodeUrlComponent ──────────────────────────────────

    /// <summary>Corvus RT: $decodeUrlComponent.</summary>
    [BenchmarkCategory("DecodeUrlComponent")]
    [Benchmark]
    public JsonElement Corvus_DecodeUrlComponent()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprDecodeUrlComponent, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $decodeUrlComponent.</summary>
    [BenchmarkCategory("DecodeUrlComponent")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_DecodeUrlComponent() => this.nativeDecodeUrlComponent.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $decodeUrlComponent.</summary>
    [BenchmarkCategory("DecodeUrlComponent")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_DecodeUrlComponent()
    {
        this.workspace.Reset();
        return DecodeUrlComponentCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $encodeUrl ───────────────────────────────────────────

    /// <summary>Corvus RT: $encodeUrl.</summary>
    [BenchmarkCategory("EncodeUrl")]
    [Benchmark]
    public JsonElement Corvus_EncodeUrl()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprEncodeUrl, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $encodeUrl.</summary>
    [BenchmarkCategory("EncodeUrl")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_EncodeUrl() => this.nativeEncodeUrl.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $encodeUrl.</summary>
    [BenchmarkCategory("EncodeUrl")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_EncodeUrl()
    {
        this.workspace.Reset();
        return EncodeUrlCodeGen.Evaluate(this.data, this.workspace);
    }

    // ── $decodeUrl ───────────────────────────────────────────

    /// <summary>Corvus RT: $decodeUrl.</summary>
    [BenchmarkCategory("DecodeUrl")]
    [Benchmark]
    public JsonElement Corvus_DecodeUrl()
    {
        this.workspace.Reset();
        return this.evaluator.Evaluate(ExprDecodeUrl, this.data, this.workspace);
    }

#if !NETFRAMEWORK
    /// <summary>Native: $decodeUrl.</summary>
    [BenchmarkCategory("DecodeUrl")]
    [Benchmark(Baseline = true)]
    public JToken JsonataDotNet_DecodeUrl() => this.nativeDecodeUrl.Eval(this.nativeData);
#endif

    /// <summary>CodeGen: $decodeUrl.</summary>
    [BenchmarkCategory("DecodeUrl")]
    [Benchmark]
    public JsonElement Corvus_CodeGen_DecodeUrl()
    {
        this.workspace.Reset();
        return DecodeUrlCodeGen.Evaluate(this.data, this.workspace);
    }
}
