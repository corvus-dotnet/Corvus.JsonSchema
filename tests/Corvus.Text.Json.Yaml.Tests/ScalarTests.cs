// <copyright file="ScalarTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;
using Xunit;

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Comprehensive scalar tests covering all styles, escape sequences, and edge cases.
/// </summary>
public class ScalarTests
{
    // ========================
    // Double-quoted scalars
    // ========================

    [Fact]
    public void DoubleQuoted_SimpleString()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"hello\""u8.ToArray());
        Assert.Equal("hello", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeNewline()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"hello\\nworld\""u8.ToArray());
        Assert.Equal("hello\nworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeTab()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"hello\\tworld\""u8.ToArray());
        Assert.Equal("hello\tworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeBackslash()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"back\\\\slash\""u8.ToArray());
        Assert.Equal("back\\slash", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeDoubleQuote()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"say \\\"hello\\\"\""u8.ToArray());
        Assert.Equal("say \"hello\"", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeNull()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"null\\0char\""u8.ToArray());
        Assert.Equal("null\0char", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EscapeCarriageReturn()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"cr\\rhere\""u8.ToArray());
        Assert.Equal("cr\rhere", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_Unicode2Digit()
    {
        // \x41 = 'A'
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"\\x41\""u8.ToArray());
        Assert.Equal("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_Unicode4Digit()
    {
        // \u0041 = 'A'
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"\\u0041\""u8.ToArray());
        Assert.Equal("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_Unicode8Digit()
    {
        // \U00000041 = 'A'
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"\\U00000041\""u8.ToArray());
        Assert.Equal("A", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void DoubleQuoted_EmptyString()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: \"\""u8.ToArray());
        Assert.Equal("", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Single-quoted scalars
    // ========================

    [Fact]
    public void SingleQuoted_SimpleString()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: 'hello'"u8.ToArray());
        Assert.Equal("hello", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void SingleQuoted_EscapedSingleQuote()
    {
        // '' inside single-quoted → '
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: 'it''s'"u8.ToArray());
        Assert.Equal("it's", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void SingleQuoted_NoEscapeSequences()
    {
        // \n is literal in single-quoted, not an escape
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: 'hello\\nworld'"u8.ToArray());
        Assert.Equal("hello\\nworld", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void SingleQuoted_EmptyString()
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse("key: ''"u8.ToArray());
        Assert.Equal("", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Plain scalars — type resolution (Core Schema)
    // ========================

    [Theory]
    [InlineData("null", JsonValueKind.Null)]
    [InlineData("Null", JsonValueKind.Null)]
    [InlineData("NULL", JsonValueKind.Null)]
    [InlineData("~", JsonValueKind.Null)]
    public void PlainScalar_NullValues(string yaml, JsonValueKind expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void PlainScalar_BoolValues(string yaml, bool expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("123", 123)]
    [InlineData("-42", -42)]
    [InlineData("+99", 99)]
    public void PlainScalar_DecimalIntegers(string yaml, int expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [Theory]
    [InlineData("0xFF", 255)]
    [InlineData("0xff", 255)]
    [InlineData("0x0", 0)]
    public void PlainScalar_HexIntegers(string yaml, int expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [Theory]
    [InlineData("0o77", 63)]
    [InlineData("0o0", 0)]
    [InlineData("0o10", 8)]
    public void PlainScalar_OctalIntegers(string yaml, int expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [Theory]
    [InlineData("1.5", 1.5)]
    [InlineData("-3.14", -3.14)]
    [InlineData("0.0", 0.0)]
    [InlineData("1.0e5", 1.0e5)]
    [InlineData("1.5E-3", 1.5E-3)]
    public void PlainScalar_Floats(string yaml, double expected)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetDouble());
    }

    [Theory]
    [InlineData(".inf")]
    [InlineData(".Inf")]
    [InlineData(".INF")]
    [InlineData("-.inf")]
    [InlineData("-.Inf")]
    [InlineData("-.INF")]
    [InlineData("+.inf")]
    [InlineData("+.Inf")]
    [InlineData("+.INF")]
    [InlineData(".nan")]
    [InlineData(".NaN")]
    [InlineData(".NAN")]
    public void PlainScalar_InfNan_MapsToNull(string yaml)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [Theory]
    [InlineData("hello")]
    [InlineData("hello world")]
    [InlineData("this is a plain scalar")]
    public void PlainScalar_UnquotedStrings(string yaml)
    {
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            System.Text.Encoding.UTF8.GetBytes($"key: {yaml}"));
        Assert.Equal(yaml, doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Literal block scalars
    // ========================

    [Fact]
    public void LiteralBlock_Simple()
    {
        byte[] yaml = "key: |\n  line1\n  line2\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        Assert.Equal("line1\nline2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void LiteralBlock_StripChomping()
    {
        byte[] yaml = "key: |-\n  line1\n  line2\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        Assert.Equal("line1\nline2", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void LiteralBlock_KeepChomping()
    {
        byte[] yaml = "key: |+\n  line1\n  line2\n\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        Assert.Equal("line1\nline2\n\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // Folded block scalars
    // ========================

    [Fact]
    public void FoldedBlock_Simple()
    {
        byte[] yaml = "key: >\n  line1\n  line2\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        // Folded: adjacent lines are joined with spaces
        Assert.Equal("line1 line2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void FoldedBlock_StripChomping()
    {
        byte[] yaml = "key: >-\n  line1\n  line2\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        Assert.Equal("line1 line2", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void FoldedBlock_KeepChomping()
    {
        byte[] yaml = "key: >+\n  line1\n  line2\n\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        Assert.Equal("line1 line2\n\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [Fact]
    public void FoldedBlock_BlankLinePreserved()
    {
        byte[] yaml = "key: >\n  para1\n\n  para2\n"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(yaml);
        // Per YAML 1.2 spec, b-l-trimmed for normal→normal:
        // b-non-content consumes the break after para1, l-empty produces 1 LF for the blank line.
        // Total: 1 LF between para1 and para2.
        Assert.Equal("para1\npara2\n", doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // YAML 1.1 compatibility
    // ========================

    [Theory]
    [InlineData("yes", true)]
    [InlineData("Yes", true)]
    [InlineData("YES", true)]
    [InlineData("no", false)]
    [InlineData("No", false)]
    [InlineData("NO", false)]
    [InlineData("on", true)]
    [InlineData("On", true)]
    [InlineData("ON", true)]
    [InlineData("off", false)]
    [InlineData("Off", false)]
    [InlineData("OFF", false)]
    public void Yaml11_BoolValues(string yaml, bool expected)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Yaml11 });
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    // ========================
    // Failsafe schema
    // ========================

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("null")]
    [InlineData("123")]
    [InlineData("1.5")]
    public void Failsafe_AllValuesAreStrings(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Failsafe });
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.Equal(yaml, doc.RootElement.GetProperty("key"u8).GetString());
    }

    // ========================
    // JSON schema (strict)
    // ========================

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void JsonSchema_BoolValues(string yaml, bool expected)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.Equal(expected, doc.RootElement.GetProperty("key"u8).GetBoolean());
    }

    [Theory]
    [InlineData("null")]
    public void JsonSchema_NullValue(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [Theory]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("Null")]
    [InlineData("NULL")]
    [InlineData("~")]
    public void JsonSchema_CasedValuesAreStrings(string yaml)
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes($"key: {yaml}");
        using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse(
            data, new YamlReaderOptions { Schema = YamlSchema.Json });
        Assert.Equal(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }
}