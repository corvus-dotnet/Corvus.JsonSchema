// <copyright file="UnescapedUtf16JsonString.cs" company="Endjin Limited">
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
/// Represents an Unescaped UTF-16 JSON string.
/// </summary>
/// <remarks>
/// This uses a rented buffer to back the string, so it is disposable.
/// </remarks>
public ref struct UnescapedUtf16JsonString
#if NET
    : IDisposable
#endif
{
    private ReadOnlyMemory<char> _chars;

    private char[]? _extraRentedArrayPoolChars;

    /// <summary>
    /// Gets the UTF-16 characters as a read-only memory.
    /// </summary>
    public readonly ReadOnlyMemory<char> Memory => _chars;

    /// <summary>
    /// Gets the UTF-16 characters as a read-only span.
    /// </summary>
    public readonly ReadOnlySpan<char> Span => _chars.Span;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnescapedUtf16JsonString"/> struct.
    /// </summary>
    /// <param name="chars">The UTF-16 characters representing the JSON string.</param>
    /// <param name="extraRentedArrayPoolChars">Optional rented array pool characters.</param>
    public UnescapedUtf16JsonString(ReadOnlyMemory<char> chars, char[]? extraRentedArrayPoolChars = null)
    {
        _chars = chars;
        _extraRentedArrayPoolChars = extraRentedArrayPoolChars;
    }

    /// <summary>
    /// Take ownership of the <see cref="ArrayPool{T}.Shared"/> characters, if any.
    /// </summary>
    /// <param name="extraRentedArrayPoolChars">The rented characters, or null if there are no rented characters.</param>
    /// <returns>The UTF-16 memory representing the rented characters.</returns>
    public ReadOnlyMemory<char> TakeOwnership(out char[]? extraRentedArrayPoolChars)
    {
        extraRentedArrayPoolChars = Interlocked.Exchange(ref _extraRentedArrayPoolChars, null);
        return _chars;
    }

    /// <summary>
    /// Disposes the unescaped UTF-16 JSON string, returning any rented array pool characters.
    /// </summary>
    public void Dispose()
    {
        if (_extraRentedArrayPoolChars != null)
        {
            char[]? extraRentedChars = Interlocked.Exchange(ref _extraRentedArrayPoolChars, null);

            if (extraRentedChars != null)
            {
                // When "extra rented chars exist" it contains the document,
                // and thus needs to be cleared before being returned.
                extraRentedChars.AsSpan(0, _chars.Length).Clear();
                ArrayPool<char>.Shared.Return(extraRentedChars);
            }
        }
    }
}