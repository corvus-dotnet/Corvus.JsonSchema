// <copyright file="ParsedJsonDocument.SourceLocation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class ParsedJsonDocument<T>
{
    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffset(int index, out int line, out int charOffset, out long lineByteOffset)
    {
        CheckNotDisposed();

        DbRow row = _parsedData.Get(index);
        int byteOffset = row.LocationOrIndex;

        return TryGetLineAndOffsetForByteOffset(_utf8Json.Span, byteOffset, out line, out charOffset, out lineByteOffset);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLineAndOffsetForPointer(ReadOnlySpan<byte> jsonPointer, int index, out int line, out int charOffset, out long lineByteOffset)
    {
        CheckNotDisposed();

        if (!TryResolveJsonPointerUnsafe(jsonPointer, index, out int valueIndex))
        {
            line = 0;
            charOffset = 0;
            lineByteOffset = 0;
            return false;
        }

        DbRow row = _parsedData.Get(valueIndex);
        int byteOffset = row.LocationOrIndex;

        return TryGetLineAndOffsetForByteOffset(_utf8Json.Span, byteOffset, out line, out charOffset, out lineByteOffset);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line)
    {
        CheckNotDisposed();

        if (lineNumber < 1 || _utf8Json.Length == 0)
        {
            line = default;
            return false;
        }

        ReadOnlySpan<byte> source = _utf8Json.Span;
        int currentLine = 1;
        int lineStart = 0;

        for (int i = 0; i < source.Length; i++)
        {
            if (source[i] == (byte)'\n')
            {
                if (currentLine == lineNumber)
                {
                    // Found the line — return from lineStart to just before the \n
                    int lineEnd = i > 0 && source[i - 1] == (byte)'\r' ? i - 1 : i;
                    line = _utf8Json.Slice(lineStart, lineEnd - lineStart);
                    return true;
                }

                currentLine++;
                lineStart = i + 1;
            }
        }

        // Handle the last line (no trailing \n)
        if (currentLine == lineNumber)
        {
            int lineEnd = source.Length > 0 && source[source.Length - 1] == (byte)'\r' ? source.Length - 1 : source.Length;
            line = _utf8Json.Slice(lineStart, lineEnd - lineStart);
            return true;
        }

        line = default;
        return false;
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line)
    {
        if (((IJsonDocument)this).TryGetLine(lineNumber, out ReadOnlyMemory<byte> utf8Line))
        {
            line = JsonReaderHelper.TranscodeHelper(utf8Line.Span);
            return true;
        }

        line = null;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool TryGetLineAndOffsetForByteOffset(ReadOnlySpan<byte> source, int byteOffset, out int line, out int charOffset, out long lineByteOffset)
    {
        if (byteOffset < 0 || byteOffset > source.Length)
        {
            line = 0;
            charOffset = 0;
            lineByteOffset = 0;
            return false;
        }

        int currentLine = 1;
        long currentLineStart = 0;

        for (int i = 0; i < byteOffset; i++)
        {
            if (source[i] == (byte)'\n')
            {
                currentLine++;
                currentLineStart = i + 1;
            }
        }

        line = currentLine;
        lineByteOffset = currentLineStart;

        // Count the UTF-16 characters from the start of the line to the byte offset
        // to get a 1-based character offset.
        int bytesOnLine = byteOffset - (int)currentLineStart;
        charOffset = bytesOnLine > 0
            ? JsonReaderHelper.GetUtf16CharCount(source.Slice((int)currentLineStart, bytesOnLine)) + 1
            : 1;

        return true;
    }
}