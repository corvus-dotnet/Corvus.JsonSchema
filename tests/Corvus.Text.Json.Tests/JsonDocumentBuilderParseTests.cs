// <copyright file="JsonDocumentBuilderParseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO;
using System.IO.Tests;
using System.Linq;
using System.Text;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonDocumentBuilder{T}.Parse"/> — parsing directly to a mutable builder.
/// </summary>
public static class JsonDocumentBuilderParseTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    #region Simple value parsing

    [Fact]
    public static void ParseSimpleNumber()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """42"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Number, root.ValueKind);
        Assert.Equal(42, root.GetInt32());
    }

    [Fact]
    public static void ParseSimpleString()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "\"hello\""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.String, root.ValueKind);
        Assert.Equal("hello", root.GetString());
    }

    [Fact]
    public static void ParseTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "true"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.True, root.ValueKind);
    }

    [Fact]
    public static void ParseFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "false"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.False, root.ValueKind);
    }

    [Fact]
    public static void ParseNull()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "null"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Null, root.ValueKind);
    }

    #endregion

    #region Object parsing

    [Fact]
    public static void ParseSimpleObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
    }

    [Fact]
    public static void ParseNestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"person":{"name":"Bob","address":{"city":"London"}}}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Bob", root.GetProperty("person"u8).GetProperty("name"u8).GetString());
        Assert.Equal("London", root.GetProperty("person"u8).GetProperty("address"u8).GetProperty("city"u8).GetString());
    }

    [Fact]
    public static void ParseEmptyObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
    }

    #endregion

    #region Array parsing

    [Fact]
    public static void ParseSimpleArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[1,2,3]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    [Fact]
    public static void ParseNestedArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[[1,2],[3,[4,5]]]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(5, root[1][1][1].GetInt32());
    }

    [Fact]
    public static void ParseEmptyArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Array, root.ValueKind);
    }

    [Fact]
    public static void ParseArrayOfObjects()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """[{"a":1},{"b":2}]"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(1, root[0].GetProperty("a"u8).GetInt32());
        Assert.Equal(2, root[1].GetProperty("b"u8).GetInt32());
    }

    #endregion

    #region Escaped string handling

    [Fact]
    public static void ParseEscapedPropertyName()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"hello\u0020world":"value"}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("value", root.GetProperty("hello world"u8).GetString());
    }

    [Fact]
    public static void ParseEscapedStringValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"key":"line1\nline2"}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("line1\nline2", root.GetProperty("key"u8).GetString());
    }

    #endregion

    #region String overload

    [Fact]
    public static void ParseFromString()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"x":42}""");

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(42, root.GetProperty("x"u8).GetInt32());
    }

    [Fact]
    public static void ParseFromReadOnlyMemoryChar()
    {
        ReadOnlyMemory<char> json = """{"x":42}""".AsMemory();

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(42, root.GetProperty("x"u8).GetInt32());
    }

    #endregion

    #region Stream overload

    [Fact]
    public static void ParseFromSeekableStream()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, new MemoryStream(data));

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
    }

    [Fact]
    public static void ParseFromUnseekableStream()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data));

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
    }

    [Fact]
    public static void ParseFromStreamWithBom()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");
        byte[] dataWithBom = Utf8Bom.Concat(data).ToArray();

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, new MemoryStream(dataWithBom));

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
    }

    #endregion

    #region ParseValue from Utf8JsonReader

    [Fact]
    public static void ParseValueFromReader()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice","age":30}""");
        var reader = new Utf8JsonReader(data);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
    }

    [Fact]
    public static void ParseValueFromReaderAtPropertyName()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"outer":{"inner":42}}""");
        var reader = new Utf8JsonReader(data);

        // Advance to the "outer" property name
        reader.Read(); // StartObject
        reader.Read(); // PropertyName "outer"

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(42, root.GetProperty("inner"u8).GetInt32());
    }

    #endregion

    #region Mutation after parse

    [Fact]
    public static void MutateAfterParse_SetProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Bob");
        Assert.Equal("Bob", root.GetProperty("name"u8).GetString());
        Assert.Equal(30, root.GetProperty("age"u8).GetInt32());
    }

    [Fact]
    public static void MutateAfterParse_RemoveProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.RemoveProperty("age"u8));
        Assert.Equal("Alice", root.GetProperty("name"u8).GetString());
        Assert.False(root.TryGetProperty("age"u8, out _));
    }

    [Fact]
    public static void MutateAfterParse_InsertArrayItem()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[1,2,3]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        root.InsertItem(1, 99);
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(99, root[1].GetInt32());
        Assert.Equal(2, root[2].GetInt32());
        Assert.Equal(3, root[3].GetInt32());
    }

    #endregion

    #region Serialization equivalence

    [Theory]
    [InlineData("""42""")]
    [InlineData("\"hello\"")]
    [InlineData("""true""")]
    [InlineData("""false""")]
    [InlineData("""null""")]
    [InlineData("""{"a":1,"b":"two","c":true,"d":null}""")]
    [InlineData("""[1,"two",true,null,{"nested":42}]""")]
    [InlineData("""{"a":{"b":{"c":[1,2,3]}}}""")]
    [InlineData("""[]""")]
    [InlineData("""{}""")]
    [InlineData("""[{"x":1},{"x":2},{"x":3}]""")]
    [InlineData("""3.14159""")]
    [InlineData("""-42""")]
    [InlineData("""1e10""")]
    public static void SerializationMatchesParseThenBuild(string json)
    {
        // Parse directly to builder
        using var workspace1 = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> directBuilder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace1, json);
        string directResult = directBuilder.RootElement.ToString();

        // Parse to ParsedJsonDocument then build (traditional path)
        using var workspace2 = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonDocumentBuilder<JsonElement.Mutable> traditionalBuilder =
            parsedDoc.RootElement.CreateBuilder(workspace2);
        string traditionalResult = traditionalBuilder.RootElement.ToString();

        Assert.Equal(traditionalResult, directResult);
    }

    #endregion

    #region Snapshot and restore

    [Fact]
    public static void SnapshotAndRestoreAfterParse()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        using JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();

        // Mutate
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Bob");
        Assert.Equal("Bob", builder.RootElement.GetProperty("name"u8).GetString());

        // Restore
        builder.Restore(snapshot);
        Assert.Equal("Alice", builder.RootElement.GetProperty("name"u8).GetString());
    }

    #endregion

    #region Clone

    [Fact]
    public static void CloneElementFromParsedBuilder()
    {
        JsonElement clone;

        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"items":[1,2,3]}"""))
        {
            clone = builder.RootElement.GetProperty("items"u8).Clone();
        }

        // Clone survives after builder disposal
        Assert.Equal("[1,2,3]", clone.GetRawText());
    }

    #endregion

    #region Large document

    [Fact]
    public static void ParseLargeDocument()
    {
        // Build a JSON object with many properties
        var sb = new StringBuilder("{");
        for (int i = 0; i < 1000; i++)
        {
            if (i > 0)
            {
                sb.Append(',');
            }

            sb.Append($"\"prop{i}\":{i}");
        }

        sb.Append('}');
        string json = sb.ToString();

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal(0, root.GetProperty("prop0"u8).GetInt32());
        Assert.Equal(999, root.GetProperty("prop999"u8).GetInt32());
    }

    [Fact]
    public static void ParseDeeplyNestedDocument()
    {
        // Build a deeply nested JSON array [[[[...]]]]
        const int depth = 64;
        string json = new string('[', depth) + new string(']', depth);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        Assert.Equal(JsonValueKind.Array, builder.RootElement.ValueKind);
    }

    #endregion

    #region Floating-point precision

    [Fact]
    public static void ParseFloatingPointNumber()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "3.141592653589793"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.Equal(3.141592653589793, root.GetDouble());
    }

    #endregion

    #region WriteTo equivalence

    [Theory]
    [InlineData("""{"a":1,"b":"two","c":true,"d":null,"e":[1,2]}""")]
    [InlineData("""[1,"two",true,null,{"nested":42},[]]""")]
    public static void WriteToProducesSameOutputAsParseThenBuild(string json)
    {
        var directBuffer = new ArrayBufferWriter<byte>(1024);
        var traditionalBuffer = new ArrayBufferWriter<byte>(1024);

        // Direct parse to builder
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json))
        using (var writer = new Utf8JsonWriter(directBuffer))
        {
            builder.WriteTo(writer);
        }

        // Traditional parse-then-build
        using (var workspace = JsonWorkspace.Create())
        using (ParsedJsonDocument<JsonElement> parsedDoc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builder =
            parsedDoc.RootElement.CreateBuilder(workspace))
        using (var writer = new Utf8JsonWriter(traditionalBuffer))
        {
            builder.WriteTo(writer);
        }

        Assert.True(directBuffer.WrittenSpan.SequenceEqual(traditionalBuffer.WrittenSpan));
    }

    #endregion
}