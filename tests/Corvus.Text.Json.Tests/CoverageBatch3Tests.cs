// <copyright file="CoverageBatch3Tests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage batch 3: targeting JsonRegexValidator edge cases and
/// JsonReaderHelper.Unescaping ArrayPool paths.
/// </summary>
public static class CoverageBatch3Tests
{
    /// <summary>
    /// Exercises <c>JsonRegexValidator</c> named capture group path (lines 424-431)
    /// by validating a pattern with <c>(?&lt;name&gt;\w+)</c>.
    /// </summary>
    [Fact]
    public static void RegexValidator_NamedCaptureGroup_IsValid()
    {
        bool result = JsonRegexValidator.Validate("(?<name>\\w+)", JsonRegexOptions.ECMAScript);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises named capture with multiple groups, triggering <c>AssignNameSlots</c> iteration.
    /// </summary>
    [Fact]
    public static void RegexValidator_MultipleNamedCaptures_IsValid()
    {
        bool result = JsonRegexValidator.Validate("(?<first>\\w+)\\s(?<last>\\w+)", JsonRegexOptions.ECMAScript);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises expression conditional group path (lines 595-604 in PopGroup)
    /// via the pattern <c>(?(condition)yes|no)</c>.
    /// </summary>
    [Fact]
    public static void RegexValidator_ExpressionConditional_IsValid()
    {
        // Expression conditional: (?(lookahead)yes|no)
        bool result = JsonRegexValidator.Validate("(?(?=a)b|c)", JsonRegexOptions.None);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises expression conditional with a non-lookahead condition.
    /// </summary>
    [Fact]
    public static void RegexValidator_ExpressionConditional_NonLookahead()
    {
        // Conditional with a capturing group condition
        bool result = JsonRegexValidator.Validate("(?(\\d)yes|no)", JsonRegexOptions.None);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises the incomplete quantifier fallback path (lines 1991-1996)
    /// where <c>{</c> is not followed by a valid <c>{n,m}</c> quantifier format.
    /// </summary>
    [Fact]
    public static void RegexValidator_IncompleteBraceQuantifier_TreatedAsLiteral()
    {
        // { not followed by valid quantifier — treated as literal character
        bool result = JsonRegexValidator.Validate("a{b", JsonRegexOptions.ECMAScript);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises incomplete quantifier with just <c>{</c> at end of pattern.
    /// </summary>
    [Fact]
    public static void RegexValidator_BraceAtEnd_TreatedAsLiteral()
    {
        bool result = JsonRegexValidator.Validate("a{", JsonRegexOptions.ECMAScript);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises incomplete quantifier with digits but no closing brace.
    /// </summary>
    [Fact]
    public static void RegexValidator_BraceWithDigitsNoClose_TreatedAsLiteral()
    {
        // {3 without closing } — falls through to literal
        bool result = JsonRegexValidator.Validate("a{3b", JsonRegexOptions.ECMAScript);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises the ArrayPool return paths in <c>UnescapeAndCompareBothInputs</c>
    /// (lines 172-182) by comparing two objects with long escaped property names
    /// that exceed the 256-byte stackalloc threshold.
    /// </summary>
    [Fact]
    public static void Unescaping_LargeEscapedPropertyNames_HitsArrayPoolPaths()
    {
        // Create a property name with escape sequences that exceeds 256 bytes when stored as raw JSON.
        // Using \u0041 (A) escape sequences: each \u0041 is 6 bytes in JSON.
        // We need > 256 bytes total, so 50 repetitions = 300 bytes of escape sequences.
        StringBuilder sb = new();
        sb.Append("{\"");
        for (int i = 0; i < 50; i++)
        {
            sb.Append("\\u0041"); // Each one is 6 bytes in JSON encoding
        }

        sb.Append("\":1}");
        string json = sb.ToString();
        byte[] jsonBytes = Encoding.UTF8.GetBytes(json);

        // Parse both copies from the same JSON — they'll have identical escaped property names
        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse(jsonBytes.AsMemory());
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse(jsonBytes.AsMemory());

        // DeepEquals compares property names. When both have escapes and are > 256 bytes,
        // it hits UnescapeAndCompareBothInputs with the ArrayPool path.
        bool areEqual = JsonElementHelpers.DeepEquals(doc1.RootElement, doc2.RootElement);
        Assert.True(areEqual);
    }

    /// <summary>
    /// Same as above but tests inequality (result = false) to exercise both return
    /// branches after the ArrayPool paths.
    /// </summary>
    [Fact]
    public static void Unescaping_LargeEscapedPropertyNames_UnequalValues()
    {
        StringBuilder sb1 = new();
        sb1.Append("{\"");
        for (int i = 0; i < 50; i++)
        {
            sb1.Append("\\u0041"); // A
        }

        sb1.Append("\":1}");

        StringBuilder sb2 = new();
        sb2.Append("{\"");
        for (int i = 0; i < 50; i++)
        {
            sb2.Append("\\u0042"); // B
        }

        sb2.Append("\":1}");

        byte[] jsonBytes1 = Encoding.UTF8.GetBytes(sb1.ToString());
        byte[] jsonBytes2 = Encoding.UTF8.GetBytes(sb2.ToString());

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse(jsonBytes1.AsMemory());
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse(jsonBytes2.AsMemory());

        bool areEqual = JsonElementHelpers.DeepEquals(doc1.RootElement, doc2.RootElement);
        Assert.False(areEqual);
    }

    /// <summary>
    /// Exercises <c>AssignNameSlots</c> while loop (lines 323-325)
    /// by using a pattern where unnamed groups take slots that collide
    /// with the auto-numbering for named groups.
    /// Uses non-ECMAScript mode where unnamed groups are noted non-explicitly.
    /// </summary>
    [Fact]
    public static void RegexValidator_NamedAndUnnamedGroups_AssignNameSlotsLoop()
    {
        // (x) takes slot 1, (y) takes slot 2; then (?<name>z) in AssignNameSlots
        // starts at _autocap=3 which is not taken, so no while loop iteration.
        // To force the loop: use (?<3>x) to explicitly reserve slot 3, then
        // unnamed groups take 1, 2 and _autocap ends at 3 — but slot 3 is already taken!
        // This requires non-ECMAScript mode for numbered named groups.
        bool result = JsonRegexValidator.Validate("(a)(b)(?<3>c)(?<name>d)", JsonRegexOptions.None);
        Assert.True(result);
    }

    /// <summary>
    /// Validates a pattern with alternation inside an expression conditional.
    /// </summary>
    [Fact]
    public static void RegexValidator_ConditionalWithAlternation()
    {
        // (?(lookahead)trueExpr|falseExpr)
        bool result = JsonRegexValidator.Validate("(?(?=\\d)\\d{3}|\\w+)", JsonRegexOptions.None);
        Assert.True(result);
    }

    /// <summary>
    /// Validates a backreference conditional pattern (?(1)yes|no).
    /// </summary>
    [Fact]
    public static void RegexValidator_BackreferenceConditional()
    {
        // Backreference conditional — (x) captures group 1, then (?(1)yes|no)
        bool result = JsonRegexValidator.Validate("(x)(?(1)yes|no)", JsonRegexOptions.None);
        Assert.True(result);
    }

    /// <summary>
    /// Exercises <c>CompareNormalizedJsonNumbers</c> sign comparison path (lines 302-303)
    /// by comparing numbers with different signs.
    /// </summary>
    [Fact]
    public static void NumericCore_CompareNormalizedJsonNumbers_DifferentSigns()
    {
        // Negative vs positive — exercises sign comparison (lines 302-303)
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: true, leftIntegral: "5"u8, leftFractional: default, leftExponent: 0,
            rightIsNegative: false, rightIntegral: "3"u8, rightFractional: default, rightExponent: 0);
        Assert.Equal(-1, result);

        // Positive vs negative
        result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false, leftIntegral: "3"u8, leftFractional: default, leftExponent: 0,
            rightIsNegative: true, rightIntegral: "5"u8, rightFractional: default, rightExponent: 0);
        Assert.Equal(1, result);
    }

    /// <summary>
    /// Exercises <c>CompareNormalizedJsonNumbers</c> equality path (line 347)
    /// when both numbers are identical.
    /// </summary>
    [Fact]
    public static void NumericCore_CompareNormalizedJsonNumbers_Equal()
    {
        // 12.5 normalized: integral="12", fractional="5", exponent=-1 (adjusted by -frac.Length)
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false, leftIntegral: "12"u8, leftFractional: "5"u8, leftExponent: -1,
            rightIsNegative: false, rightIntegral: "12"u8, rightFractional: "5"u8, rightExponent: -1);
        Assert.Equal(0, result);
    }

    /// <summary>
    /// Exercises <c>GetDigitAtPosition</c> fractional part access (lines 362-365)
    /// by comparing numbers that differ in fractional digits.
    /// </summary>
    [Fact]
    public static void NumericCore_CompareNormalizedJsonNumbers_FractionalDifference()
    {
        // 1.25 normalized: integral="1", fractional="25", exponent=-2
        // 1.3 normalized: integral="1", fractional="3", exponent=-1
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false, leftIntegral: "1"u8, leftFractional: "25"u8, leftExponent: -2,
            rightIsNegative: false, rightIntegral: "1"u8, rightFractional: "3"u8, rightExponent: -1);
        Assert.Equal(-1, result); // 1.25 < 1.3
    }

    /// <summary>
    /// Exercises <c>TryParseNumber</c> with fractional+exponent (line 154-155)
    /// for a number like 1.5e2.
    /// </summary>
    [Fact]
    public static void NumericCore_ParseNumber_FractionalAndExponent()
    {
        // 1.5e2 = 150 — has both fractional AND exponent parts
        bool equal = JsonElementHelpers.AreEqualJsonNumbers("1.5e2"u8, "150"u8);
        Assert.True(equal);
    }

    /// <summary>
    /// Exercises <c>TryParseNumber</c> with negative exponent to test normalization.
    /// </summary>
    [Fact]
    public static void NumericCore_ParseNumber_NegativeExponent()
    {
        // 15e-1 = 1.5
        bool equal = JsonElementHelpers.AreEqualJsonNumbers("15e-1"u8, "1.5"u8);
        Assert.True(equal);
    }

    /// <summary>
    /// Exercises comparison with numbers of different effective magnitudes.
    /// </summary>
    [Fact]
    public static void NumericCore_CompareNormalizedJsonNumbers_DifferentMagnitudes()
    {
        // 100 vs 99 — different effective length
        int result = JsonElementHelpers.CompareNormalizedJsonNumbers(
            leftIsNegative: false, leftIntegral: "1"u8, leftFractional: default, leftExponent: 2,
            rightIsNegative: false, rightIntegral: "99"u8, rightFractional: default, rightExponent: 0);
        Assert.Equal(1, result); // 100 > 99
    }

    #region JsonDocumentBuilder.ParseValue single-span paths

    /// <summary>
    /// Exercises <c>ParseValue</c> with a single-span reader on a Number token (lines 269-271).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_SingleSpan_Number()
    {
        byte[] json = "42"u8.ToArray();
        var reader = new Utf8JsonReader(json);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.Equal(42, builder.RootElement.GetInt32());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> with a single-span reader on a True token (lines 269-271).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_SingleSpan_True()
    {
        byte[] json = "true"u8.ToArray();
        var reader = new Utf8JsonReader(json);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.True(builder.RootElement.GetBoolean());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> with a single-span reader on a String token (lines 281-284).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_SingleSpan_String()
    {
        byte[] json = "\"hello world\""u8.ToArray();
        var reader = new Utf8JsonReader(json);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.Equal("hello world", builder.RootElement.GetString());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> with a single-span reader on a Null token (lines 269-271).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_SingleSpan_Null()
    {
        byte[] json = "null"u8.ToArray();
        var reader = new Utf8JsonReader(json);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.Equal(JsonValueKind.Null, builder.RootElement.ValueKind);
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> with a single-span reader on an Object (lines 245-249,
    /// TrySkip + OriginalSpan.Slice path).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_SingleSpan_Object()
    {
        byte[] json = """{"a":1}"""u8.ToArray();
        var reader = new Utf8JsonReader(json);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.Equal(1, builder.RootElement.GetProperty("a"u8).GetInt32());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> with multi-segment reader on a string where
    /// <c>HasValueSequence</c> is false — the string content is entirely within one segment
    /// but the reader is backed by a multi-segment sequence (lines 294-296).
    /// </summary>
    [Fact]
    public static void BuilderParseValue_MultiSegment_StringInOneSegment()
    {
        // Place the string entirely in the second segment so HasValueSequence is false
        // but OriginalSequence is non-empty.
        byte[] part1 = "   "u8.ToArray(); // whitespace in first segment
        byte[] part2 = "\"hi\""u8.ToArray(); // string in second segment

        System.Buffers.ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.ParseValue(workspace, ref reader);

        Assert.Equal("hi", builder.RootElement.GetString());
    }

    #endregion

    #region ParsedJsonDocument.Parse paths

    /// <summary>
    /// Exercises <c>ParsedJsonDocument.Parse</c> from a Stream with UTF-8 BOM
    /// to hit BOM-stripping path (lines 840-877 in ParsedJsonDocument.Parse.cs).
    /// </summary>
    [Fact]
    public static void ParsedDocument_ParseFromStreamWithBom()
    {
        byte[] bom = [0xEF, 0xBB, 0xBF];
        byte[] json = "42"u8.ToArray();
        byte[] withBom = new byte[bom.Length + json.Length];
        bom.CopyTo(withBom, 0);
        json.CopyTo(withBom, bom.Length);

        using var stream = new System.IO.MemoryStream(withBom);
        using var doc = ParsedJsonDocument<JsonElement>.Parse(stream);
        Assert.Equal(42, doc.RootElement.GetInt32());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> (useArrayPools=false) with a multi-segment reader
    /// where a number token spans segments (lines 697-699 in ParsedJsonDocument.Parse.cs).
    /// </summary>
    [Fact]
    public static void ParsedDocument_ParseValue_MultiSegment_NumberSpansSegments()
    {
        // Split number "123456" across two segments so HasValueSequence=true
        byte[] part1 = "123"u8.ToArray();
        byte[] part2 = "456"u8.ToArray();

        System.Buffers.ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        JsonElement result = JsonElementHelpers.ParseValue<JsonElement>(ref reader);
        Assert.Equal(123456, result.GetInt32());
    }

    /// <summary>
    /// Exercises <c>ParseValue</c> (useArrayPools=false) with a multi-segment reader
    /// where a string token spans segments, and the string has escape sequences
    /// (hits lines 697-706 + 756-759 via ParseUnrented with escaped string).
    /// </summary>
    [Fact]
    public static void ParsedDocument_ParseValue_MultiSegment_EscapedStringSpansSegments()
    {
        // Split an escaped string across segments: "hel\\nlo" → "hel\nlo"
        byte[] part1 = "\"hel\\"u8.ToArray();
        byte[] part2 = "nlo\""u8.ToArray();

        System.Buffers.ReadOnlySequence<byte> sequence = BufferFactory.Create(part1, part2);
        var reader = new Utf8JsonReader(sequence);

        JsonElement result = JsonElementHelpers.ParseValue<JsonElement>(ref reader);
        Assert.Equal("hel\nlo", result.GetString());
    }

    /// <summary>
    /// Exercises <c>ParseValue(Stream)</c> path (lines 213-226 in ParsedJsonDocument.Parse.cs)
    /// via <c>ParsedJsonDocument.ParseValue</c> with a stream.
    /// </summary>
    [Fact]
    public static void ParsedDocument_ParseValueFromStream()
    {
        byte[] json = "\"hello\""u8.ToArray();
        using var stream = new System.IO.MemoryStream(json);

        // ParseValue(Stream, options) is internal — uses ParseUnrented (non-disposable copy)
        using ParsedJsonDocument<JsonElement> doc =
            ParsedJsonDocument<JsonElement>.ParseValue(stream, default);
        Assert.Equal("hello", doc.RootElement.GetString());
    }

    /// <summary>
    /// Exercises <c>CreateConstant</c> string path with escape characters
    /// (lines 756-759 in ParsedJsonDocument.Parse.cs — SetHasComplexChildren for backslash).
    /// </summary>
    [Fact]
    public static void ParsedDocument_StringConstant_WithEscape()
    {
        // "hel\\nlo" (quoted JSON string with a \n escape)
        byte[] quotedString = "\"hel\\nlo\""u8.ToArray();

        JsonElement result = ParsedJsonDocument<JsonElement>.StringConstant(quotedString);
        Assert.Equal("hel\nlo", result.GetString());
    }

    #endregion
}
