// <copyright file="YamlToJsonBasicTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
using Xunit;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Basic smoke tests for YAML to JSON conversion.
/// </summary>
public class YamlToJsonBasicTests
{
    [Fact]
    public void SimpleMapping()
    {
        byte[] yaml = "name: hello\nvalue: 42"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
    }

    [Fact]
    public void SimpleSequence()
    {
        byte[] yaml = "- one\n- two\n- three"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("one", root[0].GetString());
        Assert.Equal("two", root[1].GetString());
        Assert.Equal("three", root[2].GetString());
    }

    [Fact]
    public void NullValues()
    {
        byte[] yaml = "a: null\nb: ~\nc:"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Null, root.GetProperty("a"u8).ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("b"u8).ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("c"u8).ValueKind);
    }

    [Fact]
    public void BooleanValues()
    {
        byte[] yaml = "a: true\nb: false"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.True(root.GetProperty("a"u8).GetBoolean());
        Assert.False(root.GetProperty("b"u8).GetBoolean());
    }

    [Fact]
    public void NestedMapping()
    {
        byte[] yaml = "outer:\n  inner:\n    value: deep"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal("deep", root.GetProperty("outer"u8).GetProperty("inner"u8).GetProperty("value"u8).GetString());
    }

    [Fact]
    public void DoubleQuotedScalar()
    {
        byte[] yaml = "key: \"hello world\""u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal("hello world", root.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void SingleQuotedScalar()
    {
        byte[] yaml = "key: 'hello world'"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal("hello world", root.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void FlowMapping()
    {
        byte[] yaml = "{a: 1, b: 2}"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(1, root.GetProperty("a"u8).GetInt32());
        Assert.Equal(2, root.GetProperty("b"u8).GetInt32());
    }

    [Fact]
    public void FlowSequence()
    {
        byte[] yaml = "[1, 2, 3]"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    [Fact]
    public void DocumentStart()
    {
        byte[] yaml = "---\nkey: value"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal("value", root.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void Comments()
    {
        byte[] yaml = "# comment\nkey: value # inline comment"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal("value", root.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void IntegerFormats()
    {
        byte[] yaml = "dec: 123\nhex: 0xFF\noct: 0o77"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(123, root.GetProperty("dec"u8).GetInt32());
        Assert.Equal(255, root.GetProperty("hex"u8).GetInt32());
        Assert.Equal(63, root.GetProperty("oct"u8).GetInt32());
    }

    [Fact]
    public void FloatValues()
    {
        byte[] yaml = "pi: 3.14\nneg: -1.5"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(3.14, root.GetProperty("pi"u8).GetDouble());
        Assert.Equal(-1.5, root.GetProperty("neg"u8).GetDouble());
    }

    [Fact]
    public void InfinityAndNan()
    {
        byte[] yaml = "inf: .inf\nninf: -.inf\nnan: .nan"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        // .inf/.nan have no JSON representation → mapped to null
        Assert.Equal(JsonValueKind.Null, root.GetProperty("inf"u8).ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("ninf"u8).ValueKind);
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nan"u8).ValueKind);
    }

    [Fact]
    public void EmptyDocument()
    {
        byte[] yaml = ""u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Null, root.ValueKind);
    }

    [Fact]
    public void CompactNotation()
    {
        byte[] yaml = "- name: Alice\n  age: 30\n- name: Bob\n  age: 25"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;

        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("Alice", root[0].GetProperty("name"u8).GetString());
        Assert.Equal(30, root[0].GetProperty("age"u8).GetInt32());
        Assert.Equal("Bob", root[1].GetProperty("name"u8).GetString());
        Assert.Equal(25, root[1].GetProperty("age"u8).GetInt32());
    }
}