// <copyright file="ParsedJsonDocument.JsonPointer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public sealed partial class ParsedJsonDocument<T>
{
    /// <inheritdoc />
    bool IJsonDocument.TryGetJsonPointer(int index, Span<byte> utf8Destination, out int bytesWritten, out int bytesRequired)
    {
        CheckNotDisposed();
        return TryGetJsonPointerUnsafe(index, utf8Destination, out bytesWritten, out bytesRequired);
    }

    /// <inheritdoc />
    bool IJsonDocument.TryGetJsonPointer(int index, Span<char> destination, out int charsWritten, out int charsRequired)
    {
        CheckNotDisposed();
        return TryGetJsonPointerUnsafe(index, destination, out charsWritten, out charsRequired);
    }
}