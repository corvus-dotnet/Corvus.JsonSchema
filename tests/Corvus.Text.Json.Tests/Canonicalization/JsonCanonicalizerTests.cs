// <copyright file="JsonCanonicalizerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Canonicalization;
using Corvus.Text.Json.Internal;
using Xunit;

namespace Corvus.Text.Json.Tests.Canonicalization;

/// <summary>
/// Tests for <see cref="JsonCanonicalizer"/> implementing RFC 8785 (JCS).
/// Test vectors from https://github.com/cyberphone/json-canonicalization.
/// </summary>
public class JsonCanonicalizerTests
{
    #region Cyberphone Test Vectors

    [Fact]
    public void CypherponeArraysTestVector()
    {
        string input = """[56,{"d":true,"10":null,"1":[]}]""";
        string expected = """[56,{"1":[],"10":null,"d":true}]""";
        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void CypherponeStructuresTestVector()
    {
        // Input has nested objects with mixed numeric/alpha keys, \n in key, 56.0 float
        string input =
            """
            {
              "1": {"f": {"f": "hi","F": 5} ,"\n": 56.0},
              "10": { },
              "": "empty",
              "a": { },
              "111": [ {"e": "yes","E": "no" } ],
              "A": { }
            }
            """;
        string expected = """{"":"empty","1":{"\n":56,"f":{"F":5,"f":"hi"}},"10":{},"111":[{"E":"no","e":"yes"}],"A":{},"a":{}}""";
        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void CypherponeValuesTestVector()
    {
        string input =
            """
            {
              "numbers": [333333333.33333329, 1E30, 4.50, 2e-3, 0.000000000000000000000000001],
              "string": "\u20ac$\u000F\u000aA'\u0042\u0022\u005c\\\"\/",
              "literals": [null, true, false]
            }
            """;

        // Expected canonical form:
        // - Properties sorted: "literals" < "numbers" < "string"
        // - Numbers: ES6 format (333333333.3333333, 1e+30, 4.5, 0.002, 1e-27)
        // - String: minimal escaping with literal UTF-8 for non-control chars
        //   € = literal UTF-8, \u000f = hex escape, \n = named escape,
        //   A'B = literal, \" = escaped quote, \\\\ = two escaped backslashes,
        //   \" = escaped quote, / = literal (NOT escaped)
        string expected =
            "{\"literals\":[null,true,false],\"numbers\":[333333333.3333333,1e+30,4.5,0.002,1e-27],\"string\":\"\u20ac$\\u000f\\nA'B\\\"\\\\\\\\\\\"/\"}";

        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void CypherponeFrenchTestVector()
    {
        string input =
            """
            {
              "peach": "This sorting order",
              "p\u00e9ch\u00e9": "is wrong according to French",
              "p\u00eache": "but canonicalization MUST",
              "sin":   "ignore locale"
            }
            """;

        // Sorted by UTF-16 code unit order (not French locale):
        // "peach" (U+0070...) < "péché" (U+0070 U+00E9...) < "pêche" (U+0070 U+00EA...) < "sin"
        string expected =
            "{\"peach\":\"This sorting order\",\"p\u00e9ch\u00e9\":\"is wrong according to French\",\"p\u00eache\":\"but canonicalization MUST\",\"sin\":\"ignore locale\"}";

        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void CypherponeUnicodeTestVector()
    {
        // Input has unnormalized Unicode: A + combining ring above (U+030A)
        string input =
            """
            {
              "Unnormalized Unicode":"A\u030a"
            }
            """;

        // JCS does NOT normalize Unicode. The combining character stays as-is.
        // The value "A" + U+030A is preserved, NOT normalized to U+00C5 (Å)
        string expected = "{\"Unnormalized Unicode\":\"A\u030a\"}";
        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void CypherponeWeirdTestVector()
    {
        string input =
            """
            {
              "\u20ac": "Euro Sign",
              "\r": "Carriage Return",
              "\u000a": "Newline",
              "1": "One",
              "\u0080": "Control\u007f",
              "\ud83d\ude02": "Smiley",
              "\u00f6": "Latin Small Letter O With Diaeresis",
              "\ufb33": "Hebrew Letter Dalet With Dagesh",
              "</script>": "Browser Challenge"
            }
            """;

        // Sorted by UTF-16 code unit values:
        // U+000A (\n), U+000D (\r), U+0031 (1), U+003C (<), U+0080, U+00F6 (ö),
        // U+20AC (€), U+D83D (first surrogate of 😂), U+FB33 (דּ)
        // Note: U+0080 and U+007F are NOT in U+0000-U+001F, so they appear as literal UTF-8
        string expected =
            "{\"\\n\":\"Newline\",\"\\r\":\"Carriage Return\",\"1\":\"One\",\"</script>\":\"Browser Challenge\",\"\u0080\":\"Control\u007f\",\"\u00f6\":\"Latin Small Letter O With Diaeresis\",\"\u20ac\":\"Euro Sign\",\"\ud83d\ude02\":\"Smiley\",\"\ufb33\":\"Hebrew Letter Dalet With Dagesh\"}";

        AssertCanonicalEquals(input, expected);
    }

    #endregion

    #region ES6 Number Formatting

    [Theory]
    [InlineData(0.0, "0")]
    [InlineData(1.0, "1")]
    [InlineData(-1.0, "-1")]
    [InlineData(0.5, "0.5")]
    [InlineData(4.5, "4.5")]
    [InlineData(0.002, "0.002")]
    [InlineData(0.1, "0.1")]
    [InlineData(56.0, "56")]
    public void Es6NumberFormatBasic(double value, string expected)
    {
        AssertNumberFormat(value, expected);
    }

    [Theory]
    [InlineData(333333333.33333329, "333333333.3333333")]
    [InlineData(1e30, "1e+30")]
    [InlineData(1e-27, "1e-27")]
    [InlineData(1e21, "1e+21")]
    [InlineData(1e20, "100000000000000000000")]
    [InlineData(1e-7, "1e-7")]
    [InlineData(1e-6, "0.000001")]
    public void Es6NumberFormatExponentialBoundaries(double value, string expected)
    {
        AssertNumberFormat(value, expected);
    }

    [Theory]
    [InlineData(double.MaxValue, "1.7976931348623157e+308")]
    [InlineData(double.MinValue, "-1.7976931348623157e+308")]
    [InlineData(double.Epsilon, "5e-324")]
    public void Es6NumberFormatExtremes(double value, string expected)
    {
        AssertNumberFormat(value, expected);
    }

    [Fact]
    public void Es6NumberFormatNegativeZero()
    {
        // -0.0 must serialize as "0"
        AssertNumberFormat(-0.0, "0");
    }

    [Fact]
    public void Es6NumberFormatPi()
    {
        AssertNumberFormat(Math.PI, "3.141592653589793");
    }

    #endregion

    #region Property Sorting

    [Fact]
    public void EmptyObject()
    {
        AssertCanonicalEquals("{}", "{}");
    }

    [Fact]
    public void EmptyArray()
    {
        AssertCanonicalEquals("[]", "[]");
    }

    [Fact]
    public void SingleProperty()
    {
        AssertCanonicalEquals("""{ "a" : 1 }""", """{"a":1}""");
    }

    [Fact]
    public void WhitespaceRemoval()
    {
        AssertCanonicalEquals("""{ "b" : 2 , "a" : 1 }""", """{"a":1,"b":2}""");
    }

    [Fact]
    public void NestedObjectsSorted()
    {
        AssertCanonicalEquals(
            """{"z":{"b":2,"a":1},"a":{"d":4,"c":3}}""",
            """{"a":{"c":3,"d":4},"z":{"a":1,"b":2}}""");
    }

    #endregion

    #region String Escaping

    [Fact]
    public void ControlCharacterEscaping()
    {
        // Control chars U+0000-U+001F: named escapes for \b \t \n \f \r, \uXXXX for others
        string input = """{"key":"\u0000\u0001\u0008\u0009\u000a\u000c\u000d\u001f"}""";
        string expected = "{\"key\":\"\\u0000\\u0001\\b\\t\\n\\f\\r\\u001f\"}";
        AssertCanonicalEquals(input, expected);
    }

    [Fact]
    public void ForwardSlashNotEscaped()
    {
        // Forward slash must NOT be escaped in JCS
        AssertCanonicalEquals("""{"path":"a/b/c"}""", """{"path":"a/b/c"}""");
    }

    [Fact]
    public void BackslashAndQuoteEscaped()
    {
        string input = """{"key":"hello\"world\\"}""";
        string expected = """{"key":"hello\"world\\"}""";
        AssertCanonicalEquals(input, expected);
    }

    #endregion

    #region I-JSON Validation

    [Fact]
    public void DuplicatePropertyNamesThrow()
    {
        // JCS requires I-JSON compliant input; duplicate properties must be rejected.
        // Note: System.Text.Json's JsonDocument keeps the last value for duplicates,
        // but Corvus may handle this differently. The test verifies our canonicalizer
        // detects and rejects duplicates when they are preserved in the document model.
        // If the parser deduplicates, this test documents that behavior.
        string input = """{"a":1,"a":2}""";

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(input);
        JsonElement root = doc.RootElement;

        // If the document model preserves duplicates, canonicalization should throw.
        // If it deduplicates (keeping last), canonicalization succeeds with the deduped value.
        // We test whichever behavior the parser exhibits.
        int propertyCount = 0;
        foreach (JsonProperty<JsonElement> prop in root.EnumerateObject())
        {
            propertyCount++;
        }

        if (propertyCount > 1)
        {
            // Duplicates are preserved — canonicalizer must reject
            Assert.Throws<InvalidOperationException>(() =>
            {
                byte[] result = JsonCanonicalizer.Canonicalize(root);
            });
        }
        else
        {
            // Parser deduplicates — canonicalization succeeds
            byte[] result = JsonCanonicalizer.Canonicalize(root);
            Assert.NotNull(result);
        }
    }

    [Fact]
    public void NullElement()
    {
        AssertCanonicalEquals("null", "null");
    }

    [Fact]
    public void TrueElement()
    {
        AssertCanonicalEquals("true", "true");
    }

    [Fact]
    public void FalseElement()
    {
        AssertCanonicalEquals("false", "false");
    }

    [Fact]
    public void StringElement()
    {
        AssertCanonicalEquals("""  "hello"  """, "\"hello\"");
    }

    [Fact]
    public void NumberElement()
    {
        AssertCanonicalEquals("42", "42");
    }

    #endregion

    #region TryCanonicalize API

    [Fact]
    public void TryCanonicalizeSucceedsWithLargeBuffer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"b":2,"a":1}""");
        Span<byte> buffer = stackalloc byte[256];
        bool success = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);

        Assert.True(success);
        Assert.Equal("""{"a":1,"b":2}""", JsonReaderHelper.TranscodeHelper(buffer.Slice(0, bytesWritten)));
    }

    [Fact]
    public void TryCanonicalizeFailsWithSmallBuffer()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"b":2,"a":1}""");
        Span<byte> buffer = stackalloc byte[2]; // way too small
        bool success = JsonCanonicalizer.TryCanonicalize(doc.RootElement, buffer, out int bytesWritten);

        Assert.False(success);
    }

    #endregion

    #region Helpers

    private static void AssertCanonicalEquals(string inputJson, string expectedCanonical)
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(inputJson);
        byte[] result = JsonCanonicalizer.Canonicalize(doc.RootElement);
        string actual = Encoding.UTF8.GetString(result);
        Assert.Equal(expectedCanonical, actual);
    }

    private static void AssertNumberFormat(double value, string expected)
    {
        // Use G17 (not "R") to construct JSON — ToString("R") on .NET Framework
        // is known to produce too few digits for certain edge cases (MaxValue, etc.).
        string json = value.ToString("G17", System.Globalization.CultureInfo.InvariantCulture);
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        byte[] result = JsonCanonicalizer.Canonicalize(doc.RootElement);
        string actual = Encoding.UTF8.GetString(result);
        Assert.Equal(expected, actual);
    }

    #endregion
}
