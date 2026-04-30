// <copyright file="CollectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

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
/// Tests for block and flow collections, nesting, and complex structures.
/// </summary>
public class CollectionTests
{
    // ========================
    // Block mappings
    // ========================

    [Fact]
    public void BlockMapping_MultipleKeys()
    {
        byte[] yaml = "a: 1\nb: 2\nc: 3"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(1, root.GetProperty("a"u8).GetInt32());
        Assert.Equal(2, root.GetProperty("b"u8).GetInt32());
        Assert.Equal(3, root.GetProperty("c"u8).GetInt32());
    }

    [Fact]
    public void BlockMapping_NestedMappings()
    {
        byte[] yaml = "outer:\n  middle:\n    inner: value"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement
            .GetProperty("outer"u8)
            .GetProperty("middle"u8)
            .GetProperty("inner"u8)
            .GetString());
    }

    [Fact]
    public void BlockMapping_EmptyValue()
    {
        byte[] yaml = "key:"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [Fact]
    public void BlockMapping_EmptyValueFollowedByKey()
    {
        byte[] yaml = "a:\nb: 2"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.Equal(2, doc.RootElement.GetProperty("b"u8).GetInt32());
    }

    [Fact]
    public void BlockMapping_MixedValueTypes()
    {
        byte[] yaml = "str: hello\nnum: 42\nbool: true\nnull_val:\nfloat: 3.14"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("str"u8).GetString());
        Assert.Equal(42, root.GetProperty("num"u8).GetInt32());
        Assert.True(root.GetProperty("bool"u8).GetBoolean());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("null_val"u8).ValueKind);
        Assert.Equal(3.14, root.GetProperty("float"u8).GetDouble());
    }

    // ========================
    // Block sequences
    // ========================

    [Fact]
    public void BlockSequence_Strings()
    {
        byte[] yaml = "- alpha\n- beta\n- gamma"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("alpha", root[0].GetString());
        Assert.Equal("beta", root[1].GetString());
        Assert.Equal("gamma", root[2].GetString());
    }

    [Fact]
    public void BlockSequence_MixedTypes()
    {
        byte[] yaml = "- hello\n- 42\n- true\n- null"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal("hello", root[0].GetString());
        Assert.Equal(42, root[1].GetInt32());
        Assert.True(root[2].GetBoolean());
        Assert.Equal(JsonValueKind.Null, root[3].ValueKind);
    }

    [Fact]
    public void BlockSequence_NestedSequences()
    {
        byte[] yaml = "-\n  - a\n  - b\n-\n  - c\n  - d"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("a", root[0][0].GetString());
        Assert.Equal("b", root[0][1].GetString());
        Assert.Equal("c", root[1][0].GetString());
        Assert.Equal("d", root[1][1].GetString());
    }

    // ========================
    // Compact notation (- key: value)
    // ========================

    [Fact]
    public void CompactNotation_SequenceOfMappings()
    {
        byte[] yaml = "- name: Alice\n  age: 30\n- name: Bob\n  age: 25"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("Alice", root[0].GetProperty("name"u8).GetString());
        Assert.Equal(30, root[0].GetProperty("age"u8).GetInt32());
    }

    [Fact]
    public void CompactNotation_MappingContainingSequence()
    {
        byte[] yaml = "items:\n  - one\n  - two\n  - three"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var items = doc.RootElement.GetProperty("items"u8);
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.Equal(3, items.GetArrayLength());
        Assert.Equal("one", items[0].GetString());
    }

    // ========================
    // Flow mappings
    // ========================

    [Fact]
    public void FlowMapping_Simple()
    {
        byte[] yaml = "{a: 1, b: 2}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(1, doc.RootElement.GetProperty("a"u8).GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("b"u8).GetInt32());
    }

    [Fact]
    public void FlowMapping_Empty()
    {
        byte[] yaml = "{}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [Fact]
    public void FlowMapping_QuotedKeys()
    {
        byte[] yaml = "{\"a\": 1, 'b': 2}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(1, doc.RootElement.GetProperty("a"u8).GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("b"u8).GetInt32());
    }

    [Fact]
    public void FlowMapping_NestedFlowMapping()
    {
        byte[] yaml = "{a: {b: 1}}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(1, doc.RootElement.GetProperty("a"u8).GetProperty("b"u8).GetInt32());
    }

    // ========================
    // Flow sequences
    // ========================

    [Fact]
    public void FlowSequence_Simple()
    {
        byte[] yaml = "[1, 2, 3]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(3, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void FlowSequence_Empty()
    {
        byte[] yaml = "[]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        Assert.Equal(0, doc.RootElement.GetArrayLength());
    }

    [Fact]
    public void FlowSequence_Nested()
    {
        byte[] yaml = "[[1, 2], [3, 4]]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal(1, doc.RootElement[0][0].GetInt32());
        Assert.Equal(4, doc.RootElement[1][1].GetInt32());
    }

    [Fact]
    public void FlowSequence_MixedTypes()
    {
        byte[] yaml = "[1, \"hello\", true, null]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(1, doc.RootElement[0].GetInt32());
        Assert.Equal("hello", doc.RootElement[1].GetString());
        Assert.True(doc.RootElement[2].GetBoolean());
        Assert.Equal(JsonValueKind.Null, doc.RootElement[3].ValueKind);
    }

    // ========================
    // Mixed block + flow
    // ========================

    [Fact]
    public void Mixed_BlockMappingWithFlowSequenceValue()
    {
        byte[] yaml = "key: [1, 2, 3]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var arr = doc.RootElement.GetProperty("key"u8);
        Assert.Equal(JsonValueKind.Array, arr.ValueKind);
        Assert.Equal(3, arr.GetArrayLength());
    }

    [Fact]
    public void Mixed_BlockMappingWithFlowMappingValue()
    {
        byte[] yaml = "key: {a: 1, b: 2}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var obj = doc.RootElement.GetProperty("key"u8);
        Assert.Equal(1, obj.GetProperty("a"u8).GetInt32());
    }

    [Fact]
    public void Mixed_BlockSequenceWithFlowItems()
    {
        byte[] yaml = "- {a: 1}\n- [1, 2]"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(JsonValueKind.Object, doc.RootElement[0].ValueKind);
        Assert.Equal(JsonValueKind.Array, doc.RootElement[1].ValueKind);
    }

    // ========================
    // Document markers
    // ========================

    [Fact]
    public void DocumentStart_Marker()
    {
        byte[] yaml = "---\nkey: value"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DocumentEnd_Marker()
    {
        byte[] yaml = "key: value\n..."u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DocumentStartAndEnd()
    {
        byte[] yaml = "---\nkey: value\n..."u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Comments
    // ========================

    [Fact]
    public void Comment_FullLineComment()
    {
        byte[] yaml = "# This is a comment\nkey: value"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void Comment_InlineComment()
    {
        byte[] yaml = "key: value # This is an inline comment"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal("value", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void Comment_BetweenMappingEntries()
    {
        byte[] yaml = "a: 1\n# comment\nb: 2"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.Equal(1, doc.RootElement.GetProperty("a"u8).GetInt32());
        Assert.Equal(2, doc.RootElement.GetProperty("b"u8).GetInt32());
    }

    // ========================
    // Duplicate key behavior
    // ========================

    [Fact]
    public void DuplicateKey_ErrorByDefault()
    {
        byte[] yaml = "a: 1\na: 2"u8.ToArray();
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(yaml));
    }

    [Fact]
    public void DuplicateKey_LastWins()
    {
        byte[] yaml = "a: 1\na: 2"u8.ToArray();
        using var doc = YamlTestHelper.Parse(
            yaml, new YamlReaderOptions { DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins });
        Assert.Equal(2, doc.RootElement.GetProperty("a"u8).GetInt32());
    }

    // ========================
    // Error handling
    // ========================

    [Fact]
    public void Error_MaxNestingDepthExceeded()
    {
        // Create deeply nested YAML (65 levels > max 64)
        string yaml = string.Concat(Enumerable.Range(0, 65).Select(i => new string(' ', i * 2) + "a:\n"));
        byte[] data = System.Text.Encoding.UTF8.GetBytes(yaml);
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(data));
    }

    [Fact]
    public void Error_UnterminatedDoubleQuote()
    {
        byte[] yaml = "key: \"unterminated"u8.ToArray();
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(yaml));
    }

    [Fact]
    public void Error_UnterminatedSingleQuote()
    {
        byte[] yaml = "key: 'unterminated"u8.ToArray();
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(yaml));
    }

    [Fact]
    public void Error_UnterminatedFlowMapping()
    {
        byte[] yaml = "{a: 1"u8.ToArray();
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(yaml));
    }

    [Fact]
    public void Error_UnterminatedFlowSequence()
    {
        byte[] yaml = "[1, 2"u8.ToArray();
        Assert.Throws<YamlException>(() => YamlTestHelper.Parse(yaml));
    }

    // ========================
    // Large mappings (hash set growth)
    // ========================

    [Fact]
    public void BlockMapping_MoreThan11Keys_GrowsHashSet()
    {
        byte[] yaml =
            """
            a: 1
            b: 2
            c: 3
            d: 4
            e: 5
            f: 6
            g: 7
            h: 8
            i: 9
            j: 10
            k: 11
            l: 12
            m: 13
            n: 14
            """u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(14, root.EnumerateObject().Count());
        Assert.Equal(1, root.GetProperty("a"u8).GetInt32());
        Assert.Equal(14, root.GetProperty("n"u8).GetInt32());
    }

    [Fact]
    public void FlowMapping_MoreThan11Keys_GrowsHashSet()
    {
        byte[] yaml = "{a: 1, b: 2, c: 3, d: 4, e: 5, f: 6, g: 7, h: 8, i: 9, j: 10, k: 11, l: 12}"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        var root = doc.RootElement;
        Assert.Equal(12, root.EnumerateObject().Count());
        Assert.Equal(1, root.GetProperty("a"u8).GetInt32());
        Assert.Equal(12, root.GetProperty("l"u8).GetInt32());
    }
}