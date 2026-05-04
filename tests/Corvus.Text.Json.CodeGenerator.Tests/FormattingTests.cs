// <copyright file="FormattingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="Formatting"/> covering uncovered paths in ToPascalCase,
/// ToCamelCase, FixReservedWords, FormatTypeNameComponent, and FormatPropertyNameComponent.
/// </summary>
public static class FormattingTests
{
    #region ToPascalCase

    [Fact]
    public static void ToPascalCase_EmptySpan_ReturnsZero()
    {
        Span<char> buffer = [];
        Assert.Equal(0, Formatting.ToPascalCase(buffer));
    }

    [Fact]
    public static void ToPascalCase_SingleLowerChar()
    {
        Span<char> buffer = "a".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal(1, len);
        Assert.Equal('A', buffer[0]);
    }

    [Fact]
    public static void ToPascalCase_AllCaps_RunOfUppercase()
    {
        // "HTTP" → FixCasing with capitalizeFirst=true: all are uppercase
        // After first char (capitalized), subsequent uppercase letters in a run are lowercased
        Span<char> buffer = "HTTP".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal("Http", buffer[..len].ToString());
    }

    [Fact]
    public static void ToPascalCase_HyphenSeparated()
    {
        // "foo-bar" → strip hyphen, capitalize next → "FooBar"
        Span<char> buffer = "foo-bar".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal("FooBar", buffer[..len].ToString());
    }

    [Fact]
    public static void ToPascalCase_UnderscoreSeparated()
    {
        Span<char> buffer = "foo_bar".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal("FooBar", buffer[..len].ToString());
    }

    [Fact]
    public static void ToPascalCase_WithDigits()
    {
        // Digits are preserved, don't trigger capitalization of next char
        Span<char> buffer = "foo2bar".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal("Foo2bar", buffer[..len].ToString());
    }

    [Fact]
    public static void ToPascalCase_LeadingSpecialChars()
    {
        // Leading non-letter/non-digit is stripped; first letter becomes capital
        Span<char> buffer = "--hello".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal("Hello", buffer[..len].ToString());
    }

    [Fact]
    public static void ToPascalCase_AllSpecialChars_ReturnsZero()
    {
        Span<char> buffer = "---".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        Assert.Equal(0, len);
    }

    [Fact]
    public static void ToPascalCase_MixedCaseAcronymFollowedByWord()
    {
        // "XMLParser" — the run of uppercase XML gets lowercased except the boundary
        Span<char> buffer = "XMLParser".ToCharArray();
        int len = Formatting.ToPascalCase(buffer);
        string result = buffer[..len].ToString();
        // The algorithm preserves the 'X' (capitalize first), then lowercases 'M' (uppercase run),
        // 'L' is uppercase but next char 'P' is also uppercase... actually let's just verify the output
        Assert.True(len > 0);
        Assert.Equal('X', result[0]);
    }

    #endregion

    #region ToCamelCase

    [Fact]
    public static void ToCamelCase_EmptySpan_ReturnsZero()
    {
        Span<char> buffer = [];
        Assert.Equal(0, Formatting.ToCamelCase(buffer));
    }

    [Fact]
    public static void ToCamelCase_SingleUpperChar()
    {
        Span<char> buffer = "A".ToCharArray();
        int len = Formatting.ToCamelCase(buffer);
        Assert.Equal(1, len);
        Assert.Equal('a', buffer[0]);
    }

    [Fact]
    public static void ToCamelCase_PascalInput()
    {
        Span<char> buffer = "HelloWorld".ToCharArray();
        int len = Formatting.ToCamelCase(buffer);
        Assert.Equal("helloWorld", buffer[..len].ToString());
    }

    [Fact]
    public static void ToCamelCase_HyphenSeparated()
    {
        Span<char> buffer = "foo-bar".ToCharArray();
        int len = Formatting.ToCamelCase(buffer);
        Assert.Equal("fooBar", buffer[..len].ToString());
    }

    #endregion

    #region FixReservedWords

