// <copyright file="JsonElement.SourceLocation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Text.Json;

public readonly partial struct JsonElement
{
    /// <summary>
    /// Tries to get the 1-based line number and character offset of this element
    /// in the original source document.
    /// </summary>
    /// <param name="line">When this method returns, contains the 1-based line number if successful.</param>
    /// <param name="charOffset">When this method returns, contains the 1-based character offset within the line if successful.</param>
    /// <returns><see langword="true"/> if the line and offset were successfully determined; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method returns <see langword="false"/> when the backing document does not retain the original
    /// source bytes (for example, mutable builder documents or fixed-string documents).
    /// </remarks>
    public bool TryGetLineAndOffset(out int line, out int charOffset)
    {
        if (_parent is null)
        {
            line = 0;
            charOffset = 0;
            return false;
        }

        return _parent.TryGetLineAndOffset(_idx, out line, out charOffset, out _);
    }

    /// <summary>
    /// Tries to get the 1-based line number, character offset, and byte offset of this element
    /// in the original source document.
    /// </summary>
    /// <param name="line">When this method returns, contains the 1-based line number if successful.</param>
    /// <param name="charOffset">When this method returns, contains the 1-based character offset within the line if successful.</param>
    /// <param name="lineByteOffset">When this method returns, contains the byte offset of the start of the line if successful.</param>
    /// <returns><see langword="true"/> if the line and offset were successfully determined; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method returns <see langword="false"/> when the backing document does not retain the original
    /// source bytes (for example, mutable builder documents or fixed-string documents).
    /// </remarks>
    public bool TryGetLineAndOffset(out int line, out int charOffset, out long lineByteOffset)
    {
        if (_parent is null)
        {
            line = 0;
            charOffset = 0;
            lineByteOffset = 0;
            return false;
        }

        return _parent.TryGetLineAndOffset(_idx, out line, out charOffset, out lineByteOffset);
    }

    /// <summary>
    /// Tries to get the specified line from the original source document as UTF-8 bytes.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number to retrieve.</param>
    /// <param name="line">When this method returns, contains the UTF-8 bytes of the line if successful.</param>
    /// <returns><see langword="true"/> if the line was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method returns <see langword="false"/> when the backing document does not retain the original
    /// source bytes, or when <paramref name="lineNumber"/> is out of range.
    /// </remarks>
    public bool TryGetLine(int lineNumber, out ReadOnlyMemory<byte> line)
    {
        if (_parent is null)
        {
            line = default;
            return false;
        }

        return _parent.TryGetLine(lineNumber, out line);
    }

    /// <summary>
    /// Tries to get the specified line from the original source document as a string.
    /// </summary>
    /// <param name="lineNumber">The 1-based line number to retrieve.</param>
    /// <param name="line">When this method returns, contains the line text if successful.</param>
    /// <returns><see langword="true"/> if the line was successfully retrieved; otherwise, <see langword="false"/>.</returns>
    /// <remarks>
    /// This method returns <see langword="false"/> when the backing document does not retain the original
    /// source bytes, or when <paramref name="lineNumber"/> is out of range.
    /// </remarks>
    public bool TryGetLine(int lineNumber, [NotNullWhen(true)] out string? line)
    {
        if (_parent is null)
        {
            line = null;
            return false;
        }

        return _parent.TryGetLine(lineNumber, out line);
    }
}