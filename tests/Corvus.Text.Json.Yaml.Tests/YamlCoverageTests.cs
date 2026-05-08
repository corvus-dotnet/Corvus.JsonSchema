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
using Microsoft.VisualStudio.TestTools.UnitTesting;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Coverage tests targeting uncovered paths in YamlToJsonConverter, ScalarResolver,
/// YamlDocument, and YamlEventParser.
/// </summary>
[TestClass]
public class YamlCoverageTests
{
    // ========================
    // YamlDocument.Parse(string) overloads
    // ========================

    [TestMethod]
    public void ParseString_SimpleMapping()
    {
        string yaml = "name: Alice\nage: 30";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name").GetString());
        Assert.AreEqual(30, root.GetProperty("age").GetInt32());
    }

    [TestMethod]
    public void ParseString_ConvertToJsonString()
    {
        string yaml = "key: value";
        string json = YamlDocument.ConvertToJsonString(yaml);
        StringAssert.Contains(json, "\"key\"");
        StringAssert.Contains(json, "\"value\"");
    }

    [TestMethod]
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
        StringAssert.Contains(json, "\"key0\"");
        StringAssert.Contains(json, "\"key49\"");
    }

    [TestMethod]
    public void ParseBytes_ConvertToJsonString()
    {
        byte[] yaml = Encoding.UTF8.GetBytes("items:\n  - one\n  - two");
        string json = YamlDocument.ConvertToJsonString((ReadOnlyMemory<byte>)yaml);
        StringAssert.Contains(json, "\"one\"");
        StringAssert.Contains(json, "\"two\"");
    }

    // ========================
    // ScalarResolver — Integer paths
    // ========================

    [TestMethod]
    [DataRow("123", 123)]
    [DataRow("-456", -456)]
    [DataRow("0", 0)]
    [DataRow("+789", 789)]
    public void Scalar_Integer_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    // ========================
    // ScalarResolver — Float/scientific paths
    // ========================

    [TestMethod]
    [DataRow("3.14")]
    [DataRow("1.5e10")]
    [DataRow("-2.5E+3")]
    [DataRow("1.0e-5")]
    [DataRow("0.5")]
    public void Scalar_Float_ResolvesAsNumber(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var element = doc.RootElement.GetProperty("value");
        Assert.AreEqual(JsonValueKind.Number, element.ValueKind);
    }

    // ========================
    // ScalarResolver — Hex and octal integers
    // ========================

    [TestMethod]
    [DataRow("0xFF", 255)]
    [DataRow("0x1A", 26)]
    [DataRow("0xCAFE", 0xCAFE)]
    public void Scalar_HexInteger_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    [TestMethod]
    [DataRow("0o77", 63)]
    [DataRow("0o10", 8)]
    [DataRow("0o777", 511)]
    public void Scalar_OctalInteger_ResolvesCorrectly(string scalarValue, int expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetInt32());
    }

    // ========================
    // ScalarResolver — Infinity and NaN → null
    // ========================

    [TestMethod]
    [DataRow(".inf")]
    [DataRow("-.inf")]
    [DataRow("+.inf")]
    [DataRow(".nan")]
    [DataRow(".Inf")]
    [DataRow(".INF")]
    [DataRow(".NaN")]
    [DataRow(".NAN")]
    public void Scalar_InfinityAndNaN_ResolvesAsNull(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Non-numbers that look numeric → string
    // ========================

    [TestMethod]
    [DataRow("0xGG")]
    [DataRow("0o89")]
    [DataRow("1e")]
    [DataRow("1.2.3")]
    [DataRow("+1a")]
    [DataRow("1e+")]
    public void Scalar_InvalidNumeric_ResolvesAsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — YAML 1.1 booleans
    // ========================

    [TestMethod]
    [DataRow("yes", true)]
    [DataRow("Yes", true)]
    [DataRow("YES", true)]
    [DataRow("on", true)]
    [DataRow("On", true)]
    [DataRow("ON", true)]
    [DataRow("y", true)]
    [DataRow("Y", true)]
    [DataRow("no", false)]
    [DataRow("No", false)]
    [DataRow("NO", false)]
    [DataRow("off", false)]
    [DataRow("Off", false)]
    [DataRow("OFF", false)]
    [DataRow("n", false)]
    [DataRow("N", false)]
    public void Scalar_Yaml11Boolean_ResolvesAsBool(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("TRUE", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    [DataRow("FALSE", false)]
    public void Scalar_Yaml11StandardBooleans_ResolvesAsBool(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    // ========================
    // ScalarResolver — YAML 1.1 null values
    // ========================

    [TestMethod]
    [DataRow("null")]
    [DataRow("Null")]
    [DataRow("NULL")]
    [DataRow("~")]
    [DataRow("")]
    public void Scalar_Yaml11Null_ResolvesAsNull(string scalarValue)
    {
        string yaml = scalarValue.Length > 0 ? "value: " + scalarValue : "value:";
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — JSON schema (strict mode)
    // ========================

    [TestMethod]
    [DataRow("null", JsonValueKind.Null)]
    [DataRow("true", JsonValueKind.True)]
    [DataRow("false", JsonValueKind.False)]
    [DataRow("123", JsonValueKind.Number)]
    [DataRow("-45.6", JsonValueKind.Number)]
    [DataRow("1e10", JsonValueKind.Number)]
    public void Scalar_JsonSchema_RecognizesValues(string scalarValue, JsonValueKind expected)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    [DataRow("True")]
    [DataRow("TRUE")]
    [DataRow("False")]
    [DataRow("FALSE")]
    [DataRow("Null")]
    [DataRow("NULL")]
    [DataRow("yes")]
    [DataRow("no")]
    [DataRow("0xFF")]
    [DataRow("0o77")]
    public void Scalar_JsonSchema_RejectsNonJsonValues(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Failsafe schema (everything is string)
    // ========================

    [TestMethod]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("null")]
    [DataRow("123")]
    [DataRow("3.14")]
    public void Scalar_FailsafeSchema_EverythingIsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Failsafe };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Core schema null variants
    // ========================

    [TestMethod]
    [DataRow("null")]
    [DataRow("Null")]
    [DataRow("NULL")]
    [DataRow("~")]
    public void Scalar_CoreNull_ResolvesAsNull(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // ScalarResolver — Core schema bool variants
    // ========================

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("TRUE", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    [DataRow("FALSE", false)]
    public void Scalar_CoreBool_ResolvesCorrectly(string scalarValue, bool expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").GetBoolean());
    }

    // ========================
    // Quoted strings that look like other types
    // ========================

    [TestMethod]
    [DataRow("'true'")]
    [DataRow("'123'")]
    [DataRow("'null'")]
    [DataRow("'3.14'")]
    [DataRow("\"true\"")]
    [DataRow("\"123\"")]
    [DataRow("\"null\"")]
    public void Scalar_QuotedSpecialValue_ResolvesAsString(string scalarValue)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // Special values
    // ========================

    [TestMethod]
    [DataRow("null", JsonValueKind.Null)]
    [DataRow("~", JsonValueKind.Null)]
    [DataRow("true", JsonValueKind.True)]
    [DataRow("false", JsonValueKind.False)]
    public void Scalar_SpecialValues_ResolveCorrectly(string scalarValue, JsonValueKind expected)
    {
        string yaml = "value: " + scalarValue;
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // Multi-document handling
    // ========================

    [TestMethod]
    public void Parse_MultiDocument_MultiAsArray()
    {
        string yaml = "---\nfirst: 1\n---\nsecond: 2\n---\nthird: 3";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetProperty("first").GetInt32());
        Assert.AreEqual(2, root[1].GetProperty("second").GetInt32());
        Assert.AreEqual(3, root[2].GetProperty("third").GetInt32());
    }

    [TestMethod]
    public void Parse_SingleDocument_MultiAsArray_ProducesSingleElementArray()
    {
        string yaml = "---\nkey: value";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual("value", root[0].GetProperty("key").GetString());
    }

    [TestMethod]
    public void Parse_MultiDocument_SingleRequired_Throws()
    {
        string yaml = "---\nfirst: 1\n---\nsecond: 2";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.SingleRequired };
        Assert.ThrowsExactly<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options));
    }

    [TestMethod]
    public void Parse_EmptyDocumentWithMarkers_ReturnsNull()
    {
        string yaml = "---\n...";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.ValueKind);
    }

    [TestMethod]
    public void Parse_EmptyStream_ReturnsNull()
    {
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(string.Empty));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.ValueKind);
    }

    // ========================
    // Alias/anchor resolution
    // ========================

    [TestMethod]
    public void Parse_SimpleAlias_CopiesValue()
    {
        string yaml = "original: &ref hello\ncopy: *ref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual("hello", root.GetProperty("original").GetString());
        Assert.AreEqual("hello", root.GetProperty("copy").GetString());
    }

    [TestMethod]
    public void Parse_AnchorOnMapping_CopiesStructure()
    {
        string yaml = "defaults: &def\n  color: red\n  size: large\nitem:\n  name: widget\n  props: *def";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual("red", root.GetProperty("item").GetProperty("props").GetProperty("color").GetString());
        Assert.AreEqual("large", root.GetProperty("item").GetProperty("props").GetProperty("size").GetString());
    }

    [TestMethod]
    public void Parse_AnchorOnSequence_CopiesArray()
    {
        string yaml = "colors: &colors\n  - red\n  - green\n  - blue\nfavorites: *colors";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty("favorites").ValueKind);
        Assert.AreEqual(3, root.GetProperty("favorites").GetArrayLength());
        Assert.AreEqual("red", root.GetProperty("favorites")[0].GetString());
    }

    [TestMethod]
    public void Parse_MergeKey_ProducesOutput()
    {
        // Merge keys (<<) expand anchor properties into the parent mapping.
        // If merge is not supported by this implementation, the key "<<" will exist as a regular key.
        string yaml = "defaults: &defaults\n  color: red\n  size: large\nitem:\n  <<: *defaults\n  name: widget";
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var item = doc.RootElement.GetProperty("item");
        Assert.AreEqual("widget", item.GetProperty("name").GetString());

        // The implementation may or may not expand merge keys; verify it doesn't crash.
        Assert.AreEqual(JsonValueKind.Object, item.ValueKind);
    }

    // ========================
    // Error handling
    // ========================

    [TestMethod]
    public void Parse_UnclosedFlowSequence_Throws()
    {
        string yaml = "items: [1, 2, 3";
        Assert.ThrowsExactly<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    [TestMethod]
    public void Parse_UnclosedFlowMapping_Throws()
    {
        string yaml = "obj: {key: value";
        Assert.ThrowsExactly<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    [TestMethod]
    public void Parse_DuplicateKey_ErrorMode_Throws()
    {
        string yaml = "key: first\nkey: second";
        var options = new YamlReaderOptions { DuplicateKeyBehavior = DuplicateKeyBehavior.Error };
        Assert.ThrowsExactly<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options));
    }

    [TestMethod]
    public void Parse_DuplicateKey_LastWins()
    {
        string yaml = "key: first\nkey: second";
        var options = new YamlReaderOptions { DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual("second", doc.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public void Parse_InvalidTabIndentation_Throws()
    {
        string yaml = "key:\n\t- value";
        Assert.ThrowsExactly<YamlException>(() =>
            YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml)));
    }

    // ========================
    // Deep nesting
    // ========================

    [TestMethod]
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

        Assert.AreEqual("deep", current.GetProperty("value").GetString());
    }

    // ========================
    // Flow collections
    // ========================

    [TestMethod]
    public void Parse_FlowSequence_Works()
    {
        string yaml = "items: [1, 2, 3, 4, 5]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.AreEqual(JsonValueKind.Array, items.ValueKind);
        Assert.AreEqual(5, items.GetArrayLength());
        Assert.AreEqual(1, items[0].GetInt32());
        Assert.AreEqual(5, items[4].GetInt32());
    }

    [TestMethod]
    public void Parse_FlowMapping_Works()
    {
        string yaml = "person: {name: Alice, age: 30}";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var person = doc.RootElement.GetProperty("person");
        Assert.AreEqual("Alice", person.GetProperty("name").GetString());
        Assert.AreEqual(30, person.GetProperty("age").GetInt32());
    }

    [TestMethod]
    public void Parse_NestedFlowCollections_Works()
    {
        string yaml = "data: [{name: a, items: [1, 2]}, {name: b, items: [3, 4]}]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var data = doc.RootElement.GetProperty("data");
        Assert.AreEqual(JsonValueKind.Array, data.ValueKind);
        Assert.AreEqual(2, data.GetArrayLength());
        Assert.AreEqual("a", data[0].GetProperty("name").GetString());
        Assert.AreEqual(2, data[0].GetProperty("items").GetArrayLength());
        Assert.AreEqual(1, data[0].GetProperty("items")[0].GetInt32());
        Assert.AreEqual("b", data[1].GetProperty("name").GetString());
        Assert.AreEqual(4, data[1].GetProperty("items")[1].GetInt32());
    }

    [TestMethod]
    public void Parse_EmptyFlowSequence_Works()
    {
        string yaml = "items: []";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.AreEqual(JsonValueKind.Array, items.ValueKind);
        Assert.AreEqual(0, items.GetArrayLength());
    }

    [TestMethod]
    public void Parse_EmptyFlowMapping_Works()
    {
        string yaml = "obj: {}";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.GetProperty("obj").ValueKind);
    }

    // ========================
    // Block scalars (literal and folded)
    // ========================

    [TestMethod]
    public void Parse_LiteralBlockScalar_PreservesNewlines()
    {
        string yaml = "text: |\n  line1\n  line2\n  line3\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "line1\nline2\nline3");
    }

    [TestMethod]
    public void Parse_FoldedBlockScalar_FoldsNewlines()
    {
        string yaml = "text: >\n  line1\n  line2\n  line3\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "line1 line2 line3");
    }

    [TestMethod]
    public void Parse_LiteralBlockScalar_StripChomping()
    {
        string yaml = "text: |-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        Assert.AreEqual("line1\nline2", text);
    }

    [TestMethod]
    public void Parse_LiteralBlockScalar_KeepChomping()
    {
        string yaml = "text: |+\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        Assert.IsTrue(text.EndsWith("\n\n"), $"Expected trailing newlines but got: [{text}]");
    }

    [TestMethod]
    public void Parse_FoldedBlockScalar_StripChomping()
    {
        string yaml = "text: >-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        Assert.AreEqual("line1 line2", text);
    }

    [TestMethod]
    public void Parse_LiteralBlockScalar_WithIndentIndicator()
    {
        string yaml = "text: |2\n  hello\n  world\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "hello");
        StringAssert.Contains(text, "world");
    }

    // ========================
    // Document end marker handling
    // ========================

    [TestMethod]
    public void Parse_DocumentEndMarker_Stops()
    {
        string yaml = "key: value\n...";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public void Parse_DocumentStartThenEnd()
    {
        string yaml = "---\nkey: value\n...\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Comments
    // ========================

    [TestMethod]
    public void Parse_InlineComments_Ignored()
    {
        string yaml = "name: Alice # This is a comment\nage: 30 # Another comment";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("Alice", doc.RootElement.GetProperty("name").GetString());
        Assert.AreEqual(30, doc.RootElement.GetProperty("age").GetInt32());
    }

    [TestMethod]
    public void Parse_FullLineComments_Ignored()
    {
        string yaml = "# Header comment\nname: Alice\n# Middle comment\nage: 30\n# Footer";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("Alice", doc.RootElement.GetProperty("name").GetString());
        Assert.AreEqual(30, doc.RootElement.GetProperty("age").GetInt32());
    }

    // ========================
    // Tags
    // ========================

    [TestMethod]
    public void Parse_ExplicitStringTag_ForcesString()
    {
        string yaml = "value: !!str 123";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.AreEqual("123", doc.RootElement.GetProperty("value").GetString());
    }

    [TestMethod]
    public void Parse_ExplicitIntTag_ForcesInt()
    {
        string yaml = "value: !!int 42";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(42, doc.RootElement.GetProperty("value").GetInt32());
    }

    [TestMethod]
    public void Parse_ExplicitNullTag_ForcesNull()
    {
        string yaml = "value: !!null something";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    public void Parse_ExplicitBoolTag_ForcesBool()
    {
        string yaml = "value: !!bool true";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.IsTrue(doc.RootElement.GetProperty("value").GetBoolean());
    }

    [TestMethod]
    public void Parse_ExplicitFloatTag_ForcesFloat()
    {
        string yaml = "value: !!float 3.14";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    public void Parse_NonSpecificTag_ForcesString()
    {
        string yaml = "value: ! true";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.AreEqual("true", doc.RootElement.GetProperty("value").GetString());
    }

    // ========================
    // Mixed content
    // ========================

    [TestMethod]
    public void Parse_MappingWithSequenceValues()
    {
        string yaml = "colors:\n  - red\n  - green\n  - blue\nsizes:\n  - small\n  - large";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual(3, root.GetProperty("colors").GetArrayLength());
        Assert.AreEqual(2, root.GetProperty("sizes").GetArrayLength());
        Assert.AreEqual("red", root.GetProperty("colors")[0].GetString());
        Assert.AreEqual("large", root.GetProperty("sizes")[1].GetString());
    }

    [TestMethod]
    public void Parse_SequenceOfMappings()
    {
        string yaml = "- name: Alice\n  age: 30\n- name: Bob\n  age: 25";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("Alice", root[0].GetProperty("name").GetString());
        Assert.AreEqual(25, root[1].GetProperty("age").GetInt32());
    }

    // ========================
    // Multi-line plain scalars
    // ========================

    [TestMethod]
    public void Parse_MultilinePlainScalar_FoldsNewlines()
    {
        string yaml = "description: This is a\n  long description that\n  spans multiple lines";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? desc = doc.RootElement.GetProperty("description").GetString();
        Assert.IsNotNull(desc);
        StringAssert.Contains(desc, "This is a");
        StringAssert.Contains(desc, "long description");
    }

    // ========================
    // Large numeric values
    // ========================

    [TestMethod]
    public void Parse_LargeInteger_Works()
    {
        string yaml = "value: 9999999999999999";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(9999999999999999L, doc.RootElement.GetProperty("value").GetInt64());
    }

    [TestMethod]
    public void Parse_NegativeLargeInteger_Works()
    {
        string yaml = "value: -9999999999999999";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(-9999999999999999L, doc.RootElement.GetProperty("value").GetInt64());
    }

    // ========================
    // Edge cases
    // ========================

    [TestMethod]
    public void Parse_KeyWithColonInValue_Works()
    {
        string yaml = "url: http://example.com:8080/path";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("http://example.com:8080/path", doc.RootElement.GetProperty("url").GetString());
    }

    [TestMethod]
    public void Parse_EmptySequenceItem_ReturnsNull()
    {
        string yaml = "items:\n  -\n  - value\n  -";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.AreEqual(JsonValueKind.Null, items[0].ValueKind);
        Assert.AreEqual("value", items[1].GetString());
        Assert.AreEqual(JsonValueKind.Null, items[2].ValueKind);
    }

    [TestMethod]
    public void Parse_MappingKeyIsNumber_BecomesStringKey()
    {
        string yaml = "123: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("123").GetString());
    }

    [TestMethod]
    public void Parse_MappingKeyIsBool_BecomesStringKey()
    {
        string yaml = "true: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("true").GetString());
    }

    [TestMethod]
    public void Parse_TabsInInput_Handled()
    {
        string yaml = "key: \"value\\twith\\ttabs\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value\twith\ttabs", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // ConvertToJsonString string overload (tests string→UTF8→parse path)
    // ========================

    [TestMethod]
    public void ConvertToJsonString_String_WithSpecialChars()
    {
        string yaml = "msg: \"hello\\nworld\"";
        string json = YamlDocument.ConvertToJsonString(yaml);
        StringAssert.Contains(json, "hello\\nworld");
    }

    [TestMethod]
    public void ConvertToJsonString_String_WithUnicode()
    {
        string yaml = "emoji: \"\u2764\"";
        string json = YamlDocument.ConvertToJsonString(yaml);
        // The JSON writer may escape non-ASCII to \uXXXX
        Assert.IsTrue(json.Contains("\u2764") || json.Contains("\\u2764"), $"Expected heart emoji in output: {json}");
    }

    // ========================
    // Flow sequence with trailing comma/whitespace
    // ========================

    [TestMethod]
    public void Parse_FlowSequence_WithWhitespace()
    {
        string yaml = "items: [ 1 , 2 , 3 ]";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var items = doc.RootElement.GetProperty("items");
        Assert.AreEqual(3, items.GetArrayLength());
        Assert.AreEqual(1, items[0].GetInt32());
        Assert.AreEqual(3, items[2].GetInt32());
    }

    [TestMethod]
    public void Parse_FlowMapping_WithWhitespace()
    {
        string yaml = "obj: { name : Alice , age : 30 }";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var obj = doc.RootElement.GetProperty("obj");
        Assert.AreEqual("Alice", obj.GetProperty("name").GetString());
        Assert.AreEqual(30, obj.GetProperty("age").GetInt32());
    }

    // ========================
    // IsJsonNumber edge cases (JSON schema)
    // ========================

    [TestMethod]
    [DataRow("0")]
    [DataRow("-0")]
    [DataRow("10")]
    [DataRow("-10")]
    [DataRow("0.1")]
    [DataRow("-0.1")]
    [DataRow("1e5")]
    [DataRow("1E5")]
    [DataRow("1e+5")]
    [DataRow("1e-5")]
    [DataRow("1.5e10")]
    public void JsonSchema_ValidNumbers_ResolveAsNumber(string numValue)
    {
        string yaml = "value: " + numValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    [DataRow("+1")]
    [DataRow("01")]
    [DataRow(".5")]
    [DataRow("1.")]
    [DataRow("1e")]
    [DataRow("1e+")]
    [DataRow("NaN")]
    [DataRow("Infinity")]
    public void JsonSchema_InvalidNumbers_ResolveAsString(string numValue)
    {
        string yaml = "value: " + numValue;
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ========================
    // YAML directives
    // ========================

    [TestMethod]
    public void Parse_YamlDirective_Accepted()
    {
        string yaml = "%YAML 1.2\n---\nkey: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public void Parse_TagDirective_Accepted()
    {
        string yaml = "%TAG !e! tag:example.com,2000:\n---\nkey: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Compact block sequences
    // ========================

    [TestMethod]
    public void Parse_CompactBlockSequenceInMapping()
    {
        string yaml = "people:\n- name: Alice\n- name: Bob";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        var people = doc.RootElement.GetProperty("people");
        Assert.AreEqual(2, people.GetArrayLength());
        Assert.AreEqual("Alice", people[0].GetProperty("name").GetString());
    }

    // ========================
    // Block Scalar Header Parsing
    // ========================

    [TestMethod]
    public void BlockScalar_ChompThenIndent()
    {
        // Header: |-2 (strip chomp then explicit indent 2)
        string yaml = "text: |-2\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("line1\nline2", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void BlockScalar_IndentThenChomp()
    {
        // Header: |2- (explicit indent 2 then strip chomp)
        string yaml = "text: |2-\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("line1\nline2", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void BlockScalar_KeepChompWithIndent()
    {
        // Header: |+2 (keep chomp with explicit indent 2)
        string yaml = "text: |+2\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        // Keep chomp preserves trailing newlines
        Assert.AreEqual("line1\nline2\n\n", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void BlockScalar_HeaderWithComment()
    {
        // Header: | # comment (comment after indicator)
        string yaml = "text: | # comment\n  content here\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("content here\n", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void FoldedScalar_KeepChomp()
    {
        // Header: >+ (folded with keep chomp)
        string yaml = "text: >+\n  line1\n  line2\n\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "line1 line2");
        Assert.EndsWith("\n\n", text!);
    }

    [TestMethod]
    public void FoldedScalar_KeepChompWithIndent()
    {
        // Header: >+2 (folded with keep chomp and explicit indent)
        string yaml = "text: >+2\n  line1\n  line2\n\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        Assert.EndsWith("\n\n", text!);
    }

    [TestMethod]
    public void FoldedScalar_StripChompWithIndent()
    {
        // Header: >-2 (folded with strip chomp and explicit indent)
        string yaml = "text: >-2\n  line1\n  line2\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "line1 line2");
        Assert.IsFalse(text!.EndsWith("\n"));
    }

    // ========================
    // Block Scalar More-Indented Lines
    // ========================

    [TestMethod]
    public void LiteralBlockScalar_MoreIndentedLines()
    {
        // Lines with extra indentation beyond content indent
        string yaml = "text: |\n  normal\n    extra indent\n  back to normal\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("normal\n  extra indent\nback to normal\n", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void LiteralBlockScalar_MoreIndentedWithBlankLines()
    {
        // More-indented line followed by blank line
        string yaml = "text: |\n  normal\n    more\n\n    more2\n  end\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "  more\n\n  more2");
    }

    [TestMethod]
    public void LiteralBlockScalar_MultipleMoreIndented()
    {
        // Multiple consecutive more-indented lines
        string yaml = "text: |\n  a\n    b\n    c\n  d\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("a\n  b\n  c\nd\n", doc.RootElement.GetProperty("text").GetString());
    }

    // ========================
    // Anchor on Null/Empty Value
    // ========================

    [TestMethod]
    public void Anchor_OnNullValue()
    {
        // Anchor on explicit null value
        string yaml = "ref: &myref null\nuse: *myref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("use").ValueKind);
    }

    [TestMethod]
    public void Anchor_OnEmptyValue()
    {
        // Anchor on empty/missing value — the anchor's data length is 0 → store "null"
        string yaml = "ref: &myref\nuse: *myref";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("use").ValueKind);
    }

    // ========================
    // Anchor on Mapping Key (StoreKeyAnchor)
    // ========================

    [TestMethod]
    public void Anchor_OnMappingKey()
    {
        // Anchor on the key itself via explicit key syntax
        string yaml = "? &anchor key\n: value\nother: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        // The anchor stores the key as a JSON string "key"
        Assert.AreEqual("key", doc.RootElement.GetProperty("other").GetString());
    }

    [TestMethod]
    public void Anchor_OnMappingKey_Simple()
    {
        // Anchor on key using inline node property syntax
        string yaml = "&anchor key: value\nother: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("key", doc.RootElement.GetProperty("other").GetString());
    }

    // ========================
    // UnescapeJsonString — Escaped Property Names
    // ========================

    [TestMethod]
    public void PropertyName_WithNewlineEscape()
    {
        // Property names containing \n escape
        string yaml = "\"hello\\nworld\": value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.IsTrue(doc.RootElement.TryGetProperty("hello\nworld", out JsonElement val));
        Assert.AreEqual("value", val.GetString());
    }

    [TestMethod]
    public void PropertyName_WithTabEscape()
    {
        string yaml = "\"col1\\tcol2\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.IsTrue(doc.RootElement.TryGetProperty("col1\tcol2", out JsonElement val));
        Assert.AreEqual("data", val.GetString());
    }

    [TestMethod]
    public void PropertyName_WithBackslashEscape()
    {
        string yaml = "\"path\\\\to\\\\file\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.IsTrue(doc.RootElement.TryGetProperty("path\\to\\file", out JsonElement val));
        Assert.AreEqual("data", val.GetString());
    }

    [TestMethod]
    public void PropertyName_WithQuoteEscape()
    {
        string yaml = "\"say\\\"hi\\\"\": data";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.IsTrue(doc.RootElement.TryGetProperty("say\"hi\"", out JsonElement val));
        Assert.AreEqual("data", val.GetString());
    }

    // ========================
    // Double-Quoted Unicode Escapes
    // ========================

    [TestMethod]
    public void DoubleQuoted_UnicodeEscape_2Byte()
    {
        // \u00E9 = é (2-byte UTF-8)
        string yaml = "text: \"caf\\u00e9\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("café", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void DoubleQuoted_UnicodeEscape_3Byte()
    {
        // \u2603 = ☃ (3-byte UTF-8)
        string yaml = "text: \"snow\\u2603man\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("snow☃man", doc.RootElement.GetProperty("text").GetString());
    }

    [TestMethod]
    public void DoubleQuoted_UnicodeEscape_NullChar()
    {
        // \x00 - null control character
        string yaml = "text: \"null\\x00here\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? text = doc.RootElement.GetProperty("text").GetString();
        Assert.IsNotNull(text);
        StringAssert.Contains(text, "\0");
    }

    // ========================
    // Multi-Document with Different Types
    // ========================

    [TestMethod]
    public void MultiDocument_MixedTypes()
    {
        // Different value types in each document using MultiAsArray mode
        string yaml = "---\n42\n---\ntrue\n---\nhello\n---\nnull\n...";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(42, root[0].GetInt32());
        Assert.IsTrue(root[1].GetBoolean());
        Assert.AreEqual("hello", root[2].GetString());
        Assert.AreEqual(JsonValueKind.Null, root[3].ValueKind);
    }

    [TestMethod]
    public void MultiDocument_SingleDocument_InMultiMode()
    {
        // Single document in multi mode produces one-element array
        string yaml = "---\nkey: value\n...";
        var options = new YamlReaderOptions { DocumentMode = YamlDocumentMode.MultiAsArray };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        var root = doc.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual("value", root[0].GetProperty("key").GetString());
    }

    // ========================
    // Large Buffer Growth
    // ========================

    [TestMethod]
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

        Assert.AreEqual(500, count);
    }

    // ========================
    // Scalar Resolver edge cases
    // ========================

    [TestMethod]
    public void Scalar_LeadingZeroInteger_JsonSchema_IsString()
    {
        // "007" leading zeros are not valid JSON numbers
        string yaml = "value: 007";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
        Assert.AreEqual("007", doc.RootElement.GetProperty("value").GetString());
    }

    [TestMethod]
    public void Scalar_PlusSign_JsonSchema_IsString()
    {
        // "+5" is not a valid JSON number (leading plus)
        string yaml = "value: +5";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    public void Scalar_DotLeadingDecimal_JsonSchema_IsString()
    {
        // ".5" is not a valid JSON number (missing leading digit)
        string yaml = "value: .5";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    [TestMethod]
    public void Scalar_TrailingDot_JsonSchema_IsString()
    {
        // "1." is not a valid JSON number (trailing dot)
        string yaml = "value: \"1.\"";
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml), options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("value").ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Block Scalar Headers (lines 3047-3079) in ReadBlockScalarContent
    // These lines are in the EXPLICIT KEY path (? | syntax), not value path.
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExplicitKeyBlockScalar_StripChomp()
    {
        // ? |- means explicit key with block literal, strip chomp
        string yaml = "? |-\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_KeepChomp()
    {
        // ? |+ means explicit key with block literal, keep chomp
        // With keep chomp, trailing newlines are preserved.
        // Content "mykey\n" + trailing "\n" = "mykey\n\n"
        string yaml = "? |+\n  mykey\n\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey\n\n").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_ExplicitIndent()
    {
        // ? |2 means explicit key with block literal, indent 2
        string yaml = "? |2\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey\n").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_IndentWithChomp()
    {
        // ? |2- means explicit key with indent 2, strip chomp
        string yaml = "? |2-\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_CommentAfter()
    {
        // ? | # comment — comment after block indicator in key
        string yaml = "? | # this is a comment\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey\n").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_ChompThenIndent()
    {
        // ? |-2 means strip chomp, explicit indent 2
        string yaml = "? |-2\n  mykey\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey").GetString());
    }

    [TestMethod]
    public void ExplicitKeyBlockScalar_FoldedWithKeepChomp()
    {
        // ? >+ means folded with keep chomp — trailing newlines preserved
        string yaml = "? >+\n  mykey\n\n: value\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("mykey\n\n").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Anchor on null value (lines 4107-4121)
    // NOTE: This else branch fires when dataLength == 0, meaning the JSON
    // writer produced no output between anchor start and end. This appears
    // to be defensive code — every node type writes at least "null", '""',
    // or a JSON value, so dataLength is always > 0 in practice.
    // Tests below verify anchor-on-null via the NORMAL path (dataLength > 0).
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Anchor_OnNullValue_ThenAlias()
    {
        // &ref null — anchor on null value, then alias references it
        string yaml = "a: &ref null\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [TestMethod]
    public void Anchor_OnEmptyValue_ThenAlias()
    {
        // &ref with no value (empty scalar = null)
        string yaml = "a: &ref\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [TestMethod]
    public void Anchor_OnTildeNull_ThenAlias()
    {
        // &ref ~ (tilde = null in YAML)
        string yaml = "a: &ref ~\nb: *ref\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // StoreKeyAnchor (lines 4132-4195) — anchor on mapping key
    // Lines 4170-4172: backslash in key → escaped as \\
    // Lines 4175-4182: control char in key → escaped as \uXXXX
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void Anchor_OnMappingKey_ThenAlias()
    {
        // Anchor on a key: &ref key: value → alias *ref resolves to "key"
        string yaml = "&anchor key: value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("key").GetString());
        Assert.AreEqual("key", doc.RootElement.GetProperty("ref").GetString());
    }

    [TestMethod]
    public void Anchor_OnMappingKey_WithBackslash()
    {
        // Key with literal backslash: YAML "path\\dir" → bytes path\dir
        // StoreKeyAnchor must escape \ as \\
        string yaml = "&anchor \"path\\\\dir\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("path\\dir").GetString());
        Assert.AreEqual("path\\dir", doc.RootElement.GetProperty("ref").GetString());
    }

    [TestMethod]
    public void Anchor_OnMappingKey_WithControlChar()
    {
        // Key with control char: YAML "tab\there" → bytes tab<TAB>here (0x09)
        // StoreKeyAnchor must escape 0x09 as \u0009
        string yaml = "&anchor \"tab\\there\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("tab\there").GetString());
        Assert.AreEqual("tab\there", doc.RootElement.GetProperty("ref").GetString());
    }

    [TestMethod]
    public void Anchor_OnMappingKey_WithQuote()
    {
        // Key with double quote: YAML "say\"hi" → bytes say"hi
        // StoreKeyAnchor must escape " as \"
        string yaml = "&anchor \"say\\\"hi\": value\nref: *anchor\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("say\"hi").GetString());
        Assert.AreEqual("say\"hi", doc.RootElement.GetProperty("ref").GetString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UnescapeJsonString (lines 4316-4354)
    // Called from WriteAliasAsPropertyName when stored anchor data is a
    // JSON string containing backslash escapes. Must use alias as key.
    // NOTE: Alias names terminate at whitespace, so space before ':' needed.
    // ═══════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void AliasAsKey_WithTabEscape()
    {
        // Anchor a string with tab → stored as JSON "hello\tworld"
        // Using *ref as a property key triggers WriteAliasAsPropertyName → UnescapeJsonString
        string yaml = "a: &ref \"hello\\tworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello\tworld", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello\tworld").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithNewlineEscape()
    {
        // Anchor a string with newline → stored as JSON "line\nbreak"
        string yaml = "a: &ref \"line\\nbreak\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("line\nbreak", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("line\nbreak").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithBackslashEscape()
    {
        // Anchor a string with backslash → stored as JSON "path\\dir"
        string yaml = "a: &ref \"path\\\\dir\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("path\\dir", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("path\\dir").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithSlash()
    {
        // Anchor a value with a slash → stored as JSON "hello/world"
        // In JSON, "/" can optionally be escaped as "\/" — verify unescaping handles it
        string yaml = "a: &ref \"hello/world\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello/world", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello/world").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithMultipleEscapes()
    {
        // Multiple different escape sequences
        string yaml = "a: &ref \"a\\tb\\nc\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("a\tb\nc", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("a\tb\nc").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithCarriageReturn()
    {
        // \r in YAML double-quoted → CR (0x0D) → JSON stores as \r
        string yaml = "a: &ref \"hello\\rworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello\rworld", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello\rworld").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithBackspace()
    {
        // \b in YAML double-quoted → BS (0x08) → JSON stores as \b
        string yaml = "a: &ref \"hello\\bworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello\bworld", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello\bworld").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithFormFeed()
    {
        // \f in YAML double-quoted → FF (0x0C) → JSON stores as \f (or \u000c)
        // Note: Utf8JsonWriter may use \u000C for form feed
        string yaml = "a: &ref \"hello\\fworld\"\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello\fworld", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello\fworld").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithUnicodeEscape_AngleBracket()
    {
        // '<' is a regular YAML character but Utf8JsonWriter encodes it as \u003C.
        // The stored anchor data is "hello\u003Cworld". When used as alias-as-key,
        // UnescapeJsonString must handle \uXXXX escapes to produce "hello<world".
        string yaml = "a: &ref 'hello<world'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("hello<world", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("hello<world").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithUnicodeEscape_Quote()
    {
        // '"' inside a single-quoted YAML string is literal. Utf8JsonWriter encodes
        // it as \u0022. UnescapeJsonString must handle \uXXXX to produce the correct key.
        string yaml = "a: &ref 'say\"hi'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("say\"hi", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("say\"hi").GetString());
    }

    [TestMethod]
    public void AliasAsKey_WithUnicodeEscape_Ampersand()
    {
        // '&' at the start of a bare YAML scalar would be an anchor indicator,
        // but inside a quoted string it's literal. Utf8JsonWriter encodes it as \u0026.
        string yaml = "a: &ref 'a&b'\n*ref : result\n";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("a&b", doc.RootElement.GetProperty("a").GetString());
        Assert.AreEqual("result", doc.RootElement.GetProperty("a&b").GetString());
    }

    // ========================
    // YAML double-quoted escape sequences (rare Unicode escapes)
    // Covers: YamlToJsonConverter.cs lines 2737-2754
    // ========================

    [TestMethod]
    public void DoubleQuotedEscape_NextLine()
    {
        // \N = U+0085 NEXT LINE, UTF-8: C2 85
        string yaml = "key: \"hello\\Nworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.AreEqual("hello\u0085world", value);
    }

    [TestMethod]
    public void DoubleQuotedEscape_NonBreakingSpace()
    {
        // \_ = U+00A0 NO-BREAK SPACE, UTF-8: C2 A0
        string yaml = "key: \"hello\\_world\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.AreEqual("hello\u00A0world", value);
    }

    [TestMethod]
    public void DoubleQuotedEscape_LineSeparator()
    {
        // \L = U+2028 LINE SEPARATOR, UTF-8: E2 80 A8
        string yaml = "key: \"hello\\Lworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.AreEqual("hello\u2028world", value);
    }

    [TestMethod]
    public void DoubleQuotedEscape_ParagraphSeparator()
    {
        // \P = U+2029 PARAGRAPH SEPARATOR, UTF-8: E2 80 A9
        string yaml = "key: \"hello\\Pworld\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("key").GetString();
        Assert.AreEqual("hello\u2029world", value);
    }

    // ========================
    // Anchor on null/empty value
    // Covers: YamlToJsonConverter.cs lines 4107-4121
    // ========================

    [TestMethod]
    public void AnchorOnNullValue_AliasReturnsNull()
    {
        string yaml = "a: &anchor\nb: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    [TestMethod]
    public void AnchorOnExplicitNull_AliasReturnsNull()
    {
        string yaml = "a: &anchor null\nb: *anchor";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b").ValueKind);
    }

    // ========================
    // Explicit key with colon not followed by whitespace
    // Covers: YamlToJsonConverter.cs lines 1032-1034
    // ========================

    [TestMethod]
    public void ExplicitKey_ColonNotFollowedByWhitespace_NullValue()
    {
        // ? key\n: maps to {"key": null} when there's no value after the key
        // But ?key\n:val with no space after colon is the explicit-key null-value path
        string yaml = "? explicit\n: value";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("value", doc.RootElement.GetProperty("explicit").GetString());
    }

    [TestMethod]
    public void ExplicitKey_NoColon_NullValue()
    {
        // ? key with no ':' → null value
        string yaml = "? key1\n? key2";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key1").ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key2").ValueKind);
    }

    // ========================
    // Block scalar with more-indented lines (folded variant)
    // Covers: YamlToJsonConverter.cs lines 3140-3155
    // ========================

    [TestMethod]
    public void FoldedBlockScalar_MoreIndentedLines()
    {
        // Folded block scalar where more-indented lines are preserved
        string yaml = "data: >\n  folded\n    indented\n  back";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        string? value = doc.RootElement.GetProperty("data").GetString();
        Assert.IsNotNull(value);
        // More-indented lines break out of folding; exact output depends on YAML spec
        StringAssert.Contains(value, "indented");
    }

    // ========================
    // JsonToYaml: string/bool/null/number values through reader path
    // Covers: JsonToYamlConverter.cs lines 172-189
    // ========================

    [TestMethod]
    public void JsonToYaml_StringValue()
    {
        string json = """{"key": "hello world"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        StringAssert.Contains(yaml, "hello world");
    }

    [TestMethod]
    public void JsonToYaml_BoolAndNull()
    {
        string json = """{"a": true, "b": false, "c": null}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        StringAssert.Contains(yaml, "true");
        StringAssert.Contains(yaml, "false");
        StringAssert.Contains(yaml, "null");
    }

    // ========================
    // JsonToYaml: empty mapping/sequence
    // Covers: JsonToYamlConverter.cs lines 514-517
    // ========================

    [TestMethod]
    public void JsonToYaml_EmptyObject()
    {
        string json = """{"empty": {}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        StringAssert.Contains(yaml, "{}");
    }

    [TestMethod]
    public void JsonToYaml_EmptyArray()
    {
        string json = """{"empty": []}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        StringAssert.Contains(yaml, "[]");
    }

    // ========================
    // Double-quoted escape: \x hex escape
    // ========================

    [TestMethod]
    public void DoubleQuotedEscape_HexX()
    {
        // \x41 = 'A'
        string yaml = "key: \"\\x41\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("A", doc.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public void DoubleQuotedEscape_HexU()
    {
        // \u0041 = 'A'
        string yaml = "key: \"\\u0041\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("A", doc.RootElement.GetProperty("key").GetString());
    }

    [TestMethod]
    public void DoubleQuotedEscape_HexU8()
    {
        // \U00000041 = 'A'
        string yaml = "key: \"\\U00000041\"";
        using var doc = YamlTestHelper.Parse(Encoding.UTF8.GetBytes(yaml));
        Assert.AreEqual("A", doc.RootElement.GetProperty("key").GetString());
    }

    // ========================
    // Buffer growth path: large string value
    // Covers: GrowOutputBuffer / GrowRentedBuffer (lines 4650-4687)
    // ========================

    [TestMethod]
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
        Assert.AreEqual(200, value!.Length); // 200 newline characters
    }
}
