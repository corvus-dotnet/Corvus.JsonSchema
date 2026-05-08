// <copyright file="JsonToYamlTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
#if STJ
using System.Text.Json;
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
/// Tests for JSON→YAML conversion (all three conversion paths and round-trip).
/// </summary>
[TestClass]
public class JsonToYamlTests
{
    // ===================================================================
    // Category 1: Basic scalar conversions
    // ===================================================================

    [TestMethod]
    [DataRow("""{"key": "hello"}""", "key: hello")]
    [DataRow("""{"key": 42}""", "key: 42")]
    [DataRow("""{"key": 3.14}""", "key: 3.14")]
    [DataRow("""{"key": true}""", "key: true")]
    [DataRow("""{"key": false}""", "key: false")]
    [DataRow("""{"key": null}""", "key: null")]
    public void BasicScalarValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    [TestMethod]
    [DataRow("""42""", "42")]
    [DataRow("""3.14""", "3.14")]
    [DataRow("""true""", "true")]
    [DataRow("""false""", "false")]
    [DataRow("""null""", "null")]
    public void BareScalarRootValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 2: Number handling (use "num" key to avoid "n" quoting)
    // ===================================================================

    [TestMethod]
    [DataRow("""{"num": 0}""", "num: 0")]
    [DataRow("""{"num": -42}""", "num: -42")]
    [DataRow("""{"num": 9223372036854775807}""", "num: 9223372036854775807")]
    [DataRow("""{"num": 1.23e10}""", "num: 1.23e10")]
    [DataRow("""{"num": 1.23e-10}""", "num: 1.23e-10")]
    public void NumericValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 3: Empty collections
    // ===================================================================

    [TestMethod]
    public void EmptyObject()
    {
        string yaml = YamlDocument.ConvertToYamlString("{}");
        Assert.AreEqual("{}", yaml);
    }

    [TestMethod]
    public void EmptyArray()
    {
        string yaml = YamlDocument.ConvertToYamlString("[]");
        Assert.AreEqual("[]", yaml);
    }

    [TestMethod]
    public void NestedEmptyCollections()
    {
        string yaml = YamlDocument.ConvertToYamlString("""{"obj": {}, "arr": []}""");
        Assert.AreEqual("obj: {}\narr: []", yaml);
    }

    [TestMethod]
    public void ArrayOfEmptyObjects()
    {
        string yaml = YamlDocument.ConvertToYamlString("[{}, {}, {}]");
        Assert.AreEqual("- {}\n- {}\n- {}", yaml);
    }

    // ===================================================================
    // Category 4: Nested structures
    // ===================================================================

    [TestMethod]
    public void NestedMappings()
    {
        string json = """{"a": {"b": {"c": 1}}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("a:\n  b:\n    c: 1", yaml);
    }

    [TestMethod]
    public void NestedSequences()
    {
        string json = "[[1, 2], [3, 4]]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        // Sequence-in-sequence: "- " prefix, then children on next line indented
        Assert.AreEqual("- \n  - 1\n  - 2\n- \n  - 3\n  - 4", yaml);
    }

    [TestMethod]
    public void MappingWithSequenceValue()
    {
        string json = """{"items": [1, 2, 3]}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("items:\n  - 1\n  - 2\n  - 3", yaml);
    }

    [TestMethod]
    public void SequenceOfMappings()
    {
        string json = """[{"a": 1}, {"b": 2}]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        // Mapping-in-sequence: "- " prefix, then first property on next line
        Assert.AreEqual("- \n  a: 1\n- \n  b: 2", yaml);
    }

    [TestMethod]
    public void ComplexMixedNesting()
    {
        string json = """{"people": [{"name": "Alice", "age": 30}, {"name": "Bob", "age": 25}]}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(
            "people:\n  - \n    name: Alice\n    age: 30\n  - \n    name: Bob\n    age: 25",
            yaml);
    }

    // ===================================================================
    // Category 5: Scalar quoting — strings that must be quoted to round-trip
    // ===================================================================

