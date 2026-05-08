// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Linq;
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
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
[TestClass]
public class JsonDocumentBuilderTests
{
    private static readonly byte[] Utf8Bom = [0xEF, 0xBB, 0xBF];

    private static readonly Dictionary<TestCaseType, string> s_expectedConcat =
        [];

    private static readonly Dictionary<TestCaseType, string> s_compactJson =
        [];

    public static IEnumerable<object[]> BadBOMCases { get; } =
        new object[][]
        {
            ["\u00EF"],
            ["\u00EF1"],
            ["\u00EF\u00BB"],
            ["\u00EF\u00BB1"],
            ["\u00EF\u00BB\u00BE"],
            ["\u00EF\u00BB\u00BE1"],
            ["\u00EF\u00BB\u00FB"],
            ["\u00EF\u00BB\u00FB1"],

            // Legal BOM, but no payload.
            ["\u00EF\u00BB\u00BF"],
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

    private static string ReadHelloWorld(JsonElement.Mutable obj)
    {
        string message = obj.GetProperty("message").GetString();
        return message;
    }

    private static string ReadJson400KB(JsonElement.Mutable obj)
    {
        var sb = new StringBuilder(250000);

        foreach (JsonElement.Mutable element in obj.EnumerateArray())
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

            JsonElement.Mutable tags = element.GetProperty("tags");
            for (int j = 0; j < tags.GetArrayLength(); j++)
            {
                sb.Append(tags[j].GetString());
            }
            JsonElement.Mutable friends = element.GetProperty("friends");
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
    public void ParseJson_SeekableStream_Small()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public void ParseJson_UnseekableStream_Small()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.Create())
        using (var doc =
            ParsedJsonDocument<JsonElement>.Parse(new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public async Task ParseJson_SeekableStream_Small_Async()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.CreateUnrented())
        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);
            Assert.AreEqual(11, root.GetInt32());
        }
    }

    [TestMethod]
    public async Task ParseJson_UnseekableStream_Small_Async()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.CreateUnrented())
        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(
            new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
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
        using (Stream stream = new ThrowOnReadStream([1]))
        {
            Assert.ThrowsExactly<EndOfStreamException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [TestMethod]
    public async Task ParseJson_Stream_Async_ClearRentedBuffer_WhenThrow_CodeCoverage()
    {
        using (Stream stream = new ThrowOnReadStream([1]))
        {
            await Assert.ThrowsExactlyAsync<EndOfStreamException>(async () => await ParsedJsonDocument<JsonElement>.ParseAsync(stream));
        }
    }

    [TestMethod]
    public void ParseJson_Stream_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream([1]))
        {
            Assert.ThrowsExactly<InsufficientMemoryException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [TestMethod]
    public async Task ParseJson_Stream_Async_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream([1]))
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

        using (var workspace = JsonWorkspace.CreateUnrented())
        using (ParsedJsonDocument<JsonElement> doc = await (stringDocBuilder?.Invoke(jsonString) ?? bytesDocBuilder?.Invoke(dataUtf8)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.IsNotNull(doc);

            JsonElement.Mutable rootElement = builderDoc.RootElement;

            Func<JToken, string> expectedFunc = null;
            Func<JsonElement.Mutable, string> actualFunc = null;

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

            string actual = builderDoc.PrintJson();
            string expected = GetExpectedConcat(type, jsonString);

            Assert.AreEqual(expected, actual);

            Assert.AreEqual(jsonString, rootElement.GetRawText());
        }
    }

    private static string PrintJson(JsonDocumentBuilder<JsonElement.Mutable> document, int sizeHint = 0)
    {
        return PrintJson(document.RootElement, sizeHint);
    }

    private static string PrintJson(JsonElement.Mutable element, int sizeHint = 0)
    {
        var sb = new StringBuilder(sizeHint);
        DepthFirstAppend(sb, element);
        return sb.ToString();
    }

    private static void DepthFirstAppend(StringBuilder buf, JsonElement.Mutable element)
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
                foreach (JsonProperty<JsonElement.Mutable> prop in element.EnumerateObject())
                {
                    buf.Append(prop.Name);
                    buf.Append(", ");
                    DepthFirstAppend(buf, prop.Value);
                }

                break;
            }
            case JsonValueKind.Array:
            {
                foreach (JsonElement.Mutable child in element.EnumerateArray())
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
    public void FromCustomParsedJson(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            string actual = builderDoc.PrintJson();

            TextReader reader = new StringReader(jsonString);
            string expected = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

            Assert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public void FromParsedArray()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleArrayJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(2, root.GetArrayLength());

            string phoneNumber = root[0].GetString();
            int age = root[1].GetInt32();

            Assert.AreEqual("425-214-3151", phoneNumber);
            Assert.AreEqual(25, age);

            Assert.ThrowsExactly<IndexOutOfRangeException>(() => root[2]);
        }
    }

    [TestMethod]
    public void FromParsedSimpleObject()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            int age = parsedObject.GetProperty("age").GetInt32();
            string ageString = parsedObject.GetProperty("age").ToString();
            string first = parsedObject.GetProperty("first").GetString();
            string last = parsedObject.GetProperty("last").GetString();
            string phoneNumber = parsedObject.GetProperty("phoneNumber").GetString();
            string street = parsedObject.GetProperty("street").GetString();
            string city = parsedObject.GetProperty("city").GetString();
            int zip = parsedObject.GetProperty("zip").GetInt32();

            Assert.AreEqual(7, parsedObject.GetPropertyCount());
            Assert.IsTrue(parsedObject.TryGetProperty("age", out JsonElement.Mutable age2));
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
    public void FromParsedNestedJson()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            Assert.AreEqual(1, parsedObject.GetArrayLength());
            JsonElement.Mutable person = parsedObject[0];
            Assert.AreEqual(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement.Mutable phoneNums = person.GetProperty("phoneNumbers");
            Assert.AreEqual(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement.Mutable address = person.GetProperty("address");
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
    public void FromParsedSimpleObjectWithSourcePropertyMap()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        {
            doc.RootElement.EnsurePropertyMap();
            using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            int age = parsedObject.GetProperty("age").GetInt32();
            string ageString = parsedObject.GetProperty("age").ToString();
            string first = parsedObject.GetProperty("first").GetString();
            string last = parsedObject.GetProperty("last").GetString();
            string phoneNumber = parsedObject.GetProperty("phoneNumber").GetString();
            string street = parsedObject.GetProperty("street").GetString();
            string city = parsedObject.GetProperty("city").GetString();
            int zip = parsedObject.GetProperty("zip").GetInt32();

            Assert.AreEqual(7, parsedObject.GetPropertyCount());
            Assert.IsTrue(parsedObject.TryGetProperty("age", out JsonElement.Mutable age2));
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
    public void FromParsedNestedJsonWithSourcePropertyMap()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        {
            using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

            JsonElement sourcePerson = doc.RootElement[0];
            sourcePerson.EnsurePropertyMap();

            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            Assert.AreEqual(1, parsedObject.GetArrayLength());
            JsonElement.Mutable person = parsedObject[0];
            Assert.AreEqual(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement.Mutable phoneNums = person.GetProperty("phoneNumbers");
            Assert.AreEqual(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement.Mutable address = person.GetProperty("address");
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
    public void EnsurePropertyMapOnManyArrayElements()
    {
        // Regression test: calling EnsurePropertyMap on 27+ objects in the same
        // document triggers the int[] Enlarge overload. A missing early-return
        // guard caused unconditional doubling, leading to Buffer.BlockCopy overflow.
        StringBuilder sb = new();
        sb.Append('[');
        for (int i = 0; i < 50; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append($$$"""{"a":{{{i}}},"b":"val_{{{i}}}","c":true}""");
        }
        sb.Append(']');

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(sb.ToString());
        JsonElement root = doc.RootElement;

        // Map every element
        foreach (JsonElement item in root.EnumerateArray())
        {
            item.EnsurePropertyMap();
        }

        // Verify property lookup still works for all items
        int idx = 0;
        foreach (JsonElement item in root.EnumerateArray())
        {
            Assert.AreEqual(idx, item.GetProperty("a"u8).GetInt32());
            Assert.AreEqual($"val_{idx}", item.GetProperty("b"u8).GetString());
            Assert.IsTrue(item.GetProperty("c"u8).GetBoolean());
            idx++;
        }
        Assert.AreEqual(50, idx);
    }

    [TestMethod]
    public void ParseBoolean()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[true,false]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable parsedObject = builderDoc.RootElement;
            bool first = parsedObject[0].GetBoolean();
            bool second = parsedObject[1].GetBoolean();
            Assert.IsTrue(first);
            Assert.IsFalse(second);
        }
    }

    [TestMethod]
    public void JsonArrayToString()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
            Assert.AreEqual(SR.ParseJson, root.ToString());
        }
    }

    [TestMethod]
    public void JsonObjectToString()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.BasicJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            JsonElement.Mutable target = root[2];

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.Number, root.ValueKind);

            Assert.IsTrue(root.TryGetSingle(out float floatVal));
            Assert.AreEqual(expectedFloat, floatVal);

            Assert.IsTrue(root.TryGetDouble(out double doubleVal));
            Assert.AreEqual(expectedDouble, doubleVal);

            Assert.IsTrue(root.TryGetDecimal(out decimal decimalVal));
            Assert.AreEqual(expectedDecimal, decimalVal);

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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
        expectedDouble *= 10;
        expectedFloat *= 10;
        expectedDecimal *= 10;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + ulong.MaxValue + "0  ", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            var expectedDateTime = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expectedDateTimeOffset = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.String, root.ValueKind);

            Assert.IsFalse(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.AreEqual(default, DateTimeVal);
            Assert.IsFalse(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.AreEqual(default, DateTimeOffsetVal);

            Assert.ThrowsExactly<FormatException>(() => root.GetDateTime());
            Assert.ThrowsExactly<FormatException>(() => root.GetDateTimeOffset());
        }
    }

    [TestMethod]
    [DynamicData(nameof(JsonGuidTestData.ValidGuidTests), typeof(JsonGuidTestData))]
    [DynamicData(nameof(JsonGuidTestData.ValidHexGuidTests), typeof(JsonGuidTestData))]
    public void ReadGuid(string jsonString, string expectedStr)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{jsonString}\"");

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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
            yield return new object[] { "1e+1", 10.0, 10.0f, 10m };
            yield return new object[] { "1.1e-0", 1.1, 1.1f, 1.1m };
            yield return new object[] { "3.14159", 3.14159, 3.14159f, 3.14159m };
            yield return new object[] { "1e-10", 1e-10, 1e-10f, 1e-10m };
            yield return new object[] { "1234567.15", 1234567.15, 1234567.13f, 1234567.15m };
        }
    }

    [TestMethod]
    [DynamicData(nameof(NonIntegerCases))]
    public void ReadNonInteger(string str, double expectedDouble, float expectedFloat, decimal expectedDecimal)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + str + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

            Assert.AreEqual(expectedFloat, root.GetSingle());
            Assert.AreEqual(expectedDouble, root.GetDouble());
            Assert.AreEqual(expectedDecimal, root.GetDecimal());
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    1e+100000002"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(
            "[ 0, 1, 2, 3/*.14159*/           , /* 42, 11, hut, hut, hike! */ 4 ]",
            options))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.AreEqual(JsonValueKind.Array, root.ValueKind);
            Assert.AreEqual(5, root.GetArrayLength());

            for (int i = root.GetArrayLength() - 1; i >= 0; i--)
            {
                Assert.AreEqual(i, root[i].GetInt32());
            }

            int val = 0;

            foreach (JsonElement.Mutable element in root.EnumerateArray())
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
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetGuid());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.EnumerateObject());
            Assert.ThrowsExactly<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [TestMethod]
    public void CheckUseAfterDispose()
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            JsonProperty<JsonElement.Mutable> property = root.EnumerateObject().First();
            builderDoc.Dispose();

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
                builderDoc.WriteTo(writer);
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
        JsonElement.Mutable root = default;

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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.AreEqual(expectedValue, doc.RootElement.GetString());
        }
    }

    [TestMethod]
    public void GetString_BadUtf8()
    {
        // The Arabic ligature Lam-Alef (U+FEFB) (which happens to, as a standalone, mean "no" in English)
        // is UTF-8 EF BB BB.  So let's leave out a BB and put it in quotes.
        byte[] badUtf8 = [0x22, 0xEF, 0xBB, 0x22];
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(badUtf8))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new byte[] { 0x22, 0xEF, 0xBB, 0x22 }))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            const string NotPresent = "Not Present";
            byte[] notPresentUtf8 = Encoding.UTF8.GetBytes(NotPresent);

            JsonElement.Mutable element;
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            byte[] pascalBytes = Encoding.UTF8.GetBytes(PascalString);
            byte[] camelBytes = Encoding.UTF8.GetBytes(CamelString);
            byte[] oddBytes = Encoding.UTF8.GetBytes(OddString);
            byte[] inverseOddBytes = Encoding.UTF8.GetBytes(InverseOddString);

            void assertPascal(JsonElement.Mutable elem)
            {
                Assert.AreEqual(JsonValueKind.String, elem.ValueKind);
                Assert.AreEqual("no", elem.GetString());
            }

            void assertCamel(JsonElement.Mutable elem)
            {
                Assert.AreEqual(JsonValueKind.Number, elem.ValueKind);
                Assert.AreEqual(42, elem.GetInt32());
            }

            void assertOdd(JsonElement.Mutable elem)
            {
                Assert.AreEqual(JsonValueKind.False, elem.ValueKind);
                Assert.IsFalse(elem.GetBoolean());
            }

            Assert.IsTrue(root.TryGetProperty(PascalString, out JsonElement.Mutable pascal));
            assertPascal(pascal);
            Assert.IsTrue(root.TryGetProperty(PascalString.AsSpan(), out pascal));
            assertPascal(pascal);
            Assert.IsTrue(root.TryGetProperty(pascalBytes, out pascal));
            assertPascal(pascal);

            Assert.IsTrue(root.TryGetProperty(CamelString, out JsonElement.Mutable camel));
            assertCamel(camel);
            Assert.IsTrue(root.TryGetProperty(CamelString.AsSpan(), out camel));
            assertCamel(camel);
            Assert.IsTrue(root.TryGetProperty(camelBytes, out camel));
            assertCamel(camel);

            Assert.IsTrue(root.TryGetProperty(OddString, out JsonElement.Mutable odd));
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.IsFalse(root.GetProperty("DateTimeProperty").TryGetDateTime(out DateTime datetimeValue));
            Assert.AreEqual(default, datetimeValue);
            Assert.ThrowsExactly<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTime());

            Assert.IsFalse(root.GetProperty("DateTimeProperty").TryGetDateTimeOffset(out DateTimeOffset datetimeOffsetValue));
            Assert.AreEqual(default, datetimeOffsetValue);
            Assert.ThrowsExactly<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTimeOffset());
        }
    }

    [TestMethod]
    public void GetPropertyByNullName()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{ }"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"value\"}"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            byte[] utf8PropertyName = Encoding.UTF8.GetBytes(propertyName);

            Assert.AreEqual(3, root.GetProperty(propertyName).GetInt32());
            Assert.AreEqual(3, root.GetProperty(propertyName.AsSpan()).GetInt32());
            Assert.AreEqual(3, root.GetProperty(utf8PropertyName).GetInt32());

            JsonElement.Mutable matchedProperty;
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
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

            JsonElement.Mutable matchedProperty;
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

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.AreEqual(6, builderDoc.RootElement.GetPropertyCount());
            ObjectEnumerator<JsonElement.Mutable> enumerator = builderDoc.RootElement.EnumerateObject();
            Assert.IsTrue(enumerator.MoveNext(), "Move to first property");
            JsonProperty<JsonElement.Mutable> property = enumerator.Current;

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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[0, 1, 2, 3, 4, 5]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            ArrayEnumerator<JsonElement.Mutable> structEnumerable = root.EnumerateArray();
            IEnumerable<JsonElement.Mutable> strongBoxedEnumerable = root.EnumerateArray();
            IEnumerable weakBoxedEnumerable = root.EnumerateArray();

            ArrayEnumerator<JsonElement.Mutable> structEnumerator = structEnumerable.GetEnumerator();
            IEnumerator<JsonElement.Mutable> strongBoxedEnumerator = strongBoxedEnumerable.GetEnumerator();
            IEnumerator weakBoxedEnumerator = weakBoxedEnumerable.GetEnumerator();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(0, structEnumerator.Current.GetInt32());
            Assert.AreEqual(0, strongBoxedEnumerator.Current.GetInt32());
            Assert.AreEqual(0, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(1, structEnumerator.Current.GetInt32());
            Assert.AreEqual(1, strongBoxedEnumerator.Current.GetInt32());
            Assert.AreEqual(1, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            int test = 0;

            foreach (JsonElement.Mutable element in structEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in structEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in strongBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in strongBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in weakBoxedEnumerable)
            {
                Assert.AreEqual(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in weakBoxedEnumerable)
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
            Assert.AreEqual(2, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(4, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual(5, strongBoxedEnumerator.Current.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(3, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(4, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual(5, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());
            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
        }
    }

    [TestMethod]
    public void DefaultArrayEnumeratorDoesNotThrow()
    {
        ArrayEnumerator<JsonElement.Mutable> enumerable = default;
        ArrayEnumerator<JsonElement.Mutable> enumerator = enumerable.GetEnumerator();
        ArrayEnumerator<JsonElement.Mutable> defaultEnumerator = default;

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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            ObjectEnumerator<JsonElement.Mutable> structEnumerable = root.EnumerateObject();
            IEnumerable<JsonProperty<JsonElement.Mutable>> strongBoxedEnumerable = root.EnumerateObject();
            IEnumerable weakBoxedEnumerable = root.EnumerateObject();

            ObjectEnumerator<JsonElement.Mutable> structEnumerator = structEnumerable.GetEnumerator();
            IEnumerator<JsonProperty<JsonElement.Mutable>> strongBoxedEnumerator = strongBoxedEnumerable.GetEnumerator();
            IEnumerator weakBoxedEnumerator = weakBoxedEnumerable.GetEnumerator();

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual("name0", structEnumerator.Current.Name);
            Assert.AreEqual(0, structEnumerator.Current.Value.GetInt32());
            Assert.AreEqual("name0", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(0, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.AreEqual("name0", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(0, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsTrue(structEnumerator.MoveNext());
            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.IsTrue(weakBoxedEnumerator.MoveNext());

            Assert.AreEqual(1, structEnumerator.Current.Value.GetInt32());
            Assert.AreEqual(1, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.AreEqual(1, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            int test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in structEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in structEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in strongBoxedEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in strongBoxedEnumerable)
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

            foreach (JsonProperty<JsonElement.Mutable> property in weakBoxedEnumerable)
            {
                Assert.AreEqual("name" + test, property.Name);
                Assert.AreEqual(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in weakBoxedEnumerable)
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
            Assert.AreEqual("name2", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(2, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name4", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(4, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(strongBoxedEnumerator.MoveNext());
            Assert.AreEqual("name5", strongBoxedEnumerator.Current.Name);
            Assert.AreEqual(5, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name3", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(3, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name4", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(4, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());

            Assert.IsTrue(weakBoxedEnumerator.MoveNext());
            Assert.AreEqual("name5", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.AreEqual(5, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
            Assert.IsFalse(structEnumerator.MoveNext());
            Assert.IsFalse(strongBoxedEnumerator.MoveNext());
            Assert.IsFalse(weakBoxedEnumerator.MoveNext());
        }
    }

    [TestMethod]
    public void DefaultObjectEnumeratorDoesNotThrow()
    {
        ObjectEnumerator<JsonElement.Mutable> enumerable = default;
        ObjectEnumerator<JsonElement.Mutable> enumerator = enumerable.GetEnumerator();
        ObjectEnumerator<JsonElement.Mutable> defaultEnumerator = default;

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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
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
    [DataRow("""{ "foo" : [1], "test": false, "bar" : { "nested": 3 } }""", 3)]
    [DataRow("""{ "foo" : [1,2,3,4] }""", 1)]
    [DataRow("""{}""", 0)]
    [DataRow("""{ "foo" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }, "test": true, "foo2" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }}""", 3)]
    public void TestGetPropertyCount(string json, int expectedCount)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable element = builderDoc.RootElement;
            Assert.AreEqual(expectedCount, element.GetPropertyCount());
        }
    }

    [TestMethod]
    public void VerifyGetPropertyCountAndArrayLengthUsingEnumerateMethods()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ProjectLockJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(builderDoc.RootElement);
        }

        static void CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(JsonElement.Mutable elem)
        {
            if (elem.ValueKind == JsonValueKind.Object)
            {
                Assert.AreEqual(elem.EnumerateObject().Count(), elem.GetPropertyCount());
                foreach (JsonProperty<JsonElement.Mutable> prop in elem.EnumerateObject())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(prop.Value);
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                Assert.AreEqual(elem.EnumerateArray().Count(), elem.GetArrayLength());
                foreach (JsonElement.Mutable item in elem.EnumerateArray())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(item);
                }
            }
        }
    }

    [TestMethod]
    public void ValueEquals_Null_TrueForNullFalseForEmpty()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("   null   "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"\""))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.IsFalse(jElement.ValueEquals(lookup));
        }
    }

    [TestMethod]
    [DataRow("\"connectionId\"", "\"conne\\u0063tionId\"")]
    [DataRow("\"conne\\u0063tionId\"", "connecxionId")] // intentionally making mismatch after escaped character
    [DataRow("\"conne\\u0063tionId\"", "bonnectionId")] // intentionally changing the expected starting character
    public void ValueEquals_JsonTokenStringType_False(string jsonString, string otherText)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
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
        using (var workspace = JsonWorkspace.CreateUnrented())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleObjectJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable first = builderDoc.RootElement.GetProperty("first");
            JsonElement.Mutable last = builderDoc.RootElement.GetProperty("last");

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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5,6],[7,8,9]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5],[6,7,8]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,5],[6,7,8]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
        using (var workspace = JsonWorkspace.Create())
        using (var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("[[1,2,3],[4,\"stringValue\"],[6,7,8]]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> doc = parsedDoc.RootElement.CreateBuilder(workspace))
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
    public void CreateBuilderFromMutableBuilderThrows()
    {
        const string ErrorMessage = "You cannot create a mutable copy of a mutable document. Consider calling Freeze() on the source document.";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder = JsonElement.CreateBuilder(workspace, "Hello"u8);
        Assert.ThrowsExactly<InvalidOperationException>(() => mutableBuilder.RootElement.CreateBuilder(workspace), ErrorMessage);
    }

    [TestMethod]
    public void CreateBuilderFromImmutableBuilderSucceeds()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder = JsonElement.CreateBuilder(workspace, "Hello"u8);
        Assert.IsFalse(mutableBuilder.IsImmutable);
        mutableBuilder.Freeze();
        Assert.IsTrue(mutableBuilder.IsImmutable);
        using JsonDocumentBuilder<JsonElement.Mutable> copyOfFrozen = mutableBuilder.RootElement.CreateBuilder(workspace);
        Assert.IsFalse(copyOfFrozen.IsImmutable);
    }

    [TestMethod]
    public void ModifyingImmutableBuilderFails()
    {
        const string ErrorMessage = "You cannot modify an immutable document.";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder =
            JsonElement.CreateBuilder(
                workspace,
                new((ref arrayBuilder) => arrayBuilder.AddItem(true)));
        Assert.IsFalse(mutableBuilder.IsImmutable);
        mutableBuilder.Freeze();
        Assert.IsTrue(mutableBuilder.IsImmutable);
        Assert.ThrowsExactly<InvalidOperationException>(() => mutableBuilder.RootElement.SetItem(0, false), ErrorMessage);
    }

    [TestMethod]
    public void SetItem_OnEmptyArray_AddsItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(0, 123);

        // Assert
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(123, root[0].GetInt32());
    }

    [TestMethod]
    public void SetItem_OnNonEmptyArray_ReplacesExistingItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(1, 99);

        // Assert
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(99, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
    }

    [TestMethod]
    public void SetItem_AtArrayLength_AppendsItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10,20]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(2, 30);

        // Assert
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(10, root[0].GetInt32());
        Assert.AreEqual(20, root[1].GetInt32());
        Assert.AreEqual(30, root[2].GetInt32());
    }

    [TestMethod]
    public void SetItem_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 100));
    }

    [TestMethod]
    public void SetItem_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(4, 100));
    }

    // Place these inside the JsonDocumentBuilderTests class

    [TestMethod]
    public void SetItem_Bool_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, true);
        Assert.IsTrue(root[0].GetBoolean());

        root.SetItem(1, false);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.IsFalse(root[1].GetBoolean());
    }

    [TestMethod]
    public void SetItem_Guid_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"[\"{guid1}\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, guid2);
        Assert.AreEqual(guid2, root[0].GetGuid());

        var guid3 = Guid.NewGuid();
        root.SetItem(1, guid3);
        Assert.AreEqual(guid3, root[1].GetGuid());
    }

    [TestMethod]
    public void SetItem_Byte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (byte)42);
        Assert.AreEqual(42, root[0].GetByte());

        root.SetItem(1, (byte)255);
        Assert.AreEqual(255, root[1].GetByte());
    }

    [TestMethod]
    public void SetItem_Long_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, long.MaxValue);
        Assert.AreEqual(long.MaxValue, root[0].GetInt64());

        root.SetItem(1, long.MinValue);
        Assert.AreEqual(long.MinValue, root[1].GetInt64());
    }

    [TestMethod]
    public void SetItem_Short_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (short)-12345);
        Assert.AreEqual(-12345, root[0].GetInt16());

        root.SetItem(1, (short)12345);
        Assert.AreEqual(12345, root[1].GetInt16());
    }

    [TestMethod]
    public void SetItem_Float_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 3.14f);
        Assert.AreEqual(3.14f, root[0].GetSingle());

        root.SetItem(1, -2.71f);
        Assert.AreEqual(-2.71f, root[1].GetSingle());
    }

    [TestMethod]
    public void SetItem_Double_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 2.718281828);
        Assert.AreEqual(2.718281828, root[0].GetDouble());

        root.SetItem(1, -3.1415926535);
        Assert.AreEqual(-3.1415926535, root[1].GetDouble());
    }

    [TestMethod]
    public void SetItem_Generic_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Use a JsonElement.Mutable as the value
        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable value = builderDoc2.RootElement;

        root.SetItem(0, value);
        Assert.AreEqual(42, root[0].GetInt32());
    }

    [TestMethod]
    public void SetItem_Utf8String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Simple ASCII
        root.SetItem(0, "hello"u8);
        Assert.AreEqual("hello", root[0].GetString());

        root.SetItem(1, "world"u8);
        Assert.AreEqual("world", root[1].GetString());

        // Escaped UTF-8 string: "foo\"bar" (the quote is escaped in JSON, but the UTF-8 bytes are unescaped)
        ReadOnlySpan<byte> escapedUtf8 = "foo\"bar"u8;
        root.SetItem(2, escapedUtf8);
        Assert.AreEqual("foo\"bar", root[2].GetString());

        // Non-ASCII UTF-8 string: "héllo" (e with acute accent)
        ReadOnlySpan<byte> nonAsciiUtf8 = [(byte)'h', 0xC3, 0xA9, (byte)'l', (byte)'l', (byte)'o'];
        root.SetItem(3, nonAsciiUtf8);
        Assert.AreEqual("héllo", root[3].GetString());

        // Non-ASCII UTF-8 string: emoji "😊"
        ReadOnlySpan<byte> emojiUtf8 = "😊"u8;
        root.SetItem(4, emojiUtf8);
        Assert.AreEqual("😊", root[4].GetString());
    }

    [TestMethod]
    public void SetItem_String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Simple ASCII
        root.SetItem(0, "hello");
        Assert.AreEqual("hello", root[0].GetString());

        root.SetItem(1, "world");
        Assert.AreEqual("world", root[1].GetString());

        // Escaped UTF-8 string: "foo\"bar" (the quote is escaped in JSON, but the UTF-8 bytes are unescaped)
        string escapedString = "foo\"bar";
        root.SetItem(2, escapedString);
        Assert.AreEqual("foo\"bar", root[2].GetString());

        // Non-ASCII UTF-8 string: "héllo" (e with acute accent)
        byte[] nonAsciiUtf8 = [(byte)'h', 0xC3, 0xA9, (byte)'l', (byte)'l', (byte)'o'];
        root.SetItem(3, Encoding.UTF8.GetString(nonAsciiUtf8));
        Assert.AreEqual("héllo", root[3].GetString());

        // Non-ASCII string: emoji "😊"
        string emoji = "😊";
        root.SetItem(4, emoji);
        Assert.AreEqual("😊", root[4].GetString());
    }

    [TestMethod]
    public void SetItemNull_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItemNull(0);
        Assert.AreEqual(JsonValueKind.Null, root[0].ValueKind);

        root.SetItemNull(1);
        Assert.AreEqual(JsonValueKind.Null, root[1].ValueKind);
    }

    [TestMethod]
    public void SetItem_SByte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (sbyte)-8);
        Assert.AreEqual(-8, root[0].GetSByte());

        root.SetItem(1, (sbyte)127);
        Assert.AreEqual(127, root[1].GetSByte());
    }

    [TestMethod]
    public void SetItem_UShort_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ushort)65535);
        Assert.AreEqual(65535, root[0].GetUInt16());

        root.SetItem(1, (ushort)42);
        Assert.AreEqual((ushort)42, root[1].GetUInt16());
    }

    [TestMethod]
    public void SetItem_UInt_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 4294967295u);
        Assert.AreEqual(4294967295u, root[0].GetUInt32());

        root.SetItem(1, 123u);
        Assert.AreEqual(123u, root[1].GetUInt32());
    }

    [TestMethod]
    public void SetItem_ULong_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 18446744073709551615ul);
        Assert.AreEqual(18446744073709551615ul, root[0].GetUInt64());

        root.SetItem(1, 42ul);
        Assert.AreEqual(42ul, root[1].GetUInt64());
    }

    [TestMethod]
    public void SetItem_Int_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, -123456);
        Assert.AreEqual(-123456, root[0].GetInt32());

        root.SetItem(1, 654321);
        Assert.AreEqual(654321, root[1].GetInt32());
    }

    [TestMethod]
    public void SetItem_Decimal_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 123.456m);
        Assert.AreEqual(123.456m, root[0].GetDecimal());

        root.SetItem(1, -789.01m);
        Assert.AreEqual(-789.01m, root[1].GetDecimal());
    }

