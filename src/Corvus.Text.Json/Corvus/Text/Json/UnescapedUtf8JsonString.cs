// <copyright file="UnescapedUtf8JsonString.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Threading;

namespace Corvus.Text.Json;

/// <summary>
/// Represents an Unescaped UTF-8 JSON string.
/// </summary>
/// <remarks>
/// This may use a rented buffer to back the string, so it is disposable.
/// </remarks>
public ref struct UnescapedUtf8JsonString
#if NET
    : IDisposable
#endif
{
    private ReadOnlyMemory<byte> _utf8Bytes;

    private byte[]? _extraRentedArrayPoolBytes;

    /// <summary>
    /// Gets the UTF-8 bytes as a read-only memory.
    /// </summary>
    public readonly ReadOnlyMemory<byte> Memory => _utf8Bytes;

    /// <summary>
    /// Gets the UTF-8 bytes as a read-only span.
    /// </summary>
    public readonly ReadOnlySpan<byte> Span => _utf8Bytes.Span;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnescapedUtf8JsonString"/> struct.
    /// </summary>
    /// <param name="utf8Bytes">The UTF-8 bytes representing the JSON string.</param>
    /// <param name="extraRentedArrayPoolBytes">Optional rented array pool bytes.</param>
    public UnescapedUtf8JsonString(ReadOnlyMemory<byte> utf8Bytes, byte[]? extraRentedArrayPoolBytes = null)
    {
        _utf8Bytes = utf8Bytes;
        _extraRentedArrayPoolBytes = extraRentedArrayPoolBytes;
    }

    /// <summary>
    /// Take ownership of the <see cref="ArrayPool{T}.Shared"/> bytes, if any.
    /// </summary>
    /// <param name="extraRentedArrayPoolBytes">The rented bytes, or null if there are no rented bytes.</param>
    /// <returns>The UTF-8 memory representing the rented bytes.</returns>
    public ReadOnlyMemory<byte> TakeOwnership(out byte[]? extraRentedArrayPoolBytes)
    {
        extraRentedArrayPoolBytes = Interlocked.Exchange(ref _extraRentedArrayPoolBytes, null);
        return _utf8Bytes;
    }

    /// <summary>
    /// Disposes the unescaped UTF-8 JSON string, returning any rented array pool bytes.
    /// </summary>
    public void Dispose()
    {
        if (_extraRentedArrayPoolBytes != null)
        {
            byte[]? extraRentedBytes = Interlocked.Exchange(ref _extraRentedArrayPoolBytes, null);

            if (extraRentedBytes != null)
            {
                // When "extra rented bytes exist" it contains the document,
                // and thus needs to be cleared before being returned.
                extraRentedBytes.AsSpan(0, _utf8Bytes.Length).Clear();
                ArrayPool<byte>.Shared.Return(extraRentedBytes);
            }
        }
    }
}