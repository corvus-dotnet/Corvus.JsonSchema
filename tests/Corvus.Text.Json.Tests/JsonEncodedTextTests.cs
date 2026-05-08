// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using System.Text.Encodings.Web;
using System.Text.Unicode;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public partial class JsonEncodedTextTests
{
    public static IEnumerable<object[]> JsonEncodedTextStrings
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"", "" },
                new object[] { "message", "message" },
                new object[] { "mess\"age", "mess\\u0022age" },
                new object[] { "mess\\u0022age", "mess\\\\u0022age" },
                new object[] { ">>>>>", "\\u003E\\u003E\\u003E\\u003E\\u003E" },
                new object[] { "\\u003e\\u003e\\u003e\\u003e\\u003e", "\\\\u003e\\\\u003e\\\\u003e\\\\u003e\\\\u003e" },
                new object[] { "\\u003E\\u003E\\u003E\\u003E\\u003E", "\\\\u003E\\\\u003E\\\\u003E\\\\u003E\\\\u003E" },
            };
        }
    }

    public static IEnumerable<object[]> JsonEncodedTextStringsCustom
    {
        get
        {
            return new List<object[]>
            {
                new object[] {"", "" },
                new object[] { "age", "\\u0061\\u0067\\u0065" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\u00EA\u00EA\u00EA\u00EA\u00EA" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\"\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\u0022\u00EA\u00EA\u00EA\u00EA\u00EA" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u0022\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0032\\u0032\u00EA\u00EA\u00EA\u00EA\u00EA" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9>>>>>\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003E\\u003E\\u003E\\u003E\\u003E\u00EA\u00EA\u00EA\u00EA\u00EA" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003e\\u003e\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0033\\u0065\\\\\\u0075\\u0030\\u0030\\u0033\\u0065\u00EA\u00EA\u00EA\u00EA\u00EA" },
                new object[] { "\u00E9\u00E9\u00E9\u00E9\u00E9\\u003E\\u003E\u00EA\u00EA\u00EA\u00EA\u00EA", "\u00E9\u00E9\u00E9\u00E9\u00E9\\\\\\u0075\\u0030\\u0030\\u0033\\u0045\\\\\\u0075\\u0030\\u0030\\u0033\\u0045\u00EA\u00EA\u00EA\u00EA\u00EA" },
            };
        }
    }

    public static IEnumerable<object[]> UTF8ReplacementCharacterStrings
    {
        get
        {
            return new List<object[]>
            {
                new object[] { new byte[] { 34, 97, 0xc3, 0x28, 98, 34 }, "\\u0022a\\uFFFD(b\\u0022" },
                new object[] { new byte[] { 34, 97, 0xa0, 0xa1, 98, 34 }, "\\u0022a\\uFFFD\\uFFFDb\\u0022" },
                new object[] { new byte[] { 34, 97, 0xe2, 0x28, 0xa1, 98, 34 }, "\\u0022a\\uFFFD(\\uFFFDb\\u0022" },
                new object[] { new byte[] { 34, 97, 0xe2, 0x82, 0x28, 98, 34 }, "\\u0022a\\uFFFD(b\\u0022" },
                new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0xbc, 98, 34 }, "\\u0022a\\uFFFD(\\uFFFD\\uFFFDb\\u0022" },
                new object[] { new byte[] { 34, 97, 0xf0, 0x90, 0x28, 0xbc, 98, 34 }, "\\u0022a\\uFFFD(\\uFFFDb\\u0022" },
                new object[] { new byte[] { 34, 97, 0xf0, 0x28, 0x8c, 0x28, 98, 34 }, "\\u0022a\\uFFFD(\\uFFFD(b\\u0022" },
            };
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStringsCustom))]
    public void CustomEncoder(string message, string expectedMessage)
    {
        // Latin-1 Supplement block starts from U+0080 and ends at U+00FF
        var encoder = JavaScriptEncoder.Create(UnicodeRange.Create((char)0x0080, (char)0x00FF));
        var text = JsonEncodedText.Encode(message, encoder);
        var textSpan = JsonEncodedText.Encode(message.AsSpan(), encoder);
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message), encoder);

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);

        Assert.IsTrue(text.Equals(textSpan));
        Assert.IsTrue(text.Equals(textUtf8Span));
        Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
        Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStrings))]
    public void CustomEncoderCantOverrideHtml(string message, string expectedMessage)
    {
        var encoder = JavaScriptEncoder.Create(UnicodeRange.Create(' ', '}'));
        var text = JsonEncodedText.Encode(message, encoder);
        var textSpan = JsonEncodedText.Encode(message.AsSpan(), encoder);
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message), encoder);

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);

        Assert.IsTrue(text.Equals(textSpan));
        Assert.IsTrue(text.Equals(textUtf8Span));
        Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
        Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
    }

    [TestMethod]
    public void CustomEncoderClass()
    {
        const string message = "a+";
        const string expected = "a\\u002B";
        JsonEncodedText text;

        text = JsonEncodedText.Encode(message);
        Assert.AreEqual(expected, text.Value);

        text = JsonEncodedText.Encode(message, null);
        Assert.AreEqual(expected, text.Value);

        text = JsonEncodedText.Encode(message, JavaScriptEncoder.Default);
        Assert.AreEqual(expected, text.Value);

        text = JsonEncodedText.Encode(message, new CustomEncoderAllowingPlusSign());
        Assert.AreEqual("a+", text.Value);
    }

    [TestMethod]
    public void Default()
    {
        JsonEncodedText text = default;
        Assert.IsTrue(text.EncodedUtf8Bytes.IsEmpty);
        Assert.AreEqual("", text.Value);

        Assert.AreEqual(0, text.GetHashCode());
        Assert.AreEqual("", text.ToString());
        Assert.IsTrue(text.Equals(default));
        Assert.IsTrue(text.Equals(text));
        Assert.IsFalse(text.Equals(null));

        JsonEncodedText defaultText = default;
        object obj = defaultText;
        Assert.IsTrue(text.Equals(obj));
        Assert.IsTrue(text.Equals(defaultText));
        Assert.IsTrue(defaultText.Equals(text));

        var textByteEmpty = JsonEncodedText.Encode(Array.Empty<byte>());
        Assert.IsTrue(textByteEmpty.EncodedUtf8Bytes.IsEmpty);
        Assert.AreEqual("", textByteEmpty.Value);
        Assert.AreEqual("", textByteEmpty.ToString());

        Assert.AreEqual(text.Value, textByteEmpty.Value);
        Assert.IsFalse(text.Equals(textByteEmpty));

        var textCharEmpty = JsonEncodedText.Encode(Array.Empty<char>());
        Assert.IsTrue(textCharEmpty.EncodedUtf8Bytes.IsEmpty);
        Assert.AreEqual("", textCharEmpty.Value);
        Assert.AreEqual("", textCharEmpty.ToString());

        Assert.AreEqual(text.Value, textCharEmpty.Value);
        Assert.IsFalse(text.Equals(textCharEmpty));

        Assert.IsTrue(textCharEmpty.Equals(textByteEmpty));
        Assert.AreEqual(textByteEmpty.GetHashCode(), textCharEmpty.GetHashCode());
    }

    [TestMethod]
    public void EqualsObject()
    {
        string message = "message";

        var text = JsonEncodedText.Encode(message);
        object textCopy = text;
        object textDuplicate = JsonEncodedText.Encode(message);
        object textDuplicateDiffStringRef = JsonEncodedText.Encode(string.Concat("mess", "age"));
        object differentText = JsonEncodedText.Encode("message1");

        Assert.IsTrue(text.Equals(text));

        Assert.IsTrue(text.Equals(textCopy));
        Assert.IsTrue(textCopy.Equals(text));

        Assert.IsTrue(text.Equals(textDuplicate));
        Assert.IsTrue(textDuplicate.Equals(text));

        Assert.IsTrue(text.Equals(textDuplicateDiffStringRef));
        Assert.IsTrue(textDuplicateDiffStringRef.Equals(text));

        Assert.IsFalse(text.Equals(differentText));
        Assert.IsFalse(differentText.Equals(text));

        Assert.IsFalse(text.Equals(null));
    }

    [TestMethod]
    public void EqualsTest()
    {
        string message = "message";

        var text = JsonEncodedText.Encode(message);
        JsonEncodedText textCopy = text;
        var textDuplicate = JsonEncodedText.Encode(message);
        var textDuplicateDiffStringRef = JsonEncodedText.Encode(string.Concat("mess", "age"));
        var differentText = JsonEncodedText.Encode("message1");

        Assert.IsTrue(text.Equals(text));

        Assert.IsTrue(text.Equals(textCopy));
        Assert.IsTrue(textCopy.Equals(text));

        Assert.IsTrue(text.Equals(textDuplicate));
        Assert.IsTrue(textDuplicate.Equals(text));

        Assert.IsTrue(text.Equals(textDuplicateDiffStringRef));
        Assert.IsTrue(textDuplicateDiffStringRef.Equals(text));

        Assert.IsFalse(text.Equals(differentText));
        Assert.IsFalse(differentText.Equals(text));
    }

    [TestMethod]
    public void GetHashCodeTest()
    {
        string message = "message";

        var text = JsonEncodedText.Encode(message);
        JsonEncodedText textCopy = text;
        var textDuplicate = JsonEncodedText.Encode(message);
        var textDuplicateDiffStringRef = JsonEncodedText.Encode(string.Concat("mess", "age"));
        var differentText = JsonEncodedText.Encode("message1");

        int expectedHashCode = text.GetHashCode();

        Assert.AreNotEqual(0, expectedHashCode);
        Assert.AreEqual(expectedHashCode, textCopy.GetHashCode());
        Assert.AreEqual(expectedHashCode, textDuplicate.GetHashCode());
        Assert.AreEqual(expectedHashCode, textDuplicateDiffStringRef.GetHashCode());
        Assert.AreNotEqual(expectedHashCode, differentText.GetHashCode());
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(1_000)]
    [DataRow(10_000)]
    public void GetUtf8BytesLargeTest(int stringLength)
    {
        {
            string message = new('a', stringLength);
            byte[] expectedBytes = Encoding.UTF8.GetBytes(message);

            var text = JsonEncodedText.Encode(message);
            var textSpan = JsonEncodedText.Encode(message.AsSpan());
            var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

            Assert.IsTrue(text.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
            Assert.IsTrue(textSpan.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
            Assert.IsTrue(textUtf8Span.EncodedUtf8Bytes.SequenceEqual(expectedBytes));

            Assert.IsTrue(text.Equals(textSpan));
            Assert.IsTrue(text.Equals(textUtf8Span));
            Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
            Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
        }
        {
            string message = new('>', stringLength);
            var builder = new StringBuilder(stringLength);
            for (int i = 0; i < stringLength; i++)
            {
                builder.Append("\\u003E");
            }
            byte[] expectedBytes = Encoding.UTF8.GetBytes(builder.ToString());

            var text = JsonEncodedText.Encode(message);
            var textSpan = JsonEncodedText.Encode(message.AsSpan());
            var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

            Assert.IsTrue(text.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
            Assert.IsTrue(textSpan.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
            Assert.IsTrue(textUtf8Span.EncodedUtf8Bytes.SequenceEqual(expectedBytes));

            Assert.IsTrue(text.Equals(textSpan));
            Assert.IsTrue(text.Equals(textUtf8Span));
            Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
            Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStrings))]
    public void GetUtf8BytesTest(string message, string expectedMessage)
    {
        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedMessage);

        var text = JsonEncodedText.Encode(message);
        var textSpan = JsonEncodedText.Encode(message.AsSpan());
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

        Assert.IsTrue(text.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
        Assert.IsTrue(textSpan.EncodedUtf8Bytes.SequenceEqual(expectedBytes));
        Assert.IsTrue(textUtf8Span.EncodedUtf8Bytes.SequenceEqual(expectedBytes));

        Assert.IsTrue(text.Equals(textSpan));
        Assert.IsTrue(text.Equals(textUtf8Span));
        Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
        Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(1_000)]
    [DataRow(10_000)]
    public void GetValueLargeEscapedTest(int stringLength)
    {
        string message = new('>', stringLength);
        var builder = new StringBuilder(stringLength);
        for (int i = 0; i < stringLength; i++)
        {
            builder.Append("\\u003E");
        }
        string expectedMessage = builder.ToString();

        var text = JsonEncodedText.Encode(message);
        var textSpan = JsonEncodedText.Encode(message.AsSpan());
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(1_000)]
    [DataRow(10_000)]
    public void GetValueLargeTest(int stringLength)
    {
        string message = new('a', stringLength);
        string expectedMessage = new('a', stringLength);

        var text = JsonEncodedText.Encode(message);
        var textSpan = JsonEncodedText.Encode(message.AsSpan());
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStrings))]
    public void GetValueTest(string message, string expectedMessage)
    {
        var text = JsonEncodedText.Encode(message);
        var textSpan = JsonEncodedText.Encode(message.AsSpan());
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);
    }

    [TestMethod]
    public void InvalidEncode()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => JsonEncodedText.Encode((string)null));
    }

    [TestMethod]
    public void InvalidLargeEncode()
    {
        try
        {
            string largeValueString = new('a', 400_000_000);
            byte[] utf8Value = new byte[400_000_000];
            utf8Value.AsSpan().Fill((byte)'a');

            Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(largeValueString));
            Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(largeValueString.AsSpan()));
            Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(utf8Value));
        }
        catch (OutOfMemoryException)
        {
            Assert.Inconclusive("Out of memory allocating large objects"); return;
        }
    }

    [TestMethod]
    public void InvalidUTF16()
    {
        char[] invalid = new char[5] { 'a', 'b', 'c', (char)0xDC00, 'a' };
        Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(invalid));

        invalid = new char[5] { 'a', 'b', 'c', (char)0xD800, 'a' };
        Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(invalid));

        invalid = new char[5] { 'a', 'b', 'c', (char)0xDC00, (char)0xD800 };
        Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(invalid));

        char[] valid = new char[5] { 'a', 'b', 'c', (char)0xD800, (char)0xDC00 };
        var _ = JsonEncodedText.Encode(valid);

        Assert.ThrowsExactly<ArgumentException>(() => JsonEncodedText.Encode(new string(valid).Substring(0, 4)));
    }

    [TestMethod]
    public void LatinCharsSameAsDefaultEncoder()
    {
        for (int i = 0; i <= 127; i++)
        {
            var textBuiltin = JsonEncodedText.Encode(((char)i).ToString());
            var textEncoder = JsonEncodedText.Encode(((char)i).ToString(), JavaScriptEncoder.Default);

            Assert.AreEqual(textEncoder, textBuiltin);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStrings))]
    public void NullEncoder(string message, string expectedMessage)
    {
        var text = JsonEncodedText.Encode(message, null);
        var textSpan = JsonEncodedText.Encode(message.AsSpan(), null);
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message), null);

        Assert.AreEqual(expectedMessage, text.Value);
        Assert.AreEqual(expectedMessage, textSpan.Value);
        Assert.AreEqual(expectedMessage, textUtf8Span.Value);

        Assert.IsTrue(text.Equals(textSpan));
        Assert.IsTrue(text.Equals(textUtf8Span));
        Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
        Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
    }

    [TestMethod]
    [DynamicData(nameof(UTF8ReplacementCharacterStrings))]
    public void ReplacementCharacterUTF8(byte[] dataUtf8, string expected)
    {
        var text = JsonEncodedText.Encode(dataUtf8);
        Assert.AreEqual(expected, text.Value);
    }

    [TestMethod]
    [DataRow(100)]
    [DataRow(1_000)]
    [DataRow(10_000)]
    public void ToStringLargeTest(int stringLength)
    {
        {
            string message = new('a', stringLength);
            string expectedMessage = new('a', stringLength);

            var text = JsonEncodedText.Encode(message);
            var textSpan = JsonEncodedText.Encode(message.AsSpan());
            var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

            Assert.AreEqual(expectedMessage, text.ToString());
            Assert.AreEqual(expectedMessage, textSpan.ToString());
            Assert.AreEqual(expectedMessage, textUtf8Span.ToString());

            Assert.IsTrue(text.Equals(textSpan));
            Assert.IsTrue(text.Equals(textUtf8Span));
            Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
            Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
        }
        {
            string message = new('>', stringLength);
            var builder = new StringBuilder(stringLength);
            for (int i = 0; i < stringLength; i++)
            {
                builder.Append("\\u003E");
            }
            string expectedMessage = builder.ToString();

            var text = JsonEncodedText.Encode(message);
            var textSpan = JsonEncodedText.Encode(message.AsSpan());
            var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

            Assert.AreEqual(expectedMessage, text.ToString());
            Assert.AreEqual(expectedMessage, textSpan.ToString());
            Assert.AreEqual(expectedMessage, textUtf8Span.ToString());

            Assert.IsTrue(text.Equals(textSpan));
            Assert.IsTrue(text.Equals(textUtf8Span));
            Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
            Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonEncodedTextStrings))]
    public void ToStringTest(string message, string expectedMessage)
    {
        var text = JsonEncodedText.Encode(message);
        var textSpan = JsonEncodedText.Encode(message.AsSpan());
        var textUtf8Span = JsonEncodedText.Encode(Encoding.UTF8.GetBytes(message));

        Assert.AreEqual(expectedMessage, text.ToString());
        Assert.AreEqual(expectedMessage, textSpan.ToString());
        Assert.AreEqual(expectedMessage, textUtf8Span.ToString());

        Assert.IsTrue(text.Equals(textSpan));
        Assert.IsTrue(text.Equals(textUtf8Span));
        Assert.AreEqual(text.GetHashCode(), textSpan.GetHashCode());
        Assert.AreEqual(text.GetHashCode(), textUtf8Span.GetHashCode());
    }

    /// <summary>
    /// This is not a recommended way to customize the escaping, but is present here for test purposes.
    /// </summary>
    public sealed class CustomEncoderAllowingPlusSign : JavaScriptEncoder
    {
        public override int MaxOutputCharactersPerInputCharacter
        {
            get
            {
                return Default.MaxOutputCharactersPerInputCharacter;
            }
        }

        public override unsafe int FindFirstCharacterToEncode(char* text, int textLength)
        {
            return Default.FindFirstCharacterToEncode(text, textLength);
        }

        public override unsafe bool TryEncodeUnicodeScalar(int unicodeScalar, char* buffer, int bufferLength, out int numberOfCharactersWritten)
        {
            return Default.TryEncodeUnicodeScalar(unicodeScalar, buffer, bufferLength, out numberOfCharactersWritten);
        }

        public override bool WillEncode(int unicodeScalar)
        {
            if (unicodeScalar == '+')
            {
                return false;
            }

            return Default.WillEncode(unicodeScalar);
        }
    }
}