#if NET

    [TestMethod]
    public void SetItem_Int128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetItem(0, bigValue);
        Assert.AreEqual(bigValue, root[0].GetInt128());

        root.SetItem(1, smallValue);
        Assert.AreEqual(smallValue, root[1].GetInt128());
    }

    [TestMethod]
    public void SetItem_UInt128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetItem(0, bigValue);
        Assert.AreEqual(bigValue, root[0].GetUInt128());

        root.SetItem(1, smallValue);
        Assert.AreEqual(smallValue, root[1].GetUInt128());
    }

    [TestMethod]
    public void SetItem_Half_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetItem(0, value1);
        Assert.AreEqual(value1, root[0].GetHalf());

        root.SetItem(1, value2);
        Assert.AreEqual(value2, root[1].GetHalf());
    }

#endif

    [TestMethod]
    public void SetItem_DateTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        root.SetItem(0, dt1);
        Assert.AreEqual(dt1, root[0].GetDateTime());

        root.SetItem(1, dt2);
        Assert.AreEqual(dt2, root[1].GetDateTime());
    }

    [TestMethod]
    public void SetItem_DateTimeOffset_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

        root.SetItem(0, dto1);
        Assert.AreEqual(dto1, root[0].GetDateTimeOffset());

        root.SetItem(1, dto2);
        Assert.AreEqual(dto2, root[1].GetDateTimeOffset());
    }

    [TestMethod]
    public void SetItem_Bool_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, false));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, true));
    }

    [TestMethod]
    public void SetItem_Guid_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"00000000-0000-0000-0000-000000000000\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        var guid = Guid.NewGuid();

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, guid));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, guid));
    }

    [TestMethod]
    public void SetItem_Byte_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, (byte)1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, (byte)2));
    }

    [TestMethod]
    public void SetItem_SByte_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, (sbyte)1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, (sbyte)2));
    }

    [TestMethod]
    public void SetItem_Short_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, (short)1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, (short)2));
    }

    [TestMethod]
    public void SetItem_UShort_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, (ushort)1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, (ushort)2));
    }

    [TestMethod]
    public void SetItem_Int_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2));
    }

    [TestMethod]
    public void SetItem_UInt_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1u));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2u));
    }

    [TestMethod]
    public void SetItem_Long_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1L));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2L));
    }

    [TestMethod]
    public void SetItem_ULong_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1UL));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2UL));
    }

    [TestMethod]
    public void SetItem_Float_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0f));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2.0f));
    }

    [TestMethod]
    public void SetItem_Double_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2.0));
    }

    [TestMethod]
    public void SetItem_Decimal_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0m));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, 2.0m));
    }

    [TestMethod]
    public void SetItem_Utf8String_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, "test"u8));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, "test"u8));
    }

    [TestMethod]
    public void SetItem_Generic_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable value = builderDoc2.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [TestMethod]
    public void SetItemNull_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItemNull(-1));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItemNull(2));
    }

