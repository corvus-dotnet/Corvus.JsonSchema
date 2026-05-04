// <copyright file="CSharpMemberNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="CSharpMemberName"/> covering the TranslateNonLetterToWord switch
/// and fallback paths.
/// </summary>
public static class CSharpMemberNameTests
{
    #region Single non-letter character translation (TranslateNonLetterToWord)

    [Theory]
    [InlineData(" ", "Value")]   // Space is whitespace -> IsNullOrWhiteSpace -> baseName="Value"
    [InlineData("!", "Excl")]
    [InlineData("\"", "Quot")]
    [InlineData("#", "Num")]
    [InlineData("$", "Dollar")]
    [InlineData("%", "Percent")]
    [InlineData("&", "Amp")]
    [InlineData("'", "Apos")]
    [InlineData("(", "Lpar")]
    [InlineData(")", "Rpar")]
    [InlineData("*", "Ast")]
    [InlineData("+", "Plus")]
    [InlineData(",", "Comma")]
    [InlineData("-", "Minus")]
    [InlineData(".", "Period")]
    [InlineData("/", "Sol")]
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_Punctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData("0", "Zero")]
    [InlineData("1", "One")]
    [InlineData("2", "Two")]
    [InlineData("3", "Three")]
    [InlineData("4", "Four")]
    [InlineData("5", "Five")]
    [InlineData("6", "Six")]
    [InlineData("7", "Seven")]
    [InlineData("8", "Eight")]
    [InlineData("9", "Nine")]
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_Digits(string input, string expected)
    {
        // Digits translate to words ("zero", "five" etc.) then PascalCase is applied.
        // The translated word starts with a letter, so no "Value" prefix is added.
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData(":", "Colon")]
    [InlineData(";", "Semi")]
    [InlineData("<", "Lt")]
    [InlineData("=", "EqualsValue")]   // "Equals" is a C# reserved word -> suffix appended
    [InlineData(">", "Gt")]
    [InlineData("?", "Quest")]
    [InlineData("@", "Commat")]
    [InlineData("[", "Lsqb")]
    [InlineData("\\", "Bsol")]
    [InlineData("]", "Rsqb")]
    [InlineData("^", "Caret")]
    [InlineData("_", "Lowbar")]
    [InlineData("`", "Grave")]
    [InlineData("{", "Lcub")]
    [InlineData("|", "Verbar")]
    [InlineData("}", "Rcub")]
    [InlineData("~", "Tilde")]
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_SymbolsAndBrackets(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData("\u20AC", "Euro")]       // Euro sign - CurrencySymbol
    [InlineData("\u201A", "Sbquo")]      // Single low-9 quotation mark
    [InlineData("\u201E", "Bdquo")]      // Double low-9 quotation mark
    [InlineData("\u2026", "Hellip")]     // Horizontal ellipsis
    [InlineData("\u2020", "Dagger")]     // Dagger
    [InlineData("\u2021", "Dagger")]     // Double dagger ("Dagger" with capital D -> PascalCase)
    [InlineData("\u2030", "Permil")]     // Per mille sign
    [InlineData("\u2039", "Lsaquo")]     // Single left-pointing angle quotation
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_ExtendedPunctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData("\u2018", "Lsquo")]      // Left single quotation mark
    [InlineData("\u2019", "Rsquo")]      // Right single quotation mark
    [InlineData("\u201C", "Ldquo")]      // Left double quotation mark
    [InlineData("\u201D", "Rdquo")]      // Right double quotation mark
    [InlineData("\u2022", "Bull")]       // Bullet
    [InlineData("\u2013", "Ndash")]      // En dash
    [InlineData("\u2014", "Mdash")]      // Em dash
    [InlineData("\u2122", "Trade")]      // Trade mark sign
    [InlineData("\u203A", "Rsaquo")]     // Single right-pointing angle quotation
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_SmartPunctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData("\u00A1", "Iexcl")]      // Inverted exclamation mark
    [InlineData("\u00A2", "Cent")]       // Cent sign
    [InlineData("\u00A3", "Pound")]      // Pound sign
    [InlineData("\u00A4", "Curren")]     // Currency sign
    [InlineData("\u00A5", "Yen")]        // Yen sign
    [InlineData("\u00A6", "Brvbar")]     // Broken bar
    [InlineData("\u00A7", "Sect")]       // Section sign
    [InlineData("\u00A8", "Uml")]        // Diaeresis
    [InlineData("\u00A9", "Copy")]       // Copyright sign
    [InlineData("\u00AB", "Laquo")]      // Left-pointing double angle quotation
    [InlineData("\u00AC", "Not")]        // Not sign
    [InlineData("\u00AE", "Reg")]        // Registered sign
    [InlineData("\u00AF", "Macr")]       // Macron
    [InlineData("\u00B0", "Deg")]        // Degree sign
    [InlineData("\u00B1", "Plusmn")]     // Plus-minus sign
    [InlineData("\u00B2", "Sup2")]       // Superscript two
    [InlineData("\u00B3", "Sup3")]       // Superscript three
    [InlineData("\u00B4", "Acute")]      // Acute accent
    [InlineData("\u00B6", "Para")]       // Pilcrow sign
    [InlineData("\u00B7", "Middot")]     // Middle dot
    [InlineData("\u00B8", "Cedil")]      // Cedilla
    [InlineData("\u00B9", "Sup1")]       // Superscript one
    [InlineData("\u00BB", "Raquo")]      // Right-pointing double angle quotation
    [InlineData("\u00BC", "Frac14")]     // Vulgar fraction one quarter
    [InlineData("\u00BD", "Frac12")]     // Vulgar fraction one half
    [InlineData("\u00BE", "Frac34")]     // Vulgar fraction three quarters
    [InlineData("\u00BF", "Iquest")]     // Inverted question mark
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_Latin1Supplement(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Theory]
    [InlineData("\u00D7", "Times")]      // Multiplication sign (MathSymbol, not a letter)
    [InlineData("\u00F7", "Divide")]     // Division sign (MathSymbol, not a letter)
    public static void SingleNonLetter_PascalCase_TranslatesCorrectly_MathSymbols(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.Equal(expected, name.BuildName());
    }

    [Fact]
    public static void SingleNonLetter_UnrecognizedCharacter_ReturnsValue()
    {
        // A character not in the switch table returns null -> falls back to "Value"
        var name = new CSharpMemberName("Scope", "\u0001", Casing.PascalCase);
        Assert.Equal("Value", name.BuildName());
    }

    #endregion

    #region CamelCase variants

    [Theory]
    [InlineData("!", "excl")]
    [InlineData("#", "num")]
    [InlineData("$", "dollar")]
    [InlineData("~", "tilde")]
    [InlineData("0", "zero")]
    [InlineData("9", "nine")]
    public static void SingleNonLetter_CamelCase_TranslatesCorrectly(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.CamelCase);
        Assert.Equal(expected, name.BuildName());
    }

    #endregion

    #region Fallback path (totalLength == 0 after FixReservedWords)

    [Fact]
    public static void AllSpecialChars_PascalCase_FallsBackToValue()
    {
        // "---" has no letters/digits -> ToPascalCase strips everything -> fallback to "Value"
        var name = new CSharpMemberName("Scope", "---", Casing.PascalCase);
        Assert.Equal("Value", name.BuildName());
    }

    [Fact]
    public static void AllSpecialChars_CamelCase_FallsBackTovalue()
    {
        // "---" has no letters/digits -> ToCamelCase strips everything -> fallback to "value"
        var name = new CSharpMemberName("Scope", "---", Casing.CamelCase);
        Assert.Equal("value", name.BuildName());
    }

    [Fact]
    public static void AllSpecialChars_Unmodified_UsesRawValue()
    {
        // Unmodified casing doesn't go through FixReservedWords -- uses raw value
        var name = new CSharpMemberName("Scope", "---", Casing.Unmodified);
        Assert.Equal("---", name.BuildName());
    }

    #endregion

    #region Prefix and suffix handling

    [Fact]
    public static void PascalCase_WithPrefix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.PascalCase, prefix: "my");
        Assert.Equal("MyHello", name.BuildName());
    }

    [Fact]
    public static void CamelCase_WithPrefix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.CamelCase, prefix: "my");
        Assert.Equal("myHello", name.BuildName());
    }

    [Fact]
    public static void PascalCase_WithSuffix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.PascalCase, suffix: "type");
        Assert.Equal("HelloType", name.BuildName());
    }

    [Fact]
    public static void CamelCase_WithoutPrefix_UsesBaseNameCamelCase()
    {
        var name = new CSharpMemberName("Scope", "Hello", Casing.CamelCase);
        Assert.Equal("hello", name.BuildName());
    }

    #endregion

    #region Empty/whitespace baseName

    [Fact]
    public static void EmptyBaseName_PascalCase_FallsBackToValue()
    {
        var name = new CSharpMemberName("Scope", "", Casing.PascalCase);
        Assert.Equal("Value", name.BuildName());
    }

    [Fact]
    public static void WhitespaceBaseName_PascalCase_FallsBackToValue()
    {
        var name = new CSharpMemberName("Scope", "   ", Casing.PascalCase);
        Assert.Equal("Value", name.BuildName());
    }

    #endregion
}