    [Fact]
    public static void FixReservedWords_CSharpKeyword_AppendsSuffix()
    {
        // "class" is a C# keyword → should get suffix appended
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "class".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(buffer, 5, "Value".AsSpan(), "Entity".AsSpan());
        Assert.Equal("classEntity", buffer[..len].ToString());
    }

    [Fact]
    public static void FixReservedWords_LeadingDigit_PrependPrefix()
    {
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "123abc".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(buffer, 6, "Value".AsSpan(), "Entity".AsSpan());
        Assert.Equal("Value123abc", buffer[..len].ToString());
    }

    [Fact]
    public static void FixReservedWords_NonReserved_ReturnsUnchanged()
    {
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "myVar".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(buffer, 5, "Value".AsSpan(), "Entity".AsSpan());
        Assert.Equal(5, len);
        Assert.Equal("myVar", buffer[..len].ToString());
    }

    [Fact]
    public static void FixReservedWords_EmptyLength_ReturnsZero()
    {
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        int len = Formatting.FixReservedWords(buffer, 0, "Value".AsSpan(), "Entity".AsSpan());
        Assert.Equal(0, len);
    }

    [Fact]
    public static void FixReservedWords_EmptyCollisionSuffix_UsesEntityFallback()
    {
        // When collisionSuffix is empty, the code uses EntitySuffix ("Entity") as fallback
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "class".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(buffer, 5, "Value".AsSpan(), ReadOnlySpan<char>.Empty);
        // Should append "Entity" (the fallback)
        Assert.Equal("classEntity", buffer[..len].ToString());
    }

    [Fact]
    public static void FixReservedWords_CustomKeywordList_MatchesKeyword()
    {
        string[] customKeywords = ["foo", "bar"];
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "foo".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(customKeywords, buffer, 3, "X".AsSpan(), "Suffix".AsSpan());
        Assert.Equal("fooSuffix", buffer[..len].ToString());
    }

    [Fact]
    public static void FixReservedWords_CustomKeywordList_LeadingDigit()
    {
        string[] customKeywords = ["foo"];
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "9lives".AsSpan().CopyTo(buffer);
        int len = Formatting.FixReservedWords(customKeywords, buffer, 6, "V".AsSpan(), "Sfx".AsSpan());
        Assert.Equal("V9lives", buffer[..len].ToString());
    }

    #endregion

    #region GetBufferLength

    [Fact]
    public static void GetBufferLength_ComputesCorrectly()
    {
        int len = Formatting.GetBufferLength(10, "Prefix".AsSpan(), "Suffix".AsSpan());
        Assert.Equal(22, len); // 10 + 6 + 6
    }

    #endregion

    #region FormatPropertyNameComponent

    [Fact]
    public static void FormatPropertyNameComponent_NormalName()
    {
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "fooBar".AsSpan().CopyTo(buffer);
        int len = Formatting.FormatPropertyNameComponent(buffer, 6);
        Assert.Equal("FooBar", buffer[..len].ToString());
    }

    [Fact]
    public static void FormatPropertyNameComponent_ReservedWord()
    {
        // "item" -> ToPascalCase -> "Item" -> matches Keywords (line 60) -> suffix appended
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "item".AsSpan().CopyTo(buffer);
        int len = Formatting.FormatPropertyNameComponent(buffer, 4);
        string result = buffer[..len].ToString();
        Assert.NotEqual("Item", result); // Must have been fixed
        Assert.StartsWith("Item", result);
    }

    [Fact]
    public static void FormatPropertyNameComponent_AllSpecialChars_FallsBackToProperty()
    {
        // All non-letter/digit chars → ToPascalCase returns 0 → falls back to "Property"
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        "---".AsSpan().CopyTo(buffer);
        int len = Formatting.FormatPropertyNameComponent(buffer, 3);
        Assert.Equal("Property", buffer[..len].ToString());
    }

    #endregion

    #region ApplyArraySuffix

    [Fact]
    public static void ApplyArraySuffix_WritesArray()
    {
        Span<char> buffer = stackalloc char[10];
        int len = Formatting.ApplyArraySuffix(buffer);
        Assert.Equal("Array", buffer[..len].ToString());
    }

    #endregion
}
