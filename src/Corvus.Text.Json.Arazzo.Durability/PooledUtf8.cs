// <copyright file="PooledUtf8.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A serialized UTF-8 document held in an <see cref="ArrayPool{T}"/>-rented buffer (see
/// <see cref="PersistedJson.RentDocument{TContext}"/>): the persistence-write counterpart of a GC <see cref="byte"/> array
/// for backends whose driver accepts a <see cref="ReadOnlyMemory{T}"/> / span parameter. The rented buffer is larger than
/// the written content, so read it through <see cref="Memory"/> / <see cref="Span"/> (which carry the exact length) — never
/// the bare array. Bind <see cref="Memory"/> to the command and <c>using</c> the value across the awaited write; the
/// command completing is the guarantee the driver has consumed the bytes, so <see cref="Dispose"/> then returns the buffer
/// to the pool. <c>default</c> is the empty document (nothing rented). Do not use after disposal.
/// </summary>
public readonly struct PooledUtf8 : IDisposable
{
    private readonly byte[]? rented;
    private readonly int length;

    /// <summary>Initializes a new instance of the <see cref="PooledUtf8"/> struct, taking ownership of the rented buffer.</summary>
    /// <param name="rented">The <see cref="ArrayPool{T}"/>-rented buffer (may be larger than <paramref name="length"/>).</param>
    /// <param name="length">The number of UTF-8 bytes written.</param>
    public PooledUtf8(byte[] rented, int length)
    {
        this.rented = rented;
        this.length = length;
    }

    /// <summary>Gets the written UTF-8 JSON; valid only until <see cref="Dispose"/>.</summary>
    public ReadOnlyMemory<byte> Memory => this.rented is null ? ReadOnlyMemory<byte>.Empty : this.rented.AsMemory(0, this.length);

    /// <summary>Gets the written UTF-8 JSON; valid only until <see cref="Dispose"/>.</summary>
    public ReadOnlySpan<byte> Span => this.rented is null ? default : this.rented.AsSpan(0, this.length);

    /// <summary>Gets the written UTF-8 JSON as an <see cref="ArraySegment{T}"/> (offset 0, exact length over the pooled
    /// array) — for drivers that bind a BLOB/<c>bytea</c> from an <see cref="ArraySegment{T}"/> (e.g. Npgsql, MySqlConnector);
    /// valid only until <see cref="Dispose"/>.</summary>
    public ArraySegment<byte> Segment => this.rented is null ? default : new ArraySegment<byte>(this.rented, 0, this.length);

    /// <summary>Returns the rented buffer to the pool, if any.</summary>
    public void Dispose()
    {
        if (this.rented is not null)
        {
            ArrayPool<byte>.Shared.Return(this.rented);
        }
    }
}