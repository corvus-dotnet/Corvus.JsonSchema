// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.IO;
using System.Text.Encodings.Web;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

public abstract class JsonDomWriteTests
{
    protected static readonly JsonDocumentOptions s_options =
        new()
        {
            CommentHandling = JsonCommentHandling.Skip,
        };

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ReadWriteEscapedPropertyNames(bool indented)
    {
        const string jsonIn = " { \"p\\u0069zza\": 1, \"hello\\u003c\\u003e\": 2, \"normal\": 3 }";

        WriteComplexValue(
            indented,
            jsonIn,
            @"{
  ""pizza"": 1,
  ""hello\u003c\u003e"": 2,
  ""normal"": 3
}",
            "{\"pizza\":1,\"hello\\u003c\\u003e\":2,\"normal\":3}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteAsciiString(bool indented)
    {
        WriteSimpleValue(indented, "\"pizza\"");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteAsciiStringAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "dinner",
            "\"pizza\"",
            @"{
  ""dinner"": ""pizza""
}".NormalizeLineEndings(),
            "{\"dinner\":\"pizza\"}");
    }

    [TestMethod]
    [DataRow("6.022e+23", "6,022e+23")]
    [DataRow("6.022e+23", "6.022f+23")]
    [DataRow("6.022e+23", "6.022e+ 3")]
    [DataRow("6.022e+23", "6e022e+23")]
    [DataRow("6.022e+23", "6.022e+f3")]
    [DataRow("1", "-")]
    [DataRow("12", "+2")]
    [DataRow("12", "1e")]
    [DataRow("12", "1.")]
    [DataRow("12", "02")]
    [DataRow("123", "1e+")]
    [DataRow("123", "1e-")]
    [DataRow("0.12", "0.1e")]
    [DataRow("0.123", "0.1e+")]
    [DataRow("0.123", "0.1e-")]
    [DataRow("10", "+0")]
    [DataRow("101", "-01")]
    [DataRow("12", "1a")]
    [DataRow("10", "00")]
    [DataRow("11", "01")]
    [DataRow("10.5e-012", "10.5e-0.2")]
    [DataRow("10.5e012", "10.5.012")]
    [DataRow("0.123", "0.-23")]
    [DataRow("12345", "hello")]
    public void WriteCorruptedNumber(string parseJson, string overwriteJson)
    {
        if (overwriteJson.Length != parseJson.Length)
        {
            throw new InvalidOperationException("Invalid test, parseJson and overwriteJson must have the same length");
        }

        byte[] utf8Data = Encoding.UTF8.GetBytes(parseJson);

        using (var document = ParsedJsonDocument<JsonElement>.Parse(utf8Data))
        using (var stream = new MemoryStream(Array.Empty<byte>()))
        using (var writer = new Utf8JsonWriter(stream))
        {
            // Use fixed and the older version of GetBytes-in-place because of the .NET Framework build.
            unsafe
            {
                fixed (byte* dataPtr = utf8Data)
                fixed (char* inputPtr = overwriteJson)
                {
                    // Overwrite the number in the memory buffer still referenced by the document.
                    // If it doesn't hit a 100% overlap then we're not testing what we thought we were.
                    Assert.AreEqual(
                        utf8Data.Length,
                        Encoding.UTF8.GetBytes(inputPtr, overwriteJson.Length, dataPtr, utf8Data.Length));
                }
            }

            JsonElement rootElement = document.RootElement;

            Assert.AreEqual(overwriteJson, rootElement.GetRawText());

            AssertEx.ThrowsExactly<ArgumentException>(
                "utf8FormattedNumber",
                () => WriteDocument(document, writer));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyArray(bool indented)
    {
        WriteComplexValue(
            indented,
            "[        ]",
            "[]",
            "[]");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyArrayAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "arr",
            "[        ]",
            @"{
  ""arr"": []
}".NormalizeLineEndings(),
            "{\"arr\":[]}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyCommentedArray(bool indented)
    {
        WriteComplexValue(
            indented,
            "[ /* \"No values here\" */    ]",
            "[]",
            "[]");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyCommentedArrayAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "arr",
            "[   /* 5 */     ]",
            @"{
  ""arr"": []
}".NormalizeLineEndings(),
            "{\"arr\":[]}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyCommentedObject(bool indented)
    {
        WriteComplexValue(
            indented,
            "{ /* Technically empty */ }",
            "{}",
            "{}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyCommentedObjectAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "obj",
            "{ /* Technically empty */ }",
            @"{
  ""obj"": {}
}".NormalizeLineEndings(),
            "{\"obj\":{}}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyObject(bool indented)
    {
        WriteComplexValue(
            indented,
            "{     }",
            "{}",
            "{}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEmptyObjectAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "obj",
            "{       }",
            @"{
  ""obj"": {}
}".NormalizeLineEndings(),
            "{\"obj\":{}}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEscapedNonAsciiString(bool indented)
    {
        // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
        // therefore is ingested as the UTF-8 sequence [ C3 AD ].
        //
        // When writing it back out, the writer turns it into a JSON string
        // using escaped codepoint syntax.
        //
        // The subtlety of the input vs output is the number of backslashes (and
        // the hex casing is different to show the difference more aggressively).
        //
        // The U+007A (lowercase z) is just to make sure nothing weird happens
        // between the de-escape and the UTF-8.
        WriteSimpleValue(indented, "\"p\u00cdz\\u007Aa\"", "\"p\\u00CDzza\"");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEscapedNonAsciiStringAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "lunch",
            "\"p\u00CDz\\u007Aa\"",
            @"{
  ""lunch"": ""p\u00CDzza""
}".NormalizeLineEndings(),
            "{\"lunch\":\"p\\u00CDzza\"}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEscapedString(bool indented)
    {
        WriteSimpleValue(indented, "\"p\\u0069zza\"", "\"pizza\"");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEscapedStringAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "dinner",
            "\"p\\u0069zza\"",
            @"{
  ""dinner"": ""pizza""
}".NormalizeLineEndings(),
            "{\"dinner\":\"pizza\"}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEverythingArray(bool indented)
    {
        WriteComplexValue(
            indented,
            (@"

[
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @""",
// Array with an array (primes)
[ 2, 3, 5, 7, /*9,*/ 11],
{ ""obj"": [ 21, { ""deep obj"": [
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @"""

], ""more deep"": false },
12 ], ""second property"": null }]
").NormalizeLineEndings(),
            @"[
  ""Once upon a midnight dreary"",
  42,
  1e400,
  3.141592653589793238462643383279,
  false,
  true,
  null,
  ""Escaping is not required"",
  ""Some things get lost in the m\u00EAl\u00E9e"",
  [
    2,
    3,
    5,
    7,
    11
  ],
  {
    ""obj"": [
      21,
      {
        ""deep obj"": [
          ""Once upon a midnight dreary"",
          42,
          1e400,
          3.141592653589793238462643383279,
          false,
          true,
          null,
          ""Escaping is not required"",
          ""Some things get lost in the m\u00EAl\u00E9e""
        ],
        ""more deep"": false
      },
      12
    ],
    ""second property"": null
  }
]".NormalizeLineEndings(),
            "[\"Once upon a midnight dreary\",42,1e400,3.141592653589793238462643383279," +
                "false,true,null,\"Escaping is not required\"," +
                "\"Some things get lost in the m\\u00EAl\\u00E9e\",[2,3,5,7,11]," +
                "{\"obj\":[21,{\"deep obj\":[\"Once upon a midnight dreary\",42,1e400," +
                "3.141592653589793238462643383279,false,true,null,\"Escaping is not required\"," +
                "\"Some things get lost in the m\\u00EAl\\u00E9e\"],\"more deep\":false},12]," +
                "\"second property\":null}]");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEverythingArrayAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "data",
            @"

[
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @""",
// Array with an array (primes)
[ 2, 3, 5, 7, /*9,*/ 11],
{ ""obj"": [ 21, { ""deep obj"": [
        ""Once upon a midnight dreary"",
        42, /* Yep */ 1e400,
        3.141592653589793238462643383279,
false,
true,
null,
  ""Escaping is not requ\u0069red"",
// More comments, more problems?
 ""Some th\u0069ngs get lost in the " + "m\u00EAl\u00E9e" + @"""

], ""more deep"": false },
12 ], ""second property"": null }]
",
            @"{
  ""data"": [
    ""Once upon a midnight dreary"",
    42,
    1e400,
    3.141592653589793238462643383279,
    false,
    true,
    null,
    ""Escaping is not required"",
    ""Some things get lost in the m\u00EAl\u00E9e"",
    [
      2,
      3,
      5,
      7,
      11
    ],
    {
      ""obj"": [
        21,
        {
          ""deep obj"": [
            ""Once upon a midnight dreary"",
            42,
            1e400,
            3.141592653589793238462643383279,
            false,
            true,
            null,
            ""Escaping is not required"",
            ""Some things get lost in the m\u00EAl\u00E9e""
          ],
          ""more deep"": false
        },
        12
      ],
      ""second property"": null
    }
  ]
}".NormalizeLineEndings(),

            "{\"data\":[\"Once upon a midnight dreary\",42,1e400,3.141592653589793238462643383279," +
                "false,true,null,\"Escaping is not required\"," +
                "\"Some things get lost in the m\\u00EAl\\u00E9e\",[2,3,5,7,11]," +
                "{\"obj\":[21,{\"deep obj\":[\"Once upon a midnight dreary\",42,1e400," +
                "3.141592653589793238462643383279,false,true,null,\"Escaping is not required\"," +
                "\"Some things get lost in the m\\u00EAl\\u00E9e\"],\"more deep\":false},12]," +
                "\"second property\":null}]}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEverythingObject(bool indented)
    {
        WriteComplexValue(
            indented,
            "{" +
                "\"int\": 42," +
                "\"quadratic googol\": 1e400," +
                "\"precisePi\": 3.141592653589793238462643383279," +
                "\"lit0\": null,\"lit1\":  false,/*guess next*/\"lit2\": true," +
                "\"ascii\": \"pizza\"," +
                "\"escaped\": \"p\\u0069zza\"," +
                "\"utf8\": \"p\u00CDzza\"," +
                "\"utf8ExtraEscape\": \"p\u00CDz\\u007Aa\"," +
                "\"arr\": [\"hello\", \"sa\\u0069lor\", 21, \"blackjack!\" ]," +
                "\"obj\": {" +
                    "\"arr\": [ 1, 3, 5, 7, /*9,*/ 11] " +
                "}}",
            @"{
  ""int"": 42,
  ""quadratic googol"": 1e400,
  ""precisePi"": 3.141592653589793238462643383279,
  ""lit0"": null,
  ""lit1"": false,
  ""lit2"": true,
  ""ascii"": ""pizza"",
  ""escaped"": ""pizza"",
  ""utf8"": ""p\u00CDzza"",
  ""utf8ExtraEscape"": ""p\u00CDzza"",
  ""arr"": [
    ""hello"",
    ""sailor"",
    21,
    ""blackjack!""
  ],
  ""obj"": {
    ""arr"": [
      1,
      3,
      5,
      7,
      11
    ]
  }
}".NormalizeLineEndings(),
            "{\"int\":42,\"quadratic googol\":1e400,\"precisePi\":3.141592653589793238462643383279," +
                "\"lit0\":null,\"lit1\":false,\"lit2\":true,\"ascii\":\"pizza\",\"escaped\":\"pizza\"," +
                "\"utf8\":\"p\\u00CDzza\",\"utf8ExtraEscape\":\"p\\u00CDzza\"," +
                "\"arr\":[\"hello\",\"sailor\",21,\"blackjack!\"]," +
                "\"obj\":{\"arr\":[1,3,5,7,11]}}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteEverythingObjectAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "data",
            "{" +
                "\"int\": 42," +
                "\"quadratic googol\": 1e400," +
                "\"precisePi\": 3.141592653589793238462643383279," +
                "\"lit0\": null,\"lit1\":  false,/*guess next*/\"lit2\": true," +
                "\"ascii\": \"pizza\"," +
                "\"escaped\": \"p\\u0069zza\"," +
                "\"utf8\": \"p\u00CDzza\"," +
                "\"utf8ExtraEscape\": \"p\u00CDz\\u007Aa\"," +
                "\"arr\": [\"hello\", \"sa\\u0069lor\", 21, \"blackjack!\" ]," +
                "\"obj\": {" +
                    "\"arr\": [ 1, 3, 5, 7, /*9,*/ 11] " +
                "}}",
            @"{
  ""data"": {
    ""int"": 42,
    ""quadratic googol"": 1e400,
    ""precisePi"": 3.141592653589793238462643383279,
    ""lit0"": null,
    ""lit1"": false,
    ""lit2"": true,
    ""ascii"": ""pizza"",
    ""escaped"": ""pizza"",
    ""utf8"": ""p\u00CDzza"",
    ""utf8ExtraEscape"": ""p\u00CDzza"",
    ""arr"": [
      ""hello"",
      ""sailor"",
      21,
      ""blackjack!""
    ],
    ""obj"": {
      ""arr"": [
        1,
        3,
        5,
        7,
        11
      ]
    }
  }
}".NormalizeLineEndings(),
            "{\"data\":" +
                "{\"int\":42,\"quadratic googol\":1e400,\"precisePi\":3.141592653589793238462643383279," +
                "\"lit0\":null,\"lit1\":false,\"lit2\":true,\"ascii\":\"pizza\",\"escaped\":\"pizza\"," +
                "\"utf8\":\"p\\u00CDzza\",\"utf8ExtraEscape\":\"p\\u00CDzza\"," +
                "\"arr\":[\"hello\",\"sailor\",21,\"blackjack!\"]," +
                "\"obj\":{\"arr\":[1,3,5,7,11]}}}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteFalse(bool indented)
    {
        WriteSimpleValue(indented, "false");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteFalseAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            " boolean ",
            "false",
            @"{
  "" boolean "": false
}".NormalizeLineEndings(),
            "{\" boolean \":false}");
    }

    [TestMethod]
    public void WriteIncredibleDepth()
    {
        const int TargetDepth = 500;
        JsonDocumentOptions optionsCopy = s_options;
        optionsCopy.MaxDepth = TargetDepth + 1;
        const int SpacesPre = 12;
        const int SpacesSplit = 85;
        const int SpacesPost = 4;

        byte[] jsonIn = new byte[SpacesPre + TargetDepth + SpacesSplit + TargetDepth + SpacesPost];
        jsonIn.AsSpan(0, SpacesPre).Fill((byte)' ');
        Span<byte> openBrackets = jsonIn.AsSpan(SpacesPre, TargetDepth);
        openBrackets.Fill((byte)'[');
        jsonIn.AsSpan(SpacesPre + TargetDepth, SpacesSplit).Fill((byte)' ');
        Span<byte> closeBrackets = jsonIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit, TargetDepth);
        closeBrackets.Fill((byte)']');
        jsonIn.AsSpan(SpacesPre + TargetDepth + SpacesSplit + TargetDepth).Fill((byte)' ');

        var buffer = new ArrayBufferWriter<byte>(jsonIn.Length);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonIn, optionsCopy))
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteDocument(doc, writer);
            }

            ReadOnlySpan<byte> formatted = buffer.WrittenSpan;

            Assert.AreEqual(TargetDepth + TargetDepth, formatted.Length);
            Assert.IsTrue(formatted.Slice(0, TargetDepth).SequenceEqual(openBrackets), "OpenBrackets match");
            Assert.IsTrue(formatted.Slice(TargetDepth).SequenceEqual(closeBrackets), "CloseBrackets match");
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNonAsciiString(bool indented)
    {
        // In the JSON input the U+00ED (lowercase i, acute) is a literal char,
        // therefore is ingested as the UTF-8 sequence [ C3 AD ].
        //
        // When writing it back out, the writer turns it into a JSON string
        // using escaped codepoint syntax.
        //
        // The subtlety of the input vs output is the number of backslashes (and
        // the hex casing is different to show the difference more aggressively).
        WriteSimpleValue(indented, "\"p\u00cdzza\"", "\"p\\u00CDzza\"");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNonAsciiStringAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "lunch",
            "\"p\u00CDzza\"",
            @"{
  ""lunch"": ""p\u00CDzza""
}".NormalizeLineEndings(),
            "{\"lunch\":\"p\\u00CDzza\"}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNull(bool indented)
    {
        WriteSimpleValue(indented, "null");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNullAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "someProp",
            "null",
            @"{
  ""someProp"": null
}".NormalizeLineEndings(),
            "{\"someProp\":null}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumber(bool indented)
    {
        WriteSimpleValue(indented, "42");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "ectoplasm",
            "42",
            @"{
  ""ectoplasm"": 42
}".NormalizeLineEndings(),
            "{\"ectoplasm\":42}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberAsPropertyWithLargeName(bool indented)
    {
        char[] charArray = new char[300];
        charArray.AsSpan().Fill('a');
        charArray[0] = (char)0xEA;
        string propertyName = new(charArray);

        WritePropertyValueBothForms(
            indented,
            propertyName,
            "42",
            (@"{
  ""\u00EA" + propertyName.Substring(1) + @""": 42
}").NormalizeLineEndings(),
            $"{{\"\\u00EA{propertyName.Substring(1)}\":42}}");
    }

    [TestMethod]
    [DataRow("1.2E+3", false)]
    [DataRow("5.012e-20", false)]
    [DataRow("5.012e-20", true)]
    [DataRow("5.012e20", false)]
    [DataRow("5.012e20", true)]
    [DataRow("5.012e+20", false)]
    [DataRow("5.012e+20", true)]
    [DataRow("-5.012e-20", false)]
    [DataRow("-5.012e-20", true)]
    [DataRow("-5.012e20", false)]
    [DataRow("-5.012e20", true)]
    [DataRow("-5.012e+20", false)]
    [DataRow("-5.012e+20", true)]
    public void WriteNumberDecimalScientific(string value, bool indented)
    {
        WriteSimpleValue(indented, value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberOverprecise(bool indented)
    {
        // This value is a reference "potential interoperability problem" from
        // https://tools.ietf.org/html/rfc7159#section-6
        const string PrecisePi = "3.141592653589793238462643383279";

        // To confirm that this test is doing what it intends, one could
        // confirm the printing precision of double, like
        //
        //double precisePi = double.Parse(PrecisePi);
        //Assert.AreNotEqual(PrecisePi, precisePi.ToString(JsonTestHelper.DoubleFormatString));

        WriteSimpleValue(indented, PrecisePi);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberOverpreciseAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "test property",
            "3.141592653589793238462643383279",
            @"{
  ""test property"": 3.141592653589793238462643383279
}".NormalizeLineEndings(),
            "{\"test property\":3.141592653589793238462643383279}");
    }

    [TestMethod]
    [DataRow("12E-3", false)]
    [DataRow("1e6", false)]
    [DataRow("1e6", true)]
    [DataRow("1e+6", false)]
    [DataRow("1e+6", true)]
    [DataRow("1e-6", false)]
    [DataRow("1e-6", true)]
    [DataRow("-1e6", false)]
    [DataRow("-1e6", true)]
    [DataRow("-1e+6", false)]
    [DataRow("-1e+6", true)]
    [DataRow("-1e-6", false)]
    [DataRow("-1e-6", true)]
    public void WriteNumberScientific(string value, bool indented)
    {
        WriteSimpleValue(indented, value);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberScientificAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "m\u00EAl\u00E9e",
            "1e6",
            @"{
  ""m\u00EAl\u00E9e"": 1e6
}".NormalizeLineEndings(),
            "{\"m\\u00EAl\\u00E9e\":1e6}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberTooLargeAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            // Arabic "kabir" => "big"
            "\u0643\u0628\u064A\u0631",
            "1e400",
            @"{
  ""\u0643\u0628\u064A\u0631"": 1e400
}".NormalizeLineEndings(),
            "{\"\\u0643\\u0628\\u064A\\u0631\":1e400}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteNumberTooLargeScientific(bool indented)
    {
        // This value is a reference "potential interoperability problem" from
        // https://tools.ietf.org/html/rfc7159#section-6
        const string OneQuarticGoogol = "1e400";

        // This just validates we write the literal number 1e400 even though it is too
        // large to be represented by System.Double and would be converted to
        // PositiveInfinity instead (or throw if using double.Parse on frameworks
        // older than .NET Core 3.0).
        WriteSimpleValue(indented, OneQuarticGoogol);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteSimpleArray(bool indented)
    {
        WriteComplexValue(
            indented,
            @"[ 2, 4,
6                       , 0

, 1       ]".NormalizeLineEndings(),
            @"[
  2,
  4,
  6,
  0,
  1
]".NormalizeLineEndings(),
            "[2,4,6,0,1]");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteSimpleArrayAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "valjean",
            "[ 2, 4, 6, 0, 1 /* Did you know that there's an asteroid: 24601 Valjean? */ ]",
            @"{
  ""valjean"": [
    2,
    4,
    6,
    0,
    1
  ]
}".NormalizeLineEndings(),
            "{\"valjean\":[2,4,6,0,1]}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteSimpleObject(bool indented)
    {
        WriteComplexValue(
            indented,
            @"{ ""r""   : 2,
// Comments make everything more interesting.
            ""d"":
2
}".NormalizeLineEndings(),
            @"{
  ""r"": 2,
  ""d"": 2
}".NormalizeLineEndings(),
            "{\"r\":2,\"d\":2}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteSimpleObjectAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            "bestMinorCharacter",
            @"{ ""r""   : 2,