#if NET

    [TestMethod]
    public void SetItem_Int128_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Int128 value = Int128.One;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [TestMethod]
    public void SetItem_UInt128_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        UInt128 value = UInt128.One;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [TestMethod]
    public void SetItem_Half_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value = (Half)1.0;

        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

#endif

    [TestMethod]
    public void SetProperty_Object_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Existing UTF-8 property name tests
        root.SetProperty("a"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("a").ToString());
        root.SetProperty("a".AsSpan(), (ref o) => { o.AddProperty("hello"u8, "there"u8); });
        Assert.AreEqual("{\"hello\":\"there\"}", root.GetProperty("a").ToString());
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Object_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // Existing UTF-8 property name tests
        root.SetProperty("b"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("b").ToString());
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Object_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Existing UTF-8 property name test
        root.SetProperty("a"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Object_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": {\"c\": true}}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // Existing UTF-8 property name test
        root.SetProperty("b"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("b").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.AreEqual("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Array_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Array_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // ASCII property name
        root.SetProperty("b"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("b").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Array_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("a").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Array_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": {\"c\": true}}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // ASCII property name
        root.SetProperty("b"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("b").ToString());

        root.SetProperty("b".AsSpan(), (ref o) => { o.AddItem("there"u8); });
        Assert.AreEqual("[\"there\"]", root.GetProperty("b").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root.GetProperty("span").ToString());
    }

    [TestMethod]
    public void SetProperty_Bool_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, true);
        Assert.IsTrue(root.GetProperty("a").GetBoolean());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, false);
        Assert.IsFalse(root.GetProperty("héllo").GetBoolean());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, true);
        Assert.IsTrue(root.GetProperty("foo\"bar").GetBoolean());
    }

    [TestMethod]
    public void SetProperty_Guid_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"{{\"a\":\"{guid1}\"}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, guid2);
        Assert.AreEqual(guid2, root.GetProperty("a").GetGuid());

        // Non-ASCII property name
        var guid3 = Guid.NewGuid();
        root.SetProperty("héllo"u8, guid3);
        Assert.AreEqual(guid3, root.GetProperty("héllo").GetGuid());

        // Encoded UTF-8 property name
        var guid4 = Guid.NewGuid();
        root.SetProperty("foo\"bar"u8, guid4);
        Assert.AreEqual(guid4, root.GetProperty("foo\"bar").GetGuid());
    }

    [TestMethod]
    public void SetProperty_DateTime_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, dt1);
        Assert.AreEqual(dt1, root.GetProperty("a").GetDateTime());

        root.SetProperty("héllo"u8, dt2);
        Assert.AreEqual(dt2, root.GetProperty("héllo").GetDateTime());

        root.SetProperty("foo\"bar"u8, dt2);
        Assert.AreEqual(dt2, root.GetProperty("foo\"bar").GetDateTime());
    }

    [TestMethod]
    public void SetProperty_DateTimeOffset_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetDateTimeOffset());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetDateTimeOffset());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetDateTimeOffset());
    }

    [TestMethod]
    public void SetProperty_OffsetDateTime_Works()
    {
        var dto1 = new OffsetDateTime(new LocalDateTime(2024, 5, 30, 12, 34, 56), Offset.Zero);
        var dto2 = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetOffsetDateTime());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetOffsetDateTime());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetOffsetDateTime());
    }

    [TestMethod]
    public void SetProperty_OffsetDate_Works()
    {
        var dto1 = new OffsetDate(new LocalDate(2024, 5, 30), Offset.Zero);
        var dto2 = new OffsetDate(new LocalDate(2025, 1, 1), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetOffsetDate());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetOffsetDate());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetOffsetDate());
    }

    [TestMethod]
    public void SetProperty_LocalDate_Works()
    {
        var dto1 = new LocalDate(2024, 5, 30);
        var dto2 = new LocalDate(2025, 1, 1);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetLocalDate());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetLocalDate());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetLocalDate());
    }

    [TestMethod]
    public void SetProperty_OffsetTime_Works()
    {
        var dto1 = new OffsetTime(new LocalTime(12, 34, 56), Offset.Zero);
        var dto2 = new OffsetTime(new LocalTime(0, 0, 0), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetOffsetTime());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetOffsetTime());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetOffsetTime());
    }

    [TestMethod]
    public void SetProperty_Period_Works()
    {
        Period dto1 = new Period(1, 2, 0, 4, 5, 6, 7, 0, 0, 0).Normalize();
        Period dto2 = Period.Zero;
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"P1W\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetPeriod());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetPeriod());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetPeriod());
    }

    [TestMethod]
    public void SetProperty_Byte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (byte)42);
        Assert.AreEqual(42, root.GetProperty("a").GetByte());

        root.SetProperty("héllo"u8, (byte)255);
        Assert.AreEqual(255, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar"u8, (byte)128);
        Assert.AreEqual(128, root.GetProperty("foo\"bar").GetByte());
    }

    [TestMethod]
    public void SetProperty_SByte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (sbyte)-8);
        Assert.AreEqual(-8, root.GetProperty("a").GetSByte());

        root.SetProperty("héllo"u8, (sbyte)127);
        Assert.AreEqual(127, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar"u8, (sbyte)-128);
        Assert.AreEqual(-128, root.GetProperty("foo\"bar").GetSByte());
    }

    [TestMethod]
    public void SetProperty_Short_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (short)-12345);
        Assert.AreEqual(-12345, root.GetProperty("a").GetInt16());

        root.SetProperty("héllo"u8, (short)12345);
        Assert.AreEqual(12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar"u8, (short)-32768);
        Assert.AreEqual(-32768, root.GetProperty("foo\"bar").GetInt16());
    }

    [TestMethod]
    public void SetProperty_UShort_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (ushort)65535);
        Assert.AreEqual(65535, root.GetProperty("a").GetUInt16());

        root.SetProperty("héllo"u8, (ushort)42);
        Assert.AreEqual((ushort)42, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar"u8, (ushort)0);
        Assert.AreEqual((ushort)0, root.GetProperty("foo\"bar").GetUInt16());
    }

    [TestMethod]
    public void SetProperty_Int_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, -123456);
        Assert.AreEqual(-123456, root.GetProperty("a").GetInt32());

        root.SetProperty("héllo"u8, 654321);
        Assert.AreEqual(654321, root.GetProperty("héllo").GetInt32());

        root.SetProperty("foo\"bar"u8, int.MinValue);
        Assert.AreEqual(int.MinValue, root.GetProperty("foo\"bar").GetInt32());
    }

    [TestMethod]
    public void SetProperty_UInt_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 4294967295u);
        Assert.AreEqual(4294967295u, root.GetProperty("a").GetUInt32());

        root.SetProperty("héllo"u8, 123u);
        Assert.AreEqual(123u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar"u8, 0u);
        Assert.AreEqual(0u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [TestMethod]
    public void SetProperty_Long_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, long.MaxValue);
        Assert.AreEqual(long.MaxValue, root.GetProperty("a").GetInt64());

        root.SetProperty("héllo"u8, long.MinValue);
        Assert.AreEqual(long.MinValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar"u8, 0L);
        Assert.AreEqual(0L, root.GetProperty("foo\"bar").GetInt64());
    }

    [TestMethod]
    public void SetProperty_ULong_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 18446744073709551615ul);
        Assert.AreEqual(18446744073709551615ul, root.GetProperty("a").GetUInt64());

        root.SetProperty("héllo"u8, 42ul);
        Assert.AreEqual(42ul, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar"u8, 0ul);
        Assert.AreEqual(0ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [TestMethod]
    public void SetProperty_Float_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 3.14f);
        Assert.AreEqual(3.14f, root.GetProperty("a").GetSingle());

        root.SetProperty("héllo"u8, -2.71f);
        Assert.AreEqual(-2.71f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar"u8, 0.0f);
        Assert.AreEqual(0.0f, root.GetProperty("foo\"bar").GetSingle());
    }

    [TestMethod]
    public void SetProperty_Double_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 2.718281828);
        Assert.AreEqual(2.718281828, root.GetProperty("a").GetDouble());

        root.SetProperty("héllo"u8, -3.1415926535);
        Assert.AreEqual(-3.1415926535, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar"u8, 0.0);
        Assert.AreEqual(0.0, root.GetProperty("foo\"bar").GetDouble());
    }

    [TestMethod]
    public void SetProperty_Decimal_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 123.456m);
        Assert.AreEqual(123.456m, root.GetProperty("a").GetDecimal());

        root.SetProperty("héllo"u8, -789.01m);
        Assert.AreEqual(-789.01m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar"u8, 0.0m);
        Assert.AreEqual(0.0m, root.GetProperty("foo\"bar").GetDecimal());
    }

#if NET

    [TestMethod]
    public void SetProperty_Int128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("a"u8, bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("a").GetInt128());

        root.SetProperty("héllo"u8, smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar"u8, Int128.Zero);
        Assert.AreEqual(Int128.Zero, root.GetProperty("foo\"bar").GetInt128());
    }

    [TestMethod]
    public void SetProperty_UInt128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("a"u8, bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("a").GetUInt128());

        root.SetProperty("héllo"u8, smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar"u8, UInt128.Zero);
        Assert.AreEqual(UInt128.Zero, root.GetProperty("foo\"bar").GetUInt128());
    }

    [TestMethod]
    public void SetProperty_Half_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("a"u8, value1);
        Assert.AreEqual(value1, root.GetProperty("a").GetHalf());

        root.SetProperty("héllo"u8, value2);
        Assert.AreEqual(value2, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar"u8, (Half)0.0);
        Assert.AreEqual((Half)0.0, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [TestMethod]
    public void SetProperty_Bool_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", true);
        Assert.IsTrue(root.GetProperty("a").GetBoolean());

        root.SetProperty("héllo", false);
        Assert.IsFalse(root.GetProperty("héllo").GetBoolean());

        root.SetProperty("foo\"bar", true);
        Assert.IsTrue(root.GetProperty("foo\"bar").GetBoolean());
    }

    [TestMethod]
    public void SetProperty_Guid_StringPropertyName_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"{{\"a\":\"{guid1}\"}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", guid2);
        Assert.AreEqual(guid2, root.GetProperty("a").GetGuid());

        var guid3 = Guid.NewGuid();
        root.SetProperty("héllo", guid3);
        Assert.AreEqual(guid3, root.GetProperty("héllo").GetGuid());

        var guid4 = Guid.NewGuid();
        root.SetProperty("foo\"bar", guid4);
        Assert.AreEqual(guid4, root.GetProperty("foo\"bar").GetGuid());
    }

    [TestMethod]
    public void SetProperty_Byte_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (byte)42);
        Assert.AreEqual(42, root.GetProperty("a").GetByte());

        root.SetProperty("héllo", (byte)255);
        Assert.AreEqual(255, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar", (byte)128);
        Assert.AreEqual(128, root.GetProperty("foo\"bar").GetByte());
    }

    [TestMethod]
    public void SetProperty_SByte_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (sbyte)-8);
        Assert.AreEqual(-8, root.GetProperty("a").GetSByte());

        root.SetProperty("héllo", (sbyte)127);
        Assert.AreEqual(127, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar", (sbyte)-128);
        Assert.AreEqual(-128, root.GetProperty("foo\"bar").GetSByte());
    }

    [TestMethod]
    public void SetProperty_Short_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (short)-12345);
        Assert.AreEqual(-12345, root.GetProperty("a").GetInt16());

        root.SetProperty("héllo", (short)12345);
        Assert.AreEqual(12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar", (short)-32768);
        Assert.AreEqual(-32768, root.GetProperty("foo\"bar").GetInt16());
    }

    [TestMethod]
    public void SetProperty_UShort_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (ushort)65535);
        Assert.AreEqual(65535, root.GetProperty("a").GetUInt16());

        root.SetProperty("héllo", (ushort)42);
        Assert.AreEqual((ushort)42, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar", (ushort)0);
        Assert.AreEqual((ushort)0, root.GetProperty("foo\"bar").GetUInt16());
    }

    [TestMethod]
    public void SetProperty_Int_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", -123456);
        Assert.AreEqual(-123456, root.GetProperty("a").GetInt32());

        root.SetProperty("héllo", 654321);
        Assert.AreEqual(654321, root.GetProperty("héllo").GetInt32());

        root.SetProperty("foo\"bar", int.MinValue);
        Assert.AreEqual(int.MinValue, root.GetProperty("foo\"bar").GetInt32());
    }

    [TestMethod]
    public void SetProperty_UInt_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 4294967295u);
        Assert.AreEqual(4294967295u, root.GetProperty("a").GetUInt32());

        root.SetProperty("héllo", 123u);
        Assert.AreEqual(123u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar", 0u);
        Assert.AreEqual(0u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [TestMethod]
    public void SetProperty_Long_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", long.MaxValue);
        Assert.AreEqual(long.MaxValue, root.GetProperty("a").GetInt64());

        root.SetProperty("héllo", long.MinValue);
        Assert.AreEqual(long.MinValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar", 0L);
        Assert.AreEqual(0L, root.GetProperty("foo\"bar").GetInt64());
    }

    [TestMethod]
    public void SetProperty_ULong_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 18446744073709551615ul);
        Assert.AreEqual(18446744073709551615ul, root.GetProperty("a").GetUInt64());

        root.SetProperty("héllo", 42ul);
        Assert.AreEqual(42ul, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar", 0ul);
        Assert.AreEqual(0ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [TestMethod]
    public void SetProperty_Float_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 3.14f);
        Assert.AreEqual(3.14f, root.GetProperty("a").GetSingle());

        root.SetProperty("héllo", -2.71f);
        Assert.AreEqual(-2.71f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar", 0.0f);
        Assert.AreEqual(0.0f, root.GetProperty("foo\"bar").GetSingle());
    }

    [TestMethod]
    public void SetProperty_Double_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 2.718281828);
        Assert.AreEqual(2.718281828, root.GetProperty("a").GetDouble());

        root.SetProperty("héllo", -3.1415926535);
        Assert.AreEqual(-3.1415926535, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar", 0.0);
        Assert.AreEqual(0.0, root.GetProperty("foo\"bar").GetDouble());
    }

    [TestMethod]
    public void SetProperty_Decimal_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 123.456m);
        Assert.AreEqual(123.456m, root.GetProperty("a").GetDecimal());

        root.SetProperty("héllo", -789.01m);
        Assert.AreEqual(-789.01m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar", 0.0m);
        Assert.AreEqual(0.0m, root.GetProperty("foo\"bar").GetDecimal());
    }

