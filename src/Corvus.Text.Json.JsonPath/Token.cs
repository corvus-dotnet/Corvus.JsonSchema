// <copyright file="Token.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.JsonPath;

/// <summary>
/// A single lexer token from a JSONPath expression.
/// </summary>
internal readonly struct Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct.
    /// </summary>
    /// <param name="type">The token type.</param>
    /// <param name="start">The 0-based start offset in the source expression.</param>
    /// <param name="length">The byte length of the token text.</param>
    public Token(TokenType type, int start, int length)
    {
        Type = type;
        Start = start;
        Length = length;
    }

    /// <summary>
    /// Gets the token type.
    /// </summary>
    public TokenType Type { get; }

    /// <summary>
    /// Gets the 0-based start offset in the source expression.
    /// </summary>
    public int Start { get; }

    /// <summary>
    /// Gets the byte length of the token text.
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Gets the byte span of this token from the source expression.
    /// </summary>
    /// <param name="source">The full source expression.</param>
    /// <returns>The slice of the source that this token covers.</returns>
    public ReadOnlySpan<byte> GetSpan(ReadOnlySpan<byte> source) => source.Slice(Start, Length);
}
