// <copyright file="Utf8Name.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A zero-copy reference to a UTF-8 property name within a shared source buffer.
/// Avoids allocating a separate <c>byte[]</c> per name by storing an offset/length
/// into the expression's UTF-8 byte array.
/// </summary>
internal readonly struct Utf8Name
{
    private readonly byte[] source;
    private readonly int offset;
    private readonly int length;

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8Name"/> struct referencing a
    /// slice of a shared source buffer.
    /// </summary>
    public Utf8Name(byte[] source, int offset, int length)
    {
        this.source = source;
        this.offset = offset;
        this.length = length;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Utf8Name"/> struct from a
    /// materialized byte array (used for string-backed names like keywords).
    /// </summary>
    public Utf8Name(byte[] materialized)
    {
        this.source = materialized;
        this.offset = 0;
        this.length = materialized.Length;
    }

    /// <summary>Gets a value indicating whether this instance is valid (has been initialized).</summary>
    public bool HasValue => this.source is not null;

    /// <summary>Gets the UTF-8 bytes for this name as a span.</summary>
    public ReadOnlySpan<byte> Span => this.source.AsSpan(this.offset, this.length);
}