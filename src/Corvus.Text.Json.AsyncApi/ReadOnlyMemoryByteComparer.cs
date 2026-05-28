// <copyright file="ReadOnlyMemoryByteComparer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.AsyncApi;

/// <summary>
/// Content-based equality comparer for <see cref="ReadOnlyMemory{T}"/> of <see langword="byte"/>.
/// Used as a dictionary key comparer so that correlation IDs stored as UTF-8 bytes
/// can be matched without allocating intermediate strings.
/// </summary>
public sealed class ReadOnlyMemoryByteComparer : IEqualityComparer<ReadOnlyMemory<byte>>
{
    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static readonly ReadOnlyMemoryByteComparer Instance = new();

    /// <inheritdoc/>
    public bool Equals(ReadOnlyMemory<byte> x, ReadOnlyMemory<byte> y)
    {
        return x.Span.SequenceEqual(y.Span);
    }

    /// <inheritdoc/>
    public int GetHashCode(ReadOnlyMemory<byte> obj)
    {
        HashCode hash = default;
        hash.AddBytes(obj.Span);
        return hash.ToHashCode();
    }
}