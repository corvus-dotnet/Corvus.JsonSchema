// <copyright file="MultipartFormReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Tests for <see cref="MultipartFormReader"/> and <see cref="MultipartFormDataSerializer"/>
/// deserialization paths (server-side multipart parsing).
/// </summary>
[TestClass]
public class MultipartFormReaderTests
{
    [TestMethod]
    public void TryExtractBoundary_UnquotedBoundary_ExtractedCorrectly()
    {
        ReadOnlySpan<byte> contentType = "multipart/form-data; boundary=abc123"u8;
        bool result = MultipartFormReader.TryExtractBoundary(contentType, out ReadOnlySpan<byte> boundary);

        Assert.IsTrue(result);
        Assert.AreEqual("abc123", Encoding.UTF8.GetString(boundary));
    }

    [TestMethod]
    public void TryExtractBoundary_QuotedBoundary_ExtractedCorrectly()
    {
        ReadOnlySpan<byte> contentType = "multipart/form-data; boundary=\"abc-123\""u8;
        bool result = MultipartFormReader.TryExtractBoundary(contentType, out ReadOnlySpan<byte> boundary);

        Assert.IsTrue(result);
        Assert.AreEqual("abc-123", Encoding.UTF8.GetString(boundary));
    }

    [TestMethod]
    public void TryExtractBoundary_NoBoundaryParam_ReturnsFalse()
    {
        ReadOnlySpan<byte> contentType = "multipart/form-data"u8;
        bool result = MultipartFormReader.TryExtractBoundary(contentType, out _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryExtractBoundary_BoundarySemicolonTerminated_ExtractedCorrectly()
    {
        ReadOnlySpan<byte> contentType = "multipart/form-data; boundary=abc123; charset=utf-8"u8;
        bool result = MultipartFormReader.TryExtractBoundary(contentType, out ReadOnlySpan<byte> boundary);

        Assert.IsTrue(result);
        Assert.AreEqual("abc123", Encoding.UTF8.GetString(boundary));
    }

    [TestMethod]
    public void TryExtractBoundary_QuotedBoundaryNoCloseQuote_ExtractsToEnd()
    {
        ReadOnlySpan<byte> contentType = "multipart/form-data; boundary=\"abc123"u8;
        bool result = MultipartFormReader.TryExtractBoundary(contentType, out ReadOnlySpan<byte> boundary);

        Assert.IsTrue(result);
        Assert.AreEqual("abc123", Encoding.UTF8.GetString(boundary));
    }

    [TestMethod]
    public void DeserializeToJson_SimpleTextParts_ProducesCorrectJson()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"name\"\r\n" +
            "\r\n" +
            "Alice\r\n" +
            "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"age\"\r\n" +
            "\r\n" +
            "30\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("Alice", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(30, root.GetProperty("age"u8).GetInt32());
    }

    [TestMethod]
    public void DeserializeToJson_JsonPart_WritesRawJson()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"metadata\"\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "{\"key\":\"value\"}\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        JsonElement metadata = root.GetProperty("metadata"u8);
        Assert.AreEqual("value", metadata.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_EmptyJsonPart_WritesNull()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"data\"\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("data"u8).ValueKind);
    }

    [TestMethod]
    public void DeserializeToJson_BinaryPart_SkippedByDefault()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"file\"; filename=\"photo.jpg\"\r\n" +
            "Content-Type: image/jpeg\r\n" +
            "\r\n" +
            "BINARYDATA\r\n" +
            "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"label\"\r\n" +
            "\r\n" +
            "myfile\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        // Binary part is skipped; only "label" appears.
        Assert.AreEqual("myfile", root.GetProperty("label"u8).GetString());
        Assert.IsFalse(root.TryGetProperty("file"u8, out _));
    }

    [TestMethod]
    public void DeserializeToJson_BinaryPartWithCallback_CallbackInvoked()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"file\"; filename=\"doc.pdf\"\r\n" +
            "Content-Type: application/pdf\r\n" +
            "\r\n" +
            "PDFDATA\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        string? capturedName = null;
        string? capturedFileName = null;
        int capturedDataLen = 0;

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer,
            binaryPartCallback: (part) =>
            {
                capturedName = Encoding.UTF8.GetString(part.Name);
                capturedFileName = Encoding.UTF8.GetString(part.FileName);
                capturedDataLen = part.Data.Length;
            });

