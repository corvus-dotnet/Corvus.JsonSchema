// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Corvus.Text.Json.Internal;
using Newtonsoft.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

public partial class Utf8JsonReaderTests
{
    [TestMethod]
    [DataRow("null")]
    [DataRow("false")]
    [DataRow("true")]
    [DataRow("42")]
    [DataRow("[]")]
    [DataRow("{}")]
    [DataRow("/* comment */ null", JsonCommentHandling.Allow)]
    public void CopyString_InvalidToken_ThrowsInvalidOperationException(string json, JsonCommentHandling commentHandling = default)
    {
        var options = new JsonReaderOptions { CommentHandling = commentHandling };
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test, options);

        static void Test(ref Utf8JsonReader reader)
        {
            do
            {
                JsonTestHelper.AssertThrows<InvalidOperationException>(ref reader, (ref reader) => reader.CopyString(new byte[128]));
                JsonTestHelper.AssertThrows<InvalidOperationException>(ref reader, (ref reader) => reader.CopyString(new char[128]));
            }
            while (reader.Read());

            JsonTestHelper.AssertThrows<InvalidOperationException>(ref reader, (ref reader) => reader.CopyString(new byte[128]));
            JsonTestHelper.AssertThrows<InvalidOperationException>(ref reader, (ref reader) => reader.CopyString(new char[128]));
        }
    }

    [TestMethod]
    [DataRow("this is a string without escaping", "this is a string without escaping")]
    [DataRow(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
    [DataRow(@"\n\r\""\\\/\t\b\f\u0061", "\n\r\"\\/\t\b\fa")]
    public void CopyString_Utf16_DestinationTooShort_ThrowsArgumentException(string jsonString, string expectedOutput)
    {
        int expectedSize = expectedOutput.Length;
        string json = $"\"{jsonString}\"";
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());

            char[] buffer = new char[expectedSize];
            for (int i = 0; i < expectedSize; i++)
            {
                JsonTestHelper.AssertThrows<ArgumentException>(ref reader, (ref reader) => reader.CopyString(buffer.AsSpan(0, i)));
                AssertEx.All(buffer, static c => Assert.AreEqual(0, c));
            }

            Assert.AreEqual(expectedSize, reader.CopyString(buffer));
        }
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("1", "1")]
    [DataRow("this is a string without escaping", "this is a string without escaping")]
    [DataRow(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
    [DataRow(@"\n\r\""\\\/\t\b\f\u0061", "\n\r\"\\/\t\b\fa")]
    public void CopyString_Utf16_SuccessPath(string jsonString, string expectedOutput)
    {
        string json = $"\"{jsonString}\"";
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Span<char> destination = new char[128];
            int charsWritten = reader.CopyString(destination);
            Assert.AreEqual(expectedOutput.Length, charsWritten);
            Assert.IsTrue(expectedOutput.AsSpan().SequenceEqual(destination.Slice(0, charsWritten)));
        }
    }

    [TestMethod]
    [DataRow("this is a string without escaping", "this is a string without escaping")]
    [DataRow(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
    [DataRow(@"\n\r\""\\\/\t\b\f\u0061", "\n\r\"\\/\t\b\fa")]
    public void CopyString_Utf8_DestinationTooShort_ThrowsArgumentException(string jsonString, string expectedOutput)
    {
        int expectedUtf8Size = Encoding.UTF8.GetByteCount(expectedOutput);
        string json = $"\"{jsonString}\"";
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());

            byte[] buffer = new byte[expectedUtf8Size];
            for (int i = 0; i < expectedUtf8Size; i++)
            {
                JsonTestHelper.AssertThrows<ArgumentException>(ref reader, (ref reader) => reader.CopyString(buffer.AsSpan(0, i)));
                AssertEx.All(buffer, static b => Assert.AreEqual(0, b));
            }

            Assert.AreEqual(expectedUtf8Size, reader.CopyString(buffer));
        }
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("1", "1")]
    [DataRow("this is a string without escaping", "this is a string without escaping")]
    [DataRow(@"this is\t a string with escaping\n", "this is\t a string with escaping\n")]
    [DataRow(@"\n\r\""\\\/\t\b\f\u0061", "\n\r\"\\/\t\b\fa")]
    public void CopyString_Utf8_SuccessPath(string jsonString, string expectedOutput)
    {
        string json = $"\"{jsonString}\"";
        byte[] expectedOutputUtf8 = Encoding.UTF8.GetBytes(expectedOutput);
        JsonTestHelper.AssertWithSingleAndMultiSegmentReader(json, Test);

        void Test(ref Utf8JsonReader reader)
        {
            Assert.IsTrue(reader.Read());
            Span<byte> destination = new byte[128];
            int bytesWritten = reader.CopyString(destination);
            Assert.AreEqual(expectedOutputUtf8.Length, bytesWritten);
            Assert.IsTrue(expectedOutputUtf8.AsSpan().SequenceEqual(destination.Slice(0, bytesWritten)));
        }
    }

    [TestMethod]
    public void GetBase64Unescapes()
    {
        string jsonString = "\"\\u0031234\""; // equivalent to "\"1234\""

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        Assert.IsTrue(json.Read());

        byte[] expected = Convert.FromBase64String("1234"); // new byte[3] { 215, 109, 248 }

        byte[] value = json.GetBytesFromBase64();
        CollectionAssert.AreEqual(expected, value);
        Assert.IsTrue(json.TryGetBytesFromBase64(out value));
        CollectionAssert.AreEqual(expected, value);
    }

    [TestMethod]
    [DynamicData(nameof(JsonBase64TestData.InvalidBase64Tests), typeof(JsonBase64TestData))]
    public void InvalidBase64(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        Assert.IsTrue(json.Read());
        Assert.IsFalse(json.TryGetBytesFromBase64(out byte[] value));
        Assert.IsNull(value);

        try
        {
            byte[] val = json.GetBytesFromBase64();
            Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
        }
        catch (FormatException) { }
    }

    [TestMethod]
    public void InvalidConversion()
    {
        string jsonString = "[\"stringValue\", true, /* Comment within */ 1234, null] // Comment outside";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        while (json.Read())
        {
            if (json.TokenType != JsonTokenType.String)
            {
                if (json.TokenType == JsonTokenType.Null)
                {
                    Assert.IsNull(json.GetString());
                }
                else
                {
                    JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetString());
                }

                try
                {
                    byte[] value = json.GetBytesFromBase64();
                    Assert.Fail("Expected GetBytesFromBase64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetBytesFromBase64(out byte[] value);
                    Assert.Fail("Expected TryGetBytesFromBase64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    DateTime value = json.GetDateTime();
                    Assert.Fail("Expected GetDateTime to throw InvalidOperationException due to mismatched token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetDateTime(out DateTime value);
                    Assert.Fail("Expected TryGetDateTime to throw InvalidOperationException due to mismatched token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    DateTimeOffset value = json.GetDateTimeOffset();
                    Assert.Fail("Expected GetDateTimeOffset to throw InvalidOperationException due to mismatched token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetDateTimeOffset(out DateTimeOffset value);
                    Assert.Fail("Expected TryGetDateTimeOffset to throw InvalidOperationException due to mismatched token type.");
                }
                catch (InvalidOperationException)
                { }

                JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.GetGuid());

                JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref jsonReader) => jsonReader.TryGetGuid(out _));
            }

            if (json.TokenType != JsonTokenType.Comment)
            {
                try
                {
                    string value = json.GetComment();
                    Assert.Fail("Expected GetComment to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }
            }

            if (json.TokenType != JsonTokenType.True && json.TokenType != JsonTokenType.False)
            {
                try
                {
                    bool value = json.GetBoolean();
                    Assert.Fail("Expected GetBoolean to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }
            }

            if (json.TokenType != JsonTokenType.Number)
            {
                try
                {
                    json.TryGetByte(out byte value);
                    Assert.Fail("Expected TryGetByte to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetByte();
                    Assert.Fail("Expected GetByte to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetSByte(out sbyte value);
                    Assert.Fail("Expected TryGetSByte to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetSByte();
                    Assert.Fail("Expected GetSByte to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetInt16(out short value);
                    Assert.Fail("Expected TryGetInt16 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetInt16();
                    Assert.Fail("Expected GetInt16 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetInt32(out int value);
                    Assert.Fail("Expected TryGetInt32 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetInt32();
                    Assert.Fail("Expected GetInt32 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetInt64(out long value);
                    Assert.Fail("Expected TryGetInt64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetInt64();
                    Assert.Fail("Expected GetInt64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetUInt16(out ushort value);
                    Assert.Fail("Expected TryGetUInt16 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetUInt16();
                    Assert.Fail("Expected GetUInt16 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetUInt32(out uint value);
                    Assert.Fail("Expected TryGetUInt32 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetUInt32();
                    Assert.Fail("Expected GetUInt32 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetUInt64(out ulong value);
                    Assert.Fail("Expected TryGetUInt64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetUInt64();
                    Assert.Fail("Expected GetUInt64 to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetSingle(out float value);
                    Assert.Fail("Expected TryGetSingle to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetSingle();
                    Assert.Fail("Expected GetSingle to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetDouble(out double value);
                    Assert.Fail("Expected TryGetDouble to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetDouble();
                    Assert.Fail("Expected GetDouble to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.TryGetDecimal(out decimal value);
                    Assert.Fail("Expected TryGetDecimal to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }

                try
                {
                    json.GetDecimal();
                    Assert.Fail("Expected GetDecimal to throw InvalidOperationException due to mismatch token type.");
                }
                catch (InvalidOperationException)
                { }
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(GetCommentUnescapeData))]
    public void TestGetCommentUnescape(string jsonData, string expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);
        bool commentFound = false;
        while (reader.Read())
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.Comment:
                    commentFound = true;
                    string comment = reader.GetComment();
                    Assert.AreEqual(expected, comment);
                    Assert.AreNotEqual(Regex.Unescape(expected), comment);
                    break;

                default:
                    Assert.Fail();
                    break;
            }
        }
        Assert.IsTrue(commentFound);
    }

    [TestMethod]
    [DataRow("\"a\\uDD1E\"")]
    [DataRow("\"a\\uDD1Eb\"")]
    [DataRow("\"a\\uD834\"")]
    [DataRow("\"a\\uD834\\u0030\"")]
    [DataRow("\"a\\uD834\\uD834\"")]
    [DataRow("\"a\\uD834b\"")]
    [DataRow("\"a\\uDD1E\\uD834b\"")]
    [DataRow("\"a\\\\uD834\\uDD1Eb\"")]
    [DataRow("\"a\\uDD1E\\\\uD834b\"")]
    public void TestingGetBase64InvalidUTF16(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            try
            {
                byte[] val = json.GetBytesFromBase64();
                Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
            }
            catch (InvalidOperationException) { }

            try
            {
                json.TryGetBytesFromBase64(out byte[] val);
                Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-16 JSON text.");
            }
            catch (InvalidOperationException) { }
        }
    }

    [TestMethod]
    [DynamicData(nameof(InvalidUTF8Strings))]
    public void TestingGetBase64InvalidUTF8(byte[] dataUtf8)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            // It is expected that the Utf8JsonReader won't throw an exception here
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.String, json.TokenType);

            while (json.Read())
                ;

            json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            while (json.Read())
            {
                if (json.TokenType == JsonTokenType.String)
                {
                    try
                    {
                        byte[] val = json.GetBytesFromBase64();
                        Assert.Fail("Expected InvalidOperationException when trying to decode base 64 string for invalid UTF-8 JSON text.");
                    }
                    catch (FormatException) { }

                    Assert.IsFalse(json.TryGetBytesFromBase64(out byte[] value));
                    Assert.IsNull(value);
                }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(GetCommentTestData))]
    public void TestingGetComment(string jsonData, string expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonData);
        var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = JsonCommentHandling.Allow });
        var reader = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.Comment, reader.TokenType);
        Assert.AreEqual(expected, reader.GetComment());

        Assert.IsTrue(reader.Read());
        Assert.AreEqual(JsonTokenType.EndObject, reader.TokenType);

        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow("{\"message\":\"Hello, I am \\\"Ahson!\\\"\"}")]
    [DataRow("{\"nam\\\"e\":\"ah\\\"son\"}")]
    [DataRow("{\"Here is a string: \\\"\\\"\":\"Here is a\",\"Here is a back slash\\\\\":[\"Multiline\\r\\n String\\r\\n\",\"\\tMul\\r\\ntiline String\",\"\\\"somequote\\\"\\tMu\\\"\\\"l\\r\\ntiline\\\"another\\\" String\\\\\"],\"str\":\"\\\"\\\"\"}")]
    [DataRow("[\"\\u0030\\u0031\\u0032\\u0033\\u0034\\u0035\", \"\\u0000\\u002B\", \"a\\u005C\\u0072b\", \"a\\\\u005C\\u0072b\", \"a\\u008E\\u008Fb\", \"a\\uD803\\uDE6Db\", \"a\\uD834\\uDD1Eb\", \"a\\\\uD834\\\\uDD1Eb\"]")]
    [DataRow("{\"message\":\"Hello /a/b/c \\/ \\r\\b\\n\\f\\t\\/\"}")]
    [DataRow(null)]  // Large randomly generated string
    public void TestingGetString(string jsonString)
    {
        if (jsonString == null)
        {
            var random = new Random(42);
            char[] charArray = new char[500];
            charArray[0] = '"';
            for (int i = 1; i < charArray.Length; i++)
            {
                charArray[i] = (char)random.Next('?', '\\'); // ASCII values (between 63 and 91) that don't need to be escaped.
            }

            charArray[256] = '\\';
            charArray[257] = '"';
            charArray[charArray.Length - 1] = '"';
            jsonString = new string(charArray);
        }

        var expectedPropertyNames = new List<string>();
        var expectedValues = new List<string>();

        var jsonNewtonsoft = new JsonTextReader(new StringReader(jsonString)) { MaxDepth = null };
        while (jsonNewtonsoft.Read())
        {
            if (jsonNewtonsoft.TokenType == JsonToken.String)
            {
                expectedValues.Add(jsonNewtonsoft.Value.ToString());
            }
            else if (jsonNewtonsoft.TokenType == JsonToken.PropertyName)
            {
                expectedPropertyNames.Add(jsonNewtonsoft.Value.ToString());
            }
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var actualPropertyNames = new List<string>();
        var actualValues = new List<string>();

        var json = new Utf8JsonReader(dataUtf8, true, default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                actualValues.Add(json.GetString());
            }
            else if (json.TokenType == JsonTokenType.PropertyName)
            {
                actualPropertyNames.Add(json.GetString());
            }
        }

        Assert.AreEqual(expectedPropertyNames.Count, actualPropertyNames.Count);
        for (int i = 0; i < expectedPropertyNames.Count; i++)
        {
            Assert.AreEqual(expectedPropertyNames[i], actualPropertyNames[i]);
        }

        Assert.AreEqual(expectedValues.Count, actualValues.Count);
        for (int i = 0; i < expectedValues.Count; i++)
        {
            Assert.AreEqual(expectedValues[i], actualValues[i]);
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("\"a\\uDD1E\"")]
    [DataRow("\"a\\uDD1Eb\"")]
    [DataRow("\"a\\uD834\"")]
    [DataRow("\"a\\uD834\\u0030\"")]
    [DataRow("\"a\\uD834\\uD834\"")]
    [DataRow("\"a\\uD834b\"")]
    [DataRow("\"a\\uDD1E\\uD834b\"")]
    [DataRow("\"a\\\\uD834\\uDD1Eb\"")]
    [DataRow("\"a\\uDD1E\\\\uD834b\"")]
    public void TestingGetStringInvalidUTF16(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.String, json.TokenType);

            JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.GetString());
            JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.CopyString(new byte[6 * jsonString.Length]));
            JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.CopyString(new char[6 * jsonString.Length]));
        }
    }

    [TestMethod]
    [DynamicData(nameof(InvalidUTF8Strings))]
    public void TestingGetStringInvalidUTF8(byte[] dataUtf8)
    {
        foreach (JsonCommentHandling commentHandling in Enum.GetValues(typeof(JsonCommentHandling)))
        {
            var state = new JsonReaderState(options: new JsonReaderOptions { CommentHandling = commentHandling });
            var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            // It is expected that the Utf8JsonReader won't throw an exception here
            Assert.IsTrue(json.Read());
            Assert.AreEqual(JsonTokenType.String, json.TokenType);

            while (json.Read())
                ;

            json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state);

            while (json.Read())
            {
                if (json.TokenType == JsonTokenType.String)
                {
                    int length = json.HasValueSequence ? (int)json.ValueSequence.Length : json.ValueSpan.Length;

                    InvalidOperationException ex = JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.GetString());
                    if (ex.InnerException is not null)
                    {
                        Assert.IsInstanceOfType<DecoderFallbackException>(ex.InnerException);
                    }

                    ex = JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.CopyString(new byte[length]));
                    if (ex.InnerException is not null)
                    {
                        Assert.IsInstanceOfType<DecoderFallbackException>(ex.InnerException);
                    }

                    ex = JsonTestHelper.AssertThrows<InvalidOperationException>(ref json, (ref json) => json.CopyString(new char[length]));
                    if (ex.InnerException is not null)
                    {
                        Assert.IsInstanceOfType<DecoderFallbackException>(ex.InnerException);
                    }
                }
            }
        }
    }

    [TestMethod]
    public void TestingNumbers_GetMethods()
    {
        byte[] dataUtf8 = JsonNumberTestData.JsonData;
        List<byte> bytes = JsonNumberTestData.Bytes;
        List<sbyte> sbytes = JsonNumberTestData.SBytes;
        List<short> shorts = JsonNumberTestData.Shorts;
        List<int> ints = JsonNumberTestData.Ints;
        List<long> longs = JsonNumberTestData.Longs;
        List<ushort> ushorts = JsonNumberTestData.UShorts;
        List<uint> uints = JsonNumberTestData.UInts;
        List<ulong> ulongs = JsonNumberTestData.ULongs;
        List<float> floats = JsonNumberTestData.Floats;
        List<double> doubles = JsonNumberTestData.Doubles;
        List<decimal> decimals = JsonNumberTestData.Decimals;

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        string key = "";
        int count = 0;
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.PropertyName)
            {
                key = json.GetString();
            }
            if (json.TokenType == JsonTokenType.Number)
            {
                if (key.StartsWith("byte"))
                {
                    if (count >= bytes.Count)
                        count = 0;
                    Assert.AreEqual(bytes[count], json.GetByte());
                    count++;
                }
                else if (key.StartsWith("sbyte"))
                {
                    if (count >= sbytes.Count)
                        count = 0;
                    Assert.AreEqual(sbytes[count], json.GetSByte());
                    count++;
                }
                else if (key.StartsWith("short"))
                {
                    if (count >= shorts.Count)
                        count = 0;
                    Assert.AreEqual(shorts[count], json.GetInt16());
                    count++;
                }
                else if (key.StartsWith("int"))
                {
                    if (count >= ints.Count)
                        count = 0;
                    Assert.AreEqual(ints[count], json.GetInt32());
                    count++;
                }
                else if (key.StartsWith("long"))
                {
                    if (count >= longs.Count)
                        count = 0;
                    Assert.AreEqual(longs[count], json.GetInt64());
                    count++;
                }
                else if (key.StartsWith("ushort"))
                {
                    if (count >= ushorts.Count)
                        count = 0;
                    Assert.AreEqual(ushorts[count], json.GetUInt16());
                    count++;
                }
                else if (key.StartsWith("uint"))
                {
                    if (count >= uints.Count)
                        count = 0;
                    Assert.AreEqual(uints[count], json.GetUInt32());
                    count++;
                }
                else if (key.StartsWith("ulong"))
                {
                    if (count >= ulongs.Count)
                        count = 0;
                    Assert.AreEqual(ulongs[count], json.GetUInt64());
                    count++;
                }
                else if (key.StartsWith("float"))
                {
                    if (count >= floats.Count)
                        count = 0;

                    string roundTripActual = json.GetSingle().ToString(JsonTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                    float actual = float.Parse(roundTripActual, CultureInfo.InvariantCulture);

                    string roundTripExpected = floats[count].ToString(JsonTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                    float expected = float.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                    Assert.AreEqual(expected, actual);
                    count++;
                }
                else if (key.StartsWith("double"))
                {
                    if (count >= doubles.Count)
                        count = 0;

                    string roundTripActual = json.GetDouble().ToString(JsonTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                    double actual = double.Parse(roundTripActual, CultureInfo.InvariantCulture);

                    string roundTripExpected = doubles[count].ToString(JsonTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                    double expected = double.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                    Assert.AreEqual(expected, actual);
                    count++;
                }
                else if (key.StartsWith("decimal"))
                {
                    try
                    {
                        if (count >= decimals.Count)
                            count = 0;

                        string str = string.Format(CultureInfo.InvariantCulture, "{0}", decimals[count]);
                        decimal expected = decimal.Parse(str, CultureInfo.InvariantCulture);
                        Assert.AreEqual(expected, json.GetDecimal());
                        count++;
                    }
                    catch (Exception except)
                    {
                        Assert.Fail(string.Format("Unexpected exception: {0}. Message: {1}", except.Source, except.Message));
                    }
                }
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    public void TestingNumbers_TryGetMethods()
    {
        byte[] dataUtf8 = JsonNumberTestData.JsonData;
        List<byte> bytes = JsonNumberTestData.Bytes;
        List<sbyte> sbytes = JsonNumberTestData.SBytes;
        List<short> shorts = JsonNumberTestData.Shorts;
        List<int> ints = JsonNumberTestData.Ints;
        List<long> longs = JsonNumberTestData.Longs;
        List<ushort> ushorts = JsonNumberTestData.UShorts;
        List<uint> uints = JsonNumberTestData.UInts;
        List<ulong> ulongs = JsonNumberTestData.ULongs;
        List<float> floats = JsonNumberTestData.Floats;
        List<double> doubles = JsonNumberTestData.Doubles;
        List<decimal> decimals = JsonNumberTestData.Decimals;

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        string key = "";
        int count = 0;
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.PropertyName)
            {
                key = json.GetString();
            }
            if (json.TokenType == JsonTokenType.Number)
            {
                if (key.StartsWith("byte"))
                {
                    Assert.IsTrue(json.TryGetByte(out byte numberByte));
                    if (count >= bytes.Count)
                        count = 0;
                    Assert.AreEqual(bytes[count], numberByte);
                    count++;
                }
                else if (key.StartsWith("sbyte"))
                {
                    Assert.IsTrue(json.TryGetSByte(out sbyte numberSByte));
                    if (count >= sbytes.Count)
                        count = 0;
                    Assert.AreEqual(sbytes[count], numberSByte);
                    count++;
                }
                else if (key.StartsWith("short"))
                {
                    Assert.IsTrue(json.TryGetInt16(out short numberShort));
                    if (count >= shorts.Count)
                        count = 0;
                    Assert.AreEqual(shorts[count], numberShort);
                    count++;
                }
                else if (key.StartsWith("int"))
                {
                    Assert.IsTrue(json.TryGetInt32(out int numberInt));
                    if (count >= ints.Count)
                        count = 0;
                    Assert.AreEqual(ints[count], numberInt);
                    count++;
                }
                else if (key.StartsWith("long"))
                {
                    Assert.IsTrue(json.TryGetInt64(out long numberLong));
                    if (count >= longs.Count)
                        count = 0;
                    Assert.AreEqual(longs[count], numberLong);
                    count++;
                }
                else if (key.StartsWith("ushort"))
                {
                    Assert.IsTrue(json.TryGetUInt16(out ushort numberUShort));
                    if (count >= ushorts.Count)
                        count = 0;
                    Assert.AreEqual(ushorts[count], numberUShort);
                    count++;
                }
                else if (key.StartsWith("uint"))
                {
                    Assert.IsTrue(json.TryGetUInt32(out uint numberUInt));
                    if (count >= ints.Count)
                        count = 0;
                    Assert.AreEqual(uints[count], numberUInt);
                    count++;
                }
                else if (key.StartsWith("ulong"))
                {
                    Assert.IsTrue(json.TryGetUInt64(out ulong numberULong));
                    if (count >= ints.Count)
                        count = 0;
                    Assert.AreEqual(ulongs[count], numberULong);
                    count++;
                }
                else if (key.StartsWith("float"))
                {
                    Assert.IsTrue(json.TryGetSingle(out float numberFloat));
                    if (count >= floats.Count)
                        count = 0;

                    string roundTripActual = numberFloat.ToString(JsonTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                    float actual = float.Parse(roundTripActual, CultureInfo.InvariantCulture);

                    string roundTripExpected = floats[count].ToString(JsonTestHelper.SingleFormatString, CultureInfo.InvariantCulture);
                    float expected = float.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                    Assert.AreEqual(expected, actual);
                    count++;
                }
                else if (key.StartsWith("double"))
                {
                    Assert.IsTrue(json.TryGetDouble(out double numberDouble));
                    if (count >= doubles.Count)
                        count = 0;

                    string roundTripActual = numberDouble.ToString(JsonTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                    double actual = double.Parse(roundTripActual, CultureInfo.InvariantCulture);

                    string roundTripExpected = doubles[count].ToString(JsonTestHelper.DoubleFormatString, CultureInfo.InvariantCulture);
                    double expected = double.Parse(roundTripExpected, CultureInfo.InvariantCulture);

                    Assert.AreEqual(expected, actual);
                    count++;
                }
                else if (key.StartsWith("decimal"))
                {
                    Assert.IsTrue(json.TryGetDecimal(out decimal numberDecimal));
                    if (count >= decimals.Count)
                        count = 0;

                    string str = string.Format(CultureInfo.InvariantCulture, "{0}", decimals[count]);
                    decimal expected = decimal.Parse(str, CultureInfo.InvariantCulture);
                    Assert.AreEqual(expected, numberDecimal);
                    count++;
                }
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("220.1", 220.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-1", -1)] // byte.MinValue - 1
    public void TestingNumbersInvalidConversionToByte(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetByte(out byte value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetByte();
                    Assert.Fail("Expected GetByte to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("-79228162514264337593543950336", -79228162514264337593543950336d)] // decimal.MinValue - 1
    [DataRow("79228162514264337593543950336", 79228162514264337593543950336d)]  // decimal.MaxValue + 1
    public void TestingNumbersInvalidConversionToDecimal(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetDecimal(out decimal value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetDecimal();
                    Assert.Fail("Expected GetDecimal to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-32769", -32769)] // short.MinValue - 1
    public void TestingNumbersInvalidConversionToInt16(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetInt16(out short value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetInt16();
                    Assert.Fail("Expected GetInt16 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-2147483649", -2147483649)] // int.MinValue - 1
    public void TestingNumbersInvalidConversionToInt32(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetInt32(out int value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetInt32();
                    Assert.Fail("Expected GetInt32 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-9223372036854775809", -9223372036854775809d)] // long.MinValue - 1
    public void TestingNumbersInvalidConversionToInt64(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetInt64(out long value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetInt64();
                    Assert.Fail("Expected GetInt64 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("120.1", 120.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-129", -129)] // sbyte.MinValue - 1
    public void TestingNumbersInvalidConversionToSByte(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetSByte(out sbyte value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetSByte();
                    Assert.Fail("Expected GetSByte to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-1", -1)] // ushort.MinValue - 1
    public void TestingNumbersInvalidConversionToUInt16(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetUInt16(out ushort value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetUInt16();
                    Assert.Fail("Expected GetUInt16 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("12345678901", 12345678901)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-1", -1)] // uint.MinValue - 1
    public void TestingNumbersInvalidConversionToUInt32(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetUInt32(out uint value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetUInt32();
                    Assert.Fail("Expected GetUInt32 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("0.000", 0)]
    [DataRow("1e1", 10)]
    [DataRow("1.1e2", 110)]
    [DataRow("12345.1", 12345.1)]
    [DataRow("123456789012345678901", 123456789012345678901d)]
    [DataRow("-1", -1)] // ulong.MinValue - 1
    public void TestingNumbersInvalidConversionToUInt64(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                Assert.IsFalse(json.TryGetUInt64(out ulong value));
                Assert.AreEqual(default, value);
                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expected, doubleValue);

                try
                {
                    json.GetUInt64();
                    Assert.Fail("Expected GetInt64 to throw FormatException.");
                }
                catch (FormatException)
                {
                    /* Expected exception */
                }
                doubleValue = json.GetDouble();
                Assert.AreEqual(expected, doubleValue);
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.ValidGuidTests), typeof(JsonGuidTestData))]
    [DynamicData(nameof(JsonGuidTestData.ValidHexGuidTests), typeof(JsonGuidTestData))]
    public void TestingStringsConversionToGuid(string testString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

        var expected = new Guid(expectedString);

        Assert.IsTrue(json.Read(), "Read string value");
        Assert.AreEqual(JsonTokenType.String, json.TokenType);

        Assert.IsTrue(json.TryGetGuid(out Guid actual));
        Assert.AreEqual(expected, actual);
        Assert.AreEqual(expected, json.GetGuid());

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.InvalidGuidTests), typeof(JsonGuidTestData))]
    public void TestingStringsInvalidConversionToGuid(string testString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);

        Assert.IsTrue(json.Read(), "Read string value");
        Assert.AreEqual(JsonTokenType.String, json.TokenType);

        Assert.IsFalse(json.TryGetGuid(out Guid actual));
        Assert.AreEqual(default, actual);

        JsonTestHelper.AssertThrows<FormatException>(ref json, (ref jsonReader) => jsonReader.GetGuid());
    }

    [TestMethod]
    [DataRow("-2.79769313486232E+308", double.NegativeInfinity)] // double.MinValue - 1
    [DataRow("2.79769313486232E+308", double.PositiveInfinity)]  // double.MaxValue + 1
    public void TestingTooLargeDoubleConversionToInfinity(string jsonString, double expected)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
                {
                    // .NET Framework throws for overflow rather than returning Infinity
                    // This was fixed for .NET Core 3.0 in order to be IEEE 754 compliant

                    Assert.IsFalse(json.TryGetDouble(out double _));

                    try
                    {
                        json.GetDouble();
                        Assert.Fail($"Expected {nameof(FormatException)}.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                }
                else
                {
                    Assert.IsTrue(json.TryGetDouble(out double actual));
                    Assert.AreEqual(expected, actual);

                    Assert.AreEqual(expected, json.GetDouble());
                }
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DataRow("-4.402823E+38", float.NegativeInfinity, -4.402823E+38)] // float.MinValue - 1
    [DataRow("4.402823E+38", float.PositiveInfinity, 4.402823E+38)]  // float.MaxValue + 1
    public void TestingTooLargeSingleConversionToInfinity(string jsonString, float expectedFloat, double expectedDouble)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.Number)
            {
                if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
                {
                    // .NET Framework throws for overflow rather than returning Infinity
                    // This was fixed for .NET Core 3.0 in order to be IEEE 754 compliant

                    Assert.IsFalse(json.TryGetSingle(out float _));

                    try
                    {
                        json.GetSingle();
                        Assert.Fail($"Expected {nameof(FormatException)}.");
                    }
                    catch (FormatException)
                    {
                        /* Expected exception */
                    }
                }
                else
                {
                    Assert.IsTrue(json.TryGetSingle(out float floatValue));
                    Assert.AreEqual(expectedFloat, floatValue);

                    Assert.AreEqual(expectedFloat, json.GetSingle());
                }

                Assert.IsTrue(json.TryGetDouble(out double doubleValue));
                Assert.AreEqual(expectedDouble, doubleValue);
                Assert.AreEqual(expectedDouble, json.GetDouble());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.InvalidGuidTests), typeof(JsonGuidTestData))]
    public void TryGetGuid_HasValueSequence_False(string testString)
    {
        static void Test(string testString, bool isFinalBlock)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
            var json = new Utf8JsonReader(sequence, isFinalBlock: isFinalBlock, state: default);

            Assert.IsTrue(json.Read(), "json.Read()");
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.IsTrue(json.HasValueSequence, "json.HasValueSequence");
            // If the string is empty, the ValueSequence is empty, because it contains all 0 bytes between the two characters
            Assert.AreEqual(string.IsNullOrEmpty(testString), json.ValueSequence.IsEmpty);
            Assert.IsFalse(json.TryGetGuid(out Guid actual), "json.TryGetGuid(out Guid actual)");
            Assert.AreEqual(Guid.Empty, actual);

            JsonTestHelper.AssertThrows<FormatException>(ref json, (ref jsonReader) => jsonReader.GetGuid());
        }

        Test(testString, isFinalBlock: true);
        Test(testString, isFinalBlock: false);
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.ValidGuidTests), typeof(JsonGuidTestData))]
    public void TryGetGuid_HasValueSequence_RetrievesGuid(string testString, string expectedString)
    {
        static void Test(string testString, string expectedString, bool isFinalBlock)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{testString}\"");
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
            var json = new Utf8JsonReader(sequence, isFinalBlock: isFinalBlock, state: default);

            var expected = new Guid(expectedString);

            Assert.IsTrue(json.Read(), "json.Read()");
            Assert.AreEqual(JsonTokenType.String, json.TokenType);

            Assert.IsTrue(json.HasValueSequence, "json.HasValueSequence");
            Assert.IsFalse(json.ValueSequence.IsEmpty, "json.ValueSequence.IsEmpty");
            Assert.IsTrue(json.ValueSpan.IsEmpty, "json.ValueSpan.IsEmpty");
            Assert.IsTrue(json.TryGetGuid(out Guid actual), "TryGetGuid");
            Assert.AreEqual(expected, actual);
            Assert.AreEqual(expected, json.GetGuid());
        }

        Test(testString, expectedString, isFinalBlock: true);
        Test(testString, expectedString, isFinalBlock: false);
    }

    [TestMethod]
    [DynamicData(nameof(JsonBase64TestData.ValidBase64Tests), typeof(JsonBase64TestData))]
    public void ValidBase64(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        Assert.IsTrue(json.Read());

        byte[] expected = Convert.FromBase64String(jsonString.AsSpan(1, jsonString.Length - 2).ToString());

        byte[] value = json.GetBytesFromBase64();
        CollectionAssert.AreEqual(expected, value);
        Assert.IsTrue(json.TryGetBytesFromBase64(out value));
        CollectionAssert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TestNumericEdgeCases_Get_Methods()
    {
        byte[] data1 = Encoding.UTF8.GetBytes("0");
        var reader1 = new Utf8JsonReader(data1, isFinalBlock: true, state: default);
        Assert.IsTrue(reader1.Read());
        Assert.AreEqual(0, reader1.GetByte());

        byte[] data2 = Encoding.UTF8.GetBytes("255");
        var reader2 = new Utf8JsonReader(data2, isFinalBlock: true, state: default);
        Assert.IsTrue(reader2.Read());
        Assert.AreEqual(255, reader2.GetByte());

        byte[] data3 = Encoding.UTF8.GetBytes("-128");
        var reader3 = new Utf8JsonReader(data3, isFinalBlock: true, state: default);
        Assert.IsTrue(reader3.Read());
        Assert.AreEqual(-128, reader3.GetSByte());

        byte[] data4 = Encoding.UTF8.GetBytes("127");
        var reader4 = new Utf8JsonReader(data4, isFinalBlock: true, state: default);
        Assert.IsTrue(reader4.Read());
        Assert.AreEqual(127, reader4.GetSByte());

        byte[] data5 = Encoding.UTF8.GetBytes("-32768");
        var reader5 = new Utf8JsonReader(data5, isFinalBlock: true, state: default);
        Assert.IsTrue(reader5.Read());
        Assert.AreEqual(-32768, reader5.GetInt16());

        byte[] data6 = Encoding.UTF8.GetBytes("32767");
        var reader6 = new Utf8JsonReader(data6, isFinalBlock: true, state: default);
        Assert.IsTrue(reader6.Read());
        Assert.AreEqual(32767, reader6.GetInt16());

        byte[] data7 = Encoding.UTF8.GetBytes("-2147483648");
        var reader7 = new Utf8JsonReader(data7, isFinalBlock: true, state: default);
        Assert.IsTrue(reader7.Read());
        Assert.AreEqual(-2147483648, reader7.GetInt32());

        byte[] data8 = Encoding.UTF8.GetBytes("2147483647");
        var reader8 = new Utf8JsonReader(data8, isFinalBlock: true, state: default);
        Assert.IsTrue(reader8.Read());
        Assert.AreEqual(2147483647, reader8.GetInt32());

        byte[] data9 = Encoding.UTF8.GetBytes("-9223372036854775808");
        var reader9 = new Utf8JsonReader(data9, isFinalBlock: true, state: default);
        Assert.IsTrue(reader9.Read());
        Assert.AreEqual(-9223372036854775808, reader9.GetInt64());

        byte[] data10 = Encoding.UTF8.GetBytes("9223372036854775807");
        var reader10 = new Utf8JsonReader(data10, isFinalBlock: true, state: default);
        Assert.IsTrue(reader10.Read());
        Assert.AreEqual(9223372036854775807, reader10.GetInt64());

        byte[] data11 = Encoding.UTF8.GetBytes("0");
        var reader11 = new Utf8JsonReader(data11, isFinalBlock: true, state: default);
        Assert.IsTrue(reader11.Read());
        Assert.AreEqual(0, reader11.GetUInt16());

        byte[] data12 = Encoding.UTF8.GetBytes("65535");
        var reader12 = new Utf8JsonReader(data12, isFinalBlock: true, state: default);
        Assert.IsTrue(reader12.Read());
        Assert.AreEqual(65535, reader12.GetUInt16());

        byte[] data13 = Encoding.UTF8.GetBytes("0");
        var reader13 = new Utf8JsonReader(data13, isFinalBlock: true, state: default);
        Assert.IsTrue(reader13.Read());
        Assert.AreEqual(0u, reader13.GetUInt32());

        byte[] data14 = Encoding.UTF8.GetBytes("4294967295");
        var reader14 = new Utf8JsonReader(data14, isFinalBlock: true, state: default);
        Assert.IsTrue(reader14.Read());
        Assert.AreEqual(4294967295u, reader14.GetUInt32());

        byte[] data15 = Encoding.UTF8.GetBytes("0");
        var reader15 = new Utf8JsonReader(data15, isFinalBlock: true, state: default);
        Assert.IsTrue(reader15.Read());
        Assert.AreEqual(0ul, reader15.GetUInt64());

        byte[] data16 = Encoding.UTF8.GetBytes("18446744073709551615");
        var reader16 = new Utf8JsonReader(data16, isFinalBlock: true, state: default);
        Assert.IsTrue(reader16.Read());
        Assert.AreEqual(18446744073709551615ul, reader16.GetUInt64());

        byte[] data17 = Encoding.UTF8.GetBytes("0");
        var reader17 = new Utf8JsonReader(data17, isFinalBlock: true, state: default);
        Assert.IsTrue(reader17.Read());
        Assert.AreEqual(0m, reader17.GetDecimal());
    }

    [TestMethod]
    public void TestFloatingPointEdgeCases_Get_Methods()
    {
        byte[] data1 = Encoding.UTF8.GetBytes("0.0");
        var reader1 = new Utf8JsonReader(data1, isFinalBlock: true, state: default);
        Assert.IsTrue(reader1.Read());
        Assert.AreEqual(0.0f, reader1.GetSingle());

        byte[] data2 = Encoding.UTF8.GetBytes("3.4028235E+38");
        var reader2 = new Utf8JsonReader(data2, isFinalBlock: true, state: default);
        Assert.IsTrue(reader2.Read());
        float result2 = reader2.GetSingle();
        Assert.IsTrue(Math.Abs(result2 - 3.4028235E+38f) < 1E+31f);

        byte[] data3 = Encoding.UTF8.GetBytes("-3.4028235E+38");
        var reader3 = new Utf8JsonReader(data3, isFinalBlock: true, state: default);
        Assert.IsTrue(reader3.Read());
        float result3 = reader3.GetSingle();
        Assert.IsTrue(Math.Abs(result3 - (-3.4028235E+38f)) < 1E+31f);

        byte[] data4 = Encoding.UTF8.GetBytes("0.0");
        var reader4 = new Utf8JsonReader(data4, isFinalBlock: true, state: default);
        Assert.IsTrue(reader4.Read());
        Assert.AreEqual(0.0, reader4.GetDouble());

        byte[] data5 = Encoding.UTF8.GetBytes("1.7976931348623157E+308");
        var reader5 = new Utf8JsonReader(data5, isFinalBlock: true, state: default);
        Assert.IsTrue(reader5.Read());
        double result5 = reader5.GetDouble();
        Assert.IsTrue(Math.Abs(result5 - 1.7976931348623157E+308) < 1E+300);

        byte[] data6 = Encoding.UTF8.GetBytes("-1.7976931348623157E+308");
        var reader6 = new Utf8JsonReader(data6, isFinalBlock: true, state: default);
        Assert.IsTrue(reader6.Read());
        double result6 = reader6.GetDouble();
        Assert.IsTrue(Math.Abs(result6 - (-1.7976931348623157E+308)) < 1E+300);
    }
}
