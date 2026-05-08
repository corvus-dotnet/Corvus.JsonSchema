// <copyright file="JsonDocumentBuilderParseTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Linq;
using System.IO;
using System.IO.Tests;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests for <see cref="JsonDocumentBuilder{T}.Parse"/> — parsing directly to a mutable builder.
/// </summary>
[TestClass]
public class JsonDocumentBuilderParseTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    #region Simple value parsing

    [TestMethod]
    public void ParseSimpleNumber()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """42"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
        Assert.AreEqual(42, root.GetInt32());
    }

    [TestMethod]
    public void ParseSimpleString()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "\"hello\""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.String, root.ValueKind);
        Assert.AreEqual("hello", root.GetString());
    }

    [TestMethod]
    public void ParseTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "true"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.True, root.ValueKind);
    }

    [TestMethod]
    public void ParseFalse()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "false"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.False, root.ValueKind);
    }

    [TestMethod]
    public void ParseNull()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "null"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Null, root.ValueKind);
    }

    #endregion

    #region Object parsing

    [TestMethod]
    public void ParseSimpleObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
    }

    [TestMethod]
    public void ParseNestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"person":{"name":"Bob","address":{"city":"London"}}}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Bob", root.GetProperty("person"u8).GetProperty("name"u8).GetString());
        Assert.AreEqual("London", root.GetProperty("person"u8).GetProperty("address"u8).GetProperty("city"u8).GetString());
    }

    [TestMethod]
    public void ParseEmptyObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "{}"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
    }

    #endregion

    #region Array parsing

    [TestMethod]
    public void ParseSimpleArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[1,2,3]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
    }

    [TestMethod]
    public void ParseNestedArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[[1,2],[3,[4,5]]]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        Assert.AreEqual(1, root[0][0].GetInt32());
        Assert.AreEqual(5, root[1][1][1].GetInt32());
    }

    [TestMethod]
    public void ParseEmptyArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
    }

    [TestMethod]
    public void ParseArrayOfObjects()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """[{"a":1},{"b":2}]"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(1, root[0].GetProperty("a"u8).GetInt32());
        Assert.AreEqual(2, root[1].GetProperty("b"u8).GetInt32());
    }

    #endregion

    #region Escaped string handling

    [TestMethod]
    public void ParseEscapedPropertyName()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"hello\u0020world":"value"}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("value", root.GetProperty("hello world"u8).GetString());
    }

    [TestMethod]
    public void ParseEscapedStringValue()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                """{"key":"line1\nline2"}"""u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("line1\nline2", root.GetProperty("key"u8).GetString());
    }

    #endregion

    #region String overload

    [TestMethod]
    public void ParseFromString()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"x":42}""");

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(42, root.GetProperty("x"u8).GetInt32());
    }

    [TestMethod]
    public void ParseFromReadOnlyMemoryChar()
    {
        ReadOnlyMemory<char> json = """{"x":42}""".AsMemory();

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(42, root.GetProperty("x"u8).GetInt32());
    }

    #endregion

    #region Stream overload

    [TestMethod]
    public void ParseFromSeekableStream()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, new MemoryStream(data));

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
    }

    [TestMethod]
    public void ParseFromUnseekableStream()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(
                workspace,
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data));

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
    }

    [TestMethod]
    public void ParseFromStreamWithBom()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice"}""");
        byte[] dataWithBom = Utf8Bom.Concat(data).ToArray();

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, new MemoryStream(dataWithBom));

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
    }

    #endregion

    #region ParseValue from Utf8JsonReader

    [TestMethod]
    public void ParseValueFromReader()
    {
        byte[] data = Encoding.UTF8.GetBytes("""{"name":"Alice","age":30}""");
        var reader = new Utf8JsonReader(data);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
    }

    [TestMethod]
    public void ParseValueFromReaderAtPropertyName()
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
        Assert.AreEqual(42, root.GetProperty("inner"u8).GetInt32());
    }

    #endregion

    #region ParseValue from multi-segment ReadOnlySequence

    [TestMethod]
    public void ParseValueFromReader_MultiSegment_Object()
    {
        // Split the JSON across two segments to exercise HasValueSequence paths
        byte[] part1 = Encoding.UTF8.GetBytes("""{"name":"Ali""");
        byte[] part2 = Encoding.UTF8.GetBytes("""ce","age":30}""");

        ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
    }

    [TestMethod]
    public void ParseValueFromReader_MultiSegment_Array()
    {
        // Split array JSON across segments to hit multi-segment StartArray path
        byte[] part1 = Encoding.UTF8.GetBytes("[1,2,");
        byte[] part2 = Encoding.UTF8.GetBytes("3,4]");

        ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual("[1,2,3,4]", root.ToString());
    }

    [TestMethod]
    public void ParseValueFromReader_MultiSegment_NumberSplitAcrossSegments()
    {
        // Split a number value across segments so HasValueSequence is true for Number token
        byte[] part1 = Encoding.UTF8.GetBytes("123");
        byte[] part2 = Encoding.UTF8.GetBytes("456");

        ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.AreEqual("123456", builder.RootElement.ToString());
    }

    [TestMethod]
    public void ParseValueFromReader_MultiSegment_StringSplitAcrossSegments()
    {
        // Split a string value across segments to hit the multi-segment string path
        byte[] part1 = Encoding.UTF8.GetBytes("\"hel");
        byte[] part2 = Encoding.UTF8.GetBytes("lo\"");

        ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.AreEqual("hello", builder.RootElement.GetString());
    }

    [TestMethod]
    public void ParseValueFromReader_MultiSegment_TrueSplitAcrossSegments()
    {
        // Split 'true' across segments to hit HasValueSequence for True token
        byte[] part1 = Encoding.UTF8.GetBytes("tr");
        byte[] part2 = Encoding.UTF8.GetBytes("ue");

        ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.IsTrue(builder.RootElement.GetBoolean());
    }

    #endregion

    #region Mutation after parse

    [TestMethod]
    public void MutateAfterParse_SetProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Bob");
        Assert.AreEqual("Bob", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
    }

    [TestMethod]
    public void MutateAfterParse_RemoveProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        JsonElement.Mutable root = builder.RootElement;
        Assert.IsTrue(root.RemoveProperty("age"u8));
        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
        Assert.IsFalse(root.TryGetProperty("age"u8, out _));
    }

    [TestMethod]
    public void MutateAfterParse_InsertArrayItem()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "[1,2,3]"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        root.InsertItem(1, 99);
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(99, root[1].GetInt32());
        Assert.AreEqual(2, root[2].GetInt32());
        Assert.AreEqual(3, root[3].GetInt32());
    }

    #endregion

    #region Serialization equivalence

    [TestMethod]
    [DataRow("""42""")]
    [DataRow("\"hello\"")]
    [DataRow("""true""")]
    [DataRow("""false""")]
    [DataRow("""null""")]
    [DataRow("""{"a":1,"b":"two","c":true,"d":null}""")]
    [DataRow("""[1,"two",true,null,{"nested":42}]""")]
    [DataRow("""{"a":{"b":{"c":[1,2,3]}}}""")]
    [DataRow("""[]""")]
    [DataRow("""{}""")]
    [DataRow("""[{"x":1},{"x":2},{"x":3}]""")]
    [DataRow("""3.14159""")]
    [DataRow("""-42""")]
    [DataRow("""1e10""")]
    public void SerializationMatchesParseThenBuild(string json)
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

        Assert.AreEqual(traditionalResult, directResult);
    }

    #endregion

    #region Snapshot and restore

    [TestMethod]
    public void SnapshotAndRestoreAfterParse()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"name":"Alice","age":30}""");

        using JsonDocumentBuilderSnapshot<JsonElement.Mutable> snapshot = builder.CreateSnapshot();

        // Mutate
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("name"u8, "Bob");
        Assert.AreEqual("Bob", builder.RootElement.GetProperty("name"u8).GetString());

        // Restore
        builder.Restore(snapshot);
        Assert.AreEqual("Alice", builder.RootElement.GetProperty("name"u8).GetString());
    }

    #endregion

    #region Clone

    [TestMethod]
    public void CloneElementFromParsedBuilder()
    {
        JsonElement clone;

        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, """{"items":[1,2,3]}"""))
        {
            clone = builder.RootElement.GetProperty("items"u8).Clone();
        }

        // Clone survives after builder disposal
        Assert.AreEqual("[1,2,3]", clone.GetRawText());
    }

    #endregion

    #region Large document

    [TestMethod]
    public void ParseLargeDocument()
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
        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
        Assert.AreEqual(0, root.GetProperty("prop0"u8).GetInt32());
        Assert.AreEqual(999, root.GetProperty("prop999"u8).GetInt32());
    }

    [TestMethod]
    public void ParseDeeplyNestedDocument()
    {
        // Build a deeply nested JSON array [[[[...]]]]
        const int depth = 64;
        string json = new string('[', depth) + new string(']', depth);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        Assert.AreEqual(JsonValueKind.Array, builder.RootElement.ValueKind);
    }

    #endregion

    #region Floating-point precision

    [TestMethod]
    public void ParseFloatingPointNumber()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, "3.141592653589793"u8.ToArray());

        JsonElement.Mutable root = builder.RootElement;
        Assert.AreEqual(3.141592653589793, root.GetDouble());
    }

    #endregion

    #region WriteTo equivalence

    [TestMethod]
    [DataRow("""{"a":1,"b":"two","c":true,"d":null,"e":[1,2]}""")]
    [DataRow("""[1,"two",true,null,{"nested":42},[]]""")]
    public void WriteToProducesSameOutputAsParseThenBuild(string json)
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

        Assert.IsTrue(directBuffer.WrittenSpan.SequenceEqual(traditionalBuffer.WrittenSpan));
    }

    #endregion
}
