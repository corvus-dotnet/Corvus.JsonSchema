// <copyright file="JsonSchemaEvaluation.Object.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Runtime.CompilerServices;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    public static readonly JsonSchemaMessageProvider IgnoredNotTypeObject = static (buffer, out written) => IgnoredNotType("object"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedTypeObject = static (buffer, out written) => ExpectedType("object"u8, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountEquals = static (value, buffer, out written) => ExpectedPropertyCountEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountNotEquals = static (value, buffer, out written) => ExpectedPropertyCountNotEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountGreaterThan = static (value, buffer, out written) => ExpectedPropertyCountGreaterThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountGreaterThanOrEquals = static (value, buffer, out written) => ExpectedPropertyCountGreaterThanOrEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountLessThan = static (value, buffer, out written) => ExpectedPropertyCountLessThanValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedPropertyCountLessThanOrEquals = static (value, buffer, out written) => ExpectedPropertyCountLessThanOrEqualsValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedMatchPatternPropertySchema = static (value, buffer, out written) => ExpectedMatchPatternPropertySchemaValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedPropertyNameMatchesRegularExpression = static (value, buffer, out written) => ExpectedPropertyNameMatchesRegularExpressionValue(value, buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedPropertyNameMatchesSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyNameMatchesSchema.AsSpan(), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedPropertyMatchesFallbackSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyMatchesFallbackSchema.AsSpan(), buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedMatchesDependentSchema = static (value, buffer, out written) => ExpectedMatchesDependentSchemaValue(value, buffer, out written);

    /// <summary>
    /// Matches a JSON token type against the "object" type constraint.
    /// </summary>
    /// <param name="tokenType">The JSON token type to validate.</param>
    /// <param name="typeKeyword">The type keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the token type is a start object; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTypeObject(JsonTokenType tokenType, ReadOnlySpan<byte> typeKeyword, ref JsonSchemaContext context)
    {
        if (tokenType != JsonTokenType.StartObject)
        {
            context.EvaluatedKeyword(false, ExpectedTypeObject, typeKeyword);
            return false;
        }
        else
        {
            context.EvaluatedKeyword(true, ExpectedTypeObject, typeKeyword);
        }

        return true;
    }

    /// <summary>
    /// Validates that a property count equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual != expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a property count does not equal the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is not equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountNotEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual == expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountNotEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountNotEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a property count is greater than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountGreaterThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual <= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountGreaterThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountGreaterThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a property count is greater than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountGreaterThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual < expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountGreaterThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountGreaterThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a property count is less than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountLessThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual >= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountLessThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountLessThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a property count is less than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPropertyCountLessThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual > expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedPropertyCountLessThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedPropertyCountLessThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountNotEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountNotEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountGreaterThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountGreaterThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountGreaterThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountGreaterThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountLessThanValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountLessThan.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating the expected value for a property count.
    /// </summary>
    /// <param name="value">The expected property count.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyCountLessThanOrEqualsValue(int value, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyCountLessThanOrEquals.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendQuotedInteger(value, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating that a property name was intended to match a regular expression.
    /// </summary>
    /// <param name="expression">The regular expression that should be matched.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedMatchPatternPropertySchemaValue(string expression, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedMatchPatternPropertySchema.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(expression, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating that a property name was intended to match a regular expression.
    /// </summary>
    /// <param name="expression">The regular expression that should be matched.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedPropertyNameMatchesRegularExpressionValue(string expression, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedPropertyNameMatchesRegularExpression.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(expression, buffer, ref written);
    }

    /// <summary>
    /// Tries to write a message indicating that a value was expected to match a schema becaused it contained a specific named property.
    /// </summary>
    /// <param name="propertyName">The name of the property that caused the schema to mat.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the operation succeeded; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool ExpectedMatchesDependentSchemaValue(string propertyName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedMatchesDependentSchema.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(propertyName, buffer, ref written);
    }

    /// <summary>
    /// Creates a message indicating that a required property is not present.
    /// </summary>
    /// <param name="propertyName">The name of the missing required property.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the message was successfully written; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RequiredPropertyNotPresent(ReadOnlySpan<byte> propertyName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_RequiredPropertyNotPresent.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(propertyName, buffer, ref written);
    }

    /// <summary>
    /// Creates a message indicating that a required property is present.
    /// </summary>
    /// <param name="propertyName">The name of the required property that is present.</param>
    /// <param name="buffer">The buffer to write the message to.</param>
    /// <param name="written">The number of bytes written to the buffer.</param>
    /// <returns><see langword="true"/> if the message was successfully written; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RequiredPropertyPresent(ReadOnlySpan<byte> propertyName, Span<byte> buffer, out int written)
    {
        if (!JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_RequiredPropertyPresent.AsSpan(), buffer, out written))
        {
            return false;
        }

        return AppendSingleQuotedValue(propertyName, buffer, ref written);
    }
}