    [TestMethod]
    [DataRow("""{"k": "true"}""", "k: \"true\"")] // looks like bool
    [DataRow("""{"k": "false"}""", "k: \"false\"")] // looks like bool
    [DataRow("""{"k": "null"}""", "k: \"null\"")] // looks like null
    [DataRow("""{"k": "~"}""", "k: \"~\"")] // tilde = null in YAML
    [DataRow("""{"k": "yes"}""", "k: \"yes\"")] // YAML 1.1 bool
    [DataRow("""{"k": "no"}""", "k: \"no\"")] // YAML 1.1 bool
    [DataRow("""{"k": "on"}""", "k: \"on\"")] // YAML 1.1 bool
    [DataRow("""{"k": "off"}""", "k: \"off\"")] // YAML 1.1 bool
    [DataRow("""{"k": "True"}""", "k: \"True\"")] // case variant
    [DataRow("""{"k": "FALSE"}""", "k: \"FALSE\"")] // case variant
    [DataRow("""{"k": "NULL"}""", "k: \"NULL\"")] // case variant
    [DataRow("""{"k": "Yes"}""", "k: \"Yes\"")] // case variant
    [DataRow("""{"k": "NO"}""", "k: \"NO\"")] // case variant
    public void StringsLookingLikeBoolOrNull_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    [TestMethod]
    [DataRow("""{"k": "123"}""", "k: \"123\"")] // looks like int
    [DataRow("""{"k": "3.14"}""", "k: \"3.14\"")] // looks like float
    [DataRow("""{"k": ".inf"}""", "k: \".inf\"")] // looks like YAML infinity
    [DataRow("""{"k": "-.inf"}""", "k: \"-.inf\"")] // looks like YAML -infinity
    [DataRow("""{"k": ".nan"}""", "k: \".nan\"")] // looks like YAML NaN
    [DataRow("""{"k": ".Inf"}""", "k: \".Inf\"")] // case variant
    [DataRow("""{"k": ".NaN"}""", "k: \".NaN\"")] // case variant
    [DataRow("""{"k": "0x1A"}""", "k: \"0x1A\"")] // looks like hex int
    [DataRow("""{"k": "0o17"}""", "k: \"0o17\"")] // looks like octal int
    public void StringsLookingLikeNumbers_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    [TestMethod]
    [DataRow("""{"k": ""}""", "k: \"\"")] // empty string
    [DataRow("""{"k": "  value"}""", "k: \"  value\"")] // leading spaces
    [DataRow("""{"k": "value  "}""", "k: \"value  \"")] // trailing spaces
    [DataRow("""{"k": "hello: world"}""", "k: \"hello: world\"")] // colon+space
    [DataRow("""{"k": "value #comment"}""", "k: \"value #comment\"")] // space+hash
    [DataRow("""{"k": "- item"}""", "k: \"- item\"")] // leading dash+space
    [DataRow("""{"k": "? key"}""", "k: \"? key\"")] // leading question+space
    [DataRow("""{"k": "&anchor"}""", "k: \"&anchor\"")] // anchor indicator
    [DataRow("""{"k": "*alias"}""", "k: \"*alias\"")] // alias indicator
    [DataRow("""{"k": "!tag"}""", "k: \"!tag\"")] // tag indicator
    [DataRow("""{"k": "{flow}"}""", "k: \"{flow}\"")] // flow mapping indicator
    [DataRow("""{"k": "[flow]"}""", "k: \"[flow]\"")] // flow sequence indicator
    [DataRow("""{"k": "%directive"}""", "k: \"%directive\"")] // directive indicator
    [DataRow("""{"k": "@reserved"}""", "k: \"@reserved\"")] // reserved indicator
    [DataRow("""{"k": "`backtick"}""", "k: \"`backtick\"")] // reserved indicator
    public void StringsWithSpecialCharacters_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 6: Escape sequences in double-quoted strings
    // ===================================================================

    [TestMethod]
    public void StringWithNewline_IsDoubleQuotedWithEscape()
    {
        string json = """{"k": "line1\nline2"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("k: \"line1\\nline2\"", yaml);
    }

    [TestMethod]
    public void StringWithCarriageReturn_IsDoubleQuotedWithEscape()
    {
        string json = "{\"k\": \"a\\rb\"}";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("k: \"a\\rb\"", yaml);
    }

    // Strings with only embedded quotes, backslash, or tab are NOT double-quoted
    // because those characters are safe in YAML plain scalars
    [TestMethod]
    public void StringWithEmbeddedQuotes_IsPlainScalar()
    {
        string json = """{"k": "say \"hi\""}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("k: say \"hi\"", yaml);
    }

