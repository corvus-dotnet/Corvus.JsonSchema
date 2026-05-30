// <copyright file="JsonStreamWriterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.IO.Pipelines;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

[TestClass]
public sealed class JsonStreamWriterTests
{
    [TestMethod]
    public async Task WriteItemAsync_WithServerSentEventsContentType_WritesSseDataFrames()
    {
        Pipe pipe = new();
        Utf8JsonWriter jsonWriter = new(pipe.Writer);
        JsonStreamWriter streamWriter = new(pipe.Writer, jsonWriter, "text/event-stream");

        await streamWriter.WriteItemAsync(JsonElement.ParseValue("""{"message":"hello"}"""u8));
        await streamWriter.WriteItemAsync(JsonElement.ParseValue("""{"message":"goodbye"}"""u8));
        await pipe.Writer.CompleteAsync();

        ReadResult readResult = await pipe.Reader.ReadAsync();
        string body = System.Text.Encoding.UTF8.GetString(readResult.Buffer.ToArray());

        Assert.AreEqual("data: {\"message\":\"hello\"}\n\ndata: {\"message\":\"goodbye\"}\n\n", body);
        pipe.Reader.AdvanceTo(readResult.Buffer.End);
        await pipe.Reader.CompleteAsync();
    }

    [TestMethod]
    public async Task WriteItemAsync_WithNdjsonContentType_WritesNewlineDelimitedJson()
    {
        Pipe pipe = new();
        Utf8JsonWriter jsonWriter = new(pipe.Writer);
        JsonStreamWriter streamWriter = new(pipe.Writer, jsonWriter, "application/x-ndjson");

        await streamWriter.WriteItemAsync(JsonElement.ParseValue("""{"message":"hello"}"""u8));
        await streamWriter.WriteItemAsync(JsonElement.ParseValue("""{"message":"goodbye"}"""u8));
        await pipe.Writer.CompleteAsync();

        ReadResult readResult = await pipe.Reader.ReadAsync();
        string body = System.Text.Encoding.UTF8.GetString(readResult.Buffer.ToArray());

        Assert.AreEqual("{\"message\":\"hello\"}\n{\"message\":\"goodbye\"}\n", body);
        pipe.Reader.AdvanceTo(readResult.Buffer.End);
        await pipe.Reader.CompleteAsync();
    }
}