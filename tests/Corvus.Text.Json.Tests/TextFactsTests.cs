// <copyright file="TextFactsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// The facts a metadata row records about its text (whether a number is an integer literal, whether a string is all
/// ASCII): recorded by the parser and by every mutation, and never wrong.
/// </summary>
[TestClass]
public class TextFactsTests
{
    // Whether text is recorded as all ASCII on this target. The scan that finds it is the vector scan of .NET 8 and
    // later. On .NET Framework and .NET Standard the row says nothing of a string's text, which is always allowed:
    // a reader of the fact then looks at the text itself.
#if NET
    private const bool AsciiIsRecorded = true;
#else
    private const bool AsciiIsRecorded = false;
#endif

    private const int IntegerLiteral = 1;
    private const int FractionOrExponent = 2;

    [TestMethod]
    [DataRow("0", IntegerLiteral)]
    [DataRow("-12", IntegerLiteral)]
    [DataRow("123456789012345678901234567890", IntegerLiteral)]
    [DataRow("1.0", FractionOrExponent)]
    [DataRow("-0.5", FractionOrExponent)]
    [DataRow("1e3", FractionOrExponent)]
    [DataRow("1E-3", FractionOrExponent)]
    [DataRow("1.5e+3", FractionOrExponent)]
    public void TheParserRecordsANumbersShape(string number, int shape)
    {
        using ParsedJsonDocument<JsonElement> root = ParsedJsonDocument<JsonElement>.Parse(number);
        Assert.AreEqual(shape, Shape(root.RootElement));

        using ParsedJsonDocument<JsonElement> nested = ParsedJsonDocument<JsonElement>.Parse("{\"a\": [" + number + ", " + number + "], \"b\": " + number + "}");
        Assert.AreEqual(shape, Shape(nested.RootElement.GetProperty("a")[1]));
        Assert.AreEqual(shape, Shape(nested.RootElement.GetProperty("b")));
    }

    [TestMethod]
    [DataRow("", true)]
    [DataRow("plain ascii text, long enough to be scanned by vectors: 0123456789 0123456789 0123456789", true)]
    [DataRow("caf\u00e9", false)]
    [DataRow("ascii then \u00e9 then ascii, long enough to be scanned by vectors: 0123456789 0123456789", false)]
    [DataRow("\u00e9", false)]
    public void TheParserRecordsWhetherAStringIsAscii(string text, bool ascii)
    {
        string json = "{\"" + text + "\": \"" + text + "\", \"other\": [\"" + text + "\"]}";
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));
        Assert.AreEqual(ascii && AsciiIsRecorded, IsAscii(document.RootElement.GetProperty(text)));
        Assert.AreEqual(ascii && AsciiIsRecorded, IsAscii(document.RootElement.GetProperty("other")[0]));
        Assert.AreEqual(text, document.RootElement.GetProperty(text).GetString());
        Assert.AreEqual(text, document.RootElement.GetProperty("other")[0].GetString());
    }

    [TestMethod]
    public void AnEscapedStringIsNotRecordedAsAscii()
    {
        // The text after the first escape is not scanned; the row says nothing, and readers decode it as before.
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse("[\"a\\nb\", \"caf\\u00e9\"]"u8.ToArray());
        Assert.IsFalse(IsAscii(document.RootElement[0]));
        Assert.IsFalse(IsAscii(document.RootElement[1]));
        Assert.AreEqual("a\nb", document.RootElement[0].GetString());
        Assert.AreEqual("caf\u00e9", document.RootElement[1].GetString());
    }

    [TestMethod]
    public void MutationsRecordTheSameFacts()
    {
        using var workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> parsed = ParsedJsonDocument<JsonElement>.Parse("{}");
        using JsonDocumentBuilder<JsonElement.Mutable> builder = parsed.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builder.RootElement;
        root.SetProperty("int", 42);
        root.SetProperty("long", 1234567890123L);
        root.SetProperty("double", 1.5);
        root.SetProperty("wholeDouble", 3.0);
        root.SetProperty("decimal", 2.25m);
        root.SetProperty("ascii", "plain text");
        root.SetProperty("accented", "caf\u00e9");

        Assert.AreEqual(IntegerLiteral, Shape(root.GetProperty("int")));
        Assert.AreEqual(IntegerLiteral, Shape(root.GetProperty("long")));
        Assert.AreEqual(FractionOrExponent, Shape(root.GetProperty("double")));
        Assert.AreEqual(FractionOrExponent, Shape(root.GetProperty("decimal")));

        // A whole double is written as its formatter writes it: the row says what the text is, either way.
        string wholeDouble = root.GetProperty("wholeDouble").GetRawText();
        Assert.AreEqual(wholeDouble.IndexOfAny(['.', 'e', 'E']) < 0 ? IntegerLiteral : FractionOrExponent, Shape(root.GetProperty("wholeDouble")));

        Assert.AreEqual(AsciiIsRecorded, IsAscii(root.GetProperty("ascii")));
        Assert.AreEqual("plain text", root.GetProperty("ascii").GetString());

        // The row says what the stored text is: the default encoder stores an accented character escaped, as ASCII.
        Assert.AreEqual(AsciiIsRecorded && root.GetProperty("accented").GetRawText().All(c => c < 128), IsAscii(root.GetProperty("accented")));
        Assert.AreEqual("caf\u00e9", root.GetProperty("accented").GetString());
    }

