// <copyright file="GeneratedCodeFile.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A generated code file.
/// </summary>
/// <remarks>
/// The content is held either as a string or as chunks of at most <see cref="ChunkSize"/> characters (below the
/// large object heap threshold). <see cref="FileContent"/> materialises the string on first use; writers that can
/// take chunks (<see cref="WriteTo(TextWriter)"/>, <see cref="Chunks"/>) never need it.
/// </remarks>
[DebuggerDisplay("{FileName}")]
public class GeneratedCodeFile
{
    /// <summary>
    /// The largest chunk, in characters: 80 KB of UTF-16, below the large object heap threshold and the size Roslyn
    /// uses for its own large source texts.
    /// </summary>
    public const int ChunkSize = 40 * 1024;

    private readonly ReadOnlyMemory<char>[] chunks;
    private string? fileContent;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedCodeFile"/> class from a string.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="fileContent">The file content.</param>
    /// <param name="typeDeclaration">The type declaration for which the file was generated, if any.</param>
    public GeneratedCodeFile(string fileName, string fileContent, TypeDeclaration? typeDeclaration = null)
    {
        this.FileName = fileName;
        this.fileContent = fileContent;
        this.chunks = fileContent.Length == 0 ? [] : [fileContent.AsMemory()];
        this.Length = fileContent.Length;
        this.TypeDeclaration = typeDeclaration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneratedCodeFile"/> class from chunks of text.
    /// </summary>
    /// <param name="fileName">The file name.</param>
    /// <param name="chunks">The content, in order; each chunk holds at most <see cref="ChunkSize"/> characters.</param>
    /// <param name="typeDeclaration">The type declaration for which the file was generated, if any.</param>
    public GeneratedCodeFile(string fileName, ReadOnlyMemory<char>[] chunks, TypeDeclaration? typeDeclaration = null)
    {
        this.FileName = fileName;
        this.chunks = chunks;
        int length = 0;
        foreach (ReadOnlyMemory<char> chunk in chunks)
        {
            length += chunk.Length;
        }

        this.Length = length;
        this.TypeDeclaration = typeDeclaration;
    }

    /// <summary>
    /// Gets the type declaration for which the file was generated, if any.
    /// </summary>
    public TypeDeclaration? TypeDeclaration { get; }

    /// <summary>
    /// Gets the file name.
    /// </summary>
    public string FileName { get; }

    /// <summary>
    /// Gets the file content as a string, built on first use from the chunks and then kept.
    /// </summary>
    public string FileContent => this.fileContent ??= this.BuildString();

    /// <summary>
    /// Gets the length of the content in characters.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the content as chunks of at most <see cref="ChunkSize"/> characters, in order.
    /// </summary>
    public IReadOnlyList<ReadOnlyMemory<char>> Chunks => this.chunks;

    /// <summary>
    /// Opens a reader over the content without materialising the string.
    /// </summary>
    /// <returns>A reader positioned at the start of the content.</returns>
    public TextReader OpenReader()
    {
        return new ChunkedTextReader(this.chunks);
    }

    /// <summary>
    /// Writes the content to a writer without materialising the string.
    /// </summary>
    /// <param name="writer">The writer.</param>
    public void WriteTo(TextWriter writer)
    {
        foreach (ReadOnlyMemory<char> chunk in this.chunks)
        {
#if NET8_0_OR_GREATER
            writer.Write(chunk.Span);
#else
            if (MemoryMarshal.TryGetArray(chunk, out ArraySegment<char> segment))
            {
                writer.Write(segment.Array!, segment.Offset, segment.Count);
            }
            else
            {
                writer.Write(chunk.ToString());
            }
#endif
        }
    }

    private string BuildString()
    {
        if (this.chunks.Length == 1)
        {
            // A chunk that is a whole string (the string-capture mode, or the string constructor) is the content itself.
            ReadOnlyMemory<char> only = this.chunks[0];
            return MemoryMarshal.TryGetString(only, out string? text, out int start, out int count) && start == 0 && count == text.Length
                ? text
                : only.ToString();
        }

#if NET8_0_OR_GREATER
        return string.Create(this.Length, this.chunks, static (span, chunks) =>
        {
            foreach (ReadOnlyMemory<char> chunk in chunks)
            {
                chunk.Span.CopyTo(span);
                span = span[chunk.Length..];
            }
        });
#else
        char[] buffer = new char[this.Length];
        int offset = 0;
        foreach (ReadOnlyMemory<char> chunk in this.chunks)
        {
            chunk.CopyTo(buffer.AsMemory(offset));
            offset += chunk.Length;
        }

        return new string(buffer);
#endif
    }
}