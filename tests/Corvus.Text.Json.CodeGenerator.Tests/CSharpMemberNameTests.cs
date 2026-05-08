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
[TestClass]
    public class CSharpMemberNameTests
{
    #region Single non-letter character translation (TranslateNonLetterToWord)

    [TestMethod]
    [DataRow(" ", "Value")]   // Space is whitespace -> IsNullOrWhiteSpace -> baseName="Value"
    [DataRow("!", "Excl")]
    [DataRow("\"", "Quot")]
    [DataRow("#", "Num")]
    [DataRow("$", "Dollar")]
    [DataRow("%", "Percent")]
    [DataRow("&", "Amp")]
    [DataRow("'", "Apos")]
    [DataRow("(", "Lpar")]
    [DataRow(")", "Rpar")]
    [DataRow("*", "Ast")]
    [DataRow("+", "Plus")]
    [DataRow(",", "Comma")]
    [DataRow("-", "Minus")]
    [DataRow(".", "Period")]
    [DataRow("/", "Sol")]
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_Punctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow("0", "Zero")]
    [DataRow("1", "One")]
    [DataRow("2", "Two")]
    [DataRow("3", "Three")]
    [DataRow("4", "Four")]
    [DataRow("5", "Five")]
    [DataRow("6", "Six")]
    [DataRow("7", "Seven")]
    [DataRow("8", "Eight")]
    [DataRow("9", "Nine")]
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_Digits(string input, string expected)
    {
        // Digits translate to words ("zero", "five" etc.) then PascalCase is applied.
        // The translated word starts with a letter, so no "Value" prefix is added.
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow(":", "Colon")]
    [DataRow(";", "Semi")]
    [DataRow("<", "Lt")]
    [DataRow("=", "EqualsValue")]   // "Equals" is a C# reserved word -> suffix appended
    [DataRow(">", "Gt")]
    [DataRow("?", "Quest")]
    [DataRow("@", "Commat")]
    [DataRow("[", "Lsqb")]
    [DataRow("\\", "Bsol")]
    [DataRow("]", "Rsqb")]
    [DataRow("^", "Caret")]
    [DataRow("_", "Lowbar")]
    [DataRow("`", "Grave")]
    [DataRow("{", "Lcub")]
    [DataRow("|", "Verbar")]
    [DataRow("}", "Rcub")]
    [DataRow("~", "Tilde")]
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_SymbolsAndBrackets(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow("\u20AC", "Euro")]       // Euro sign - CurrencySymbol
    [DataRow("\u201A", "Sbquo")]      // Single low-9 quotation mark
    [DataRow("\u201E", "Bdquo")]      // Double low-9 quotation mark
    [DataRow("\u2026", "Hellip")]     // Horizontal ellipsis
    [DataRow("\u2020", "Dagger")]     // Dagger
    [DataRow("\u2021", "Dagger")]     // Double dagger ("Dagger" with capital D -> PascalCase)
    [DataRow("\u02DC", "Tilde")]      // Small tilde (ModifierSymbol, NOT a letter)
    [DataRow("\u2030", "Permil")]     // Per mille sign
    [DataRow("\u2039", "Lsaquo")]     // Single left-pointing angle quotation
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_ExtendedPunctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow("\u2018", "Lsquo")]      // Left single quotation mark
    [DataRow("\u2019", "Rsquo")]      // Right single quotation mark
    [DataRow("\u201C", "Ldquo")]      // Left double quotation mark
    [DataRow("\u201D", "Rdquo")]      // Right double quotation mark
    [DataRow("\u2022", "Bull")]       // Bullet
    [DataRow("\u2013", "Ndash")]      // En dash
    [DataRow("\u2014", "Mdash")]      // Em dash
    [DataRow("\u2122", "Trade")]      // Trade mark sign
    [DataRow("\u203A", "Rsaquo")]     // Single right-pointing angle quotation
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_SmartPunctuation(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow("\u00A1", "Iexcl")]      // Inverted exclamation mark
    [DataRow("\u00A2", "Cent")]       // Cent sign
    [DataRow("\u00A3", "Pound")]      // Pound sign
    [DataRow("\u00A4", "Curren")]     // Currency sign
    [DataRow("\u00A5", "Yen")]        // Yen sign
    [DataRow("\u00A6", "Brvbar")]     // Broken bar
    [DataRow("\u00A7", "Sect")]       // Section sign
    [DataRow("\u00A8", "Uml")]        // Diaeresis
    [DataRow("\u00A9", "Copy")]       // Copyright sign
    [DataRow("\u00AB", "Laquo")]      // Left-pointing double angle quotation
    [DataRow("\u00AC", "Not")]        // Not sign
    [DataRow("\u00AE", "Reg")]        // Registered sign
    [DataRow("\u00AF", "Macr")]       // Macron
    [DataRow("\u00B0", "Deg")]        // Degree sign
    [DataRow("\u00B1", "Plusmn")]     // Plus-minus sign
    [DataRow("\u00B2", "Sup2")]       // Superscript two
    [DataRow("\u00B3", "Sup3")]       // Superscript three
    [DataRow("\u00B4", "Acute")]      // Acute accent
    [DataRow("\u00B6", "Para")]       // Pilcrow sign
    [DataRow("\u00B7", "Middot")]     // Middle dot
    [DataRow("\u00B8", "Cedil")]      // Cedilla
    [DataRow("\u00B9", "Sup1")]       // Superscript one
    [DataRow("\u00BB", "Raquo")]      // Right-pointing double angle quotation
    [DataRow("\u00BC", "Frac14")]     // Vulgar fraction one quarter
    [DataRow("\u00BD", "Frac12")]     // Vulgar fraction one half
    [DataRow("\u00BE", "Frac34")]     // Vulgar fraction three quarters
    [DataRow("\u00BF", "Iquest")]     // Inverted question mark
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_Latin1Supplement(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    [DataRow("\u00D7", "Times")]      // Multiplication sign (MathSymbol, not a letter)
    [DataRow("\u00F7", "Divide")]     // Division sign (MathSymbol, not a letter)
    public void SingleNonLetter_PascalCase_TranslatesCorrectly_MathSymbols(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.PascalCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    [TestMethod]
    public void SingleNonLetter_UnrecognizedCharacter_ReturnsValue()
    {
        // A character not in the switch table returns null -> falls back to "Value"
        var name = new CSharpMemberName("Scope", "\u0001", Casing.PascalCase);
        Assert.AreEqual("Value", name.BuildName());
    }

    #endregion

    #region CamelCase variants

    [TestMethod]
    [DataRow("!", "excl")]
    [DataRow("#", "num")]
    [DataRow("$", "dollar")]
    [DataRow("~", "tilde")]
    [DataRow("0", "zero")]
    [DataRow("9", "nine")]
    public void SingleNonLetter_CamelCase_TranslatesCorrectly(string input, string expected)
    {
        var name = new CSharpMemberName("Scope", input, Casing.CamelCase);
        Assert.AreEqual(expected, name.BuildName());
    }

    #endregion

    #region Fallback path (totalLength == 0 after FixReservedWords)

    [TestMethod]
    public void AllSpecialChars_PascalCase_FallsBackToValue()
    {
        // "---" has no letters/digits -> ToPascalCase strips everything -> fallback to "Value"
        var name = new CSharpMemberName("Scope", "---", Casing.PascalCase);
        Assert.AreEqual("Value", name.BuildName());
    }

    [TestMethod]
    public void AllSpecialChars_CamelCase_FallsBackTovalue()
    {
        // "---" has no letters/digits -> ToCamelCase strips everything -> fallback to "value"
        var name = new CSharpMemberName("Scope", "---", Casing.CamelCase);
        Assert.AreEqual("value", name.BuildName());
    }

    [TestMethod]
    public void AllSpecialChars_Unmodified_UsesRawValue()
    {
        // Unmodified casing doesn't go through FixReservedWords -- uses raw value
        var name = new CSharpMemberName("Scope", "---", Casing.Unmodified);
        Assert.AreEqual("---", name.BuildName());
    }

    #endregion

    #region Prefix and suffix handling

    [TestMethod]
    public void PascalCase_WithPrefix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.PascalCase, prefix: "my");
        Assert.AreEqual("MyHello", name.BuildName());
    }

    [TestMethod]
    public void CamelCase_WithPrefix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.CamelCase, prefix: "my");
        Assert.AreEqual("myHello", name.BuildName());
    }

    [TestMethod]
    public void PascalCase_WithSuffix()
    {
        var name = new CSharpMemberName("Scope", "hello", Casing.PascalCase, suffix: "type");
        Assert.AreEqual("HelloType", name.BuildName());
    }

    [TestMethod]
    public void CamelCase_WithoutPrefix_UsesBaseNameCamelCase()
    {
        var name = new CSharpMemberName("Scope", "Hello", Casing.CamelCase);
        Assert.AreEqual("hello", name.BuildName());
    }

    #endregion

    #region Empty/whitespace baseName

    [TestMethod]
    public void EmptyBaseName_PascalCase_FallsBackToValue()
    {
        var name = new CSharpMemberName("Scope", "", Casing.PascalCase);
        Assert.AreEqual("Value", name.BuildName());
    }

    [TestMethod]
    public void WhitespaceBaseName_PascalCase_FallsBackToValue()
    {
        var name = new CSharpMemberName("Scope", "   ", Casing.PascalCase);
        Assert.AreEqual("Value", name.BuildName());
    }

    #endregion
}
