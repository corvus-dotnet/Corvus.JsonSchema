// <copyright file="Comparand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A resolved operand of a <c>simple</c> criterion — a JSON-comparable scalar (or an opaque JSON
/// value, compared only for equality by its canonical text).
/// </summary>
internal readonly struct Comparand
{
    private Comparand(ComparandKind kind, bool boolean, double number, string? text)
    {
        this.Kind = kind;
        this.Boolean = boolean;
        this.Number = number;
        this.Text = text;
    }

    public ComparandKind Kind { get; }

    public bool Boolean { get; }

    public double Number { get; }

    public string? Text { get; }

    public static Comparand Undefined => new(ComparandKind.Undefined, false, 0, null);

    public static Comparand Null => new(ComparandKind.Null, false, 0, null);

    public static Comparand FromBoolean(bool value) => new(ComparandKind.Boolean, value, 0, null);

    public static Comparand FromNumber(double value) => new(ComparandKind.Number, false, value, null);

    public static Comparand FromString(string value) => new(ComparandKind.String, false, 0, value);

    public static Comparand FromJson(string canonicalText) => new(ComparandKind.Json, false, 0, canonicalText);

    /// <summary>
    /// Evaluates equality (<c>==</c>) between two comparands. Differing kinds are never equal
    /// (except that two undefined values are not equal — an undefined operand makes the comparison false).
    /// </summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns><see langword="true"/> if equal.</returns>
    public bool ValueEquals(in Comparand other)
    {
        if (this.Kind == ComparandKind.Undefined || other.Kind == ComparandKind.Undefined)
        {
            return false;
        }

        if (this.Kind != other.Kind)
        {
            return false;
        }

        return this.Kind switch
        {
            ComparandKind.Null => true,
            ComparandKind.Boolean => this.Boolean == other.Boolean,
            ComparandKind.Number => this.Number.Equals(other.Number),

            // Arazzo §Condition Evaluation: string comparisons MUST be case-insensitive.
            ComparandKind.String => string.Equals(this.Text, other.Text, StringComparison.OrdinalIgnoreCase),
            ComparandKind.Json => string.Equals(this.Text, other.Text, StringComparison.Ordinal),
            _ => false,
        };
    }

    /// <summary>
    /// Compares two numeric comparands for ordering.
    /// </summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <param name="comparison">The sign of the comparison (this vs other) when both are numbers.</param>
    /// <returns><see langword="true"/> if both operands are numbers and a comparison was produced.</returns>
    public bool TryCompareNumeric(in Comparand other, out int comparison)
    {
        if (this.Kind == ComparandKind.Number && other.Kind == ComparandKind.Number)
        {
            comparison = this.Number.CompareTo(other.Number);
            return true;
        }

        comparison = 0;
        return false;
    }

    /// <summary>
    /// Parses a <c>simple</c>-condition literal token (number, single/double-quoted string,
    /// <c>true</c>, <c>false</c>, or <c>null</c>) into a comparand.
    /// </summary>
    /// <param name="token">The literal token.</param>
    /// <returns>The parsed comparand, or <see cref="Undefined"/> if the token is not a recognized literal.</returns>
    public static Comparand ParseLiteral(ReadOnlySpan<char> token)
    {
        if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
        {
            // Arazzo §Literals: single-quoted; a literal single quote is escaped by doubling ('').
            return FromString(token[1..^1].ToString().Replace("''", "'", StringComparison.Ordinal));
        }

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            // Leniently accept double-quoted strings (the spec mandates single quotes).
            return FromString(token[1..^1].ToString());
        }

        if (token.SequenceEqual("true"))
        {
            return FromBoolean(true);
        }

        if (token.SequenceEqual("false"))
        {
            return FromBoolean(false);
        }

        if (token.SequenceEqual("null"))
        {
            return Null;
        }

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
        {
            return FromNumber(number);
        }

        return Undefined;
    }
}