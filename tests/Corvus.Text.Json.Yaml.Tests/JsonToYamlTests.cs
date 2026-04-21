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
using Xunit;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Tests for JSON→YAML conversion (all three conversion paths and round-trip).
/// </summary>
public class JsonToYamlTests
{
    // ===================================================================
    // Category 1: Basic scalar conversions
    // ===================================================================

    [Theory]
    [InlineData("""{"key": "hello"}""", "key: hello")]
    [InlineData("""{"key": 42}""", "key: 42")]
    [InlineData("""{"key": 3.14}""", "key: 3.14")]
    [InlineData("""{"key": true}""", "key: true")]
    [InlineData("""{"key": false}""", "key: false")]
    [InlineData("""{"key": null}""", "key: null")]
    public void BasicScalarValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    [Theory]
    [InlineData("""42""", "42")]
    [InlineData("""3.14""", "3.14")]
    [InlineData("""true""", "true")]
    [InlineData("""false""", "false")]
    [InlineData("""null""", "null")]
    public void BareScalarRootValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 2: Number handling (use "num" key to avoid "n" quoting)
    // ===================================================================

    [Theory]
    [InlineData("""{"num": 0}""", "num: 0")]
    [InlineData("""{"num": -42}""", "num: -42")]
    [InlineData("""{"num": 9223372036854775807}""", "num: 9223372036854775807")]
    [InlineData("""{"num": 1.23e10}""", "num: 1.23e10")]
    [InlineData("""{"num": 1.23e-10}""", "num: 1.23e-10")]
    public void NumericValues(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 3: Empty collections
    // ===================================================================

    [Fact]
    public void EmptyObject()
    {
        string yaml = YamlDocument.ConvertToYamlString("{}");
        Assert.Equal("{}", yaml);
    }

    [Fact]
    public void EmptyArray()
    {
        string yaml = YamlDocument.ConvertToYamlString("[]");
        Assert.Equal("[]", yaml);
    }

    [Fact]
    public void NestedEmptyCollections()
    {
        string yaml = YamlDocument.ConvertToYamlString("""{"obj": {}, "arr": []}""");
        Assert.Equal("obj: {}\narr: []", yaml);
    }

    [Fact]
    public void ArrayOfEmptyObjects()
    {
        string yaml = YamlDocument.ConvertToYamlString("[{}, {}, {}]");
        Assert.Equal("- {}\n- {}\n- {}", yaml);
    }

    // ===================================================================
    // Category 4: Nested structures
    // ===================================================================

    [Fact]
    public void NestedMappings()
    {
        string json = """{"a": {"b": {"c": 1}}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("a:\n  b:\n    c: 1", yaml);
    }

    [Fact]
    public void NestedSequences()
    {
        string json = "[[1, 2], [3, 4]]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        // Sequence-in-sequence: "- " prefix, then children on next line indented
        Assert.Equal("- \n  - 1\n  - 2\n- \n  - 3\n  - 4", yaml);
    }

    [Fact]
    public void MappingWithSequenceValue()
    {
        string json = """{"items": [1, 2, 3]}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("items:\n  - 1\n  - 2\n  - 3", yaml);
    }

    [Fact]
    public void SequenceOfMappings()
    {
        string json = """[{"a": 1}, {"b": 2}]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        // Mapping-in-sequence: "- " prefix, then first property on next line
        Assert.Equal("- \n  a: 1\n- \n  b: 2", yaml);
    }

    [Fact]
    public void ComplexMixedNesting()
    {
        string json = """{"people": [{"name": "Alice", "age": 30}, {"name": "Bob", "age": 25}]}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(
            "people:\n  - \n    name: Alice\n    age: 30\n  - \n    name: Bob\n    age: 25",
            yaml);
    }

    // ===================================================================
    // Category 5: Scalar quoting — strings that must be quoted to round-trip
    // ===================================================================

    [Theory]
    [InlineData("""{"k": "true"}""", "k: \"true\"")] // looks like bool
    [InlineData("""{"k": "false"}""", "k: \"false\"")] // looks like bool
    [InlineData("""{"k": "null"}""", "k: \"null\"")] // looks like null
    [InlineData("""{"k": "~"}""", "k: \"~\"")] // tilde = null in YAML
    [InlineData("""{"k": "yes"}""", "k: \"yes\"")] // YAML 1.1 bool
    [InlineData("""{"k": "no"}""", "k: \"no\"")] // YAML 1.1 bool
    [InlineData("""{"k": "on"}""", "k: \"on\"")] // YAML 1.1 bool
    [InlineData("""{"k": "off"}""", "k: \"off\"")] // YAML 1.1 bool
    [InlineData("""{"k": "True"}""", "k: \"True\"")] // case variant
    [InlineData("""{"k": "FALSE"}""", "k: \"FALSE\"")] // case variant
    [InlineData("""{"k": "NULL"}""", "k: \"NULL\"")] // case variant
    [InlineData("""{"k": "Yes"}""", "k: \"Yes\"")] // case variant
    [InlineData("""{"k": "NO"}""", "k: \"NO\"")] // case variant
    public void StringsLookingLikeBoolOrNull_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    [Theory]
    [InlineData("""{"k": "123"}""", "k: \"123\"")] // looks like int
    [InlineData("""{"k": "3.14"}""", "k: \"3.14\"")] // looks like float
    [InlineData("""{"k": ".inf"}""", "k: \".inf\"")] // looks like YAML infinity
    [InlineData("""{"k": "-.inf"}""", "k: \"-.inf\"")] // looks like YAML -infinity
    [InlineData("""{"k": ".nan"}""", "k: \".nan\"")] // looks like YAML NaN
    [InlineData("""{"k": ".Inf"}""", "k: \".Inf\"")] // case variant
    [InlineData("""{"k": ".NaN"}""", "k: \".NaN\"")] // case variant
    [InlineData("""{"k": "0x1A"}""", "k: \"0x1A\"")] // looks like hex int
    [InlineData("""{"k": "0o17"}""", "k: \"0o17\"")] // looks like octal int
    public void StringsLookingLikeNumbers_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    [Theory]
    [InlineData("""{"k": ""}""", "k: \"\"")] // empty string
    [InlineData("""{"k": "  value"}""", "k: \"  value\"")] // leading spaces
    [InlineData("""{"k": "value  "}""", "k: \"value  \"")] // trailing spaces
    [InlineData("""{"k": "hello: world"}""", "k: \"hello: world\"")] // colon+space
    [InlineData("""{"k": "value #comment"}""", "k: \"value #comment\"")] // space+hash
    [InlineData("""{"k": "- item"}""", "k: \"- item\"")] // leading dash+space
    [InlineData("""{"k": "? key"}""", "k: \"? key\"")] // leading question+space
    [InlineData("""{"k": "&anchor"}""", "k: \"&anchor\"")] // anchor indicator
    [InlineData("""{"k": "*alias"}""", "k: \"*alias\"")] // alias indicator
    [InlineData("""{"k": "!tag"}""", "k: \"!tag\"")] // tag indicator
    [InlineData("""{"k": "{flow}"}""", "k: \"{flow}\"")] // flow mapping indicator
    [InlineData("""{"k": "[flow]"}""", "k: \"[flow]\"")] // flow sequence indicator
    [InlineData("""{"k": "%directive"}""", "k: \"%directive\"")] // directive indicator
    [InlineData("""{"k": "@reserved"}""", "k: \"@reserved\"")] // reserved indicator
    [InlineData("""{"k": "`backtick"}""", "k: \"`backtick\"")] // reserved indicator
    public void StringsWithSpecialCharacters_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 6: Escape sequences in double-quoted strings
    // ===================================================================

    [Fact]
    public void StringWithNewline_IsDoubleQuotedWithEscape()
    {
        string json = """{"k": "line1\nline2"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("k: \"line1\\nline2\"", yaml);
    }

    [Fact]
    public void StringWithCarriageReturn_IsDoubleQuotedWithEscape()
    {
        string json = "{\"k\": \"a\\rb\"}";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("k: \"a\\rb\"", yaml);
    }

    // Strings with only embedded quotes, backslash, or tab are NOT double-quoted
    // because those characters are safe in YAML plain scalars
    [Fact]
    public void StringWithEmbeddedQuotes_IsPlainScalar()
    {
        string json = """{"k": "say \"hi\""}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("k: say \"hi\"", yaml);
    }

    [Fact]
    public void StringWithBackslash_IsPlainScalar()
    {
        string json = """{"k": "c:\\path"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("k: c:\\path", yaml);
    }

    [Fact]
    public void StringWithTab_IsPlainScalar()
    {
        // Tab is safe in YAML plain scalars — it passes through as a literal byte
        string json = """{"k": "col1\tcol2"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("k: col1\tcol2", yaml);
    }

    // ===================================================================
    // Category 7: Empty string as key
    // ===================================================================

    [Fact]
    public void EmptyStringKey()
    {
        string json = """{"": "value"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("\"\": value", yaml);
    }

    // ===================================================================
    // Category 8: IndentSize options
    // ===================================================================

    [Theory]
    [InlineData(2, "a:\n  b: 1")]
    [InlineData(4, "a:\n    b: 1")]
    [InlineData(1, "a:\n b: 1")]
    public void IndentSizeOption(int indentSize, string expectedYaml)
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = indentSize });
        Assert.Equal(expectedYaml, yaml);
    }

    [Fact]
    public void DefaultOptionsUsesTwoSpaceIndent()
    {
        // Verifies that default(YamlWriterOptions) with IndentSize=0 is treated as 2
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("a:\n  b: 1", yaml);
    }

    // ===================================================================
    // Category 9: Conversion path equivalence — string, UTF-8, JsonElement
    // ===================================================================

    [Theory]
    [InlineData("""{"a": 1, "b": [2, 3], "c": {"d": true}}""")]
    [InlineData("""[1, "two", null, false, {"nested": []}]""")]
    [InlineData("""{"reserved": "true", "num": "123", "empty": ""}""")]
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

        Assert.Equal(fromString, fromUtf8);
        Assert.Equal(fromString, fromElement);
    }

    // ===================================================================
    // Category 10: Stream and buffer writer output paths
    // ===================================================================

    [Fact]
    public void ConvertToYaml_WritesToStream()
    {
        string json = """{"name": "test", "value": 42}""";
        using MemoryStream stream = new();

        YamlDocument.ConvertToYaml(json, stream);

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        Assert.Equal("name: test\nvalue: 42", yaml);
    }

    [Fact]
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
        Assert.Equal("name: test\nvalue: 42", yaml);
    }

    // ===================================================================
    // Category 11: Round-trip (JSON → YAML → JSON)
    // ===================================================================

    [Theory]
    [InlineData("""{"a": 1}""")]
    [InlineData("""{"a": 1, "b": "hello", "c": true, "d": null}""")]
    [InlineData("""[1, 2, 3]""")]
    [InlineData("""{"nested": {"deep": {"value": 42}}}""")]
    [InlineData("""{"arr": [{"a": 1}, {"b": 2}]}""")]
    [InlineData("""{"empty_obj": {}, "empty_arr": []}""")]
    public void RoundTrip_PreservesStructure(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [Theory]
    [InlineData("""{"k": "true"}""")] // string "true" must stay string, not become bool
    [InlineData("""{"k": "false"}""")]
    [InlineData("""{"k": "null"}""")] // string "null" must stay string, not become null
    [InlineData("""{"k": "123"}""")] // string "123" must stay string, not become number
    [InlineData("""{"k": "3.14"}""")]
    [InlineData("""{"k": "yes"}""")]
    [InlineData("""{"k": "no"}""")]
    [InlineData("""{"k": "~"}""")]
    [InlineData("""{"k": ""}""")]
    [InlineData("""{"k": ".inf"}""")]
    [InlineData("""{"k": ".nan"}""")]
    public void RoundTrip_PreservesStringTypes(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [Theory]
    [InlineData("""{"k": "hello: world"}""")]
    [InlineData("""{"k": "value #comment"}""")]
    [InlineData("""{"k": "- item"}""")]
    [InlineData("""{"k": "  leading"}""")]
    [InlineData("""{"k": "trailing  "}""")]
    [InlineData("""{"k": "&anchor"}""")]
    [InlineData("""{"k": "*alias"}""")]
    [InlineData("""{"k": "line1\nline2"}""")]
    [InlineData("""{"k": "col1\tcol2"}""")]
    [InlineData("""{"k": "say \"hi\""}""")]
    [InlineData("""{"k": "back\\slash"}""")]
    public void RoundTrip_PreservesSpecialStrings(string json)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [Fact]
    public void RoundTrip_AllReservedWords()
    {
        string json = """{"yes": "yes", "no": "no", "true": "true", "false": "false", "null": "null", "tilde": "~", "on": "on", "off": "off"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);

        AssertJsonEqual(json, roundTripped);
    }

    [Fact]
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

    [Theory]
    [InlineData("""{"k": "你好"}""", "k: 你好")]
    [InlineData("""{"k": "שלום"}""", "k: שלום")]
    public void UnicodeStrings(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    [Fact]
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

    [Fact]
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

    [Fact]
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

    [Theory]
    [InlineData("""{"n": 1}""", "\"n\": 1")]
    [InlineData("""{"y": 1}""", "\"y\": 1")]
    [InlineData("""{"N": 1}""", "\"N\": 1")]
    [InlineData("""{"Y": 1}""", "\"Y\": 1")]
    public void SingleCharReservedKeys_AreQuoted(string json, string expectedYaml)
    {
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expectedYaml, yaml);
    }

    // ===================================================================
    // Category 15: Deep nesting (beyond initial stack capacity)
    // ===================================================================

    [Theory]
    [InlineData(5)]
    [InlineData(16)]
    [InlineData(20)]  // Beyond MaxStackDepth initial capacity
    [InlineData(32)]
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
        Assert.Equal(depth, lines.Length);
        for (int i = 0; i < depth - 1; i++)
        {
            Assert.StartsWith(new string(' ', i * 2) + "a:", lines[i]);
        }

        // Last line is the value
        Assert.StartsWith(new string(' ', (depth - 1) * 2) + "a: 1", lines[depth - 1]);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(20)]
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

    [Theory]
    [InlineData("""{"a": 1, "b": [2, 3]}""")]
    [InlineData("""[1, "two", null]""")]
    [InlineData("""{"nested": {"deep": true}}""")]
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

        Assert.Equal(fromString, fromElement);
    }