    [TestMethod]
    public void StringWithBackslash_IsPlainScalar()
    {
        string json = """{"k": "c:\\path"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("k: c:\\path", yaml);
    }

    [TestMethod]
    public void StringWithTab_IsPlainScalar()
    {
        // Tab is safe in YAML plain scalars — it passes through as a literal byte
        string json = """{"k": "col1\tcol2"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("k: col1\tcol2", yaml);
    }

    // ===================================================================
    // Category 7: Empty string as key
    // ===================================================================

    [TestMethod]
    public void EmptyStringKey()
    {
        string json = """{"": "value"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("\"\": value", yaml);
    }

    // ===================================================================
    // Category 8: IndentSize options
    // ===================================================================

    [TestMethod]
    [DataRow(2, "a:\n  b: 1")]
    [DataRow(4, "a:\n    b: 1")]
    [DataRow(1, "a:\n b: 1")]
    public void IndentSizeOption(int indentSize, string expectedYaml)
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = indentSize });
        Assert.AreEqual(expectedYaml, yaml);
    }

    [TestMethod]
    public void DefaultOptionsUsesTwoSpaceIndent()
    {
        // Verifies that default(YamlWriterOptions) with IndentSize=0 is treated as 2
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual("a:\n  b: 1", yaml);
    }

    // ===================================================================
    // Category 9: Conversion path equivalence — string, UTF-8, JsonElement
    // ===================================================================

    [TestMethod]
    [DataRow("""{"a": 1, "b": [2, 3], "c": {"d": true}}""")]
    [DataRow("""[1, "two", null, false, {"nested": []}]""")]
    [DataRow("""{"reserved": "true", "num": "123", "empty": ""}""")]
    public void AllConversionPathsProduceIdenticalOutput(string json)
    {
        // Path 1: string
        string fromString = YamlDocument.ConvertToYamlString(json);

        // Path 2: UTF-8 bytes
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        string fromUtf8 = YamlDocument.ConvertToYamlString((ReadOnlySpan<byte>)utf8);

        // Path 3: JsonElement
#if STJ
        using JsonDocument doc = JsonDocument.Parse(json);
        string fromElement = YamlDocument.ConvertToYamlString(doc.RootElement);
#else
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        string fromElement = YamlDocument.ConvertToYamlString(doc.RootElement);
#endif

        Assert.AreEqual(fromString, fromUtf8);
        Assert.AreEqual(fromString, fromElement);
    }

    // ===================================================================
    // Category 10: Stream and buffer writer output paths
    // ===================================================================

    [TestMethod]
    public void ConvertToYaml_WritesToStream()
    {
        string json = """{"name": "test", "value": 42}""";
        using MemoryStream stream = new();

        YamlDocument.ConvertToYaml(json, stream);

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        Assert.AreEqual("name: test\nvalue: 42", yaml);
    }

    [TestMethod]
    public void ConvertToYaml_WritesToBufferWriter()
    {
        string json = """{"name": "test", "value": 42}""";
        ArrayBufferWriter<byte> writer = new();

        YamlDocument.ConvertToYaml(json, writer);

#if NET
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan.ToArray());
#endif
        Assert.AreEqual("name: test\nvalue: 42", yaml);
    }

    // ===================================================================
    // Category 11: Round-trip (JSON → YAML → JSON)
    // ===================================================================

    [TestMethod]
    [DataRow("""{"a": 1}""")]
    [DataRow("""{"a": 1, "b": "hello", "c": true, "d": null}""")]
    [DataRow("""[1, 2, 3]""")]
    [DataRow("""{"nested": {"deep": {"value": 42}}}""")]
    [DataRow("""{"arr": [{"a": 1}, {"b": 2}]}""")]
    [DataRow("""{"empty_obj": {}, "empty_arr": []}""")]
    public void RoundTrip_PreservesStructure(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    [DataRow("""{"k": "true"}""")] // string "true" must stay string, not become bool
    [DataRow("""{"k": "false"}""")]
    [DataRow("""{"k": "null"}""")] // string "null" must stay string, not become null
    [DataRow("""{"k": "123"}""")] // string "123" must stay string, not become number
    [DataRow("""{"k": "3.14"}""")]
    [DataRow("""{"k": "yes"}""")]
    [DataRow("""{"k": "no"}""")]
    [DataRow("""{"k": "~"}""")]
    [DataRow("""{"k": ""}""")]
    [DataRow("""{"k": ".inf"}""")]
    [DataRow("""{"k": ".nan"}""")]
    public void RoundTrip_PreservesStringTypes(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    [DataRow("""{"k": "hello: world"}""")]
    [DataRow("""{"k": "value #comment"}""")]
    [DataRow("""{"k": "- item"}""")]
    [DataRow("""{"k": "  leading"}""")]
    [DataRow("""{"k": "trailing  "}""")]
    [DataRow("""{"k": "&anchor"}""")]
    [DataRow("""{"k": "*alias"}""")]
    [DataRow("""{"k": "line1\nline2"}""")]
    [DataRow("""{"k": "col1\tcol2"}""")]
    [DataRow("""{"k": "say \"hi\""}""")]
    [DataRow("""{"k": "back\\slash"}""")]
    public void RoundTrip_PreservesSpecialStrings(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    public void RoundTrip_AllReservedWords()
    {
        string json = """{"yes": "yes", "no": "no", "true": "true", "false": "false", "null": "null", "tilde": "~", "on": "on", "off": "off"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    public void RoundTrip_MixedTypes()
    {
        string json = """{"str": "hello", "num": 42, "float": 3.25, "bool": true, "nil": null, "arr": [1, "two"], "obj": {"inner": true}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 12: Unicode handling
    // ===================================================================

    [TestMethod]
    [DataRow("""{"k": "你好"}""", "k: 你好")]
    [DataRow("""{"k": "שלום"}""", "k: שלום")]
    public void UnicodeStrings(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    [TestMethod]
    public void RoundTrip_UnicodeStrings()
    {
        string json = """{"emoji": "hello 🌍", "cjk": "你好世界"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 13: Large and complex documents
    // ===================================================================

    [TestMethod]
    public void LargeArray()
    {
        StringBuilder sb = new("[");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append(i);
        }

        sb.Append(']');
        string json = sb.ToString();

        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    public void LargeObject()
    {
        StringBuilder sb = new("{");
        for (int i = 0; i < 100; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"key{i}\": {i}");
        }

        sb.Append('}');
        string json = sb.ToString();

        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 14: Key quoting for reserved single-char keys
    // ===================================================================

    [TestMethod]
    [DataRow("""{"n": 1}""", "\"n\": 1")]
    [DataRow("""{"y": 1}""", "\"y\": 1")]
    [DataRow("""{"N": 1}""", "\"N\": 1")]
    [DataRow("""{"Y": 1}""", "\"Y\": 1")]
    public void SingleCharReservedKeys_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 15: Deep nesting (beyond initial stack capacity)
    // ===================================================================

    [TestMethod]
    [DataRow(5)]
    [DataRow(16)]
    [DataRow(20)]  // Beyond MaxStackDepth initial capacity
    [DataRow(32)]
    public void DeeplyNestedObjects_RoundTrip(int depth)
    {
        // Build JSON: {"a":{"a":{"a":...1...}}}
        StringBuilder sb = new();
        for (int i = 0; i < depth; i++)
        {
            sb.Append("{\"a\":");
        }

        sb.Append('1');
        for (int i = 0; i < depth; i++)
        {
            sb.Append('}');
        }

        string json = sb.ToString();
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);

        // Verify correct indentation depth
        string[] lines = yaml.Split('\n');
        Assert.AreEqual(depth, lines.Length);
        for (int i = 0; i < depth - 1; i++)
        {
            Assert.StartsWith(new string(' ', i * 2) + "a:", lines[i]);
        }

        // Last line is the value
        Assert.StartsWith(new string(' ', (depth - 1) * 2) + "a: 1", lines[depth - 1]);
    }

    [TestMethod]
    [DataRow(5)]
    [DataRow(20)]
    public void DeeplyNestedArrays_RoundTrip(int depth)
    {
        // Build JSON: [[[...1...]]]
        StringBuilder sb = new();
        for (int i = 0; i < depth; i++)
        {
            sb.Append('[');
        }

        sb.Append('1');
        for (int i = 0; i < depth; i++)
        {
            sb.Append(']');
        }

        string json = sb.ToString();
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 16: Element overloads
    // ===================================================================

    [TestMethod]
    [DataRow("""{"a": 1, "b": [2, 3]}""")]
    [DataRow("""[1, "two", null]""")]
    [DataRow("""{"nested": {"deep": true}}""")]
    public void ElementOverload_ProducesSameResult(string json)
    {
        string fromString = YamlDocument.ConvertToYamlString(json);

#if STJ
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        string fromElement = YamlDocument.ConvertToYamlString(doc.RootElement);
#else
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        string fromElement = YamlDocument.ConvertToYamlString(doc.RootElement);
#endif

        Assert.AreEqual(fromString, fromElement);
    }

    [TestMethod]
    [DataRow("""{"a": 1, "b": [2, 3]}""")]
    [DataRow("""[1, "two", null]""")]
    public void ElementOverload_WritesToStream(string json)
    {
#if STJ
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        using MemoryStream stream = new();
        YamlDocument.ConvertToYaml(doc.RootElement, stream);
#else
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        using MemoryStream stream = new();
        YamlDocument.ConvertToYaml(doc.RootElement, stream);
#endif

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        string expected = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("""{"a": 1, "b": [2, 3]}""")]
    [DataRow("""[1, "two", null]""")]
    public void ElementOverload_WritesToBufferWriter(string json)
    {
#if STJ
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        ArrayBufferWriter<byte> writer = new();
        YamlDocument.ConvertToYaml(doc.RootElement, writer);
#else
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        ArrayBufferWriter<byte> writer = new();
        YamlDocument.ConvertToYaml(doc.RootElement, writer);
#endif

#if NET
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan.ToArray());
#endif
        string expected = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expected, yaml);
    }

    // ===================================================================
    // Category 17: UTF-8 byte overloads with Stream/IBufferWriter
    // ===================================================================

    [TestMethod]
    [DataRow("""{"a": 1, "b": "hello"}""")]
    public void Utf8Overload_WritesToStream(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        using MemoryStream stream = new();

        YamlDocument.ConvertToYaml((ReadOnlySpan<byte>)utf8, stream);

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        string expected = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expected, yaml);
    }

    [TestMethod]
    [DataRow("""{"a": 1, "b": "hello"}""")]
    public void Utf8Overload_WritesToBufferWriter(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        ArrayBufferWriter<byte> writer = new();

        YamlDocument.ConvertToYaml((ReadOnlySpan<byte>)utf8, writer);

#if NET
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan);
#else
        string yaml = Encoding.UTF8.GetString(writer.WrittenSpan.ToArray());
#endif
        string expected = YamlDocument.ConvertToYamlString(json);
        Assert.AreEqual(expected, yaml);
    }

    // ===================================================================
    // Category 18: IndentSize edge cases
    // ===================================================================

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    [DataRow(-100)]
    public void IndentSize_ZeroOrNegative_DefaultsToTwo(int indentSize)
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = indentSize });
        Assert.AreEqual("a:\n  b: 1", yaml);
    }

    [TestMethod]
    public void IndentSize_Large_ProducesWideIndent()
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = 8 });
        Assert.AreEqual("a:\n        b: 1", yaml);
    }

    // ===================================================================
    // Category 19: Empty containers at various depths
    // ===================================================================

    [TestMethod]
    public void EmptyContainersInsideNestedStructure_RoundTrip()
    {
        string json = """{"outer": {"empty_obj": {}, "empty_arr": [], "value": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    public void NestedEmptyArraysInArray_RoundTrip()
    {
        string json = """[[], {}, [1], {"a": 1}]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 20: Consecutive escape sequences
    // ===================================================================

    [TestMethod]
    public void ConsecutiveEscapeSequences_RoundTrip()
    {
        string json = """{"k": "line1\nline2\n\ttab\r\nwindows"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    [TestMethod]
    public void QuotesAndBackslashesInString_RoundTrip()
    {
        string json = """{"k": "say \"hello\" and use \\path\\file"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    // ===================================================================
    // Category 21: Property order preservation
    // ===================================================================

    [TestMethod]
    public void PropertyOrderPreserved()
    {
        string json = """{"z": 1, "a": 2, "m": 3, "b": 4}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        string[] lines = yaml.Split('\n');
        Assert.AreEqual("z: 1", lines[0]);
        Assert.AreEqual("a: 2", lines[1]);
        Assert.AreEqual("m: 3", lines[2]);
        Assert.AreEqual("b: 4", lines[3]);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static void AssertJsonEqual(string expected, string actual)
    {
        // Normalize both JSONs to compact form via re-serialization
        Assert.AreEqual(NormalizeJson(expected), NormalizeJson(actual));
    }

    private static string NormalizeJson(string json)
    {
        using System.Text.Json.JsonDocument doc = System.Text.Json.JsonDocument.Parse(json);
        using MemoryStream stream = new();
        using (System.Text.Json.Utf8JsonWriter writer = new(stream))
        {
            doc.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}
