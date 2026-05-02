// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Linq;
using System.Text;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that target specific uncovered lines in <see cref="JsonSchemaEvaluation"/>.
/// </summary>
public static class JsonSchemaEvaluationCoverageTests
{
    #region Array: MatchItemCount success paths

    [Fact]
    public static void MatchItemCountEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchItemCountEquals(3, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchItemCountNotEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchItemCountNotEquals(3, 5, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchItemCountGreaterThan_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchItemCountGreaterThan(3, 5, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchItemCountLessThan_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchItemCountLessThan(5, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    #endregion

    #region Array: MatchContainsCount success paths

    [Fact]
    public static void MatchContainsCountEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchContainsCountEquals(2, 2, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchContainsCountNotEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchContainsCountNotEquals(2, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchContainsCountLessThan_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartArray);

        bool result = JsonSchemaEvaluation.MatchContainsCountLessThan(5, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    #endregion

    #region Object: MatchPropertyCount success paths

    [Fact]
    public static void MatchPropertyCountEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartObject);

        bool result = JsonSchemaEvaluation.MatchPropertyCountEquals(3, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchPropertyCountNotEquals_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartObject);

        bool result = JsonSchemaEvaluation.MatchPropertyCountNotEquals(3, 5, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchPropertyCountGreaterThan_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartObject);

        bool result = JsonSchemaEvaluation.MatchPropertyCountGreaterThan(3, 5, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchPropertyCountLessThan_SuccessPath()
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.StartObject);

        bool result = JsonSchemaEvaluation.MatchPropertyCountLessThan(5, 3, "test"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    #endregion

    #region Number: MatchUInt16 non-integer path

    [Theory]
    [InlineData(false, "123", "", -2, false)]
    [InlineData(false, "1", "", -2, false)]
    [InlineData(false, "123", "45", -4, false)]
    [InlineData(false, "", "123", -2, false)]
    [InlineData(false, "1", "23", -2, false)]
    public static void MatchUInt16_DoesNotMatchNormalizedFloatingPoint(bool isNegative, string integral, string fractional, int exponent, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.Number);

        bool result = JsonSchemaEvaluation.MatchUInt16(
            isNegative,
            Encoding.UTF8.GetBytes(integral),
            Encoding.UTF8.GetBytes(fractional),
            exponent,
            "dummy"u8,
            ref context);

        Assert.Equal(expected, result);
        collector.AssertState();
        context.Dispose();
    }

    #endregion

    #region String: MatchEmail edge cases

    [Theory]
    [InlineData("ab", false)]     // Too short (< 3)
    [InlineData("a@", false)]     // Too short (< 3)
    public static void MatchEmail_TooShort_ReturnsFalse(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.Equal(expected, result);
        context.Dispose();
    }

    [Fact]
    public static void MatchEmail_TooLong_ReturnsFalse()
    {
        // Create an email longer than 320 characters (64 + 1 + 256 = 321)
        string localPart = new string('a', 64);
        string domain = new string('b', 252) + ".com";
        string email = localPart + "@" + domain;
        Assert.True(email.Length > 320);

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchEmail_LocalPartOver64Chars_ReturnsFalse()
    {
        // Local part > 64 characters
        string localPart = new string('a', 65);
        string email = localPart + "@example.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchEmail_CommentOnlyLocalPart_ReturnsFalse()
    {
        // Local part is just a comment — empty after stripping
        string email = "(comment)@example.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region String: MatchIdnEmail edge cases

    [Theory]
    [InlineData("ab", false)]
    [InlineData("a@", false)]
    public static void MatchIdnEmail_TooShort_ReturnsFalse(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.Equal(expected, result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnEmail_TooLong_ReturnsFalse()
    {
        string localPart = new string('a', 64);
        string domain = new string('b', 252) + ".com";
        string email = localPart + "@" + domain;
        Assert.True(email.Length > 320);

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnEmail_LocalPartOver64Chars_ReturnsFalse()
    {
        string localPart = new string('a', 65);
        string email = localPart + "@example.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnEmail_CommentOnlyLocalPart_ReturnsFalse()
    {
        string email = "(comment)@example.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Theory]
    [InlineData(".user@example.com", false)]          // dot at start
    [InlineData("user.@example.com", false)]          // dot at end
    [InlineData("user..name@example.com", false)]     // consecutive dots
    public static void MatchIdnEmail_DotPositionInLocalPart_ReturnsFalse(string value, bool expected)
    {
        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.Equal(expected, result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnEmail_NonLetterDigitMarkAtStart_ReturnsFalse()
    {
        // U+00A9 (©) at start of local part — OtherPunctuation category, not letter/digit/mark
        string email = "\u00a9user@example.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnEmail(Encoding.UTF8.GetBytes(email), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region String: MatchIPV4/IPV6 length edges

    [Fact]
    public static void MatchIPV4_TooLong_ReturnsFalse()
    {
        // MaxIPv4StringLength is 15 ("255.255.255.255"); create 16+ char string
        string value = "192.168.001.001.x";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIPV4(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIPV6_TooLong_ReturnsFalse()
    {
        // MaxIPv6StringLength is 65; create a string longer than that
        string value = "2001:0db8:85a3:0000:0000:8a2e:0370:7334:2001:0db8:85a3:0000:0000xx";
        Assert.True(value.Length > 65);

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIPV6(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region String: MatchBase64String with large payload (ArrayPool path)

    [Fact]
    public static void MatchBase64String_LargePayload_UsesArrayPool()
    {
        // The stackalloc threshold is 256 bytes. Base64 decoded output must exceed that.
        // 256 decoded bytes needs ~344 base64 chars, so use a 400-byte payload.
        byte[] raw = new byte[400];
        for (int i = 0; i < raw.Length; i++)
        {
            raw[i] = (byte)(i % 256);
        }

        string base64 = Convert.ToBase64String(raw);

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchBase64String(Encoding.UTF8.GetBytes(base64), "dummy"u8, ref context);

        Assert.True(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchBase64Content_LargePayload_UsesArrayPool()
    {
        // MatchBase64Content decodes then validates JSON.
        // The stackalloc threshold is 256 bytes, so decoded content must exceed that.
        // Create a large JSON object as base64.
        string json = "{" + string.Join(",", Enumerable.Range(0, 50).Select(i => $"\"key{i}\":\"value{i}\"")) + "}";
        Assert.True(json.Length > 256);

        string base64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        bool result = JsonSchemaEvaluation.MatchBase64Content(Encoding.UTF8.GetBytes(base64));

        Assert.True(result);
    }

    #endregion

    #region String: MatchRegularExpression with large input (ArrayPool path)

    [Fact]
    public static void MatchRegularExpression_LargeInput_UsesArrayPool()
    {
        // StackallocCharThreshold is 128. GetMaxCharCount for ASCII is ~length+1.
        // Need value > 127 bytes to trigger the ArrayPool<char> rent path.
        string largeValue = new string('a', 200);
        byte[] utf8Value = Encoding.UTF8.GetBytes(largeValue);

        var regex = new System.Text.RegularExpressions.Regex("^a+$");

        bool result = JsonSchemaEvaluation.MatchRegularExpression(utf8Value, regex);

        Assert.True(result);
    }

    [Fact]
    public static void MatchRegularExpression_LargeInput_NoMatch_UsesArrayPool()
    {
        // Same ArrayPool path but with no match — verifies the return path.
        string largeValue = new string('a', 200) + "!";
        byte[] utf8Value = Encoding.UTF8.GetBytes(largeValue);

        var regex = new System.Text.RegularExpressions.Regex("^a+$");

        bool result = JsonSchemaEvaluation.MatchRegularExpression(utf8Value, regex);

        Assert.False(result);
    }

    #endregion

    #region String: MatchRegex with large input (ArrayPool path)

    [Fact]
    public static void MatchRegex_LargeValidRegex_UsesArrayPool()
    {
        // StackallocNonRecursiveCharThreshold is 2048. GetMaxCharCount for ASCII is ~length+1.
        // Need value > 2047 bytes to trigger the ArrayPool<char> rent path in MatchRegex.
        // Create a valid ECMAScript regex pattern that is > 2047 bytes.
        string longPattern = "^" + new string('a', 2050) + "$";
        byte[] utf8Pattern = Encoding.UTF8.GetBytes(longPattern);

        bool result = JsonSchemaEvaluation.MatchRegex(utf8Pattern);

        Assert.True(result);
    }

    #endregion

    #region String: IDN hostname edge cases (in-loop label boundary checks)

    [Fact]
    public static void MatchIdnHostname_DigitAfterDot_ReturnsFalse()
    {
        // After a dot, a non-letter character (digit) should fail IDN validation
        string value = "abc.1def";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnHostname(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnHostname_KatakanaMiddleDotAtLabelBoundary_WithoutHKHan_ReturnsFalse()
    {
        // Katakana middle dot (\u30FB) in a label without Hiragana/Katakana/Han,
        // followed by a dot — triggers the in-loop check at label boundary
        string value = "def\u30fb.com";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnHostname(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchIdnHostname_MixedArabicIndicDigits_AtLabelBoundary_ReturnsFalse()
    {
        // Arabic-Indic digit (U+0660) and Extended Arabic-Indic digit (U+06F0)
        // in the same label followed by a dot — triggers the in-loop check
        string value = "\u0628\u0660\u06f0.\u0628";

        var collector = new DummyResultsCollector();
        JsonSchemaContext context = CreateContext(collector, JsonTokenType.String);

        bool result = JsonSchemaEvaluation.MatchIdnHostname(Encoding.UTF8.GetBytes(value), "dummy"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region Message formatting delegates (requires real JsonSchemaResultsCollector)

    [Fact]
    public static void MatchLengthEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthEquals(5, 3, "minLength"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLengthNotEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthNotEquals(5, 5, "test"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLengthGreaterThan_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthGreaterThan(5, 3, "test"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLengthGreaterThanOrEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthGreaterThanOrEquals(5, 3, "minLength"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLengthLessThan_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthLessThan(5, 10, "test"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLengthLessThanOrEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchLengthLessThanOrEquals(5, 10, "maxLength"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchPattern_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        var regex = new System.Text.RegularExpressions.Regex("^[0-9]+$");
        bool result = JsonSchemaEvaluation.MatchRegularExpression("hello"u8, regex, "^[0-9]+$", "pattern"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchStringConstantValue_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        bool result = JsonSchemaEvaluation.MatchStringConstantValue("hello"u8, "world"u8, "world", "const"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region Number message formatting delegates

    [Fact]
    public static void MatchUInt16_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // Normalized 1.5 has exponent=-1 — not an integer, triggers format message
        bool result = JsonSchemaEvaluation.MatchUInt16(false, "15"u8, ""u8, -1, "format"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchNotEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // Both sides equal (42 == 42) — MatchNotEquals should fail
        bool result = JsonSchemaEvaluation.MatchNotEquals(
            false, "42"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "not"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLessThan_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // 50 >= 42 — MatchLessThan should fail
        bool result = JsonSchemaEvaluation.MatchLessThan(
            false, "50"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "exclusiveMaximum"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchLessThanOrEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // 50 > 42 — MatchLessThanOrEquals should fail
        bool result = JsonSchemaEvaluation.MatchLessThanOrEquals(
            false, "50"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "maximum"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchGreaterThan_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // 10 <= 42 — MatchGreaterThan should fail
        bool result = JsonSchemaEvaluation.MatchGreaterThan(
            false, "10"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "exclusiveMinimum"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchGreaterThanOrEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // 10 < 42 — MatchGreaterThanOrEquals should fail
        bool result = JsonSchemaEvaluation.MatchGreaterThanOrEquals(
            false, "10"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "minimum"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    [Fact]
    public static void MatchEquals_Failure_FormatsMessage()
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        JsonSchemaContext context = CreateContext(collector);

        // 10 != 42 — MatchEquals should fail
        bool result = JsonSchemaEvaluation.MatchEquals(
            false, "10"u8, ""u8, 0,
            false, "42"u8, ""u8, 0,
            "42", "const"u8, ref context);

        Assert.False(result);
        context.Dispose();
    }

    #endregion

    #region Helpers

    private static JsonSchemaContext CreateContext(DummyResultsCollector collector, JsonTokenType tokenType)
    {
        return JsonSchemaContext.BeginContext(new DummyDocument(tokenType), 0, false, false, collector);
    }

    private static JsonSchemaContext CreateContext(JsonSchemaResultsCollector collector)
    {
        return JsonSchemaContext.BeginContext(new DummyDocument(JsonTokenType.String), 0, false, false, collector);
    }

    #endregion
}