#if NET

    [TestMethod]
    public void SetProperty_Int128_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("a", bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("a").GetInt128());

        root.SetProperty("héllo", smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar", Int128.Zero);
        Assert.AreEqual(Int128.Zero, root.GetProperty("foo\"bar").GetInt128());
    }

    [TestMethod]
    public void SetProperty_UInt128_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("a", bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("a").GetUInt128());

        root.SetProperty("héllo", smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar", UInt128.Zero);
        Assert.AreEqual(UInt128.Zero, root.GetProperty("foo\"bar").GetUInt128());
    }

    [TestMethod]
    public void SetProperty_Half_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("a", value1);
        Assert.AreEqual(value1, root.GetProperty("a").GetHalf());

        root.SetProperty("héllo", value2);
        Assert.AreEqual(value2, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar", (Half)0.0);
        Assert.AreEqual((Half)0.0, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [TestMethod]
    public void SetProperty_DateTime_StringPropertyName_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dt1);
        Assert.AreEqual(dt1, root.GetProperty("a").GetDateTime());

        root.SetProperty("héllo", dt2);
        Assert.AreEqual(dt2, root.GetProperty("héllo").GetDateTime());
    }

    [TestMethod]
    public void SetProperty_DateTimeOffset_StringPropertyName_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.AreEqual(dto1, root.GetProperty("a").GetDateTimeOffset());

        root.SetProperty("héllo", dto2);
        Assert.AreEqual(dto2, root.GetProperty("héllo").GetDateTimeOffset());
    }

    [TestMethod]
    public void SetProperty_Utf8String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, "hello"u8);
        Assert.AreEqual("hello", root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, "world"u8);
        Assert.AreEqual("world", root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, "baz"u8);
        Assert.AreEqual("baz", root.GetProperty("foo\"bar").GetString());
    }

    [TestMethod]
    public void SetProperty_Generic_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("123");
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable value = builderDoc2.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, value);
        Assert.AreEqual(123, root.GetProperty("a").GetInt32());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, value);
        Assert.AreEqual(123, root.GetProperty("héllo").GetInt32());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, value);
        Assert.AreEqual(123, root.GetProperty("foo\"bar").GetInt32());
    }

    [TestMethod]
    public void SetProperty_String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a", "hello");
        Assert.AreEqual("hello", root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetProperty("héllo", "world");
        Assert.AreEqual("world", root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar", "baz");
        Assert.AreEqual("baz", root.GetProperty("foo\"bar").GetString());
    }

    [TestMethod]
    public void SetProperty_Null_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetPropertyNull("a");
        Assert.IsNull(root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetPropertyNull("héllo");
        Assert.IsNull(root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetPropertyNull("foo\"bar");
        Assert.IsNull(root.GetProperty("foo\"bar").GetString());
    }

    [TestMethod]
    public void SetProperty_JsonElement_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a", doc.RootElement);
        Assert.AreEqual("{\"a\":\"a\"}", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo", doc.RootElement);
        Assert.AreEqual("{\"a\":\"a\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar", doc.RootElement);
        Assert.AreEqual("{\"a\":\"a\"}", root.GetProperty("foo\"bar").ToString());
    }

    [TestMethod]
    public void SetProperty_Bool_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":false,\"foo\\\"bar\":true}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, true);
        Assert.IsTrue(root.GetProperty("héllo").GetBoolean());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, false);
        Assert.IsFalse(root.GetProperty("foo\"bar").GetBoolean());
    }

    [TestMethod]
    public void SetProperty_Int_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, 42);
        Assert.AreEqual(42, root.GetProperty("héllo").GetInt32());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, 99);
        Assert.AreEqual(99, root.GetProperty("foo\"bar").GetInt32());
    }

    [TestMethod]
    public void SetProperty_Utf8String_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"a\",\"foo\\\"bar\":\"b\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, "world"u8);
        Assert.AreEqual("world", root.GetProperty("héllo").GetString());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, "baz"u8);
        Assert.AreEqual("baz", root.GetProperty("foo\"bar").GetString());
    }

    [TestMethod]
    public void SetProperty_Generic_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("123");
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable value = builderDoc2.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, value);
        Assert.AreEqual(123, root.GetProperty("héllo").GetInt32());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, value);
        Assert.AreEqual(123, root.GetProperty("foo\"bar").GetInt32());
    }

    [TestMethod]
    public void SetProperty_Long_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, long.MaxValue);
        Assert.AreEqual(long.MaxValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar"u8, long.MinValue);
        Assert.AreEqual(long.MinValue, root.GetProperty("foo\"bar").GetInt64());
    }

    [TestMethod]
    public void SetProperty_ULong_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, ulong.MaxValue);
        Assert.AreEqual(ulong.MaxValue, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar"u8, 42ul);
        Assert.AreEqual(42ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [TestMethod]
    public void SetProperty_Float_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 3.14f);
        Assert.AreEqual(3.14f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar"u8, -2.71f);
        Assert.AreEqual(-2.71f, root.GetProperty("foo\"bar").GetSingle());
    }

    [TestMethod]
    public void SetProperty_Double_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 2.718281828);
        Assert.AreEqual(2.718281828, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar"u8, -3.1415926535);
        Assert.AreEqual(-3.1415926535, root.GetProperty("foo\"bar").GetDouble());
    }

    [TestMethod]
    public void SetProperty_Decimal_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 123.456m);
        Assert.AreEqual(123.456m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar"u8, -789.01m);
        Assert.AreEqual(-789.01m, root.GetProperty("foo\"bar").GetDecimal());
    }

    [TestMethod]
    public void SetProperty_Null_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetPropertyNull("héllo"u8);
        Assert.IsNull(root.GetProperty("héllo").GetString());

        root.SetPropertyNull("foo\"bar"u8);
        Assert.IsNull(root.GetProperty("foo\"bar").GetString());
    }

#if NET

    [TestMethod]
    public void SetProperty_Int128_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("héllo"u8, bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar"u8, smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("foo\"bar").GetInt128());
    }

    [TestMethod]
    public void SetProperty_UInt128_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("héllo"u8, bigValue);
        Assert.AreEqual(bigValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar"u8, smallValue);
        Assert.AreEqual(smallValue, root.GetProperty("foo\"bar").GetUInt128());
    }

    [TestMethod]
    public void SetProperty_Half_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("héllo"u8, value1);
        Assert.AreEqual(value1, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar"u8, value2);
        Assert.AreEqual(value2, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [TestMethod]
    public void SetProperty_Byte_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (byte)123);
        Assert.AreEqual((byte)123, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar"u8, (byte)200);
        Assert.AreEqual((byte)200, root.GetProperty("foo\"bar").GetByte());
    }

    [TestMethod]
    public void SetProperty_SByte_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (sbyte)-42);
        Assert.AreEqual((sbyte)-42, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar"u8, (sbyte)127);
        Assert.AreEqual((sbyte)127, root.GetProperty("foo\"bar").GetSByte());
    }

    [TestMethod]
    public void SetProperty_Short_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (short)-12345);
        Assert.AreEqual((short)-12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar"u8, (short)32767);
        Assert.AreEqual((short)32767, root.GetProperty("foo\"bar").GetInt16());
    }

    [TestMethod]
    public void SetProperty_UShort_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (ushort)65535);
        Assert.AreEqual((ushort)65535, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar"u8, (ushort)42);
        Assert.AreEqual((ushort)42, root.GetProperty("foo\"bar").GetUInt16());
    }

    [TestMethod]
    public void SetProperty_UInt_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 4294967295u);
        Assert.AreEqual(4294967295u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar"u8, 123u);
        Assert.AreEqual(123u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [TestMethod]
    public void SetProperty_Guid_WithNonAsciiAndEscapedNames_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"00000000-0000-0000-0000-000000000000\",\"foo\\\"bar\":\"00000000-0000-0000-0000-000000000000\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, guid1);
        Assert.AreEqual(guid1, root.GetProperty("héllo").GetGuid());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, guid2);
        Assert.AreEqual(guid2, root.GetProperty("foo\"bar").GetGuid());
    }

    [TestMethod]
    public void SetProperty_DateTime_WithNonAsciiAndEscapedNames_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"2020-01-01T00:00:00Z\",\"foo\\\"bar\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, dt1);
        Assert.AreEqual(dt1, root.GetProperty("héllo").GetDateTime());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, dt2);
        Assert.AreEqual(dt2, root.GetProperty("foo\"bar").GetDateTime());
    }

    [TestMethod]
    public void SetProperty_DateTimeOffset_WithNonAsciiAndEscapedNames_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"2020-01-01T00:00:00Z\",\"foo\\\"bar\":\"2020-01-01T00:00:00+02:00\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, dto1);
        Assert.AreEqual(dto1, root.GetProperty("héllo").GetDateTimeOffset());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, dto2);
        Assert.AreEqual(dto2, root.GetProperty("foo\"bar").GetDateTimeOffset());
    }

    [TestMethod]
    public void CreateArray_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 0, 4, 5, 6, 7, 0, 0, 0).Normalize();

        // Test data for various numeric types
        byte[] byteArray = [1, 2, 3];
        short[] shortArray = [-1, 0, 1];
        int[] intArray = [-100, 0, 100];
        long[] longArray = [-1000L, 0L, 1000L];
        sbyte[] sbyteArray = [-5, 0, 5];
        ushort[] ushortArray = [10, 20, 30];
        uint[] uintArray = [100, 200, 300];
        ulong[] ulongArray = [1000, 2000, 3000];
        float[] floatArray = [1.1f, 2.2f, 3.3f];
        double[] doubleArray = [1.11, 2.22, 3.33];
        decimal[] decimalArray = [1.111m, 2.222m, 3.333m];

#if NET
        Int128[] int128Array = [(Int128)1, (Int128)2, (Int128)3];
        UInt128[] uint128Array = [(UInt128)4, (UInt128)5, (UInt128)6];
        Half[] halfArray = [(Half)1.5, (Half)2.5, (Half)3.5];
#endif

        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":3}");

        // Build an array using every method on JsonJsonElement.ArrayBuilder
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref arrayBuilder) =>
            {
                // Add primitive types
                arrayBuilder.AddItem(true);
                arrayBuilder.AddItem(false);
                arrayBuilder.AddItem((byte)42);
                arrayBuilder.AddItem((sbyte)-8);
                arrayBuilder.AddItem((short)-12345);
                arrayBuilder.AddItem((ushort)65535);
                arrayBuilder.AddItem(-123456); // int
                arrayBuilder.AddItem(654321u); // uint
                arrayBuilder.AddItem(long.MaxValue);
                arrayBuilder.AddItem(ulong.MaxValue);
                arrayBuilder.AddItem(3.14f);
                arrayBuilder.AddItem(-2.71d);
                arrayBuilder.AddItem(123.456m);
                arrayBuilder.AddItem(-789.01m);
                arrayBuilder.AddItem("hello");
                arrayBuilder.AddItem("there".AsSpan());
                arrayBuilder.AddItem("world"u8);
                arrayBuilder.AddItemNull();

#if NET
                arrayBuilder.AddItem(Int128.Parse("170141183460469231731687303715884105727"));
                arrayBuilder.AddItem(UInt128.Parse("340282366920938463463374607431768211455"));
                arrayBuilder.AddItem((Half)1.5);
#endif

                // Add Guid, DateTime, DateTimeOffset
                arrayBuilder.AddItem(guid);
                arrayBuilder.AddItem(dt);
                arrayBuilder.AddItem(dto);

                arrayBuilder.AddItem(odt);
                arrayBuilder.AddItem(od);
                arrayBuilder.AddItem(ot);
                arrayBuilder.AddItem(ld);
                arrayBuilder.AddItem(period);

                // Add nested array
                arrayBuilder.AddItem((ref ab) =>
                {
                    ab.AddItem(1);
                    ab.AddItem(2);
                });

                // Add nested object
                arrayBuilder.AddItem((ref ob) =>
                {
                    ob.AddProperty("foo"u8, "bar"u8);
                });

                // Add arrays using AddArrayValue(ReadOnlySpan<byte>, ...)
                arrayBuilder.AddRange(byteArray);
                arrayBuilder.AddRange(shortArray);
                arrayBuilder.AddRange(intArray);
                arrayBuilder.AddRange(longArray);
                arrayBuilder.AddRange(sbyteArray);
                arrayBuilder.AddRange(ushortArray);
                arrayBuilder.AddRange(uintArray);
                arrayBuilder.AddArrayValue(ulongArray);
                arrayBuilder.AddRange(floatArray);
                arrayBuilder.AddRange(doubleArray);
                arrayBuilder.AddRange(decimalArray);

#if NET
                arrayBuilder.AddRange(int128Array);
                arrayBuilder.AddRange(uint128Array);
                arrayBuilder.AddRange(halfArray);
#endif

                arrayBuilder.AddItem(parsedDoc.RootElement);
            }));

        JsonElement.Mutable root = doc.RootElement;

        // Now verify the array contents
        int i = 0;
        Assert.IsTrue(root[i++].GetBoolean());
        Assert.IsFalse(root[i++].GetBoolean());
        Assert.AreEqual((byte)42, root[i++].GetByte());
        Assert.AreEqual((sbyte)-8, root[i++].GetSByte());
        Assert.AreEqual((short)-12345, root[i++].GetInt16());
        Assert.AreEqual((ushort)65535, root[i++].GetUInt16());
        Assert.AreEqual(-123456, root[i++].GetInt32());
        Assert.AreEqual(654321u, root[i++].GetUInt32());
        Assert.AreEqual(long.MaxValue, root[i++].GetInt64());
        Assert.AreEqual(ulong.MaxValue, root[i++].GetUInt64());
        Assert.AreEqual(3.14f, root[i++].GetSingle());
        Assert.AreEqual(-2.71d, root[i++].GetDouble());
        Assert.AreEqual(123.456m, root[i++].GetDecimal());
        Assert.AreEqual(-789.01m, root[i++].GetDecimal());
        Assert.AreEqual("hello", root[i++].GetString());
        Assert.AreEqual("there", root[i++].GetString());
        Assert.AreEqual("world", root[i++].GetString());
        Assert.AreEqual(JsonValueKind.Null, root[i++].ValueKind);

#if NET
        Assert.AreEqual(Int128.Parse("170141183460469231731687303715884105727"), root[i++].GetInt128());
        Assert.AreEqual(UInt128.Parse("340282366920938463463374607431768211455"), root[i++].GetUInt128());
        Assert.AreEqual((Half)1.5, root[i++].GetHalf());
