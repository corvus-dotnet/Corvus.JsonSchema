// <copyright file="Comparand.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace Corvus.Text.Json.Arazzo;

/// <summary>
/// A resolved operand of a <c>simple</c> criterion — a JSON-comparable scalar (or an opaque JSON
/// value, compared only for equality).
/// </summary>
/// <remarks>
/// <para>
/// String operands are held as UTF-8 — either a baked literal/scalar byte buffer or a JSON string
/// element accessed via <see cref="JsonElement.GetUtf8String"/> — so equality and numeric coercion
/// are performed over UTF-8 spans with no managed-string allocation on the hot path.
/// </para>
/// <para>
/// This type is the single source of truth for <c>simple</c>-criterion operand semantics. It is
/// <see langword="public"/> so generated workflow executors can inline criterion evaluation —
/// resolving each operand statically and calling <see cref="ValueEquals"/>/<see cref="ValueNotEquals"/>/
/// the ordering helpers/<see cref="IsTrue"/> directly — without going through the runtime interpreter.
/// </para>
/// </remarks>
public readonly struct Comparand
{
    private readonly ComparandKind kind;
    private readonly bool boolean;
    private readonly double number;
    private readonly byte[]? utf8;     // String kind, baked literal/scalar value.
    private readonly JsonElement element; // String kind from JSON, or Json (object/array) kind.

    private Comparand(ComparandKind kind, bool boolean, double number, byte[]? utf8, JsonElement element)
    {
        this.kind = kind;
        this.boolean = boolean;
        this.number = number;
        this.utf8 = utf8;
        this.element = element;
    }

    public ComparandKind Kind => this.kind;

    /// <summary>Gets a value indicating whether this comparand is boolean <c>true</c> (lone-operand truthiness).</summary>
    public bool IsTrue => this.kind == ComparandKind.Boolean && this.boolean;

    public static Comparand Undefined => default;

    public static Comparand Null => new(ComparandKind.Null, false, 0, null, default);

    public static Comparand FromBoolean(bool value) => new(ComparandKind.Boolean, value, 0, null, default);

    public static Comparand FromNumber(double value) => new(ComparandKind.Number, false, value, null, default);

    /// <summary>Creates a string comparand from a baked UTF-8 buffer (a literal or scalar value).</summary>
    /// <param name="value">The UTF-8 bytes.</param>
    /// <returns>The comparand.</returns>
    public static Comparand FromUtf8String(byte[] value) => new(ComparandKind.String, false, 0, value, default);

    /// <summary>Creates a string comparand backed by a JSON string element (no string is materialized).</summary>
    /// <param name="value">The JSON string element.</param>
    /// <returns>The comparand.</returns>
    public static Comparand FromJsonString(in JsonElement value) => new(ComparandKind.String, false, 0, null, value);

    /// <summary>Creates an opaque JSON (object/array) comparand, compared only for equality.</summary>
    /// <param name="value">The JSON element.</param>
    /// <returns>The comparand.</returns>
    public static Comparand FromJson(in JsonElement value) => new(ComparandKind.Json, false, 0, null, value);

    /// <summary>
    /// Creates a comparand from a resolved JSON value, mapping each <see cref="JsonValueKind"/> to the
    /// matching comparand kind (string/number/boolean/null, or opaque JSON for object/array). This is
    /// the mapping a generated executor applies to a statically-navigated operand.
    /// </summary>
    /// <param name="value">The resolved JSON element.</param>
    /// <returns>The comparand, or <see cref="Undefined"/> for <see cref="JsonValueKind.Undefined"/>.</returns>
    public static Comparand FromJsonElement(in JsonElement value)
        => value.ValueKind switch
        {
            JsonValueKind.String => FromJsonString(value),
            JsonValueKind.Number => FromNumber(value.GetDouble()),
            JsonValueKind.True => FromBoolean(true),
            JsonValueKind.False => FromBoolean(false),
            JsonValueKind.Null => Null,
            JsonValueKind.Object or JsonValueKind.Array => FromJson(value),
            _ => Undefined,
        };

    /// <summary>
    /// Evaluates equality (<c>==</c>). Differing kinds are never equal; an undefined operand makes
    /// the comparison false. String comparison is case-insensitive (Arazzo §Condition Evaluation),
    /// performed over UTF-8 spans.
    /// </summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns><see langword="true"/> if equal.</returns>
    public bool ValueEquals(in Comparand other)
    {
        if (this.kind == ComparandKind.Undefined || other.kind == ComparandKind.Undefined || this.kind != other.kind)
        {
            return false;
        }

        return this.kind switch
        {
            ComparandKind.Null => true,
            ComparandKind.Boolean => this.boolean == other.boolean,
            ComparandKind.Number => this.number.Equals(other.number),
            ComparandKind.String => StringEquals(this, other),
            ComparandKind.Json => JsonEquals(this.element, other.element),
            _ => false,
        };
    }

    /// <summary>
    /// Evaluates inequality (<c>!=</c>). Mirrors the runtime's <c>simple</c>-criterion semantics: an
    /// undefined operand makes the comparison <see langword="false"/> (it is <em>not</em> the negation
    /// of <see cref="ValueEquals"/>, which is also false for undefined operands).
    /// </summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns><see langword="true"/> if both operands are defined and not equal.</returns>
    public bool ValueNotEquals(in Comparand other)
        => this.kind != ComparandKind.Undefined && other.kind != ComparandKind.Undefined && !this.ValueEquals(other);

    /// <summary>Evaluates <c>&lt;</c> (numeric, coercing numeric strings); false unless both operands are numeric.</summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns>The comparison result.</returns>
    public bool LessThan(in Comparand other) => this.TryCompareNumeric(other, out int c) && c < 0;

    /// <summary>Evaluates <c>&lt;=</c> (numeric, coercing numeric strings); false unless both operands are numeric.</summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns>The comparison result.</returns>
    public bool LessThanOrEqual(in Comparand other) => this.TryCompareNumeric(other, out int c) && c <= 0;

    /// <summary>Evaluates <c>&gt;</c> (numeric, coercing numeric strings); false unless both operands are numeric.</summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns>The comparison result.</returns>
    public bool GreaterThan(in Comparand other) => this.TryCompareNumeric(other, out int c) && c > 0;

    /// <summary>Evaluates <c>&gt;=</c> (numeric, coercing numeric strings); false unless both operands are numeric.</summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <returns>The comparison result.</returns>
    public bool GreaterThanOrEqual(in Comparand other) => this.TryCompareNumeric(other, out int c) && c >= 0;

    /// <summary>
    /// Gets this comparand as a number, coercing a numeric string per the Arazzo spec
    /// ("Numeric strings SHOULD be coerced to numbers when compared with numeric operators").
    /// </summary>
    /// <param name="value">The numeric value, when this comparand is a number or numeric string.</param>
    /// <returns><see langword="true"/> if a number was produced.</returns>
    public bool TryAsNumber(out double value)
    {
        if (this.kind == ComparandKind.Number)
        {
            value = this.number;
            return true;
        }

        if (this.kind == ComparandKind.String)
        {
            if (this.utf8 is not null)
            {
                return TryParseUtf8Number(this.utf8, out value);
            }

            using UnescapedUtf8JsonString unescaped = this.element.GetUtf8String();
            return TryParseUtf8Number(unescaped.Span, out value);
        }

        value = 0;
        return false;
    }

    /// <summary>
    /// Compares two comparands for ordering, coercing numeric strings to numbers.
    /// </summary>
    /// <param name="other">The right-hand comparand.</param>
    /// <param name="comparison">The sign of the comparison (this vs other) when both are numeric.</param>
    /// <returns><see langword="true"/> if both operands are numeric (or numeric strings).</returns>
    public bool TryCompareNumeric(in Comparand other, out int comparison)
    {
        if (this.TryAsNumber(out double a) && other.TryAsNumber(out double b))
        {
            comparison = a.CompareTo(b);
            return true;
        }

        comparison = 0;
        return false;
    }

    /// <summary>
    /// Parses a <c>simple</c>-condition literal token (number, single/double-quoted string,
    /// <c>true</c>, <c>false</c>, or <c>null</c>) into a comparand. String literals are baked to UTF-8.
    /// </summary>
    /// <param name="token">The literal token.</param>
    /// <returns>The parsed comparand, or <see cref="Undefined"/> if the token is not a recognized literal.</returns>
    public static Comparand ParseLiteral(ReadOnlySpan<char> token)
    {
        if (token.Length >= 2 && token[0] == '\'' && token[^1] == '\'')
        {
            // Single-quoted; a literal single quote is escaped by doubling ('').
            return FromUtf8String(Encoding.UTF8.GetBytes(token[1..^1].ToString().Replace("''", "'", StringComparison.Ordinal)));
        }

        if (token.Length >= 2 && token[0] == '"' && token[^1] == '"')
        {
            return FromUtf8String(Encoding.UTF8.GetBytes(token[1..^1].ToString()));
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

        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
        {
            return FromNumber(parsed);
        }

        return Undefined;
    }

    private static bool StringEquals(in Comparand a, in Comparand b)
    {
        if (a.utf8 is not null)
        {
            return StringEqualsRight(a.utf8, b);
        }

        using UnescapedUtf8JsonString left = a.element.GetUtf8String();
        return StringEqualsRight(left.Span, b);
    }

    private static bool StringEqualsRight(ReadOnlySpan<byte> left, in Comparand b)
    {
        if (b.utf8 is not null)
        {
            return Utf8EqualsIgnoreCase(left, b.utf8);
        }

        using UnescapedUtf8JsonString right = b.element.GetUtf8String();
        return Utf8EqualsIgnoreCase(left, right.Span);
    }

    /// <summary>
    /// Case-insensitive equality of two UTF-8 spans. Fast path: when both are ASCII, an ASCII
    /// ordinal-ignore-case comparison (no allocation). Slow path (non-ASCII present): transcode to
    /// UTF-16 and compare with <see cref="StringComparison.OrdinalIgnoreCase"/> for full-Unicode
    /// case-insensitivity; large inputs use a pooled buffer, short inputs the stack.
    /// </summary>
    private static bool Utf8EqualsIgnoreCase(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        if (Ascii.IsValid(left) && Ascii.IsValid(right))
        {
            return Ascii.EqualsIgnoreCase(left, right);
        }

        return EqualsIgnoreCaseUnicode(left, right);
    }

    private static bool EqualsIgnoreCaseUnicode(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right)
    {
        const int StackThreshold = 128;

        int leftChars = Encoding.UTF8.GetCharCount(left);
        int rightChars = Encoding.UTF8.GetCharCount(right);

        char[]? leftRented = null;
        char[]? rightRented = null;
        Span<char> leftBuffer = leftChars <= StackThreshold ? stackalloc char[StackThreshold] : (leftRented = ArrayPool<char>.Shared.Rent(leftChars));
        Span<char> rightBuffer = rightChars <= StackThreshold ? stackalloc char[StackThreshold] : (rightRented = ArrayPool<char>.Shared.Rent(rightChars));

        try
        {
            int l = Encoding.UTF8.GetChars(left, leftBuffer);
            int r = Encoding.UTF8.GetChars(right, rightBuffer);
            return leftBuffer[..l].Equals(rightBuffer[..r], StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (leftRented is not null)
            {
                ArrayPool<char>.Shared.Return(leftRented);
            }

            if (rightRented is not null)
            {
                ArrayPool<char>.Shared.Return(rightRented);
            }
        }
    }

    private static bool JsonEquals(in JsonElement a, in JsonElement b)
    {
        // Object/array equality is rare and off the hot path; compare canonical text.
        return string.Equals(a.GetRawText(), b.GetRawText(), StringComparison.Ordinal);
    }

    private static bool TryParseUtf8Number(ReadOnlySpan<byte> utf8, out double value)
        => Utf8Parser.TryParse(utf8, out value, out int consumed) && consumed == utf8.Length;
}