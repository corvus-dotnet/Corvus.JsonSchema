// <copyright file="NameNode.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata.Ast;

/// <summary>
/// A field name reference in a path expression.
/// </summary>
internal sealed class NameNode : JsonataNode
{
    private string? value;
    private byte[]? utf8Source;
    private int sourceOffset;
    private int sourceLength;

    /// <inheritdoc/>
    public override NodeType Type => NodeType.Name;

    /// <summary>Gets or sets the field name.</summary>
    public string Value
    {
#if NETSTANDARD2_0
        get => this.value ??= System.Text.Encoding.UTF8.GetString(this.utf8Source!, this.sourceOffset, this.sourceLength);
#else
        get => this.value ??= System.Text.Encoding.UTF8.GetString(this.utf8Source.AsSpan(this.sourceOffset, this.sourceLength));
#endif
        set => this.value = value;
    }

    /// <summary>
    /// Creates a <see cref="NameNode"/> backed by a range in a UTF-8 byte array.
    /// The <see cref="Value"/> property materializes the string lazily only when accessed.
    /// </summary>
    internal static NameNode FromUtf8Source(byte[] utf8Source, int offset, int length, int position)
    {
        return new NameNode
        {
            utf8Source = utf8Source,
            sourceOffset = offset,
            sourceLength = length,
            Position = position,
        };
    }

    /// <summary>
    /// Returns a zero-copy <see cref="Utf8Name"/> referencing this name's UTF-8 bytes
    /// within the shared source buffer. No allocation for source-backed names.
    /// </summary>
    internal Utf8Name GetUtf8Name()
    {
        if (this.utf8Source is not null)
        {
            return new Utf8Name(this.utf8Source, this.sourceOffset, this.sourceLength);
        }

        return new Utf8Name(System.Text.Encoding.UTF8.GetBytes(this.value!));
    }

    /// <summary>
    /// Returns the UTF-8 bytes for this name as a new byte array.
    /// Prefer <see cref="GetUtf8Name"/> to avoid copying.
    /// </summary>
    internal byte[] GetUtf8Bytes()
    {
        if (this.utf8Source is not null)
        {
            return this.utf8Source.AsSpan(this.sourceOffset, this.sourceLength).ToArray();
        }

        return System.Text.Encoding.UTF8.GetBytes(this.value!);
    }
}