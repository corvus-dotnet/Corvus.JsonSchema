// <copyright file="JsonSchemaEvaluation.Array.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers.Text;
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    /// <summary>
    /// Message provider for ignored "not array type" validation messages.
    /// </summary>
    public static readonly JsonSchemaMessageProvider IgnoredNotTypeArray = static (buffer, out written) => IgnoredNotType("array"u8, buffer, out written);

    /// <summary>
    /// Provides a path provider for array item indices in JSON schema validation.
    /// </summary>
    public static readonly JsonSchemaPathProvider<int> ItemIndex = static (index, buffer, out written) => AppendIndex(index, buffer, out written);

    /// <summary>
    /// Message provider for expected "array type" validation messages.
    /// </summary>
    public static readonly JsonSchemaMessageProvider ExpectedTypeArray = static (buffer, out written) => ExpectedType("array"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedUniqueItems = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedUniqueItems.AsSpan(), buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountEquals = static (value, buffer, out written) => ExpectedItemCountEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountNotEquals = static (value, buffer, out written) => ExpectedItemCountNotEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountGreaterThan = static (value, buffer, out written) => ExpectedItemCountGreaterThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountGreaterThanOrEquals = static (value, buffer, out written) => ExpectedItemCountGreaterThanOrEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountLessThan = static (value, buffer, out written) => ExpectedItemCountLessThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedItemCountLessThanOrEquals = static (value, buffer, out written) => ExpectedItemCountLessThanOrEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountEquals = static (value, buffer, out written) => ExpectedContainsCountEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountNotEquals = static (value, buffer, out written) => ExpectedContainsCountNotEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountGreaterThan = static (value, buffer, out written) => ExpectedContainsCountGreaterThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountGreaterThanOrEquals = static (value, buffer, out written) => ExpectedContainsCountGreaterThanOrEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountLessThan = static (value, buffer, out written) => ExpectedContainsCountLessThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedContainsCountLessThanOrEquals = static (value, buffer, out written) => ExpectedContainsCountLessThanOrEqualsValue(value, buffer, out written);

    /// <summary>
    /// Matches a JSON token type against the "array" type constraint.
    /// </summary>
    /// <param name="tokenType">The JSON token type to validate.</param>
    /// <param name="typeKeyword">The type keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the token type is a start array; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTypeArray(JsonTokenType tokenType, ReadOnlySpan<byte> typeKeyword, ref JsonSchemaContext context)
    {
        if (tokenType != JsonTokenType.StartArray)
        {
            context.EvaluatedKeyword(false, ExpectedTypeArray, typeKeyword);
            return false;
        }
        else
        {
            context.EvaluatedKeyword(true, ExpectedTypeArray, typeKeyword);
        }

        return true;
    }

    /// <summary>
    /// Writes the schema location for an item at a specific index in an array.
    /// </summary>
    /// <param name="arraySchemaLocation">The base schema location for the array.</param>
    /// <param name="itemIndex">The index of the item within the array.</param>
    /// <param name="buffer">The buffer to write the schema location to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the schema location was successfully written; otherwise, <see langword="false"/>.</returns>
    public static bool SchemaLocationForItemIndex(ReadOnlySpan<byte> arraySchemaLocation, int itemIndex, Span<byte> buffer, out int written)
    {
        if (buffer.Length < arraySchemaLocation.Length)
        {
            written = 0;
            return false;
        }

        arraySchemaLocation.CopyTo(buffer);
        written = arraySchemaLocation.Length;

        if (buffer[written - 1] != (byte)'/')
        {
            if (buffer.Length <= written)
            {
                written = 0;
                return false;
            }

            buffer[written++] = (byte)'/';
        }

        if (!Utf8Formatter.TryFormat(itemIndex, buffer[written..], out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written += bytesWritten;
        return true;
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountNotEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountNotEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountGreaterThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountGreaterThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountGreaterThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountGreaterThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountLessThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountLessThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a item count.
    /// </summary>
    /// <param name="value">The expected item count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedItemCountLessThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedItemCountLessThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Validates that a item count equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual != expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a item count does not equal the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is not equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountNotEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual == expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountNotEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountNotEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a item count is greater than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountGreaterThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual <= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountGreaterThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountGreaterThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a item count is greater than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountGreaterThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual < expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountGreaterThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountGreaterThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a item count is less than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountLessThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual >= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountLessThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountLessThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a item count is less than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchItemCountLessThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual > expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedItemCountLessThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedItemCountLessThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountNotEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountNotEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountGreaterThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountGreaterThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountGreaterThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountGreaterThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountLessThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountLessThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a contains count.
    /// </summary>
    /// <param name="value">The expected contains count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedContainsCountLessThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedContainsCountLessThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Validates that a contains count equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual != expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a contains count does not equal the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is not equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountNotEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual == expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountNotEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountNotEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a contains count is greater than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountGreaterThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual <= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountGreaterThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountGreaterThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a contains count is greater than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountGreaterThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual < expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountGreaterThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountGreaterThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a contains count is less than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountLessThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual >= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountLessThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountLessThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a contains count is less than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchContainsCountLessThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual > expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedContainsCountLessThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedContainsCountLessThanOrEquals, keyword);
        return true;
    }

    private static bool AppendIndex(int index, Span<byte> buffer, out int written)
    {
        if (buffer.Length < 2)
        {
            written = 0;
            return false;
        }

        if (!Utf8Formatter.TryFormat(index, buffer, out int bytesWritten))
        {
            written = 0;
            return false;
        }

        written = bytesWritten;
        return true;
    }
}