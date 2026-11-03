// <copyright file="JsonSchemaEvaluation.String.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
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

    public static readonly JsonSchemaMessageProvider ExpectedDate = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIso8601Date ??= Utf8(SR.JsonSchema_ExpectedIso8601Date), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedDateTime = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIso8601OffsetDateTime ??= Utf8(SR.JsonSchema_ExpectedIso8601OffsetDateTime), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedDuration = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIso8601Duration ??= Utf8(SR.JsonSchema_ExpectedIso8601Duration), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedEmail = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedEmail ??= Utf8(SR.JsonSchema_ExpectedEmail), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedHostname = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedHostname ??= Utf8(SR.JsonSchema_ExpectedHostname), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIdnEmail = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIdnEmail ??= Utf8(SR.JsonSchema_ExpectedIdnEmail), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIdnHostname = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIdnHostname ??= Utf8(SR.JsonSchema_ExpectedIdnHostname), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIPV4 = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIPV4 ??= Utf8(SR.JsonSchema_ExpectedIPV4), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIPV6 = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIPV6 ??= Utf8(SR.JsonSchema_ExpectedIPV6), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIri = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIri ??= Utf8(SR.JsonSchema_ExpectedIri), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedIriReference = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIriReference ??= Utf8(SR.JsonSchema_ExpectedIriReference), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedJsonPointer = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedJsonPointer ??= Utf8(SR.JsonSchema_ExpectedJsonPointer), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedRegex = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedRegex ??= Utf8(SR.JsonSchema_ExpectedRegex), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedBase64String = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedBase64String ??= Utf8(SR.JsonSchema_ExpectedBase64String), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedJsonContent = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedJsonContent ??= Utf8(SR.JsonSchema_ExpectedJsonContent), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedBase64Content = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedBase64Content ??= Utf8(SR.JsonSchema_ExpectedBase64Content), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedRelativeJsonPointer = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedRelativeJsonPointer ??= Utf8(SR.JsonSchema_ExpectedRelativeJsonPointer), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedTime = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedIso8601OffsetTime ??= Utf8(SR.JsonSchema_ExpectedIso8601OffsetTime), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedUri = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedUri ??= Utf8(SR.JsonSchema_ExpectedUri), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedUriReference = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedUriReference ??= Utf8(SR.JsonSchema_ExpectedUriReference), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedUriTemplate = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedUriTemplate ??= Utf8(SR.JsonSchema_ExpectedUriTemplate), buffer, out written);

    public static readonly JsonSchemaMessageProvider ExpectedUuid = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ExpectedUuid ??= Utf8(SR.JsonSchema_ExpectedUuid), buffer, out written);

    public static readonly JsonSchemaMessageProvider<string> ExpectedStringMatchesRegularExpression = static (expression, buffer, out written) => ExpectedMatchRegularExpression(expression, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthEquals = static (length, buffer, out written) => ExpectedLengthEquals(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthNotEquals = static (length, buffer, out written) => ExpectedLengthNotEquals(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthLessThan = static (length, buffer, out written) => ExpectedLengthLessThan(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthLessThanOrEquals = static (length, buffer, out written) => ExpectedLengthLessThanOrEquals(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthGreaterThan = static (length, buffer, out written) => ExpectedLengthGreaterThan(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider<int> ExpectedStringLengthGreaterThanOrEquals = static (length, buffer, out written) => ExpectedLengthGreaterThanOrEquals(length, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningDate = static (buffer, out written) => WriteWarning(ExpectedDate, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningDateTime = static (buffer, out written) => WriteWarning(ExpectedDateTime, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningDuration = static (buffer, out written) => WriteWarning(ExpectedDuration, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningEmail = static (buffer, out written) => WriteWarning(ExpectedEmail, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningHostname = static (buffer, out written) => WriteWarning(ExpectedHostname, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIdnEmail = static (buffer, out written) => WriteWarning(ExpectedIdnEmail, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIdnHostname = static (buffer, out written) => WriteWarning(ExpectedIdnHostname, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIPV4 = static (buffer, out written) => WriteWarning(ExpectedIPV4, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIPV6 = static (buffer, out written) => WriteWarning(ExpectedIPV6, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIri = static (buffer, out written) => WriteWarning(ExpectedIri, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningIriReference = static (buffer, out written) => WriteWarning(ExpectedIriReference, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningJsonPointer = static (buffer, out written) => WriteWarning(ExpectedJsonPointer, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningRegex = static (buffer, out written) => WriteWarning(ExpectedRegex, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningRelativeJsonPointer = static (buffer, out written) => WriteWarning(ExpectedRelativeJsonPointer, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningTime = static (buffer, out written) => WriteWarning(ExpectedTime, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningUri = static (buffer, out written) => WriteWarning(ExpectedUri, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningUriReference = static (buffer, out written) => WriteWarning(ExpectedUriReference, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningUriTemplate = static (buffer, out written) => WriteWarning(ExpectedUriTemplate, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningUuid = static (buffer, out written) => WriteWarning(ExpectedUuid, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningBase64String = static (buffer, out written) => WriteWarning(ExpectedBase64String, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningJsonContent = static (buffer, out written) => WriteWarning(ExpectedJsonContent, buffer, out written);

    public static readonly JsonSchemaMessageProvider WarningBase64Content = static (buffer, out written) => WriteWarning(ExpectedBase64Content, buffer, out written);

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
            // Text that is not UTF-8 matches no pattern.
            if (!JsonReaderHelper.TryGetTextFromUtf8(value, transcodedValue, out int written) ||
                !regularExpression.IsMatch(transcodedValue.Slice(0, written)))
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
        // from Span<char>. Text that is not UTF-8 matches no pattern.
        if (!JsonWriterHelper.IsValidUtf8String(value) || !regularExpression.IsMatch(JsonReaderHelper.GetTextFromUtf8(value)))
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
            // Text that is not UTF-8 matches no pattern.
            return JsonReaderHelper.TryGetTextFromUtf8(value, transcodedValue, out int written) &&
                regularExpression.IsMatch(transcodedValue.Slice(0, written));
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
        // from Span<char>. Text that is not UTF-8 matches no pattern.
        return JsonWriterHelper.IsValidUtf8String(value) && regularExpression.IsMatch(JsonReaderHelper.GetTextFromUtf8(value));
#endif
    }

    /// <summary>
    /// Validates that a string matches an ECMA-262 regular expression, recording the result.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate, matched as it is.</param>
    /// <param name="regularExpression">The regular expression.</param>
    /// <param name="originalExpressionString">The pattern as written in the schema, for diagnostics.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value matches; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="RegexMatchTimeoutException">The match ran for longer than the expression's match timeout.</exception>
    /// <exception cref="ArgumentException">The value is not UTF-8.</exception>
    [CLSCompliant(false)]
    public static bool MatchRegularExpression(ReadOnlySpan<byte> value, Corvus.Ecma262.RegularExpressions.Regex regularExpression, string originalExpressionString, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRegularExpression(value, regularExpression))
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
    }

    /// <summary>
    /// Whether a string matches an ECMA-262 regular expression. The UTF-8 value is matched as it is, with no
    /// transcoding.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value.</param>
    /// <param name="regularExpression">The regular expression.</param>
    /// <returns><see langword="true"/> if the value matches; otherwise, <see langword="false"/>.</returns>
    /// <exception cref="RegexMatchTimeoutException">The match ran for longer than the expression's match timeout, or
    /// needed more backtracking memory than a match may hold. It is the exception of .NET's engine, as a match by a
    /// <see cref="Regex"/> would throw, so a caller that catches it sees the same.</exception>
    /// <exception cref="ArgumentException">The value is not UTF-8.</exception>
    [CLSCompliant(false)]
    public static bool MatchRegularExpression(ReadOnlySpan<byte> value, Corvus.Ecma262.RegularExpressions.Regex regularExpression)
    {
        try
        {
            return regularExpression.IsMatch(value);
        }
        catch (Corvus.Ecma262.RegularExpressions.RegexMatchTimeoutException)
        {
            ThrowRegexMatchTimeout(value, regularExpression);
            return false;
        }
    }

    [DoesNotReturn]
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void ThrowRegexMatchTimeout(ReadOnlySpan<byte> value, Corvus.Ecma262.RegularExpressions.Regex regularExpression)
    {
        throw new RegexMatchTimeoutException(JsonReaderHelper.GetTextFromUtf8(value), regularExpression.Pattern, regularExpression.MatchTimeout);
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
    /// As in ECMA-262, <c>.</c> does not match a line terminator, so a string containing one does not match.
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
        if (!MatchRangeRegularExpression(value, min, max))
        {
            context.EvaluatedKeyword(false, originalExpressionString, messageProvider: ExpectedStringMatchesRegularExpression, keyword);
            return false;
        }

        context.EvaluatedKeyword(true, originalExpressionString, ExpectedStringMatchesRegularExpression, keyword);
        return true;
    }

    /// <summary>
    /// Validates that a UTF-8 byte span's Unicode codepoint length is within a range and that it contains no line
    /// terminator, as a replacement for patterns like <c>^.{n,m}$</c>.
    /// Used for <c>patternProperties</c> matching where no context reporting is needed.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value to check.</param>
    /// <param name="min">The minimum number of Unicode codepoints.</param>
    /// <param name="max">The maximum number of Unicode codepoints.</param>
    /// <returns><see langword="true"/> if the length is within range; otherwise, <see langword="false"/>.</returns>
    public static bool MatchRangeRegularExpression(ReadOnlySpan<byte> value, int min, int max)
    {
        if (ContainsRegexLineTerminator(value))
        {
            return false;
        }

        // A rune is one to four bytes, so the byte length bounds the rune count both ways: only values inside that
        // band are counted.
        int bytes = value.Length;
        if (bytes < min || ((bytes + 3) >> 2) > max)
        {
            return false;
        }

        if (bytes <= max && ((bytes + 3) >> 2) >= min)
        {
            return true;
        }

        int runeCount = JsonElementHelpers.CountRunes(value);
        return runeCount >= min && runeCount <= max;
    }

    /// <summary>
    /// Determines whether a UTF-8 byte span contains a character other than a line terminator, as a replacement for
    /// the patterns <c>.</c> and <c>.+</c> (ECMA-262's <c>.</c> matches anything but <c>\n</c>, <c>\r</c>, U+2028
    /// and U+2029).
    /// Used for <c>patternProperties</c> matching where no context reporting is needed.
    /// </summary>
    /// <param name="value">The UTF-8 encoded value to check.</param>
    /// <returns><see langword="true"/> if some character is not a line terminator; otherwise, <see langword="false"/>.</returns>
    public static bool MatchNonEmptyRegularExpression(ReadOnlySpan<byte> value)
    {
        int i = 0;
        while (i < value.Length)
        {
            byte b = value[i];
            if (b == (byte)'\n' || b == (byte)'\r')
            {
                i++;
            }
            else if (b == 0xE2 && i + 2 < value.Length && value[i + 1] == 0x80 && (value[i + 2] == 0xA8 || value[i + 2] == 0xA9))
            {
                i += 3;
            }
            else
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a UTF-8 byte span contains an ECMA-262 line terminator (<c>\n</c>, <c>\r</c>, U+2028 or U+2029).
    /// </summary>
    /// <param name="value">The UTF-8 encoded value to check.</param>
    /// <returns><see langword="true"/> if the value contains a line terminator; otherwise, <see langword="false"/>.</returns>
    internal static bool ContainsRegexLineTerminator(ReadOnlySpan<byte> value)
    {
        if (value.IndexOfAny((byte)'\n', (byte)'\r') >= 0)
        {
            return true;
        }

        int i = value.IndexOf((byte)0xE2);
        while (i >= 0 && i + 2 < value.Length)
        {
            if (value[i + 1] == 0x80 && (value[i + 2] == 0xA8 || value[i + 2] == 0xA9))
            {
                return true;
            }

            int next = value[(i + 1)..].IndexOf((byte)0xE2);
            i = next < 0 ? -1 : i + 1 + next;
        }

        return false;
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
        if (!FormatMatchers.MatchDate(value))
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
        if (!FormatMatchers.MatchDateTime(value))
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
        if (!MatchDuration(value))
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
    /// Validates that a string value is a valid ECMA-262 regular expression, read with today's grammar.
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
    /// Validates that a string value is a valid ECMA-262 regular expression in the grammar of the <c>u</c> flag, of
    /// an edition and under a version of Unicode.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="edition">The edition of the grammar.</param>
    /// <param name="unicodeVersion">The version of Unicode.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns><see langword="true"/> if the value is a valid regex; otherwise, <see langword="false"/>.</returns>
    [CLSCompliant(false)]
    public static bool MatchRegex(ReadOnlySpan<byte> value, Corvus.Ecma262.RegularExpressions.RegexEdition edition, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion unicodeVersion, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRegex(value, edition, unicodeVersion))
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
        if (!FormatMatchers.MatchTime(value))
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
    /// Writes a <c>WARNING: </c> prefix followed by the message produced by the
    /// supplied inner message provider. Used by the warning-mode format methods.
    /// </summary>
    /// <param name="inner">The inner message provider supplying the format-specific message.</param>
    /// <param name="buffer">The buffer into which to write the message.</param>
    /// <param name="bytesWritten">The number of bytes written.</param>
    /// <returns><see langword="true"/> if the message was written; otherwise, <see langword="false"/>.</returns>
    private static bool WriteWarning(JsonSchemaMessageProvider inner, Span<byte> buffer, out int bytesWritten)
    {
        ReadOnlySpan<byte> prefix = "WARNING: "u8;
        if (buffer.Length < prefix.Length)
        {
            bytesWritten = 0;
            return false;
        }

        prefix.CopyTo(buffer);
        if (!inner(buffer.Slice(prefix.Length), out int innerWritten))
        {
            bytesWritten = 0;
            return false;
        }

        bytesWritten = prefix.Length + innerWritten;
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an ISO 8601 date.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnDate(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!FormatMatchers.MatchDate(value))
        {
            context.EvaluatedKeyword(true, WarningDate, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedDate, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an ISO 8601 offset date-time.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnDateTime(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!FormatMatchers.MatchDateTime(value))
        {
            context.EvaluatedKeyword(true, WarningDateTime, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedDateTime, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an ISO 8601 duration.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnDuration(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchDuration(value))
        {
            context.EvaluatedKeyword(true, WarningDuration, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedDuration, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an email address.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnEmail(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchEmail(value))
        {
            context.EvaluatedKeyword(true, WarningEmail, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedEmail, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a hostname.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnHostname(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchHostname(value))
        {
            context.EvaluatedKeyword(true, WarningHostname, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedHostname, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an internationalized (IDN) email address.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIdnEmail(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIdnEmail(value))
        {
            context.EvaluatedKeyword(true, WarningIdnEmail, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIdnEmail, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an internationalized (IDN) hostname.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIdnHostname(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIdnHostname(value))
        {
            context.EvaluatedKeyword(true, WarningIdnHostname, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIdnHostname, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an IPv4 address.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIPV4(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIPV4(value))
        {
            context.EvaluatedKeyword(true, WarningIPV4, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIPV4, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an IPv6 address.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIPV6(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIPV6(value))
        {
            context.EvaluatedKeyword(true, WarningIPV6, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIPV6, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an IRI.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIri(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIri(value))
        {
            context.EvaluatedKeyword(true, WarningIri, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIri, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an IRI reference.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnIriReference(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchIriReference(value))
        {
            context.EvaluatedKeyword(true, WarningIriReference, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedIriReference, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a JSON Pointer.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnJsonPointer(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchJsonPointer(value))
        {
            context.EvaluatedKeyword(true, WarningJsonPointer, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedJsonPointer, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an ECMAScript regular expression.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnRegex(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRegex(value))
        {
            context.EvaluatedKeyword(true, WarningRegex, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedRegex, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a relative JSON Pointer.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnRelativeJsonPointer(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchRelativeJsonPointer(value))
        {
            context.EvaluatedKeyword(true, WarningRelativeJsonPointer, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedRelativeJsonPointer, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is an ISO 8601 offset time.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnTime(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!FormatMatchers.MatchTime(value))
        {
            context.EvaluatedKeyword(true, WarningTime, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedTime, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a URI.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnUri(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUri(value))
        {
            context.EvaluatedKeyword(true, WarningUri, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedUri, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a URI reference.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnUriReference(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUriReference(value))
        {
            context.EvaluatedKeyword(true, WarningUriReference, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedUriReference, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a URI template.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnUriTemplate(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUriTemplate(value))
        {
            context.EvaluatedKeyword(true, WarningUriTemplate, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedUriTemplate, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a UUID.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnUuid(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchUuid(value))
        {
            context.EvaluatedKeyword(true, WarningUuid, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedUuid, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a Base64-encoded string.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnBase64String(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchBase64String(value))
        {
            context.EvaluatedKeyword(true, WarningBase64String, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedBase64String, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is valid JSON content.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnJsonContent(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchJsonContent(value))
        {
            context.EvaluatedKeyword(true, WarningJsonContent, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedJsonContent, keyword);
        return true;
    }

    /// <summary>
    /// Validates, in warning mode, that a string value is a Base64-encoded JSON document.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The JSON schema validation context.</param>
    /// <returns>Always <see langword="true"/>; a non-conformant value records a <c>WARNING</c> annotation rather than failing validation.</returns>
    [CLSCompliant(false)]
    public static bool WarnBase64Content(ReadOnlySpan<byte> value, ReadOnlySpan<byte> keyword, ref JsonSchemaContext context)
    {
        if (!MatchBase64Content(value))
        {
            context.EvaluatedKeyword(true, WarningBase64Content, keyword);
            return true;
        }

        context.EvaluatedKeyword(true, ExpectedBase64Content, keyword);
        return true;
    }

    // The format checks themselves are in FormatMatchers, which the V4 engine shares.

    /// <inheritdoc cref="FormatMatchers.MatchDecodedHostname(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchDecodedHostname(ReadOnlySpan<byte> value) => FormatMatchers.MatchDecodedHostname(value);

    /// <inheritdoc cref="FormatMatchers.MatchDuration(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchDuration(ReadOnlySpan<byte> value) => FormatMatchers.MatchDuration(value);

    /// <inheritdoc cref="FormatMatchers.MatchEmail(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchEmail(ReadOnlySpan<byte> value) => FormatMatchers.MatchEmail(value);

    /// <inheritdoc cref="FormatMatchers.MatchHostname(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchHostname(ReadOnlySpan<byte> value) => FormatMatchers.MatchHostname(value);

    /// <inheritdoc cref="FormatMatchers.MatchIPV4(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV4(ReadOnlySpan<byte> value) => FormatMatchers.MatchIPV4(value);

    /// <inheritdoc cref="FormatMatchers.MatchIPV6(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIPV6(ReadOnlySpan<byte> value) => FormatMatchers.MatchIPV6(value);

    /// <inheritdoc cref="FormatMatchers.MatchIdnEmail(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnEmail(ReadOnlySpan<byte> value) => FormatMatchers.MatchIdnEmail(value);

    /// <inheritdoc cref="FormatMatchers.MatchIdnHostname(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIdnHostname(ReadOnlySpan<byte> value) => FormatMatchers.MatchIdnHostname(value);

    /// <inheritdoc cref="FormatMatchers.MatchIri(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIri(ReadOnlySpan<byte> value) => FormatMatchers.MatchIri(value);

    /// <inheritdoc cref="FormatMatchers.MatchIriReference(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchIriReference(ReadOnlySpan<byte> value) => FormatMatchers.MatchIriReference(value);

    /// <inheritdoc cref="FormatMatchers.MatchJsonPointer(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchJsonPointer(ReadOnlySpan<byte> value) => FormatMatchers.MatchJsonPointer(value);

    /// <inheritdoc cref="FormatMatchers.MatchRelativeJsonPointer(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchRelativeJsonPointer(ReadOnlySpan<byte> value) => FormatMatchers.MatchRelativeJsonPointer(value);

    /// <inheritdoc cref="FormatMatchers.MatchUri(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUri(ReadOnlySpan<byte> value) => FormatMatchers.MatchUri(value);

    /// <inheritdoc cref="FormatMatchers.MatchUriReference(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriReference(ReadOnlySpan<byte> value) => FormatMatchers.MatchUriReference(value);

    /// <inheritdoc cref="FormatMatchers.MatchUriTemplate(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUriTemplate(ReadOnlySpan<byte> value) => FormatMatchers.MatchUriTemplate(value);

    /// <inheritdoc cref="FormatMatchers.MatchUuid(ReadOnlySpan{byte})"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool MatchUuid(ReadOnlySpan<byte> value) => FormatMatchers.MatchUuid(value);

    /// <summary>
    /// Validates that a string value is a valid ECMA-262 regular expression, read with today's grammar.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <returns><see langword="true"/> if the value is a valid regex; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchRegex(ReadOnlySpan<byte> value)
    {
        return MatchRegex(value, Corvus.Ecma262.RegularExpressions.RegexEdition.Current, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion.Default);
    }

    /// <summary>
    /// Validates that a string value is a valid ECMA-262 regular expression in the grammar of the <c>u</c> flag, as
    /// the JSON Schema Test Suite reads <c>format: regex</c> (<c>\a</c>, which only the grammar of no flag accepts, is
    /// not one). A <c>pattern</c> is read with the grammar of no flag when the u flag's refuses it, so a value can be
    /// a pattern a schema accepts and not this format. The format is the grammar alone: a pattern over one of the
    /// engine's limits of size is valid, though a schema that held it as a <c>pattern</c> would be refused. Nothing is
    /// allocated.
    /// </summary>
    /// <param name="value">The UTF-8 encoded string value to validate.</param>
    /// <param name="edition">The edition of the grammar.</param>
    /// <param name="unicodeVersion">The version of Unicode.</param>
    /// <returns><see langword="true"/> if the value is a valid regex; otherwise, <see langword="false"/>.</returns>
    internal static bool MatchRegex(ReadOnlySpan<byte> value, Corvus.Ecma262.RegularExpressions.RegexEdition edition, Corvus.Ecma262.RegularExpressions.RegexUnicodeVersion unicodeVersion)
    {
        return Corvus.Ecma262.RegularExpressions.Regex.IsValidPattern(value, Corvus.Ecma262.RegularExpressions.RegexFlags.Unicode, edition, unicodeVersion);
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