// Comments make everything more interesting.
            ""d"":
2
}".NormalizeLineEndings(),
            @"{
  ""bestMinorCharacter"": {
    ""r"": 2,
    ""d"": 2
  }
}".NormalizeLineEndings(),
            "{\"bestMinorCharacter\":{\"r\":2,\"d\":2}}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteSimpleObjectNeedsEscaping(bool indented)
    {
        WriteComplexValue(
            indented,
            @"{ ""prop><erty""   : 3,
            ""> This is one long & unusual property name. <"":
4
}",
            @"{
  ""prop\u003E\u003Certy"": 3,
  ""\u003E This is one long \u0026 unusual property name. \u003C"": 4
}",
            "{\"prop\\u003E\\u003Certy\":3,\"\\u003E This is one long \\u0026 unusual property name. \\u003C\":4}");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteTrue(bool indented)
    {
        WriteSimpleValue(indented, "true");
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteTrueAsProperty(bool indented)
    {
        WritePropertyValueBothForms(
            indented,
            " boolean ",
            "true",
            @"{
  "" boolean "": true
}".NormalizeLineEndings(),
            "{\" boolean \":true}");
    }

    [TestMethod]
    public void WriteValueSurrogatesEscapeString()
    {
        string unicodeString = "\uD800\uDC00\uD803\uDE6D \uD834\uDD1E\uDBFF\uDFFF";
        string expectedStr = "\"\\uD800\\uDC00\\uD803\\uDE6D \\uD834\\uDD1E\\uDBFF\\uDFFF\"";
        string json = $"\"{unicodeString}\"";
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(json))
        {
            using (var writer = new Utf8JsonWriter(buffer))
            {
                WriteSingleValue(doc, writer);
            }

            JsonTestHelper.AssertContents(expectedStr, buffer);
        }
    }

    [TestMethod]
    [DataRow(false, "\"message\"", "\"message\"", true)]
    [DataRow(true, "\"message\"", "\"message\"", true)]
    [DataRow(false, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
    [DataRow(true, "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", "\">><++>>>\\\">>\\\\>>&>>>\u6f22\u5B57>>>\"", false)]
    [DataRow(false, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
    [DataRow(true, "\"mess\\r\\nage\\u0008\\u0001!\"", "\"mess\\r\\nage\\b\\u0001!\"", true)]
    public void WriteWithRelaxedEscaper(bool indented, string jsonIn, string jsonOut, bool matchesRelaxedEscaping)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            {
                var options = new JsonWriterOptions
                {
                    Indented = indented,
                };

                using (var writer = new Utf8JsonWriter(buffer, options))
                {
                    WriteSingleValue(doc, writer);
                }

                if (matchesRelaxedEscaping)
                {
                    JsonTestHelper.AssertContents(jsonOut, buffer);
                }
                else
                {
                    JsonTestHelper.AssertContentsNotEqual(jsonOut, buffer);
                }
            }

            buffer.Clear();

            {
                var options = new JsonWriterOptions
                {
                    Indented = indented,
                    Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                };

                using (var writer = new Utf8JsonWriter(buffer, options))
                {
                    WriteSingleValue(doc, writer);
                }

                JsonTestHelper.AssertContents(jsonOut, buffer);
            }
        }
    }

    protected abstract ParsedJsonDocument<JsonElement> PrepareDocument(string jsonIn);

    protected abstract void WriteDocument(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer);

    protected abstract void WriteSingleValue(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer);

    private void WriteComplexValue(
        bool indented,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        byte[] bufferOutput;

        var options = new JsonWriterOptions
        {
            Indented = indented
        };

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                WriteSingleValue(doc, writer);
            }

            JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);

            bufferOutput = buffer.WrittenSpan.ToArray();
        }

        // After reading the output and writing it again, it should be byte-for-byte identical.
        {
            string bufferString = Encoding.UTF8.GetString(bufferOutput);
            buffer.Clear();

            using (ParsedJsonDocument<JsonElement> doc2 = PrepareDocument(bufferString))
            {
                using (var writer = new Utf8JsonWriter(buffer, options))
                {
                    WriteSingleValue(doc2, writer);
                }
            }

            Assert.IsTrue(buffer.WrittenSpan.SequenceEqual(bufferOutput));
        }
    }

    private void WritePropertyValue(
        bool indented,
        string propertyName,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            var options = new JsonWriterOptions
            {
                Indented = indented,
            };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                WriteSingleValue(doc, writer);
                writer.WriteEndObject();
            }

            JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
        }
    }

    private void WritePropertyValue(
        bool indented,
        ReadOnlySpan<char> propertyName,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            var options = new JsonWriterOptions
            {
                Indented = indented,
            };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                WriteSingleValue(doc, writer);
                writer.WriteEndObject();
            }

            JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
        }
    }

    private void WritePropertyValue(
        bool indented,
        ReadOnlySpan<byte> propertyName,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            var options = new JsonWriterOptions
            {
                Indented = indented,
            };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                WriteSingleValue(doc, writer);
                writer.WriteEndObject();
            }

            JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
        }
    }

    private void WritePropertyValue(
        bool indented,
        JsonEncodedText propertyName,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            var options = new JsonWriterOptions
            {
                Indented = indented,
            };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                writer.WriteStartObject();
                writer.WritePropertyName(propertyName);
                WriteSingleValue(doc, writer);
                writer.WriteEndObject();
            }

            JsonTestHelper.AssertContents(indented ? expectedIndent : expectedMinimal, buffer);
        }
    }

    private void WritePropertyValueBothForms(
        bool indented,
        string propertyName,
        string jsonIn,
        string expectedIndent,
        string expectedMinimal)
    {
        WritePropertyValue(
            indented,
            propertyName,
            jsonIn,
            expectedIndent,
            expectedMinimal);

        WritePropertyValue(
            indented,
            propertyName.AsSpan(),
            jsonIn,
            expectedIndent,
            expectedMinimal);

        WritePropertyValue(
            indented,
            Encoding.UTF8.GetBytes(propertyName ?? ""),
            jsonIn,
            expectedIndent,
            expectedMinimal);

        WritePropertyValue(
            indented,
            JsonEncodedText.Encode(propertyName.AsSpan()),
            jsonIn,
            expectedIndent,
            expectedMinimal);
    }

    private void WriteSimpleValue(bool indented, string jsonIn, string jsonOut = null)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);

        using (ParsedJsonDocument<JsonElement> doc = PrepareDocument(jsonIn))
        {
            var options = new JsonWriterOptions
            {
                Indented = indented,
            };

            using (var writer = new Utf8JsonWriter(buffer, options))
            {
                WriteSingleValue(doc, writer);
            }

            JsonTestHelper.AssertContents(jsonOut ?? jsonIn, buffer);
        }
    }
}

