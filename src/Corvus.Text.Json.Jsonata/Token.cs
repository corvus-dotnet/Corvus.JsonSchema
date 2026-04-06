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

    /// <summary>Gets the token type.</summary>
    public TokenType Type { get; }

    /// <summary>Gets the token value (the raw text for names/variables/strings/numbers, or the operator symbol).</summary>
    public string Value { get; }

    /// <summary>Gets the zero-based character position in the source expression.</summary>
    public int Position { get; }

    /// <summary>Gets the numeric value of a <see cref="TokenType.Number"/> token.</summary>
    public double NumericValue { get; init; }

    /// <summary>Gets the regex pattern for a <see cref="TokenType.Regex"/> token.</summary>
    public string? RegexPattern { get; init; }

    /// <summary>Gets the regex flags for a <see cref="TokenType.Regex"/> token.</summary>
    public string? RegexFlags { get; init; }

    /// <inheritdoc/>
    public override string ToString() => $"{this.Type}({this.Value}) @{this.Position}";
}