// <copyright file="ChunkedTextReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A <see cref="TextReader"/> over the chunks of a <see cref="GeneratedCodeFile"/>, so that consumers that read text
/// (for example Roslyn's <c>SourceText.From(TextReader, int, Encoding)</c>) never need the file as one string.
/// </summary>
internal sealed class ChunkedTextReader(IReadOnlyList<ReadOnlyMemory<char>> chunks) : TextReader
{
    private int chunkIndex;
    private int offset;

    /// <inheritdoc/>
    public override int Peek()
    {
        this.SkipEmptyChunks();
        return this.chunkIndex < chunks.Count ? chunks[this.chunkIndex].Span[this.offset] : -1;
    }

    /// <inheritdoc/>
    public override int Read()
    {
        this.SkipEmptyChunks();
        if (this.chunkIndex >= chunks.Count)
        {
            return -1;
        }

        char c = chunks[this.chunkIndex].Span[this.offset++];
        return c;
    }

    /// <inheritdoc/>
    public override int Read(char[] buffer, int index, int count)
    {
        return this.ReadCore(buffer.AsSpan(index, count));
    }

#if NET8_0_OR_GREATER
    /// <inheritdoc/>
    public override int Read(Span<char> buffer)
    {
        return this.ReadCore(buffer);
    }
#endif

    private int ReadCore(Span<char> buffer)
    {
        int written = 0;
        while (written < buffer.Length)
        {
            this.SkipEmptyChunks();
            if (this.chunkIndex >= chunks.Count)
            {
                break;
            }

            ReadOnlySpan<char> remaining = chunks[this.chunkIndex].Span[this.offset..];
            int take = Math.Min(remaining.Length, buffer.Length - written);
            remaining[..take].CopyTo(buffer[written..]);
            written += take;
            this.offset += take;
        }

        return written;
    }

    private void SkipEmptyChunks()
    {
        while (this.chunkIndex < chunks.Count && this.offset >= chunks[this.chunkIndex].Length)
        {
            this.chunkIndex++;
            this.offset = 0;
        }
    }
}