#if NET
    [TestMethod]
    public void TheStringScanStopsWhereAReferenceScanStops()
    {
        // Every length around the vector sizes, with each kind of stopping byte at every position (and none).
        byte[] stops = [(byte)'"', (byte)'\\', 0x00, 0x1F, 0x80, 0xC3, 0xFF];
        for (int length = 0; length <= 100; length++)
        {
            byte[] text = new byte[length];
            for (int position = -1; position < length; position++)
            {
                foreach (byte stop in stops)
                {
                    for (int i = 0; i < length; i++)
                    {
                        text[i] = (byte)(0x20 + ((i * 7) % 0x5F));
                        if (text[i] == (byte)'"' || text[i] == (byte)'\\')
                        {
                            text[i] = (byte)'a';
                        }
                    }

                    if (position >= 0)
                    {
                        text[position] = stop;

                        // A later stop is not reached.
                        if (position + 3 < length)
                        {
                            text[position + 3] = (byte)'"';
                        }
                    }

                    Assert.AreEqual(position, JsonReaderHelper.IndexOfQuoteOrAnyControlOrBackSlashOrNonAscii(text), $"length {length}, position {position}, stop {stop}");
                }
            }
        }
    }
#endif

    [TestMethod]
    public void AStringNearTheEndOfTheTextIsScannedCorrectly()
    {
        // The last strings of a document have less than a vector of text after them.
        for (int length = 0; length <= 70; length++)
        {
            string ascii = new('x', length);
            string accented = ascii + "\u00e9";
            using ParsedJsonDocument<JsonElement> a = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("\"" + ascii + "\""));
            using ParsedJsonDocument<JsonElement> b = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes("\"" + accented + "\""));
            Assert.AreEqual(AsciiIsRecorded, IsAscii(a.RootElement));
            Assert.AreEqual(ascii, a.RootElement.GetString());
            Assert.IsFalse(IsAscii(b.RootElement));
            Assert.AreEqual(accented, b.RootElement.GetString());
        }
    }

    private static int Shape<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return ((JsonDocument)element.ParentDocument).GetNumberShape(element.ParentDocumentIndex);
    }

    private static bool IsAscii<T>(in T element)
        where T : struct, IJsonElement<T>
    {
        return ((JsonDocument)element.ParentDocument).IsAsciiText(element.ParentDocumentIndex);
    }
}