// <copyright file="StreamingMultipartReaderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi32.Server.Runtime.Tests;

/// <summary>
/// Tests for <see cref="StreamingMultipartReader"/>: wire-order part traversal,
/// bounded part body streams, and boundary detection across arbitrary chunk splits.
/// </summary>
[TestClass]
public class StreamingMultipartReaderTests
{
    private const string Boundary = "sb";

    private static byte[] BuildBody(params (string Headers, byte[] Body)[] parts)
    {
        using MemoryStream ms = new();
        foreach ((string headers, byte[] body) in parts)
        {
            ms.Write(Encoding.UTF8.GetBytes($"--{Boundary}\r\n{headers}\r\n\r\n"));
            ms.Write(body);
            ms.Write("\r\n"u8);
        }

        ms.Write(Encoding.UTF8.GetBytes($"--{Boundary}--\r\n"));
        return ms.ToArray();
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream, int readSize = 11)
    {
        using MemoryStream ms = new();
        byte[] buf = new byte[readSize];
        int n;
        while ((n = await stream.ReadAsync(buf)) > 0)
        {
            ms.Write(buf, 0, n);
        }

        return ms.ToArray();
    }

    [TestMethod]
    public async Task Reader_TraversesPartsInWireOrder_AtEveryChunkSize()
    {
        byte[] fileBytes = new byte[300];
        for (int i = 0; i < fileBytes.Length; i++)
        {
            fileBytes[i] = (byte)(i % 251);
        }

        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"caption\"", Encoding.UTF8.GetBytes("hello")),
            ("Content-Disposition: form-data; name=\"file\"; filename=\"f.bin\"\r\nContent-Type: application/octet-stream", fileBytes));

        for (int chunk = 1; chunk <= 40; chunk++)
        {
            using ChunkedStream source = new(body, chunk);
            await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

            Assert.IsTrue(await reader.MoveNextPartAsync(), $"chunk={chunk}: first part expected");
            Assert.AreEqual("caption", Encoding.UTF8.GetString(reader.CurrentName), $"chunk={chunk}");
            Assert.IsFalse(reader.CurrentIsBinary, $"chunk={chunk}");
            CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("hello"), await ReadAllAsync(reader.CurrentBodyStream), $"chunk={chunk}: caption body");

            Assert.IsTrue(await reader.MoveNextPartAsync(), $"chunk={chunk}: second part expected");
            Assert.AreEqual("file", Encoding.UTF8.GetString(reader.CurrentName), $"chunk={chunk}");
            Assert.AreEqual("f.bin", Encoding.UTF8.GetString(reader.CurrentFileName), $"chunk={chunk}");
            Assert.IsTrue(reader.CurrentIsBinary, $"chunk={chunk}");
            CollectionAssert.AreEqual(fileBytes, await ReadAllAsync(reader.CurrentBodyStream), $"chunk={chunk}: file body");

            Assert.IsFalse(await reader.MoveNextPartAsync(), $"chunk={chunk}: end expected");
        }
    }

    [TestMethod]
    public async Task Reader_BodyContainingDelimiterPrefixes_IsDeliveredIntact()
    {
        // Payload riddled with near-delimiter sequences to stress tail retention.
        using MemoryStream payload = new();
        for (int i = 0; i < 50; i++)
        {
            payload.Write("\r\n--s"u8);
            payload.Write("\r\n--"u8);
            payload.Write(Encoding.UTF8.GetBytes($"x{i}"));
            payload.Write("\r"u8);
        }

        byte[] tricky = payload.ToArray();
        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"blob\"\r\nContent-Type: application/octet-stream", tricky));

        for (int chunk = 1; chunk <= 17; chunk++)
        {
            using ChunkedStream source = new(body, chunk);
            await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

            Assert.IsTrue(await reader.MoveNextPartAsync());
            CollectionAssert.AreEqual(tricky, await ReadAllAsync(reader.CurrentBodyStream, 5), $"chunk={chunk}");
            Assert.IsFalse(await reader.MoveNextPartAsync());
        }
    }

    [TestMethod]
    public async Task Reader_PartLargerThanWorkingBuffer_Streams()
    {
        byte[] large = new byte[128 * 1024];
        Random.Shared.NextBytes(large);

        // Random bytes can contain CR/LF runs; delimiter collisions are what the scan
        // must survive. Use a small working buffer to force many refills.
        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"big\"; filename=\"big.bin\"\r\nContent-Type: application/octet-stream", large));

        using ChunkedStream source = new(body, 4096);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary), bufferSize: 256);

        Assert.IsTrue(await reader.MoveNextPartAsync());
        CollectionAssert.AreEqual(large, await ReadAllAsync(reader.CurrentBodyStream, 1024));
        Assert.IsFalse(await reader.MoveNextPartAsync());
    }

    [TestMethod]
    public async Task Reader_MoveNext_DrainsUnreadBody()
    {
        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"skipme\"", new byte[9000]),
            ("Content-Disposition: form-data; name=\"wanted\"", Encoding.UTF8.GetBytes("value")));

        using ChunkedStream source = new(body, 33);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("skipme", Encoding.UTF8.GetString(reader.CurrentName));

        // Do not read the body; the next MoveNext must drain it.
        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("wanted", Encoding.UTF8.GetString(reader.CurrentName));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("value"), await ReadAllAsync(reader.CurrentBodyStream));
        Assert.IsFalse(await reader.MoveNextPartAsync());
    }

    [TestMethod]
    public async Task Reader_PreambleBeforeFirstBoundary_IsSkipped()
    {
        byte[] wellFormed = BuildBody(
            ("Content-Disposition: form-data; name=\"k\"", Encoding.UTF8.GetBytes("v")));
        byte[] body = [.. Encoding.UTF8.GetBytes("this is preamble junk\r\n"), .. wellFormed];

        using ChunkedStream source = new(body, 7);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("k", Encoding.UTF8.GetString(reader.CurrentName));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("v"), await ReadAllAsync(reader.CurrentBodyStream));
        Assert.IsFalse(await reader.MoveNextPartAsync());
    }

    [TestMethod]
    public async Task Reader_EmptyPartBody_YieldsEmpty()
    {
        byte[] body = BuildBody(
            ("Content-Disposition: form-data; name=\"empty\"", []),
            ("Content-Disposition: form-data; name=\"after\"", Encoding.UTF8.GetBytes("x")));

        using ChunkedStream source = new(body, 3);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual(0, (await ReadAllAsync(reader.CurrentBodyStream)).Length);
        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("after", Encoding.UTF8.GetString(reader.CurrentName));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("x"), await ReadAllAsync(reader.CurrentBodyStream));
        Assert.IsFalse(await reader.MoveNextPartAsync());
    }

    [TestMethod]
    public async Task Reader_TruncatedBody_Throws()
    {
        byte[] wellFormed = BuildBody(
            ("Content-Disposition: form-data; name=\"k\"", new byte[500]));
        byte[] truncated = wellFormed[..^30];

        using ChunkedStream source = new(truncated, 13);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

        Assert.IsTrue(await reader.MoveNextPartAsync());
        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await ReadAllAsync(reader.CurrentBodyStream));
    }

    [TestMethod]
    public async Task Reader_OversizedHeaderBlock_Throws()
    {
        string bigHeader = "X-Padding: " + new string('a', 70_000);
        byte[] body = BuildBody(
            ($"Content-Disposition: form-data; name=\"k\"\r\n{bigHeader}", Encoding.UTF8.GetBytes("v")));

        using ChunkedStream source = new(body, 4096);
        await using StreamingMultipartReader reader = new(source, Encoding.UTF8.GetBytes(Boundary));

        await Assert.ThrowsExactlyAsync<InvalidDataException>(
            async () => await reader.MoveNextPartAsync());
    }

    [TestMethod]
    public async Task Reader_SerializerRoundTrip_BinaryLastWire()
    {
        // The client serializer's output (binary parts last) is read back part for part.
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(
            """{"file":"placeholder","caption":"round trip"}""");
        byte[] payload = new byte[10_000];
        Random.Shared.NextBytes(payload);

        Dictionary<string, BinaryPartData> binaryParts = new(StringComparer.Ordinal)
        {
            ["file"] = new BinaryPartData(
                WriteContentAsync: async (stream, ct) => await stream.WriteAsync(payload, ct),
                ContentType: "application/octet-stream",
                FileName: "p.bin"),
        };

        using MemoryStream wire = new();
        await MultipartFormDataSerializer.SerializeAsync(doc.RootElement, wire, "rt-boundary", null, binaryParts);
        wire.Position = 0;

        using ChunkedStream source = new(wire.ToArray(), 19);
        await using StreamingMultipartReader reader = new(source, "rt-boundary"u8);

        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("caption", Encoding.UTF8.GetString(reader.CurrentName));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes("round trip"), await ReadAllAsync(reader.CurrentBodyStream));

        Assert.IsTrue(await reader.MoveNextPartAsync());
        Assert.AreEqual("file", Encoding.UTF8.GetString(reader.CurrentName));
        Assert.IsTrue(reader.CurrentIsBinary);
        CollectionAssert.AreEqual(payload, await ReadAllAsync(reader.CurrentBodyStream, 777));

        Assert.IsFalse(await reader.MoveNextPartAsync());
    }

    private sealed class ChunkedStream : Stream
    {
        private readonly byte[] data;
        private readonly int chunk;
        private int position;

        public ChunkedStream(byte[] data, int chunk)
        {
            this.data = data;
            this.chunk = chunk;
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(Math.Min(count, this.chunk), this.data.Length - this.position);
            Array.Copy(this.data, this.position, buffer, offset, n);
            this.position += n;
            return n;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            int n = Math.Min(Math.Min(buffer.Length, this.chunk), this.data.Length - this.position);
            this.data.AsSpan(this.position, n).CopyTo(buffer.Span);
            this.position += n;
            return new ValueTask<int>(n);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}