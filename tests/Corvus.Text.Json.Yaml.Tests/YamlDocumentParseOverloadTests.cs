// <copyright file="YamlDocumentParseOverloadTests.cs" company="Endjin Limited">
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
/// Tests for the Parse overloads: ReadOnlySequence, ReadOnlyMemory&lt;char&gt;, Stream, and ParseAsync.
/// </summary>
public class YamlDocumentParseOverloadTests
{
    private const string SimpleYaml = "name: hello\nvalue: 42\n";

    private const string NestedYaml = """
        root:
          child:
            key: value
          list:
            - one
            - two
            - three
        """;

    private const string LargeYaml = """
        items:
          - id: 1
            name: alpha
            tags: [a, b, c]
          - id: 2
            name: beta
            tags: [d, e, f]
          - id: 3
            name: gamma
            tags: [g, h, i]
          - id: 4
            name: delta
            tags: [j, k, l]
          - id: 5
            name: epsilon
            tags: [m, n, o]
        """;

    #region ReadOnlySequence<byte> overload

    [Fact]
    public void Parse_ReadOnlySequence_SingleSegment()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        ReadOnlySequence<byte> sequence = new(yaml);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public void Parse_ReadOnlySequence_MultiSegment()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        ReadOnlySequence<byte> sequence = CreateMultiSegmentSequence(yaml, 5);

        // Verify it's actually multi-segment
        Assert.False(sequence.IsSingleSegment);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public void Parse_ReadOnlySequence_NestedStructure()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(NestedYaml);
        ReadOnlySequence<byte> sequence = CreateMultiSegmentSequence(yaml, 10);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root").GetProperty("child").GetProperty("key").GetString());
        Assert.Equal(3, root.GetProperty("root").GetProperty("list").GetArrayLength());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root"u8).GetProperty("child"u8).GetProperty("key"u8).GetString());
        Assert.Equal(3, root.GetProperty("root"u8).GetProperty("list"u8).GetArrayLength());
#endif
    }

    [Fact]
    public void Parse_ReadOnlySequence_LargeContent()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(LargeYaml);
        ReadOnlySequence<byte> sequence = CreateMultiSegmentSequence(yaml, 3);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(5, root.GetProperty("items").GetArrayLength());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(sequence);
        JsonElement root = doc.RootElement;
        Assert.Equal(5, root.GetProperty("items"u8).GetArrayLength());
#endif
    }

    #endregion

    #region ReadOnlyMemory<char> overload

    [Fact]
    public void Parse_ReadOnlyMemoryChar_Simple()
    {
        ReadOnlyMemory<char> yaml = SimpleYaml.AsMemory();

#if STJ
        using JsonDocument doc = YamlDocument.Parse(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public void Parse_ReadOnlyMemoryChar_NestedStructure()
    {
        ReadOnlyMemory<char> yaml = NestedYaml.AsMemory();

#if STJ
        using JsonDocument doc = YamlDocument.Parse(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root").GetProperty("child").GetProperty("key").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root"u8).GetProperty("child"u8).GetProperty("key"u8).GetString());
#endif
    }

    [Fact]
    public void Parse_ReadOnlyMemoryChar_UnicodeContent()
    {
        ReadOnlyMemory<char> yaml = "greeting: héllo wörld\nemoji: 🎉\n".AsMemory();

#if STJ
        using JsonDocument doc = YamlDocument.Parse(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("héllo wörld", root.GetProperty("greeting").GetString());
        Assert.Equal("🎉", root.GetProperty("emoji").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("héllo wörld", root.GetProperty("greeting"u8).GetString());
        Assert.Equal("🎉", root.GetProperty("emoji"u8).GetString());
#endif
    }

    [Fact]
    public void Parse_ReadOnlyMemoryChar_Substring()
    {
        // Test with a substring memory (not starting from position 0)
        string padded = "PADDING" + SimpleYaml;
        ReadOnlyMemory<char> yaml = padded.AsMemory(7);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
#endif
    }

    [Fact]
    public void Parse_ReadOnlyMemoryChar_LargeContent()
    {
        // Generate content larger than StackallocByteThreshold (256 bytes)
        StringBuilder sb = new();
        sb.AppendLine("items:");
        for (int i = 0; i < 50; i++)
        {
            sb.AppendLine($"  - index: {i}");
            sb.AppendLine($"    description: \"Item number {i} with some padding text to increase size\"");
        }

        ReadOnlyMemory<char> yaml = sb.ToString().AsMemory();

#if STJ
        using JsonDocument doc = YamlDocument.Parse(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal(50, root.GetProperty("items").GetArrayLength());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml);
        JsonElement root = doc.RootElement;
        Assert.Equal(50, root.GetProperty("items"u8).GetArrayLength());
#endif
    }

    #endregion

    #region Stream overload (sync)

    [Fact]
    public void Parse_Stream_SeekableStream()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using MemoryStream stream = new(yaml);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public void Parse_Stream_NonSeekableStream()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using NonSeekableStream stream = new(yaml);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public void Parse_Stream_NestedStructure()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(NestedYaml);
        using MemoryStream stream = new(yaml);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root").GetProperty("child").GetProperty("key").GetString());
        Assert.Equal(3, root.GetProperty("root").GetProperty("list").GetArrayLength());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root"u8).GetProperty("child"u8).GetProperty("key"u8).GetString());
        Assert.Equal(3, root.GetProperty("root"u8).GetProperty("list"u8).GetArrayLength());
#endif
    }

    [Fact]
    public void Parse_Stream_LargeNonSeekable()
    {
        // Content larger than the initial 4096-byte buffer to test buffer growth
        StringBuilder sb = new();
        sb.AppendLine("data:");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"  key_{i}: \"value with enough padding to make this large {i}\"");
        }

        byte[] yaml = Encoding.UTF8.GetBytes(sb.ToString());
        using NonSeekableStream stream = new(yaml);

#if STJ
        using JsonDocument doc = YamlDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data"u8).ValueKind);
#endif
    }

    [Fact]
    public void Parse_Stream_PartiallyConsumed()
    {
        // Put extra bytes before the YAML and seek past them
        byte[] prefix = "GARBAGE"u8.ToArray();
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        byte[] combined = new byte[prefix.Length + yaml.Length];
        prefix.CopyTo(combined, 0);
        yaml.CopyTo(combined, prefix.Length);

        using MemoryStream stream = new(combined);
        stream.Position = prefix.Length;

#if STJ
        using JsonDocument doc = YamlDocument.Parse(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
#endif
    }

    [Fact]
    public void Parse_Stream_NullThrowsArgumentNullException()
    {
#if STJ
        Assert.Throws<ArgumentNullException>(() => YamlDocument.Parse((Stream)null!));
#else
        Assert.Throws<ArgumentNullException>(() => YamlDocument.Parse<JsonElement>((Stream)null!));
#endif
    }

    #endregion

    #region ParseAsync overload

    [Fact]
    public async Task ParseAsync_SeekableStream()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using MemoryStream stream = new(yaml);

#if STJ
        using JsonDocument doc = await YamlDocument.ParseAsync(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name").GetString());
        Assert.Equal(42, root.GetProperty("value").GetInt32());
#else
        using ParsedJsonDocument<JsonElement> doc = await YamlDocument.ParseAsync<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.ValueKind);
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
        Assert.Equal(42, root.GetProperty("value"u8).GetInt32());
#endif
    }

    [Fact]
    public async Task ParseAsync_NonSeekableStream()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using NonSeekableStream stream = new(yaml);