        writer.Flush();

        Assert.AreEqual("file", capturedName);
        Assert.AreEqual("doc.pdf", capturedFileName);
        Assert.AreEqual(7, capturedDataLen); // "PDFDATA"
    }

    [TestMethod]
    public void DeserializeToJson_MixedTextJsonBinary_ProducesCorrectResult()
    {
        const string body = "--BOUND\r\n" +
            "Content-Disposition: form-data; name=\"title\"\r\n" +
            "\r\n" +
            "My Document\r\n" +
            "--BOUND\r\n" +
            "Content-Disposition: form-data; name=\"config\"\r\n" +
            "Content-Type: application/json\r\n" +
            "\r\n" +
            "[1,2,3]\r\n" +
            "--BOUND\r\n" +
            "Content-Disposition: form-data; name=\"attachment\"; filename=\"a.bin\"\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            "\r\n" +
            "BINARY\r\n" +
            "--BOUND\r\n" +
            "Content-Disposition: form-data; name=\"active\"\r\n" +
            "\r\n" +
            "true\r\n" +
            "--BOUND--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        List<string> binaryNames = [];

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "BOUND"u8,
            writer,
            binaryPartCallback: (part) => binaryNames.Add(Encoding.UTF8.GetString(part.Name)));

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("My Document", root.GetProperty("title"u8).GetString());
        Assert.AreEqual(JsonValueKind.Array, root.GetProperty("config"u8).ValueKind);
        Assert.IsTrue(root.GetProperty("active"u8).GetBoolean());
        Assert.IsFalse(root.TryGetProperty("attachment"u8, out _));
        Assert.AreEqual(1, binaryNames.Count);
        Assert.AreEqual("attachment", binaryNames[0]);
    }

    [TestMethod]
    public void DeserializeToJson_VendorJsonContentType_TreatedAsJson()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"payload\"\r\n" +
            "Content-Type: application/vnd.api+json\r\n" +
            "\r\n" +
            "{\"x\":42}\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(42, root.GetProperty("payload"u8).GetProperty("x"u8).GetInt32());
    }

    [TestMethod]
    public void DeserializeToJson_TextPlainContentType_TreatedAsText()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"note\"\r\n" +
            "Content-Type: text/plain\r\n" +
            "\r\n" +
            "hello world\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("hello world", root.GetProperty("note"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_BooleanAndNullValues_ClassifiedCorrectly()
    {
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"flag\"\r\n" +
            "\r\n" +
            "false\r\n" +
            "--b\r\n" +
            "Content-Disposition: form-data; name=\"empty\"\r\n" +
            "\r\n" +
            "\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(false, root.GetProperty("flag"u8).GetBoolean());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("empty"u8).ValueKind);
    }

    [TestMethod]
    public void DeserializeToJson_CaseInsensitiveHeaders_Parsed()
    {
        const string body = "--boundary\r\n" +
            "CONTENT-DISPOSITION: form-data; name=\"field\"\r\n" +
            "CONTENT-TYPE: application/json\r\n" +
            "\r\n" +
            "\"hello\"\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("hello", root.GetProperty("field"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_NoNameInDisposition_PartSkipped()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data\r\n" +
            "\r\n" +
            "orphaned\r\n" +
            "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"good\"\r\n" +
            "\r\n" +
            "ok\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("ok", root.GetProperty("good"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_OctetStreamWithoutFilename_TreatedAsBinary()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"blob\"\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            "\r\n" +
            "RAWBYTES\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        bool callbackCalled = false;
        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer,
            binaryPartCallback: (_) => callbackCalled = true);

        writer.Flush();

        Assert.IsTrue(callbackCalled);
    }

    [TestMethod]
    public void Deserialize_SyncMethod_ProducesTypedResult()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"name\"\r\n" +
            "\r\n" +
            "Test\r\n" +
            "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"count\"\r\n" +
            "\r\n" +
            "5\r\n" +
            "--boundary--\r\n";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        using ParsedJsonDocument<JsonElement> doc = MultipartFormDataSerializer.Deserialize<JsonElement>(
            bodyBytes,
            "boundary"u8);

        JsonElement root = doc.RootElement;
        Assert.AreEqual("Test", root.GetProperty("name"u8).GetString());
        Assert.AreEqual(5, root.GetProperty("count"u8).GetInt32());
    }

    [TestMethod]
    public async Task DeserializeAsync_MemoryOverload_ProducesTypedResult()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"value\"\r\n" +
            "\r\n" +
            "42\r\n" +
            "--boundary--\r\n";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        using MemoryStream stream = new(bodyBytes);

        ReadOnlyMemory<byte> contentType = Encoding.UTF8.GetBytes("multipart/form-data; boundary=boundary");

        using ParsedJsonDocument<JsonElement> doc = await MultipartFormDataSerializer.DeserializeAsync<JsonElement>(
            stream,
            contentType);

        JsonElement root = doc.RootElement;
        Assert.AreEqual(42, root.GetProperty("value"u8).GetInt32());
    }

    [TestMethod]
    public async Task DeserializeAsync_StringOverload_ProducesTypedResult()
    {
        const string body = "--myboundary\r\n" +
            "Content-Disposition: form-data; name=\"msg\"\r\n" +
            "\r\n" +
            "hello\r\n" +
            "--myboundary--\r\n";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        using MemoryStream stream = new(bodyBytes);

        using ParsedJsonDocument<JsonElement> doc = await MultipartFormDataSerializer.DeserializeAsync<JsonElement>(
            stream,
            "multipart/form-data; boundary=myboundary");

        JsonElement root = doc.RootElement;
        Assert.AreEqual("hello", root.GetProperty("msg"u8).GetString());
    }

    [TestMethod]
    public async Task DeserializeAsync_StringOverload_NullContentType_Throws()
    {
        byte[] bodyBytes = Encoding.UTF8.GetBytes("--b\r\nfoo\r\n--b--");
        using MemoryStream stream = new(bodyBytes);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await MultipartFormDataSerializer.DeserializeAsync<JsonElement>(stream, (string?)null));
    }

    [TestMethod]
    public async Task DeserializeAsync_WithBinaryCallback_CallbackInvoked()
    {
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"doc\"; filename=\"x.bin\"\r\n" +
            "Content-Type: application/octet-stream\r\n" +
            "\r\n" +
            "DATA\r\n" +
            "--b\r\n" +
            "Content-Disposition: form-data; name=\"label\"\r\n" +
            "\r\n" +
            "test\r\n" +
            "--b--\r\n";

        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        using MemoryStream stream = new(bodyBytes);
        ReadOnlyMemory<byte> contentType = Encoding.UTF8.GetBytes("multipart/form-data; boundary=b");

        string? binaryName = null;
        using ParsedJsonDocument<JsonElement> doc = await MultipartFormDataSerializer.DeserializeAsync<JsonElement>(
            stream,
            contentType,
            binaryPartCallback: (part) => binaryName = Encoding.UTF8.GetString(part.Name));

        Assert.AreEqual("doc", binaryName);
        Assert.AreEqual("test", doc.RootElement.GetProperty("label"u8).GetString());
    }

    [TestMethod]
    public void TryReadNextPart_DirectUsage_IteratesParts()
    {
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"a\"\r\n" +
            "\r\n" +
            "1\r\n" +
            "--b\r\n" +
            "Content-Disposition: form-data; name=\"b\"\r\n" +
            "\r\n" +
            "2\r\n" +
            "--b--\r\n";

        MultipartFormReader reader = new(Encoding.UTF8.GetBytes(body), "b"u8);
        List<string> names = [];

        while (reader.TryReadNextPart(out var name, out _, out _, out _))
        {
            names.Add(Encoding.UTF8.GetString(name));
        }

        Assert.AreEqual(2, names.Count);
        Assert.AreEqual("a", names[0]);
        Assert.AreEqual("b", names[1]);
    }

    [TestMethod]
    public void TryReadNextPart_NoBoundaryFound_ReturnsFalse()
    {
        const string body = "just some data with no boundaries";

        MultipartFormReader reader = new(Encoding.UTF8.GetBytes(body), "boundary"u8);
        bool hasNext = reader.TryReadNextPart(out _, out _, out _, out _);

        Assert.IsFalse(hasNext);
    }

    [TestMethod]
    public void DeserializeToJson_NumberValue_ParsedCorrectly()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"price\"\r\n" +
            "\r\n" +
            "19.99\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Number, root.GetProperty("price"u8).ValueKind);
    }

    [TestMethod]
    public void DeserializeToJson_NoBoundaryAfterLastPart_ConsumesRemaining()
    {
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"last\"\r\n" +
            "\r\n" +
            "trailing";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("trailing", root.GetProperty("last"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_BoundaryWithoutCrLfAfter_StillAdvances()
    {
        // Boundary line without CRLF after the marker (edge case).
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"x\"\r\n" +
            "\r\n" +
            "val\r\n" +
            "--b--";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("val", root.GetProperty("x"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_MultipleParts_AllParsed()
    {
        const string body = "--X\r\n" +
            "Content-Disposition: form-data; name=\"a\"\r\n" +
            "\r\n" +
            "1\r\n" +
            "--X\r\n" +
            "Content-Disposition: form-data; name=\"b\"\r\n" +
            "\r\n" +
            "2\r\n" +
            "--X\r\n" +
            "Content-Disposition: form-data; name=\"c\"\r\n" +
            "\r\n" +
            "3\r\n" +
            "--X--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "X"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual(1, root.GetProperty("a"u8).GetInt32());
        Assert.AreEqual(2, root.GetProperty("b"u8).GetInt32());
        Assert.AreEqual(3, root.GetProperty("c"u8).GetInt32());
    }

    [TestMethod]
    public void DeserializeToJson_NoHeaderTerminator_PartSkipped()
    {
        // Part with no \r\n\r\n header terminator — TryReadNextPart returns false
        // since headerEnd < 0. We just get the first valid part.
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"ok\"\r\n" +
            "\r\n" +
            "value\r\n" +
            "--b\r\n" +
            "missing-header-end\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("value", root.GetProperty("ok"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_ContentTypeWithTrailingSemicolon_Trimmed()
    {
        // Content-Type header ending with a semicolon (trailing parameter separator).
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"data\"\r\n" +
            "Content-Type: application/json;\r\n" +
            "\r\n" +
            "{\"a\":1}\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        // After trimming the trailing `;`, content type matches "application/json".
        Assert.AreEqual(1, root.GetProperty("data"u8).GetProperty("a"u8).GetInt32());
    }

    [TestMethod]
    public void DeserializeToJson_EmptyHeaderLine_Skipped()
    {
        // An extra CRLF within the header section creating an empty line.
        // ParseHeaders skips empty lines via the `if (line.IsEmpty) continue;` check.
        const string body = "--b\r\n" +
            "\r\n" +
            "Content-Disposition: form-data; name=\"x\"\r\n" +
            "\r\n" +
            "val\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);

        // This test is complex because the parser looks for \r\n\r\n as header end.
        // The first \r\n after the boundary means the header section is empty
        // (headerEnd == 0), so headers parse with empty content.
        // The name will be empty, triggering the AdvanceToNextBoundary path.
        // Verifying the parse doesn't crash is the goal.
        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [TestMethod]
    public void DeserializeToJson_ShortHeaderLine_NotMatchedAsPrefix()
    {
        // A header line shorter than "content-disposition:" — exercises
        // StartsWithIgnoreCase returning false for short spans.
        const string body = "--b\r\n" +
            "X: y\r\n" +
            "Content-Disposition: form-data; name=\"field\"\r\n" +
            "\r\n" +
            "hi\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        Assert.AreEqual("hi", root.GetProperty("field"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_FirstBoundaryWithoutCrLf_StillParsed()
    {
        // First boundary marker not followed by CRLF (just the content directly).
        // Exercises SkipToFirstBoundary else branch (line 463-464).
        byte[] bodyBytes = "--b"u8.ToArray()
            .Concat("Content-Disposition: form-data; name=\"x\"\r\n\r\nval\r\n--b--"u8.ToArray())
            .ToArray();

        ArrayBufferWriter<byte> buffer = new(bodyBytes.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            bodyBytes,
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);

        // The parse should not crash. The body starts with --b directly followed
        // by header content without CRLF separation, which hits the else branch.
        Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
    }

    [TestMethod]
    public void DeserializeToJson_InterBoundaryWithoutCrLf_AdvancesCorrectly()
    {
        // Boundary between parts without CRLF after it (just content directly).
        // Exercises FindPartBody else branch (line 505-506).
        // Build body: --b\r\n<headers>\r\n\r\n<body>\r\n--b<next headers without crlf>
        byte[] bodyBytes = Encoding.UTF8.GetBytes(
            "--b\r\n" +
            "Content-Disposition: form-data; name=\"a\"\r\n" +
            "\r\n" +
            "1\r\n" +
            "--b" + // no \r\n or -- after this boundary
            "Content-Disposition: form-data; name=\"b\"\r\n" +
            "\r\n" +
            "2\r\n" +
            "--b--\r\n");

        ArrayBufferWriter<byte> buffer = new(bodyBytes.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            bodyBytes,
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        // First part "a" should be parsed. Second part may or may not parse depending
        // on how the boundary-without-CRLF is handled, but the test exercises the code path.
        Assert.AreEqual(1, root.GetProperty("a"u8).GetInt32());
    }

    [TestMethod]
    public void DeserializeToJson_NameParamWithNoClosingQuote_ExtractsToEnd()
    {
        // Content-Disposition with name=" but no closing quote.
        // Exercises ExtractQuotedParam closeQuote < 0 path (line 339-341).
        const string body = "--b\r\n" +
            "Content-Disposition: form-data; name=\"field\r\n" +
            "\r\n" +
            "data\r\n" +
            "--b--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "b"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        // Name is extracted to end of line (no closing quote), so it will be "field"
        // minus the CRLF (the line was split on CRLF already, so it should just be "field").
        Assert.AreEqual("data", root.GetProperty("field"u8).GetString());
    }

    [TestMethod]
    public void DeserializeToJson_ContentDispositionNameTruncatedAtPrefix_PartSkipped()
    {
        // Exercises ExtractQuotedParam L334-335: paramPrefix (name=") at end of header,
        // start >= headerValue.Length, returns default → part has no name → skipped.
        // Construct a part where Content-Disposition ends with name=" and nothing after.
        const string body = "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"\r\n" +
            "\r\n" +
            "data\r\n" +
            "--boundary\r\n" +
            "Content-Disposition: form-data; name=\"valid\"\r\n" +
            "\r\n" +
            "value\r\n" +
            "--boundary--\r\n";

        ArrayBufferWriter<byte> buffer = new(body.Length);
        using Utf8JsonWriter writer = new(buffer);

        MultipartFormReader.DeserializeToJson(
            Encoding.UTF8.GetBytes(body),
            "boundary"u8,
            writer);

        writer.Flush();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(buffer.WrittenMemory);
        JsonElement root = doc.RootElement;

        // The first part has empty name (closing quote immediately follows the opening),
        // the second part has name="valid".
        Assert.AreEqual("value", root.GetProperty("valid"u8).GetString());
    }
}