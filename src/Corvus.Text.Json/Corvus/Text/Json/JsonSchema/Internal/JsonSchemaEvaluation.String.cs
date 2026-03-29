// <copyright file="JsonSchemaEvaluation.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Corvus.Globalization;

namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    /// <summary>
    /// A message provider for ignored non-string type validation.
    /// </summary>
    public static readonly JsonSchemaMessageProvider IgnoredNotTypeString = static (buffer, out written) => IgnoredNotType("string"u8, buffer, out written);

    /// <summary>
    /// A message provider for expected string type validation.
    /// </summary>
    public static readonly JsonSchemaMessageProvider ExpectedTypeString = static (buffer, out written) => ExpectedType("string"u8, buffer, out written);

    /// <summary>
    /// A message provider for expected string constant validation.
    /// </summary>
    public static readonly JsonSchemaMessageProvider<string> ExpectedStringEquals = static (constantValue, buffer, out written) => ExpectedStringEqualsValue(constantValue, buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedDate = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIso8601Date.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedDateTime = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIso8601OffsetDateTime.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedDuration = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIso8601Duration.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedEmail = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedEmail.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedHostname = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedHostname.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIdnEmail = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIdnEmail.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIdnHostname = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIdnHostname.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIPV4 = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIPV4.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIPV6 = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIPV6.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIri = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIri.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedIriReference = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIriReference.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedJsonPointer = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedJsonPointer.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedRegex = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedRegex.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedBase64String = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedBase64String.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedJsonContent = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedJsonContent.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedBase64Content = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedBase64Content.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedRelativeJsonPointer = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedRelativeJsonPointer.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedTime = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedIso8601OffsetTime.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUri = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedUri.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUriReference = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedUriReference.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUriTemplate = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedUriTemplate.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider ExpectedUuid = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_ExpectedUuid.AsSpan(), buffer, out written);

    private static readonly JsonSchemaMessageProvider<string> ExpectedStringMatchesRegularExpression = static (expression, buffer, out written) => ExpectedMatchRegularExpression(expression, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthEquals = static (length, buffer, out written) => ExpectedLengthEquals(length, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthNotEquals = static (length, buffer, out written) => ExpectedLengthNotEquals(length, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthLessThan = static (length, buffer, out written) => ExpectedLengthLessThan(length, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthLessThanOrEquals = static (length, buffer, out written) => ExpectedLengthLessThanOrEquals(length, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthGreaterThan = static (length, buffer, out written) => ExpectedLengthGreaterThan(length, buffer, out written);

    private static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthGreaterThanOrEquals = static (length, buffer, out written) => ExpectedLengthGreaterThanOrEquals(length, buffer, out written);

    /// <summary>
    /// Gets the allowed characters for the local part of an email address.
    /// </summary>
    private static ReadOnlySpan<byte> AllowedLocalCharacters => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-/=?^_`{|}~"u8;

    /// <summary>
    /// Gets the allowed characters for the local part of an email address.
    /// </summary>
    private static ReadOnlySpan<byte> AllowedLocalCharactersInQuotedString => "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!#$%&'*+-/=?^_`{|}~\"@. \r\n\t"u8;

    private static ReadOnlySpan<int> DisallowedIdn =>
            [0x0640, 0x07FA, 0x302E, 0x302F,
        0x3031, 0x3032, 0x3033, 0x3034,
        0x3035, 0x303B];

    private static ReadOnlySpan<int> ViramaTable =>
            [0x094D, 0x09CD, 0x0A4D, 0x0ACD,
        0x0B4D, 0x0BCD, 0x0C4D, 0x0CCD,
        0x0D3B, 0x0D3C, 0x0D4D, 0x0DCA,
        0x0E3A, 0x0EBA, 0x0F84, 0x1039,
        0x103A, 0x1714, 0x1715, 0x1734,
        0x17D2, 0x1A60, 0x1B44, 0x1BAA,
        0x1BAB, 0x1BF2, 0x1BF3, 0x2D7F,
        0xA806, 0xA82C, 0xA8C4, 0xA953,
        0xA9C0, 0xAAF6, 0xABED, 0x10A3F,
        0x11046, 0x11070, 0x1107F, 0x110B9,
        0x11133, 0x11134, 0x111C0, 0x11235,
        0x112EA, 0x1134D, 0x11442, 0x114C2,
        0x115BF, 0x1163F, 0x116B6, 0x1172B,
        0x11839, 0x1193D, 0x1193E, 0x119E0,
        0x11A34, 0x11A47, 0x11A99, 0x11C3F,
        0x11D44, 0x11D45, 0x11D97];

    /// <summary>
    /// Validates that a string length equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRegularExpression(ReadOnlySpan<byte> value, Regex regularExpression, string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
#if NET
        int desiredLength = Encoding.UTF8.GetMaxCharCount(value.Length);
        char[]? rentedChars = null;
        Span<char> transcodedValue = desiredLength < JsonConstants.StackallocCharThreshold ?
            stackalloc char[desiredLength] :
            (rentedChars = ArrayPool<char>.Shared.Rent(desiredLength));

        try
        {
            JsonReaderHelper.TryGetTextFromUtf8(value, transcodedValue, out int written);

            if (!regularExpression.IsMatch(transcodedValue.Slice(0, written)))
            {
                context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
                return false;
            }

            context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
            return true;
        }
        finally
        {
            if (rentedChars is char[] c)
            {
                ArrayPool<char>.Shared.Return(c);
            }
        }
#else
        // We pay for a conversion to string because netstandard does not support regular expressions
        // from Span<char>
        if (!regularExpression.IsMatch(JsonReaderHelper.GetTextFromUtf8(value)))
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
#endif
    }

    /// <summary>
    /// Validates that a string length equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRegularExpression(ReadOnlySpan<byte> value, Regex regularExpression)
    {
#if NET
        int desiredLength = Encoding.UTF8.GetMaxCharCount(value.Length);
        char[]? rentedChars = null;
        Span<char> transcodedValue = desiredLength < JsonConstants.StackallocCharThreshold ?
            stackalloc char[desiredLength] :
            (rentedChars = ArrayPool<char>.Shared.Rent(desiredLength));

        try
        {
            JsonReaderHelper.TryGetTextFromUtf8(value, transcodedValue, out int written);

            return regularExpression.IsMatch(transcodedValue.Slice(0, written));
        }
        finally
        {
            if (rentedChars is char[] c)
            {
                ArrayPool<char>.Shared.Return(c);
            }
        }
#else
        // We pay for a conversion to string because netstandard does not support regular expressions
        // from Span<char>
        return regularExpression.IsMatch(JsonReaderHelper.GetTextFromUtf8(value));
#endif
    }

    /// <summary>
    /// Records a successful match for a noop regular expression pattern
    /// (one that always matches, such as <c>.*</c>).
    /// </summary>
    /// <param name="originalExpressionString">The original pattern string for diagnostics.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    [CLSCompliant(false)]
    public static void MatchNoopRegularExpression(string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
    }

    /// <summary>
    /// Validates that a string is non-empty, as a replacement for patterns like <c>.+</c>.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="originalExpressionString">The original pattern string for diagnostics.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the string is non-empty; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchNonEmptyRegularExpression(ReadOnlySpan<byte> value, string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (value.Length == 0)
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string starts with a literal prefix, as a replacement for patterns like <c>^prefix</c>.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="prefix">The expected UTF-8 encoded prefix.</param>
    /// <param name="originalExpressionString">The original pattern string for diagnostics.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the string starts with the prefix; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchPrefixRegularExpression(ReadOnlySpan<byte> value, ReadOnlySpan<byte> prefix, string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!value.StartsWith(prefix))
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string's Unicode codepoint length is within a range, as a replacement for patterns like <c>^.{n,m}$</c>.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="min">The minimum number of Unicode codepoints.</param>
    /// <param name="max">The maximum number of Unicode codepoints.</param>
    /// <param name="originalExpressionString">The original pattern string for diagnostics.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the string length is within the range; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRangeRegularExpression(ReadOnlySpan<byte> value, int min, int max, string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        int runeCount = JsonElementHelpers.CountRunes(value);

        if (runeCount < min || runeCount > max)
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a UTF-8 byte span's Unicode codepoint length is within a range.
    /// Used for <c>patternProperties</c> matching where no context reporting is needed.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value to check.</param>
    /// <param name="min">The minimum number of Unicode codepoints.</param>
    /// <param name="max">The maximum number of Unicode codepoints.</param>
    /// <returns><see langword="true"/> if the length is within range; otherwise, <see langword="false"/>.</returns>
    public static bool MatchRangeRegularExpression(ReadOnlySpan<byte> value, int min, int max)
    {
        int runeCount = JsonElementHelpers.CountRunes(value);
        return runeCount >= min && runeCount <= max;
    }

    /// <summary>
    /// Validates that a string equals the given value.
    /// </summary>
    /// <param name="actual">The UTF-8 encoded string value to validate.</param>
    /// <param name="expected">The UTF-8 encoded string value expected.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchStringConstantValue(ReadOnlySpan<byte> actual, ReadOnlySpan<byte> expected, string expectedString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!actual.SequenceEqual(expected))
        {
            context.EvaluatedKeyword(false, expectedString, messageProvider: ExpectedStringEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expectedString, ExpectedStringEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length equals the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual != expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length does not equal the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is not equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthNotEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual == expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthNotEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthNotEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length is greater than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthGreaterThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual <= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthGreaterThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthGreaterThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length is greater than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is greater than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthGreaterThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual < expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthGreaterThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthGreaterThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length is less than the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthLessThan(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual >= expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthLessThan, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthLessThan, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string length is less than or equal to the given value.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is less than or equal to the given value; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchLengthLessThanOrEquals(int expected, int actual, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (actual > expected)
        {
            context.EvaluatedKeyword(false, expected, messageProvider: ExpectedStringLengthLessThanOrEquals, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, expected, ExpectedStringLengthLessThanOrEquals, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value conforms to the ISO 8601 date format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid ISO 8601 date; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchDate(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.TryParseLocalDate(value, out _))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDate, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedDate, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value conforms to the ISO 8601 offset date-time format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid ISO 8601 offset date-time; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchDateTime(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.TryParseOffsetDateTime(value, out _))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDateTime, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedDateTime, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value conforms to the ISO 8601 duration format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid ISO 8601 duration; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchDuration(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.TryParsePeriod(value, out _))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedDuration, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedDuration, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid email address; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchEmail(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchEmail(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedEmail, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedEmail, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid hostname; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchHostname(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchHostname(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedHostname, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedHostname, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN email address; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIdnEmail(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIdnEmail(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIdnEmail, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIdnEmail, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN hostname; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIdnHostname(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIdnHostname(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIdnHostname, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIdnHostname, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid IPv4 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv4 address; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIPV4(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIPV4(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIPV4, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIPV4, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid IPv6 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv6 address; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIPV6(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIPV6(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIPV6, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIPV6, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIri(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIri(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIri, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIri, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) reference format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI reference; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchIriReference(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIriReference(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedIriReference, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedIriReference, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchJsonPointer(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchJsonPointer(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedJsonPointer, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedJsonPointer, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid ECMAScript regular expression format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid regex; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRegex(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRegex(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedRegex, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedRegex, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid relative JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid relative JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRelativeJsonPointer(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRelativeJsonPointer(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedRelativeJsonPointer, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedRelativeJsonPointer, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value conforms to the ISO 8601 offset time format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid ISO 8601 offset time; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTime(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!JsonElementHelpers.TryParseOffsetTime(value, out _))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedTime, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedTime, keyword);
        return true;
    }

    /// <summary>
    /// Matches a JSON token type against the string type constraint.
    /// </summary>
    /// <param name="tokenType">The JSON token type to validate.</param>
    /// <param name="typeKeyword">The type keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the token type matches the string type constraint; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchTypeString(JsonTokenType tokenType, ReadOnlySpan<byte> typeKeyword, ref JsonSchemaContext context)
    {
        // Allow property names for strings
        if (tokenType is not (JsonTokenType.String or JsonTokenType.PropertyName))
        {
            context.EvaluatedKeyword(false, ExpectedTypeString, typeKeyword);
            return false;
        }
        else
        {
            context.EvaluatedKeyword(true, ExpectedTypeString, typeKeyword);
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid URI format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid URI; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUri(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUri(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUri, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUri, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid URI reference format (absolute or relative URI).
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid URI reference; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUriReference(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUriReference(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUriReference, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUriReference, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid URI template format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid URI template; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUriTemplate(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUriTemplate(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUriTemplate, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUriTemplate, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid UUID format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid UUID; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchUuid(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUuid(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedUuid, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedUuid, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Base64-encoded string.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid Base64-encoded string; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchBase64String(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchBase64String(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedBase64String, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedBase64String, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value contains valid JSON content.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is valid JSON content; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchJsonContent(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchJsonContent(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedJsonContent, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedJsonContent, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Base64-encoded JSON document.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid Base64-encoded JSON document; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchBase64Content(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchBase64Content(value))
        {
            context.EvaluatedKeyword(false, messageProvider: ExpectedBase64Content, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, ExpectedBase64Content, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a decoded UTF-8 hostname value conforms to the hostname format requirements.
    /// </summary>
    /// <param name="value">The decoded UTF-8 hostname value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid decoded hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchDecodedHostname(ReadOnlySpan<byte> value)
    {
        bool wasLastDot = false;
        int i = 0;
        Rune previousRune = default;
        bool hasHiraganaKatakanaOrHan = false;
        bool hasKatakankaMiddleDot = false;
        bool hasArabicIndicDigits = false;
        bool hasExtendedArabicIndicDigits = false;
        int runeCount = 0;
        while (i < value.Length)
        {
            // Increment the rune count to make it 1-based
            runeCount++;

            byte byteValue = value[i];
            Rune.DecodeFromUtf8(value.Slice(i), out Rune rune, out int bytesConsumed);

            if (i == 0)
            {
                System.Globalization.UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                if (category == System.Globalization.UnicodeCategory.SpacingCombiningMark ||
                    category == System.Globalization.UnicodeCategory.EnclosingMark ||
                    category == System.Globalization.UnicodeCategory.NonSpacingMark)
                {
                    return false;
                }
            }

            i += bytesConsumed;

            hasHiraganaKatakanaOrHan |= IsHiraganaKatakanaOrHanNotMiddleDot(rune.Value);
            hasArabicIndicDigits |= IsArabicIndicDigit(rune.Value);
            hasExtendedArabicIndicDigits |= IsExtendedArabicIndicDigit(rune.Value);

            if (wasLastDot)
            {
                if (!Rune.IsLetter(rune))
                {
                    return false;
                }
            }

            if (DisallowedIdn.IndexOf(rune.Value) >= 0)
            {
                // Disallowed characters in IDN
                return false;
            }

            if (!Rune.IsLetterOrDigit(rune))
            {
                if (byteValue == (byte)'.' || rune.Value == 0x3002 || rune.Value == 0xFF0E || rune.Value == 0xFF61)
                {
                    if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
                    {
                        // If we have a Katakana middle dot, it must be have a Hiragana, Katakana or Han character.
                        return false;
                    }

                    if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
                    {
                        // You are not permitted both arabic indic AND extended arabic indic
                        return false;
                    }

                    hasHiraganaKatakanaOrHan = false;
                    hasKatakankaMiddleDot = false;
                    hasArabicIndicDigits = false;
                    hasExtendedArabicIndicDigits = false;
                    wasLastDot = true;
                    previousRune = rune;
                    continue;
                }

                hasKatakankaMiddleDot |= (rune.Value == 0x30FB);

                // First we do all the items that are allowed at the first character
                Rune lookahead = default;

                if (rune.Value == 0x0375)
                {
                    // If we have a Greek Keraia, it must be followed by a Greek character.
                    // If we are the last character, that's a fail
                    if (i >= value.Length)
                    {
                        return false;
                    }

                    Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);

                    // If the next character is not Greek, that's a fail
                    if (!IsGreek(lookahead.Value))
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                // Middle dot
                if (rune.Value == 0x00B7)
                {
                    if (previousRune.Value != 0x006C)
                    {
                        return false;
                    }

                    if (lookahead.Value == 0)
                    {
                        Rune.DecodeFromUtf8(value.Slice(i), out lookahead, out _);
                        if (lookahead.Value != 0x006C)
                        {
                            return false;
                        }
                    }
                }

                // These are all the tests which require preceding characters
                if (byteValue == (byte)'-')
                {
                    if (i == bytesConsumed || i >= value.Length)
                    {
                        return false;
                    }

                    // Positions 3 and 4 must not be '--'
                    if (runeCount == 4 && previousRune.Value == 0x002d)
                    {
                        return false;
                    }

                    wasLastDot = false;
                    previousRune = rune;
                    continue;
                }

                // ZERO WIDTH JOINER not preceded by Virama
                if (rune.Value == 0x200D && !IsVirama(previousRune.Value))
                {
                    return false;
                }

                // If we are Geresh or Gershayim, the previous rune must be Hebrew
                if ((rune.Value == 0x05F3 || rune.Value == 0x05F4) && !IsHebrew(previousRune.Value))
                {
                    return false;
                }
            }

            wasLastDot = false;
            previousRune = rune;
        }

        if (hasKatakankaMiddleDot && !hasHiraganaKatakanaOrHan)
        {
            // If we have a Katakana middle dot, it must be have a Hiragana, Katakana or Han character.
            return false;
        }

        if (hasArabicIndicDigits && hasExtendedArabicIndicDigits)
        {
            // You are not permitted both Arabic Indic AND extended Arabic Indic
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid email address; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchEmail(ReadOnlySpan<byte> value)
    {
        if (value.Length > 320 || value.Length < 3)
        {
            // The maximum length of an email address is 320 characters (RFC 5321).
            return false;
        }

        int atIndex = value.LastIndexOf((byte)'@');
        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return false;
        }

        // Local part
        ReadOnlySpan<byte> segment = value.Slice(0, atIndex);

        if (!MatchEmailLocalPart(segment))
        {
            return false;
        }

        // Domain part
        segment = value.Slice(atIndex + 1);

        if (segment.Length > 2)
        {
            if (segment[0] == (byte)'[' && segment[segment.Length - 1] == (byte)']')
            {
                // This is an address literal, so we need to validate the inside of the brackets as an IP address
                ReadOnlySpan<byte> addressLiteral = segment.Slice(1, segment.Length - 2);
                if (addressLiteral.StartsWith("IPv6:"u8))
                {
                    return MatchIPV6(addressLiteral.Slice(5));
                }
                else
                {
                    return MatchIPV4(addressLiteral);
                }
            }
        }

        return MatchHostname(segment);
    }

    /// <summary>
    /// Validates that a string value is a valid hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchHostname(ReadOnlySpan<byte> value)
    {
        if (value.Length > 253)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[256];
        int i = 0;
        int characterCount = 0;
        byte lastAscii = 0;
        bool decodePunicode = false;
        while (i < value.Length)
        {
            if (value[i] > 0x7F)
            {
                // This is not ASCII, so give up
                return false;
            }

            if (lastAscii == (byte)'-' && value[i] == (byte)'-')
            {
                // Look for punicode signature
                if (characterCount != 3 ||
                    !((value[i - 3] == (byte)'x' || value[i - 3] == (byte)'X') &&
                      (value[i - 2] == (byte)'n' || value[i - 2] == (byte)'N')))
                {
                    // Disallow "--" for non-punicode signature
                    return false;
                }

                decodePunicode = true;
                break;
            }

            lastAscii = value[i];

            if (lastAscii == (byte)'.')
            {
                if (characterCount > 63)
                {
                    return false;
                }

                characterCount = 0;
                i++;
                continue;
            }

            if (!char.IsLetterOrDigit((char)lastAscii) && !(characterCount != 0 && lastAscii == (byte)'-'))
            {
                return false;
            }

            characterCount++;
            i++;
        }

        if (decodePunicode)
        {
            if (!IdnMapping.Default.GetUnicode(value, decoded, out int written))
            {
                return false;
            }

            scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

            return MatchDecodedHostname(segment);
        }

        if (characterCount == 0 || characterCount > 63)
        {
            return false;
        }

        if (lastAscii == '-' || lastAscii == '.')
        {
            return false;
        }

        if (value[0] == '.')
        {
            return false;
        }

        if (value.Length > 3 &&
            value[2] == '-' &&
            value[3] == '-')
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) email address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN email address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnEmail(ReadOnlySpan<byte> value)
    {
        if (value.Length > 320 || value.Length < 3)
        {
            // The maximum length of an email address is 320 characters (RFC 5321).
            return false;
        }

        int atIndex = value.LastIndexOf((byte)'@');

        if (atIndex <= 0 || atIndex == value.Length - 1)
        {
            return false;
        }

        // Local part
        ReadOnlySpan<byte> segment = value.Slice(0, atIndex);

        if (!MatchEmailLocalPartUnicode(segment))
        {
            return false;
        }

        // Domain part
        segment = value.Slice(atIndex + 1);

        return MatchIdnHostname(segment);
    }

    /// <summary>
    /// Validates that a string value is a valid internationalized domain name (IDN) hostname format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IDN hostname; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnHostname(ReadOnlySpan<byte> value)
    {
        if (value.Length > 254)
        {
            return false;
        }

        Span<byte> decoded = stackalloc byte[256];

        if (!IdnMapping.Default.GetUnicode(value, decoded, out int written))
        {
            return false;
        }

        scoped ReadOnlySpan<byte> segment = decoded.Slice(0, written);

        return MatchDecodedHostname(segment);
    }

    /// <summary>
    /// Validates that a string value is a valid IPv4 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv4 address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV4(ReadOnlySpan<byte> value)
    {
        if (value.Length > IPAddressParser.MaxIPv4StringLength)
        {
            return false;
        }

        return IPAddressParser.IsValidIPV4(value);
    }

    /// <summary>
    /// Validates that a string value is a valid IPv6 address format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IPv6 address; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV6(ReadOnlySpan<byte> value)
    {
        if (value.Length > IPAddressParser.MaxIPv6StringLength)
        {
            return false;
        }

        return IPAddressParser.IsValidIPV6(value);
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIri(ReadOnlySpan<byte> value)
    {
        // Uri.TryCreate considers full-qualified file paths to be acceptable as absolute Uris.
        // This means that on Linux "/abc" is considered an acceptable absolute Uri! (This is
        // conceptually equivalent to "C:\abc" being an absolute Uri on Windows, but it's more
        // of a problem because a lot of relative Uris of the kind you come across on the web
        // look exactly like Unix file paths.)
        // https:// github.com/dotnet/runtime/issues/22718
        // However, this only needs to be a problem if you insist that the Uri is absolute.
        // If you accept either absolute or relative Uris, it will interpret "/abc" as a
        // relative Uri on either Windows or Linux. It only interprets it as an absolute Uri
        // if you pass UriKind.Absolute when parsing.
        // This is why we take the peculiar-looking step of passing UriKind.RelativeOrAbsolute
        // and then rejecting relative Uris. This causes this method to reject "/abc" on all
        // platforms. Back when we passed UriKind.Absolute, this code incorrectly accepted
        // "abc".
        if (!Utf8UriTools.Validate(value, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: true, allowIri: true, allowUNCPath: false))
        {
            // We may need the extra tests here
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Internationalized Resource Identifier (IRI) reference format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid IRI reference; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIriReference(ReadOnlySpan<byte> value)
    {
        return Utf8UriTools.Validate(value, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: true, allowUNCPath: false);
    }

    /// <summary>
    /// Validates that a string value is a valid JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchJsonPointer(ReadOnlySpan<byte> value)
    {
        return Utf8JsonPointerTools.Validate(value);
    }

    /// <summary>
    /// Validates that a string value is a valid ECMAScript regular expression format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid regex; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchRegex(ReadOnlySpan<byte> value)
    {
        char[]? charBuffer = null;
        int length = Encoding.UTF8.GetMaxCharCount(value.Length);

        Span<char> buffer = length < JsonConstants.StackallocNonRecursiveCharThreshold ? stackalloc char[length]
            : (charBuffer = ArrayPool<char>.Shared.Rent(length)).AsSpan();

        int written = 0;

        try
        {
            written = JsonReaderHelper.TranscodeHelper(value, buffer);

            return JsonRegexValidator.Validate(buffer.Slice(0, written), JsonRegexOptions.ECMAScript);
        }
        finally
        {
            if (charBuffer is not null)
            {
                if (written > 0)
                {
                    // Clear the buffer to avoid leaking sensitive information.
                    Array.Clear(charBuffer, 0, written);
                }

                ArrayPool<char>.Shared.Return(charBuffer);
            }
        }
    }

    /// <summary>
    /// Validates that a string value is a valid relative JSON Pointer format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid relative JSON Pointer; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchRelativeJsonPointer(ReadOnlySpan<byte> value)
    {
        return Utf8JsonPointerTools.ValidateRelative(value);
    }

    /// <summary>
    /// Validates that a string value is a valid URI format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUri(ReadOnlySpan<byte> value)
    {
        // Uri.TryCreate considers full-qualified file paths to be acceptable as absolute Uris.
        // This means that on Linux "/abc" is considered an acceptable absolute Uri! (This is
        // conceptually equivalent to "C:\abc" being an absolute Uri on Windows, but it's more
        // of a problem because a lot of relative Uris of the kind you come across on the web
        // look exactly like Unix file paths.)
        // https:// github.com/dotnet/runtime/issues/22718
        // However, this only needs to be a problem if you insist that the Uri is absolute.
        // If you accept either absolute or relative Uris, it will interpret "/abc" as a
        // relative Uri on either Windows or Linux. It only interprets it as an absolute Uri
        // if you pass UriKind.Absolute when parsing.
        // This is why we take the peculiar-looking step of passing UriKind.RelativeOrAbsolute
        // and then rejecting relative Uris. This causes this method to reject "/abc" on all
        // platforms. Back when we passed UriKind.Absolute, this code incorrectly accepted
        // "abc".
        if (!Utf8UriTools.Validate(value, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: true, allowIri: false, allowUNCPath: false))
        {
            // We may need the extra tests here
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid URI reference format (absolute or relative URI).
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI reference; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriReference(ReadOnlySpan<byte> value)
    {
        if (!Utf8UriTools.Validate(value, Utf8UriKind.RelativeOrAbsolute, requireAbsolute: false, allowIri: false, allowUNCPath: false))
        {
            // We may need the extra tests for empty fragment etc.
            return false;
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid URI template format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid URI template; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriTemplate(ReadOnlySpan<byte> value)
    {
        return Utf8UriTemplate.Validate(value);
    }

    /// <summary>
    /// Validates that a string value is a valid UUID format.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid UUID; otherwise, <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUuid(ReadOnlySpan<byte> value)
    {
        return Utf8Parser.TryParse(value, out Guid _, out _);
    }

    private static bool IsArabicIndicDigit(int value) => (value >= 0x0660 && value <= 0x0669);

    private static bool IsExtendedArabicIndicDigit(int value) => (value >= 0x06F0 && value <= 0x06F9);

    private static bool IsGreek(int value) => (value >= 0x0370 && value <= 0x03FF) || (value >= 0x1F00 && value <= 0x1FFF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHebrew(int value) => (value >= 0x0590 && value <= 0x05FF);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsHiraganaKatakanaOrHanNotMiddleDot(int value)
    {
        // Don't allow middle dot
        if (value == 0x30FB)
        {
            return false;
        }

        return
            (value >= 0x30A0 && value <= 0x30FF) ||
            (value >= 0x3040 && value <= 0x309F) ||
            (value >= 0x3400 && value <= 0x4DB5) ||
            (value >= 0x4E00 && value <= 0x9FCB) ||
            (value >= 0xF900 && value <= 0xFA6A);
    }

    private static bool IsVirama(int value) => ViramaTable.IndexOf(value) >= 0;

    private static bool MatchEmailLocalPart(ReadOnlySpan<byte> segment)
    {
        if (segment.Length > 64)
        {
            return false;
        }

        // Skip an opening comment
        if (segment[0] == (byte)'(')
        {
            int closeBracket = segment.IndexOf((byte)')');
            if (closeBracket < 0)
            {
                return false;
            }

            segment = segment.Slice(closeBracket + 1);

            if (segment.Length == 0)
            {
                return false;
            }
        }

        int lastDot = -1;

        if (segment[0] == '\"')
        {
            if (segment[segment.Length - 1] != '\"')
            {
                return false;
            }

            for (int i = 1; i < segment.Length - 1; i++)
            {
                byte c = segment[i];

                if (AllowedLocalCharactersInQuotedString.IndexOf(c) < 0)
                {
                    // Invalid character in quoted string local part
                    return false;
                }
            }

            return true;
        }

        for (int i = 0; i < segment.Length; i++)
        {
            byte c = segment[i];

            if (c == (byte)'.')
            {
                if (i == 0 || i == segment.Length - 1 || lastDot == i - 1)
                {
                    // Dot at the start or end, or two dots in a row
                    return false;
                }

                lastDot = i;
            }
            else if (c == (byte)'(')
            {
                // This is an end comment.
                int closeBracket = segment.IndexOf((byte)')');
                return closeBracket >= 0 && closeBracket == segment.Length - 1;
            }
            else if (AllowedLocalCharacters.IndexOf(c) < 0)
            {
                // Invalid character in local part
                return false;
            }
        }

        return true;
    }

    private static bool MatchEmailLocalPartUnicode(ReadOnlySpan<byte> segment)
    {
        if (segment.Length > 64)
        {
            return false;
        }

        // Skip an opening comment
        if (segment[0] == (byte)'(')
        {
            int closeBracket = segment.IndexOf((byte)')');
            if (closeBracket < 0)
            {
                return false;
            }

            segment = segment.Slice(closeBracket + 1);

            if (segment.Length == 0)
            {
                return false;
            }
        }

        int lastDot = -1;
        for (int i = 0; i < segment.Length; i++)
        {
            byte c = segment[i];
            if (c == (byte)'.')
            {
                if (i == 0 || i == segment.Length - 1 || lastDot == i - 1)
                {
                    // Dot at the start or end, or two dots in a row
                    return false;
                }

                lastDot = i;
            }
            else if (c == (byte)'(')
            {
                // This is an end comment.
                int closeBracket = segment.IndexOf((byte)')');
                return closeBracket >= 0 && closeBracket == segment.Length - 1;
            }
            else if (AllowedLocalCharacters.IndexOf(c) < 0)
            {
                // This could be a unicode character, so let's check
                Rune.DecodeFromUtf8(segment.Slice(i), out Rune rune, out int bytesConsumed);
                if (!Rune.IsLetterOrDigit(rune))
                {
                    System.Globalization.UnicodeCategory category = Rune.GetUnicodeCategory(rune);
                    if (i == 0 ||
                        (category != System.Globalization.UnicodeCategory.SpacingCombiningMark &&
                         category != System.Globalization.UnicodeCategory.EnclosingMark &&
                         category != System.Globalization.UnicodeCategory.NonSpacingMark))
                    {
                        return false;
                    }
                }

                i += bytesConsumed - 1; // Adjust i to account for the bytes consumed by the rune
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that a string value is a valid Base64-encoded string.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid Base64-encoded string; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchBase64String(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        int decodedLength = Base64.GetMaxDecodedFromUtf8Length(value.Length);

        byte[]? rentedArray = null;
        Span<byte> decoded = decodedLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(decodedLength));

        try
        {
            OperationStatus status = Base64.DecodeFromUtf8(value, decoded, out _, out _, isFinalBlock: true);
            return status == OperationStatus.Done;
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    /// <summary>
    /// Validates that a string value contains valid JSON content.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is valid JSON content; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchJsonContent(ReadOnlySpan<byte> value)
    {
        try
        {
            Utf8JsonReader reader = new(value, isFinalBlock: true, default);

            // Read through the entire document to verify it is well-formed.
            while (reader.Read())
            {
            }

            // The reader must have consumed at least one token.
            return reader.BytesConsumed > 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Validates that a string value is a valid Base64-encoded JSON document.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is valid Base64-encoded JSON content; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchBase64Content(ReadOnlySpan<byte> value)
    {
        if (value.Length == 0)
        {
            return true;
        }

        int decodedLength = Base64.GetMaxDecodedFromUtf8Length(value.Length);

        byte[]? rentedArray = null;
        Span<byte> decoded = decodedLength <= JsonConstants.StackallocByteThreshold
            ? stackalloc byte[JsonConstants.StackallocByteThreshold]
            : (rentedArray = ArrayPool<byte>.Shared.Rent(decodedLength));

        try
        {
            OperationStatus status = Base64.DecodeFromUtf8(value, decoded, out _, out int bytesWritten, isFinalBlock: true);

            if (status != OperationStatus.Done)
            {
                return false;
            }

            return MatchJsonContent(decoded.Slice(0, bytesWritten));
        }
        finally
        {
            if (rentedArray != null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }
}