#if STJ
        using JsonDocument doc = await YamlDocument.ParseAsync(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = await YamlDocument.ParseAsync<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("name"u8).GetString());
#endif
    }

    [Fact]
    public async Task ParseAsync_NestedStructure()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(NestedYaml);
        using MemoryStream stream = new(yaml);

#if STJ
        using JsonDocument doc = await YamlDocument.ParseAsync(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root").GetProperty("child").GetProperty("key").GetString());
#else
        using ParsedJsonDocument<JsonElement> doc = await YamlDocument.ParseAsync<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("root"u8).GetProperty("child"u8).GetProperty("key"u8).GetString());
#endif
    }

    [Fact]
    public async Task ParseAsync_CancellationToken()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using MemoryStream stream = new(yaml);
        using CancellationTokenSource cts = new();

#if STJ
        using JsonDocument doc = await YamlDocument.ParseAsync(stream, default, cts.Token);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
#else
        using ParsedJsonDocument<JsonElement> doc = await YamlDocument.ParseAsync<JsonElement>(stream, default, cts.Token);
        Assert.Equal(JsonValueKind.Object, doc.RootElement.ValueKind);
#endif
    }

    [Fact]
    public async Task ParseAsync_CancelledToken_ThrowsOperationCanceled()
    {
        byte[] yaml = Encoding.UTF8.GetBytes(SimpleYaml);
        using NonSeekableStream stream = new(yaml);
        using CancellationTokenSource cts = new();
        cts.Cancel();

#if STJ
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => YamlDocument.ParseAsync(stream, default, cts.Token));
#else
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => YamlDocument.ParseAsync<JsonElement>(stream, default, cts.Token));
#endif
    }

    [Fact]
    public async Task ParseAsync_NullThrowsArgumentNullException()
    {
#if STJ
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => YamlDocument.ParseAsync((Stream)null!));
#else
        await Assert.ThrowsAsync<ArgumentNullException>(
            () => YamlDocument.ParseAsync<JsonElement>((Stream)null!));
