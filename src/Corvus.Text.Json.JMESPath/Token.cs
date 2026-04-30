// <copyright file="Token.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JMESPath;

/// <summary>
/// Represents a single token produced by the JMESPath lexer.
/// </summary>
internal readonly struct Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct for a simple token with no value.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <param name="position">The byte offset in the source expression.</param>
    public Token(TokenType type, int position)
    {
        this.Type = type;
        this.Position = position;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct for an integer token.
    /// </summary>
    /// <param name="type">The token type (must be <see cref="TokenType.Number"/>).</param>
    /// <param name="position">The byte offset in the source expression.</param>
    /// <param name="integerValue">The parsed integer value.</param>
    public Token(TokenType type, int position, int integerValue)
    {
        this.Type = type;
        this.Position = position;
        this.IntegerValue = integerValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct for a token with a UTF-8 value
    /// stored as an offset/length into the source buffer.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <param name="position">The byte offset in the source expression.</param>
    /// <param name="valueOffset">The start offset of the value in the source buffer.</param>
    /// <param name="valueLength">The byte length of the value in the source buffer.</param>
    public Token(TokenType type, int position, int valueOffset, int valueLength)
    {
        this.Type = type;
        this.Position = position;
        this.ValueOffset = valueOffset;
        this.ValueLength = valueLength;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct for a token with a
    /// materialized byte array value (used for quoted identifiers with escape sequences).
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <param name="position">The byte offset in the source expression.</param>
    /// <param name="materializedValue">The materialized UTF-8 byte array.</param>
    public Token(TokenType type, int position, byte[] materializedValue)
    {
        this.Type = type;
        this.Position = position;
        this.MaterializedValue = materializedValue;
    }

    /// <summary>Gets the token type.</summary>
    public TokenType Type { get; }

    /// <summary>Gets the byte offset in the source expression where this token starts.</summary>
    public int Position { get; }

    /// <summary>Gets the parsed integer value for <see cref="TokenType.Number"/> tokens.</summary>
    public int IntegerValue { get; }

    /// <summary>Gets the start offset of the value in the source buffer (for identifiers and literals).</summary>
    public int ValueOffset { get; }

    /// <summary>Gets the byte length of the value in the source buffer.</summary>
    public int ValueLength { get; }

    /// <summary>Gets the materialized UTF-8 byte array (for quoted identifiers with escapes).</summary>
    public byte[]? MaterializedValue { get; }

    /// <summary>
    /// Gets the UTF-8 value span for this token, using either the materialized value or the
    /// source buffer.
    /// </summary>
    /// <param name="source">The original UTF-8 source buffer.</param>
    /// <returns>A span of the token's UTF-8 value bytes.</returns>
    public ReadOnlySpan<byte> GetValueSpan(ReadOnlySpan<byte> source)
    {
        if (this.MaterializedValue is not null)
        {
            return this.MaterializedValue;
        }

        return source.Slice(this.ValueOffset, this.ValueLength);
    }
}