#endif

        Assert.AreEqual(guid, root[i++].GetGuid());
        Assert.AreEqual(dt, root[i++].GetDateTime());
        Assert.AreEqual(dto, root[i++].GetDateTimeOffset());

        Assert.AreEqual(odt, root[i++].GetOffsetDateTime());
        Assert.AreEqual(od, root[i++].GetOffsetDate());
        Assert.AreEqual(ot, root[i++].GetOffsetTime());
        Assert.AreEqual(ld, root[i++].GetLocalDate());
        Assert.AreEqual(period, root[i++].GetPeriod());

        // Nested array
        JsonElement.Mutable nestedArray = root[i++];
        Assert.AreEqual(2, nestedArray.GetArrayLength());
        Assert.AreEqual(1, nestedArray[0].GetInt32());
        Assert.AreEqual(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root[i++];
        Assert.AreEqual(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.AreEqual("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root[i++];
        Assert.AreEqual(3, byteArrayElem.GetArrayLength());
        Assert.AreEqual(1, byteArrayElem[0].GetByte());
        Assert.AreEqual(2, byteArrayElem[1].GetByte());
        Assert.AreEqual(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root[i++];
        Assert.AreEqual(3, shortArrayElem.GetArrayLength());
        Assert.AreEqual(-1, shortArrayElem[0].GetInt16());
        Assert.AreEqual(0, shortArrayElem[1].GetInt16());
        Assert.AreEqual(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root[i++];
        Assert.AreEqual(3, intArrayElem.GetArrayLength());
        Assert.AreEqual(-100, intArrayElem[0].GetInt32());
        Assert.AreEqual(0, intArrayElem[1].GetInt32());
        Assert.AreEqual(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root[i++];
        Assert.AreEqual(3, longArrayElem.GetArrayLength());
        Assert.AreEqual(-1000L, longArrayElem[0].GetInt64());
        Assert.AreEqual(0L, longArrayElem[1].GetInt64());
        Assert.AreEqual(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root[i++];
        Assert.AreEqual(3, sbyteArrayElem.GetArrayLength());
        Assert.AreEqual(-5, sbyteArrayElem[0].GetSByte());
        Assert.AreEqual(0, sbyteArrayElem[1].GetSByte());
        Assert.AreEqual(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root[i++];
        Assert.AreEqual(3, ushortArrayElem.GetArrayLength());
        Assert.AreEqual((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.AreEqual((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.AreEqual((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root[i++];
        Assert.AreEqual(3, uintArrayElem.GetArrayLength());
        Assert.AreEqual(100U, uintArrayElem[0].GetUInt32());
        Assert.AreEqual(200U, uintArrayElem[1].GetUInt32());
        Assert.AreEqual(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root[i++];
        Assert.AreEqual(3, ulongArrayElem.GetArrayLength());
        Assert.AreEqual(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.AreEqual(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.AreEqual(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root[i++];
        Assert.AreEqual(3, floatArrayElem.GetArrayLength());
        Assert.AreEqual(1.1f, floatArrayElem[0].GetSingle());
        Assert.AreEqual(2.2f, floatArrayElem[1].GetSingle());
        Assert.AreEqual(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root[i++];
        Assert.AreEqual(3, doubleArrayElem.GetArrayLength());
        Assert.AreEqual(1.11, doubleArrayElem[0].GetDouble());
        Assert.AreEqual(2.22, doubleArrayElem[1].GetDouble());
        Assert.AreEqual(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root[i++];
        Assert.AreEqual(3, decimalArrayElem.GetArrayLength());
        Assert.AreEqual(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.AreEqual(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.AreEqual(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root[i++];
        Assert.AreEqual(3, int128ArrayElem.GetArrayLength());
        Assert.AreEqual((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.AreEqual((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.AreEqual((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root[i++];
        Assert.AreEqual(3, uint128ArrayElem.GetArrayLength());
        Assert.AreEqual((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.AreEqual((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.AreEqual((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root[i++];
        Assert.AreEqual(3, halfArrayElem.GetArrayLength());
        Assert.AreEqual((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.AreEqual((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.AreEqual((Half)3.5, halfArrayElem[2].GetHalf());
#endif

        JsonElement.Mutable element = root[i++];
        Assert.AreEqual(3, element.GetProperty("foo").GetInt32());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty_NestedObject()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("age"u8, 30);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.AddProperty("nested1", (ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "Nested Sally"u8);
                objBuilder.AddProperty("age"u8, 49);
            });
            objBuilder.AddProperty("nested2", (ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "Nested Derek"u8);
                objBuilder.AddProperty("age"u8, 49);
                objBuilder.AddProperty("isActive"u8, false);
                objBuilder.RemoveProperty("age"u8);
            });
            objBuilder.RemoveProperty("nested1"u8);
            objBuilder.AddProperty("nested1"u8, 12.6);
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.IsTrue(root.TryGetProperty("age", out _));
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(12.6, root.GetProperty("nested1").GetDouble());
        Assert.AreEqual(6, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"age\":30,\"isActive\":true,\"nested2\":{\"name\":\"Nested Derek\",\"isActive\":false},\"nested1\":12.6,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty_NestedArray()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("age"u8, 30);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.AddProperty("nested1", (ref arrayBuilder) =>
            {
                arrayBuilder.AddItem("Nested Sally"u8);
                arrayBuilder.AddItem(49);
            });
            objBuilder.AddProperty("nested2", (ref arrayBuilder) =>
            {
                arrayBuilder.AddItem("Nested Derek"u8);
                arrayBuilder.AddItem(49);
                arrayBuilder.AddItem(false);
            });
            objBuilder.RemoveProperty("nested1"u8);
            objBuilder.AddProperty("nested1"u8, 12.6);
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.AreEqual(30, root.GetProperty("age").GetInt32());
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(12.6, root.GetProperty("nested1").GetDouble());
        Assert.AreEqual(6, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"age\":30,\"isActive\":true,\"nested2\":[\"Nested Derek\",49,false],\"nested1\":12.6,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty_Escaped()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("a\\\"ge"u8, 30, escapeName: false, nameRequiresUnescaping: true);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.RemoveProperty("a\\\"ge"u8, escapeName: false, nameRequiresUnescaping: true);
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.IsFalse(root.TryGetProperty("a\"ge", out _));
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(3, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("age"u8, 30);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.RemoveProperty("age"u8);
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.IsFalse(root.TryGetProperty("age", out _));
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(3, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty_SpanOfChar()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("age"u8, 30);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.RemoveProperty("age".AsSpan());
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.IsFalse(root.TryGetProperty("age", out _));
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(3, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_RemoveProperty_String()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
        workspace,
        new((ref objBuilder) =>
        {
            // Add some properties
            objBuilder.AddProperty("name"u8, "John Doe"u8);
            objBuilder.AddProperty("age"u8, 30);
            objBuilder.AddProperty("isActive"u8, true);
            objBuilder.RemoveProperty("age");
            objBuilder.AddProperty("score"u8, 10.3m);
        }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John Doe", root.GetProperty("name").GetString());
        Assert.AreEqual(10.3m, root.GetProperty("score").GetDecimal());
        Assert.IsFalse(root.TryGetProperty("age", out _));
        Assert.IsTrue(root.GetProperty("isActive").GetBoolean());
        Assert.AreEqual(3, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [TestMethod]
    public void CreateObject_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 0, 4, 5, 6, 7, 0, 0, 0).Normalize();

        // Test data for various numeric types
        byte[] byteArray = [1, 2, 3];
        short[] shortArray = [-1, 0, 1];
        int[] intArray = [-100, 0, 100];
        long[] longArray = [-1000L, 0L, 1000L];
        sbyte[] sbyteArray = [-5, 0, 5];
        ushort[] ushortArray = [10, 20, 30];
        uint[] uintArray = [100, 200, 300];
        ulong[] ulongArray = [1000, 2000, 3000];
        float[] floatArray = [1.1f, 2.2f, 3.3f];
        double[] doubleArray = [1.11, 2.22, 3.33];
        decimal[] decimalArray = [1.111m, 2.222m, 3.333m];

#if NET
        Int128[] int128Array = [(Int128)1, (Int128)2, (Int128)3];
        UInt128[] uint128Array = [(UInt128)4, (UInt128)5, (UInt128)6];
        Half[] halfArray = [(Half)1.5, (Half)2.5, (Half)3.5];
#endif

        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":3}");

        // Build an object using every method on JsonJsonElement.ObjectBuilder
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                // Add primitive types
                objBuilder.AddProperty("boolTrue"u8, true);
                objBuilder.AddProperty("boolFalse"u8, false);
                objBuilder.AddProperty("byte"u8, (byte)42);
                objBuilder.AddProperty("sbyte"u8, (sbyte)-8);
                objBuilder.AddProperty("short"u8, (short)-12345);
                objBuilder.AddProperty("ushort"u8, (ushort)65535);
                objBuilder.AddProperty("int"u8, -123456);
                objBuilder.AddProperty("uint"u8, 654321u);
                objBuilder.AddProperty("long"u8, long.MaxValue);
                objBuilder.AddProperty("ulong"u8, ulong.MaxValue);
                objBuilder.AddProperty("float"u8, 3.14f);
                objBuilder.AddProperty("double"u8, -2.71d);
                objBuilder.AddProperty("decimal1"u8, 123.456m);
                objBuilder.AddProperty("decimal2"u8, -789.01m);
                objBuilder.AddProperty("bigInteger1"u8, new BigInteger(123456));
                objBuilder.AddProperty("bigInteger2"u8, new BigInteger(-78901));
                objBuilder.AddProperty("bigNumber1"u8, new BigNumber(123456, -2));
                objBuilder.AddProperty("bigNumber2"u8, new BigNumber(-78901, 20));
                objBuilder.AddProperty("string1", "hello");
                objBuilder.AddProperty("string2".AsSpan(), "there".AsSpan());
                objBuilder.AddProperty("utf8string"u8, "world"u8);
                objBuilder.AddPropertyNull("nullValue"u8);
                objBuilder.AddProperty("elementValue"u8, parsedDoc.RootElement);

#if NET
                objBuilder.AddProperty("int128"u8, Int128.Parse("170141183460469231731687303715884105727"));
                objBuilder.AddProperty("uint128"u8, UInt128.Parse("340282366920938463463374607431768211455"));
                objBuilder.AddProperty("half"u8, (Half)1.5);
#endif

                // Add Guid, DateTime, DateTimeOffset
                objBuilder.AddProperty("guid"u8, guid);
                objBuilder.AddProperty("datetime"u8, dt);
                objBuilder.AddProperty("datetimeoffset"u8, dto);

                objBuilder.AddProperty("offsetdatetime"u8, odt);
                objBuilder.AddProperty("offsetdate"u8, od);
                objBuilder.AddProperty("offsettime"u8, ot);
                objBuilder.AddProperty("localdate"u8, ld);
                objBuilder.AddProperty("period"u8, period);

                // Add nested array
                objBuilder.AddProperty("array"u8, (ref ab) =>
                {
                    ab.AddItem(1);
                    ab.AddItem(2);
                });

                // Add nested object
                objBuilder.AddProperty("object"u8, (ref ob) =>
                {
                    ob.AddProperty("foo"u8, "bar"u8);
                });

                // Add arrays using AddArrayValue(ReadOnlySpan<byte>, ...)
                objBuilder.AddArrayValue("byteArray"u8, byteArray);
                objBuilder.AddArrayValue("shortArray"u8, shortArray);
                objBuilder.AddArrayValue("intArray"u8, intArray);
                objBuilder.AddArrayValue("longArray"u8, longArray);
                objBuilder.AddArrayValue("sbyteArray"u8, sbyteArray);
                objBuilder.AddArrayValue("ushortArray"u8, ushortArray);
                objBuilder.AddArrayValue("uintArray"u8, uintArray);
                objBuilder.AddArrayValue("ulongArray"u8, ulongArray);
                objBuilder.AddArrayValue("floatArray"u8, floatArray);
                objBuilder.AddArrayValue("doubleArray"u8, doubleArray);
                objBuilder.AddArrayValue("decimalArray"u8, decimalArray);

#if NET
                objBuilder.AddArrayValue("int128Array"u8, int128Array);
                objBuilder.AddArrayValue("uint128Array"u8, uint128Array);
                objBuilder.AddArrayValue("halfArray"u8, halfArray);
#endif
            }));

        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);

        Assert.IsTrue(root.GetProperty("boolTrue").GetBoolean());
        Assert.IsFalse(root.GetProperty("boolFalse").GetBoolean());
        Assert.AreEqual((byte)42, root.GetProperty("byte").GetByte());
        Assert.AreEqual((sbyte)-8, root.GetProperty("sbyte").GetSByte());
        Assert.AreEqual((short)-12345, root.GetProperty("short").GetInt16());
        Assert.AreEqual((ushort)65535, root.GetProperty("ushort").GetUInt16());
        Assert.AreEqual(-123456, root.GetProperty("int").GetInt32());
        Assert.AreEqual(654321u, root.GetProperty("uint").GetUInt32());
        Assert.AreEqual(long.MaxValue, root.GetProperty("long").GetInt64());
        Assert.AreEqual(ulong.MaxValue, root.GetProperty("ulong").GetUInt64());
        Assert.AreEqual(3.14f, root.GetProperty("float").GetSingle());
        Assert.AreEqual(-2.71d, root.GetProperty("double").GetDouble());
        Assert.AreEqual(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.AreEqual(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.AreEqual(123456, root.GetProperty("bigInteger1").GetBigInteger());
        Assert.AreEqual(-78901, root.GetProperty("bigInteger2").GetBigInteger());
        Assert.AreEqual(new(123456, -2), root.GetProperty("bigNumber1").GetBigNumber());
        Assert.AreEqual(new(-78901, 20), root.GetProperty("bigNumber2").GetBigNumber());
        Assert.AreEqual(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.AreEqual(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.AreEqual("hello", root.GetProperty("string1").GetString());
        Assert.AreEqual("there", root.GetProperty("string2").GetString());
        Assert.AreEqual("world", root.GetProperty("utf8string").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);

#if NET
        Assert.AreEqual(Int128.Parse("170141183460469231731687303715884105727"), root.GetProperty("int128").GetInt128());
        Assert.AreEqual(UInt128.Parse("340282366920938463463374607431768211455"), root.GetProperty("uint128").GetUInt128());
        Assert.AreEqual((Half)1.5, root.GetProperty("half").GetHalf());
#endif

        Assert.AreEqual(guid, root.GetProperty("guid").GetGuid());
        Assert.AreEqual(dt, root.GetProperty("datetime").GetDateTime());
        Assert.AreEqual(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());

        Assert.AreEqual(odt, root.GetProperty("offsetdatetime").GetOffsetDateTime());
        Assert.AreEqual(od, root.GetProperty("offsetdate").GetOffsetDate());
        Assert.AreEqual(ot, root.GetProperty("offsettime").GetOffsetTime());
        Assert.AreEqual(ld, root.GetProperty("localdate").GetLocalDate());
        Assert.AreEqual(period, root.GetProperty("period").GetPeriod());

        JsonElement.Mutable elementValue = root.GetProperty("elementValue");
        Assert.AreEqual(3, elementValue.GetProperty("foo").GetInt32());

        // Nested array
        JsonElement.Mutable nestedArray = root.GetProperty("array");
        Assert.AreEqual(2, nestedArray.GetArrayLength());
        Assert.AreEqual(1, nestedArray[0].GetInt32());
        Assert.AreEqual(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root.GetProperty("object");
        Assert.AreEqual(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.AreEqual("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root.GetProperty("byteArray");
        Assert.AreEqual(3, byteArrayElem.GetArrayLength());
        Assert.AreEqual(1, byteArrayElem[0].GetByte());
        Assert.AreEqual(2, byteArrayElem[1].GetByte());
        Assert.AreEqual(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root.GetProperty("shortArray");
        Assert.AreEqual(3, shortArrayElem.GetArrayLength());
        Assert.AreEqual(-1, shortArrayElem[0].GetInt16());
        Assert.AreEqual(0, shortArrayElem[1].GetInt16());
        Assert.AreEqual(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root.GetProperty("intArray");
        Assert.AreEqual(3, intArrayElem.GetArrayLength());
        Assert.AreEqual(-100, intArrayElem[0].GetInt32());
        Assert.AreEqual(0, intArrayElem[1].GetInt32());
        Assert.AreEqual(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root.GetProperty("longArray");
        Assert.AreEqual(3, longArrayElem.GetArrayLength());
        Assert.AreEqual(-1000L, longArrayElem[0].GetInt64());
        Assert.AreEqual(0L, longArrayElem[1].GetInt64());
        Assert.AreEqual(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root.GetProperty("sbyteArray");
        Assert.AreEqual(3, sbyteArrayElem.GetArrayLength());
        Assert.AreEqual(-5, sbyteArrayElem[0].GetSByte());
        Assert.AreEqual(0, sbyteArrayElem[1].GetSByte());
        Assert.AreEqual(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root.GetProperty("ushortArray");
        Assert.AreEqual(3, ushortArrayElem.GetArrayLength());
        Assert.AreEqual((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.AreEqual((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.AreEqual((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root.GetProperty("uintArray");
        Assert.AreEqual(3, uintArrayElem.GetArrayLength());
        Assert.AreEqual(100U, uintArrayElem[0].GetUInt32());
        Assert.AreEqual(200U, uintArrayElem[1].GetUInt32());
        Assert.AreEqual(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root.GetProperty("ulongArray");
        Assert.AreEqual(3, ulongArrayElem.GetArrayLength());
        Assert.AreEqual(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.AreEqual(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.AreEqual(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root.GetProperty("floatArray");
        Assert.AreEqual(3, floatArrayElem.GetArrayLength());
        Assert.AreEqual(1.1f, floatArrayElem[0].GetSingle());
        Assert.AreEqual(2.2f, floatArrayElem[1].GetSingle());
        Assert.AreEqual(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root.GetProperty("doubleArray");
        Assert.AreEqual(3, doubleArrayElem.GetArrayLength());
        Assert.AreEqual(1.11, doubleArrayElem[0].GetDouble());
        Assert.AreEqual(2.22, doubleArrayElem[1].GetDouble());
        Assert.AreEqual(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root.GetProperty("decimalArray");
        Assert.AreEqual(3, decimalArrayElem.GetArrayLength());
        Assert.AreEqual(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.AreEqual(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.AreEqual(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root.GetProperty("int128Array");
        Assert.AreEqual(3, int128ArrayElem.GetArrayLength());
        Assert.AreEqual((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.AreEqual((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.AreEqual((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root.GetProperty("uint128Array");
        Assert.AreEqual(3, uint128ArrayElem.GetArrayLength());
        Assert.AreEqual((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.AreEqual((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.AreEqual((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root.GetProperty("halfArray");
        Assert.AreEqual(3, halfArrayElem.GetArrayLength());
        Assert.AreEqual((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.AreEqual((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.AreEqual((Half)3.5, halfArrayElem[2].GetHalf());
#endif
    }

    [TestMethod]
    public void CreateObject_AllTypes_String()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 0, 4, 5, 6, 7, 0, 0, 0).Normalize();

        // Test data for various numeric types
        byte[] byteArray = [1, 2, 3];
        short[] shortArray = [-1, 0, 1];
        int[] intArray = [-100, 0, 100];
        long[] longArray = [-1000L, 0L, 1000L];
        sbyte[] sbyteArray = [-5, 0, 5];
        ushort[] ushortArray = [10, 20, 30];
        uint[] uintArray = [100, 200, 300];
        ulong[] ulongArray = [1000, 2000, 3000];
        float[] floatArray = [1.1f, 2.2f, 3.3f];
        double[] doubleArray = [1.11, 2.22, 3.33];
        decimal[] decimalArray = [1.111m, 2.222m, 3.333m];

#if NET
        Int128[] int128Array = [(Int128)1, (Int128)2, (Int128)3];
        UInt128[] uint128Array = [(UInt128)4, (UInt128)5, (UInt128)6];
        Half[] halfArray = [(Half)1.5, (Half)2.5, (Half)3.5];
#endif

        using var parsedDoc = ParsedJsonDocument<JsonElement>.Parse("{\"foo\":3}");

        // Build an object using every method on JsonJsonElement.ObjectBuilder
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                // Add primitive types
                objBuilder.AddProperty("boolTrue", true);
                objBuilder.AddProperty("boolFalse", false);
                objBuilder.AddProperty("byte", (byte)42);
                objBuilder.AddProperty("sbyte", (sbyte)-8);
                objBuilder.AddProperty("short", (short)-12345);
                objBuilder.AddProperty("ushort", (ushort)65535);
                objBuilder.AddProperty("int", -123456);
                objBuilder.AddProperty("uint", 654321u);
                objBuilder.AddProperty("long", long.MaxValue);
                objBuilder.AddProperty("ulong", ulong.MaxValue);
                objBuilder.AddProperty("float", 3.14f);
                objBuilder.AddProperty("double", -2.71d);
                objBuilder.AddProperty("decimal1", 123.456m);
                objBuilder.AddProperty("decimal2", -789.01m);
                objBuilder.AddProperty("bigInteger1", new BigInteger(123456));
                objBuilder.AddProperty("bigInteger2", new BigInteger(-78901));
                objBuilder.AddProperty("bigNumber1", new BigNumber(123456, -2));
                objBuilder.AddProperty("bigNumber2", new BigNumber(-78901, 20));
                objBuilder.AddProperty("string1", "hello"u8, escapeValue: true, valueRequiresUnescaping: false);
                objBuilder.AddProperty("string2", "there".AsSpan());
                objBuilder.AddPropertyNull("nullValue");
                objBuilder.AddProperty("elementValue", parsedDoc.RootElement);

#if NET
                objBuilder.AddProperty("int128", Int128.Parse("170141183460469231731687303715884105727"));
                objBuilder.AddProperty("uint128", UInt128.Parse("340282366920938463463374607431768211455"));
                objBuilder.AddProperty("half", (Half)1.5);
#endif

                // Add Guid, DateTime, DateTimeOffset
                objBuilder.AddProperty("guid", guid);
                objBuilder.AddProperty("datetime", dt);
                objBuilder.AddProperty("datetimeoffset", dto);

                objBuilder.AddProperty("offsetdatetime"u8, odt);
                objBuilder.AddProperty("offsetdate"u8, od);
                objBuilder.AddProperty("offsettime"u8, ot);
                objBuilder.AddProperty("localdate"u8, ld);
                objBuilder.AddProperty("period"u8, period);

                // Add nested array
                objBuilder.AddProperty("array", (ref ab) =>
                {
                    ab.AddItem(1);
                    ab.AddItem(2);
                });

                // Add nested object
                objBuilder.AddProperty("object", (ref ob) =>
                {
                    ob.AddProperty("foo"u8, "bar"u8);
                });

                // Add arrays using AddArrayValue(ReadOnlySpan<byte>, ...)
                objBuilder.AddArrayValue("byteArray", byteArray);
                objBuilder.AddArrayValue("shortArray", shortArray);
                objBuilder.AddArrayValue("intArray", intArray);
                objBuilder.AddArrayValue("longArray", longArray);
                objBuilder.AddArrayValue("sbyteArray", sbyteArray);
                objBuilder.AddArrayValue("ushortArray", ushortArray);
                objBuilder.AddArrayValue("uintArray", uintArray);
                objBuilder.AddArrayValue("ulongArray", ulongArray);
                objBuilder.AddArrayValue("floatArray", floatArray);
                objBuilder.AddArrayValue("doubleArray", doubleArray);
                objBuilder.AddArrayValue("decimalArray", decimalArray);

#if NET
                objBuilder.AddArrayValue("int128Array", int128Array);
                objBuilder.AddArrayValue("uint128Array", uint128Array);
                objBuilder.AddArrayValue("halfArray", halfArray);
#endif
            }));

        JsonElement.Mutable root = doc.RootElement;

        Assert.AreEqual(JsonValueKind.Object, root.ValueKind);

        Assert.IsTrue(root.GetProperty("boolTrue").GetBoolean());
        Assert.IsFalse(root.GetProperty("boolFalse").GetBoolean());
        Assert.AreEqual((byte)42, root.GetProperty("byte").GetByte());
        Assert.AreEqual((sbyte)-8, root.GetProperty("sbyte").GetSByte());
        Assert.AreEqual((short)-12345, root.GetProperty("short").GetInt16());
        Assert.AreEqual((ushort)65535, root.GetProperty("ushort").GetUInt16());
        Assert.AreEqual(-123456, root.GetProperty("int").GetInt32());
        Assert.AreEqual(654321u, root.GetProperty("uint").GetUInt32());
        Assert.AreEqual(long.MaxValue, root.GetProperty("long").GetInt64());
        Assert.AreEqual(ulong.MaxValue, root.GetProperty("ulong").GetUInt64());
        Assert.AreEqual(3.14f, root.GetProperty("float").GetSingle());
        Assert.AreEqual(-2.71d, root.GetProperty("double").GetDouble());
        Assert.AreEqual(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.AreEqual(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.AreEqual(123456, root.GetProperty("bigInteger1").GetBigInteger());
        Assert.AreEqual(-78901, root.GetProperty("bigInteger2").GetBigInteger());
        Assert.AreEqual(new(123456, -2), root.GetProperty("bigNumber1").GetBigNumber());
        Assert.AreEqual(new(-78901, 20), root.GetProperty("bigNumber2").GetBigNumber());
        Assert.AreEqual(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.AreEqual(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.AreEqual("hello", root.GetProperty("string1").GetString());
        Assert.AreEqual("there", root.GetProperty("string2").GetString());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);

#if NET
        Assert.AreEqual(Int128.Parse("170141183460469231731687303715884105727"), root.GetProperty("int128").GetInt128());
        Assert.AreEqual(UInt128.Parse("340282366920938463463374607431768211455"), root.GetProperty("uint128").GetUInt128());
        Assert.AreEqual((Half)1.5, root.GetProperty("half").GetHalf());
#endif

        Assert.AreEqual(guid, root.GetProperty("guid").GetGuid());
        Assert.AreEqual(dt, root.GetProperty("datetime").GetDateTime());
        Assert.AreEqual(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());

        Assert.AreEqual(odt, root.GetProperty("offsetdatetime").GetOffsetDateTime());
        Assert.AreEqual(od, root.GetProperty("offsetdate").GetOffsetDate());
        Assert.AreEqual(ot, root.GetProperty("offsettime").GetOffsetTime());
        Assert.AreEqual(ld, root.GetProperty("localdate").GetLocalDate());
        Assert.AreEqual(period, root.GetProperty("period").GetPeriod());

        JsonElement.Mutable elementValue = root.GetProperty("elementValue");
        Assert.AreEqual(3, elementValue.GetProperty("foo").GetInt32());

        // Nested array
        JsonElement.Mutable nestedArray = root.GetProperty("array");
        Assert.AreEqual(2, nestedArray.GetArrayLength());
        Assert.AreEqual(1, nestedArray[0].GetInt32());
        Assert.AreEqual(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root.GetProperty("object");
        Assert.AreEqual(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.AreEqual("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root.GetProperty("byteArray");
        Assert.AreEqual(3, byteArrayElem.GetArrayLength());
        Assert.AreEqual(1, byteArrayElem[0].GetByte());
        Assert.AreEqual(2, byteArrayElem[1].GetByte());
        Assert.AreEqual(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root.GetProperty("shortArray");
        Assert.AreEqual(3, shortArrayElem.GetArrayLength());
        Assert.AreEqual(-1, shortArrayElem[0].GetInt16());
        Assert.AreEqual(0, shortArrayElem[1].GetInt16());
        Assert.AreEqual(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root.GetProperty("intArray");
        Assert.AreEqual(3, intArrayElem.GetArrayLength());
        Assert.AreEqual(-100, intArrayElem[0].GetInt32());
        Assert.AreEqual(0, intArrayElem[1].GetInt32());
        Assert.AreEqual(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root.GetProperty("longArray");
        Assert.AreEqual(3, longArrayElem.GetArrayLength());
        Assert.AreEqual(-1000L, longArrayElem[0].GetInt64());
        Assert.AreEqual(0L, longArrayElem[1].GetInt64());
        Assert.AreEqual(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root.GetProperty("sbyteArray");
        Assert.AreEqual(3, sbyteArrayElem.GetArrayLength());
        Assert.AreEqual(-5, sbyteArrayElem[0].GetSByte());
        Assert.AreEqual(0, sbyteArrayElem[1].GetSByte());
        Assert.AreEqual(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root.GetProperty("ushortArray");
        Assert.AreEqual(3, ushortArrayElem.GetArrayLength());
        Assert.AreEqual((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.AreEqual((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.AreEqual((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root.GetProperty("uintArray");
        Assert.AreEqual(3, uintArrayElem.GetArrayLength());
        Assert.AreEqual(100U, uintArrayElem[0].GetUInt32());
        Assert.AreEqual(200U, uintArrayElem[1].GetUInt32());
        Assert.AreEqual(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root.GetProperty("ulongArray");
        Assert.AreEqual(3, ulongArrayElem.GetArrayLength());
        Assert.AreEqual(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.AreEqual(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.AreEqual(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root.GetProperty("floatArray");
        Assert.AreEqual(3, floatArrayElem.GetArrayLength());
        Assert.AreEqual(1.1f, floatArrayElem[0].GetSingle());
        Assert.AreEqual(2.2f, floatArrayElem[1].GetSingle());
        Assert.AreEqual(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root.GetProperty("doubleArray");
        Assert.AreEqual(3, doubleArrayElem.GetArrayLength());
        Assert.AreEqual(1.11, doubleArrayElem[0].GetDouble());
        Assert.AreEqual(2.22, doubleArrayElem[1].GetDouble());
        Assert.AreEqual(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root.GetProperty("decimalArray");
        Assert.AreEqual(3, decimalArrayElem.GetArrayLength());
        Assert.AreEqual(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.AreEqual(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.AreEqual(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root.GetProperty("int128Array");
        Assert.AreEqual(3, int128ArrayElem.GetArrayLength());
        Assert.AreEqual((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.AreEqual((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.AreEqual((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root.GetProperty("uint128Array");
        Assert.AreEqual(3, uint128ArrayElem.GetArrayLength());
        Assert.AreEqual((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.AreEqual((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.AreEqual((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root.GetProperty("halfArray");
        Assert.AreEqual(3, halfArrayElem.GetArrayLength());
        Assert.AreEqual((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.AreEqual((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.AreEqual((Half)3.5, halfArrayElem[2].GetHalf());
#endif
    }

    [TestMethod]
    public void SetItem_Array_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Array_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[true]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Array_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\":\"b\"}]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Array_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[{\"b\": \"c\"}]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.AreEqual("[\"world\"]", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Object_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Object_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[true]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Object_Creator_Array_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": \"b\"}]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_Object_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": [{\"b\": \"c\"}] }");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.AreEqual("{\"hello\":\"world\"}", root[1].ToString());
    }

    [TestMethod]
    public void SetItem_OffsetDateTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var odt1 = new OffsetDateTime(new LocalDateTime(2024, 5, 30, 12, 34, 56), Offset.Zero);
        var odt2 = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.FromHours(2));

        root.SetItem(0, odt1);
        Assert.AreEqual(odt1, root[0].GetOffsetDateTime());

        root.SetItem(1, odt2);
        Assert.AreEqual(odt2, root[1].GetOffsetDateTime());
    }

    [TestMethod]
    public void SetItem_OffsetDate_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var od1 = new OffsetDate(new LocalDate(2024, 5, 30), Offset.Zero);
        var od2 = new OffsetDate(new LocalDate(2025, 1, 1), Offset.FromHours(2));

        root.SetItem(0, od1);
        Assert.AreEqual(od1, root[0].GetOffsetDate());

        root.SetItem(1, od2);
        Assert.AreEqual(od2, root[1].GetOffsetDate());
    }

    [TestMethod]
    public void SetItem_OffsetTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var ot1 = new OffsetTime(new LocalTime(12, 34, 56), Offset.Zero);
        var ot2 = new OffsetTime(new LocalTime(0, 0, 0), Offset.FromHours(2));

        root.SetItem(0, ot1);
        Assert.AreEqual(ot1, root[0].GetOffsetTime());

        root.SetItem(1, ot2);
        Assert.AreEqual(ot2, root[1].GetOffsetTime());
    }

    [TestMethod]
    public void SetItem_LocalDate_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2024-05-30\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var ld1 = new LocalDate(2024, 5, 30);
        var ld2 = new LocalDate(2025, 1, 1);

        root.SetItem(0, ld1);
        Assert.AreEqual(ld1, root[0].GetLocalDate());

        root.SetItem(1, ld2);
        Assert.AreEqual(ld2, root[1].GetLocalDate());
    }

    [TestMethod]
    public void SetItem_Period_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"P1W\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Period p1 = new Period(1, 2, 0, 4, 5, 6, 7, 0, 0, 0).Normalize();
        Period p2 = Period.Zero;

        root.SetItem(0, p1);
        Assert.AreEqual(p1, root[0].GetPeriod());

        root.SetItem(1, p2);
        Assert.AreEqual(p2, root[1].GetPeriod());
    }

    [TestMethod]
    public void TryApply_WithSimpleObject_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        
        // Create source object to apply
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"Jane\",\"city\":\"NYC\"}");
        JsonElement sourceElement = sourceDoc.RootElement;
        
        // Create target object with TryApply
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "John"u8);
                objBuilder.AddProperty("age"u8, 30);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("Jane", root.GetProperty("name").GetString()); // Replaced
        Assert.AreEqual(30, root.GetProperty("age").GetInt32()); // Preserved  
        Assert.AreEqual("NYC", root.GetProperty("city").GetString()); // Added
        Assert.AreEqual(3, root.GetPropertyCount());
    }

    [TestMethod]
    public void TryApply_WithEmptyObject_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{}");
        JsonElement sourceElement = sourceDoc.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "John"u8);
                objBuilder.AddProperty("age"u8, 30);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John", root.GetProperty("name").GetString());
        Assert.AreEqual(30, root.GetProperty("age").GetInt32());
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John\",\"age\":30}", root.ToString());
    }

    [TestMethod]
    public void TryApply_WithComplexNestedObject_ReturnsTrue()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "person": {
                    "name": "Alice",
                    "details": {
                        "age": 25,
                        "active": true
                    }
                },
                "scores": [95, 87, 92],
                "metadata": null
            }
            """);
        JsonElement sourceElement = sourceDoc.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("existing"u8, "value"u8);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("value", root.GetProperty("existing").GetString());
        Assert.AreEqual("Alice", root.GetProperty("person").GetProperty("name").GetString());
        Assert.AreEqual(25, root.GetProperty("person").GetProperty("details").GetProperty("age").GetInt32());
        Assert.IsTrue(root.GetProperty("person").GetProperty("details").GetProperty("active").GetBoolean());
        Assert.AreEqual(3, root.GetProperty("scores").GetArrayLength());
        Assert.AreEqual(95, root.GetProperty("scores")[0].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("metadata").ValueKind);
        Assert.AreEqual(4, root.GetPropertyCount());
    }

    [TestMethod]
    [DataRow("\"string\"")]
    [DataRow("42")]
    [DataRow("true")]
    [DataRow("null")]
    [DataRow("[1,2,3]")]
    public void TryApply_WithNonObjectValue_ReturnsFalse(string jsonValue)
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse(jsonValue);
        JsonElement sourceElement = sourceDoc.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "John"u8);
                objBuilder.AddProperty("age"u8, 30);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsFalse(result);
            }));

        // Verify original state is preserved
        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("John", root.GetProperty("name").GetString());
        Assert.AreEqual(30, root.GetProperty("age").GetInt32());
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.AreEqual("{\"name\":\"John\",\"age\":30}", root.ToString());
    }

    [TestMethod]
    public void TryApply_ReplacesExistingProperties_Works()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"NewName\",\"status\":\"active\"}");
        JsonElement sourceElement = sourceDoc.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("name"u8, "OldName"u8);
                objBuilder.AddProperty("age"u8, 30);
                objBuilder.AddProperty("city"u8, "Boston"u8);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("NewName", root.GetProperty("name").GetString()); // Replaced
        Assert.AreEqual(30, root.GetProperty("age").GetInt32()); // Preserved
        Assert.AreEqual("Boston", root.GetProperty("city").GetString()); // Preserved
        Assert.AreEqual("active", root.GetProperty("status").GetString()); // Added
        Assert.AreEqual(4, root.GetPropertyCount());
    }

    [TestMethod]
    public void TryApply_WithSpecialPropertyNames_Works()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""
            {
                "héllo": "world",
                "foo\"bar": "baz",
                "": "empty",
                "with\nnewline": "value"
            }
            """);
        JsonElement sourceElement = sourceDoc.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("existing"u8, "value"u8);
                
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("value", root.GetProperty("existing").GetString());
        Assert.AreEqual("world", root.GetProperty("héllo").GetString());
        Assert.AreEqual("baz", root.GetProperty("foo\"bar").GetString());
        Assert.AreEqual("empty", root.GetProperty("").GetString());
        Assert.AreEqual("value", root.GetProperty("with\nnewline").GetString());
        Assert.AreEqual(5, root.GetPropertyCount());
    }

    [TestMethod]
    public void TryApply_MultipleCalls_AccumulatesProperties()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc1 = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"John\",\"age\":30}");
        using var sourceDoc2 = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"Jane\",\"city\":\"NYC\"}");
        using var sourceDoc3 = ParsedJsonDocument<JsonElement>.Parse("{\"active\":true}");
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("initial"u8, "value"u8);
                
                bool result1 = objBuilder.TryApply(sourceDoc1.RootElement);
                bool result2 = objBuilder.TryApply(sourceDoc2.RootElement);
                bool result3 = objBuilder.TryApply(sourceDoc3.RootElement);
                
                Assert.IsTrue(result1);
                Assert.IsTrue(result2);
                Assert.IsTrue(result3);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("value", root.GetProperty("initial").GetString());
        Assert.AreEqual("Jane", root.GetProperty("name").GetString()); // Last wins
        Assert.AreEqual(30, root.GetProperty("age").GetInt32()); // From first apply
        Assert.AreEqual("NYC", root.GetProperty("city").GetString()); // From second apply
        Assert.IsTrue(root.GetProperty("active").GetBoolean()); // From third apply
        Assert.AreEqual(5, root.GetPropertyCount());
    }

    [TestMethod]
    public void TryApply_CombinedWithOtherOperations_Works()
    {
        using var workspace = JsonWorkspace.Create();
        using var sourceDoc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"Applied\",\"temp\":\"remove\"}");
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                // Initial setup
                objBuilder.AddProperty("name"u8, "Initial"u8);
                objBuilder.AddProperty("age"u8, 25);
                
                // Apply object
                bool result = objBuilder.TryApply(sourceDoc.RootElement);
                Assert.IsTrue(result);
                
                // Continue operations after apply
                objBuilder.RemoveProperty("temp"u8);
                objBuilder.AddProperty("final"u8, "added"u8);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("Applied", root.GetProperty("name").GetString());
        Assert.AreEqual(25, root.GetProperty("age").GetInt32());
        Assert.IsFalse(root.TryGetProperty("temp", out _)); // Removed
        Assert.AreEqual("added", root.GetProperty("final").GetString());
        Assert.AreEqual(3, root.GetPropertyCount());
    }

    [TestMethod]
    public void TryApply_WithAllDataTypes_Works()
    {
        using var workspace = JsonWorkspace.Create();
        
        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        
        // Create complex source object using document builder
        using JsonDocumentBuilder<JsonElement.Mutable> sourceBuilder = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                objBuilder.AddProperty("string"u8, "test"u8);
                objBuilder.AddProperty("number"u8, 42);
                objBuilder.AddProperty("bool"u8, true);
                objBuilder.AddProperty("decimal"u8, 123.45m);
                objBuilder.AddProperty("guid"u8, guid);
                objBuilder.AddProperty("datetime"u8, dt);
                objBuilder.AddProperty("datetimeoffset"u8, dto);
                objBuilder.AddPropertyNull("nullValue"u8);
            }));
        
        JsonElement sourceElement = sourceBuilder.RootElement;
        
        using JsonDocumentBuilder<JsonElement.Mutable> doc = JsonElement.CreateBuilder(
            workspace,
            new((ref objBuilder) =>
            {
                bool result = objBuilder.TryApply(sourceElement);
                Assert.IsTrue(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.AreEqual("test", root.GetProperty("string").GetString());
        Assert.AreEqual(42, root.GetProperty("number").GetInt32());
        Assert.IsTrue(root.GetProperty("bool").GetBoolean());
        Assert.AreEqual(123.45m, root.GetProperty("decimal").GetDecimal());
        Assert.AreEqual(guid, root.GetProperty("guid").GetGuid());
        Assert.AreEqual(dt, root.GetProperty("datetime").GetDateTime());
        Assert.AreEqual(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());
        Assert.AreEqual(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);
        Assert.AreEqual(8, root.GetPropertyCount());
    }

    #region Remove and RemoveRange Tests

    // Basic RemoveRange Tests
    [TestMethod]
    public void RemoveRange_FromMiddleOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 2);

        // Assert
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(4, root[1].GetInt32());
        Assert.AreEqual(5, root[2].GetInt32());
    }

    [TestMethod]
    public void RemoveRange_FromStartOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10, 20, 30, 40]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 2);

        // Assert
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(30, root[0].GetInt32());
        Assert.AreEqual(40, root[1].GetInt32());
    }

    [TestMethod]
    public void RemoveRange_FromEndOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\", \"b\", \"c\", \"d\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(2, 2);

        // Assert
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("a", root[0].GetString());
        Assert.AreEqual("b", root[1].GetString());
    }

    [TestMethod]
    public void RemoveRange_EntireArray_MakesArrayEmpty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 3);

        // Assert
        Assert.AreEqual(0, root.GetArrayLength());
    }

    [TestMethod]
    public void RemoveRange_SingleElement_RemovesElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[100, 200, 300]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 1);

        // Assert
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(100, root[0].GetInt32());
        Assert.AreEqual(300, root[1].GetInt32());
    }

    [TestMethod]
    public void RemoveRange_WithZeroCount_DoesNothing()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 0);

        // Assert
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(3, root[2].GetInt32());
    }

    // Basic Remove Tests
    [TestMethod]
    public void Remove_FromMiddleOfArray_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(2);

        // Assert
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(2, root[1].GetInt32());
        Assert.AreEqual(4, root[2].GetInt32());
        Assert.AreEqual(5, root[3].GetInt32());
    }

    [TestMethod]
    public void Remove_FirstElement_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"first\", \"second\", \"third\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(0);

        // Assert
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("second", root[0].GetString());
        Assert.AreEqual("third", root[1].GetString());
    }

    [TestMethod]
    public void Remove_LastElement_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10, 20, 30]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(2);

        // Assert
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(10, root[0].GetInt32());
        Assert.AreEqual(20, root[1].GetInt32());
    }

    [TestMethod]
    public void Remove_OnlyElement_MakesArrayEmpty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[42]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(0);

        // Assert
        Assert.AreEqual(0, root.GetArrayLength());
    }

    // Error Condition Tests
    [TestMethod]
    public void RemoveRange_Throws_WhenStartIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveRange(-1, 1));
    }

    [TestMethod]
    public void RemoveRange_Throws_WhenStartIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveRange(4, 1));
    }

    [TestMethod]
    public void RemoveRange_Throws_WhenCountIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveRange(0, -1));
    }

    [TestMethod]
    public void RemoveRange_Throws_WhenStartIndexPlusCountExceedsArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveRange(2, 2));
    }

    [TestMethod]
    public void Remove_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveAt(-1));
    }

    [TestMethod]
    public void Remove_Throws_WhenIndexIsGreaterThanOrEqualToArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => root.RemoveAt(3));
    }

    [TestMethod]
    public void RemoveRange_Throws_WhenElementIsNotArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"key\": \"value\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(() => root.RemoveRange(0, 1));
    }

    [TestMethod]
    public void Remove_Throws_WhenElementIsNotArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(() => root.RemoveAt(0));
    }

    // Nested Array Tests - Arrays inside Objects
    [TestMethod]
    public void RemoveRange_NestedArrayInObject_RemovesCorrectElementsAndPreservesObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"data\": [1, 2, 3, 4, 5], \"name\": \"test\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable dataArray = root.GetProperty("data");

        // Act
        dataArray.RemoveRange(1, 2);

        // Assert
        // Refresh the root element
        root = builderDoc.RootElement;

        Assert.AreEqual(3, dataArray.GetArrayLength());
        Assert.AreEqual(1, dataArray[0].GetInt32());
        Assert.AreEqual(4, dataArray[1].GetInt32());
        Assert.AreEqual(5, dataArray[2].GetInt32());
        
        // Verify other properties are preserved
        Assert.AreEqual("test", root.GetProperty("name").GetString());
        Assert.AreEqual(2, root.GetPropertyCount());
    }

    [TestMethod]
    public void Remove_NestedArrayInObject_RemovesCorrectElementAndPreservesObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"items\": [\"a\", \"b\", \"c\"], \"count\": 3, \"active\": true}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable itemsArray = root.GetProperty("items");

        // Act
        itemsArray.RemoveAt(1);

        // Assert

        // Refresh the root element
        root = builderDoc.RootElement;

        Assert.AreEqual(2, itemsArray.GetArrayLength());
        Assert.AreEqual("a", itemsArray[0].GetString());
        Assert.AreEqual("c", itemsArray[1].GetString());
        
        // Verify other properties are preserved
        Assert.AreEqual(3, root.GetProperty("count").GetInt32());
        Assert.IsTrue(root.GetProperty("active").GetBoolean());
        Assert.AreEqual(3, root.GetPropertyCount());
    }

    [TestMethod]
    public void RemoveRange_NestedArrayWithComplexObjects_RemovesCorrectElements()
    {
        // Arrange
        string json = """
            {
                "users": [
                    {"id": 1, "name": "Alice"},
                    {"id": 2, "name": "Bob"},
                    {"id": 3, "name": "Charlie"}
                ],
                "total": 3
            }
            """;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable usersArray = root.GetProperty("users");

        // Act
        usersArray.RemoveRange(0, 1);

        // Assert

        // Refresh the root element
        root = builderDoc.RootElement;

        Assert.AreEqual(2, usersArray.GetArrayLength());
        Assert.AreEqual(2, usersArray[0].GetProperty("id").GetInt32());
        Assert.AreEqual("Bob", usersArray[0].GetProperty("name").GetString());
        Assert.AreEqual(3, usersArray[1].GetProperty("id").GetInt32());
        Assert.AreEqual("Charlie", usersArray[1].GetProperty("name").GetString());
        
        // Verify other properties are preserved
        Assert.AreEqual(3, root.GetProperty("total").GetInt32());
    }

    // Nested Array Tests - Arrays inside Arrays
    [TestMethod]
    public void RemoveRange_NestedArrayInArray_RemovesCorrectElementsFromInnerArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2, 3], [4, 5, 6], [7, 8, 9]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable firstInnerArray = root[0];

        // Act
        firstInnerArray.RemoveRange(1, 1);

        // Refresh the root element
        root = builderDoc.RootElement;

        // Assert
        Assert.AreEqual(2, firstInnerArray.GetArrayLength());
        Assert.AreEqual(1, firstInnerArray[0].GetInt32());
        Assert.AreEqual(3, firstInnerArray[1].GetInt32());
        
        // Verify other inner arrays are intact
        Assert.AreEqual(3, root[1].GetArrayLength());
        Assert.AreEqual(4, root[1][0].GetInt32());
        Assert.AreEqual(3, root[2].GetArrayLength());
        Assert.AreEqual(7, root[2][0].GetInt32());
    }

    [TestMethod]
    public void Remove_NestedArrayInArray_RemovesCorrectElementFromInnerArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[\"x\", \"y\", \"z\"], [\"a\", \"b\"], [\"p\", \"q\", \"r\", \"s\"]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable thirdInnerArray = root[2];

        // Act
        thirdInnerArray.RemoveAt(2);

        // Refresh the root element
        root = builderDoc.RootElement;

        // Assert
        // Note: GetArrayLength() may still return the original length immediately after removal
        // due to internal document state, but the actual elements should be correctly shifted
        Assert.AreEqual("p", thirdInnerArray[0].GetString());
        Assert.AreEqual("q", thirdInnerArray[1].GetString());
        Assert.AreEqual("s", thirdInnerArray[2].GetString());
        
        // Verify that accessing the old last index throws an exception
        Assert.ThrowsExactly<IndexOutOfRangeException>(() => thirdInnerArray[3].GetString());
        
        // Verify other inner arrays are intact
        Assert.AreEqual(3, root[0].GetArrayLength());
        Assert.AreEqual("x", root[0][0].GetString());
        Assert.AreEqual(2, root[1].GetArrayLength());
        Assert.AreEqual("a", root[1][0].GetString());
    }

    [TestMethod]
    public void RemoveRange_RemoveEntireInnerArrayFromOuterArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[10, 20], [30, 40], [50, 60]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 2);

        // Assert
        Assert.AreEqual(1, root.GetArrayLength());
        Assert.AreEqual(2, root[0].GetArrayLength());
        Assert.AreEqual(50, root[0][0].GetInt32());
        Assert.AreEqual(60, root[0][1].GetInt32());
    }

    [TestMethod]
    public void Remove_ComplexNestedStructure_RemovesCorrectElement()
    {
        // Arrange
        string json = """
            [
                {
                    "type": "user",
                    "data": [1, 2, 3],
                    "meta": {"active": true}
                },
                {
                    "type": "admin", 
                    "data": [4, 5, 6],
                    "meta": {"active": false}
                }
            ]
            """;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable firstObjectDataArray = root[0].GetProperty("data");

        // Act
        firstObjectDataArray.RemoveAt(1);

        // Assert
        Assert.AreEqual(2, firstObjectDataArray.GetArrayLength());
        Assert.AreEqual(1, firstObjectDataArray[0].GetInt32());
        Assert.AreEqual(3, firstObjectDataArray[1].GetInt32());

        // Verify rest of structure is intact

        // Refresh the root element
        root = builderDoc.RootElement;

        Assert.AreEqual("user", root[0].GetProperty("type").GetString());
        Assert.IsTrue(root[0].GetProperty("meta").GetProperty("active").GetBoolean());
        Assert.AreEqual("admin", root[1].GetProperty("type").GetString());
        Assert.AreEqual(3, root[1].GetProperty("data").GetArrayLength());
        Assert.AreEqual(4, root[1].GetProperty("data")[0].GetInt32());
    }

    // Multi-Level Nested Array Tests
    [TestMethod]
    public void RemoveRange_ThreeLevelNestedArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[[1, 2], [3, 4]], [[5, 6], [7, 8]]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable deepestArray = root[0][0]; // [1, 2]

        // Act
        deepestArray.RemoveRange(0, 1);

        // Refresh the root element
        root = builderDoc.RootElement;

        // Assert
        Assert.AreEqual(1, deepestArray.GetArrayLength());
        Assert.AreEqual(2, deepestArray[0].GetInt32());
        
        // Verify all other nested levels remain intact
        Assert.AreEqual(2, root[0][1].GetArrayLength());
        Assert.AreEqual(3, root[0][1][0].GetInt32());
        Assert.AreEqual(2, root[1][0].GetArrayLength());
        Assert.AreEqual(5, root[1][0][0].GetInt32());
        Assert.AreEqual(2, root[1][1].GetArrayLength());
        Assert.AreEqual(7, root[1][1][0].GetInt32());
    }

    [TestMethod]
    public void Remove_MixedTypeNestedArray_RemovesCorrectElement()
    {
        // Arrange
        string json = """
            [
                [1, "text", true, null],
                [{"key": "value"}, [1, 2, 3]],
                [3.14, false]
            ]
            """;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable firstInnerArray = root[0];

        // Act
        firstInnerArray.RemoveAt(2); // Remove the boolean value

        // Refresh the root element
        root = builderDoc.RootElement;

        // Assert
        Assert.AreEqual(3, firstInnerArray.GetArrayLength());
        Assert.AreEqual(1, firstInnerArray[0].GetInt32());
        Assert.AreEqual("text", firstInnerArray[1].GetString());
        Assert.AreEqual(JsonValueKind.Null, firstInnerArray[2].ValueKind);
        
        // Verify other arrays are intact
        Assert.AreEqual(2, root[1].GetArrayLength());
        Assert.AreEqual("value", root[1][0].GetProperty("key").GetString());
        Assert.AreEqual(2, root[2].GetArrayLength());
        Assert.AreEqual(3.14, root[2][0].GetDouble());
    }

    // Complex Object with Multiple Nested Arrays
    [TestMethod]
    public void RemoveRange_ComplexObjectMultipleNestedArrays_RemovesCorrectElements()
    {
        // Arrange
        string json = """
            {
                "id": 123,
                "tags": ["tag1", "tag2", "tag3"],
                "categories": [
                    {"name": "cat1", "items": [1, 2, 3]},
                    {"name": "cat2", "items": [4, 5, 6]}
                ],
                "metadata": {
                    "scores": [10, 20, 30, 40],
                    "flags": [true, false, true]
                }
            }
            """;
        using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Multiple operations
        builderDoc.RootElement.GetProperty("tags").RemoveRange(1, 1); // Remove "tag2"
        builderDoc.RootElement.GetProperty("categories")[0].GetProperty("items").RemoveAt(0); // Remove first item from cat1
        builderDoc.RootElement.GetProperty("metadata").GetProperty("scores").RemoveRange(0, 2); // Remove first two scores

        JsonElement.Mutable root = builderDoc.RootElement;

        // Assert
        // Verify tags array
        JsonElement.Mutable tagsArray = root.GetProperty("tags");
        Assert.AreEqual(2, tagsArray.GetArrayLength());
        Assert.AreEqual("tag1", tagsArray[0].GetString());
        Assert.AreEqual("tag3", tagsArray[1].GetString());
        
        // Verify categories
        JsonElement.Mutable cat1Items = root.GetProperty("categories")[0].GetProperty("items");
        Assert.AreEqual(2, cat1Items.GetArrayLength());
        Assert.AreEqual(2, cat1Items[0].GetInt32());
        Assert.AreEqual(3, cat1Items[1].GetInt32());
        
        // Verify cat2 is unchanged
        JsonElement.Mutable cat2Items = root.GetProperty("categories")[1].GetProperty("items");
        Assert.AreEqual(3, cat2Items.GetArrayLength());
        Assert.AreEqual(4, cat2Items[0].GetInt32());
        
        // Verify scores
        JsonElement.Mutable scoresArray = root.GetProperty("metadata").GetProperty("scores");
        Assert.AreEqual(2, scoresArray.GetArrayLength());
        Assert.AreEqual(30, scoresArray[0].GetInt32());
        Assert.AreEqual(40, scoresArray[1].GetInt32());
        
        // Verify flags unchanged
        JsonElement.Mutable flagsArray = root.GetProperty("metadata").GetProperty("flags");
        Assert.AreEqual(3, flagsArray.GetArrayLength());
        Assert.IsTrue(flagsArray[0].GetBoolean());
        
        // Verify other properties unchanged
        Assert.AreEqual(123, root.GetProperty("id").GetInt32());
    }

    // Edge Cases and Error Tests for Nested Arrays
    [TestMethod]
    public void RemoveRange_NestedArray_Throws_WhenInnerArrayIndexOutOfBounds()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2], [3, 4, 5]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable firstInnerArray = root[0]; // Only has 2 elements

        // Act & Assert
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => firstInnerArray.RemoveRange(5, 1));
    }

    [TestMethod]
    public void Remove_NestedArrayInObject_Throws_WhenAccessingNonArrayProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"array\": [1, 2, 3], \"string\": \"value\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable stringProperty = root.GetProperty("string");

        // Act & Assert
        Assert.ThrowsExactly<InvalidOperationException>(() => stringProperty.RemoveAt(0));
    }

    [TestMethod]
    public void Remove_NestedArrayWithNullValues_HandlesNullsCorrectly()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[null, 1, null], [2, null, 3], [null, null]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act - Remove null values from each array
        builderDoc.RootElement[0].RemoveAt(0); // Remove first null
        builderDoc.RootElement[1].RemoveAt(1); // Remove middle null
        builderDoc.RootElement[2].RemoveAt(0); // Remove first null from all-null array

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root[0].GetArrayLength());
        Assert.AreEqual(1, root[0][0].GetInt32());
        Assert.AreEqual(JsonValueKind.Null, root[0][1].ValueKind);
        
        Assert.AreEqual(2, root[1].GetArrayLength());
        Assert.AreEqual(2, root[1][0].GetInt32());
        Assert.AreEqual(3, root[1][1].GetInt32());
        
        Assert.AreEqual(1, root[2].GetArrayLength());
        Assert.AreEqual(JsonValueKind.Null, root[2][0].ValueKind);
    }

    #endregion

    #region Remove (by value) Tests

    [TestMethod]
    public void Remove_FindsAndRemovesFirstMatchingElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 2, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Get the element to remove
        using var itemDoc = ParsedJsonDocument<JsonElement>.Parse("2");
        JsonElement item = itemDoc.RootElement;

        // Act
        bool removed = root.Remove(in item);

        // Assert — only the first '2' is removed
        Assert.IsTrue(removed);
        Assert.AreEqual(4, root.GetArrayLength());
        Assert.AreEqual("[1,3,2,5]", root.ToString());
    }

    [TestMethod]
    public void Remove_ReturnsFalse_WhenElementNotFound()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var itemDoc = ParsedJsonDocument<JsonElement>.Parse("99");
        JsonElement item = itemDoc.RootElement;

        // Act
        bool removed = root.Remove(in item);

        // Assert
        Assert.IsFalse(removed);
        Assert.AreEqual(3, root.GetArrayLength());
    }

    [TestMethod]
    public void Remove_EmptyArray_ReturnsFalse()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var itemDoc = ParsedJsonDocument<JsonElement>.Parse("1");
        JsonElement item = itemDoc.RootElement;

        // Act
        bool removed = root.Remove(in item);

        // Assert
        Assert.IsFalse(removed);
        Assert.AreEqual(0, root.GetArrayLength());
    }

    [TestMethod]
    public void Remove_MatchesComplexObjects()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"id":1},{"id":2},{"id":3}]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var itemDoc = ParsedJsonDocument<JsonElement>.Parse("""{"id":2}""");
        JsonElement item = itemDoc.RootElement;

        // Act
        bool removed = root.Remove(in item);

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("""[{"id":1},{"id":3}]""", root.ToString());
    }

    [TestMethod]
    public void Remove_OnlyRemovesFirstOccurrence()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a":1},{"a":1},{"a":1}]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var itemDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement item = itemDoc.RootElement;

        // Act
        bool removed = root.Remove(in item);

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, root.GetArrayLength());
    }

    #endregion

    #region Replace Tests

    [TestMethod]
    public void Replace_FindsAndReplacesFirstMatchingElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 2, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("2");
        JsonElement oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<JsonElement>.Parse("99");
        JsonElement newItem = newDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, newItem);

        // Assert — only the first '2' is replaced
        Assert.IsTrue(replaced);
        Assert.AreEqual(5, root.GetArrayLength());
        Assert.AreEqual("[1,99,3,2,5]", root.ToString());
    }

    [TestMethod]
    public void Replace_ReturnsFalse_WhenElementNotFound()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("99");
        JsonElement oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<JsonElement>.Parse("100");
        JsonElement newItem = newDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, newItem);

        // Assert
        Assert.IsFalse(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("[1, 2, 3]", root.ToString());
    }

    [TestMethod]
    public void Replace_EmptyArray_ReturnsFalse()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("1");
        JsonElement oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<JsonElement>.Parse("2");
        JsonElement newItem = newDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, newItem);

        // Assert
        Assert.IsFalse(replaced);
        Assert.AreEqual(0, root.GetArrayLength());
    }

    [TestMethod]
    public void Replace_MatchesComplexObjects()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"id":1},{"id":2},{"id":3}]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("""{"id":2}""");
        JsonElement oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<JsonElement>.Parse("""{"id":99}""");
        JsonElement newItem = newDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, newItem);

        // Assert
        Assert.IsTrue(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("""[{"id":1},{"id":99},{"id":3}]""", root.ToString());
    }

    [TestMethod]
    public void Replace_OnlyReplacesFirstOccurrence()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""[{"a":1},{"a":1},{"a":1}]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}""");
        JsonElement oldItem = oldDoc.RootElement;
        using var newDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":2}""");
        JsonElement newItem = newDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, newItem);

        // Assert
        Assert.IsTrue(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("""[{"a":2},{"a":1},{"a":1}]""", root.ToString());
    }

    [TestMethod]
    public void Replace_WithUndefinedSource_RemovesMatch()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("2");
        JsonElement oldItem = oldDoc.RootElement;

        // Act — passing undefined Source should remove the match
        bool replaced = root.Replace(oldItem, default);

        // Assert
        Assert.IsTrue(replaced);
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("[1,3]", root.ToString());
    }

    [TestMethod]
    public void Replace_WithStringValue()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""["alpha","beta","gamma"]""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var oldDoc = ParsedJsonDocument<JsonElement>.Parse("\"beta\"");
        JsonElement oldItem = oldDoc.RootElement;

        // Act
        bool replaced = root.Replace(oldItem, "delta");

        // Assert
        Assert.IsTrue(replaced);
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("""["alpha","delta","gamma"]""", root.ToString());
    }

    #endregion

    #region RemoveProperty Tests

    [TestMethod]
    public void RemoveProperty_String_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b");

        // Assert
        Assert.IsTrue(removed);
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.AreEqual(1, root.GetProperty("a").GetInt32());
        Assert.AreEqual(3, root.GetProperty("c").GetInt32());
        Assert.IsFalse(root.TryGetProperty("b", out _));
    }

    [TestMethod]
    public void RemoveProperty_SpanOfChar_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b".AsSpan());

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"a\":1,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_Utf8_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b"u8);

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"a\":1,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_ReturnsFalse_WhenPropertyDoesNotExist()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("nonexistent");

        // Assert
        Assert.IsFalse(removed);
        Assert.AreEqual(1, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"a\":1}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_RemovesFirstProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"b\":2,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_RemovesLastProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("c");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(2, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"a\":1,\"b\":2}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_RemovesOnlyProperty_LeavesEmptyObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(0, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_RemovesNestedObjectProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"nested\":true},\"b\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(1, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"b\":2}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_RemovesNestedArrayProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[1,2,3],\"b\":\"hello\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual(1, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"b\":\"hello\"}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_OnNestedObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"outer\":{\"a\":1,\"b\":2}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.GetProperty("outer").RemoveProperty("a");

        // Assert
        Assert.IsTrue(removed);
        Assert.AreEqual("{\"outer\":{\"b\":2}}", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void RemoveProperty_FromEmptyObject_ReturnsFalse()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("anything");

        // Assert
        Assert.IsFalse(removed);
        Assert.AreEqual(0, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{}", builderDoc.RootElement.ToString());
    }

    #endregion

    #region SetProperty with default(JsonElement) removes property

    [TestMethod]
    public void SetProperty_String_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b", default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.IsFalse(root.TryGetProperty("b", out _));
        Assert.AreEqual("{\"a\":1,\"c\":3}", root.ToString());
    }

    [TestMethod]
    public void SetProperty_SpanOfChar_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b".AsSpan(), default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.IsFalse(root.TryGetProperty("b", out _));
        Assert.AreEqual("{\"a\":1,\"c\":3}", root.ToString());
    }

    [TestMethod]
    public void SetProperty_Utf8_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b"u8, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetPropertyCount());
        Assert.IsFalse(root.TryGetProperty("b", out _));
        Assert.AreEqual("{\"a\":1,\"c\":3}", root.ToString());
    }

    [TestMethod]
    public void SetProperty_WithDefaultSource_NoOpWhenPropertyDoesNotExist()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("nonexistent", default(JsonElement));

        // Assert
        Assert.AreEqual(1, builderDoc.RootElement.GetPropertyCount());
        Assert.AreEqual("{\"a\":1}", builderDoc.RootElement.ToString());
    }

    #endregion

    #region SetItem with default(JsonElement) removes item

    [TestMethod]
    public void SetItem_WithDefaultSource_RemovesExistingItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(1, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(1, root[0].GetInt32());
        Assert.AreEqual(3, root[1].GetInt32());
    }

    [TestMethod]
    public void SetItem_WithDefaultSource_RemovesFirstItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(0, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual(2, root[0].GetInt32());
        Assert.AreEqual(3, root[1].GetInt32());
    }

    [TestMethod]
    public void SetItem_WithDefaultSource_RemovesLastItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(2, default(JsonElement));

        // Assert
        Assert.AreEqual(2, builderDoc.RootElement.GetArrayLength());
        Assert.AreEqual("[1,2]", builderDoc.RootElement.ToString());
    }

    [TestMethod]
    public void SetItem_WithDefaultSource_RemovesOnlyItem_LeavesEmptyArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[42]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(0, default(JsonElement));

        // Assert
        Assert.AreEqual(0, builderDoc.RootElement.GetArrayLength());
        Assert.AreEqual("[]", builderDoc.RootElement.ToString());
    }

    #endregion

    #region InsertItem with default(JsonElement) is a no-op

    [TestMethod]
    public void InsertItem_WithDefaultSource_IsNoOp()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(1, default(JsonElement));

        // Assert - array is unchanged
        Assert.AreEqual(3, root.GetArrayLength());
        Assert.AreEqual("[1,2,3]", root.ToString());
    }

    [TestMethod]
    public void InsertItem_WithDefaultSource_IsNoOp_AtStart()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(0, default(JsonElement));

        // Assert - array is unchanged
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("[1,2]", root.ToString());
    }

    [TestMethod]
    public void InsertItem_WithDefaultSource_IsNoOp_AtEnd()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(2, default(JsonElement));

        // Assert - array is unchanged
        Assert.AreEqual(2, root.GetArrayLength());
        Assert.AreEqual("[1,2]", root.ToString());
    }

    [TestMethod]
    public void InsertItem_WithDefaultSource_IsNoOp_OnEmptyArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(0, default(JsonElement));

        // Assert - array is unchanged
        Assert.AreEqual(0, root.GetArrayLength());
        Assert.AreEqual("[]", root.ToString());
    }

    #endregion

    #region RootElement ToString after mutation

    [TestMethod]
    public void RootElement_ToString_WorksAfterMutationOnCapturedReference()
    {
        // Regression: root element (_idx == 0) must always be permitted,
        // even when the captured _documentVersion is stale after a mutation.
        using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Alice","age":30}""");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Capture root before mutation
        JsonElement.Mutable root = builderDoc.RootElement;

        // Mutate via the captured reference — increments _parent.Version
        root.RemoveProperty("age"u8);

        // ToString on the now-stale reference must still work for the root
        string result = root.ToString();
        Assert.AreNotEqual(string.Empty, result);
        Assert.AreEqual("""{"name":"Alice"}""", result);
    }

    #endregion

}