#endif
    }

    [Fact]
    public async Task ParseAsync_LargeNonSeekable()
    {
        StringBuilder sb = new();
        sb.AppendLine("data:");
        for (int i = 0; i < 200; i++)
        {
            sb.AppendLine($"  key_{i}: \"value with enough padding to make this large {i}\"");
        }

        byte[] yaml = Encoding.UTF8.GetBytes(sb.ToString());
        using NonSeekableStream stream = new(yaml);

#if STJ
        using JsonDocument doc = await YamlDocument.ParseAsync(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data").ValueKind);
#else
        using ParsedJsonDocument<JsonElement> doc = await YamlDocument.ParseAsync<JsonElement>(stream);
        JsonElement root = doc.RootElement;
        Assert.Equal(JsonValueKind.Object, root.GetProperty("data"u8).ValueKind);
#endif
    }

    #endregion

    #region Cross-overload consistency

    [Fact]
    public async Task AllOverloads_ProduceSameResult()
    {
        const string yaml = "items:\n  - a\n  - b\n  - c\n";
        byte[] utf8Yaml = Encoding.UTF8.GetBytes(yaml);

        // Parse via each overload and compare
#if STJ
        using JsonDocument fromBytes = YamlDocument.Parse((ReadOnlyMemory<byte>)utf8Yaml);
        using JsonDocument fromString = YamlDocument.Parse(yaml);
        using JsonDocument fromCharMem = YamlDocument.Parse(yaml.AsMemory());
        using JsonDocument fromSequence = YamlDocument.Parse(new ReadOnlySequence<byte>(utf8Yaml));
        using MemoryStream ms = new(utf8Yaml);
        using JsonDocument fromStream = YamlDocument.Parse(ms);
        ms.Position = 0;
        using JsonDocument fromAsync = await YamlDocument.ParseAsync(ms);

        string expected = fromBytes.RootElement.GetRawText();
        Assert.Equal(expected, fromString.RootElement.GetRawText());
        Assert.Equal(expected, fromCharMem.RootElement.GetRawText());
        Assert.Equal(expected, fromSequence.RootElement.GetRawText());
        Assert.Equal(expected, fromStream.RootElement.GetRawText());
        Assert.Equal(expected, fromAsync.RootElement.GetRawText());
#else
        using ParsedJsonDocument<JsonElement> fromBytes = YamlDocument.Parse<JsonElement>((ReadOnlyMemory<byte>)utf8Yaml);
        using ParsedJsonDocument<JsonElement> fromString = YamlDocument.Parse<JsonElement>(yaml);
        using ParsedJsonDocument<JsonElement> fromCharMem = YamlDocument.Parse<JsonElement>(yaml.AsMemory());
        using ParsedJsonDocument<JsonElement> fromSequence = YamlDocument.Parse<JsonElement>(new ReadOnlySequence<byte>(utf8Yaml));
        using MemoryStream ms = new(utf8Yaml);
        using ParsedJsonDocument<JsonElement> fromStream = YamlDocument.Parse<JsonElement>(ms);
        ms.Position = 0;
        using ParsedJsonDocument<JsonElement> fromAsync = await YamlDocument.ParseAsync<JsonElement>(ms);

        string expected = fromBytes.RootElement.ToString();
        Assert.Equal(expected, fromString.RootElement.ToString());
        Assert.Equal(expected, fromCharMem.RootElement.ToString());
        Assert.Equal(expected, fromSequence.RootElement.ToString());
        Assert.Equal(expected, fromStream.RootElement.ToString());
        Assert.Equal(expected, fromAsync.RootElement.ToString());
#endif
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a multi-segment <see cref="ReadOnlySequence{T}"/> by splitting the data into
    /// chunks of the specified size.
    /// </summary>
    private static ReadOnlySequence<byte> CreateMultiSegmentSequence(byte[] data, int chunkSize)
    {
        if (data.Length <= chunkSize)
        {
            return new ReadOnlySequence<byte>(data);
        }

        SegmentNode? first = null;
        SegmentNode? last = null;

        int offset = 0;
        while (offset < data.Length)
        {
            int count = Math.Min(chunkSize, data.Length - offset);
            byte[] chunk = new byte[count];
            Array.Copy(data, offset, chunk, 0, count);

            SegmentNode node = new(chunk, last);
            if (first is null)
            {
                first = node;
            }

            last = node;
            offset += count;
        }

        return new ReadOnlySequence<byte>(
            first!,
            0,
            last!,
            last!.Memory.Length);
    }

    /// <summary>
    /// A linked-list segment for building multi-segment ReadOnlySequence.
    /// </summary>
    private sealed class SegmentNode : ReadOnlySequenceSegment<byte>
    {
        public SegmentNode(byte[] data, SegmentNode? previous)
        {
            Memory = data;
            if (previous is not null)
            {
                RunningIndex = previous.RunningIndex + previous.Memory.Length;
                previous.Next = this;
            }
        }
    }

    /// <summary>
    /// A non-seekable stream wrapper for testing the non-seekable stream path.
    /// </summary>
    private sealed class NonSeekableStream : Stream
    {
        private readonly MemoryStream inner;

        public NonSeekableStream(byte[] data)
        {
            this.inner = new MemoryStream(data);
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
            => this.inner.Read(buffer, offset, count);

#if NET
        public override int Read(Span<byte> buffer)
            => this.inner.Read(buffer);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this.inner.ReadAsync(buffer, cancellationToken);
        }
#endif

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return this.inner.ReadAsync(buffer, offset, count, cancellationToken);
        }

        public override void Flush() => this.inner.Flush();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.inner.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    #endregion
}
