// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

public partial class Utf8JsonReaderTests
{
    [TestMethod]
    public void TestingDateTimeMaxValue()
    {
        string jsonString = @"""9999-12-31T23:59:59""";
        string expectedString = "9999-12-31T23:59:59";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTime.Parse(expectedString);

                Assert.IsTrue(json.TryGetDateTime(out DateTime actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTime());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);

        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString);
        Assert.AreEqual(DateTime.Parse(expectedString), doc.RootElement.GetDateTime());
    }

    [TestMethod]
    public void TestingDateTimeMinValue_UtcOffsetGreaterThan0()
    {
        string jsonString = @"""0001-01-01T00:00:00""";
        string expectedString = "0001-01-01T00:00:00";
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTime.Parse(expectedString);

                Assert.IsTrue(json.TryGetDateTime(out DateTime actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTime());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);

        // Test upstream
        using var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString);
        Assert.AreEqual(DateTime.Parse(expectedString), doc.RootElement.GetDateTime());
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TestingStringsConversionToDateTime(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTime.Parse(expectedString);

                Assert.IsTrue(json.TryGetDateTime(out DateTime actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTime());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TestingStringsConversionToDateTimeOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTimeOffset.Parse(expectedString);

                Assert.IsTrue(json.TryGetDateTimeOffset(out DateTimeOffset actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTimeOffset());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TestingStringsInvalidConversionToDateTime(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            Assert.IsFalse(json.TryGetDateTime(out DateTime actualDateTime));
            Assert.AreEqual(default, actualDateTime);

            try
            {
                DateTime value = json.GetDateTime();
                Assert.Fail("Expected GetDateTime to throw FormatException due to invalid ISO 8601 input.");
            }
            catch (FormatException)
            { }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TestingStringsInvalidConversionToDateTimeOffset(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                Assert.IsFalse(json.TryGetDateTimeOffset(out DateTimeOffset actualDateTime));
                Assert.AreEqual(default, actualDateTime);

                try
                {
                    DateTimeOffset value = json.GetDateTimeOffset();
                    Assert.Fail("Expected GetDateTimeOffset to throw FormatException due to invalid ISO 8601 input.");
                }
                catch (FormatException)
                { }
            }
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601TestsWithUtcOffset), typeof(JsonDateTimeTestData))]
    public void TestingStringsWithUTCOffsetToDateTime(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                Assert.IsTrue(json.TryGetDateTime(out DateTime actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTime());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601TestsWithUtcOffset), typeof(JsonDateTimeTestData))]
    public void TestingStringsWithUTCOffsetToDateTimeOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        var json = new Utf8JsonReader(dataUtf8, isFinalBlock: true, state: default);
        while (json.Read())
        {
            if (json.TokenType == JsonTokenType.String)
            {
                var expected = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

                Assert.IsTrue(json.TryGetDateTimeOffset(out DateTimeOffset actual));
                Assert.AreEqual(expected, actual);

                Assert.AreEqual(expected, json.GetDateTimeOffset());
            }
        }

        Assert.AreEqual(dataUtf8.Length, json.BytesConsumed);
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TryGetDateTime_HasValueSequence_False(string testString)
    {
        static void Test(string testString, bool isFinalBlock)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(testString);
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
            var json = new Utf8JsonReader(sequence, isFinalBlock: isFinalBlock, state: default);

            Assert.IsTrue(json.Read(), "json.Read()");
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.IsTrue(json.HasValueSequence, "json.HasValueSequence");
            // If the string is empty, the ValueSequence is empty, because it contains all 0 bytes between the two characters
            Assert.AreEqual(string.IsNullOrEmpty(testString), json.ValueSequence.IsEmpty);
            Assert.IsFalse(json.TryGetDateTime(out DateTime actual), "json.TryGetDateTime(out DateTime actual)");
            Assert.AreEqual(DateTime.MinValue, actual);

            JsonTestHelper.AssertThrows<FormatException>(ref json, (ref jsonReader) => jsonReader.GetDateTime());
        }

        Test(testString, isFinalBlock: true);
        Test(testString, isFinalBlock: false);
    }

    [TestMethod]
    [DataRow(@"""\u001c\u0001""")]
    [DataRow(@"""\u001c\u0001\u0001""")]
    public void TryGetDateTimeAndOffset_InvalidPropertyValue(string testString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(testString);
        var json = new Utf8JsonReader(dataUtf8);
        Assert.IsTrue(json.Read());

        Assert.IsFalse(json.TryGetDateTime(out DateTime dateTime));
        Assert.AreEqual(default, dateTime);
        JsonTestHelper.AssertThrows<FormatException>(ref json, (ref json) => json.GetDateTime());

        Assert.IsFalse(json.TryGetDateTimeOffset(out DateTimeOffset dateTimeOffset));
        Assert.AreEqual(default, dateTimeOffset);
        JsonTestHelper.AssertThrows<FormatException>(ref json, (ref json) => json.GetDateTimeOffset());
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void TryGetDateTimeOffset_HasValueSequence_False(string testString)
    {
        static void Test(string testString, bool isFinalBlock)
        {
            byte[] dataUtf8 = Encoding.UTF8.GetBytes(testString);
            ReadOnlySequence<byte> sequence = JsonTestHelper.GetSequence(dataUtf8, 1);
            var json = new Utf8JsonReader(sequence, isFinalBlock: isFinalBlock, state: default);

            Assert.IsTrue(json.Read(), "json.Read()");
            Assert.AreEqual(JsonTokenType.String, json.TokenType);
            Assert.IsTrue(json.HasValueSequence, "json.HasValueSequence");
            // If the string is empty, the ValueSequence is empty, because it contains all 0 bytes between the two characters
            Assert.AreEqual(string.IsNullOrEmpty(testString), json.ValueSequence.IsEmpty);
            Assert.IsFalse(json.TryGetDateTimeOffset(out DateTimeOffset actual), "json.TryGetDateTimeOffset(out DateTimeOffset actual)");
            Assert.AreEqual(DateTimeOffset.MinValue, actual);

            JsonTestHelper.AssertThrows<FormatException>(ref json, (ref jsonReader) => jsonReader.GetDateTimeOffset());
        }

        Test(testString, isFinalBlock: true);
        Test(testString, isFinalBlock: false);
    }
}
