// <copyright file="ResolvedUtf8.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A resolved runtime-expression value exposed as a UTF-8 span without allocating a managed string.
/// </summary>
/// <remarks>
/// The span is backed either by a stored scalar buffer (no lease) or by a JSON string element's
/// <see cref="UnescapedUtf8JsonString"/> lease, which must be returned via <see cref="Dispose"/>.
/// </remarks>
internal ref struct ResolvedUtf8
{
    private UnescapedUtf8JsonString lease;
    private readonly bool hasLease;

    private ResolvedUtf8(ReadOnlySpan<byte> span)
    {
        this.Span = span;
        this.lease = default;
        this.hasLease = false;
    }

    private ResolvedUtf8(UnescapedUtf8JsonString lease)
    {
        this.lease = lease;
        this.Span = lease.Span;
        this.hasLease = true;
    }

    /// <summary>Gets the UTF-8 bytes of the resolved value.</summary>
    public ReadOnlySpan<byte> Span { get; }

    /// <summary>Creates a value over an already-resident UTF-8 buffer (no lease).</summary>
    /// <param name="span">The UTF-8 bytes.</param>
    /// <returns>The resolved value.</returns>
    public static ResolvedUtf8 FromBytes(ReadOnlySpan<byte> span) => new(span);

    /// <summary>Creates a value that owns a JSON string lease.</summary>
    /// <param name="lease">The unescaped UTF-8 JSON string lease.</param>
    /// <returns>The resolved value.</returns>
    public static ResolvedUtf8 FromLease(UnescapedUtf8JsonString lease) => new(lease);

    /// <summary>Returns any rented buffer held by a JSON string lease.</summary>
    public void Dispose()
    {
        if (this.hasLease)
        {
            this.lease.Dispose();
        }
    }
}