[TestClass]
public sealed class JsonElementWriteTests : JsonDomWriteTests
{
    [TestMethod]
    public void CheckByPassingNullWriter()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("true", default))
        {
            JsonElement root = doc.RootElement;
            AssertEx.ThrowsExactly<ArgumentNullException>("writer", () => root.WriteTo(null));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WritePropertyOutsideObject(bool skipValidation)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
        {
            JsonElement root = doc.RootElement;
            var options = new JsonWriterOptions
            {
                SkipValidation = skipValidation,
            };

            const string CharLabel = "char";
            using var writer = new Utf8JsonWriter(buffer, options);

            if (skipValidation)
            {
                foreach (JsonElement val in root.EnumerateArray())
                {
                    writer.WritePropertyName(CharLabel);
                    val.WriteTo(writer);
                    writer.WritePropertyName(CharLabel.AsSpan());
                    val.WriteTo(writer);
                    writer.WritePropertyName("byte"u8);
                    val.WriteTo(writer);
                    writer.WritePropertyName(JsonEncodedText.Encode(CharLabel));
                    val.WriteTo(writer);
                }

                writer.Flush();

                JsonTestHelper.AssertContents(
                    "\"char\":null,\"char\":null,\"byte\":null,\"char\":null," +
                        "\"char\":false,\"char\":false,\"byte\":false,\"char\":false," +
                        "\"char\":true,\"char\":true,\"byte\":true,\"char\":true," +
                        "\"char\":\"hi\",\"char\":\"hi\",\"byte\":\"hi\",\"char\":\"hi\"," +
                        "\"char\":5,\"char\":5,\"byte\":5,\"char\":5," +
                        "\"char\":{},\"char\":{},\"byte\":{},\"char\":{}," +
                        "\"char\":[],\"char\":[],\"byte\":[],\"char\":[]",
                    buffer);
            }
            else
            {
                Assert.ThrowsExactly<InvalidOperationException>(() => writer.WritePropertyName(CharLabel));
                Assert.ThrowsExactly<InvalidOperationException>(() => writer.WritePropertyName(CharLabel.AsSpan()));
                Assert.ThrowsExactly<InvalidOperationException>(() => writer.WritePropertyName("byte"u8));
                Assert.ThrowsExactly<InvalidOperationException>(() => writer.WritePropertyName(JsonEncodedText.Encode(CharLabel)));

                writer.Flush();

                JsonTestHelper.AssertContents("", buffer);
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void WriteValueInsideObject(bool skipValidation)
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[ null, false, true, \"hi\", 5, {}, [] ]", s_options))
        {
            JsonElement root = doc.RootElement;
            var options = new JsonWriterOptions
            {
                SkipValidation = skipValidation,
            };

            using var writer = new Utf8JsonWriter(buffer, options);
            writer.WriteStartObject();

            if (skipValidation)
            {
                foreach (JsonElement val in root.EnumerateArray())
                {
                    val.WriteTo(writer);
                }

                writer.WriteEndObject();
                writer.Flush();

                JsonTestHelper.AssertContents(
                    "{null,false,true,\"hi\",5,{},[]}",
                    buffer);
            }
            else
            {
                foreach (JsonElement val in root.EnumerateArray())
                {
                    Assert.ThrowsExactly<InvalidOperationException>(() => val.WriteTo(writer));
                }

                writer.WriteEndObject();
                writer.Flush();

                JsonTestHelper.AssertContents("{}", buffer);
            }
        }
    }

    protected override ParsedJsonDocument<JsonElement> PrepareDocument(string jsonIn)
    {
        return ParsedJsonDocument<JsonElement>.Parse($" [  {jsonIn}  ]", s_options);
    }

    protected override void WriteDocument(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer)
    {
        document.RootElement.WriteTo(writer);
    }

    protected override void WriteSingleValue(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer)
    {
        document.RootElement[0].WriteTo(writer);
    }
}

[TestClass]
public sealed class ParsedJsonDocumentWriteTests : JsonDomWriteTests
{
    [TestMethod]
    public void CheckByPassingNullWriter()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("true", default))
        {
            AssertEx.ThrowsExactly<ArgumentNullException>("writer", () => doc.WriteTo(null));
        }
    }

    protected override ParsedJsonDocument<JsonElement> PrepareDocument(string jsonIn)
    {
        var jsonDocument = ParsedJsonDocument<JsonElement>.Parse(jsonIn, s_options);
        return jsonDocument;
    }

    protected override void WriteDocument(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer)
    {
        document.WriteTo(writer);
    }

    protected override void WriteSingleValue(ParsedJsonDocument<JsonElement> document, Utf8JsonWriter writer)
    {
        document.WriteTo(writer);
    }
}
