// <copyright file="JsonToYamlCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
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
/// Targeted coverage tests for JsonToYamlConverter.cs — exercises WriteReaderToken
/// scalar branches and WriteReaderStringOrName escape paths.
/// </summary>
public class JsonToYamlCoverageTests
{
    // ========================
    // WriteReaderToken scalar cases (L172-189)
    // These are hit when the FIRST element in an array is a scalar,
    // because the peek-ahead for empty arrays reads the first token
    // and dispatches it via WriteReaderToken.
    // ========================

    [Fact]
    public void ArrayFirstElement_String()
    {
        // First array element is String → WriteReaderToken L172-173
        string json = """["hello", "world"]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- hello\n- world", yaml);
    }

    [Fact]
    public void ArrayFirstElement_True()
    {
        // First array element is True → WriteReaderToken L180-181
        string json = "[true, false]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- true\n- false", yaml);
    }

    [Fact]
    public void ArrayFirstElement_False()
    {
        // First array element is False → WriteReaderToken L184-185
        string json = "[false, true]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- false\n- true", yaml);
    }

    [Fact]
    public void ArrayFirstElement_Null()
    {
        // First array element is Null → WriteReaderToken L188-189
        string json = "[null, 1]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- null\n- 1", yaml);
    }

    [Fact]
    public void ObjectFirstProperty_String()
    {
        // First property NAME after StartObject → WriteReaderToken L167-168 (PropertyName)
        // This should already be covered but let's ensure it works with arrays
        string json = """{"a": "b"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("a: b", yaml);
    }

    // ========================
    // Empty object as a nested value (L515-517)
    // ========================

    [Fact]
    public void EmptyObjectValue()
    {
        // Empty object as property value → WriteEmptyMapping (L515-517)
        string json = """{"empty": {}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("empty: {}", yaml);
    }

    [Fact]
    public void EmptyObjectInArray()
    {
        string json = """[{}]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- {}", yaml);
    }

    [Fact]
    public void EmptyArrayInMapping()
    {
        string json = """{"empty": []}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("empty: []", yaml);
    }

    // ========================
    // WriteReaderStringOrName with escaped content (L226-228, L237-239)
    // ========================

    [Fact]
    public void EscapedPropertyName()
    {
        // Property name with escape sequences → ValueIsEscaped=true → L225-228
        string json = """{"key\nname": "value"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        // The property name has a literal newline after unescaping, needs quoting in YAML
        Assert.Contains("key", yaml);
        Assert.Contains("value", yaml);
    }

    [Fact]
    public void EscapedStringValue()
    {
        // String value with escapes → ValueIsEscaped=true, isKey=false → L230-231
        string json = """{"key": "hello\nworld"}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("key", yaml);
    }

    [Fact]
    public void LongEscapedPropertyName_RentsBuffer()
    {
        // Property name longer than StackallocByteThreshold (256) with escapes
        // → rentedArray path → L237-239 (return rented array)
        // Build a JSON string with a long escaped property name
        StringBuilder sb = new();
        sb.Append("{\"");
        // Create a property name with 300+ characters including escape sequences
        for (int i = 0; i < 150; i++)
        {
            sb.Append("\\n"); // each escape is 2 chars in JSON, 1 byte after unescape
        }

        sb.Append("\": 1}");
        string json = sb.ToString();
        string yaml = YamlDocument.ConvertToYamlString(json);
        // Should produce valid YAML with the key quoted (contains newlines)
        Assert.Contains("1", yaml);
    }

    [Fact]
    public void LongEscapedStringValue_RentsBuffer()
    {
        // String value longer than 256 bytes with escapes → rented buffer path
        StringBuilder sb = new();
        sb.Append("{\"k\": \"");
        for (int i = 0; i < 150; i++)
        {
            sb.Append("\\t");
        }

        sb.Append("\"}");
        string json = sb.ToString();
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("k:", yaml);
    }

    // ========================
    // Mixed scenarios to ensure full paths are exercised
    // ========================

    [Fact]
    public void ArrayOfAllScalarTypes()
    {
        string json = """["text", 42, true, false, null, 3.14]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- text\n- 42\n- true\n- false\n- null\n- 3.14", yaml);
    }

    [Fact]
    public void NestedEmptyContainers()
    {
        string json = """{"a": {}, "b": [], "c": {"nested": {}}}""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Contains("a: {}", yaml);
        Assert.Contains("b: []", yaml);
    }

    [Fact]
    public void ArrayStartsWithNestedObject()
    {
        // First array element is StartObject → WriteReaderToken L141-152
        string json = """[{"x": 1}]""";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- \n  x: 1", yaml);
    }

    [Fact]
    public void ArrayStartsWithNestedArray()
    {
        // First array element is StartArray → WriteReaderToken L154-163
        string json = "[[1]]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- \n  - 1", yaml);
    }

    [Fact]
    public void ArrayStartsWithNestedEmptyObject()
    {
        // First array element is empty object → peek-ahead in WriteReaderToken
        string json = "[{}, 1]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- {}\n- 1", yaml);
    }

    [Fact]
    public void ArrayStartsWithNestedEmptyArray()
    {
        // First array element is empty array → peek-ahead in WriteReaderToken
        string json = "[[], 1]";
        string yaml = YamlDocument.ConvertToYamlString(json);
        Assert.Equal("- []\n- 1", yaml);
    }

    // ========================
    // CTJ element-walk path (Path 3) — empty object (L515-517)
    // ========================

    [Fact]
    public void ElementPath_EmptyObject()
    {
        // Use the element-based API to exercise CTJ Path 3 WriteObject empty check
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"empty": {}}"""u8.ToArray());
        var root = doc.RootElement;
        string yaml = YamlDocument.ConvertToYamlString(in root);
        Assert.Contains("empty: {}", yaml);
    }

    [Fact]
    public void ElementPath_EmptyArray()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"items": []}"""u8.ToArray());
        var root = doc.RootElement;
        string yaml = YamlDocument.ConvertToYamlString(in root);
        Assert.Contains("items: []", yaml);
    }

    [Fact]
    public void ElementPath_ScalarValues()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"str": "hi", "num": 42, "tr": true, "fl": false, "nu": null}"""u8.ToArray());
        var root = doc.RootElement;
        string yaml = YamlDocument.ConvertToYamlString(in root);
        Assert.Contains("str: hi", yaml);
        Assert.Contains("num: 42", yaml);
        Assert.Contains("tr: true", yaml);
        Assert.Contains("fl: false", yaml);
        Assert.Contains("nu: null", yaml);
    }
}
