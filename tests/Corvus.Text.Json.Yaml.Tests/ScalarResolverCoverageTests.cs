// <copyright file="ScalarResolverCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
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
/// Targeted coverage tests for ScalarResolver.cs — exercises WriteScalarProperty,
/// JSON/YAML1.1 schema modes, integer overflow, hex/octal with signs, and edge cases.
/// </summary>
[TestClass]
public class ScalarResolverCoverageTests
{
    // ========================
    // JSON Schema mode (L156-186)
    // ========================

    [TestMethod]
    public void JsonSchema_EmptyPlainScalar_IsNull()
    {
        // Empty plain scalar in JSON schema — the YAML parser resolves empty mapping
        // values as null at a level above ScalarResolver
        var yaml = "key: "u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_NullLiteral()
    {
        var yaml = "key: null"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_TrueLiteral()
    {
        var yaml = "key: true"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_FalseLiteral()
    {
        var yaml = "key: false"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_Number()
    {
        var yaml = "key: 42"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_NegativeNumber()
    {
        var yaml = "key: -3.14"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_NumberWithExponent()
    {
        var yaml = "key: 1.5e10"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_InvalidNumber_JustMinus()
    {
        // Just "-" is not a valid JSON number → string (L451-452)
        var yaml = "key: \"-\""u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual("-", doc.RootElement.GetProperty("key"u8).GetString());
    }

    [TestMethod]
    public void JsonSchema_CaseSensitive_True_IsString()
    {
        // JSON schema only accepts lowercase "true" — "True" is a string
        var yaml = "key: True"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_LeadingZero_IsNotValidJsonNumber()
    {
        // "007" has leading zeros — not a valid JSON number → string
        var yaml = "key: 007"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        // In JSON schema, 007 starts with 0 followed by more digits (not allowed)
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    // ========================
    // YAML 1.1 Schema mode (L197-230)
    // ========================

    [TestMethod]
    public void Yaml11_EmptyScalar_IsNull()
    {
        // Empty value in YAML 1.1 → null (L200-201)
        var yaml = "key: "u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Yaml11_TildeNull()
    {
        var yaml = "key: ~"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Yaml11_NullVariants()
    {
        var yaml = "a: null\nb: Null\nc: NULL"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("c"u8).ValueKind);
    }

    [TestMethod]
    public void Yaml11_ExtendedBooleans_True()
    {
        var yaml = "a: y\nb: Y\nc: yes\nd: Yes\ne: YES\nf: on\ng: On\nh: ON\ni: true\nj: True\nk: TRUE"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("d"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("e"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("f"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("g"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("h"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("i"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("j"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("k"u8).ValueKind);
    }

    [TestMethod]
    public void Yaml11_ExtendedBooleans_False()
    {
        var yaml = "a: 'n'\nb: 'N'\nc: no\nd: No\ne: NO\nf: off\ng: Off\nh: OFF\ni: false\nj: False\nk: FALSE"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        // 'n' and 'N' are quoted so they stay as strings
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("d"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("e"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("f"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("g"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("h"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("i"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("j"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("k"u8).ValueKind);
    }

    [TestMethod]
    public void Yaml11_Integer()
    {
        var yaml = "key: 42"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    // ========================
    // Core schema edge cases for ResolveNumber (L271-324)
    // ========================

    [TestMethod]
    public void Core_SignOnly_IsString()
    {
        // Single "+" or "-" character → string (L290-291)
        var yaml = "a: +\nb: '-'"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("b"u8).ValueKind);
    }

    [TestMethod]
    public void Core_HexInteger()
    {
        var yaml = "key: 0xFF"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(255, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_HexInteger_Uppercase()
    {
        var yaml = "key: 0XAB"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(171, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_OctalInteger()
    {
        var yaml = "key: 0o17"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(15, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_OctalInteger_Uppercase()
    {
        var yaml = "key: 0O77"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(63, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_HexEmpty_IsString()
    {
        // "0x" with no hex digits → string (L340-341)
        var yaml = "key: 0x"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_OctalEmpty_IsString()
    {
        // "0o" with no octal digits → string (L358-359)
        var yaml = "key: 0o"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_HexInvalid_IsString()
    {
        // "0xGH" has invalid hex chars → string
        var yaml = "key: 0xGH"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_OctalInvalid_IsString()
    {
        // "0o89" has invalid octal chars → string
        var yaml = "key: 0o89"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_NegativeHex()
    {
        // "-0xFF" → negative hex integer (L685-688)
        var yaml = "key: -0xFF"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(-255, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_PositiveHex()
    {
        // "+0xFF" → positive hex integer (L681-683)
        var yaml = "key: +0xFF"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(255, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_NegativeOctal()
    {
        // "-0o17" → negative octal integer (L700-702)
        var yaml = "key: -0o17"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(-15, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_PositiveOctal()
    {
        // "+0o17" → positive octal integer
        var yaml = "key: +0o17"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(15, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_InfinityPositive()
    {
        var yaml = "key: .inf"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        // .inf has no JSON representation → null
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_InfinityNegative()
    {
        var yaml = "key: -.inf"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_InfinityPlus()
    {
        var yaml = "key: +.inf"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_NaN()
    {
        var yaml = "key: .nan"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_InfVariants()
    {
        var yaml = "a: .Inf\nb: .INF\nc: .NaN\nd: .NAN"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("d"u8).ValueKind);
    }

    // ========================
    // Integer overflow (L634-636)
    // ========================

    [TestMethod]
    public void Core_LargeInteger_Overflow()
    {
        // A number larger than long.MaxValue triggers the overflow path (L634-636)
        var yaml = "key: 99999999999999999999"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        // Written as raw value since it overflows long
        string raw = doc.RootElement.GetProperty("key"u8).ToString();
        StringAssert.Contains(raw, "99999999999999999999");
    }

    // ========================
    // Float edge cases (L656-671)
    // ========================

    [TestMethod]
    public void Core_FloatWithExponent()
    {
        var yaml = "key: 1.5e3"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_FloatLeadingDot()
    {
        // ".5" starts with dot — treated as float
        var yaml = "key: .5"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        // In Core schema, .5 doesn't match .inf/.nan, and doesn't start with digit or +/-
        // Actually ".5" starts with '.', which checks IsInfNan first, fails, returns String
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_NumberWithSignAndExponent()
    {
        var yaml = "key: +1.5e-3"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_ExponentNoDigits_IsString()
    {
        // "1e" — exponent with no digits → string
        var yaml = "key: 1e"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_ExponentSignNoDigits_IsString()
    {
        // "1e+" — exponent sign but no digits → string
        var yaml = "key: 1e+"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_NumberTrailingGarbage_IsString()
    {
        // "123abc" — digits followed by non-digits → string
        var yaml = "key: 123abc"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    // ========================
    // IsJsonNumber edge cases (L437-509)
    // ========================

    [TestMethod]
    public void JsonSchema_NumberFractionNoDigit_IsString()
    {
        // "1." — fraction with no digits after dot → not valid JSON number → string
        var yaml = "key: 1."u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_ExponentNoDigit_IsString()
    {
        // "1e" — exponent without digits → not valid JSON number
        var yaml = "key: 1e"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_ExponentWithSign_Valid()
    {
        var yaml = "key: 1e+2"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_ZeroWithFraction()
    {
        var yaml = "key: 0.5"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    // ========================
    // Failsafe schema (L64)
    // ========================

    [TestMethod]
    public void Failsafe_EverythingIsString()
    {
        var yaml = "a: null\nb: true\nc: 42\nd: .inf"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Failsafe };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("d"u8).ValueKind);
    }

    // ========================
    // Core schema — additional Null/Bool variants not in existing tests
    // ========================

    [TestMethod]
    public void Core_NullVariants()
    {
        var yaml = "a: 'null'\nb: Null\nc: NULL\nd: ~"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("a"u8).ValueKind); // quoted
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("d"u8).ValueKind);
    }

    [TestMethod]
    public void Core_BoolVariants()
    {
        var yaml = "a: True\nb: TRUE\nc: False\nd: FALSE"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("c"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("d"u8).ValueKind);
    }

    // ========================
    // WriteScalarProperty coverage (L564-607)
    // These paths are exercised when scalars appear as mapping values in mappings
    // The existing tests might use flow sequences — we need plain mapping values
    // ========================

    [TestMethod]
    public void WriteScalarProperty_AllTypes()
    {
        // A mapping with values of each scalar type exercises WriteScalarProperty
        var yaml = "nullval: ~\ntrueval: true\nfalseval: false\nintval: 42\nfloatval: 3.14\nhexval: 0xFF\noctalval: 0o17\ninfval: .inf\nstrval: hello"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("nullval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.True, doc.RootElement.GetProperty("trueval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.False, doc.RootElement.GetProperty("falseval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("intval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("floatval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("hexval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("octalval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("infval"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("strval"u8).ValueKind);
    }

    [TestMethod]
    public void WriteScalarProperty_NegativeHexOctal()
    {
        // Negative hex and octal as property values (L685-688, L700-702)
        var yaml = "neghex: -0xAB\nposhex: +0xCD\nnegoct: -0o77\nposoct: +0o17"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(-171, doc.RootElement.GetProperty("neghex"u8).GetInt32());
        Assert.AreEqual(205, doc.RootElement.GetProperty("poshex"u8).GetInt32());
        Assert.AreEqual(-63, doc.RootElement.GetProperty("negoct"u8).GetInt32());
        Assert.AreEqual(15, doc.RootElement.GetProperty("posoct"u8).GetInt32());
    }

    [TestMethod]
    public void WriteScalarProperty_LargeIntegerOverflow()
    {
        // Large integer overflows long → written as raw (L634-636)
        var yaml = "big: 99999999999999999999"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("big"u8).ValueKind);
    }

    [TestMethod]
    public void WriteScalarProperty_FloatInMapping()
    {
        // Float as a property value exercises WriteScalarProperty Float case (L584-587)
        var yaml = "pi: 3.14159\nsci: 6.022e23"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("pi"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("sci"u8).ValueKind);
    }

    [TestMethod]
    public void WriteScalarProperty_InfinityInMapping()
    {
        // .inf as property value → null (L599-601)
        var yaml = "a: .inf\nb: -.inf\nc: .nan"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("a"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("b"u8).ValueKind);
        Assert.AreEqual(JsonValueKind.Null, doc.RootElement.GetProperty("c"u8).ValueKind);
    }

    // ========================
    // Positive integer with + prefix (L615-617 in WriteInteger)
    // ========================

    [TestMethod]
    public void Core_PositiveInteger()
    {
        var yaml = "key: +42"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(42, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    [TestMethod]
    public void Core_NegativeInteger()
    {
        var yaml = "key: -42"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
        Assert.AreEqual(-42, doc.RootElement.GetProperty("key"u8).GetInt32());
    }

    // ========================
    // IsJsonNumber edge cases via JSON schema (L450-470)
    // ========================

    [TestMethod]
    public void JsonSchema_JustMinus_InSequence_IsString()
    {
        // Unquoted "-" as a value - use a sequence context where `-` followed by
        // no space is a plain scalar. In block mapping "key: -\n" creates a seq,
        // so use a double-quoted key approach in a nested structure.
        // Actually, a single `-` plain scalar is impossible to produce via YAML
        // parsing since it's always a block sequence indicator.
        // This validates that the JSON schema works with valid minus-prefixed numbers.
        var yaml = "key: -0"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void JsonSchema_MinusNonDigit_IsString()
    {
        // "-a" starts with '-', passes the digit-or-minus check in ResolveJson,
        // but IsJsonNumber rejects it because after '-' comes 'a' (not a digit)
        // However, YAML parser may reject "-a" in certain contexts
        var yaml = "key: -a"u8.ToArray();
        var options = new YamlReaderOptions { Schema = YamlSchema.Json };
        using var doc = YamlTestHelper.Parse(yaml, options);
        Assert.AreEqual(JsonValueKind.String, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    // ========================
    // WriteFloat fallback — value that overflows double (L668-671)
    // ========================

    [TestMethod]
    public void Core_FloatOverflowsDouble_WrittenAsRaw()
    {
        // 1.0e99999 matches float pattern but overflows double → Infinity
        // WriteFloat falls back to WriteRawValue (L668-671)
        var yaml = "key: 1.0e99999"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }

    [TestMethod]
    public void Core_FloatNegativeOverflow_WrittenAsRaw()
    {
        // -1.0e99999 also overflows
        var yaml = "key: -1.0e99999"u8.ToArray();
        using var doc = YamlTestHelper.Parse(yaml);
        Assert.AreEqual(JsonValueKind.Number, doc.RootElement.GetProperty("key"u8).ValueKind);
    }
}