    [Theory]
    [InlineData("""{"a": 1, "b": [2, 3]}""")]
    [InlineData("""[1, "two", null]""")]
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
        Assert.Equal(expected, yaml);
    }

    [Theory]
    [InlineData("""{"a": 1, "b": [2, 3]}""")]
    [InlineData("""[1, "two", null]""")]
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
        Assert.Equal(expected, yaml);
    }

    // ===================================================================
    // Category 17: UTF-8 byte overloads with Stream/IBufferWriter
    // ===================================================================

    [Theory]
    [InlineData("""{"a": 1, "b": "hello"}""")]
    public void Utf8Overload_WritesToStream(string json)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        using MemoryStream stream = new();

        YamlDocument.ConvertToYaml((ReadOnlySpan<byte>)utf8, stream);

        stream.Position = 0;
        string yaml = new StreamReader(stream, Encoding.UTF8).ReadToEnd();
        string expected = YamlDocument.ConvertToYamlString(json);
        Assert.Equal(expected, yaml);
    }

    [Theory]
    [InlineData("""{"a": 1, "b": "hello"}""")]
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
        Assert.Equal(expected, yaml);
    }

    // ===================================================================
    // Category 18: IndentSize edge cases
    // ===================================================================

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void IndentSize_ZeroOrNegative_DefaultsToTwo(int indentSize)
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = indentSize });
        Assert.Equal("a:\n  b: 1", yaml);
    }

    [Fact]
    public void IndentSize_Large_ProducesWideIndent()
    {
        string json = """{"a": {"b": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json, new YamlWriterOptions { IndentSize = 8 });
        Assert.Equal("a:\n        b: 1", yaml);
    }

    // ===================================================================
    // Category 19: Empty containers at various depths
    // ===================================================================

    [Fact]
    public void EmptyContainersInsideNestedStructure_RoundTrip()
    {
        string json = """{"outer": {"empty_obj": {}, "empty_arr": [], "value": 1}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    [Fact]
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

    [Fact]
    public void ConsecutiveEscapeSequences_RoundTrip()
    {
        string json = """{"k": "line1\nline2\n\ttab\r\nwindows"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        byte[] yamlBytes = Encoding.UTF8.GetBytes(yaml);
        string roundTripped = YamlDocument.ConvertToJsonString(yamlBytes);
        AssertJsonEqual(json, roundTripped);
    }

    [Fact]
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

    [Fact]
    public void PropertyOrderPreserved()
    {
        string json = """{"z": 1, "a": 2, "m": 3, "b": 4}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        string[] lines = yaml.Split('\n');
        Assert.Equal("z: 1", lines[0]);
        Assert.Equal("a: 2", lines[1]);
        Assert.Equal("m: 3", lines[2]);
        Assert.Equal("b: 4", lines[3]);
    }

    // ===================================================================
    // Helpers
    // ===================================================================

    private static void AssertJsonEqual(string expected, string actual)
    {
        // Normalize both JSONs to compact form via re-serialization
        Assert.Equal(NormalizeJson(expected), NormalizeJson(actual));
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
