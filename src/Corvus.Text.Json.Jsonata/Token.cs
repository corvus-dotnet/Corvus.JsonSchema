// <copyright file="Token.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// A token produced by the JSONata lexer.
/// </summary>
internal readonly struct Token
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct.
    /// </summary>
    public Token(TokenType type, string value, int position)
    {
        this.Type = type;
        this.Value = value;
        this.Position = position;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Token"/> struct with deferred value.
    /// The value is represented by offset and length into the source string.
    /// </summary>
    public Token(TokenType type, int position, int valueOffset, int valueLength)
    {
        this.Type = type;
        this.Position = position;
        this.ValueOffset = valueOffset;
        this.ValueLength = valueLength;
    }

    /// <summary>Gets the token type.</summary>
    public TokenType Type { get; }

    /// <summary>Gets the token value (the raw text for names/variables/strings/numbers, or the operator symbol).</summary>
    public string? Value { get; }

    /// <summary>Gets the zero-based character position in the source expression.</summary>
    public int Position { get; }

    /// <summary>Gets the numeric value of a <see cref="TokenType.Number"/> token.</summary>
    public double NumericValue { get; init; }

    /// <summary>Gets the regex pattern for a <see cref="TokenType.Regex"/> token.</summary>
    public string? RegexPattern { get; init; }

    /// <summary>Gets the regex flags for a <see cref="TokenType.Regex"/> token.</summary>
    public string? RegexFlags { get; init; }

    /// <summary>Gets the offset of the semantic value within the UTF-8 source, or 0 if not set.</summary>
    public int ValueOffset { get; }

    /// <summary>Gets the length of the semantic value within the UTF-8 source.</summary>
    public int ValueLength { get; }

    /// <summary>
    /// Gets the token value, materializing from the UTF-8 source bytes if necessary.
    /// </summary>
    /// <param name="utf8Source">The UTF-8 encoded source expression.</param>
    /// <returns>The token value string.</returns>
    public string GetValue(byte[] utf8Source)
    {
#if NETSTANDARD2_0
        return this.Value ?? System.Text.Encoding.UTF8.GetString(utf8Source, this.ValueOffset, this.ValueLength);
#else
        return this.Value ?? System.Text.Encoding.UTF8.GetString(utf8Source.AsSpan(this.ValueOffset, this.ValueLength));
#endif
    }

    /// <inheritdoc/>
    public override string ToString() => $"{this.Type}({this.Value}) @{this.Position}";
}