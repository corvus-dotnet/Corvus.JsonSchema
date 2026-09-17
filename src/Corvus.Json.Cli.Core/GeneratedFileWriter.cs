// <copyright file="GeneratedFileWriter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Text;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Writes generated code files to disk from their chunks, as UTF-8 without a byte order mark (the bytes
/// <see cref="File.WriteAllText(string, string)"/> writes), through one pooled byte buffer and an unbuffered stream, so
/// that a file costs no string, no per-file writer buffers and no large-object-heap allocation.
/// </summary>
internal static class GeneratedFileWriter
{
    private const int BufferSize = 64 * 1024;
    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    /// <summary>
    /// Writes a generated file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="path">The path to write to.</param>
    public static void Write(GeneratedCodeFile file, string path)
    {
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 1);
            Encoder encoder = Utf8NoBom.GetEncoder();
            foreach (ReadOnlyMemory<char> chunk in file.Chunks)
            {
                ReadOnlySpan<char> remaining = chunk.Span;
                while (remaining.Length > 0)
                {
                    encoder.Convert(remaining, buffer, flush: false, out int charsUsed, out int bytesUsed, out _);
                    stream.Write(buffer, 0, bytesUsed);
                    remaining = remaining[charsUsed..];
                }
            }

            encoder.Convert([], buffer, flush: true, out _, out int tail, out _);
            if (tail > 0)
            {
                stream.Write(buffer, 0, tail);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>
    /// Writes a generated file.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="path">The path to write to.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    public static Task WriteAsync(GeneratedCodeFile file, string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Write(file, path);
        return Task.CompletedTask;
    }
}