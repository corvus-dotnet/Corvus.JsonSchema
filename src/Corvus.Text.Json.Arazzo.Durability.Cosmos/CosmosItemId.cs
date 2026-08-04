// <copyright file="CosmosItemId.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Security.Cryptography;
using System.Text;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// Derives a store's Cosmos <c>id</c> from the composite key that identifies a row: the key parts are hashed and the
/// digest is rendered under a per-store prefix, so an id is deterministic, collision-free and free of the characters
/// Cosmos forbids in an id, whatever the key parts contain.
/// </summary>
/// <remarks>
/// The parts are written into scratch and hashed from the span. Composing them into a string and calling
/// <c>Encoding.UTF8.GetBytes</c> cost two allocations per id, on every read and write, for bytes consumed immediately
/// and never kept. The digest, and therefore the id, is exactly what that produced, which is why each caller passes
/// the separator it has always hashed rather than taking one from here.
/// </remarks>
internal static class CosmosItemId
{
    // Key parts are short, so the hash scratch fits the stack in the common case, with an ArrayPool fallback.
    private const int StackThreshold = 256;

    /// <summary>Renders the id for a single-part key.</summary>
    /// <param name="prefix">The store's id prefix.</param>
    /// <param name="part">The key part.</param>
    /// <returns>The Cosmos item id.</returns>
    internal static string Compose(string prefix, ReadOnlySpan<char> part)
        => Compose(prefix, default, part, default, default, parts: 1);

    /// <summary>Renders the id for a two-part key.</summary>
    /// <param name="prefix">The store's id prefix.</param>
    /// <param name="separator">The byte separating the parts, as this store has always hashed it.</param>
    /// <param name="first">The first key part.</param>
    /// <param name="second">The second key part.</param>
    /// <returns>The Cosmos item id.</returns>
    internal static string Compose(string prefix, byte separator, ReadOnlySpan<char> first, ReadOnlySpan<char> second)
        => Compose(prefix, separator, first, second, default, parts: 2);

    /// <summary>Renders the id for a three-part key.</summary>
    /// <param name="prefix">The store's id prefix.</param>
    /// <param name="separator">The byte separating the parts, as this store has always hashed it.</param>
    /// <param name="first">The first key part.</param>
    /// <param name="second">The second key part.</param>
    /// <param name="third">The third key part.</param>
    /// <returns>The Cosmos item id.</returns>
    internal static string Compose(string prefix, byte separator, ReadOnlySpan<char> first, ReadOnlySpan<char> second, ReadOnlySpan<char> third)
        => Compose(prefix, separator, first, second, third, parts: 3);

    private static string Compose(string prefix, byte separator, ReadOnlySpan<char> first, ReadOnlySpan<char> second, ReadOnlySpan<char> third, int parts)
    {
        int maxRaw = Encoding.UTF8.GetMaxByteCount(first.Length + second.Length + third.Length) + 2;
        byte[]? rented = maxRaw > StackThreshold ? ArrayPool<byte>.Shared.Rent(maxRaw) : null;
        try
        {
            Span<byte> raw = rented ?? stackalloc byte[StackThreshold];
            int pos = Encoding.UTF8.GetBytes(first, raw);
            if (parts > 1)
            {
                raw[pos++] = separator;
                pos += Encoding.UTF8.GetBytes(second, raw[pos..]);
            }

            if (parts > 2)
            {
                raw[pos++] = separator;
                pos += Encoding.UTF8.GetBytes(third, raw[pos..]);
            }

            Span<byte> hash = stackalloc byte[32];
            SHA256.HashData(raw[..pos], hash);
            return string.Concat(prefix, Convert.ToHexStringLower(hash));
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }
}