// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Tests;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using NodaTime.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class ParsedJsonDocumentTests
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };

    private static readonly Dictionary<TestCaseType, string> s_expectedConcat =
        [];

    private static readonly Dictionary<TestCaseType, string> s_compactJson =
        [];

    public static IEnumerable<object[]> BadBOMCases { get; } =
        new object[][]
        {
            new object[] { "\u00EF" },
            new object[] { "\u00EF1" },
            new object[] { "\u00EF\u00BB" },
            new object[] { "\u00EF\u00BB1" },
            new object[] { "\u00EF\u00BB\u00BE" },
            new object[] { "\u00EF\u00BB\u00BE1" },
            new object[] { "\u00EF\u00BB\u00FB" },
            new object[] { "\u00EF\u00BB\u00FB1" },

            // Legal BOM, but no payload.
            new object[] { "\u00EF\u00BB\u00BF" },
        };

    public static IEnumerable<object[]> ReducedTestCases { get; } =
        new List<object[]>
        {
            new object[] { true, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
            new object[] { true, TestCaseType.Json40KB, SR.Json40KB},
            new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
            new object[] { false, TestCaseType.Json400KB, SR.Json400KB},
        };

    public static IEnumerable<object[]> TestCases { get; } =
        new List<object[]>
        {
            new object[] { true, TestCaseType.Basic, SR.BasicJson},
            new object[] { true, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
            new object[] { true, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
            new object[] { true, TestCaseType.DeepTree, SR.DeepTree},
            new object[] { true, TestCaseType.FullSchema1, SR.FullJsonSchema1},
            new object[] { true, TestCaseType.HelloWorld, SR.HelloWorld},
            new object[] { true, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
            new object[] { true, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
            new object[] { true, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
            new object[] { true, TestCaseType.Json400B, SR.Json400B},
            new object[] { true, TestCaseType.Json4KB, SR.Json4KB},
            new object[] { true, TestCaseType.Json40KB, SR.Json40KB},
            new object[] { true, TestCaseType.Json400KB, SR.Json400KB},

            new object[] { false, TestCaseType.Basic, SR.BasicJson},
            new object[] { false, TestCaseType.BasicLargeNum, SR.BasicJsonWithLargeNum}, // Json.NET treats numbers starting with 0 as octal (0425 becomes 277)
            new object[] { false, TestCaseType.BroadTree, SR.BroadTree}, // \r\n behavior is different between Json.NET and Corvus.Text.Json
            new object[] { false, TestCaseType.DeepTree, SR.DeepTree},
            new object[] { false, TestCaseType.FullSchema1, SR.FullJsonSchema1},
            new object[] { false, TestCaseType.HelloWorld, SR.HelloWorld},
            new object[] { false, TestCaseType.LotsOfNumbers, SR.LotsOfNumbers},
            new object[] { false, TestCaseType.LotsOfStrings, SR.LotsOfStrings},
            new object[] { false, TestCaseType.ProjectLockJson, SR.ProjectLockJson},
            new object[] { false, TestCaseType.Json400B, SR.Json400B},
            new object[] { false, TestCaseType.Json4KB, SR.Json4KB},
            new object[] { false, TestCaseType.Json40KB, SR.Json40KB},
            new object[] { false, TestCaseType.Json400KB, SR.Json400KB},
        };

    // TestCaseType is only used to give the json strings a descriptive name within the unit tests.
    public enum TestCaseType
    {
        HelloWorld,
        Basic,
        BasicLargeNum,
        ProjectLockJson,
        FullSchema1,
        DeepTree,
        BroadTree,
        LotsOfNumbers,
        LotsOfStrings,
        Json400B,
        Json4KB,
        Json40KB,
        Json400KB,
    }

    public enum ParseReaderScenario
    {
        NotStarted,
        StartAtFirstToken,
        StartAtNestedValue,
        StartAtPropertyName,
    }

    public static IEnumerable<object[]> ParseReaderSuccessfulCases { get; } = BuildParseReaderSuccessfulCases().ToList();

    private static IEnumerable<object[]> BuildParseReaderSuccessfulCases()
    {
        var scenarios = (ParseReaderScenario[])Enum.GetValues(typeof(ParseReaderScenario));

        foreach (int segmentCount in new[] { 0, 1, 2, 15, 125 })
        {
            foreach (ParseReaderScenario scenario in scenarios)
            {
                yield return new object[] { scenario, segmentCount };
            }
        }
    }

    private static string ReadHelloWorld(JToken obj)
    {
        string message = (string)obj["message"];
        return message;
    }

    private static string ReadJson400KB(JToken obj)
    {
        var sb = new StringBuilder(250000);
        foreach (JToken token in obj)
        {
            sb.Append((string)token["_id"]);
            sb.Append((int)token["index"]);
            sb.Append((string)token["guid"]);
            sb.Append((bool)token["isActive"]);
            sb.Append((string)token["balance"]);
            sb.Append((string)token["picture"]);
            sb.Append((int)token["age"]);
            sb.Append((string)token["eyeColor"]);
            sb.Append((string)token["name"]);
            sb.Append((string)token["gender"]);
            sb.Append((string)token["company"]);
            sb.Append((string)token["email"]);
            sb.Append((string)token["phone"]);
            sb.Append((string)token["address"]);
            sb.Append((string)token["about"]);
            sb.Append((string)token["registered"]);
            sb.Append((double)token["latitude"]);
            sb.Append((double)token["longitude"]);

            JToken tags = token["tags"];
            foreach (JToken tag in tags)
            {
                sb.Append((string)tag);
            }
            JToken friends = token["friends"];
            foreach (JToken friend in friends)
            {
                sb.Append((int)friend["id"]);
                sb.Append((string)friend["name"]);
            }
            sb.Append((string)token["greeting"]);
            sb.Append((string)token["favoriteFruit"]);
        }
        return sb.ToString();
    }

    private static string ReadHelloWorld(JsonElement obj)
    {
        string message = obj.GetProperty("message").GetString();
        return message;
    }

    private static string ReadJson400KB(JsonElement obj)
    {
        var sb = new StringBuilder(250000);

        foreach (JsonElement element in obj.EnumerateArray())
        {
            sb.Append(element.GetProperty("_id").GetString());
            sb.Append(element.GetProperty("index").GetInt32());
            sb.Append(element.GetProperty("guid").GetString());
            sb.Append(element.GetProperty("isActive").GetBoolean());
            sb.Append(element.GetProperty("balance").GetString());
            sb.Append(element.GetProperty("picture").GetString());
            sb.Append(element.GetProperty("age").GetInt32());
            sb.Append(element.GetProperty("eyeColor").GetString());
            sb.Append(element.GetProperty("name").GetString());
            sb.Append(element.GetProperty("gender").GetString());
            sb.Append(element.GetProperty("company").GetString());
            sb.Append(element.GetProperty("email").GetString());
            sb.Append(element.GetProperty("phone").GetString());
            sb.Append(element.GetProperty("address").GetString());
            sb.Append(element.GetProperty("about").GetString());
            sb.Append(element.GetProperty("registered").GetString());
            sb.Append(element.GetProperty("latitude").GetDouble());
            sb.Append(element.GetProperty("longitude").GetDouble());

            JsonElement tags = element.GetProperty("tags");
            for (int j = 0; j < tags.GetArrayLength(); j++)
            {
                sb.Append(tags[j].GetString());
            }
            JsonElement friends = element.GetProperty("friends");
            for (int j = 0; j < friends.GetArrayLength(); j++)
            {
                sb.Append(friends[j].GetProperty("id").GetInt32());
                sb.Append(friends[j].GetProperty("name").GetString());
            }
            sb.Append(element.GetProperty("greeting").GetString());
            sb.Append(element.GetProperty("favoriteFruit").GetString());
        }

        return sb.ToString();
    }

    [TestMethod]
    // The ReadOnlyMemory<bytes> variant is the only one that runs all the input documents.
    // The rest use a reduced set as because (implementation detail) they ultimately
    // funnel into the same worker code and the difference between Reduced and Full
    // is about 0.7 seconds (which adds up).
    //
    // If the internals change such that one of these is exercising substantially different
    // code, then it should switch to the full variation set.
    [DynamicData(nameof(TestCases))]
    public async Task ParseJson_MemoryBytes(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(bytes.AsMemory())));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_String(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            str => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(str)),
            null);
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SeekableStream(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(bytes))));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SeekableStream_Async(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(bytes)));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_UnseekableStream(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, bytes)));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_UnseekableStream_Async(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, bytes)));
    }

    [TestMethod]
    public void ParseJson_SeekableStream_Small()
    {
        byte[] data = { (byte)'1', (byte)'1' };

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(data)))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public void ParseJson_UnseekableStream_Small()
    {
        byte[] data = { (byte)'1', (byte)'1' };

        using (var doc =
            ParsedJsonDocument<JsonElement>.Parse(new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public async Task ParseJson_SeekableStream_Small_Async()
    {
        byte[] data = { (byte)'1', (byte)'1' };

        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(data)))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public async Task ParseJson_UnseekableStream_Small_Async()
    {
        byte[] data = { (byte)'1', (byte)'1' };

        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(
            new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SeekableStream_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(Utf8Bom.Concat(bytes).ToArray()))));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SeekableStream_Async_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(Utf8Bom.Concat(bytes).ToArray())));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_UnseekableStream_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, Utf8Bom.Concat(bytes).ToArray()))));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_UnseekableStream_Async_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(
                    new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, Utf8Bom.Concat(bytes).ToArray())));
    }

    [TestMethod]
    public void ParseJson_Stream_ClearRentedBuffer_WhenThrow_CodeCoverage()
    {
        using (Stream stream = new ThrowOnReadStream(new byte[] { 1 }))
        {
            Assert.ThrowsExactly<EndOfStreamException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [TestMethod]
    public async Task ParseJson_Stream_Async_ClearRentedBuffer_WhenThrow_CodeCoverage()
    {
        using (Stream stream = new ThrowOnReadStream(new byte[] { 1 }))
        {
            await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await ParsedJsonDocument<JsonElement>.ParseAsync(stream));
        }
    }

    [TestMethod]
    public void ParseJson_Stream_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream(new byte[] { 1 }))
        {
            Assert.ThrowsExactly<InsufficientMemoryException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [TestMethod]
    public async Task ParseJson_Stream_Async_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream(new byte[] { 1 }))
        {
            await Assert.ThrowsExactlyAsync<InsufficientMemoryException>(async () => await ParsedJsonDocument<JsonElement>.ParseAsync(stream));
        }
    }

    [TestMethod]
    [DynamicData(nameof(BadBOMCases))]
    public void ParseJson_SeekableStream_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(data)));
    }

    [TestMethod]
    [DynamicData(nameof(BadBOMCases))]
    public Task ParseJson_SeekableStream_Async_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        return Assert.ThrowsAsync<JsonException>(() => ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(data)));
    }

    [TestMethod]
    [DynamicData(nameof(BadBOMCases))]
    public void ParseJson_UnseekableStream_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);

        Assert.Throws<JsonException>(
            () => ParsedJsonDocument<JsonElement>.Parse(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data)));
    }

    [TestMethod]
    [DynamicData(nameof(BadBOMCases))]
    public Task ParseJson_UnseekableStream_Async_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);

        return Assert.ThrowsAsync<JsonException>(
            () => ParsedJsonDocument<JsonElement>.ParseAsync(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data)));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SequenceBytes_Single(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(new ReadOnlySequence<byte>(bytes))));
    }

    [TestMethod]
    [DynamicData(nameof(ReducedTestCases))]
    public async Task ParseJson_SequenceBytes_Multi(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(JsonTestHelper.SegmentInto(bytes, 31))));
    }

    private static async Task ParseJsonAsync(
        bool compactData,
        TestCaseType type,
        string jsonString,
        Func<string, Task<ParsedJsonDocument<JsonElement>>> stringDocBuilder,
        Func<byte[], Task<ParsedJsonDocument<JsonElement>>> bytesDocBuilder)
    {
        // One, but not both, must be null.
        if ((stringDocBuilder == null) == (bytesDocBuilder == null))
            throw new InvalidOperationException();

        // Remove all formatting/indentation
        if (compactData)
        {
            jsonString = GetCompactJson(type, jsonString);
        }

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (ParsedJsonDocument<JsonElement> doc = await (stringDocBuilder?.Invoke(jsonString) ?? bytesDocBuilder?.Invoke(dataUtf8)))
        {
            Assert.IsNotNull(doc);

            JsonElement rootElement = doc.RootElement;

            Func<JToken, string> expectedFunc = null;
            Func<JsonElement, string> actualFunc = null;

            switch (type)
            {
                case TestCaseType.Json400KB:
                    expectedFunc = token => ReadJson400KB(token);
                    actualFunc = element => ReadJson400KB(element);
                    break;

                case TestCaseType.HelloWorld:
                    expectedFunc = token => ReadHelloWorld(token);
                    actualFunc = element => ReadHelloWorld(element);
                    break;
            }

            if (expectedFunc != null)
            {
                string expectedCustom;
                string actualCustom;

                using (var stream = new MemoryStream(dataUtf8))
                using (var streamReader = new StreamReader(stream, Encoding.UTF8, false, 1024, true))
                using (var jsonReader = new JsonTextReader(streamReader) { MaxDepth = null })
                {
                    var jToken = JToken.ReadFrom(jsonReader);

                    expectedCustom = expectedFunc(jToken);
                    actualCustom = actualFunc(rootElement);
                }

                Assert.AreEqual(expectedCustom, actualCustom);
            }

            string actual = PrintJson(doc);
            string expected = GetExpectedConcat(type, jsonString);

            Assert.AreEqual(expected, actual);

            Assert.AreEqual(jsonString, rootElement.GetRawText());
        }
    }

    private static string PrintJson(ParsedJsonDocument<JsonElement> document, int sizeHint = 0)
    {
        return PrintJson(document.RootElement, sizeHint);
    }

    private static string PrintJson(JsonElement element, int sizeHint = 0)
    {
        var sb = new StringBuilder(sizeHint);
        DepthFirstAppend(sb, element);
        return sb.ToString();
    }

    private static void DepthFirstAppend(StringBuilder buf, JsonElement element)
    {
        JsonValueKind kind = element.ValueKind;

        switch (kind)
        {
            case JsonValueKind.False:
            case JsonValueKind.True:
            case JsonValueKind.String:
            case JsonValueKind.Number:
            {
                buf.Append(element.ToString());
                buf.Append(", ");
                break;
            }
            case JsonValueKind.Object:
            {
                foreach (JsonProperty<JsonElement> prop in element.EnumerateObject())
                {
                    buf.Append(prop.Name);
                    buf.Append(", ");
                    DepthFirstAppend(buf, prop.Value);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (JsonElement child in element.EnumerateArray())
                {
                    DepthFirstAppend(buf, child);
                }

                break;
            }
        }
    }

    [TestMethod]
    [DataRow("[{\"arrayWithObjects\":[\"text\",14,[],null,false,{},{\"time\":24},[\"1\",\"2\",\"3\"]]}]")]
    [DataRow("[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}]")]
    [DataRow("[{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}}]")]
    [DataRow("{\"a\":\"b\"}")]
    [DataRow("{}")]
    [DataRow("[]")]
    public void CustomParseJson(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            string actual = PrintJson(doc);

            TextReader reader = new StringReader(jsonString);
            string expected = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

            Assert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public void ParseArray()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleArrayJson))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(2, root.GetArrayLength());

            string phoneNumber = root[0].GetString();
            int age = root[1].GetInt32();

            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual(25, age);

            Assert.ThrowsExactly<IndexOutOfRangeException>(() => root[2]);
        }
    }

    [TestMethod]
    public void ParseSimpleObject()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        {
            JsonElement parsedObject = doc.RootElement;

            int age = parsedObject.GetProperty("age").GetInt32();
            string ageString = parsedObject.GetProperty("age").ToString();
            string first = parsedObject.GetProperty("first").GetString();
            string last = parsedObject.GetProperty("last").GetString();
            string phoneNumber = parsedObject.GetProperty("phoneNumber").GetString();
            string street = parsedObject.GetProperty("street").GetString();
            string city = parsedObject.GetProperty("city").GetString();
            int zip = parsedObject.GetProperty("zip").GetInt32();

            Assert.AreEqual(7, parsedObject.GetPropertyCount());
            Assert.IsTrue(parsedObject.TryGetProperty("age", out JsonElement age2));
            Assert.AreEqual(30, age2.GetInt32());

            Assert.AreEqual(30, age);
            Assert.AreEqual("30", ageString);
            Assert.AreEqual("John", first);
            Assert.AreEqual("Smith", last);
            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual("1 Microsoft Way", street);
            Assert.AreEqual("Redmond", city);
            Assert.AreEqual(98052, zip);
        }
    }

    [TestMethod]
    public void ParseNestedJson()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        {
            JsonElement parsedObject = doc.RootElement;

            Assert.AreEqual(1, parsedObject.GetArrayLength());
            JsonElement person = parsedObject[0];
            Assert.AreEqual(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement phoneNums = person.GetProperty("phoneNumbers");
            Assert.AreEqual(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement address = person.GetProperty("address");
            string street = address.GetProperty("street").GetString();
            string city = address.GetProperty("city").GetString();
            double zipCode = address.GetProperty("zip").GetDouble();
            const string ThrowsAnyway = "throws-anyway";

            Assert.AreEqual(30, age);
            Assert.AreEqual("John", first);
            Assert.AreEqual("Smith", last);
            Assert.AreEqual("425-000-1212", phoneNum1);
            Assert.AreEqual("425-000-1213", phoneNum2);
            Assert.AreEqual("1 Microsoft Way", street);
            Assert.AreEqual("Redmond", city);
            Assert.AreEqual(98052, zipCode);

            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetArrayLength());
            Assert.ThrowsExactly<IndexOutOfRangeException>(() => phoneNums[2]);
            Assert.ThrowsExactly<InvalidOperationException>(() => phoneNums.GetProperty("2"));
            Assert.ThrowsExactly<KeyNotFoundException>(() => address.GetProperty("2"));
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("city").GetDouble());
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("city").GetBoolean());
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("zip").GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetProperty("phoneNumbers").GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        }
    }

    [TestMethod]
    public void ParseSimpleObjectWithPropertyMap()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        {
            JsonElement parsedObject = doc.RootElement;
            parsedObject.EnsurePropertyMap();
            int age = parsedObject.GetProperty("age").GetInt32();
            string ageString = parsedObject.GetProperty("age").ToString();
            string first = parsedObject.GetProperty("first").GetString();
            string last = parsedObject.GetProperty("last").GetString();
            string phoneNumber = parsedObject.GetProperty("phoneNumber").GetString();
            string street = parsedObject.GetProperty("street").GetString();
            string city = parsedObject.GetProperty("city").GetString();
            int zip = parsedObject.GetProperty("zip").GetInt32();

            Assert.AreEqual(7, parsedObject.GetPropertyCount());
            Assert.IsTrue(parsedObject.TryGetProperty("age", out JsonElement age2));
            Assert.AreEqual(30, age2.GetInt32());

            Assert.AreEqual(30, age);
            Assert.AreEqual("30", ageString);
            Assert.AreEqual("John", first);
            Assert.AreEqual("Smith", last);
            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual("1 Microsoft Way", street);
            Assert.AreEqual("Redmond", city);
            Assert.AreEqual(98052, zip);
        }
    }

    [TestMethod]
    public void EqualsInOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))

        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
            Assert.IsTrue(doc.RootElement.Equals(doc3.RootElement));
            Assert.IsTrue(doc.RootElement.Equals(doc4.RootElement));
            Assert.IsTrue(doc3.RootElement.Equals(doc4.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals2))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
            Assert.IsTrue(doc.RootElement.Equals(doc3.RootElement));
            Assert.IsTrue(doc.RootElement.Equals(doc4.RootElement));
            Assert.IsTrue(doc3.RootElement.Equals(doc4.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_LeftEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "f\u006fo": "hello" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsArray_MismatchedLength()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            [ "bar", "foo" ]
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            [ "bar", "foo", "baz" ]
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_MismatchedTypes()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "foo" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            [ "bar", "foo", "baz" ]
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_MismatchedTypesBoolean()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            true
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            false
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_MatchTypesNull()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            null
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            null
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_MatchedTypesBooleanTrue()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            true
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            true
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_MatchedTypesBooleanFalse()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            false
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            false
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsArray_MismatchedValue()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            [ "bar", "foo" ]
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            [ "bar", "baz" ]
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_BothEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "f\u006fo": "hello" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "f\u006fo": "hello", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_RightEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "f\u006fo": "hello" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_RightEscapedLong()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo_but_this_should_be_long_enough_to_cause_a_shared_array_allocation_0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789": "hello", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            {  "bar": "world", "fo\u006f_but_this_should_be_long_enough_to_cause_a_shared_array_allocation_0123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789012345678901234567890123456789": "hello" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_RightValueEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_LeftValueEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_BothValuesEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hell\u006f", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_NotEquals_RightEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hello" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "fo\u006f": "yo", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_NotEquals_LeftEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "fo\u006f": "yo", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hello" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsOutOfOrder_NotEqualsBothValuesEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "y\u006f", "bar": "world" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsInOrder_NotEqualsBothValuesEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "y\u006f" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsInOrder_NotEqualsLeftValueEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hell\u006f" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "yo" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void EqualsInOrder_NotEqualsRightValueEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "hello" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "foo": "yo" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
        }
    }

    [TestMethod]
    public void Equals_NotEquals()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsFalse(doc.RootElement.Equals(doc2.RootElement));
            Assert.IsFalse(doc.RootElement.Equals(doc4.RootElement));
            Assert.IsFalse(doc3.RootElement.Equals(doc4.RootElement));
        }
    }

    [TestMethod]
    public void OperatorEqualsInOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))

        {
            Assert.IsTrue(doc.RootElement == doc2.RootElement);
            Assert.IsTrue(doc.RootElement == doc3.RootElement);
            Assert.IsTrue(doc.RootElement == doc4.RootElement);
            Assert.IsTrue(doc3.RootElement == doc4.RootElement);
        }
    }

    [TestMethod]
    public void OperatorEqualsOutOfOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals2))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsTrue(doc.RootElement == doc2.RootElement);
            Assert.IsTrue(doc.RootElement == doc3.RootElement);
            Assert.IsTrue(doc.RootElement == doc4.RootElement);
            Assert.IsTrue(doc3.RootElement == doc4.RootElement);
        }
    }

    [TestMethod]
    public void OperatorEqualsOutOfOrder_RightEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "f\u006fo": "hello" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsTrue(doc.RootElement == doc2.RootElement);
        }
    }

    [TestMethod]
    public void OperatorEquals_NotEquals()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsFalse(doc.RootElement == doc2.RootElement);
            Assert.IsFalse(doc.RootElement == doc4.RootElement);
            Assert.IsFalse(doc3.RootElement == doc4.RootElement);
        }
    }

    [TestMethod]
    public void OperatorNotEqualsInOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))

        {
            Assert.IsFalse(doc.RootElement != doc2.RootElement);
            Assert.IsFalse(doc.RootElement != doc3.RootElement);
            Assert.IsFalse(doc.RootElement != doc4.RootElement);
            Assert.IsFalse(doc3.RootElement != doc4.RootElement);
        }
    }

    [TestMethod]
    public void OperatorNotEqualsOutOfOrder()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals2))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsFalse(doc.RootElement != doc2.RootElement);
            Assert.IsFalse(doc.RootElement != doc3.RootElement);
            Assert.IsFalse(doc.RootElement != doc4.RootElement);
            Assert.IsFalse(doc3.RootElement != doc4.RootElement);
        }
    }

    [TestMethod]
    public void OperatorNotEqualsOutOfOrder_RightEscaped()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "foo": "hello", "bar": "world" }
            """
            ))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(
            """
            { "bar": "world", "f\u006fo": "hello" }
            """
            ))
        using (var workspace = JsonWorkspace.Create())
        {
            Assert.IsFalse(doc.RootElement != doc2.RootElement);
        }
    }

    [TestMethod]
    public void OperatorNotEquals_NotEquals()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.JsonDeepEquals1))
        using (var doc2 = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        using (var workspace = JsonWorkspace.Create())
        using (JsonDocumentBuilder<JsonElement.Mutable> doc3 = doc.RootElement.CreateBuilder(workspace))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc4 = doc2.RootElement.BuildDynamicDocument(workspace))
        {
            Assert.IsTrue(doc.RootElement != doc2.RootElement);
            Assert.IsTrue(doc.RootElement != doc4.RootElement);
            Assert.IsTrue(doc3.RootElement != doc4.RootElement);
        }
    }

    [TestMethod]
    public void ParsedWithPropertyMap()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJsonWithComplexName))
        {
            JsonElement parsedObject = doc.RootElement;
            parsedObject.EnsurePropertyMap();
            int age = parsedObject.GetProperty("ag\"e567890").GetInt32();
            string ageString = parsedObject.GetProperty("ag\"e567890").ToString();
            string first = parsedObject.GetProperty("first").GetString();
            string last = parsedObject.GetProperty("last").GetString();
            string phoneNumber = parsedObject.GetProperty("phoneNumber").GetString();
            string street = parsedObject.GetProperty("street").GetString();
            string city = parsedObject.GetProperty("city").GetString();
            int zip = parsedObject.GetProperty("zip").GetInt32();

            Assert.AreEqual(7, parsedObject.GetPropertyCount());
            Assert.IsTrue(parsedObject.TryGetProperty("ag\"e567890", out JsonElement age2));
            Assert.AreEqual(30, age2.GetInt32());

            Assert.AreEqual(30, age);
            Assert.AreEqual("30", ageString);
            Assert.AreEqual("John", first);
            Assert.AreEqual("Smith", last);
            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual("1 Microsoft Way", street);
            Assert.AreEqual("Redmond", city);
            Assert.AreEqual(98052, zip);
        }
    }

    [TestMethod]
    public void ParseNestedJsonWithPropertyMap()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        {
            JsonElement parsedObject = doc.RootElement;

            Assert.AreEqual(1, parsedObject.GetArrayLength());
            JsonElement person = parsedObject[0];
            person.EnsurePropertyMap();
            Assert.AreEqual(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement phoneNums = person.GetProperty("phoneNumbers");
            Assert.AreEqual(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement address = person.GetProperty("address");
            string street = address.GetProperty("street").GetString();
            string city = address.GetProperty("city").GetString();
            double zipCode = address.GetProperty("zip").GetDouble();
            const string ThrowsAnyway = "throws-anyway";

            Assert.AreEqual(30, age);
            Assert.AreEqual("John", first);
            Assert.AreEqual("Smith", last);
            Assert.AreEqual("425-000-1212", phoneNum1);
            Assert.AreEqual("425-000-1213", phoneNum2);
            Assert.AreEqual("1 Microsoft Way", street);
            Assert.AreEqual("Redmond", city);
            Assert.AreEqual(98052, zipCode);

            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetArrayLength());
            Assert.ThrowsExactly<IndexOutOfRangeException>(() => phoneNums[2]);
            Assert.ThrowsExactly<InvalidOperationException>(() => phoneNums.GetProperty("2"));
            Assert.ThrowsExactly<KeyNotFoundException>(() => address.GetProperty("2"));
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("city").GetDouble());
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("city").GetBoolean());
            Assert.ThrowsExactly<InvalidOperationException>(() => address.GetProperty("zip").GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetProperty("phoneNumbers").GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.GetString());
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => person.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        }
    }

    [TestMethod]
    public void ParseBoolean()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[true,false]"))
        {
            JsonElement parsedObject = doc.RootElement;
            bool first = parsedObject[0].GetBoolean();
            bool second = parsedObject[1].GetBoolean();
            Assert.IsTrue(first);
            Assert.IsFalse(second);
        }
    }

    [TestMethod]
    public void JsonArrayToString()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
            Assert.AreEqual(SR.ParseJson, root.ToString());
        }
    }

    [TestMethod]
    public void JsonObjectToString()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.BasicJson))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Object, root.ValueKind);
            Assert.AreEqual(SR.BasicJson, root.ToString());
        }
    }

    [TestMethod]
    public void MixedArrayIndexing()
    {
        // The root object is an array with "complex" children
        // root[0] is a number (simple single forward)
        // root[1] is an object which needs to account for the start entry, the children, and end.
        // root[2] is the target inner array
        // root[3] is a simple value past two complex values
        //
        // Within root[2] the array has only simple values, so it uses a different indexing algorithm.
        const string json = " [ 6, { \"hi\": \"mom\" }, [ \"425-214-3151\", 25 ], null ] ";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;
            JsonElement target = root[2];

            Assert.AreEqual(2, target.GetArrayLength());

            string phoneNumber = target[0].GetString();
            int age = target[1].GetInt32();

            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual(25, age);
            Assert.AreEqual(JsonValueKind.Null, root[3].ValueKind);

            Assert.ThrowsExactly<IndexOutOfRangeException>(() => root[4]);
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(sbyte.MaxValue)]
    [DataRow(sbyte.MinValue)]
    public void ReadNumber_1Byte(int valueAsInt)
    {
        sbyte value = (sbyte)valueAsInt;
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;
        BigInteger expectedBigInteger = value;
        BigNumber expectedBigNumber = new(value, 0);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.IsTrue(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(value, sbyteVal);

            Assert.IsTrue(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(value, shortVal);

            Assert.IsTrue(root.TryGetInt32(out int intVal));
            Assert.AreEqual(value, intVal);

            Assert.IsTrue(root.TryGetInt64(out long longVal));
            Assert.AreEqual(value, longVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.AreEqual(expectedBigInteger, root.GetBigInteger());
            Assert.AreEqual(expectedBigNumber, root.GetBigNumber());
            Assert.AreEqual(value, root.GetInt32());
            Assert.AreEqual(value, root.GetInt64());

            if (value >= 0)
            {
                byte expectedByte = (byte)value;
                ushort expectedUShort = (ushort)value;
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.IsTrue(root.TryGetByte(out byte byteVal));
                Assert.AreEqual(expectedByte, byteVal);

                Assert.IsTrue(root.TryGetUInt16(out ushort ushortVal));
                Assert.AreEqual(expectedUShort, ushortVal);

                Assert.IsTrue(root.TryGetUInt32(out uint uintVal));
                Assert.AreEqual(expectedUInt, uintVal);

                Assert.IsTrue(root.TryGetUInt64(out ulong ulongVal));
                Assert.AreEqual(expectedULong, ulongVal);

                Assert.AreEqual(expectedUInt, root.GetUInt32());
                Assert.AreEqual(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.IsFalse(root.TryGetByte(out byte byteValue));
                Assert.AreEqual(0, byteValue);

                Assert.IsFalse(root.TryGetUInt16(out ushort ushortValue));
                Assert.AreEqual(0, ushortValue);

                Assert.IsFalse(root.TryGetUInt32(out uint uintValue));
                Assert.AreEqual(0U, uintValue);

                Assert.IsFalse(root.TryGetUInt64(out ulong ulongValue));
                Assert.AreEqual(0UL, ulongValue);

                Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
                Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DataRow((short)0)]
    [DataRow(short.MaxValue)]
    [DataRow(short.MinValue)]
    public void ReadNumber_2Bytes(short value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;
        BigInteger expectedBigInteger = value;
        BigNumber expectedBigNumber = new(value, 0);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.AreEqual((value == 0), root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.AreEqual((value == 0), root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsTrue(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(value, shortVal);

            Assert.IsTrue(root.TryGetInt32(out int intVal));
            Assert.AreEqual(value, intVal);

            Assert.IsTrue(root.TryGetInt64(out long longVal));
            Assert.AreEqual(value, longVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.AreEqual(expectedBigInteger, root.GetBigInteger());
            Assert.AreEqual(expectedBigNumber, root.GetBigNumber());
            Assert.AreEqual(value, root.GetInt32());
            Assert.AreEqual(value, root.GetInt64());

            if (value >= 0)
            {
                byte expectedByte = (byte)value;
                ushort expectedUShort = (ushort)value;
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.IsTrue(root.TryGetUInt16(out ushort ushortVal));
                Assert.AreEqual(expectedUShort, ushortVal);

                Assert.IsTrue(root.TryGetUInt32(out uint uintVal));
                Assert.AreEqual(expectedUInt, uintVal);

                Assert.IsTrue(root.TryGetUInt64(out ulong ulongVal));
                Assert.AreEqual(expectedULong, ulongVal);

                Assert.AreEqual(expectedUInt, root.GetUInt32());
                Assert.AreEqual(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.IsFalse(root.TryGetUInt16(out ushort ushortValue));
                Assert.AreEqual(0, ushortValue);

                Assert.IsFalse(root.TryGetUInt32(out uint uintValue));
                Assert.AreEqual(0U, uintValue);

                Assert.IsFalse(root.TryGetUInt64(out ulong ulongValue));
                Assert.AreEqual(0UL, ulongValue);

                Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
                Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(int.MaxValue)]
    [DataRow(int.MinValue)]
    public void ReadSmallInteger(int value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;
        BigInteger expectedBigInteger = value;
        BigNumber expectedBigNumber = new(value, 0);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.AreEqual((value == 0), root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.AreEqual((value == 0), root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.AreEqual((value == 0), root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.AreEqual((value == 0), root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsTrue(root.TryGetInt32(out int intVal));
            Assert.AreEqual(value, intVal);

            Assert.IsTrue(root.TryGetInt64(out long longVal));
            Assert.AreEqual(value, longVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.AreEqual(expectedBigInteger, root.GetBigInteger());
            Assert.AreEqual(expectedBigNumber, root.GetBigNumber());
            Assert.AreEqual(value, root.GetInt32());
            Assert.AreEqual(value, root.GetInt64());

            if (value >= 0)
            {
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.IsTrue(root.TryGetUInt32(out uint uintVal));
                Assert.AreEqual(expectedUInt, uintVal);

                Assert.IsTrue(root.TryGetUInt64(out ulong ulongVal));
                Assert.AreEqual(expectedULong, ulongVal);

                Assert.AreEqual(expectedUInt, root.GetUInt32());
                Assert.AreEqual(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.IsFalse(root.TryGetUInt32(out uint uintValue));
                Assert.AreEqual(0U, uintValue);

                Assert.IsFalse(root.TryGetUInt64(out ulong ulongValue));
                Assert.AreEqual(0UL, ulongValue);

                Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
                Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DataRow((long)int.MaxValue + 1)]
    [DataRow((long)uint.MaxValue)]
    [DataRow(long.MaxValue)]
    [DataRow((long)int.MinValue - 1)]
    [DataRow(long.MinValue)]
    public void ReadMediumInteger(long value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;
        BigInteger expectedBigInteger = value;
        BigNumber expectedBigNumber = new(value, 0);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.IsFalse(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.IsFalse(root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsFalse(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.IsFalse(root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsFalse(root.TryGetInt32(out int intVal));
            Assert.AreEqual(0, intVal);

            Assert.IsTrue(root.TryGetInt64(out long longVal));
            Assert.AreEqual(value, longVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt32());
            Assert.AreEqual(value, root.GetInt64());

            if (value >= 0)
            {
                if (value <= uint.MaxValue)
                {
                    uint expectedUInt = (uint)value;
                    Assert.IsTrue(root.TryGetUInt32(out uint uintVal));
                    Assert.AreEqual(expectedUInt, uintVal);

                    Assert.AreEqual(expectedUInt, root.GetUInt64());
                }
                else
                {
                    Assert.IsFalse(root.TryGetUInt32(out uint uintValue));
                    Assert.AreEqual(0U, uintValue);

                    Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
                }

                ulong expectedULong = (ulong)value;
                Assert.IsTrue(root.TryGetUInt64(out ulong ulongVal));
                Assert.AreEqual(expectedULong, ulongVal);

                Assert.AreEqual(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.IsFalse(root.TryGetUInt32(out uint uintValue));
                Assert.AreEqual(0U, uintValue);

                Assert.IsFalse(root.TryGetUInt64(out ulong ulongValue));
                Assert.AreEqual(0UL, ulongValue);

                Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
                Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DataRow((ulong)long.MaxValue + 1)]
    [DataRow(ulong.MaxValue)]
    public void ReadLargeInteger(ulong value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;
        BigInteger expectedBigInteger = value;
        BigNumber expectedBigNumber = new(value, 0);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.IsFalse(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.IsFalse(root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsFalse(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.IsFalse(root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsFalse(root.TryGetInt32(out int intVal));
            Assert.AreEqual(0, intVal);

            Assert.IsFalse(root.TryGetUInt32(out uint uintVal));
            Assert.AreEqual(0U, uintVal);

            Assert.IsFalse(root.TryGetInt64(out long longVal));
            Assert.AreEqual(0L, longVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt64());

            Assert.IsTrue(root.TryGetUInt64(out ulong ulongVal));
            Assert.AreEqual(value, ulongVal);

            Assert.AreEqual(value, root.GetUInt64());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    public void ReadTooLargeInteger()
    {
        float expectedFloat = ulong.MaxValue;
        double expectedDouble = ulong.MaxValue;
        decimal expectedDecimal = ulong.MaxValue;
        BigInteger expectedBigInteger = ulong.MaxValue;
        BigNumber expectedBigNumber = new BigNumber(new BigInteger(ulong.MaxValue) * 10, 0).Normalize();
        expectedDouble *= 10;
        expectedFloat *= 10;
        expectedDecimal *= 10;
        expectedBigInteger *= 10;

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + ulong.MaxValue + "0  ", default))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsTrue(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(expectedBigInteger, bigIntegerVal);

            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.IsFalse(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.IsFalse(root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsFalse(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.IsFalse(root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsFalse(root.TryGetInt32(out int intVal));
            Assert.AreEqual(0, intVal);

            Assert.IsFalse(root.TryGetUInt32(out uint uintVal));
            Assert.AreEqual(0U, uintVal);

            Assert.IsFalse(root.TryGetInt64(out long longVal));
            Assert.AreEqual(0L, longVal);

            Assert.IsFalse(root.TryGetUInt64(out ulong ulongVal));
            Assert.AreEqual(0UL, ulongVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.AreEqual(expectedBigInteger, root.GetBigInteger());
            Assert.AreEqual(expectedBigNumber, root.GetBigNumber());

            Assert.ThrowsExactly<FormatException>(() => root.GetInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt64());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void ReadDateTimeAndDateTimeOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            var expectedDateTime = DateTime.Parse(expectedString);
            var expectedDateTimeOffset = DateTimeOffset.Parse(expectedString);
            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsTrue(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.AreEqual(expectedDateTime, DateTimeVal);

            Assert.IsTrue(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.AreEqual(expectedDateTimeOffset, DateTimeOffsetVal);

            Assert.AreEqual(expectedDateTime, root.GetDateTime());
            Assert.AreEqual(expectedDateTimeOffset, root.GetDateTimeOffset());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetSByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.ValidISO8601TestsWithUtcOffset), typeof(JsonDateTimeTestData))]
    public void ReadDateTimeAndDateTimeOffset_WithUtcOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            var expectedDateTime = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expectedDateTimeOffset = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            OffsetDateTime expectedOffsetDateTime = OffsetDateTimePattern.ExtendedIso.Parse(expectedString).GetValueOrThrow();
            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsTrue(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.AreEqual(expectedDateTime, DateTimeVal);

            Assert.IsTrue(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.AreEqual(expectedDateTimeOffset, DateTimeOffsetVal);

            Assert.AreEqual(expectedDateTime, root.GetDateTime());
            Assert.AreEqual(expectedDateTimeOffset, root.GetDateTimeOffset());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), typeof(JsonDateTimeTestData))]
    public void ReadDateTimeAndDateTimeOffset_InvalidTests(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsFalse(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.AreEqual(default, DateTimeVal);

            Assert.IsFalse(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.AreEqual(default, DateTimeOffsetVal);
            Assert.IsFalse(root.TryGetOffsetDateTime(out OffsetDateTime OffsetDateTimeVal));
            Assert.AreEqual(default, OffsetDateTimeVal);

            Assert.ThrowsExactly<FormatException>(() => root.GetDateTime());
            Assert.ThrowsExactly<FormatException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<FormatException>(() => root.GetOffsetDateTime());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.ValidGuidTests), typeof(JsonGuidTestData))]
    [DynamicData(nameof(JsonGuidTestData.ValidHexGuidTests), typeof(JsonGuidTestData))]
    public void ReadGuid(string jsonString, string expectedStr)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{jsonString}\"");

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            var expected = new Guid(expectedStr);

            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsTrue(root.TryGetGuid(out Guid GuidVal));
            Assert.AreEqual(expected, GuidVal);

            Assert.AreEqual(expected, root.GetGuid());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetSByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.InvalidGuidTests), typeof(JsonGuidTestData))]
    public void ReadGuid_InvalidTests(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{jsonString}\"");

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsFalse(root.TryGetGuid(out Guid GuidVal));
            Assert.AreEqual(default, GuidVal);

            Assert.ThrowsExactly<FormatException>(() => root.GetGuid());
        }
    }

    public static IEnumerable<object[]> NonIntegerCases
    {
        get
        {
            yield return new object[] { "1e+1", 10.0, 10.0f, 10m, new BigNumber(1, 1) };
            yield return new object[] { "1.1e-0", 1.1, 1.1f, 1.1m, new BigNumber(11, -1) };
            yield return new object[] { "3.14159", 3.14159, 3.14159f, 3.14159m, new BigNumber(314159, -5) };
            yield return new object[] { "1e-10", 1e-10, 1e-10f, 1e-10m, new BigNumber(1, -10) };
            yield return new object[] { "1234567.15", 1234567.15, 1234567.13f, 1234567.15m, new BigNumber(123456715, -2) };
        }
    }

    [TestMethod]
    [DynamicData(nameof(NonIntegerCases))]
    public void ReadNonInteger(string str, double expectedDouble, float expectedFloat, decimal expectedDecimal, BigNumber expectedBigNumber)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + str + "  "))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

            Assert.IsFalse(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.IsFalse(root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsFalse(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.IsFalse(root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsFalse(root.TryGetInt32(out int intVal));
            Assert.AreEqual(0, intVal);

            Assert.IsFalse(root.TryGetUInt32(out uint uintVal));
            Assert.AreEqual(0U, uintVal);

            Assert.IsFalse(root.TryGetInt64(out long longVal));
            Assert.AreEqual(0L, longVal);

            Assert.IsFalse(root.TryGetUInt64(out ulong ulongVal));
            Assert.AreEqual(0UL, ulongVal);

            Assert.IsFalse(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(0UL, bigIntegerVal);

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
            Assert.AreEqual(expectedBigNumber, root.GetBigNumber());
            Assert.ThrowsExactly<FormatException>(() => root.GetSByte());
            Assert.ThrowsExactly<FormatException>(() => root.GetByte());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt16());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt16());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt64());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    public void ReadTooPreciseDouble()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    1e+100000002"))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Assert.IsFalse(root.TryGetSingle(out float floatVal));
                Assert.AreEqual(0f, floatVal);

                Assert.IsFalse(root.TryGetDouble(out double doubleVal));
                Assert.AreEqual(0d, doubleVal);
            }
            else
            {
                Assert.IsTrue(root.TryGetSingle(out float floatVal));
                Assert.AreEqual(float.PositiveInfinity, floatVal);

                Assert.IsTrue(root.TryGetDouble(out double doubleVal));
                Assert.AreEqual(double.PositiveInfinity, doubleVal);
            }

            Assert.IsFalse(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(0m, decimalVal);

            Assert.IsFalse(root.TryGetBigInteger(out BigInteger bigIntegerVal));
            Assert.AreEqual(0, bigIntegerVal);

            var expectedBigNumber = new BigNumber(1, 100000002);
            Assert.IsTrue(root.TryGetBigNumber(out BigNumber bigNumberVal));
            Assert.AreEqual(expectedBigNumber, bigNumberVal);

            Assert.IsFalse(root.TryGetSByte(out sbyte sbyteVal));
            Assert.AreEqual(0, sbyteVal);

            Assert.IsFalse(root.TryGetByte(out byte byteVal));
            Assert.AreEqual(0, byteVal);

            Assert.IsFalse(root.TryGetInt16(out short shortVal));
            Assert.AreEqual(0, shortVal);

            Assert.IsFalse(root.TryGetUInt16(out ushort ushortVal));
            Assert.AreEqual(0, ushortVal);

            Assert.IsFalse(root.TryGetInt32(out int intVal));
            Assert.AreEqual(0, intVal);

            Assert.IsFalse(root.TryGetUInt32(out uint uintVal));
            Assert.AreEqual(0U, uintVal);

            Assert.IsFalse(root.TryGetInt64(out long longVal));
            Assert.AreEqual(0L, longVal);

            Assert.IsFalse(root.TryGetUInt64(out ulong ulongVal));
            Assert.AreEqual(0UL, ulongVal);

            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Assert.ThrowsExactly<FormatException>(() => root.GetSingle());
                Assert.ThrowsExactly<FormatException>(() => root.GetDouble());
            }
            else
            {
                Assert.AreEqual(float.PositiveInfinity, root.GetSingle());
                Assert.AreEqual(double.PositiveInfinity, root.GetDouble());
            }

            Assert.ThrowsExactly<FormatException>(() => root.GetDecimal());
            Assert.ThrowsExactly<FormatException>(() => root.GetSByte());
            Assert.ThrowsExactly<FormatException>(() => root.GetByte());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt16());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt16());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt32());
            Assert.ThrowsExactly<FormatException>(() => root.GetInt64());
            Assert.ThrowsExactly<FormatException>(() => root.GetUInt64());

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    public void ReadArrayWithComments()
    {
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip,
        };

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            "[ 0, 1, 2, 3/*.14159*/           , /* 42, 11, hut, hut, hike! */ 4 ]",
            options))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
            Assert.AreEqual(5, root.GetArrayLength());

            for (int i = root.GetArrayLength() - 1; i >= 0; i--)
            {
                Assert.AreEqual(i, root[i].GetInt32());
            }

            int val = 0;

            foreach (JsonElement element in root.EnumerateArray())
            {
                Assert.AreEqual(val, element.GetInt32());
                val++;
            }

            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDouble());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetDouble(out double _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetSByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetSByte(out sbyte _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetByte());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetByte(out byte _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt16(out short _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt16());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt16(out ushort _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt32(out int _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt32());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt32(out uint _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt64(out long _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt64(out ulong _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] _));
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    public void CheckUseAfterDispose()
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        {
            JsonElement root = doc.RootElement;
            JsonProperty<JsonElement> property = root.EnumerateObject().First();
            doc.Dispose();

            Assert.ThrowsExactly<ObjectDisposedException>(() => root.ValueKind);
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetArrayLength());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetPropertyCount());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.EnumerateArray());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetDouble());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetDouble(out double _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetSByte());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetSByte(out sbyte _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetByte());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetByte(out byte _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetInt16());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetInt16(out short _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetUInt16());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetUInt16(out ushort _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetInt32());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetInt32(out int _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetUInt32());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetUInt32(out uint _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetInt64());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetInt64(out long _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetUInt64());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetUInt64(out ulong _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetBytesFromBase64());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.TryGetBytesFromBase64(out byte[] _));
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetBoolean());
            Assert.ThrowsExactly<ObjectDisposedException>(() => root.GetRawText());

            Assert.ThrowsExactly<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                root.WriteTo(writer);
            });

            Assert.ThrowsExactly<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                doc.WriteTo(writer);
            });

            Assert.ThrowsExactly<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                property.WriteTo(writer);
            });
        }
    }

    [TestMethod]
    public void CheckUseDefault()
    {
        JsonElement root = default;

        Assert.AreEqual(JsonValueKind.Undefined, root.ValueKind);

        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetArrayLength());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPropertyCount());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateArray());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDouble());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetDouble(out double _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetSByte());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetSByte(out sbyte _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetByte());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetByte(out byte _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt16());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt16(out short _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt16());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt16(out ushort _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt32());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt32(out int _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt32());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt32(out uint _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetInt64());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetInt64(out long _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetUInt64());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetUInt64(out ulong _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString());
        const string ThrowsAnyway = "throws-anyway";
        Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBytesFromBase64());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] _));
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTime());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetDateTimeOffset());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDateTime());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetTime());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetLocalDate());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetOffsetDate());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetPeriod());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        Assert.ThrowsExactly<InvalidOperationException>(() => root.GetRawText());

        Assert.ThrowsExactly<InvalidOperationException>(() =>
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            using var writer = new Utf8JsonWriter(buffer);
            root.WriteTo(writer);
        });
    }

    [TestMethod]
    public void CheckInvalidString()
    {
        Assert.ThrowsExactly<ArgumentException>(() => ParsedJsonDocument<JsonElement>.Parse("{ \"unpaired\uDFFE\": true }"));
    }

    [TestMethod]
    [DataRow("\"hello\"    ", "hello")]
    [DataRow("    null     ", (string)null)]
    [DataRow("\"\\u0033\\u0031\"", "31")]
    public void ReadString(string json, string expectedValue)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            Assert.AreEqual(expectedValue, doc.RootElement.GetString());
        }
    }

    [TestMethod]
    public void GetString_BadUtf8()
    {
        // The Arabic ligature Lam-Alef (U+FEFB) (which happens to, as a standalone, mean "no" in English)
        // is UTF-8 EF BB BB.  So let's leave out a BB and put it in quotes.
        byte[] badUtf8 = new byte[] { 0x22, 0xEF, 0xBB, 0x22 };
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(badUtf8))
        {
            JsonElement root = doc.RootElement;

            const string ErrorMessage = "Cannot transcode invalid UTF-8 JSON text to UTF-16 string.";
            Assert.AreEqual(JsonValueKind.String, root.ValueKind);
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetString(), ErrorMessage);
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetRawText());
            const string DummyString = "dummy-string";
            Assert.IsFalse(root.ValueEquals(badUtf8));
            Assert.IsFalse(root.ValueEquals(DummyString));
            Assert.IsFalse(root.ValueEquals(DummyString.AsSpan()));
            Assert.IsFalse(root.ValueEquals(Encoding.UTF8.GetBytes(DummyString)));
        }
    }

    [TestMethod]
    public void GetBase64String_BadUtf8()
    {
        // The Arabic ligature Lam-Alef (U+FEFB) (which happens to, as a standalone, mean "no" in English)
        // is UTF-8 EF BB BB.  So let's leave out a BB and put it in quotes.
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new byte[] { 0x22, 0xEF, 0xBB, 0x22 }))
        {
            JsonElement root = doc.RootElement;

            Assert.AreEqual(JsonValueKind.String, root.ValueKind);
            Assert.ThrowsExactly<FormatException>(() => root.GetBytesFromBase64());
            Assert.IsFalse(root.TryGetBytesFromBase64(out byte[] value));
            Assert.IsNull(value);
        }
    }

    [TestMethod]
    public void GetBase64Unescapes()
    {
        string jsonString = "\"\\u0031234\""; // equivalent to "\"1234\""

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        {
            byte[] expected = Convert.FromBase64String("1234"); // new byte[3] { 215, 109, 248 }

            CollectionAssert.AreEqual(expected, doc.RootElement.GetBytesFromBase64());

            Assert.IsTrue(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            CollectionAssert.AreEqual(expected, value);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonBase64TestData.ValidBase64Tests), typeof(JsonBase64TestData))]
    public void ReadBase64String(string jsonString)
    {
        byte[] expected = Convert.FromBase64String(jsonString.AsSpan(1, jsonString.Length - 2).ToString());

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            CollectionAssert.AreEqual(expected, doc.RootElement.GetBytesFromBase64());

            Assert.IsTrue(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            CollectionAssert.AreEqual(expected, value);
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonBase64TestData.InvalidBase64Tests), typeof(JsonBase64TestData))]
    public void InvalidBase64(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        {
            Assert.IsFalse(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            Assert.IsNull(value);

            Assert.ThrowsExactly<FormatException>(() => doc.RootElement.GetBytesFromBase64());
        }
    }

    [TestMethod]
    [DataRow(" { \"hi\": \"there\" }")]
    [DataRow(" { \n\n\n\n } ")]
    [DataRow(" { \"outer\": { \"inner\": [ 1, 2, 3 ] }, \"secondOuter\": [ 2, 4, 6, 0, 1 ] }")]
    public void TryGetProperty_NoProperty(string json)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;

            const string NotPresent = "Not Present";
            byte[] notPresentUtf8 = Encoding.UTF8.GetBytes(NotPresent);

            JsonElement element;
            Assert.IsFalse(root.TryGetProperty(NotPresent, out element));
            Assert.AreEqual(default, element);
            Assert.IsFalse(root.TryGetProperty(NotPresent.AsSpan(), out element));
            Assert.AreEqual(default, element);
            Assert.IsFalse(root.TryGetProperty(notPresentUtf8, out element));
            Assert.AreEqual(default, element);
            Assert.IsFalse(root.TryGetProperty(new string('z', 512), out element));
            Assert.AreEqual(default, element);

            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(NotPresent));
            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(NotPresent.AsSpan()));
            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(notPresentUtf8));
            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(new string('z', 512)));
        }
    }

    [TestMethod]
    public void TryGetProperty_CaseSensitive()
    {
        const string PascalString = "Needle";
        const string CamelString = "needle";
        const string OddString = "nEeDle";
        const string InverseOddString = "NeEdLE";

        string json = $"{{ \"{PascalString}\": \"no\", \"{CamelString}\": 42, \"{OddString}\": false }}";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;

            byte[] pascalBytes = Encoding.UTF8.GetBytes(PascalString);
            byte[] camelBytes = Encoding.UTF8.GetBytes(CamelString);
            byte[] oddBytes = Encoding.UTF8.GetBytes(OddString);
            byte[] inverseOddBytes = Encoding.UTF8.GetBytes(InverseOddString);

            void assertPascal(JsonElement elem)
            {
                Assert.AreEqual(JsonValueKind.String, elem.ValueKind);
                Assert.AreEqual("no", elem.GetString());
            }

            void assertCamel(JsonElement elem)
            {
                Assert.AreEqual(JsonValueKind.Number, elem.ValueKind);
                Assert.AreEqual(42, elem.GetInt32());
            }

            void assertOdd(JsonElement elem)
            {
                Assert.AreEqual(JsonValueKind.False, elem.ValueKind);
                Assert.IsFalse(elem.GetBoolean());
            }

            Assert.IsTrue(root.TryGetProperty(PascalString, out JsonElement pascal));
            assertPascal(pascal);
            Assert.IsTrue(root.TryGetProperty(PascalString.AsSpan(), out pascal));
            assertPascal(pascal);
            Assert.IsTrue(root.TryGetProperty(pascalBytes, out pascal));
            assertPascal(pascal);

            Assert.IsTrue(root.TryGetProperty(CamelString, out JsonElement camel));
            assertCamel(camel);
            Assert.IsTrue(root.TryGetProperty(CamelString.AsSpan(), out camel));
            assertCamel(camel);
            Assert.IsTrue(root.TryGetProperty(camelBytes, out camel));
            assertCamel(camel);

            Assert.IsTrue(root.TryGetProperty(OddString, out JsonElement odd));
            assertOdd(odd);
            Assert.IsTrue(root.TryGetProperty(OddString.AsSpan(), out odd));
            assertOdd(odd);
            Assert.IsTrue(root.TryGetProperty(oddBytes, out odd));
            assertOdd(odd);

            Assert.IsFalse(root.TryGetProperty(InverseOddString, out _));
            Assert.IsFalse(root.TryGetProperty(InverseOddString.AsSpan(), out _));
            Assert.IsFalse(root.TryGetProperty(inverseOddBytes, out _));

            assertPascal(root.GetProperty(PascalString));
            assertPascal(root.GetProperty(PascalString.AsSpan()));
            assertPascal(root.GetProperty(pascalBytes));

            assertCamel(root.GetProperty(CamelString));
            assertCamel(root.GetProperty(CamelString.AsSpan()));
            assertCamel(root.GetProperty(camelBytes));

            assertOdd(root.GetProperty(OddString));
            assertOdd(root.GetProperty(OddString.AsSpan()));
            assertOdd(root.GetProperty(oddBytes));

            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(InverseOddString));
            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(InverseOddString.AsSpan()));
            Assert.ThrowsExactly<KeyNotFoundException>(() => root.GetProperty(inverseOddBytes));
        }
    }

    [TestMethod]
    [DataRow(">>")]
    [DataRow(">>>")]
    [DataRow(">>a")]
    [DataRow(">a>")]
    public void TryGetDateTimeAndOffset_InvalidPropertyValue(string testData)
    {
        string jsonString = System.Text.Json.JsonSerializer.Serialize(new { DateTimeProperty = testData });

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        {
            JsonElement root = doc.RootElement;

            Assert.IsFalse(root.GetProperty("DateTimeProperty").TryGetDateTime(out DateTime datetimeValue));
            Assert.AreEqual(default, datetimeValue);
            Assert.ThrowsExactly<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTime());

            Assert.IsFalse(root.GetProperty("DateTimeProperty").TryGetDateTimeOffset(out DateTimeOffset datetimeOffsetValue));
            Assert.AreEqual(default, datetimeOffsetValue);
            Assert.ThrowsExactly<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTimeOffset());

            Assert.IsFalse(root.GetProperty("DateTimeProperty").TryGetOffsetDateTime(out OffsetDateTime offsetDatetimeValue));
            Assert.AreEqual(default, offsetDatetimeValue);
            Assert.ThrowsExactly<FormatException>(() => root.GetProperty("DateTimeProperty").GetOffsetDateTime());
        }
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("    ")]
    [DataRow("1 2")]
    [DataRow("[ 1")]
    public Task CheckUnparsable(string json)
    {
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(json));

        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(utf8));

        var singleSeq = new ReadOnlySequence<byte>(utf8);
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(singleSeq));

        ReadOnlySequence<byte> multiSegment = JsonTestHelper.SegmentInto(utf8, 6);
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(multiSegment));

        var stream = new MemoryStream(utf8);
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));

        stream.Seek(0, SeekOrigin.Begin);
        return Assert.ThrowsAsync<JsonException>(() => ParsedJsonDocument<JsonElement>.ParseAsync(stream));
    }

    [TestMethod]
    public void CheckParseDepth()
    {
        const int OkayCount = 64;
        string okayJson = new string('[', OkayCount) + "2" + new string(']', OkayCount);
        int depth = 0;

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(okayJson))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);

            JsonElement cur = root;

            while (cur.ValueKind == JsonValueKind.Array)
            {
                Assert.AreEqual(1, cur.GetArrayLength());
                cur = cur[0];
                depth++;
            }

            Assert.AreEqual(JsonValueKind.Number, cur.ValueKind);
            Assert.AreEqual(2, cur.GetInt32());
            Assert.AreEqual(OkayCount, depth);
        }

        string badJson = $"[{okayJson}]";

        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(badJson));
    }

    [TestMethod]
    public void HonorReaderOptionsMaxDepth()
    {
        const int OkayCount = 65;
        string okayJson = new string('[', OkayCount) + "2" + new string(']', OkayCount);
        int depth = 0;

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = OkayCount }))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);

            JsonElement cur = root;

            while (cur.ValueKind == JsonValueKind.Array)
            {
                Assert.AreEqual(1, cur.GetArrayLength());
                cur = cur[0];
                depth++;
            }

            Assert.AreEqual(JsonValueKind.Number, cur.ValueKind);
            Assert.AreEqual(2, cur.GetInt32());
            Assert.AreEqual(OkayCount, depth);
        }

        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = 32 }));
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(okayJson));
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = 0 }));
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = 64 }));
    }

    [TestMethod]
    public void LargeMaxDepthIsAllowed()
    {
        // MaxDepthOverflow * 8 > int.MaxValue
        const int MaxDepthOverflow = 1 << 28; //268_435_456;

        string okayJson = "[]";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = MaxDepthOverflow }))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        }

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(okayJson, new JsonDocumentOptions { MaxDepth = int.MaxValue }))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
        }
    }

    [TestMethod]
    public void EnableComments()
    {
        string json = "3";
        byte[] utf8 = Encoding.UTF8.GetBytes(json);

        var readerOptions = new JsonReaderOptions
        {
            CommentHandling = JsonCommentHandling.Allow,
        };

        AssertEx.ThrowsExactly<ArgumentException>(
            "reader",
            () =>
            {
                var state = new JsonReaderState(readerOptions);
                var reader = new Utf8JsonReader(utf8, isFinalBlock: false, state);
                ParsedJsonDocument<JsonElement>.ParseValue(ref reader);
            });

        AssertEx.ThrowsExactly<ArgumentException>(
            "reader",
            () =>
            {
                var state = new JsonReaderState(readerOptions);
                var reader = new Utf8JsonReader(utf8, isFinalBlock: false, state);
                ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out _);
            });
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow((int)JsonCommentHandling.Allow)]
    [DataRow(3)]
    [DataRow(byte.MaxValue)]
    [DataRow(byte.MaxValue + 3)] // Other values, like byte.MaxValue + 1 overflows to 0 (i.e. JsonCommentHandling.Disallow), which is valid.
    [DataRow(byte.MaxValue + 4)]
    public void ReadCommentHandlingDoesNotSupportAllow(int enumValue)
    {
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonDocumentOptions
        {
            CommentHandling = (JsonCommentHandling)enumValue
        });
    }

    [TestMethod]
    public void ReadCommentHandlingWorksForNegativeOverflow()
    {
        var options = new JsonDocumentOptions
        {
            CommentHandling = unchecked((JsonCommentHandling)(-255))
        };
        Assert.AreEqual(JsonCommentHandling.Skip, options.CommentHandling);

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("/* some comment */{ }", options))
        {
            Assert.AreEqual(JsonValueKind.Object, doc.RootElement.ValueKind);
        }
    }

    [TestMethod]
    [DataRow(-1)]
    public void TestDepthInvalid(int depth)
    {
        AssertEx.ThrowsExactly<ArgumentOutOfRangeException>("value", () => new JsonDocumentOptions
        {
            MaxDepth = depth
        });
    }

    [TestMethod]
    [DataRow("{ \"object\": { \"1-1\": null, \"1-2\": \"12\", }, \"array\": [ 4, 8, 1, 9, 2 ] }")]
    [DataRow("[ 5, 4, 3, 2, 1, ]")]
    [DataRow("{ \"shape\": \"square\", \"size\": 10, \"color\": \"green\", }")]
    public void TrailingCommas(string json)
    {
        var options = new JsonDocumentOptions
        {
            AllowTrailingCommas = true
        };

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json, options))
        {
            Assert.AreEqual(json, doc.RootElement.GetRawText());
        }
        Assert.Throws<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(json));
    }

    [TestMethod]
    public void GetPropertyByNullName()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{ }"))
        {
            AssertEx.ThrowsExactly<ArgumentNullException>(
                "propertyName",
                () => doc.RootElement.GetProperty((string)null));

            AssertEx.ThrowsExactly<ArgumentNullException>(
                "propertyName",
                () => doc.RootElement.TryGetProperty((string)null, out _));
        }
    }

    [TestMethod]
    public void GetPropertyInvalidUtf16()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"value\"}"))
        {
            Assert.ThrowsExactly<ArgumentException>(() => doc.RootElement.GetProperty("unpaired\uDFFE"));

            Assert.ThrowsExactly<ArgumentException>(() => doc.RootElement.TryGetProperty("unpaired\uDFFE", out _));
        }
    }

    [TestMethod]
    [DataRow("short")]
    [DataRow("thisValueIsLongerThan86CharsSoWeDeferTheTranscodingUntilWeFindAViableCandidateAsAPropertyMatch")]
    public void GetPropertyFindsLast(string propertyName)
    {
        string json = $"{{ \"{propertyName}\": 1, \"{propertyName}\": 2, \"nope\": -1, \"{propertyName}\": 3 }}";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;
            byte[] utf8PropertyName = Encoding.UTF8.GetBytes(propertyName);

            Assert.AreEqual(3, root.GetProperty(propertyName).GetInt32());
            Assert.AreEqual(3, root.GetProperty(propertyName.AsSpan()).GetInt32());
            Assert.AreEqual(3, root.GetProperty(utf8PropertyName).GetInt32());

            JsonElement matchedProperty;
            Assert.IsTrue(root.TryGetProperty(propertyName, out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(propertyName.AsSpan(), out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(utf8PropertyName, out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
        }
    }

    [TestMethod]
    [DataRow("short")]
    [DataRow("thisValueIsLongerThan86CharsSoWeDeferTheTranscodingUntilWeFindAViableCandidateAsAPropertyMatch")]
    public void GetPropertyFindsLast_WithEscaping(string propertyName)
    {
        string first = $"\\u{(int)propertyName[0]:X4}{propertyName.Substring(1)}";
        var builder = new StringBuilder(propertyName.Length * 6);

        int half = propertyName.Length / 2;
        builder.Append(propertyName.Substring(0, half));

        for (int i = half; i < propertyName.Length; i++)
        {
            builder.AppendFormat("\\u{0:X4}", (int)propertyName[i]);
        }

        builder.Append('2');
        string second = builder.ToString();
        builder.Clear();

        for (int i = 0; i < propertyName.Length; i++)
        {
            if ((i & 1) == 0)
            {
                builder.Append(propertyName[i]);
            }
            else
            {
                builder.AppendFormat("\\u{0:X4}", (int)propertyName[i]);
            }
        }

        builder.Append('3');
        string third = builder.ToString();

        string pn2 = propertyName + "2";
        string pn3 = propertyName + "3";

        string json =
            $"{{ \"{propertyName}\": 0, \"{first}\": 1, \"{pn2}\": 0, \"{second}\": 2, \"{pn3}\": 0, \"nope\": -1, \"{third}\": 3 }}";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;
            byte[] utf8PropertyName = Encoding.UTF8.GetBytes(propertyName);
            byte[] utf8PropertyName2 = Encoding.UTF8.GetBytes(pn2);
            byte[] utf8PropertyName3 = Encoding.UTF8.GetBytes(pn3);

            Assert.AreEqual(1, root.GetProperty(propertyName).GetInt32());
            Assert.AreEqual(1, root.GetProperty(propertyName.AsSpan()).GetInt32());
            Assert.AreEqual(1, root.GetProperty(utf8PropertyName).GetInt32());

            Assert.AreEqual(2, root.GetProperty(pn2).GetInt32());
            Assert.AreEqual(2, root.GetProperty(pn2.AsSpan()).GetInt32());
            Assert.AreEqual(2, root.GetProperty(utf8PropertyName2).GetInt32());

            Assert.AreEqual(3, root.GetProperty(pn3).GetInt32());
            Assert.AreEqual(3, root.GetProperty(pn3.AsSpan()).GetInt32());
            Assert.AreEqual(3, root.GetProperty(utf8PropertyName3).GetInt32());

            JsonElement matchedProperty;
            Assert.IsTrue(root.TryGetProperty(propertyName, out matchedProperty));
            Assert.AreEqual(1, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(propertyName.AsSpan(), out matchedProperty));
            Assert.AreEqual(1, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(utf8PropertyName, out matchedProperty));
            Assert.AreEqual(1, matchedProperty.GetInt32());

            Assert.IsTrue(root.TryGetProperty(pn2, out matchedProperty));
            Assert.AreEqual(2, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(pn2.AsSpan(), out matchedProperty));
            Assert.AreEqual(2, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(utf8PropertyName2, out matchedProperty));
            Assert.AreEqual(2, matchedProperty.GetInt32());

            Assert.IsTrue(root.TryGetProperty(pn3, out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(pn3.AsSpan(), out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
            Assert.IsTrue(root.TryGetProperty(utf8PropertyName3, out matchedProperty));
            Assert.AreEqual(3, matchedProperty.GetInt32());
        }
    }

    [TestMethod]
    public void GetRawText()
    {
        const string json =
// Don't let there be a newline before the first embedded quote,
// because the index would change across CRLF vs LF compile environments.
@"{  ""  weird  property  name""
                  :
       {
         ""nested"":
         [ 1, 2, 3
,
4, 5, 6 ],
        ""also"" : 3
  },
  ""number"": 1.02e+4,
  ""bool"": false,
  ""n\u0075ll"": null,
  ""multiLineArray"":

[

0,
1,
2,

    3

],
  ""string"":

""Aren't string just the greatest?\r\nNot a terminating quote: \""     \r   \n   \t  \\   ""
}";

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            Assert.AreEqual(6, doc.RootElement.GetPropertyCount());
            ObjectEnumerator<JsonElement> enumerator = doc.RootElement.EnumerateObject();
            Assert.IsTrue(enumerator.MoveNext(), "Move to first property");
            JsonProperty<JsonElement> property = enumerator.Current;

            Assert.AreEqual("  weird  property  name", property.Name);
            string rawText = property.ToString();
            int crCount = rawText.Count(c => c == '\r');
            Assert.AreEqual(2, property.Value.GetPropertyCount());
            Assert.AreEqual(128 + crCount, rawText.Length);
            Assert.AreEqual('\"', rawText[0]);
            Assert.AreEqual(' ', rawText[1]);
            Assert.AreEqual('}', rawText[rawText.Length - 1]);
            Assert.AreEqual(json.Substring(json.IndexOf('\"'), rawText.Length), rawText);

            Assert.IsTrue(enumerator.MoveNext(), "Move to number property");
            property = enumerator.Current;

            Assert.AreEqual("number", property.Name);
            Assert.AreEqual("\"number\": 1.02e+4", property.ToString());
            Assert.AreEqual(10200.0, property.Value.GetDouble());
            Assert.AreEqual("1.02e+4", property.Value.GetRawText());

            Assert.IsTrue(enumerator.MoveNext(), "Move to bool property");
            property = enumerator.Current;

            Assert.AreEqual("bool", property.Name);
            Assert.IsFalse(property.Value.GetBoolean());
            Assert.AreEqual("false", property.Value.GetRawText());
            Assert.AreEqual(bool.FalseString, property.Value.ToString());

            Assert.IsTrue(enumerator.MoveNext(), "Move to null property");
            property = enumerator.Current;

            Assert.AreEqual("null", property.Name);
            Assert.AreEqual("null", property.Value.GetRawText());
            Assert.AreEqual(string.Empty, property.Value.ToString());
            Assert.AreEqual("\"n\\u0075ll\": null", property.ToString());

            Assert.IsTrue(enumerator.MoveNext(), "Move to multiLineArray property");
            property = enumerator.Current;

            Assert.AreEqual("multiLineArray", property.Name);
            Assert.AreEqual(4, property.Value.GetArrayLength());
            rawText = property.Value.GetRawText();
            Assert.AreEqual('[', rawText[0]);
            Assert.AreEqual(']', rawText[rawText.Length - 1]);
            StringAssert.Contains(rawText, "3");
            StringAssert.Contains(rawText, "\n");

            Assert.IsTrue(enumerator.MoveNext(), "Move to string property");
            property = enumerator.Current;

            Assert.AreEqual("string", property.Name);
            rawText = property.Value.GetRawText();
            Assert.AreEqual('\"', rawText[0]);
            Assert.AreEqual('\"', rawText[rawText.Length - 1]);
            string strValue = property.Value.GetString();
            int newlineIdx = strValue.IndexOf('\r');
            int colonIdx = strValue.IndexOf(':');
            int escapedQuoteIdx = colonIdx + 2;
            Assert.AreEqual(rawText.Substring(1, newlineIdx), strValue.Substring(0, newlineIdx));
            Assert.AreEqual('\\', rawText[escapedQuoteIdx + 3]);
            Assert.AreEqual('\"', rawText[escapedQuoteIdx + 4]);
            Assert.AreEqual('\"', strValue[escapedQuoteIdx]);
            StringAssert.Contains(strValue, "\r");
            StringAssert.Contains(rawText, @"\r");
            string valueText = rawText;
            rawText = property.ToString();
            Assert.AreEqual('\"', rawText[0]);
            Assert.AreEqual('\"', rawText[rawText.Length - 1]);
            Assert.AreNotEqual(valueText, rawText);
            Assert.EndsWith(valueText, rawText);

            Assert.IsFalse(enumerator.MoveNext(), "Move past the last property");
        }
    }

    [TestMethod]
    public void ArrayEnumeratorIndependentWalk()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[0, 1, 2, 3, 4, 5]"))
        {
            JsonElement root = doc.RootElement;
            ArrayEnumerator<JsonElement> structEnumerable = root.EnumerateArray();
            IEnumerable<JsonElement> strongBoxedEnumerable = root.EnumerateArray();
            IEnumerable weakBoxedEnumerable = root.EnumerateArray();

            ArrayEnumerator<JsonElement> structEnumerator = structEnumerable.GetEnumerator();
            IEnumerator<JsonElement> strongBoxedEnumerator = strongBoxedEnumerable.GetEnumerator();
            IEnumerator weakBoxedEnumerator = weakBoxedEnumerable.GetEnumerator();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(0, structEnumerator.Current.GetInt32());
            Assert.AreEqual(0, strongBoxedEnumerator.Current.GetInt32());
            Assert.AreEqual(0, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(1, structEnumerator.Current.GetInt32());
            Assert.AreEqual(1, strongBoxedEnumerator.Current.GetInt32());
            Assert.AreEqual(1, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            int test = 0;

            foreach (JsonElement element in structEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement element in structEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement element in strongBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement element in strongBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement element in weakBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement element in weakBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            structEnumerator.Reset();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(0, structEnumerator.Current.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(1, structEnumerator.Current.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(2, structEnumerator.Current.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(3, structEnumerator.Current.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(2, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(4, structEnumerator.Current.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual(5, structEnumerator.Current.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(3, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(2, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(4, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(5, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(3, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(4, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(5, ((JsonElement)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());
            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
        }
    }

    [TestMethod]
    public void DefaultArrayEnumeratorDoesNotThrow()
    {
        ArrayEnumerator<JsonElement> enumerable = default;
        ArrayEnumerator<JsonElement> enumerator = enumerable.GetEnumerator();
        ArrayEnumerator<JsonElement> defaultEnumerator = default;

        Assert.AreEqual(JsonValueKind.Undefined, enumerable.Current.ValueKind);
        Assert.AreEqual(JsonValueKind.Undefined, enumerator.Current.ValueKind);

        Assert.IsFalse(enumerable.MoveNext());
        Assert.IsFalse(enumerable.MoveNext());
        Assert.IsFalse(defaultEnumerator.MoveNext());
    }

    [TestMethod]
    public void ObjectEnumeratorIndependentWalk()
    {
        const string json = @"
{
  ""name0"": 0,
  ""name1"": 1,
  ""name2"": 2,
  ""name3"": 3,
  ""name4"": 4,
  ""name5"": 5
}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;
            ObjectEnumerator<JsonElement> structEnumerable = root.EnumerateObject();
            IEnumerable<JsonProperty<JsonElement>> strongBoxedEnumerable = root.EnumerateObject();
            IEnumerable weakBoxedEnumerable = root.EnumerateObject();

            ObjectEnumerator<JsonElement> structEnumerator = structEnumerable.GetEnumerator();
            IEnumerator<JsonProperty<JsonElement>> strongBoxedEnumerator = strongBoxedEnumerable.GetEnumerator();
            IEnumerator weakBoxedEnumerator = weakBoxedEnumerable.GetEnumerator();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual("name0", structEnumerator.Current.Name);
            Assert.AreEqual(0, structEnumerator.Current.Value.GetInt32());
            Assert.AreEqual("name0", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(0, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.AreEqual("name0", ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(0, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(1, structEnumerator.Current.Value.GetInt32());
            Assert.AreEqual(1, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.AreEqual(1, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            int test = 0;

            foreach (JsonProperty<JsonElement> property in structEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement> property in structEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement> property in strongBoxedEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement> property in strongBoxedEnumerable)
            {
                string propertyName = property.Name;
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;

                // Subsequent read of the same JsonProperty should return an equal string
                string propertyName2 = property.Name;
                Assert.AreEqual(propertyName, propertyName2);
            }

            test = 0;

            foreach (JsonProperty<JsonElement> property in weakBoxedEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement> property in weakBoxedEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            structEnumerator.Reset();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name0", structEnumerator.Current.Name);
            Assert.AreEqual(0, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name1", structEnumerator.Current.Name);
            Assert.AreEqual(1, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name2", structEnumerator.Current.Name);
            Assert.AreEqual(2, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name3", structEnumerator.Current.Name);
            Assert.AreEqual(3, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name2", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(2, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name4", structEnumerator.Current.Name);
            Assert.AreEqual(4, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.AreEqual("name5", structEnumerator.Current.Name);
            Assert.AreEqual(5, structEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name3", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(3, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name2", ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(2, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name4", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(4, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name5", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(5, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name3", ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(3, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name4", ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(4, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name5", ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(5, ((JsonProperty<JsonElement>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());
            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
        }
    }

    [TestMethod]
    public void DefaultObjectEnumeratorDoesNotThrow()
    {
        ObjectEnumerator<JsonElement> enumerable = default;
        ObjectEnumerator<JsonElement> enumerator = enumerable.GetEnumerator();
        ObjectEnumerator<JsonElement> defaultEnumerator = default;

        Assert.AreEqual(JsonValueKind.Undefined, enumerable.Current.Value.ValueKind);
        Assert.AreEqual(JsonValueKind.Undefined, enumerator.Current.Value.ValueKind);

        Assert.ThrowsExactly<InvalidOperationException>(() => enumerable.Current.Name);
        Assert.ThrowsExactly<InvalidOperationException>(() => enumerator.Current.Name);

        Assert.IsFalse(enumerable.MoveNext());
        Assert.IsFalse(enumerable.MoveNext());
        Assert.IsFalse(defaultEnumerator.MoveNext());
    }

    [TestMethod]
    public void ReadNestedObject()
    {
        const string json = @"
{
  ""first"":
  {
    ""true"": true,
    ""false"": false,
    ""null"": null,
    ""int"": 3,
    ""nearlyPi"": 3.14159,
    ""text"": ""This is some text that does not end... <EOT>""
  },
  ""second"":
  {
    ""blub"": { ""bool"": true },
    ""glub"": { ""bool"": false }
  }
}";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        {
            JsonElement root = doc.RootElement;
            Assert.AreEqual(JsonValueKind.Object, root.ValueKind);

            Assert.IsTrue(root.GetProperty("first").GetProperty("true").GetBoolean());
            Assert.IsFalse(root.GetProperty("first").GetProperty("false").GetBoolean());
            Assert.AreEqual(JsonValueKind.Null, root.GetProperty("first").GetProperty("null").ValueKind);
            Assert.AreEqual(3, root.GetProperty("first").GetProperty("int").GetInt32());
            Assert.AreEqual(3.14159f, root.GetProperty("first").GetProperty("nearlyPi").GetSingle());
            Assert.AreEqual("This is some text that does not end... <EOT>", root.GetProperty("first").GetProperty("text").GetString());

            Assert.IsTrue(root.GetProperty("second").GetProperty("blub").GetProperty("bool").GetBoolean());
            Assert.IsFalse(root.GetProperty("second").GetProperty("glub").GetProperty("bool").GetBoolean());
        }
    }

    [TestMethod]
    public void ParseNull()
    {
        AssertEx.ThrowsExactly<ArgumentNullException>(
            "json",
            () => ParsedJsonDocument<JsonElement>.Parse((string)null));

        AssertEx.ThrowsExactly<ArgumentNullException>(
            "utf8Json",
            () => ParsedJsonDocument<JsonElement>.Parse((Stream)null));

        // This synchronously throws the ArgumentNullException
        AssertEx.ThrowsExactly<ArgumentNullException>(
            "utf8Json",
            () => { ParsedJsonDocument<JsonElement>.ParseAsync(null); });
    }

    [TestMethod]
    public void ParseDefaultReader()
    {
        Assert.Throws<JsonException>(() =>
        {
            Utf8JsonReader reader = default;
            ParsedJsonDocument<JsonElement>.ParseValue(ref reader);
        });

        {
            Utf8JsonReader reader = default;
            Assert.IsFalse(ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out ParsedJsonDocument<JsonElement> document));
            Assert.IsNull(document);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtNumber(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(
            scenario,
            "963258741012365478965412325874125896325874123654789",
            JsonTokenType.Number,
            segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtString(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(
            scenario,
            "\"Now is the time for all good men to come to the aid of their country\"",
            JsonTokenType.String,
            segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtObject(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(
            scenario,
            "{ \"hello\": 3, \"world\": false }",
            JsonTokenType.StartObject,
            segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtArray(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(
            scenario,
            "[ { \"hello\": 3, \"world\": false }, 5, 2, 1, \"hello\", null, false, true ]",
            JsonTokenType.StartArray,
            segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtTrue(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(scenario, "true", JsonTokenType.True, segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtFalse(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(scenario, "false", JsonTokenType.False, segmentCount);
    }

    [TestMethod]
    [DynamicData(nameof(ParseReaderSuccessfulCases))]
    public void ParseReaderAtNull(ParseReaderScenario scenario, int segmentCount)
    {
        ParseReaderInScenario(scenario, "null", JsonTokenType.Null, segmentCount);
    }

    private static void ParseReaderInScenario(
        ParseReaderScenario scenario,
        string valueJson,
        JsonTokenType tokenType,
        int segmentCount)
    {
        switch (scenario)
        {
            case ParseReaderScenario.NotStarted:
            case ParseReaderScenario.StartAtFirstToken:
                ParseReaderAtStart(
                    scenario == ParseReaderScenario.StartAtFirstToken,
                    valueJson,
                    tokenType,
                    segmentCount);
                break;

            case ParseReaderScenario.StartAtNestedValue:
                ParseReaderAtNestedValue(valueJson, tokenType, segmentCount);
                break;

            case ParseReaderScenario.StartAtPropertyName:
                ParseReaderAtPropertyName(valueJson, tokenType, segmentCount);
                break;
        }
    }

    private static void ParseReaderAtStart(
        bool readFirst,
        string valueJson,
        JsonTokenType tokenType,
        int segmentCount)
    {
        string json = $"      {valueJson}   5";
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8, segmentCount, state);

        if (readFirst)
        {
            Assert.IsTrue(reader.Read(), "Move to first token");
        }

        using (var document = ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
        {
            Assert.AreEqual(valueJson, document.RootElement.GetRawText());
        }

        Exception ex;
        long position = reader.BytesConsumed;

        try
        {
            reader.Read();
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        BuildSegmentedReader(out reader, utf8.AsMemory((int)position), 0, state, true);

        Assert.IsTrue(reader.Read(), "Read trailing number");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
    }

    private static void ParseReaderAtNestedValue(
        string valueJson,
        JsonTokenType tokenType,
        int segmentCount)
    {
        // Open-ended value (missing final ])
        string json = $"[ 0, 1, 2, 3, 4, {valueJson}     , 6, 7, 8, 9 ";
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8, segmentCount, state);

        Assert.IsTrue(reader.Read(), "Read [");
        Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 0");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 1");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 2");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 3");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 4");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);

        Assert.IsTrue(reader.Read(), "Read desired value");
        Assert.AreEqual(tokenType, reader.TokenType);
        long currentPosition = reader.BytesConsumed;

        using (var document = ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
        {
            Assert.AreEqual(valueJson, document.RootElement.GetRawText());
        }

        switch (tokenType)
        {
            case JsonTokenType.StartArray:
                Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);
                Assert.IsTrue(reader.BytesConsumed >= currentPosition + 1 && reader.BytesConsumed <= long.MaxValue);
                break;

            case JsonTokenType.StartObject:
                Assert.AreEqual(JsonTokenType.EndObject, reader.TokenType);
                Assert.IsTrue(reader.BytesConsumed >= currentPosition + 1 && reader.BytesConsumed <= long.MaxValue);
                break;

            default:
                Assert.AreEqual(tokenType, reader.TokenType);
                Assert.AreEqual(currentPosition, reader.BytesConsumed);
                break;
        }

        Assert.IsTrue(reader.Read(), "Read 6");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 7");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 8");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 9");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);

        Assert.IsFalse(reader.Read());
    }

    private static void ParseReaderAtPropertyName(
        string valueJson,
        JsonTokenType tokenType,
        int segmentCount)
    {
        // Open-ended value
        string json = $"{{ \"this\": [ {{ \"is\": {{ \"the\": {{ \"target\": {valueJson}    , \"capiche\" : [ 6, 7, 8, 9 ] ";
        byte[] utf8 = Encoding.UTF8.GetBytes(json);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8, segmentCount, state);

        Assert.IsTrue(reader.Read(), "Read root object start");
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"this\" property name");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"this\" StartArray");
        Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"this\" StartObject");
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"is\" property name");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"is\" StartObject");
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"the\" property name");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"the\" StartObject");
        Assert.AreEqual(JsonTokenType.StartObject, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"target\" property name");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);

        long currentPosition = reader.BytesConsumed;

        using (var document = ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
        {
            Assert.AreEqual(valueJson, document.RootElement.GetRawText());
        }

        // The reader will have moved.
        Assert.IsTrue(reader.BytesConsumed >= currentPosition + 1 && reader.BytesConsumed <= long.MaxValue);

        switch (tokenType)
        {
            case JsonTokenType.StartArray:
                Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);
                break;

            case JsonTokenType.StartObject:
                Assert.AreEqual(JsonTokenType.EndObject, reader.TokenType);
                break;

            default:
                Assert.AreEqual(tokenType, reader.TokenType);
                break;
        }

        Assert.IsTrue(reader.Read(), "Read \"capiche\" property name");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"capiche\" StartArray");
        Assert.AreEqual(JsonTokenType.StartArray, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 6");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 7");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 8");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read 9");
        Assert.AreEqual(JsonTokenType.Number, reader.TokenType);
        Assert.IsTrue(reader.Read(), "Read \"capiche\" EndArray");
        Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);

        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    [DataRow(JsonTokenType.EndObject)]
    [DataRow(JsonTokenType.EndArray)]
    public void ParseReaderAtInvalidStart(JsonTokenType tokenType)
    {
        string jsonString = tokenType switch
        {
            JsonTokenType.EndObject => "[ { } ]",
            JsonTokenType.EndArray => "[ [ ] ]",
            _ => throw new ArgumentOutOfRangeException(nameof(tokenType)),
        };
        byte[] jsonUtf8 = Encoding.UTF8.GetBytes(jsonString);
        JsonReaderState state = default;

        var reader = new Utf8JsonReader(
            jsonUtf8,
            isFinalBlock: false,
            state);

        Assert.IsTrue(reader.Read(), "Move to first [");
        Assert.IsTrue(reader.Read(), "Move to internal start");
        Assert.IsTrue(reader.Read(), "Move to internal end");

        Exception ex;

        long initialPosition = reader.BytesConsumed;

        try
        {
            using (ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
            {
            }

            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(initialPosition, reader.BytesConsumed);

        Assert.IsFalse(ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out ParsedJsonDocument<JsonElement> doc));
        Assert.IsNull(doc);
        Assert.AreEqual(initialPosition, reader.BytesConsumed);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(15)]
    public void ParseReaderThrowsWhileReading(int segmentCount)
    {
        const string jsonString = "[ { \"this\": { \"is\": [ invalid ] } } ]";
        byte[] utf8Json = Encoding.UTF8.GetBytes(jsonString);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8Json, segmentCount, state);

        Assert.IsTrue(reader.Read(), "Read StartArray");
        Assert.IsTrue(reader.Read(), "Read StartObject");
        Assert.IsTrue(reader.Read(), "Read \"this\" PropertyName");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        long startPosition = reader.BytesConsumed;

        Exception ex;

        try
        {
            using (ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
            {
            }

            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);

        ParsedJsonDocument<JsonElement> doc = null;

        try
        {
            ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out doc);
            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);
        Assert.IsNull(doc);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(15)]
    public void ParseReaderTerminatesWhileReading(int segmentCount)
    {
        const string jsonString = "[ { \"this\": { \"is\": [ ";
        byte[] utf8Json = Encoding.UTF8.GetBytes(jsonString);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8Json, segmentCount, state);

        Assert.IsTrue(reader.Read(), "Read StartArray");
        Assert.IsTrue(reader.Read(), "Read StartObject");
        Assert.IsTrue(reader.Read(), "Read \"this\" PropertyName");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        long startPosition = reader.BytesConsumed;

        Exception ex;

        try
        {
            using (ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
            {
            }

            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);

        Assert.IsFalse(ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out ParsedJsonDocument<JsonElement> doc));
        Assert.IsNull(doc);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(15)]
    public void ParseReaderTerminatesAfterPropertyName(int segmentCount)
    {
        const string jsonString = "[ { \"this\": ";
        byte[] utf8Json = Encoding.UTF8.GetBytes(jsonString);
        JsonReaderState state = default;

        BuildSegmentedReader(out Utf8JsonReader reader, utf8Json, segmentCount, state);

        Assert.IsTrue(reader.Read(), "Read StartArray");
        Assert.IsTrue(reader.Read(), "Read StartObject");
        Assert.IsTrue(reader.Read(), "Read \"this\" PropertyName");
        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        long startPosition = reader.BytesConsumed;

        Exception ex;

        try
        {
            using (ParsedJsonDocument<JsonElement>.ParseValue(ref reader))
            {
            }

            ex = null;
        }
        catch (Exception e)
        {
            ex = e;
        }

        Assert.IsNotNull(ex);
        AssertEx.IsAssignableFrom<JsonException>(ex);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);

        Assert.IsFalse(ParsedJsonDocument<JsonElement>.TryParseValue(ref reader, out ParsedJsonDocument<JsonElement> doc));
        Assert.IsNull(doc);

        Assert.AreEqual(JsonTokenType.PropertyName, reader.TokenType);
        Assert.AreEqual(startPosition, reader.BytesConsumed);
    }

    [TestMethod]
    public void ParseValue_AllowMultipleValues_TrailingJson()
    {
        var options = new JsonReaderOptions { AllowMultipleValues = true };
        var reader = new Utf8JsonReader("[null,false,42,{},[1]]             [43]"u8, options);

        using var doc1 = ParsedJsonDocument<JsonElement>.ParseValue(ref reader);
        Assert.AreEqual("[null,false,42,{},[1]]", doc1.RootElement.GetRawText());
        Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);

        Assert.IsTrue(reader.Read());
        using var doc2 = ParsedJsonDocument<JsonElement>.ParseValue(ref reader);
        Assert.AreEqual("[43]", doc2.RootElement.GetRawText());

        Assert.IsFalse(reader.Read());
    }

    [TestMethod]
    public void ParseValue_AllowMultipleValues_TrailingContent()
    {
        var options = new JsonReaderOptions { AllowMultipleValues = true };
        var reader = new Utf8JsonReader("[null,false,42,{},[1]]             <NotJson/>"u8, options);

        using var doc = ParsedJsonDocument<JsonElement>.ParseValue(ref reader);
        Assert.AreEqual("[null,false,42,{},[1]]", doc.RootElement.GetRawText());
        Assert.AreEqual(JsonTokenType.EndArray, reader.TokenType);

        JsonTestHelper.AssertThrows<JsonException>(ref reader, (ref reader) => reader.Read());
    }

    [TestMethod]
    [DataRow("""{ "foo" : [1], "test": false, "bar" : { "nested": 3 } }""", 3)]
    [DataRow("""{ "foo" : [1,2,3,4] }""", 1)]
    [DataRow("""{}""", 0)]
    [DataRow("""{ "foo" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }, "test": true, "foo2" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }}""", 3)]
    public void TestGetPropertyCount(string json, int expectedCount)
    {
        var element = JsonElement.ParseValue(json);
        Assert.AreEqual(expectedCount, element.GetPropertyCount());
    }

    [TestMethod]
    public void VerifyGetPropertyCountAndArrayLengthUsingEnumerateMethods()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ProjectLockJson))
        {
            CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(doc.RootElement);
        }

        static void CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(JsonElement elem)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                Assert.AreEqual(elem.EnumerateObject().Count(), elem.GetPropertyCount());
                foreach (JsonProperty<JsonElement> prop in elem.EnumerateObject())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(prop.Value);
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                Assert.AreEqual(elem.EnumerateArray().Count(), elem.GetArrayLength());
                foreach (JsonElement item in elem.EnumerateArray())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(item);
                }
            }
        }
    }

    [TestMethod]
    public void EnsureResizeSucceeds()
    {
        // This test increases coverage, so it's based on a lot of implementation detail,
        // to ensure that the otherwise untested blocks produce the right functional behavior.
        //
        // The initial database size is just over the number of bytes of UTF-8 data in the payload,
        // capped at 2^20 (unless the payload exceeds 2^22).
        //
        // Regrowth happens if the rented array (which may be bigger than we asked for) is not sufficient,
        // meaning tokens (on average) occur more often than every 12 bytes.
        //
        // The array pool (for bytes) returns power-of-two sizes.
        //
        // Conclusion: A resize will happen if a payload of 1MB+epsilon has tokens more often than every 12 bytes.
        //
        // Integer numbers as strings, padded to 4 with no whitespace in series in an array: 7x + 1.
        //  That would take 149797 integers.
        //
        // Padded to 5 (8x + 1) => 131072 integers.
        // Padded to 6 (9x + 1) => 116509 integers.
        //
        // At pad-to-6 tokens occur every 9 bytes, and we can represent values without repeat.

        const int NumberOfNumbers = (1024 * 1024 / 9) + 1;
        const int NumberOfBytes = 9 * NumberOfNumbers + 1;

        byte[] utf8Json = new byte[NumberOfBytes];
        utf8Json.AsSpan().Fill((byte)'"');
        utf8Json[0] = (byte)'[';

        Span<byte> valuesSpan = utf8Json.AsSpan(1);
        var format = StandardFormat.Parse("D6");

        for (int i = 0; i < NumberOfNumbers; i++)
        {
            // Just inside the quote
            Span<byte> curDest = valuesSpan.Slice(9 * i + 1);

            if (!Utf8Formatter.TryFormat(i, curDest, out int bytesWritten, format) || bytesWritten != 6)
            {
                throw new InvalidOperationException("" + i);
            }

            curDest[7] = (byte)',';
        }

        // Replace last comma with ]
        utf8Json[NumberOfBytes - 1] = (byte)']';

        using (var doc = ParsedJsonDocument<JsonElement>.Parse(utf8Json))
        {
            JsonElement root = doc.RootElement;
            int count = root.GetArrayLength();

            for (int i = 0; i < count; i++)
            {
                Assert.AreEqual(i, int.Parse(root[i].GetString()));
            }
        }
    }

    [TestMethod]
    public void ValueEquals_Null_TrueForNullFalseForEmpty()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("   null   "))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsTrue(jElement.ValueEquals((string)null));
            Assert.IsTrue(jElement.ValueEquals(default(ReadOnlySpan<char>)));
            Assert.IsTrue(jElement.ValueEquals((ReadOnlySpan<byte>)null));
            Assert.IsTrue(jElement.ValueEquals(default(ReadOnlySpan<byte>)));

            Assert.IsFalse(jElement.ValueEquals(Array.Empty<byte>()));
            Assert.IsFalse(jElement.ValueEquals(""));
            Assert.IsFalse(jElement.ValueEquals("".AsSpan()));
        }
    }

    [TestMethod]
    public void ValueEquals_EmptyJsonString_True()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"\""))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsTrue(jElement.ValueEquals(""));
            Assert.IsTrue(jElement.ValueEquals("".AsSpan()));
            Assert.IsTrue(jElement.ValueEquals(default(ReadOnlySpan<char>)));
            Assert.IsTrue(jElement.ValueEquals(default(ReadOnlySpan<byte>)));
            Assert.IsTrue(jElement.ValueEquals(Array.Empty<byte>()));
        }
    }

    [TestMethod]
    public void ValueEquals_TestTextEqualsLargeMatch_True()
    {
        string lookup = new('a', 320);
        string jsonString = "\"" + lookup + "\"";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsTrue(jElement.ValueEquals((string)lookup));
            Assert.IsTrue(jElement.ValueEquals((ReadOnlySpan<char>)lookup.AsSpan()));
        }
    }

    [TestMethod]
    [DataRow("\"conne\\u0063tionId\"", "connectionId")]
    [DataRow("\"connectionId\"", "connectionId")]
    [DataRow("\"123\"", "123")]
    [DataRow("\"My name is \\\"Ahson\\\"\"", "My name is \"Ahson\"")]
    public void ValueEquals_JsonTokenStringType_True(string jsonString, string expected)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsTrue(jElement.ValueEquals(expected));
            Assert.IsTrue(jElement.ValueEquals(expected.AsSpan()));
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(expected);
            Assert.IsTrue(jElement.ValueEquals(expectedGetBytes));
        }
    }

    [TestMethod]
    [DataRow("\"conne\\u0063tionId\"", "c")]
    public void ValueEquals_DestinationTooSmallComparesEscaping_False(string jsonString, string other)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsFalse(jElement.ValueEquals(other));
            Assert.IsFalse(jElement.ValueEquals(other.AsSpan()));
            byte[] otherGetBytes = Encoding.UTF8.GetBytes(other);
            Assert.IsFalse(jElement.ValueEquals(otherGetBytes));
        }
    }

    [TestMethod]
    [DataRow("\"hello\"", new char[1] { (char)0xDC01 })]    // low surrogate - invalid
    [DataRow("\"hello\"", new char[1] { (char)0xD801 })]    // high surrogate - missing pair
    public void InvalidUTF16Search(string jsonString, char[] lookup)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsFalse(jElement.ValueEquals(lookup));
        }
    }

    [TestMethod]
    [DataRow("\"connectionId\"", "\"conne\\u0063tionId\"")]
    [DataRow("\"conne\\u0063tionId\"", "connecxionId")] // intentionally making mismatch after escaped character
    [DataRow("\"conne\\u0063tionId\"", "bonnectionId")] // intentionally changing the expected starting character
    public void ValueEquals_JsonTokenStringType_False(string jsonString, string otherText)
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            Assert.IsFalse(jElement.ValueEquals(otherText));
            Assert.IsFalse(jElement.ValueEquals(otherText.AsSpan()));
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.IsFalse(jElement.ValueEquals(expectedGetBytes));
        }
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"\" : \"\"}")]
    [DataRow("{\"sample\" : \"\"}")]
    [DataRow("{\"sample\" : null}")]
    [DataRow("{\"sample\" : \"sample-value\"}")]
    [DataRow("{\"connectionId\" : \"123\"}")]
    public void ValueEquals_NotString_Throws(string jsonString)
    {
        const string ErrorMessage = "The requested operation requires an element of type 'String', but the target element has type 'Object'.";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway), ErrorMessage);
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway.AsSpan()), ErrorMessage);
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)), ErrorMessage);
        }
    }

    [TestMethod]
    public void NameEquals_Empty_Throws()
    {
        const string jsonString = "{\"\" : \"some-value\"}";
        const string ErrorMessage = "The requested operation requires an element of type 'String', but the target element has type 'Object'.";
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        {
            JsonElement jElement = doc.RootElement;
            const string ThrowsAnyway = "throws-anyway";
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway), ErrorMessage);
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway.AsSpan()), ErrorMessage);
            Assert.ThrowsExactly<InvalidOperationException>(() => jElement.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)), ErrorMessage);
        }
    }

    ////[TestMethod]
    [TestMethod]
    [TestCategory("outerloop")] // thread-safety / stress test
    public async Task GetString_ConcurrentUse_ThreadSafe()
    {
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        {
            JsonElement first = doc.RootElement.GetProperty("first");
            JsonElement last = doc.RootElement.GetProperty("last");

            const int Iters = 10_000;
            using (var gate = new Barrier(2))
            {
                await Task.WhenAll(
                    Task.Factory.StartNew(() =>
                    {
                        gate.SignalAndWait();
                        for (int i = 0; i < Iters; i++)
                        {
                            Assert.AreEqual("John", first.GetString());
                            Assert.IsTrue(first.ValueEquals("John"));
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default),
                    Task.Factory.StartNew(() =>
                    {
                        gate.SignalAndWait();
                        for (int i = 0; i < Iters; i++)
                        {
                            Assert.AreEqual("Smith", last.GetString());
                            Assert.IsTrue(last.ValueEquals("Smith"));
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
            }
        }
    }

    [TestMethod]
    public void CopySingleDimensionalArray()
    {
        const int length = 3;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        {
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif
            IJsonElement root = doc.RootElement;

            int written = 0;

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, out written));
            Assert.IsTrue(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, out written));
            Assert.IsTrue(outputByte.SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, out written));
            Assert.IsTrue(outputInt16.SequenceEqual(new short[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, out written));
            Assert.IsTrue(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, out written));
            Assert.IsTrue(outputInt32.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, out written));
            Assert.IsTrue(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, out written));
            Assert.IsTrue(outputInt64.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, out written));
            Assert.IsTrue(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, out written));
            Assert.IsTrue(outputDouble.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, out written));
            Assert.IsTrue(outputSingle.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, out written));
            Assert.IsTrue(outputDecimal.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

#if NET
            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, out written));
            Assert.IsTrue(outputInt128.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, out written));
            Assert.IsTrue(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, out written));
            Assert.IsTrue(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3]));
            Assert.AreEqual(length, written);
#endif
        }
    }

    [TestMethod]
    public void CopyArrayRank1()
    {
        const int length = 3;
        const int rank = 1;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        {
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif
            IJsonElement root = doc.RootElement;

            int written = 0;

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.IsTrue(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.IsTrue(outputByte.SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.IsTrue(outputInt16.SequenceEqual(new short[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.IsTrue(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.IsTrue(outputInt32.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.IsTrue(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.IsTrue(outputInt64.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.IsTrue(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.IsTrue(outputDouble.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.IsTrue(outputSingle.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.IsTrue(outputDecimal.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

#if NET
            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.IsTrue(outputInt128.SequenceEqual([1, 2, 3]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.IsTrue(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.IsTrue(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3]));
            Assert.AreEqual(length, written);
#endif
        }
    }

    [TestMethod]
    public void CopyArrayRank1_OutputTooShort()
    {
        const int length = 2;
        const int rank = 1;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        {
            IJsonElement root = doc.RootElement;
            int written;

            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif

            // The expected behavior is that TryCopyArrayOfRankTo returns false and written == 0
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.AreEqual(0, written);

#if NET
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.AreEqual(0, written);
#endif
        }
    }

    [TestMethod]
    public void CopyArrayRank2()
    {
        const int length = 9;
        const int rank = 2;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5,6],[7,8,9]]"))
        {
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif
            IJsonElement root = doc.RootElement;

            int written = 0;

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.IsTrue(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.IsTrue(outputByte.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.IsTrue(outputInt16.SequenceEqual(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.IsTrue(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.IsTrue(outputInt32.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.IsTrue(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.IsTrue(outputInt64.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.IsTrue(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.IsTrue(outputDouble.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.IsTrue(outputSingle.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.IsTrue(outputDecimal.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

#if NET
            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.IsTrue(outputInt128.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.IsTrue(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.IsTrue(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3, (Half)4, (Half)5, (Half)6, (Half)7, (Half)8, (Half)9]));
            Assert.AreEqual(length, written);
#endif
        }
    }

    [TestMethod]
    public void CopyArrayRank3Ragged()
    {
        const int length = 8;
        const int rank = 2;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5],[6,7,8]]"))
        {
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif
            IJsonElement root = doc.RootElement;

            int written = 0;

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.IsTrue(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.IsTrue(outputByte.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.IsTrue(outputInt16.SequenceEqual(new short[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.IsTrue(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.IsTrue(outputInt32.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.IsTrue(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.IsTrue(outputInt64.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.IsTrue(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.IsTrue(outputDouble.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.IsTrue(outputSingle.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.IsTrue(outputDecimal.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

#if NET
            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.IsTrue(outputInt128.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.IsTrue(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.AreEqual(length, written);

            Assert.IsTrue(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.IsTrue(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3, (Half)4, (Half)5, (Half)6, (Half)7, (Half)8]));
            Assert.AreEqual(length, written);
#endif
        }
    }

    [TestMethod]
    public void CopyArrayRank3Ragged_OutputTooShort()
    {
        const int length = 7; // The JSON array has 8 elements, so this is too short
        const int rank = 2;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5],[6,7,8]]"))
        {
            IJsonElement root = doc.RootElement;
            int written;

            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif

            // The expected behavior is that TryCopyArrayOfRankTo returns false, written == 0, and the array is not modified
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputByte, val => Assert.AreEqual(0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputInt16, val => Assert.AreEqual(0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputUInt16, val => Assert.AreEqual((ushort)0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputInt32, val => Assert.AreEqual(0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputUInt32, val => Assert.AreEqual((uint)0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputInt64, val => Assert.AreEqual(0L, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputUInt64, val => Assert.AreEqual((ulong)0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputDouble, val => Assert.AreEqual(0d, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputSingle, val => Assert.AreEqual(0f, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputDecimal, val => Assert.AreEqual(0m, val));

#if NET
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputInt128, val => Assert.AreEqual((Int128)0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputUInt128, val => Assert.AreEqual((UInt128)0, val));

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.AreEqual(0, written);
            AssertEx.All(outputHalf, val => Assert.AreEqual((Half)0, val));
#endif
        }
    }

    [TestMethod]
    public void CopyArrayNonIntegerType()
    {
        const int length = 8;
        const int rank = 2;
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,\"stringValue\"],[6,7,8]]"))
        {
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif

            IJsonElement root = doc.RootElement;

            int written = 0;

            const string ErrorMessage = "The requested operation requires an element of type 'Number', but the target element has type 'String'.";

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

#if NET
            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);

            Assert.ThrowsExactly<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            AssertEx.All(outputSbyte, val => Assert.AreEqual(0, val)); // Ensure the array is not modified
            Assert.AreEqual(0, written);
#endif
        }
    }

    private static void BuildSegmentedReader(
        out Utf8JsonReader reader,
        ReadOnlyMemory<byte> data,
        int segmentCount,
        scoped in JsonReaderState state,
        bool isFinalBlock = false)
    {
        if (segmentCount == 0)
        {
            reader = new Utf8JsonReader(
                data.Span,
                isFinalBlock,
                state);
        }
        else if (segmentCount == 1)
        {
            reader = new Utf8JsonReader(
                new ReadOnlySequence<byte>(data),
                isFinalBlock,
                state);
        }
        else
        {
            reader = new Utf8JsonReader(
                JsonTestHelper.SegmentInto(data, segmentCount),
                isFinalBlock,
                state);
        }
    }

    private static string GetExpectedConcat(TestCaseType testCaseType, string jsonString)
    {
        if (s_expectedConcat.TryGetValue(testCaseType, out string existing))
        {
            return existing;
        }

        TextReader reader = new StringReader(jsonString);
        return s_expectedConcat[testCaseType] = JsonTestHelper.NewtonsoftReturnStringHelper(reader);
    }

    private static string GetCompactJson(TestCaseType testCaseType, string jsonString)
    {
        if (s_compactJson.TryGetValue(testCaseType, out string existing))
        {
            return existing;
        }

        using (var jsonReader = new JsonTextReader(new StringReader(jsonString)) { MaxDepth = null })
        {
            jsonReader.FloatParseHandling = FloatParseHandling.Decimal;
            var jtoken = JToken.ReadFrom(jsonReader);
            var stringWriter = new StringWriter();

            using (var jsonWriter = new JsonTextWriter(stringWriter))
            {
                jtoken.WriteTo(jsonWriter);
                existing = stringWriter.ToString();
            }
        }

        return s_compactJson[testCaseType] = existing;
    }

    [TestMethod]
    public async Task VerifyMultiThreadedDispose()
    {
        Action<object> disposeAction = document => ((ParsedJsonDocument<JsonElement>)document).Dispose();

        // Create a bunch of parallel tasks that call Dispose several times on the same object.
        var tasks = new Task[100];
        int count = 0;
        for (int j = 0; j < 10; j++)
        {
            var document = ParsedJsonDocument<JsonElement>.Parse("123" + j);
            for (int i = 0; i < 10; i++)
            {
                tasks[count] = new Task(disposeAction, document);
                tasks[count].Start();

                count++;
            }
        }

        await Task.WhenAll(tasks);

        // When ArrayPool gets corrupted, the Rent method might return an already rented array, which is incorrect.
        // So we will rent as many arrays as calls to JsonElement.Dispose and check they are unique.
        // The minimum length that we ask for is a mirror of the size of the string passed to ParsedJsonDocument<JsonElement>.Parse.
        var uniqueAddresses = new HashSet<byte[]>();
        while (count > 0)
        {
            byte[] arr = ArrayPool<byte>.Shared.Rent(4);
            Assert.IsTrue(uniqueAddresses.Add(arr));
            count--;
        }
    }

    [TestMethod]
    public void DeserializeNullAsNullLiteral()
    {
        var jsonDocument = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.IsNotNull(jsonDocument);
        Assert.AreEqual(JsonValueKind.Null, jsonDocument.RootElement.ValueKind);
    }

    [TestMethod]
    public void CopySingleDimensionalArray_OutputTooShort()
    {
        const int length = 2;

        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        {
            IJsonElement root = doc.RootElement;
            int written;

            // All output arrays are too short (length 2, but need 3)
            sbyte[] outputSbyte = new sbyte[length];
            byte[] outputByte = new byte[length];
            short[] outputInt16 = new short[length];
            ushort[] outputUInt16 = new ushort[length];
            int[] outputInt32 = new int[length];
            uint[] outputUInt32 = new uint[length];
            long[] outputInt64 = new long[length];
            ulong[] outputUInt64 = new ulong[length];
            double[] outputDouble = new double[length];
            float[] outputSingle = new float[length];
            decimal[] outputDecimal = new decimal[length];
#if NET
            var outputInt128 = new Int128[length];
            var outputUInt128 = new UInt128[length];
            var outputHalf = new Half[length];
#endif

            // The expected behavior is that TryCopyTo returns false and written == 0
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, out written));
            Assert.AreEqual(0, written);

#if NET
            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, out written));
            Assert.AreEqual(0, written);

            Assert.IsFalse(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, out written));
            Assert.AreEqual(0, written);
#endif
        }
    }

    [TestMethod]
    [DataRow("foo", 3)]
    [DataRow("fo", 2)]
    [DataRow("f", 1)]
    [DataRow("", 0)]
    [DataRow("\uD83D\uDCA9", 1)]
    [DataRow("\uD83D\uDCA9\uD83D\uDCA9", 2)]
    public void GetUtf8StringLengthWorks(string inputString, int length)
    {
        Assert.AreEqual(length, JsonElementHelpers.GetUtf8StringLength(Encoding.UTF8.GetBytes(inputString)));
    }
}

public class ThrowOnReadStream : MemoryStream
{
    public ThrowOnReadStream(byte[] bytes) : base(bytes)
    {
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        throw new EndOfStreamException();
    }
}

public class ThrowOnCanSeekStream : MemoryStream
{
    public ThrowOnCanSeekStream(byte[] bytes) : base(bytes)
    {
    }

    public override bool CanSeek => throw new InsufficientMemoryException();
}
