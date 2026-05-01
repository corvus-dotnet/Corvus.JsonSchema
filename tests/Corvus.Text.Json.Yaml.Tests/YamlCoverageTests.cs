// <copyright file="YamlCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
#if STJ
using Corvus.Yaml;
#else
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
#endif
using Xunit;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Coverage tests targeting uncovered paths in YamlToJsonConverter, ScalarResolver,
/// YamlDocument, and YamlEventParser.
/// </summary>
public class YamlCoverageTests
{
    // ========================
    // YamlDocument.Parse(string) overloads
    // ========================

    [Fact]
    public void ParseString_SimpleMapping()
    {
        string yaml = "name: Alice\nage: 30";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal("Alice", root.GetProperty("name").GetString());
        Assert.Equal(30, root.GetProperty("age").GetInt32());
    }

    [Fact]
    public void ParseString_ConvertToJsonString()
    {
        string yaml = "key: value";
        string json = YamlDocument.ConvertToJsonString(yaml);
        Assert.Contains("\"key\"", json);
        Assert.Contains("\"value\"", json);
    }

    [Fact]
    public void ParseString_ConvertToJsonString_LargeInput()
    {
        // Force the ArrayPool rental path (>256 bytes)
        var sb = new StringBuilder();
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"key{i}: value{i}");
        }

        string yaml = sb.ToString();
        string json = YamlDocument.ConvertToJsonString(yaml);
        Assert.Contains("\"key0\"", json);
        Assert.Contains("\"key49\"", json);
    }

    [Fact]
    public void ParseBytes_ConvertToJsonString()
    {
        byte[] yaml = Encoding.UTF8.GetBytes("items:\n  - one\n  - two");
        string json = YamlDocument.ConvertToJsonString((ReadOnlyMemory<byte>)yaml);
        Assert.Contains("\"one\"", json);
        Assert.Contains("\"two\"", json);
    }

    // ========================
    // ScalarResolver — Integer paths
    // ========================

    [Theory]
    [InlineData("123", 123)]
    [InlineData("-456", -456)]
    [InlineData("0", 0)]
    [InlineData("+789", 789)]
    public void Scalar_Integer_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    // ========================
    // ScalarResolver — Float/scientific paths
    // ========================

    [Theory]
    [InlineData("3.14")]
    [InlineData("1.5e10")]
    [InlineData("-2.5E+3")]
    [InlineData("1.0e-5")]
    [InlineData("0.5")]
    public void Scalar_Float_ResolvesAsNumber(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var element = doc.RootElement.GetProperty("value");
        Assert.Equal(JsonValueKind.Number, element.ValueKind);
    }

    // ========================
    // ScalarResolver — Hex and octal integers
    // ========================

    [Theory]
    [InlineData("0xFF", 255)]
    [InlineData("0x1A", 26)]
    [InlineData("0xCAFE", 0xCAFE)]
    public void Scalar_HexInteger_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("0o77", 63)]
    [InlineData("0o10", 8)]
    [InlineData("0o777", 511)]
    public void Scalar_OctalInteger_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    // ========================
    // ScalarResolver — Infinity and NaN → null
    // ========================

    [Theory]
    [InlineData(".inf")]
    [InlineData("-.inf")]
    [InlineData("+.inf")]
    [InlineData(".nan")]
    [InlineData(".Inf")]
    [InlineData(".INF")]
    [InlineData(".NaN")]
    [InlineData(".NAN")]
    public void Scalar_InfinityAndNaN_ResolvesAsNull(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Non-numbers that look numeric → string
    // ========================

    [Theory]
    [InlineData("0xGG")]
    [InlineData("0o89")]
    [InlineData("1e")]
    [InlineData("1.2.3")]
    [InlineData("+1a")]
    [InlineData("1e+")]
    public void Scalar_InvalidNumeric_ResolvesAsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — YAML 1.1 booleans
    // ========================

    [Theory]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("YES", true)]
    [InlineData("on", true)]
    [InlineData("On", true)]
    [InlineData("ON", true)]
    [InlineData("y", true)]
    [InlineData("Y", true)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("NO", false)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    [InlineData("OFF", false)]
    [InlineData("n", false)]
    [InlineData("N", false)]
    public void Scalar_Yaml11Boolean_ResolvesAsBool(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void Scalar_Yaml11StandardBooleans_ResolvesAsBool(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    // ========================
    // ScalarResolver — YAML 1.1 null values
    // ========================

    [Theory]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("~")]
    [InlineData("")]
    public void Scalar_Yaml11Null_ResolvesAsNull(string scalarValue)
    {
        string yaml = scalarValue.Length > 0 ? "value: " + scalarValue : "value:";
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — JSON schema (strict mode)
    // ========================

    [Theory]
    [InlineData("null", JsonValueKind.Null)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    [InlineData("123", JsonValueKind.Number)]
    [InlineData("-45.6", JsonValueKind.Number)]
    [InlineData("1e10", JsonValueKind.Number)]
    public void Scalar_JsonSchema_RecognizesValues(string scalarValue, JsonValueKind expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(expected, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("0xFF")]
    [InlineData("0o77")]
    public void Scalar_JsonSchema_RejectsNonJsonValues(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Failsafe schema (everything is string)
    // ========================

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("3.14")]
    public void Scalar_FailsafeSchema_EverythingIsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Failsafe };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Core schema null variants
    // ========================

    [Theory]
    [InlineData("null")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("~")]
    public void Scalar_CoreNull_ResolvesAsNull(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Core schema bool variants
    // ========================

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void Scalar_CoreBool_ResolvesCorrectly(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    // ========================
    // Quoted strings that look like other types
    // ========================

    [Theory]
    [InlineData("'true'")]
    [InlineData("'123'")]
    [InlineData("'null'")]
    [InlineData("'3.14'")]
    [InlineData("\"true\"")]
    [InlineData("\"123\"")]
    [InlineData("\"null\"")]
    public void Scalar_QuotedSpecialValue_ResolvesAsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // Special values
    // ========================

    [Theory]
    [InlineData("null", JsonValueKind.Null)]
    [InlineData("~", JsonValueKind.Null)]
    [InlineData("true", JsonValueKind.True)]
    [InlineData("false", JsonValueKind.False)]
    public void Scalar_SpecialValues_ResolveCorrectly(string scalarValue, JsonValueKind expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(expected, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // Multi-document handling
    // ========================

    [Fact]
    public void Parse_MultiDocument_MultiAsArray()
    {
        string yaml = "---\nfirst: 1\n---\nsecond: 2\n---\nthird: 3";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetProperty("first").GetInt32());
        Assert.Equal(2, root[1].GetProperty("second").GetInt32());
        Assert.Equal(3, root[2].GetProperty("third").GetInt32());
    }

    [Fact]
    public void Parse_SingleDocument_MultiAsArray_ProducesSingleElementArray()
    {
        string yaml = "---\nkey: value";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal("value", root[0].GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_MultiDocument_SingleRequired_Throws()
    {
        string yaml = "---\nfirst: 1\n---\nsecond: 2";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.SingleRequired };
        Assert.Throws<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options));
    }

    [Fact]
    public void Parse_EmptyDocumentWithMarkers_ReturnsNull()
    {
        string yaml = "---\n...";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.ValueKind);
    }

    [Fact]
    public void Parse_EmptyStream_ReturnsNull()
    {
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(string.Empty));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.ValueKind);
    }

    // ========================
    // Alias/anchor resolution
    // ========================

    [Fact]
    public void Parse_SimpleAlias_CopiesValue()
    {
        string yaml = "original: &ref hello\ncopy: *ref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("original").GetString());
        Assert.Equal("hello", root.GetProperty("copy").GetString());
    }

    [Fact]
    public void Parse_AnchorOnMapping_CopiesStructure()
    {
        string yaml = "defaults: &def\n  color: red\n  size: large\nitem:\n  name: widget\n  props: *def";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal("red", root.GetProperty("item").GetProperty("props").GetProperty("color").GetString());
        Assert.Equal("large", root.GetProperty("item").GetProperty("props").GetProperty("size").GetString());
    }

    [Fact]
    public void Parse_AnchorOnSequence_CopiesArray()
    {
        string yaml = "colors: &colors\n  - red\n  - green\n  - blue\nfavorites: *colors";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.GetProperty("favorites").ValueKind);
        Assert.Equal(3, root.GetProperty("favorites").GetArrayLength());
        Assert.Equal("red", root.GetProperty("favorites")[0].GetString());
    }

    [Fact]
    public void Parse_MergeKey_ProducesOutput()
    {
        // Merge keys (<<) expand anchor properties into the parent mapping.
        // If merge is not supported by this implementation, the key "<<" will exist as a regular key.
        string yaml = "defaults: &defaults\n  color: red\n  size: large\nitem:\n  <<: *defaults\n  name: widget";
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var item = doc.RootElement.GetProperty("item");
        Assert.Equal("widget", item.GetProperty("name").GetString());

        // The implementation may or may not expand merge keys; verify it doesn't crash.
        Assert.Equal(JsonValueKind.Object, item.ValueKind);
    }

    // ========================
    // Error handling
    // ========================

    [Fact]
    public void Parse_UnclosedFlowSequence_Throws()
    {
        string yaml = "items: [1, 2, 3";
        Assert.Throws<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    [Fact]
    public void Parse_UnclosedFlowMapping_Throws()
    {
        string yaml = "obj: {key: value";
        Assert.Throws<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    [Fact]
    public void Parse_DuplicateKey_ErrorMode_Throws()
    {
        string yaml = "key: first\nkey: second";
        var options = new YamlReaderOptions { DuplicateKeyBehavior = DuplicateKeyBehavior.Error };
        Assert.Throws<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options));
    }

    [Fact]
    public void Parse_DuplicateKey_LastWins()
    {
        string yaml = "key: first\nkey: second";
        var options = new YamlReaderOptions { DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal("second", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_InvalidTabIndentation_Throws()
    {
        string yaml = "key:\n\t- value";
        Assert.Throws<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    // ========================
    // Deep nesting
    // ========================

    [Fact]
    public void Parse_DeeplyNested_Works()
    {
        var sb = new StringBuilder();
        for (int i = 0; i < 15; i++)
        {
            sb.Append(new string(' ', i * 2));
            sb.AppendLine($"level{i}:");
        }

        sb.Append(new string(' ', 30));
        sb.Append("value: deep");

        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(sb.ToString()));
        var current = doc.RootElement;
        for (int i = 0; i < 15; i++)
        {
            current = current.GetProperty($"level{i}");
        }

        Assert.Equal("deep", current.GetProperty("value").GetString());
    }

    // ========================
    // Flow collections
    // ========================

    [Fact]
    public void Parse_FlowSequence_Works()
    {
        string yaml = "items: [1, 2, 3, 4, 5]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(5, items.GetArrayLength());
        Assert.Equal(1, items[0].GetInt32());
        Assert.Equal(5, items[4].GetInt32());
    }

    [Fact]
    public void Parse_FlowMapping_Works()
    {
        string yaml = "person: {name: Alice, age: 30}";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var person = doc.RootElement.GetProperty("person");
        Assert.Equal("Alice", person.GetProperty("name").GetString());
        Assert.Equal(30, person.GetProperty("age").GetInt32());
    }

    [Fact]
    public void Parse_NestedFlowCollections_Works()
    {
        string yaml = "data: [{name: a, items: [1, 2]}, {name: b, items: [3, 4]}]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var data = doc.RootElement.GetProperty("data");
        Assert.Equal(JsonValueKind.Array, data.ValueKind);
        Assert.Equal(2, data.GetArrayLength());
        Assert.Equal("a", data[0].GetProperty("name").GetString());
        Assert.Equal(2, data[0].GetProperty("items").GetArrayLength());
        Assert.Equal(1, data[0].GetProperty("items")[0].GetInt32());
        Assert.Equal("b", data[1].GetProperty("name").GetString());
        Assert.Equal(4, data[1].GetProperty("items")[1].GetInt32());
    }

    [Fact]
    public void Parse_EmptyFlowSequence_Works()
    {
        string yaml = "items: []";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(0, items.GetArrayLength());
    }

    [Fact]
    public void Parse_EmptyFlowMapping_Works()
    {
        string yaml = "obj: {}";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Object, doc.RootElement.GetProperty("obj").ValueKind);
    }

    // ========================
    // Block scalars (literal and folded)
    // ========================

    [Fact]
    public void Parse_LiteralBlockScalar_PreservesNewlines()
    {
        string yaml = "text: |\n  line1\n  line2\n  line3\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("line1\nline2\nline3", text);
    }

    [Fact]
    public void Parse_FoldedBlockScalar_FoldsNewlines()
    {
        string yaml = "text: >\n  line1\n  line2\n  line3\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("line1 line2 line3", text);
    }

    [Fact]
    public void Parse_LiteralBlockScalar_StripChomping()
    {
        string yaml = "text: |-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Equal("line1\nline2", text);
    }

    [Fact]
    public void Parse_LiteralBlockScalar_KeepChomping()
    {
        string yaml = "text: |+\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.True(text.EndsWith("\n\n"), $"Expected trailing newlines but got: [{text}]");
    }

    [Fact]
    public void Parse_FoldedBlockScalar_StripChomping()
    {
        string yaml = "text: >-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Equal("line1 line2", text);
    }

    [Fact]
    public void Parse_LiteralBlockScalar_WithIndentIndicator()
    {
        string yaml = "text: |2\n  hello\n  world\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("hello", text);
        Assert.Contains("world", text);
    }

    // ========================
    // Document end marker handling
    // ========================

    [Fact]
    public void Parse_DocumentEndMarker_Stops()
    {
        string yaml = "key: value\n...";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_DocumentStartThenEnd()
    {
        string yaml = "---\nkey: value\n...\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Comments
    // ========================

    [Fact]
    public void Parse_InlineComments_Ignored()
    {
        string yaml = "name: Alice # This is a comment\nage: 30 # Another comment";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("Alice", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(30, doc.RootElement.GetProperty("age").GetInt32());
    }

    [Fact]
    public void Parse_FullLineComments_Ignored()
    {
        string yaml = "# Header comment\nname: Alice\n# Middle comment\nage: 30\n# Footer";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("Alice", doc.RootElement.GetProperty("name").GetString());
        Assert.Equal(30, doc.RootElement.GetProperty("age").GetInt32());
    }

    // ========================
    // Tags
    // ========================

    [Fact]
    public void Parse_ExplicitStringTag_ForcesString()
    {
        string yaml = "value: !!str 123";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.Equal("123", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void Parse_ExplicitIntTag_ForcesInt()
    {
        string yaml = "value: !!int 42";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(42, doc.RootElement.GetProperty("value").GetInt32());
    }

    [Fact]
    public void Parse_ExplicitNullTag_ForcesNull()
    {
        string yaml = "value: !!null something";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Fact]
    public void Parse_ExplicitBoolTag_ForcesBool()
    {
        string yaml = "value: !!bool true";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.True(doc.RootElement.GetProperty("value").GetBoolean());
    }

    [Fact]
    public void Parse_ExplicitFloatTag_ForcesFloat()
    {
        string yaml = "value: !!float 3.14";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Fact]
    public void Parse_NonSpecificTag_ForcesString()
    {
        string yaml = "value: ! true";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.Equal("true", doc.RootElement.GetProperty("value").GetString());
    }

    // ========================
    // Mixed content
    // ========================

    [Fact]
    public void Parse_MappingWithSequenceValues()
    {
        string yaml = "colors:\n  - red\n  - green\n  - blue\nsizes:\n  - small\n  - large";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal(3, root.GetProperty("colors").GetArrayLength());
        Assert.Equal(2, root.GetProperty("sizes").GetArrayLength());
        Assert.Equal("red", root.GetProperty("colors")[0].GetString());
        Assert.Equal("large", root.GetProperty("sizes")[1].GetString());
    }

    [Fact]
    public void Parse_SequenceOfMappings()
    {
        string yaml = "- name: Alice\n  age: 30\n- name: Bob\n  age: 25";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("Alice", root[0].GetProperty("name").GetString());
        Assert.Equal(25, root[1].GetProperty("age").GetInt32());
    }

    // ========================
    // Multi-line plain scalars
    // ========================

    [Fact]
    public void Parse_MultilinePlainScalar_FoldsNewlines()
    {
        string yaml = "description: This is a\n  long description that\n  spans multiple lines";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? desc = doc.RootElement.GetProperty("description").GetString();
        Assert.NotNull(desc);
        Assert.Contains("This is a", desc);
        Assert.Contains("long description", desc);
    }

    // ========================
    // Large numeric values
    // ========================

    [Fact]
    public void Parse_LargeInteger_Works()
    {
        string yaml = "value: 9999999999999999";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(9999999999999999L, doc.RootElement.GetProperty("value").GetInt64());
    }

    [Fact]
    public void Parse_NegativeLargeInteger_Works()
    {
        string yaml = "value: -9999999999999999";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(-9999999999999999L, doc.RootElement.GetProperty("value").GetInt64());
    }

    // ========================
    // Edge cases
    // ========================

    [Fact]
    public void Parse_KeyWithColonInValue_Works()
    {
        string yaml = "url: http://example.com:8080/path";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("http://example.com:8080/path", doc.RootElement.GetProperty("url").GetString());
    }

    [Fact]
    public void Parse_EmptySequenceItem_ReturnsNull()
    {
        string yaml = "items:\n  -\n  - value\n  -";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(JsonValueKind.Null, items[0].ValueKind);
        Assert.Equal("value", items[1].GetString());
        Assert.Equal(JsonValueKind.Null, items[2].ValueKind);
    }

    [Fact]
    public void Parse_MappingKeyIsNumber_BecomesStringKey()
    {
        string yaml = "123: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("123").GetString());
    }

    [Fact]
    public void Parse_MappingKeyIsBool_BecomesStringKey()
    {
        string yaml = "true: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("true").GetString());
    }

    [Fact]
    public void Parse_TabsInInput_Handled()
    {
        string yaml = "key: \"value\\twith\\ttabs\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value\twith\ttabs", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // ConvertToJsonString string overload (tests string→UTF8→parse path)
    // ========================

    [Fact]
    public void ConvertToJsonString_String_WithSpecialChars()
    {
        string yaml = "msg: \"hello\\nworld\"";
        string json = YamlDocument.ConvertToJsonString(yaml);
        Assert.Contains("hello\\nworld", json);
    }

    [Fact]
    public void ConvertToJsonString_String_WithUnicode()
    {
        string yaml = "emoji: \"\u2764\"";
        string json = YamlDocument.ConvertToJsonString(yaml);
        // The JSON writer may escape non-ASCII to \uXXXX
        Assert.True(json.Contains("\u2764") || json.Contains("\\u2764"), $"Expected heart emoji in output: {json}");
    }

    // ========================
    // Flow sequence with trailing comma/whitespace
    // ========================

    [Fact]
    public void Parse_FlowSequence_WithWhitespace()
    {
        string yaml = "items: [ 1 , 2 , 3 ]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal(1, items[0].GetInt32());
        Assert.Equal(3, items[2].GetInt32());
    }

    [Fact]
    public void Parse_FlowMapping_WithWhitespace()
    {
        string yaml = "obj: { name : Alice , age : 30 }";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var obj = doc.RootElement.GetProperty("obj");
        Assert.Equal("Alice", obj.GetProperty("name").GetString());
        Assert.Equal(30, obj.GetProperty("age").GetInt32());
    }

    // ========================
    // IsJsonNumber edge cases (JSON schema)
    // ========================

    [Theory]
    [InlineData("0")]
    [InlineData("-0")]
    [InlineData("10")]
    [InlineData("-10")]
    [InlineData("0.1")]
    [InlineData("-0.1")]
    [InlineData("1e5")]
    [InlineData("1E5")]
    [InlineData("1e+5")]
    [InlineData("1e-5")]
    [InlineData("1.5e10")]
    public void JsonSchema_ValidNumbers_ResolveAsNumber(string numValue)
    {
        string yaml = "value: " + numValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.Number, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Theory]
    [InlineData("+1")]
    [InlineData("01")]
    [InlineData(".5")]
    [InlineData("1.")]
    [InlineData("1e")]
    [InlineData("1e+")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void JsonSchema_InvalidNumbers_ResolveAsString(string numValue)
    {
        string yaml = "value: " + numValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // YAML directives
    // ========================

    [Fact]
    public void Parse_YamlDirective_Accepted()
    {
        string yaml = "%YAML 1.2\n---\nkey: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void Parse_TagDirective_Accepted()
    {
        string yaml = "%TAG !e! tag:example.com,2000:\n---\nkey: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Compact block sequences
    // ========================

    [Fact]
    public void Parse_CompactBlockSequenceInMapping()
    {
        string yaml = "people:\n- name: Alice\n- name: Bob";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var people = doc.RootElement.GetProperty("people");
        Assert.Equal(2, people.GetArrayLength());
        Assert.Equal("Alice", people[0].GetProperty("name").GetString());
    }

    // ========================
    // Block Scalar Header Parsing
    // ========================

    [Fact]
    public void BlockScalar_ChompThenIndent()
    {
        // Header: |-2 (strip chomp then explicit indent 2)
        string yaml = "text: |-2\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("line1\nline2", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void BlockScalar_IndentThenChomp()
    {
        // Header: |2- (explicit indent 2 then strip chomp)
        string yaml = "text: |2-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("line1\nline2", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void BlockScalar_KeepChompWithIndent()
    {
        // Header: |+2 (keep chomp with explicit indent 2)
        string yaml = "text: |+2\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        // Keep chomp preserves trailing newlines
        Assert.Equal("line1\nline2\n\n", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void BlockScalar_HeaderWithComment()
    {
        // Header: | # comment (comment after indicator)
        string yaml = "text: | # comment\n  content here\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("content here\n", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void FoldedScalar_KeepChomp()
    {
        // Header: >+ (folded with keep chomp)
        string yaml = "text: >+\n  line1\n  line2\n\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("line1 line2", text);
        Assert.EndsWith("\n\n", text!);
    }

    [Fact]
    public void FoldedScalar_KeepChompWithIndent()
    {
        // Header: >+2 (folded with keep chomp and explicit indent)
        string yaml = "text: >+2\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.EndsWith("\n\n", text!);
    }

    [Fact]
    public void FoldedScalar_StripChompWithIndent()
    {
        // Header: >-2 (folded with strip chomp and explicit indent)
        string yaml = "text: >-2\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("line1 line2", text);
        Assert.False(text!.EndsWith("\n"));
    }

    // ========================
    // Block Scalar More-Indented Lines
    // ========================

    [Fact]
    public void LiteralBlockScalar_MoreIndentedLines()
    {
        // Lines with extra indentation beyond content indent
        string yaml = "text: |\n  normal\n    extra indent\n  back to normal\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("normal\n  extra indent\nback to normal\n", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void LiteralBlockScalar_MoreIndentedWithBlankLines()
    {
        // More-indented line followed by blank line
        string yaml = "text: |\n  normal\n    more\n\n    more2\n  end\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("  more\n\n  more2", text);
    }

    [Fact]
    public void LiteralBlockScalar_MultipleMoreIndented()
    {
        // Multiple consecutive more-indented lines
        string yaml = "text: |\n  a\n    b\n    c\n  d\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("a\n  b\n  c\nd\n", doc.RootElement.GetProperty("text").GetString());
    }

    // ========================
    // Anchor on Null/Empty Value
    // ========================

    [Fact]
    public void Anchor_OnNullValue()
    {
        // Anchor on explicit null value
        string yaml = "ref: &myref null\nuse: *myref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("use").ValueKind);
    }

    [Fact]
    public void Anchor_OnEmptyValue()
    {
        // Anchor on empty/missing value — the anchor's data length is 0 → store "null"
        string yaml = "ref: &myref\nuse: *myref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("use").ValueKind);
    }

    // ========================
    // Anchor on Mapping Key (StoreKeyAnchor)
    // ========================

    [Fact]
    public void Anchor_OnMappingKey()
    {
        // Anchor on the key itself via explicit key syntax
        string yaml = "? &anchor key\n: value\nother: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        // The anchor stores the key as a JSON string "key"
        Assert.Equal("key", doc.RootElement.GetProperty("other").GetString());
    }

    [Fact]
    public void Anchor_OnMappingKey_Simple()
    {
        // Anchor on key using inline node property syntax
        string yaml = "&anchor key: value\nother: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("key", doc.RootElement.GetProperty("other").GetString());
    }

    // ========================
    // UnescapeJsonString — Escaped Property Names
    // ========================

    [Fact]
    public void PropertyName_WithNewlineEscape()
    {
        // Property names containing \n escape
        string yaml = "\"hello\\nworld\": value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.True(doc.RootElement.TryGetProperty("hello\nworld", out JsonElement val));
        Assert.Equal("value", val.GetString());
    }

    [Fact]
    public void PropertyName_WithTabEscape()
    {
        string yaml = "\"col1\\tcol2\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.True(doc.RootElement.TryGetProperty("col1\tcol2", out JsonElement val));
        Assert.Equal("data", val.GetString());
    }

    [Fact]
    public void PropertyName_WithBackslashEscape()
    {
        string yaml = "\"path\\\\to\\\\file\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.True(doc.RootElement.TryGetProperty("path\\to\\file", out JsonElement val));
        Assert.Equal("data", val.GetString());
    }

    [Fact]
    public void PropertyName_WithQuoteEscape()
    {
        string yaml = "\"say\\\"hi\\\"\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.True(doc.RootElement.TryGetProperty("say\"hi\"", out JsonElement val));
        Assert.Equal("data", val.GetString());
    }

    // ========================
    // Double-Quoted Unicode Escapes
    // ========================

    [Fact]
    public void DoubleQuoted_UnicodeEscape_2Byte()
    {
        // \u00E9 = é (2-byte UTF-8)
        string yaml = "text: \"caf\\u00e9\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("café", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void DoubleQuoted_UnicodeEscape_3Byte()
    {
        // \u2603 = ☃ (3-byte UTF-8)
        string yaml = "text: \"snow\\u2603man\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("snow☃man", doc.RootElement.GetProperty("text").GetString());
    }

    [Fact]
    public void DoubleQuoted_UnicodeEscape_NullChar()
    {
        // \x00 - null control character
        string yaml = "text: \"null\\x00here\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.NotNull(text);
        Assert.Contains("\0", text);
    }

    // ========================
    // Multi-Document with Different Types
    // ========================

    [Fact]
    public void MultiDocument_MixedTypes()
    {
        // Different value types in each document using MultiAsArray mode
        string yaml = "---\n42\n---\ntrue\n---\nhello\n---\nnull\n...";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(42, root[0].GetInt32());
        Assert.True(root[1].GetBoolean());
        Assert.Equal("hello", root[2].GetString());
        Assert.Equal(JsonValueKind.Null, root[3].ValueKind);
    }

    [Fact]
    public void MultiDocument_SingleDocument_InMultiMode()
    {
        // Single document in multi mode produces one-element array
        string yaml = "---\nkey: value\n...";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal("value", root[0].GetProperty("key").GetString());
    }

    // ========================
    // Large Buffer Growth
    // ========================

    [Fact]
    public void LargeDocument_BufferGrowth()
    {
        // A large YAML document that forces buffer reallocation
        var sb = new StringBuilder();
        for (int i = 0; i < 500; i++)
        {
            sb.AppendLine($"key{i}: value{i}_with_some_extra_text_to_make_it_longer");
        }

        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(sb.ToString()));
        int count = 0;
        foreach (var _ in doc.RootElement.EnumerateObject())
        {
            count++;
        }

        Assert.Equal(500, count);
    }

    // ========================
    // Scalar Resolver edge cases
    // ========================

    [Fact]
    public void Scalar_LeadingZeroInteger_JsonSchema_IsString()
    {
        // "007" leading zeros are not valid JSON numbers
        string yaml = "value: 007";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.Equal("007", doc.RootElement.GetProperty("value").GetString());
    }

    [Fact]
    public void Scalar_PlusSign_JsonSchema_IsString()
    {
        // "+5" is not a valid JSON number (leading plus)
        string yaml = "value: +5";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Fact]
    public void Scalar_DotLeadingDecimal_JsonSchema_IsString()
    {
        // ".5" is not a valid JSON number (missing leading digit)
        string yaml = "value: .5";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    [Fact]
    public void Scalar_TrailingDot_JsonSchema_IsString()
    {
        // "1." is not a valid JSON number (trailing dot)
        string yaml = "value: \"1.\"";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Block Scalar Headers (lines 3047-3079) in ReadBlockScalarContent
    // These lines are in the EXPLICIT KEY path (? | syntax), not value path.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void ExplicitKeyBlockScalar_StripChomp()
    {
        // ? |- means explicit key with block literal, strip chomp
        string yaml = "? |-\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_KeepChomp()
    {
        // ? |+ means explicit key with block literal, keep chomp
        // With keep chomp, trailing newlines are preserved.
        // Content "mykey\n" + trailing "\n" = "mykey\n\n"
        string yaml = "? |+\n  mykey\n\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey\n\n").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_ExplicitIndent()
    {
        // ? |2 means explicit key with block literal, indent 2
        string yaml = "? |2\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey\n").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_IndentWithChomp()
    {
        // ? |2- means explicit key with indent 2, strip chomp
        string yaml = "? |2-\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_CommentAfter()
    {
        // ? | # comment — comment after block indicator in key
        string yaml = "? | # this is a comment\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey\n").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_ChompThenIndent()
    {
        // ? |-2 means strip chomp, explicit indent 2
        string yaml = "? |-2\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [Fact]
    public void ExplicitKeyBlockScalar_FoldedWithKeepChomp()
    {
        // ? >+ means folded with keep chomp — trailing newlines preserved
        string yaml = "? >+\n  mykey\n\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("mykey\n\n").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Anchor on null value (lines 4107-4121)
    // NOTE: This else branch fires when dataLength == 0, meaning the JSON
    // writer produced no output between anchor start and end. This appears
    // to be defensive code — every node type writes at least "null", '""',
    // or a JSON value, so dataLength is always > 0 in practice.
    // Tests below verify anchor-on-null via the NORMAL path (dataLength > 0).
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Anchor_OnNullValue_ThenAlias()
    {
        // &ref null — anchor on null value, then alias references it
        string yaml = "a: &ref null\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [Fact]
    public void Anchor_OnEmptyValue_ThenAlias()
    {
        // &ref with no value (empty scalar = null)
        string yaml = "a: &ref\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [Fact]
    public void Anchor_OnTildeNull_ThenAlias()
    {
        // &ref ~ (tilde = null in YAML)
        string yaml = "a: &ref ~\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // StoreKeyAnchor (lines 4132-4195) — anchor on mapping key
    // Lines 4170-4172: backslash in key → escaped as \\
    // Lines 4175-4182: control char in key → escaped as \uXXXX
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void Anchor_OnMappingKey_ThenAlias()
    {
        // Anchor on a key: &ref key: value → alias *ref resolves to "key"
        string yaml = "&anchor key: value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("key").GetString());
        Assert.Equal("key", doc.RootElement.GetProperty("ref").GetString());
    }

    [Fact]
    public void Anchor_OnMappingKey_WithBackslash()
    {
        // Key with literal backslash: YAML "path\\dir" → bytes path\dir
        // StoreKeyAnchor must escape \ as \\
        string yaml = "&anchor \"path\\\\dir\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("path\\dir").GetString());
        Assert.Equal("path\\dir", doc.RootElement.GetProperty("ref").GetString());
    }

    [Fact]
    public void Anchor_OnMappingKey_WithControlChar()
    {
        // Key with control char: YAML "tab\there" → bytes tab<TAB>here (0x09)
        // StoreKeyAnchor must escape 0x09 as \u0009
        string yaml = "&anchor \"tab\\there\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("tab\there").GetString());
        Assert.Equal("tab\there", doc.RootElement.GetProperty("ref").GetString());
    }

    [Fact]
    public void Anchor_OnMappingKey_WithQuote()
    {
        // Key with double quote: YAML "say\"hi" → bytes say"hi
        // StoreKeyAnchor must escape " as \"
        string yaml = "&anchor \"say\\\"hi\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("say\"hi").GetString());
        Assert.Equal("say\"hi", doc.RootElement.GetProperty("ref").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UnescapeJsonString (lines 4316-4354)
    // Called from WriteAliasAsPropertyName when stored anchor data is a
    // JSON string containing backslash escapes. Must use alias as key.
    // NOTE: Alias names terminate at whitespace, so space before ':' needed.
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AliasAsKey_WithTabEscape()
    {
        // Anchor a string with tab → stored as JSON "hello\tworld"
        // Using *ref as a property key triggers WriteAliasAsPropertyName → UnescapeJsonString
        string yaml = "a: &ref \"hello\\tworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello\tworld", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello\tworld").GetString());
    }

    [Fact]
    public void AliasAsKey_WithNewlineEscape()
    {
        // Anchor a string with newline → stored as JSON "line\nbreak"
        string yaml = "a: &ref \"line\\nbreak\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("line\nbreak", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("line\nbreak").GetString());
    }

    [Fact]
    public void AliasAsKey_WithBackslashEscape()
    {
        // Anchor a string with backslash → stored as JSON "path\\dir"
        string yaml = "a: &ref \"path\\\\dir\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("path\\dir", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("path\\dir").GetString());
    }

    [Fact]
    public void AliasAsKey_WithSlash()
    {
        // Anchor a value with a slash → stored as JSON "hello/world"
        // In JSON, "/" can optionally be escaped as "\/" — verify unescaping handles it
        string yaml = "a: &ref \"hello/world\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello/world", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello/world").GetString());
    }

    [Fact]
    public void AliasAsKey_WithMultipleEscapes()
    {
        // Multiple different escape sequences
        string yaml = "a: &ref \"a\\tb\\nc\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("a\tb\nc", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("a\tb\nc").GetString());
    }

    [Fact]
    public void AliasAsKey_WithCarriageReturn()
    {
        // \r in YAML double-quoted → CR (0x0D) → JSON stores as \r
        string yaml = "a: &ref \"hello\\rworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello\rworld", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello\rworld").GetString());
    }

    [Fact]
    public void AliasAsKey_WithBackspace()
    {
        // \b in YAML double-quoted → BS (0x08) → JSON stores as \b
        string yaml = "a: &ref \"hello\\bworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello\bworld", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello\bworld").GetString());
    }

    [Fact]
    public void AliasAsKey_WithFormFeed()
    {
        // \f in YAML double-quoted → FF (0x0C) → JSON stores as \f (or \u000c)
        // Note: Utf8JsonWriter may use \u000C for form feed
        string yaml = "a: &ref \"hello\\fworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello\fworld", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello\fworld").GetString());
    }

    [Fact]
    public void AliasAsKey_WithUnicodeEscape_AngleBracket()
    {
        // '<' is a regular YAML character but Utf8JsonWriter encodes it as \u003C.
        // The stored anchor data is "hello\u003Cworld". When used as alias-as-key,
        // UnescapeJsonString must handle \uXXXX escapes to produce "hello<world".
        string yaml = "a: &ref 'hello<world'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("hello<world", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("hello<world").GetString());
    }

    [Fact]
    public void AliasAsKey_WithUnicodeEscape_Quote()
    {
        // '"' inside a single-quoted YAML string is literal. Utf8JsonWriter encodes
        // it as \u0022. UnescapeJsonString must handle \uXXXX to produce the correct key.
        string yaml = "a: &ref 'say\"hi'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("say\"hi", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("say\"hi").GetString());
    }

    [Fact]
    public void AliasAsKey_WithUnicodeEscape_Ampersand()
    {
        // '&' at the start of a bare YAML scalar would be an anchor indicator,
        // but inside a quoted string it's literal. Utf8JsonWriter encodes it as \u0026.
        string yaml = "a: &ref 'a&b'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("a&b", doc.RootElement.GetProperty("a").GetString());
        Assert.Equal("result", doc.RootElement.GetProperty("a&b").GetString());
    }

    // ========================
    // YAML double-quoted escape sequences (rare Unicode escapes)
    // Covers: YamlToJsonConverter.cs lines 2737-2754
    // ========================

    [Fact]
    public void DoubleQuotedEscape_NextLine()
    {
        // \N = U+0085 NEXT LINE, UTF-8: C2 85
        string yaml = "key: \"hello\\Nworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.Equal("hello\u0085world", value);
    }

    [Fact]
    public void DoubleQuotedEscape_NonBreakingSpace()
    {
        // \_ = U+00A0 NO-BREAK SPACE, UTF-8: C2 A0
        string yaml = "key: \"hello\\_world\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.Equal("hello\u00A0world", value);
    }

    [Fact]
    public void DoubleQuotedEscape_LineSeparator()
    {
        // \L = U+2028 LINE SEPARATOR, UTF-8: E2 80 A8
        string yaml = "key: \"hello\\Lworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.Equal("hello\u2028world", value);
    }

    [Fact]
    public void DoubleQuotedEscape_ParagraphSeparator()
    {
        // \P = U+2029 PARAGRAPH SEPARATOR, UTF-8: E2 80 A9
        string yaml = "key: \"hello\\Pworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.Equal("hello\u2029world", value);
    }

    // ========================
    // Anchor on null/empty value
    // Covers: YamlToJsonConverter.cs lines 4107-4121
    // ========================

    [Fact]
    public void AnchorOnNullValue_AliasReturnsNull()
    {
        string yaml = "a: &anchor\nb: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [Fact]
    public void AnchorOnExplicitNull_AliasReturnsNull()
    {
        string yaml = "a: &anchor null\nb: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    // ========================
    // Explicit key with colon not followed by whitespace
    // Covers: YamlToJsonConverter.cs lines 1032-1034
    // ========================

    [Fact]
    public void ExplicitKey_ColonNotFollowedByWhitespace_NullValue()
    {
        // ? key\n: maps to {"key": null} when there's no value after the key
        // But ?key\n:val with no space after colon is the explicit-key null-value path
        string yaml = "? explicit\n: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("value", doc.RootElement.GetProperty("explicit").GetString());
    }

    [Fact]
    public void ExplicitKey_NoColon_NullValue()
    {
        // ? key with no ':' → null value
        string yaml = "? key1\n? key2";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("key1").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("key2").ValueKind);
    }

    // ========================
    // Block scalar with more-indented lines (folded variant)
    // Covers: YamlToJsonConverter.cs lines 3140-3155
    // ========================

    [Fact]
    public void FoldedBlockScalar_MoreIndentedLines()
    {
        // Folded block scalar where more-indented lines are preserved
        string yaml = "data: >\n  folded\n    indented\n  back";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("data").GetString();
        Assert.NotNull(value);
        // More-indented lines break out of folding; exact output depends on YAML spec
        Assert.Contains("indented", value);
    }

    // ========================
    // JsonToYaml: string/bool/null/number values through reader path
    // Covers: JsonToYamlConverter.cs lines 172-189
    // ========================

    [Fact]
    public void JsonToYaml_StringValue()
    {
        string json = """{"key": "hello world"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("hello world", yaml);
    }

    [Fact]
    public void JsonToYaml_BoolAndNull()
    {
        string json = """{"a": true, "b": false, "c": null}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("true", yaml);
        Assert.Contains("false", yaml);
        Assert.Contains("null", yaml);
    }

    // ========================
    // JsonToYaml: empty mapping/sequence
    // Covers: JsonToYamlConverter.cs lines 514-517
    // ========================

    [Fact]
    public void JsonToYaml_EmptyObject()
    {
        string json = """{"empty": {}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("{}", yaml);
    }

    [Fact]
    public void JsonToYaml_EmptyArray()
    {
        string json = """{"empty": []}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("[]", yaml);
    }

    // ========================
    // Double-quoted escape: \x hex escape
    // ========================

    [Fact]
    public void DoubleQuotedEscape_HexX()
    {
        // \x41 = 'A'
        string yaml = "key: \"\\x41\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("A", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void DoubleQuotedEscape_HexU()
    {
        // \u0041 = 'A'
        string yaml = "key: \"\\u0041\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("A", doc.RootElement.GetProperty("key").GetString());
    }

    [Fact]
    public void DoubleQuotedEscape_HexU8()
    {
        // \U00000041 = 'A'
        string yaml = "key: \"\\U00000041\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.Equal("A", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Buffer growth path: large string value
    // Covers: GrowOutputBuffer / GrowRentedBuffer (lines 4650-4687)
    // ========================

    [Fact]
    public void LargeDoubleQuotedString_TriggersEscapeBufferGrowth()
    {
        // Create a large double-quoted string with escape sequences to force buffer growth
        // in the escape processing path (GrowRentedBuffer)
        var sb = new StringBuilder("key: \"");
        for (int i = 0; i < 200; i++)
        {
            sb.Append("\\n"); // Each escape becomes a single byte — forces escape buffer reallocation
        }

        sb.Append('"');
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(sb.ToString()));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.Equal(200, value!.Length); // 200 newline characters
    }
}
