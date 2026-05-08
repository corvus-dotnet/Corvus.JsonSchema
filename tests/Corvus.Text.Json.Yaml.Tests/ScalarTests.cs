// <copyright file="ScalarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
using System.Text.Json;
using Corvus.Yaml;
#else
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
#endif
using Microsoft.VisualStudio.TestTools.UnitTesting;

#if STJ
namespace Corvus.Yaml.SystemTextJson.Tests;
#else
namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Comprehensive scalar tests covering all styles, escape sequences, and edge cases.
/// </summary>
[TestClass]
public class ScalarTests
{
    // ========================
    // Double-quoted scalars
    // ========================

    [TestMethod]
    public void DoubleQuoted_SimpleString()
    {
        using var doc = YamlTestHelper.Parse("key: \"hello\""u8.ToArray());
        Assert.AreEqual("hello", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeNewline()
    {
        using var doc = YamlTestHelper.Parse("key: \"hello\\nworld\""u8.ToArray());
        Assert.AreEqual("hello\nworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeTab()
    {
        using var doc = YamlTestHelper.Parse("key: \"hello\\tworld\""u8.ToArray());
        Assert.AreEqual("hello\tworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeBackslash()
    {
        using var doc = YamlTestHelper.Parse("key: \"back\\\\slash\""u8.ToArray());
        Assert.AreEqual("back\\slash", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeDoubleQuote()
    {
        using var doc = YamlTestHelper.Parse("key: \"say \\\"hello\\\"\""u8.ToArray());
        Assert.AreEqual("say \"hello\"", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeNull()
    {
        using var doc = YamlTestHelper.Parse("key: \"null\\0char\""u8.ToArray());
        Assert.AreEqual("null\0char", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EscapeCarriageReturn()
    {
        using var doc = YamlTestHelper.Parse("key: \"cr\\rhere\""u8.ToArray());
        Assert.AreEqual("cr\rhere", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_Unicode2Digit()
    {
        // \x41 = 'A'
        using var doc = YamlTestHelper.Parse("key: \"\\x41\""u8.ToArray());
        Assert.AreEqual("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_Unicode4Digit()
    {
        // \u0041 = 'A'
        using var doc = YamlTestHelper.Parse("key: \"\\u0041\""u8.ToArray());
        Assert.AreEqual("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_Unicode8Digit()
    {
        // \U00000041 = 'A'
        using var doc = YamlTestHelper.Parse("key: \"\\U00000041\""u8.ToArray());
        Assert.AreEqual("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void DoubleQuoted_EmptyString()
    {
        using var doc = YamlTestHelper.Parse("key: \"\""u8.ToArray());
        Assert.AreEqual("", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Single-quoted scalars
    // ========================

    [TestMethod]
    public void SingleQuoted_SimpleString()
    {
        using var doc = YamlTestHelper.Parse("key: 'hello'"u8.ToArray());
        Assert.AreEqual("hello", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void SingleQuoted_EscapedSingleQuote()
    {
        // '' inside single-quoted → '
        using var doc = YamlTestHelper.Parse("key: 'it''s'"u8.ToArray());
        Assert.AreEqual("it's", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void SingleQuoted_NoEscapeSequences()
    {
        // \n is literal in single-quoted, not an escape
        using var doc = YamlTestHelper.Parse("key: 'hello\\nworld'"u8.ToArray());
        Assert.AreEqual("hello\\nworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void SingleQuoted_EmptyString()
    {
        using var doc = YamlTestHelper.Parse("key: ''"u8.ToArray());
        Assert.AreEqual("", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Plain scalars — type resolution (Core Schema)
    // ========================

    [TestMethod]
    [DataRow("null", JsonValueKind.Null)]
    [DataRow("Null", JsonValueKind.Null)]
    [DataRow("NULL", JsonValueKind.Null)]
    [DataRow("~", JsonValueKind.Null)]
    public void PlainScalar_NullValues(string yaml, JsonValueKind expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("True", true)]
    [DataRow("TRUE", true)]
    [DataRow("false", false)]
    [DataRow("False", false)]
    [DataRow("FALSE", false)]
    public void PlainScalar_BoolValues(string yaml, bool expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("123", 123)]
    [DataRow("-42", -42)]
    [DataRow("+99", 99)]
    public void PlainScalar_DecimalIntegers(string yaml, int expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    [DataRow("0xFF", 255)]
    [DataRow("0xff", 255)]
    [DataRow("0x0", 0)]
    public void PlainScalar_HexIntegers(string yaml, int expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    [DataRow("0o77", 63)]
    [DataRow("0o0", 0)]
    [DataRow("0o10", 8)]
    public void PlainScalar_OctalIntegers(string yaml, int expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    [DataRow("1.5", 1.5)]
    [DataRow("-3.14", -3.14)]
    [DataRow("0.0", 0.0)]
    [DataRow("1.0e5", 1.0e5)]
    [DataRow("1.5E-3", 1.5E-3)]
    public void PlainScalar_Floats(string yaml, double expected)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetDouble());
    }

    [TestMethod]
    [DataRow(".inf")]
    [DataRow(".Inf")]
    [DataRow(".INF")]
    [DataRow("-.inf")]
    [DataRow("-.Inf")]
    [DataRow("-.INF")]
    [DataRow("+.inf")]
    [DataRow("+.Inf")]
    [DataRow("+.INF")]
    [DataRow(".nan")]
    [DataRow(".NaN")]
    [DataRow(".NAN")]
    public void PlainScalar_InfNan_MapsToNull(string yaml)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    [DataRow("hello")]
    [DataRow("hello world")]
    [DataRow("this is a plain scalar")]
    public void PlainScalar_UnquotedStrings(string yaml)
    {
        using var doc = YamlTestHelper.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.AreEqual(yaml, doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Literal block scalars
    // ========================

    [TestMethod]
    public void LiteralBlock_Simple()
    {
        byte[] yaml = "key: |\n  line1\n  line2\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual("line1\nline2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void LiteralBlock_StripChomping()
    {
        byte[] yaml = "key: |-\n  line1\n  line2\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual("line1\nline2", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void LiteralBlock_KeepChomping()
    {
        byte[] yaml = "key: |+\n  line1\n  line2\n\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual("line1\nline2\n\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Folded block scalars
    // ========================

    [TestMethod]
    public void FoldedBlock_Simple()
    {
        byte[] yaml = "key: >\n  line1\n  line2\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        // Folded: adjacent lines are joined with spaces
        Assert.AreEqual("line1 line2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void FoldedBlock_StripChomping()
    {
        byte[] yaml = "key: >-\n  line1\n  line2\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual("line1 line2", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void FoldedBlock_KeepChomping()
    {
        byte[] yaml = "key: >+\n  line1\n  line2\n\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual("line1 line2\n\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void FoldedBlock_BlankLinePreserved()
    {
        byte[] yaml = "key: >\n  para1\n\n  para2\n"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        // Per YAML 1.2 spec, b-l-trimmed for normal→normal:
        // b-non-content consumes the break after para1, l-empty produces 1 LF for the blank line.
        // Total: 1 LF between para1 and para2.
        Assert.AreEqual("para1\npara2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // YAML 1.1 compatibility
    // ========================

    [TestMethod]
    [DataRow("yes", true)]
    [DataRow("Yes", true)]
    [DataRow("YES", true)]
    [DataRow("no", false)]
    [DataRow("No", false)]
    [DataRow("NO", false)]
    [DataRow("on", true)]
    [DataRow("On", true)]
    [DataRow("ON", true)]
    [DataRow("off", false)]
    [DataRow("Off", false)]
    [DataRow("OFF", false)]
    public void Yaml11_BoolValues(string yaml, bool expected)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using var doc = YamlTestHelper.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Yaml11 });
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    // ========================
    // Failsafe schema
    // ========================

    [TestMethod]
    [DataRow("true")]
    [DataRow("false")]
    [DataRow("null")]
    [DataRow("123")]
    [DataRow("1.5")]
    public void Failsafe_AllValuesAreStrings(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using var doc = YamlTestHelper.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Failsafe });
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(yaml, doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // JSON schema (strict)
    // ========================

    [TestMethod]
    [DataRow("true", true)]
    [DataRow("false", false)]
    public void JsonSchema_BoolValues(string yaml, bool expected)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using var doc = YamlTestHelper.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.AreEqual(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    [TestMethod]
    [DataRow("null")]
    public void JsonSchema_NullValue(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using var doc = YamlTestHelper.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    [DataRow("True")]
    [DataRow("TRUE")]
    [DataRow("False")]
    [DataRow("FALSE")]
    [DataRow("Null")]
    [DataRow("NULL")]
    [DataRow("~")]
    public void JsonSchema_CasedValuesAreStrings(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using var doc = YamlTestHelper.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }
}