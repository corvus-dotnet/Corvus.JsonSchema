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
}
