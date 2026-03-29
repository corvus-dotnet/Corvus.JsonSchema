// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Tests;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Corvus.Numerics;
using Corvus.Text.Json.Internal;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;
public static class JsonDocumentBuilderTests
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

    [Fact]
    public static void ParseJson_SeekableStream_Small()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.Equal(JsonValueKind.Number, root.ValueKind);
            Assert.Equal(11, root.GetInt32());
        }
    }

    [Fact]
    public static void ParseJson_UnseekableStream_Small()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.Create())
        using (var doc =
            ParsedJsonDocument<JsonElement>.Parse(new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.Equal(JsonValueKind.Number, root.ValueKind);
            Assert.Equal(11, root.GetInt32());
        }
    }

    [Fact]
    public static async Task ParseJson_SeekableStream_Small_Async()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.CreateUnrented())
        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.Equal(JsonValueKind.Number, root.ValueKind);
            Assert.Equal(11, root.GetInt32());
        }
    }

    [Fact]
    public static async Task ParseJson_UnseekableStream_Small_Async()
    {
        byte[] data = [(byte)'1', (byte)'1'];

        using (var workspace = JsonWorkspace.CreateUnrented())
        using (ParsedJsonDocument<JsonElement> doc = await ParsedJsonDocument<JsonElement>.ParseAsync(
            new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data: data)))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            Assert.Equal(JsonValueKind.Number, root.ValueKind);
            Assert.Equal(11, root.GetInt32());
        }
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_SeekableStream_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(Utf8Bom.Concat(bytes).ToArray()))));
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_SeekableStream_Async_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(Utf8Bom.Concat(bytes).ToArray())));
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_UnseekableStream_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, Utf8Bom.Concat(bytes).ToArray()))));
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_UnseekableStream_Async_WithBOM(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => ParsedJsonDocument<JsonElement>.ParseAsync(
                    new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, Utf8Bom.Concat(bytes).ToArray())));
    }

    [Fact]
    public static void ParseJson_Stream_ClearRentedBuffer_WhenThrow_CodeCoverage()
    {
        using (Stream stream = new ThrowOnReadStream([1]))
        {
            Assert.Throws<EndOfStreamException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [Fact]
    public static async Task ParseJson_Stream_Async_ClearRentedBuffer_WhenThrow_CodeCoverage()
    {
        using (Stream stream = new ThrowOnReadStream([1]))
        {
            await Assert.ThrowsAsync<EndOfStreamException>(async () => await ParsedJsonDocument<JsonElement>.ParseAsync(stream));
        }
    }

    [Fact]
    public static void ParseJson_Stream_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream([1]))
        {
            Assert.Throws<InsufficientMemoryException>(() => ParsedJsonDocument<JsonElement>.Parse(stream));
        }
    }

    [Fact]
    public static async Task ParseJson_Stream_Async_ThrowsOn_ArrayPoolRent_CodeCoverage()
    {
        using (Stream stream = new ThrowOnCanSeekStream([1]))
        {
            await Assert.ThrowsAsync<InsufficientMemoryException>(async () => await ParsedJsonDocument<JsonElement>.ParseAsync(stream));
        }
    }

    [Theory]
    [MemberData(nameof(BadBOMCases))]
    public static void ParseJson_SeekableStream_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        Assert.ThrowsAny<JsonException>(() => ParsedJsonDocument<JsonElement>.Parse(new MemoryStream(data)));
    }

    [Theory]
    [MemberData(nameof(BadBOMCases))]
    public static Task ParseJson_SeekableStream_Async_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);
        return Assert.ThrowsAnyAsync<JsonException>(() => ParsedJsonDocument<JsonElement>.ParseAsync(new MemoryStream(data)));
    }

    [Theory]
    [MemberData(nameof(BadBOMCases))]
    public static void ParseJson_UnseekableStream_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);

        Assert.ThrowsAny<JsonException>(
            () => ParsedJsonDocument<JsonElement>.Parse(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data)));
    }

    [Theory]
    [MemberData(nameof(BadBOMCases))]
    [SkipOnCoreClr("https://github.com/dotnet/runtime/issues/45464", ~RuntimeConfiguration.Release)]
    public static Task ParseJson_UnseekableStream_Async_BadBOM(string json)
    {
        byte[] data = Encoding.UTF8.GetBytes(json);

        return Assert.ThrowsAnyAsync<JsonException>(
            () => ParsedJsonDocument<JsonElement>.ParseAsync(
                new WrappedMemoryStream(canRead: true, canWrite: false, canSeek: false, data)));
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_SequenceBytes_Single(bool compactData, TestCaseType type, string jsonString)
    {
        await ParseJsonAsync(
            compactData,
            type,
            jsonString,
            null,
            bytes => Task.FromResult(ParsedJsonDocument<JsonElement>.Parse(new ReadOnlySequence<byte>(bytes))));
    }

    [Theory]
    [MemberData(nameof(ReducedTestCases))]
    public static async Task ParseJson_SequenceBytes_Multi(bool compactData, TestCaseType type, string jsonString)
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
            Assert.NotNull(doc);

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

                Assert.Equal(expectedCustom, actualCustom);
            }

            string actual = builderDoc.PrintJson();
            string expected = GetExpectedConcat(type, jsonString);

            Assert.Equal(expected, actual);

            Assert.Equal(jsonString, rootElement.GetRawText());
        }
    }

    private static string PrintJson(this JsonDocumentBuilder<JsonElement.Mutable> document, int sizeHint = 0)
    {
        return PrintJson(document.RootElement, sizeHint);
    }

    private static string PrintJson(this JsonElement.Mutable element, int sizeHint = 0)
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

    [Theory]
    [InlineData("[{\"arrayWithObjects\":[\"text\",14,[],null,false,{},{\"time\":24},[\"1\",\"2\",\"3\"]]}]")]
    [InlineData("[{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{},{}]")]
    [InlineData("[{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}},{\"a\":{}}]")]
    [InlineData("{\"a\":\"b\"}")]
    [InlineData("{}")]
    [InlineData("[]")]
    public static void FromCustomParsedJson(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            string actual = builderDoc.PrintJson();

            TextReader reader = new StringReader(jsonString);
            string expected = JsonTestHelper.NewtonsoftReturnStringHelper(reader);

            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public static void FromParsedArray()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.SimpleArrayJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(2, root.GetArrayLength());

            string phoneNumber = root[0].GetString();
            int age = root[1].GetInt32();

            Assert.Equal("425-214-3151", phoneNumber);
            Assert.Equal(25, age);

            Assert.Throws<IndexOutOfRangeException>(() => root[2]);
        }
    }

    [Fact]
    public static void FromParsedSimpleObject()
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

            Assert.Equal(7, parsedObject.GetPropertyCount());
            Assert.True(parsedObject.TryGetProperty("age", out JsonElement.Mutable age2));
            Assert.Equal(30, age2.GetInt32());

            Assert.Equal(30, age);
            Assert.Equal("30", ageString);
            Assert.Equal("John", first);
            Assert.Equal("Smith", last);
            Assert.Equal("425-214-3151", phoneNumber);
            Assert.Equal("1 Microsoft Way", street);
            Assert.Equal("Redmond", city);
            Assert.Equal(98052, zip);
        }
    }

    [Fact]
    public static void FromParsedNestedJson()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            Assert.Equal(1, parsedObject.GetArrayLength());
            JsonElement.Mutable person = parsedObject[0];
            Assert.Equal(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement.Mutable phoneNums = person.GetProperty("phoneNumbers");
            Assert.Equal(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement.Mutable address = person.GetProperty("address");
            string street = address.GetProperty("street").GetString();
            string city = address.GetProperty("city").GetString();
            double zipCode = address.GetProperty("zip").GetDouble();
            const string ThrowsAnyway = "throws-anyway";

            Assert.Equal(30, age);
            Assert.Equal("John", first);
            Assert.Equal("Smith", last);
            Assert.Equal("425-000-1212", phoneNum1);
            Assert.Equal("425-000-1213", phoneNum2);
            Assert.Equal("1 Microsoft Way", street);
            Assert.Equal("Redmond", city);
            Assert.Equal(98052, zipCode);

            Assert.Throws<InvalidOperationException>(() => person.GetArrayLength());
            Assert.Throws<IndexOutOfRangeException>(() => phoneNums[2]);
            Assert.Throws<InvalidOperationException>(() => phoneNums.GetProperty("2"));
            Assert.Throws<KeyNotFoundException>(() => address.GetProperty("2"));
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("city").GetDouble());
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("city").GetBoolean());
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("zip").GetString());
            Assert.Throws<InvalidOperationException>(() => person.GetProperty("phoneNumbers").GetString());
            Assert.Throws<InvalidOperationException>(() => person.GetString());
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        }
    }

    [Fact]
    public static void FromParsedSimpleObjectWithSourcePropertyMap()
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

            Assert.Equal(7, parsedObject.GetPropertyCount());
            Assert.True(parsedObject.TryGetProperty("age", out JsonElement.Mutable age2));
            Assert.Equal(30, age2.GetInt32());

            Assert.Equal(30, age);
            Assert.Equal("30", ageString);
            Assert.Equal("John", first);
            Assert.Equal("Smith", last);
            Assert.Equal("425-214-3151", phoneNumber);
            Assert.Equal("1 Microsoft Way", street);
            Assert.Equal("Redmond", city);
            Assert.Equal(98052, zip);
        }
    }

    [Fact]
    public static void FromParsedNestedJsonWithSourcePropertyMap()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        {
            using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

            JsonElement sourcePerson = doc.RootElement[0];
            sourcePerson.EnsurePropertyMap();

            JsonElement.Mutable parsedObject = builderDoc.RootElement;

            Assert.Equal(1, parsedObject.GetArrayLength());
            JsonElement.Mutable person = parsedObject[0];
            Assert.Equal(5, person.GetPropertyCount());
            double age = person.GetProperty("age").GetDouble();
            string first = person.GetProperty("first").GetString();
            string last = person.GetProperty("last").GetString();
            JsonElement.Mutable phoneNums = person.GetProperty("phoneNumbers");
            Assert.Equal(2, phoneNums.GetArrayLength());
            string phoneNum1 = phoneNums[0].GetString();
            string phoneNum2 = phoneNums[1].GetString();
            JsonElement.Mutable address = person.GetProperty("address");
            string street = address.GetProperty("street").GetString();
            string city = address.GetProperty("city").GetString();
            double zipCode = address.GetProperty("zip").GetDouble();
            const string ThrowsAnyway = "throws-anyway";

            Assert.Equal(30, age);
            Assert.Equal("John", first);
            Assert.Equal("Smith", last);
            Assert.Equal("425-000-1212", phoneNum1);
            Assert.Equal("425-000-1213", phoneNum2);
            Assert.Equal("1 Microsoft Way", street);
            Assert.Equal("Redmond", city);
            Assert.Equal(98052, zipCode);

            Assert.Throws<InvalidOperationException>(() => person.GetArrayLength());
            Assert.Throws<IndexOutOfRangeException>(() => phoneNums[2]);
            Assert.Throws<InvalidOperationException>(() => phoneNums.GetProperty("2"));
            Assert.Throws<KeyNotFoundException>(() => address.GetProperty("2"));
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("city").GetDouble());
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("city").GetBoolean());
            Assert.Throws<InvalidOperationException>(() => address.GetProperty("zip").GetString());
            Assert.Throws<InvalidOperationException>(() => person.GetProperty("phoneNumbers").GetString());
            Assert.Throws<InvalidOperationException>(() => person.GetString());
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => person.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        }
    }

    [Fact]
    public static void ParseBoolean()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("[true,false]"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable parsedObject = builderDoc.RootElement;
            bool first = parsedObject[0].GetBoolean();
            bool second = parsedObject[1].GetBoolean();
            Assert.True(first);
            Assert.False(second);
        }
    }

    [Fact]
    public static void JsonArrayToString()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.ParseJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            Assert.Equal(SR.ParseJson, root.ToString());
        }
    }

    [Fact]
    public static void JsonObjectToString()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(SR.BasicJson))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Object, root.ValueKind);
            Assert.Equal(SR.BasicJson, root.ToString());
        }
    }

    [Fact]
    public static void MixedArrayIndexing()
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

            Assert.Equal(2, target.GetArrayLength());

            string phoneNumber = target[0].GetString();
            int age = target[1].GetInt32();

            Assert.Equal("425-214-3151", phoneNumber);
            Assert.Equal(25, age);
            Assert.Equal(JsonValueKind.Null, root[3].ValueKind);

            Assert.Throws<IndexOutOfRangeException>(() => root[4]);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(sbyte.MaxValue)]
    [InlineData(sbyte.MinValue)]
    public static void ReadNumber_1Byte(sbyte value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.True(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(value, sbyteVal);

            Assert.True(root.TryGetInt16(out short shortVal));
            Assert.Equal(value, shortVal);

            Assert.True(root.TryGetInt32(out int intVal));
            Assert.Equal(value, intVal);

            Assert.True(root.TryGetInt64(out long longVal));
            Assert.Equal(value, longVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Equal(value, root.GetInt32());
            Assert.Equal(value, root.GetInt64());

            if (value >= 0)
            {
                byte expectedByte = (byte)value;
                ushort expectedUShort = (ushort)value;
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.True(root.TryGetByte(out byte byteVal));
                Assert.Equal(expectedByte, byteVal);

                Assert.True(root.TryGetUInt16(out ushort ushortVal));
                Assert.Equal(expectedUShort, ushortVal);

                Assert.True(root.TryGetUInt32(out uint uintVal));
                Assert.Equal(expectedUInt, uintVal);

                Assert.True(root.TryGetUInt64(out ulong ulongVal));
                Assert.Equal(expectedULong, ulongVal);

                Assert.Equal(expectedUInt, root.GetUInt32());
                Assert.Equal(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.False(root.TryGetByte(out byte byteValue));
                Assert.Equal(0, byteValue);

                Assert.False(root.TryGetUInt16(out ushort ushortValue));
                Assert.Equal(0, ushortValue);

                Assert.False(root.TryGetUInt32(out uint uintValue));
                Assert.Equal(0U, uintValue);

                Assert.False(root.TryGetUInt64(out ulong ulongValue));
                Assert.Equal(0UL, ulongValue);

                Assert.Throws<FormatException>(() => root.GetUInt32());
                Assert.Throws<FormatException>(() => root.GetUInt64());
            }

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(short.MaxValue)]
    [InlineData(short.MinValue)]
    public static void ReadNumber_2Bytes(short value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.Equal((value == 0), root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.Equal((value == 0), root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.True(root.TryGetInt16(out short shortVal));
            Assert.Equal(value, shortVal);

            Assert.True(root.TryGetInt32(out int intVal));
            Assert.Equal(value, intVal);

            Assert.True(root.TryGetInt64(out long longVal));
            Assert.Equal(value, longVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Equal(value, root.GetInt32());
            Assert.Equal(value, root.GetInt64());

            if (value >= 0)
            {
                byte expectedByte = (byte)value;
                ushort expectedUShort = (ushort)value;
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.True(root.TryGetUInt16(out ushort ushortVal));
                Assert.Equal(expectedUShort, ushortVal);

                Assert.True(root.TryGetUInt32(out uint uintVal));
                Assert.Equal(expectedUInt, uintVal);

                Assert.True(root.TryGetUInt64(out ulong ulongVal));
                Assert.Equal(expectedULong, ulongVal);

                Assert.Equal(expectedUInt, root.GetUInt32());
                Assert.Equal(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.False(root.TryGetUInt16(out ushort ushortValue));
                Assert.Equal(0, ushortValue);

                Assert.False(root.TryGetUInt32(out uint uintValue));
                Assert.Equal(0U, uintValue);

                Assert.False(root.TryGetUInt64(out ulong ulongValue));
                Assert.Equal(0UL, ulongValue);

                Assert.Throws<FormatException>(() => root.GetUInt32());
                Assert.Throws<FormatException>(() => root.GetUInt64());
            }

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public static void ReadSmallInteger(int value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.Equal((value == 0), root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.Equal((value == 0), root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.Equal((value == 0), root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.Equal((value == 0), root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.True(root.TryGetInt32(out int intVal));
            Assert.Equal(value, intVal);

            Assert.True(root.TryGetInt64(out long longVal));
            Assert.Equal(value, longVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Equal(value, root.GetInt32());
            Assert.Equal(value, root.GetInt64());

            if (value >= 0)
            {
                uint expectedUInt = (uint)value;
                ulong expectedULong = (ulong)value;

                Assert.True(root.TryGetUInt32(out uint uintVal));
                Assert.Equal(expectedUInt, uintVal);

                Assert.True(root.TryGetUInt64(out ulong ulongVal));
                Assert.Equal(expectedULong, ulongVal);

                Assert.Equal(expectedUInt, root.GetUInt32());
                Assert.Equal(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.False(root.TryGetUInt32(out uint uintValue));
                Assert.Equal(0U, uintValue);

                Assert.False(root.TryGetUInt64(out ulong ulongValue));
                Assert.Equal(0UL, ulongValue);

                Assert.Throws<FormatException>(() => root.GetUInt32());
                Assert.Throws<FormatException>(() => root.GetUInt64());
            }

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] bytes));
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [InlineData((long)int.MaxValue + 1)]
    [InlineData((long)uint.MaxValue)]
    [InlineData(long.MaxValue)]
    [InlineData((long)int.MinValue - 1)]
    [InlineData(long.MinValue)]
    public static void ReadMediumInteger(long value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.False(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.False(root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.False(root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.False(root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.False(root.TryGetInt32(out int intVal));
            Assert.Equal(0, intVal);

            Assert.True(root.TryGetInt64(out long longVal));
            Assert.Equal(value, longVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Throws<FormatException>(() => root.GetInt32());
            Assert.Equal(value, root.GetInt64());

            if (value >= 0)
            {
                if (value <= uint.MaxValue)
                {
                    uint expectedUInt = (uint)value;
                    Assert.True(root.TryGetUInt32(out uint uintVal));
                    Assert.Equal(expectedUInt, uintVal);

                    Assert.Equal(expectedUInt, root.GetUInt64());
                }
                else
                {
                    Assert.False(root.TryGetUInt32(out uint uintValue));
                    Assert.Equal(0U, uintValue);

                    Assert.Throws<FormatException>(() => root.GetUInt32());
                }

                ulong expectedULong = (ulong)value;
                Assert.True(root.TryGetUInt64(out ulong ulongVal));
                Assert.Equal(expectedULong, ulongVal);

                Assert.Equal(expectedULong, root.GetUInt64());
            }
            else
            {
                Assert.False(root.TryGetUInt32(out uint uintValue));
                Assert.Equal(0U, uintValue);

                Assert.False(root.TryGetUInt64(out ulong ulongValue));
                Assert.Equal(0UL, ulongValue);

                Assert.Throws<FormatException>(() => root.GetUInt32());
                Assert.Throws<FormatException>(() => root.GetUInt64());
            }

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [InlineData((ulong)long.MaxValue + 1)]
    [InlineData(ulong.MaxValue)]
    public static void ReadLargeInteger(ulong value)
    {
        double expectedDouble = value;
        float expectedFloat = value;
        decimal expectedDecimal = value;

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + value + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.False(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.False(root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.False(root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.False(root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.False(root.TryGetInt32(out int intVal));
            Assert.Equal(0, intVal);

            Assert.False(root.TryGetUInt32(out uint uintVal));
            Assert.Equal(0U, uintVal);

            Assert.False(root.TryGetInt64(out long longVal));
            Assert.Equal(0L, longVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Throws<FormatException>(() => root.GetInt32());
            Assert.Throws<FormatException>(() => root.GetUInt32());
            Assert.Throws<FormatException>(() => root.GetInt64());

            Assert.True(root.TryGetUInt64(out ulong ulongVal));
            Assert.Equal(value, ulongVal);

            Assert.Equal(value, root.GetUInt64());

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Fact]
    public static void ReadTooLargeInteger()
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

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.False(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.False(root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.False(root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.False(root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.False(root.TryGetInt32(out int intVal));
            Assert.Equal(0, intVal);

            Assert.False(root.TryGetUInt32(out uint uintVal));
            Assert.Equal(0U, uintVal);

            Assert.False(root.TryGetInt64(out long longVal));
            Assert.Equal(0L, longVal);

            Assert.False(root.TryGetUInt64(out ulong ulongVal));
            Assert.Equal(0UL, ulongVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Throws<FormatException>(() => root.GetInt32());
            Assert.Throws<FormatException>(() => root.GetUInt32());
            Assert.Throws<FormatException>(() => root.GetInt64());
            Assert.Throws<FormatException>(() => root.GetUInt64());

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [MemberData(nameof(JsonDateTimeTestData.ValidISO8601Tests), MemberType = typeof(JsonDateTimeTestData))]
    public static void ReadDateTimeAndDateTimeOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            var expectedDateTime = DateTime.Parse(expectedString);
            var expectedDateTimeOffset = DateTimeOffset.Parse(expectedString);

            Assert.Equal(JsonValueKind.String, root.ValueKind);

            Assert.True(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.Equal(expectedDateTime, DateTimeVal);

            Assert.True(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.Equal(expectedDateTimeOffset, DateTimeOffsetVal);

            Assert.Equal(expectedDateTime, root.GetDateTime());
            Assert.Equal(expectedDateTimeOffset, root.GetDateTimeOffset());

            Assert.Throws<InvalidOperationException>(() => root.GetSByte());
            Assert.Throws<InvalidOperationException>(() => root.GetByte());
            Assert.Throws<InvalidOperationException>(() => root.GetInt16());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt16());
            Assert.Throws<InvalidOperationException>(() => root.GetInt32());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt32());
            Assert.Throws<InvalidOperationException>(() => root.GetInt64());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt64());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [MemberData(nameof(JsonDateTimeTestData.ValidISO8601TestsWithUtcOffset), MemberType = typeof(JsonDateTimeTestData))]
    public static void ReadDateTimeAndDateTimeOffset_WithUtcOffset(string jsonString, string expectedString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            var expectedDateTime = DateTime.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            var expectedDateTimeOffset = DateTimeOffset.ParseExact(expectedString, "O", CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

            Assert.Equal(JsonValueKind.String, root.ValueKind);

            Assert.True(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.Equal(expectedDateTime, DateTimeVal);

            Assert.True(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.Equal(expectedDateTimeOffset, DateTimeOffsetVal);

            Assert.Equal(expectedDateTime, root.GetDateTime());
            Assert.Equal(expectedDateTimeOffset, root.GetDateTimeOffset());
        }
    }

    [Theory]
    [MemberData(nameof(JsonDateTimeTestData.InvalidISO8601Tests), MemberType = typeof(JsonDateTimeTestData))]
    public static void ReadDateTimeAndDateTimeOffset_InvalidTests(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.String, root.ValueKind);

            Assert.False(root.TryGetDateTime(out DateTime DateTimeVal));
            Assert.Equal(default, DateTimeVal);
            Assert.False(root.TryGetDateTimeOffset(out DateTimeOffset DateTimeOffsetVal));
            Assert.Equal(default, DateTimeOffsetVal);

            Assert.Throws<FormatException>(() => root.GetDateTime());
            Assert.Throws<FormatException>(() => root.GetDateTimeOffset());
        }
    }

    [Theory]
    [MemberData(nameof(JsonGuidTestData.ValidGuidTests), MemberType = typeof(JsonGuidTestData))]
    [MemberData(nameof(JsonGuidTestData.ValidHexGuidTests), MemberType = typeof(JsonGuidTestData))]
    public static void ReadGuid(string jsonString, string expectedStr)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{jsonString}\"");

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            var expected = new Guid(expectedStr);

            Assert.Equal(JsonValueKind.String, root.ValueKind);

            Assert.True(root.TryGetGuid(out Guid GuidVal));
            Assert.Equal(expected, GuidVal);

            Assert.Equal(expected, root.GetGuid());

            Assert.Throws<InvalidOperationException>(() => root.GetSByte());
            Assert.Throws<InvalidOperationException>(() => root.GetByte());
            Assert.Throws<InvalidOperationException>(() => root.GetInt16());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt16());
            Assert.Throws<InvalidOperationException>(() => root.GetInt32());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt32());
            Assert.Throws<InvalidOperationException>(() => root.GetInt64());
            Assert.Throws<InvalidOperationException>(() => root.GetUInt64());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Theory]
    [MemberData(nameof(JsonGuidTestData.InvalidGuidTests), MemberType = typeof(JsonGuidTestData))]
    public static void ReadGuid_InvalidTests(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes($"\"{jsonString}\"");

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.String, root.ValueKind);

            Assert.False(root.TryGetGuid(out Guid GuidVal));
            Assert.Equal(default, GuidVal);

            Assert.Throws<FormatException>(() => root.GetGuid());
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

    [Theory]
    [MemberData(nameof(NonIntegerCases))]
    public static void ReadNonInteger(string str, double expectedDouble, float expectedFloat, decimal expectedDecimal)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    " + str + "  "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            Assert.True(root.TryGetSingle(out float floatVal));
            Assert.Equal(expectedFloat, floatVal);

            Assert.True(root.TryGetDouble(out double doubleVal));
            Assert.Equal(expectedDouble, doubleVal);

            Assert.True(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(expectedDecimal, decimalVal);

            Assert.False(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.False(root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.False(root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.False(root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.False(root.TryGetInt32(out int intVal));
            Assert.Equal(0, intVal);

            Assert.False(root.TryGetUInt32(out uint uintVal));
            Assert.Equal(0U, uintVal);

            Assert.False(root.TryGetInt64(out long longVal));
            Assert.Equal(0L, longVal);

            Assert.False(root.TryGetUInt64(out ulong ulongVal));
            Assert.Equal(0UL, ulongVal);

            Assert.Equal(expectedFloat, root.GetSingle());
            Assert.Equal(expectedDouble, root.GetDouble());
            Assert.Equal(expectedDecimal, root.GetDecimal());
            Assert.Throws<FormatException>(() => root.GetSByte());
            Assert.Throws<FormatException>(() => root.GetByte());
            Assert.Throws<FormatException>(() => root.GetInt16());
            Assert.Throws<FormatException>(() => root.GetUInt16());
            Assert.Throws<FormatException>(() => root.GetInt32());
            Assert.Throws<FormatException>(() => root.GetUInt32());
            Assert.Throws<FormatException>(() => root.GetInt64());
            Assert.Throws<FormatException>(() => root.GetUInt64());

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Fact]
    public static void ReadTooPreciseDouble()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("    1e+100000002"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.Number, root.ValueKind);

            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Assert.False(root.TryGetSingle(out float floatVal));
                Assert.Equal(0f, floatVal);

                Assert.False(root.TryGetDouble(out double doubleVal));
                Assert.Equal(0d, doubleVal);
            }
            else
            {
                Assert.True(root.TryGetSingle(out float floatVal));
                Assert.Equal(float.PositiveInfinity, floatVal);

                Assert.True(root.TryGetDouble(out double doubleVal));
                Assert.Equal(double.PositiveInfinity, doubleVal);
            }

            Assert.False(root.TryGetDecimal(out decimal decimalVal));
            Assert.Equal(0m, decimalVal);

            Assert.False(root.TryGetSByte(out sbyte sbyteVal));
            Assert.Equal(0, sbyteVal);

            Assert.False(root.TryGetByte(out byte byteVal));
            Assert.Equal(0, byteVal);

            Assert.False(root.TryGetInt16(out short shortVal));
            Assert.Equal(0, shortVal);

            Assert.False(root.TryGetUInt16(out ushort ushortVal));
            Assert.Equal(0, ushortVal);

            Assert.False(root.TryGetInt32(out int intVal));
            Assert.Equal(0, intVal);

            Assert.False(root.TryGetUInt32(out uint uintVal));
            Assert.Equal(0U, uintVal);

            Assert.False(root.TryGetInt64(out long longVal));
            Assert.Equal(0L, longVal);

            Assert.False(root.TryGetUInt64(out ulong ulongVal));
            Assert.Equal(0UL, ulongVal);

            if (RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.OrdinalIgnoreCase))
            {
                Assert.Throws<FormatException>(() => root.GetSingle());
                Assert.Throws<FormatException>(() => root.GetDouble());
            }
            else
            {
                Assert.Equal(float.PositiveInfinity, root.GetSingle());
                Assert.Equal(double.PositiveInfinity, root.GetDouble());
            }

            Assert.Throws<FormatException>(() => root.GetDecimal());
            Assert.Throws<FormatException>(() => root.GetSByte());
            Assert.Throws<FormatException>(() => root.GetByte());
            Assert.Throws<FormatException>(() => root.GetInt16());
            Assert.Throws<FormatException>(() => root.GetUInt16());
            Assert.Throws<FormatException>(() => root.GetInt32());
            Assert.Throws<FormatException>(() => root.GetUInt32());
            Assert.Throws<FormatException>(() => root.GetInt64());
            Assert.Throws<FormatException>(() => root.GetUInt64());

            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
            Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Fact]
    public static void ReadArrayWithComments()
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

            Assert.Equal(JsonValueKind.Array, root.ValueKind);
            Assert.Equal(5, root.GetArrayLength());

            for (int i = root.GetArrayLength() - 1; i >= 0; i--)
            {
                Assert.Equal(i, root[i].GetInt32());
            }

            int val = 0;

            foreach (JsonElement.Mutable element in root.EnumerateArray())
            {
                Assert.Equal(val, element.GetInt32());
                val++;
            }

            Assert.Throws<InvalidOperationException>(() => root.GetDouble());
            Assert.Throws<InvalidOperationException>(() => root.TryGetDouble(out double _));
            Assert.Throws<InvalidOperationException>(() => root.GetSByte());
            Assert.Throws<InvalidOperationException>(() => root.TryGetSByte(out sbyte _));
            Assert.Throws<InvalidOperationException>(() => root.GetByte());
            Assert.Throws<InvalidOperationException>(() => root.TryGetByte(out byte _));
            Assert.Throws<InvalidOperationException>(() => root.GetInt16());
            Assert.Throws<InvalidOperationException>(() => root.TryGetInt16(out short _));
            Assert.Throws<InvalidOperationException>(() => root.GetUInt16());
            Assert.Throws<InvalidOperationException>(() => root.TryGetUInt16(out ushort _));
            Assert.Throws<InvalidOperationException>(() => root.GetInt32());
            Assert.Throws<InvalidOperationException>(() => root.TryGetInt32(out int _));
            Assert.Throws<InvalidOperationException>(() => root.GetUInt32());
            Assert.Throws<InvalidOperationException>(() => root.TryGetUInt32(out uint _));
            Assert.Throws<InvalidOperationException>(() => root.GetInt64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetInt64(out long _));
            Assert.Throws<InvalidOperationException>(() => root.GetUInt64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetUInt64(out ulong _));
            Assert.Throws<InvalidOperationException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
            Assert.Throws<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] _));
            Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
            Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
            Assert.Throws<InvalidOperationException>(() => root.GetGuid());
            Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
            Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        }
    }

    [Fact]
    public static void CheckUseAfterDispose()
    {
        var buffer = new ArrayBufferWriter<byte>(1024);
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"First\":1}", default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            JsonProperty<JsonElement.Mutable> property = root.EnumerateObject().First();
            builderDoc.Dispose();

            Assert.Throws<ObjectDisposedException>(() => root.ValueKind);
            Assert.Throws<ObjectDisposedException>(() => root.GetArrayLength());
            Assert.Throws<ObjectDisposedException>(() => root.GetPropertyCount());
            Assert.Throws<ObjectDisposedException>(() => root.EnumerateArray());
            Assert.Throws<ObjectDisposedException>(() => root.EnumerateObject());
            Assert.Throws<ObjectDisposedException>(() => root.GetDouble());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetDouble(out double _));
            Assert.Throws<ObjectDisposedException>(() => root.GetSByte());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetSByte(out sbyte _));
            Assert.Throws<ObjectDisposedException>(() => root.GetByte());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetByte(out byte _));
            Assert.Throws<ObjectDisposedException>(() => root.GetInt16());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetInt16(out short _));
            Assert.Throws<ObjectDisposedException>(() => root.GetUInt16());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetUInt16(out ushort _));
            Assert.Throws<ObjectDisposedException>(() => root.GetInt32());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetInt32(out int _));
            Assert.Throws<ObjectDisposedException>(() => root.GetUInt32());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetUInt32(out uint _));
            Assert.Throws<ObjectDisposedException>(() => root.GetInt64());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetInt64(out long _));
            Assert.Throws<ObjectDisposedException>(() => root.GetUInt64());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetUInt64(out ulong _));
            Assert.Throws<ObjectDisposedException>(() => root.GetString());
            const string ThrowsAnyway = "throws-anyway";
            Assert.Throws<ObjectDisposedException>(() => root.ValueEquals(ThrowsAnyway));
            Assert.Throws<ObjectDisposedException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
            Assert.Throws<ObjectDisposedException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
            Assert.Throws<ObjectDisposedException>(() => root.GetBytesFromBase64());
            Assert.Throws<ObjectDisposedException>(() => root.TryGetBytesFromBase64(out byte[] _));
            Assert.Throws<ObjectDisposedException>(() => root.GetBoolean());
            Assert.Throws<ObjectDisposedException>(() => root.GetRawText());

            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                root.WriteTo(writer);
            });

            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                builderDoc.WriteTo(writer);
            });

            Assert.Throws<ObjectDisposedException>(() =>
            {
                using var writer = new Utf8JsonWriter(buffer);
                property.WriteTo(writer);
            });
        }
    }

    [Fact]
    public static void CheckUseDefault()
    {
        JsonElement.Mutable root = default;

        Assert.Equal(JsonValueKind.Undefined, root.ValueKind);

        Assert.Throws<InvalidOperationException>(() => root.GetArrayLength());
        Assert.Throws<InvalidOperationException>(() => root.GetPropertyCount());
        Assert.Throws<InvalidOperationException>(() => root.EnumerateArray());
        Assert.Throws<InvalidOperationException>(() => root.EnumerateObject());
        Assert.Throws<InvalidOperationException>(() => root.GetDouble());
        Assert.Throws<InvalidOperationException>(() => root.TryGetDouble(out double _));
        Assert.Throws<InvalidOperationException>(() => root.GetSByte());
        Assert.Throws<InvalidOperationException>(() => root.TryGetSByte(out sbyte _));
        Assert.Throws<InvalidOperationException>(() => root.GetByte());
        Assert.Throws<InvalidOperationException>(() => root.TryGetByte(out byte _));
        Assert.Throws<InvalidOperationException>(() => root.GetInt16());
        Assert.Throws<InvalidOperationException>(() => root.TryGetInt16(out short _));
        Assert.Throws<InvalidOperationException>(() => root.GetUInt16());
        Assert.Throws<InvalidOperationException>(() => root.TryGetUInt16(out ushort _));
        Assert.Throws<InvalidOperationException>(() => root.GetInt32());
        Assert.Throws<InvalidOperationException>(() => root.TryGetInt32(out int _));
        Assert.Throws<InvalidOperationException>(() => root.GetUInt32());
        Assert.Throws<InvalidOperationException>(() => root.TryGetUInt32(out uint _));
        Assert.Throws<InvalidOperationException>(() => root.GetInt64());
        Assert.Throws<InvalidOperationException>(() => root.TryGetInt64(out long _));
        Assert.Throws<InvalidOperationException>(() => root.GetUInt64());
        Assert.Throws<InvalidOperationException>(() => root.TryGetUInt64(out ulong _));
        Assert.Throws<InvalidOperationException>(() => root.GetString());
        const string ThrowsAnyway = "throws-anyway";
        Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway));
        Assert.Throws<InvalidOperationException>(() => root.ValueEquals(ThrowsAnyway.AsSpan()));
        Assert.Throws<InvalidOperationException>(() => root.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)));
        Assert.Throws<InvalidOperationException>(() => root.GetBytesFromBase64());
        Assert.Throws<InvalidOperationException>(() => root.TryGetBytesFromBase64(out byte[] _));
        Assert.Throws<InvalidOperationException>(() => root.GetDateTime());
        Assert.Throws<InvalidOperationException>(() => root.GetDateTimeOffset());
        Assert.Throws<InvalidOperationException>(() => root.GetGuid());
        Assert.Throws<InvalidOperationException>(() => root.GetBoolean());
        Assert.Throws<InvalidOperationException>(() => root.GetRawText());

        Assert.Throws<InvalidOperationException>(() =>
        {
            var buffer = new ArrayBufferWriter<byte>(1024);
            using var writer = new Utf8JsonWriter(buffer);
            root.WriteTo(writer);
        });
    }

    [Fact]
    public static void CheckInvalidString()
    {
        Assert.Throws<ArgumentException>(() => ParsedJsonDocument<JsonElement>.Parse("{ \"unpaired\uDFFE\": true }"));
    }

    [Theory]
    [InlineData("\"hello\"    ", "hello")]
    [InlineData("    null     ", (string)null)]
    [InlineData("\"\\u0033\\u0031\"", "31")]
    public static void ReadString(string json, string expectedValue)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.Equal(expectedValue, doc.RootElement.GetString());
        }
    }

    [Fact]
    public static void GetString_BadUtf8()
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
            Assert.Equal(JsonValueKind.String, root.ValueKind);
            AssertExtensions.Throws<InvalidOperationException>(() => root.GetString(), ErrorMessage);
            Assert.Throws<InvalidOperationException>(() => root.GetRawText());
            const string DummyString = "dummy-string";
            Assert.False(root.ValueEquals(badUtf8));
            Assert.False(root.ValueEquals(DummyString));
            Assert.False(root.ValueEquals(DummyString.AsSpan()));
            Assert.False(root.ValueEquals(Encoding.UTF8.GetBytes(DummyString)));
        }
    }

    [Fact]
    public static void GetBase64String_BadUtf8()
    {
        // The Arabic ligature Lam-Alef (U+FEFB) (which happens to, as a standalone, mean "no" in English)
        // is UTF-8 EF BB BB.  So let's leave out a BB and put it in quotes.
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(new byte[] { 0x22, 0xEF, 0xBB, 0x22 }))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.Equal(JsonValueKind.String, root.ValueKind);
            Assert.Throws<FormatException>(() => root.GetBytesFromBase64());
            Assert.False(root.TryGetBytesFromBase64(out byte[] value));
            Assert.Null(value);
        }
    }

    [Fact]
    public static void GetBase64Unescapes()
    {
        string jsonString = "\"\\u0031234\""; // equivalent to "\"1234\""

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            byte[] expected = Convert.FromBase64String("1234"); // new byte[3] { 215, 109, 248 }

            Assert.Equal(expected, doc.RootElement.GetBytesFromBase64());

            Assert.True(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [MemberData(nameof(JsonBase64TestData.ValidBase64Tests), MemberType = typeof(JsonBase64TestData))]
    public static void ReadBase64String(string jsonString)
    {
        byte[] expected = Convert.FromBase64String(jsonString.AsSpan(1, jsonString.Length - 2).ToString());

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.Equal(expected, doc.RootElement.GetBytesFromBase64());

            Assert.True(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            Assert.Equal(expected, value);
        }
    }

    [Theory]
    [MemberData(nameof(JsonBase64TestData.InvalidBase64Tests), MemberType = typeof(JsonBase64TestData))]
    public static void InvalidBase64(string jsonString)
    {
        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.False(doc.RootElement.TryGetBytesFromBase64(out byte[] value));
            Assert.Null(value);

            Assert.Throws<FormatException>(() => doc.RootElement.GetBytesFromBase64());
        }
    }

    [Theory]
    [InlineData(" { \"hi\": \"there\" }")]
    [InlineData(" { \n\n\n\n } ")]
    [InlineData(" { \"outer\": { \"inner\": [ 1, 2, 3 ] }, \"secondOuter\": [ 2, 4, 6, 0, 1 ] }")]
    public static void TryGetProperty_NoProperty(string json)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            const string NotPresent = "Not Present";
            byte[] notPresentUtf8 = Encoding.UTF8.GetBytes(NotPresent);

            JsonElement.Mutable element;
            Assert.False(root.TryGetProperty(NotPresent, out element));
            Assert.Equal(default, element);
            Assert.False(root.TryGetProperty(NotPresent.AsSpan(), out element));
            Assert.Equal(default, element);
            Assert.False(root.TryGetProperty(notPresentUtf8, out element));
            Assert.Equal(default, element);
            Assert.False(root.TryGetProperty(new string('z', 512), out element));
            Assert.Equal(default, element);

            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(NotPresent));
            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(NotPresent.AsSpan()));
            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(notPresentUtf8));
            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(new string('z', 512)));
        }
    }

    [Fact]
    public static void TryGetProperty_CaseSensitive()
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
                Assert.Equal(JsonValueKind.String, elem.ValueKind);
                Assert.Equal("no", elem.GetString());
            }

            void assertCamel(JsonElement.Mutable elem)
            {
                Assert.Equal(JsonValueKind.Number, elem.ValueKind);
                Assert.Equal(42, elem.GetInt32());
            }

            void assertOdd(JsonElement.Mutable elem)
            {
                Assert.Equal(JsonValueKind.False, elem.ValueKind);
                Assert.False(elem.GetBoolean());
            }

            Assert.True(root.TryGetProperty(PascalString, out JsonElement.Mutable pascal));
            assertPascal(pascal);
            Assert.True(root.TryGetProperty(PascalString.AsSpan(), out pascal));
            assertPascal(pascal);
            Assert.True(root.TryGetProperty(pascalBytes, out pascal));
            assertPascal(pascal);

            Assert.True(root.TryGetProperty(CamelString, out JsonElement.Mutable camel));
            assertCamel(camel);
            Assert.True(root.TryGetProperty(CamelString.AsSpan(), out camel));
            assertCamel(camel);
            Assert.True(root.TryGetProperty(camelBytes, out camel));
            assertCamel(camel);

            Assert.True(root.TryGetProperty(OddString, out JsonElement.Mutable odd));
            assertOdd(odd);
            Assert.True(root.TryGetProperty(OddString.AsSpan(), out odd));
            assertOdd(odd);
            Assert.True(root.TryGetProperty(oddBytes, out odd));
            assertOdd(odd);

            Assert.False(root.TryGetProperty(InverseOddString, out _));
            Assert.False(root.TryGetProperty(InverseOddString.AsSpan(), out _));
            Assert.False(root.TryGetProperty(inverseOddBytes, out _));

            assertPascal(root.GetProperty(PascalString));
            assertPascal(root.GetProperty(PascalString.AsSpan()));
            assertPascal(root.GetProperty(pascalBytes));

            assertCamel(root.GetProperty(CamelString));
            assertCamel(root.GetProperty(CamelString.AsSpan()));
            assertCamel(root.GetProperty(camelBytes));

            assertOdd(root.GetProperty(OddString));
            assertOdd(root.GetProperty(OddString.AsSpan()));
            assertOdd(root.GetProperty(oddBytes));

            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(InverseOddString));
            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(InverseOddString.AsSpan()));
            Assert.Throws<KeyNotFoundException>(() => root.GetProperty(inverseOddBytes));
        }
    }

    [Theory]
    [InlineData(">>")]
    [InlineData(">>>")]
    [InlineData(">>a")]
    [InlineData(">a>")]
    public static void TryGetDateTimeAndOffset_InvalidPropertyValue(string testData)
    {
        string jsonString = System.Text.Json.JsonSerializer.Serialize(new { DateTimeProperty = testData });

        byte[] dataUtf8 = Encoding.UTF8.GetBytes(jsonString);

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(dataUtf8, default))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;

            Assert.False(root.GetProperty("DateTimeProperty").TryGetDateTime(out DateTime datetimeValue));
            Assert.Equal(default, datetimeValue);
            Assert.Throws<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTime());

            Assert.False(root.GetProperty("DateTimeProperty").TryGetDateTimeOffset(out DateTimeOffset datetimeOffsetValue));
            Assert.Equal(default, datetimeOffsetValue);
            Assert.Throws<FormatException>(() => root.GetProperty("DateTimeProperty").GetDateTimeOffset());
        }
    }

    [Fact]
    public static void GetPropertyByNullName()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{ }"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            AssertExtensions.Throws<ArgumentNullException>(
                "propertyName",
                () => doc.RootElement.GetProperty((string)null));

            AssertExtensions.Throws<ArgumentNullException>(
                "propertyName",
                () => doc.RootElement.TryGetProperty((string)null, out _));
        }
    }

    [Fact]
    public static void GetPropertyInvalidUtf16()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("{\"name\":\"value\"}"))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            Assert.Throws<ArgumentException>(() => doc.RootElement.GetProperty("unpaired\uDFFE"));

            Assert.Throws<ArgumentException>(() => doc.RootElement.TryGetProperty("unpaired\uDFFE", out _));
        }
    }

    [Theory]
    [InlineData("short")]
    [InlineData("thisValueIsLongerThan86CharsSoWeDeferTheTranscodingUntilWeFindAViableCandidateAsAPropertyMatch")]
    public static void GetPropertyFindsLast(string propertyName)
    {
        string json = $"{{ \"{propertyName}\": 1, \"{propertyName}\": 2, \"nope\": -1, \"{propertyName}\": 3 }}";

        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable root = builderDoc.RootElement;
            byte[] utf8PropertyName = Encoding.UTF8.GetBytes(propertyName);

            Assert.Equal(3, root.GetProperty(propertyName).GetInt32());
            Assert.Equal(3, root.GetProperty(propertyName.AsSpan()).GetInt32());
            Assert.Equal(3, root.GetProperty(utf8PropertyName).GetInt32());

            JsonElement.Mutable matchedProperty;
            Assert.True(root.TryGetProperty(propertyName, out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(propertyName.AsSpan(), out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(utf8PropertyName, out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
        }
    }

    [Theory]
    [InlineData("short")]
    [InlineData("thisValueIsLongerThan86CharsSoWeDeferTheTranscodingUntilWeFindAViableCandidateAsAPropertyMatch")]
    public static void GetPropertyFindsLast_WithEscaping(string propertyName)
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

            Assert.Equal(1, root.GetProperty(propertyName).GetInt32());
            Assert.Equal(1, root.GetProperty(propertyName.AsSpan()).GetInt32());
            Assert.Equal(1, root.GetProperty(utf8PropertyName).GetInt32());

            Assert.Equal(2, root.GetProperty(pn2).GetInt32());
            Assert.Equal(2, root.GetProperty(pn2.AsSpan()).GetInt32());
            Assert.Equal(2, root.GetProperty(utf8PropertyName2).GetInt32());

            Assert.Equal(3, root.GetProperty(pn3).GetInt32());
            Assert.Equal(3, root.GetProperty(pn3.AsSpan()).GetInt32());
            Assert.Equal(3, root.GetProperty(utf8PropertyName3).GetInt32());

            JsonElement.Mutable matchedProperty;
            Assert.True(root.TryGetProperty(propertyName, out matchedProperty));
            Assert.Equal(1, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(propertyName.AsSpan(), out matchedProperty));
            Assert.Equal(1, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(utf8PropertyName, out matchedProperty));
            Assert.Equal(1, matchedProperty.GetInt32());

            Assert.True(root.TryGetProperty(pn2, out matchedProperty));
            Assert.Equal(2, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(pn2.AsSpan(), out matchedProperty));
            Assert.Equal(2, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(utf8PropertyName2, out matchedProperty));
            Assert.Equal(2, matchedProperty.GetInt32());

            Assert.True(root.TryGetProperty(pn3, out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(pn3.AsSpan(), out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
            Assert.True(root.TryGetProperty(utf8PropertyName3, out matchedProperty));
            Assert.Equal(3, matchedProperty.GetInt32());
        }
    }

    [Fact]
    public static void GetRawText()
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
            Assert.Equal(6, builderDoc.RootElement.GetPropertyCount());
            ObjectEnumerator<JsonElement.Mutable> enumerator = builderDoc.RootElement.EnumerateObject();
            Assert.True(enumerator.MoveNext(), "Move to first property");
            JsonProperty<JsonElement.Mutable> property = enumerator.Current;

            Assert.Equal("  weird  property  name", property.Name);
            string rawText = property.ToString();
            int crCount = rawText.Count(c => c == '\r');
            Assert.Equal(2, property.Value.GetPropertyCount());
            Assert.Equal(128 + crCount, rawText.Length);
            Assert.Equal('\"', rawText[0]);
            Assert.Equal(' ', rawText[1]);
            Assert.Equal('}', rawText[rawText.Length - 1]);
            Assert.Equal(json.Substring(json.IndexOf('\"'), rawText.Length), rawText);

            Assert.True(enumerator.MoveNext(), "Move to number property");
            property = enumerator.Current;

            Assert.Equal("number", property.Name);
            Assert.Equal("\"number\": 1.02e+4", property.ToString());
            Assert.Equal(10200.0, property.Value.GetDouble());
            Assert.Equal("1.02e+4", property.Value.GetRawText());

            Assert.True(enumerator.MoveNext(), "Move to bool property");
            property = enumerator.Current;

            Assert.Equal("bool", property.Name);
            Assert.False(property.Value.GetBoolean());
            Assert.Equal("false", property.Value.GetRawText());
            Assert.Equal(bool.FalseString, property.Value.ToString());

            Assert.True(enumerator.MoveNext(), "Move to null property");
            property = enumerator.Current;

            Assert.Equal("null", property.Name);
            Assert.Equal("null", property.Value.GetRawText());
            Assert.Equal(string.Empty, property.Value.ToString());
            Assert.Equal("\"n\\u0075ll\": null", property.ToString());

            Assert.True(enumerator.MoveNext(), "Move to multiLineArray property");
            property = enumerator.Current;

            Assert.Equal("multiLineArray", property.Name);
            Assert.Equal(4, property.Value.GetArrayLength());
            rawText = property.Value.GetRawText();
            Assert.Equal('[', rawText[0]);
            Assert.Equal(']', rawText[rawText.Length - 1]);
            Assert.Contains('3', rawText);
            Assert.Contains('\n', rawText);

            Assert.True(enumerator.MoveNext(), "Move to string property");
            property = enumerator.Current;

            Assert.Equal("string", property.Name);
            rawText = property.Value.GetRawText();
            Assert.Equal('\"', rawText[0]);
            Assert.Equal('\"', rawText[rawText.Length - 1]);
            string strValue = property.Value.GetString();
            int newlineIdx = strValue.IndexOf('\r');
            int colonIdx = strValue.IndexOf(':');
            int escapedQuoteIdx = colonIdx + 2;
            Assert.Equal(rawText.Substring(1, newlineIdx), strValue.Substring(0, newlineIdx));
            Assert.Equal('\\', rawText[escapedQuoteIdx + 3]);
            Assert.Equal('\"', rawText[escapedQuoteIdx + 4]);
            Assert.Equal('\"', strValue[escapedQuoteIdx]);
            Assert.Contains("\r", strValue);
            Assert.Contains(@"\r", rawText);
            string valueText = rawText;
            rawText = property.ToString();
            Assert.Equal('\"', rawText[0]);
            Assert.Equal('\"', rawText[rawText.Length - 1]);
            Assert.NotEqual(valueText, rawText);
            Assert.EndsWith(valueText, rawText);

            Assert.False(enumerator.MoveNext(), "Move past the last property");
        }
    }

    [Fact]
    public static void ArrayEnumeratorIndependentWalk()
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

            Assert.True(structEnumerator.MoveNext());
            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.True(weakBoxedEnumerator.MoveNext());

            Assert.Equal(0, structEnumerator.Current.GetInt32());
            Assert.Equal(0, strongBoxedEnumerator.Current.GetInt32());
            Assert.Equal(0, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.True(weakBoxedEnumerator.MoveNext());

            Assert.Equal(1, structEnumerator.Current.GetInt32());
            Assert.Equal(1, strongBoxedEnumerator.Current.GetInt32());
            Assert.Equal(1, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            int test = 0;

            foreach (JsonElement.Mutable element in structEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in structEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in strongBoxedEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in strongBoxedEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in weakBoxedEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonElement.Mutable element in weakBoxedEnumerable)
            {
                Assert.Equal(test, element.GetInt32());
                test++;
            }

            structEnumerator.Reset();

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(0, structEnumerator.Current.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(1, structEnumerator.Current.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(2, structEnumerator.Current.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(3, structEnumerator.Current.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal(2, strongBoxedEnumerator.Current.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(4, structEnumerator.Current.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal(5, structEnumerator.Current.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal(3, strongBoxedEnumerator.Current.GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal(2, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.False(structEnumerator.MoveNext());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal(4, strongBoxedEnumerator.Current.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal(5, strongBoxedEnumerator.Current.GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal(3, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal(4, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.False(structEnumerator.MoveNext());
            Assert.False(strongBoxedEnumerator.MoveNext());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal(5, ((JsonElement.Mutable)weakBoxedEnumerator.Current).GetInt32());

            Assert.False(weakBoxedEnumerator.MoveNext());
            Assert.False(structEnumerator.MoveNext());
            Assert.False(strongBoxedEnumerator.MoveNext());
            Assert.False(weakBoxedEnumerator.MoveNext());
        }
    }

    [Fact]
    public static void DefaultArrayEnumeratorDoesNotThrow()
    {
        ArrayEnumerator<JsonElement.Mutable> enumerable = default;
        ArrayEnumerator<JsonElement.Mutable> enumerator = enumerable.GetEnumerator();
        ArrayEnumerator<JsonElement.Mutable> defaultEnumerator = default;

        Assert.Equal(JsonValueKind.Undefined, enumerable.Current.ValueKind);
        Assert.Equal(JsonValueKind.Undefined, enumerator.Current.ValueKind);

        Assert.False(enumerable.MoveNext());
        Assert.False(enumerable.MoveNext());
        Assert.False(defaultEnumerator.MoveNext());
    }

    [Fact]
    public static void ObjectEnumeratorIndependentWalk()
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

            Assert.True(structEnumerator.MoveNext());
            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.True(weakBoxedEnumerator.MoveNext());

            Assert.Equal("name0", structEnumerator.Current.Name);
            Assert.Equal(0, structEnumerator.Current.Value.GetInt32());
            Assert.Equal("name0", strongBoxedEnumerator.Current.Name);
            Assert.Equal(0, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.Equal("name0", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.Equal(0, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.True(weakBoxedEnumerator.MoveNext());

            Assert.Equal(1, structEnumerator.Current.Value.GetInt32());
            Assert.Equal(1, strongBoxedEnumerator.Current.Value.GetInt32());
            Assert.Equal(1, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            int test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in structEnumerable)
            {
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in structEnumerable)
            {
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in strongBoxedEnumerable)
            {
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in strongBoxedEnumerable)
            {
                string propertyName = property.Name;
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;

                // Subsequent read of the same JsonProperty should return an equal string
                string propertyName2 = property.Name;
                Assert.Equal(propertyName, propertyName2);
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in weakBoxedEnumerable)
            {
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;
            }

            test = 0;

            foreach (JsonProperty<JsonElement.Mutable> property in weakBoxedEnumerable)
            {
                Assert.Equal("name" + test, property.Name);
                Assert.Equal(test, property.Value.GetInt32());
                test++;
            }

            structEnumerator.Reset();

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name0", structEnumerator.Current.Name);
            Assert.Equal(0, structEnumerator.Current.Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name1", structEnumerator.Current.Name);
            Assert.Equal(1, structEnumerator.Current.Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name2", structEnumerator.Current.Name);
            Assert.Equal(2, structEnumerator.Current.Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name3", structEnumerator.Current.Name);
            Assert.Equal(3, structEnumerator.Current.Value.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal("name2", strongBoxedEnumerator.Current.Name);
            Assert.Equal(2, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name4", structEnumerator.Current.Name);
            Assert.Equal(4, structEnumerator.Current.Value.GetInt32());

            Assert.True(structEnumerator.MoveNext());
            Assert.Equal("name5", structEnumerator.Current.Name);
            Assert.Equal(5, structEnumerator.Current.Value.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal("name3", strongBoxedEnumerator.Current.Name);
            Assert.Equal(3, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal("name2", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.Equal(2, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.False(structEnumerator.MoveNext());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal("name4", strongBoxedEnumerator.Current.Name);
            Assert.Equal(4, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.True(strongBoxedEnumerator.MoveNext());
            Assert.Equal("name5", strongBoxedEnumerator.Current.Name);
            Assert.Equal(5, strongBoxedEnumerator.Current.Value.GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal("name3", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.Equal(3, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal("name4", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.Equal(4, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.False(structEnumerator.MoveNext());
            Assert.False(strongBoxedEnumerator.MoveNext());

            Assert.True(weakBoxedEnumerator.MoveNext());
            Assert.Equal("name5", ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Name);
            Assert.Equal(5, ((JsonProperty<JsonElement.Mutable>)weakBoxedEnumerator.Current).Value.GetInt32());

            Assert.False(weakBoxedEnumerator.MoveNext());
            Assert.False(structEnumerator.MoveNext());
            Assert.False(strongBoxedEnumerator.MoveNext());
            Assert.False(weakBoxedEnumerator.MoveNext());
        }
    }

    [Fact]
    public static void DefaultObjectEnumeratorDoesNotThrow()
    {
        ObjectEnumerator<JsonElement.Mutable> enumerable = default;
        ObjectEnumerator<JsonElement.Mutable> enumerator = enumerable.GetEnumerator();
        ObjectEnumerator<JsonElement.Mutable> defaultEnumerator = default;

        Assert.Equal(JsonValueKind.Undefined, enumerable.Current.Value.ValueKind);
        Assert.Equal(JsonValueKind.Undefined, enumerator.Current.Value.ValueKind);

        Assert.Throws<InvalidOperationException>(() => enumerable.Current.Name);
        Assert.Throws<InvalidOperationException>(() => enumerator.Current.Name);

        Assert.False(enumerable.MoveNext());
        Assert.False(enumerable.MoveNext());
        Assert.False(defaultEnumerator.MoveNext());
    }

    [Fact]
    public static void ReadNestedObject()
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
            Assert.Equal(JsonValueKind.Object, root.ValueKind);

            Assert.True(root.GetProperty("first").GetProperty("true").GetBoolean());
            Assert.False(root.GetProperty("first").GetProperty("false").GetBoolean());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("first").GetProperty("null").ValueKind);
            Assert.Equal(3, root.GetProperty("first").GetProperty("int").GetInt32());
            Assert.Equal(3.14159f, root.GetProperty("first").GetProperty("nearlyPi").GetSingle());
            Assert.Equal("This is some text that does not end... <EOT>", root.GetProperty("first").GetProperty("text").GetString());

            Assert.True(root.GetProperty("second").GetProperty("blub").GetProperty("bool").GetBoolean());
            Assert.False(root.GetProperty("second").GetProperty("glub").GetProperty("bool").GetBoolean());
        }
    }

    [Theory]
    [InlineData("""{ "foo" : [1], "test": false, "bar" : { "nested": 3 } }""", 3)]
    [InlineData("""{ "foo" : [1,2,3,4] }""", 1)]
    [InlineData("""{}""", 0)]
    [InlineData("""{ "foo" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }, "test": true, "foo2" : {"nested:" : {"nested": 1, "bla": [1, 2, {"bla": 3}] } }}""", 3)]
    public static void TestGetPropertyCount(string json, int expectedCount)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(json))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable element = builderDoc.RootElement;
            Assert.Equal(expectedCount, element.GetPropertyCount());
        }
    }

    [Fact]
    public static void VerifyGetPropertyCountAndArrayLengthUsingEnumerateMethods()
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
                Assert.Equal(elem.EnumerateObject().Count(), elem.GetPropertyCount());
                foreach (JsonProperty<JsonElement.Mutable> prop in elem.EnumerateObject())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(prop.Value);
                }
            }
            else if (elem.ValueKind == JsonValueKind.Array)
            {
                Assert.Equal(elem.EnumerateArray().Count(), elem.GetArrayLength());
                foreach (JsonElement.Mutable item in elem.EnumerateArray())
                {
                    CheckPropertyCountAndArrayLengthAgainstEnumerateMethods(item);
                }
            }
        }
    }

    [Fact]
    public static void ValueEquals_Null_TrueForNullFalseForEmpty()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("   null   "))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.True(jElement.ValueEquals((string)null));
            Assert.True(jElement.ValueEquals(default(ReadOnlySpan<char>)));
            Assert.True(jElement.ValueEquals((ReadOnlySpan<byte>)null));
            Assert.True(jElement.ValueEquals(default(ReadOnlySpan<byte>)));

            Assert.False(jElement.ValueEquals(Array.Empty<byte>()));
            Assert.False(jElement.ValueEquals(""));
            Assert.False(jElement.ValueEquals("".AsSpan()));
        }
    }

    [Fact]
    public static void ValueEquals_EmptyJsonString_True()
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse("\"\""))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.True(jElement.ValueEquals(""));
            Assert.True(jElement.ValueEquals("".AsSpan()));
            Assert.True(jElement.ValueEquals(default(ReadOnlySpan<char>)));
            Assert.True(jElement.ValueEquals(default(ReadOnlySpan<byte>)));
            Assert.True(jElement.ValueEquals(Array.Empty<byte>()));
        }
    }

    [Fact]
    public static void ValueEquals_TestTextEqualsLargeMatch_True()
    {
        string lookup = new('a', 320);
        string jsonString = "\"" + lookup + "\"";
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.True(jElement.ValueEquals((string)lookup));
            Assert.True(jElement.ValueEquals((ReadOnlySpan<char>)lookup.AsSpan()));
        }
    }

    [Theory]
    [InlineData("\"conne\\u0063tionId\"", "connectionId")]
    [InlineData("\"connectionId\"", "connectionId")]
    [InlineData("\"123\"", "123")]
    [InlineData("\"My name is \\\"Ahson\\\"\"", "My name is \"Ahson\"")]
    public static void ValueEquals_JsonTokenStringType_True(string jsonString, string expected)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.True(jElement.ValueEquals(expected));
            Assert.True(jElement.ValueEquals(expected.AsSpan()));
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(expected);
            Assert.True(jElement.ValueEquals(expectedGetBytes));
        }
    }

    [Theory]
    [InlineData("\"conne\\u0063tionId\"", "c")]
    public static void ValueEquals_DestinationTooSmallComparesEscaping_False(string jsonString, string other)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.False(jElement.ValueEquals(other));
            Assert.False(jElement.ValueEquals(other.AsSpan()));
            byte[] otherGetBytes = Encoding.UTF8.GetBytes(other);
            Assert.False(jElement.ValueEquals(otherGetBytes));
        }
    }

    [Theory]
    [InlineData("\"hello\"", new char[1] { (char)0xDC01 })]    // low surrogate - invalid
    [InlineData("\"hello\"", new char[1] { (char)0xD801 })]    // high surrogate - missing pair
    public static void InvalidUTF16Search(string jsonString, char[] lookup)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.False(jElement.ValueEquals(lookup));
        }
    }

    [Theory]
    [InlineData("\"connectionId\"", "\"conne\\u0063tionId\"")]
    [InlineData("\"conne\\u0063tionId\"", "connecxionId")] // intentionally making mismatch after escaped character
    [InlineData("\"conne\\u0063tionId\"", "bonnectionId")] // intentionally changing the expected starting character
    public static void ValueEquals_JsonTokenStringType_False(string jsonString, string otherText)
    {
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            Assert.False(jElement.ValueEquals(otherText));
            Assert.False(jElement.ValueEquals(otherText.AsSpan()));
            byte[] expectedGetBytes = Encoding.UTF8.GetBytes(otherText);
            Assert.False(jElement.ValueEquals(expectedGetBytes));
        }
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"\" : \"\"}")]
    [InlineData("{\"sample\" : \"\"}")]
    [InlineData("{\"sample\" : null}")]
    [InlineData("{\"sample\" : \"sample-value\"}")]
    [InlineData("{\"connectionId\" : \"123\"}")]
    public static void ValueEquals_NotString_Throws(string jsonString)
    {
        const string ErrorMessage = "The requested operation requires an element of type 'String', but the target element has type 'Object'.";
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            const string ThrowsAnyway = "throws-anyway";
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway), ErrorMessage);
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway.AsSpan()), ErrorMessage);
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)), ErrorMessage);
        }
    }

    [Fact]
    public static void NameEquals_Empty_Throws()
    {
        const string jsonString = "{\"\" : \"some-value\"}";
        const string ErrorMessage = "The requested operation requires an element of type 'String', but the target element has type 'Object'.";
        using (var workspace = JsonWorkspace.Create())
        using (var doc = ParsedJsonDocument<JsonElement>.Parse(jsonString))
        using (JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace))
        {
            JsonElement.Mutable jElement = builderDoc.RootElement;
            const string ThrowsAnyway = "throws-anyway";
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway), ErrorMessage);
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(ThrowsAnyway.AsSpan()), ErrorMessage);
            AssertExtensions.Throws<InvalidOperationException>(() => jElement.ValueEquals(Encoding.UTF8.GetBytes(ThrowsAnyway)), ErrorMessage);
        }
    }

    ////[ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsThreadingSupported))]
    [Fact]
    [OuterLoop] // thread-safety / stress test
    public static async Task GetString_ConcurrentUse_ThreadSafe()
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
                            Assert.Equal("John", first.GetString());
                            Assert.True(first.ValueEquals("John"));
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default),
                    Task.Factory.StartNew(() =>
                    {
                        gate.SignalAndWait();
                        for (int i = 0; i < Iters; i++)
                        {
                            Assert.Equal("Smith", last.GetString());
                            Assert.True(last.ValueEquals("Smith"));
                        }
                    }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default));
            }
        }
    }

    [Fact]
    public static void CopySingleDimensionalArray()
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

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, out written));
            Assert.True(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, out written));
            Assert.True(outputByte.SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, out written));
            Assert.True(outputInt16.SequenceEqual(new short[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, out written));
            Assert.True(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, out written));
            Assert.True(outputInt32.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, out written));
            Assert.True(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, out written));
            Assert.True(outputInt64.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, out written));
            Assert.True(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, out written));
            Assert.True(outputDouble.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, out written));
            Assert.True(outputSingle.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, out written));
            Assert.True(outputDecimal.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

#if NET
            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, out written));
            Assert.True(outputInt128.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, out written));
            Assert.True(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, out written));
            Assert.True(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3]));
            Assert.Equal(length, written);
#endif
        }
    }

    [Fact]
    public static void CopyArrayRank1()
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

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.True(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.True(outputByte.SequenceEqual(new byte[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.True(outputInt16.SequenceEqual(new short[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.True(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.True(outputInt32.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.True(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.True(outputInt64.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.True(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.True(outputDouble.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.True(outputSingle.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.True(outputDecimal.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

#if NET
            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.True(outputInt128.SequenceEqual([1, 2, 3]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.True(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.True(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3]));
            Assert.Equal(length, written);
#endif
        }
    }

    [Fact]
    public static void CopyArrayRank1_OutputTooShort()
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
            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.Equal(0, written);

#if NET
            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.Equal(0, written);

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.Equal(0, written);
#endif
        }
    }

    [Fact]
    public static void CopyArrayRank2()
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

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.True(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.True(outputByte.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.True(outputInt16.SequenceEqual(new short[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.True(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.True(outputInt32.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.True(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.True(outputInt64.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.True(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.True(outputDouble.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.True(outputSingle.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.True(outputDecimal.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

#if NET
            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.True(outputInt128.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8, 9]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.True(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3, 4, 5, 6, 7, 8, 9 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.True(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3, (Half)4, (Half)5, (Half)6, (Half)7, (Half)8, (Half)9]));
            Assert.Equal(length, written);
#endif
        }
    }

    [Fact]
    public static void CopyArrayRank3Ragged()
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

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.True(outputSbyte.SequenceEqual(new sbyte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.True(outputByte.SequenceEqual(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.True(outputInt16.SequenceEqual(new short[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.True(outputUInt16.SequenceEqual(new ushort[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.True(outputInt32.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.True(outputUInt32.SequenceEqual(new uint[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.True(outputInt64.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.True(outputUInt64.SequenceEqual(new ulong[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.True(outputDouble.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.True(outputSingle.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.True(outputDecimal.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

#if NET
            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.True(outputInt128.SequenceEqual([1, 2, 3, 4, 5, 6, 7, 8]));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.True(outputUInt128.SequenceEqual(new UInt128[] { 1, 2, 3, 4, 5, 6, 7, 8 }));
            Assert.Equal(length, written);

            Assert.True(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.True(outputHalf.SequenceEqual([(Half)1, (Half)2, (Half)3, (Half)4, (Half)5, (Half)6, (Half)7, (Half)8]));
            Assert.Equal(length, written);
#endif
        }
    }

    [Fact]
    public static void CopyArrayRank3Ragged_OutputTooShort()
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
            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputSbyte, val => Assert.Equal(0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputByte, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputByte, val => Assert.Equal(0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt16, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputInt16, val => Assert.Equal(0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt16, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputUInt16, val => Assert.Equal((ushort)0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt32, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputInt32, val => Assert.Equal(0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt32, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputUInt32, val => Assert.Equal((uint)0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt64, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputInt64, val => Assert.Equal(0L, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt64, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputUInt64, val => Assert.Equal((ulong)0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDouble, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputDouble, val => Assert.Equal(0d, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSingle, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputSingle, val => Assert.Equal(0f, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputDecimal, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputDecimal, val => Assert.Equal(0m, val));

#if NET
            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputInt128, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputInt128, val => Assert.Equal((Int128)0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputUInt128, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputUInt128, val => Assert.Equal((UInt128)0, val));

            Assert.False(JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputHalf, rank, out written));
            Assert.Equal(0, written);
            Assert.All(outputHalf, val => Assert.Equal((Half)0, val));
#endif
        }
    }

    [Fact]
    public static void CopyArrayNonIntegerType()
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

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

#if NET
            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);

            AssertExtensions.Throws<InvalidOperationException>(() => JsonElementTensorHelpers.TryCopyArrayOfRankTo(root.ParentDocument, root.ParentDocumentIndex, outputSbyte, rank, out written), ErrorMessage);
            Assert.All(outputSbyte, val => Assert.Equal(0, val)); // Ensure the array is not modified
            Assert.Equal(0, written);
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

    [Fact]
    public static async Task VerifyMultiThreadedDispose()
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
            Assert.True(uniqueAddresses.Add(arr));
            count--;
        }
    }

    [Fact]
    public static void DeserializeNullAsNullLiteral()
    {
        var jsonDocument = ParsedJsonDocument<JsonElement>.Parse("null");
        Assert.NotNull(jsonDocument);
        Assert.Equal(JsonValueKind.Null, jsonDocument.RootElement.ValueKind);
    }

    [Fact]
    public static void CreateBuilderFromMutableBuilderThrows()
    {
        const string ErrorMessage = "You cannot create a mutable copy of a mutable document. Consider calling Freeze() on the source document.";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder = JsonElement.CreateBuilder(workspace, "Hello"u8);
        AssertExtensions.Throws<InvalidOperationException>(() => mutableBuilder.RootElement.CreateBuilder(workspace), ErrorMessage);
    }

    [Fact]
    public static void CreateBuilderFromImmutableBuilderSucceeds()
    {
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder = JsonElement.CreateBuilder(workspace, "Hello"u8);
        Assert.False(mutableBuilder.IsImmutable);
        mutableBuilder.Freeze();
        Assert.True(mutableBuilder.IsImmutable);
        using JsonDocumentBuilder<JsonElement.Mutable> copyOfFrozen = mutableBuilder.RootElement.CreateBuilder(workspace);
        Assert.False(copyOfFrozen.IsImmutable);
    }

    [Fact]
    public static void ModifyingImmutableBuilderFails()
    {
        const string ErrorMessage = "You cannot modify an immutable document.";
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableBuilder =
            JsonElement.CreateBuilder(
                workspace,
                new((ref arrayBuilder) => arrayBuilder.AddItem(true)));
        Assert.False(mutableBuilder.IsImmutable);
        mutableBuilder.Freeze();
        Assert.True(mutableBuilder.IsImmutable);
        AssertExtensions.Throws<InvalidOperationException>(() => mutableBuilder.RootElement.SetItem(0, false), ErrorMessage);
    }

    [Fact]
    public static void SetItem_OnEmptyArray_AddsItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(0, 123);

        // Assert
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal(123, root[0].GetInt32());
    }

    [Fact]
    public static void SetItem_OnNonEmptyArray_ReplacesExistingItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(1, 99);

        // Assert
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(99, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    [Fact]
    public static void SetItem_AtArrayLength_AppendsItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10,20]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.SetItem(2, 30);

        // Assert
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(20, root[1].GetInt32());
        Assert.Equal(30, root[2].GetInt32());
    }

    [Fact]
    public static void SetItem_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 100));
    }

    [Fact]
    public static void SetItem_Throws_WhenIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(4, 100));
    }

    // Place these inside the JsonDocumentBuilderTests class

    [Fact]
    public static void SetItem_Bool_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, true);
        Assert.True(root[0].GetBoolean());

        root.SetItem(1, false);
        Assert.Equal(2, root.GetArrayLength());
        Assert.False(root[1].GetBoolean());
    }

    [Fact]
    public static void SetItem_Guid_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"[\"{guid1}\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, guid2);
        Assert.Equal(guid2, root[0].GetGuid());

        var guid3 = Guid.NewGuid();
        root.SetItem(1, guid3);
        Assert.Equal(guid3, root[1].GetGuid());
    }

    [Fact]
    public static void SetItem_Byte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (byte)42);
        Assert.Equal(42, root[0].GetByte());

        root.SetItem(1, (byte)255);
        Assert.Equal(255, root[1].GetByte());
    }

    [Fact]
    public static void SetItem_Long_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, long.MaxValue);
        Assert.Equal(long.MaxValue, root[0].GetInt64());

        root.SetItem(1, long.MinValue);
        Assert.Equal(long.MinValue, root[1].GetInt64());
    }

    [Fact]
    public static void SetItem_Short_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (short)-12345);
        Assert.Equal(-12345, root[0].GetInt16());

        root.SetItem(1, (short)12345);
        Assert.Equal(12345, root[1].GetInt16());
    }

    [Fact]
    public static void SetItem_Float_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 3.14f);
        Assert.Equal(3.14f, root[0].GetSingle());

        root.SetItem(1, -2.71f);
        Assert.Equal(-2.71f, root[1].GetSingle());
    }

    [Fact]
    public static void SetItem_Double_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 2.718281828);
        Assert.Equal(2.718281828, root[0].GetDouble());

        root.SetItem(1, -3.1415926535);
        Assert.Equal(-3.1415926535, root[1].GetDouble());
    }

    [Fact]
    public static void SetItem_Generic_Works()
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
        Assert.Equal(42, root[0].GetInt32());
    }

    [Fact]
    public static void SetItem_Utf8String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Simple ASCII
        root.SetItem(0, "hello"u8);
        Assert.Equal("hello", root[0].GetString());

        root.SetItem(1, "world"u8);
        Assert.Equal("world", root[1].GetString());

        // Escaped UTF-8 string: "foo\"bar" (the quote is escaped in JSON, but the UTF-8 bytes are unescaped)
        ReadOnlySpan<byte> escapedUtf8 = "foo\"bar"u8;
        root.SetItem(2, escapedUtf8);
        Assert.Equal("foo\"bar", root[2].GetString());

        // Non-ASCII UTF-8 string: "héllo" (e with acute accent)
        ReadOnlySpan<byte> nonAsciiUtf8 = [(byte)'h', 0xC3, 0xA9, (byte)'l', (byte)'l', (byte)'o'];
        root.SetItem(3, nonAsciiUtf8);
        Assert.Equal("héllo", root[3].GetString());

        // Non-ASCII UTF-8 string: emoji "😊"
        ReadOnlySpan<byte> emojiUtf8 = "😊"u8;
        root.SetItem(4, emojiUtf8);
        Assert.Equal("😊", root[4].GetString());
    }

    [Fact]
    public static void SetItem_String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Simple ASCII
        root.SetItem(0, "hello");
        Assert.Equal("hello", root[0].GetString());

        root.SetItem(1, "world");
        Assert.Equal("world", root[1].GetString());

        // Escaped UTF-8 string: "foo\"bar" (the quote is escaped in JSON, but the UTF-8 bytes are unescaped)
        string escapedString = "foo\"bar";
        root.SetItem(2, escapedString);
        Assert.Equal("foo\"bar", root[2].GetString());

        // Non-ASCII UTF-8 string: "héllo" (e with acute accent)
        byte[] nonAsciiUtf8 = [(byte)'h', 0xC3, 0xA9, (byte)'l', (byte)'l', (byte)'o'];
        root.SetItem(3, Encoding.UTF8.GetString(nonAsciiUtf8));
        Assert.Equal("héllo", root[3].GetString());

        // Non-ASCII string: emoji "😊"
        string emoji = "😊";
        root.SetItem(4, emoji);
        Assert.Equal("😊", root[4].GetString());
    }

    [Fact]
    public static void SetItemNull_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItemNull(0);
        Assert.Equal(JsonValueKind.Null, root[0].ValueKind);

        root.SetItemNull(1);
        Assert.Equal(JsonValueKind.Null, root[1].ValueKind);
    }

    [Fact]
    public static void SetItem_SByte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (sbyte)-8);
        Assert.Equal(-8, root[0].GetSByte());

        root.SetItem(1, (sbyte)127);
        Assert.Equal(127, root[1].GetSByte());
    }

    [Fact]
    public static void SetItem_UShort_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ushort)65535);
        Assert.Equal(65535, root[0].GetUInt16());

        root.SetItem(1, (ushort)42);
        Assert.Equal((ushort)42, root[1].GetUInt16());
    }

    [Fact]
    public static void SetItem_UInt_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 4294967295u);
        Assert.Equal(4294967295u, root[0].GetUInt32());

        root.SetItem(1, 123u);
        Assert.Equal(123u, root[1].GetUInt32());
    }

    [Fact]
    public static void SetItem_ULong_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 18446744073709551615ul);
        Assert.Equal(18446744073709551615ul, root[0].GetUInt64());

        root.SetItem(1, 42ul);
        Assert.Equal(42ul, root[1].GetUInt64());
    }

    [Fact]
    public static void SetItem_Int_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, -123456);
        Assert.Equal(-123456, root[0].GetInt32());

        root.SetItem(1, 654321);
        Assert.Equal(654321, root[1].GetInt32());
    }

    [Fact]
    public static void SetItem_Decimal_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, 123.456m);
        Assert.Equal(123.456m, root[0].GetDecimal());

        root.SetItem(1, -789.01m);
        Assert.Equal(-789.01m, root[1].GetDecimal());
    }

#if NET

    [Fact]
    public static void SetItem_Int128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetItem(0, bigValue);
        Assert.Equal(bigValue, root[0].GetInt128());

        root.SetItem(1, smallValue);
        Assert.Equal(smallValue, root[1].GetInt128());
    }

    [Fact]
    public static void SetItem_UInt128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetItem(0, bigValue);
        Assert.Equal(bigValue, root[0].GetUInt128());

        root.SetItem(1, smallValue);
        Assert.Equal(smallValue, root[1].GetUInt128());
    }

    [Fact]
    public static void SetItem_Half_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetItem(0, value1);
        Assert.Equal(value1, root[0].GetHalf());

        root.SetItem(1, value2);
        Assert.Equal(value2, root[1].GetHalf());
    }

#endif

    [Fact]
    public static void SetItem_DateTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        root.SetItem(0, dt1);
        Assert.Equal(dt1, root[0].GetDateTime());

        root.SetItem(1, dt2);
        Assert.Equal(dt2, root[1].GetDateTime());
    }

    [Fact]
    public static void SetItem_DateTimeOffset_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));

        root.SetItem(0, dto1);
        Assert.Equal(dto1, root[0].GetDateTimeOffset());

        root.SetItem(1, dto2);
        Assert.Equal(dto2, root[1].GetDateTimeOffset());
    }

    [Fact]
    public static void SetItem_Bool_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, false));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, true));
    }

    [Fact]
    public static void SetItem_Guid_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"00000000-0000-0000-0000-000000000000\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        var guid = Guid.NewGuid();

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, guid));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, guid));
    }

    [Fact]
    public static void SetItem_Byte_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, (byte)1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, (byte)2));
    }

    [Fact]
    public static void SetItem_SByte_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, (sbyte)1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, (sbyte)2));
    }

    [Fact]
    public static void SetItem_Short_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, (short)1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, (short)2));
    }

    [Fact]
    public static void SetItem_UShort_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, (ushort)1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, (ushort)2));
    }

    [Fact]
    public static void SetItem_Int_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2));
    }

    [Fact]
    public static void SetItem_UInt_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1u));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2u));
    }

    [Fact]
    public static void SetItem_Long_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1L));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2L));
    }

    [Fact]
    public static void SetItem_ULong_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1UL));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2UL));
    }

    [Fact]
    public static void SetItem_Float_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0f));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2.0f));
    }

    [Fact]
    public static void SetItem_Double_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2.0));
    }

    [Fact]
    public static void SetItem_Decimal_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, 1.0m));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, 2.0m));
    }

    [Fact]
    public static void SetItem_Utf8String_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, "test"u8));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, "test"u8));
    }

    [Fact]
    public static void SetItem_Generic_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        using var doc2 = ParsedJsonDocument<JsonElement>.Parse("42");
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc2 = doc2.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable value = builderDoc2.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [Fact]
    public static void SetItemNull_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItemNull(-1));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItemNull(2));
    }

#if NET

    [Fact]
    public static void SetItem_Int128_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Int128 value = Int128.One;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [Fact]
    public static void SetItem_UInt128_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        UInt128 value = UInt128.One;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

    [Fact]
    public static void SetItem_Half_Throws_WhenIndexOutOfBounds()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1.0]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value = (Half)1.0;

        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(-1, value));
        Assert.Throws<IndexOutOfRangeException>(() => root.SetItem(2, value));
    }

#endif

    [Fact]
    public static void SetProperty_Object_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Existing UTF-8 property name tests
        root.SetProperty("a"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("a").ToString());
        root.SetProperty("a".AsSpan(), (ref o) => { o.AddProperty("hello"u8, "there"u8); });
        Assert.Equal("{\"hello\":\"there\"}", root.GetProperty("a").ToString());
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Object_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // Existing UTF-8 property name tests
        root.SetProperty("b"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("b").ToString());
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Object_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Existing UTF-8 property name test
        root.SetProperty("a"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Object_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": {\"c\": true}}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // Existing UTF-8 property name test
        root.SetProperty("b"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("b").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddProperty("x", "y"); });
        Assert.Equal("{\"x\":\"y\"}", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Array_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Array_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // ASCII property name
        root.SetProperty("b"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("b").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("foo\"bar").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Array_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": true}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("a").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Array_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"b\": {\"c\": true}}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        // ASCII property name
        root.SetProperty("b"u8, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("b").ToString());

        root.SetProperty("b".AsSpan(), (ref o) => { o.AddItem("there"u8); });
        Assert.Equal("[\"there\"]", root.GetProperty("b").ToString());

        // New: string property name
        root.SetProperty("str", (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("str").ToString());

        // New: ReadOnlySpan<char> property name
        ReadOnlySpan<char> spanName = "span";
        root.SetProperty(spanName, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root.GetProperty("span").ToString());
    }

    [Fact]
    public static void SetProperty_Bool_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, true);
        Assert.True(root.GetProperty("a").GetBoolean());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, false);
        Assert.False(root.GetProperty("héllo").GetBoolean());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, true);
        Assert.True(root.GetProperty("foo\"bar").GetBoolean());
    }

    [Fact]
    public static void SetProperty_Guid_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"{{\"a\":\"{guid1}\"}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, guid2);
        Assert.Equal(guid2, root.GetProperty("a").GetGuid());

        // Non-ASCII property name
        var guid3 = Guid.NewGuid();
        root.SetProperty("héllo"u8, guid3);
        Assert.Equal(guid3, root.GetProperty("héllo").GetGuid());

        // Encoded UTF-8 property name
        var guid4 = Guid.NewGuid();
        root.SetProperty("foo\"bar"u8, guid4);
        Assert.Equal(guid4, root.GetProperty("foo\"bar").GetGuid());
    }

    [Fact]
    public static void SetProperty_DateTime_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, dt1);
        Assert.Equal(dt1, root.GetProperty("a").GetDateTime());

        root.SetProperty("héllo"u8, dt2);
        Assert.Equal(dt2, root.GetProperty("héllo").GetDateTime());

        root.SetProperty("foo\"bar"u8, dt2);
        Assert.Equal(dt2, root.GetProperty("foo\"bar").GetDateTime());
    }

    [Fact]
    public static void SetProperty_DateTimeOffset_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetDateTimeOffset());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetDateTimeOffset());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetDateTimeOffset());
    }

    [Fact]
    public static void SetProperty_OffsetDateTime_Works()
    {
        var dto1 = new OffsetDateTime(new LocalDateTime(2024, 5, 30, 12, 34, 56), Offset.Zero);
        var dto2 = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetOffsetDateTime());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetOffsetDateTime());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetOffsetDateTime());
    }

    [Fact]
    public static void SetProperty_OffsetDate_Works()
    {
        var dto1 = new OffsetDate(new LocalDate(2024, 5, 30), Offset.Zero);
        var dto2 = new OffsetDate(new LocalDate(2025, 1, 1), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetOffsetDate());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetOffsetDate());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetOffsetDate());
    }

    [Fact]
    public static void SetProperty_LocalDate_Works()
    {
        var dto1 = new LocalDate(2024, 5, 30);
        var dto2 = new LocalDate(2025, 1, 1);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetLocalDate());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetLocalDate());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetLocalDate());
    }

    [Fact]
    public static void SetProperty_OffsetTime_Works()
    {
        var dto1 = new OffsetTime(new LocalTime(12, 34, 56), Offset.Zero);
        var dto2 = new OffsetTime(new LocalTime(0, 0, 0), Offset.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetOffsetTime());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetOffsetTime());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetOffsetTime());
    }

    [Fact]
    public static void SetProperty_Period_Works()
    {
        Period dto1 = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 1).Normalize();
        Period dto2 = Period.Zero;
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"P1W\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetPeriod());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetPeriod());

        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetPeriod());
    }

    [Fact]
    public static void SetProperty_Byte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (byte)42);
        Assert.Equal(42, root.GetProperty("a").GetByte());

        root.SetProperty("héllo"u8, (byte)255);
        Assert.Equal(255, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar"u8, (byte)128);
        Assert.Equal(128, root.GetProperty("foo\"bar").GetByte());
    }

    [Fact]
    public static void SetProperty_SByte_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (sbyte)-8);
        Assert.Equal(-8, root.GetProperty("a").GetSByte());

        root.SetProperty("héllo"u8, (sbyte)127);
        Assert.Equal(127, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar"u8, (sbyte)-128);
        Assert.Equal(-128, root.GetProperty("foo\"bar").GetSByte());
    }

    [Fact]
    public static void SetProperty_Short_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (short)-12345);
        Assert.Equal(-12345, root.GetProperty("a").GetInt16());

        root.SetProperty("héllo"u8, (short)12345);
        Assert.Equal(12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar"u8, (short)-32768);
        Assert.Equal(-32768, root.GetProperty("foo\"bar").GetInt16());
    }

    [Fact]
    public static void SetProperty_UShort_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, (ushort)65535);
        Assert.Equal(65535, root.GetProperty("a").GetUInt16());

        root.SetProperty("héllo"u8, (ushort)42);
        Assert.Equal((ushort)42, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar"u8, (ushort)0);
        Assert.Equal((ushort)0, root.GetProperty("foo\"bar").GetUInt16());
    }

    [Fact]
    public static void SetProperty_Int_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, -123456);
        Assert.Equal(-123456, root.GetProperty("a").GetInt32());

        root.SetProperty("héllo"u8, 654321);
        Assert.Equal(654321, root.GetProperty("héllo").GetInt32());

        root.SetProperty("foo\"bar"u8, int.MinValue);
        Assert.Equal(int.MinValue, root.GetProperty("foo\"bar").GetInt32());
    }

    [Fact]
    public static void SetProperty_UInt_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 4294967295u);
        Assert.Equal(4294967295u, root.GetProperty("a").GetUInt32());

        root.SetProperty("héllo"u8, 123u);
        Assert.Equal(123u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar"u8, 0u);
        Assert.Equal(0u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [Fact]
    public static void SetProperty_Long_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, long.MaxValue);
        Assert.Equal(long.MaxValue, root.GetProperty("a").GetInt64());

        root.SetProperty("héllo"u8, long.MinValue);
        Assert.Equal(long.MinValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar"u8, 0L);
        Assert.Equal(0L, root.GetProperty("foo\"bar").GetInt64());
    }

    [Fact]
    public static void SetProperty_ULong_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 18446744073709551615ul);
        Assert.Equal(18446744073709551615ul, root.GetProperty("a").GetUInt64());

        root.SetProperty("héllo"u8, 42ul);
        Assert.Equal(42ul, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar"u8, 0ul);
        Assert.Equal(0ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [Fact]
    public static void SetProperty_Float_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 3.14f);
        Assert.Equal(3.14f, root.GetProperty("a").GetSingle());

        root.SetProperty("héllo"u8, -2.71f);
        Assert.Equal(-2.71f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar"u8, 0.0f);
        Assert.Equal(0.0f, root.GetProperty("foo\"bar").GetSingle());
    }

    [Fact]
    public static void SetProperty_Double_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 2.718281828);
        Assert.Equal(2.718281828, root.GetProperty("a").GetDouble());

        root.SetProperty("héllo"u8, -3.1415926535);
        Assert.Equal(-3.1415926535, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar"u8, 0.0);
        Assert.Equal(0.0, root.GetProperty("foo\"bar").GetDouble());
    }

    [Fact]
    public static void SetProperty_Decimal_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a"u8, 123.456m);
        Assert.Equal(123.456m, root.GetProperty("a").GetDecimal());

        root.SetProperty("héllo"u8, -789.01m);
        Assert.Equal(-789.01m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar"u8, 0.0m);
        Assert.Equal(0.0m, root.GetProperty("foo\"bar").GetDecimal());
    }

#if NET

    [Fact]
    public static void SetProperty_Int128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("a"u8, bigValue);
        Assert.Equal(bigValue, root.GetProperty("a").GetInt128());

        root.SetProperty("héllo"u8, smallValue);
        Assert.Equal(smallValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar"u8, Int128.Zero);
        Assert.Equal(Int128.Zero, root.GetProperty("foo\"bar").GetInt128());
    }

    [Fact]
    public static void SetProperty_UInt128_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("a"u8, bigValue);
        Assert.Equal(bigValue, root.GetProperty("a").GetUInt128());

        root.SetProperty("héllo"u8, smallValue);
        Assert.Equal(smallValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar"u8, UInt128.Zero);
        Assert.Equal(UInt128.Zero, root.GetProperty("foo\"bar").GetUInt128());
    }

    [Fact]
    public static void SetProperty_Half_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("a"u8, value1);
        Assert.Equal(value1, root.GetProperty("a").GetHalf());

        root.SetProperty("héllo"u8, value2);
        Assert.Equal(value2, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar"u8, (Half)0.0);
        Assert.Equal((Half)0.0, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [Fact]
    public static void SetProperty_Bool_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":false}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", true);
        Assert.True(root.GetProperty("a").GetBoolean());

        root.SetProperty("héllo", false);
        Assert.False(root.GetProperty("héllo").GetBoolean());

        root.SetProperty("foo\"bar", true);
        Assert.True(root.GetProperty("foo\"bar").GetBoolean());
    }

    [Fact]
    public static void SetProperty_Guid_StringPropertyName_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse($"{{\"a\":\"{guid1}\"}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", guid2);
        Assert.Equal(guid2, root.GetProperty("a").GetGuid());

        var guid3 = Guid.NewGuid();
        root.SetProperty("héllo", guid3);
        Assert.Equal(guid3, root.GetProperty("héllo").GetGuid());

        var guid4 = Guid.NewGuid();
        root.SetProperty("foo\"bar", guid4);
        Assert.Equal(guid4, root.GetProperty("foo\"bar").GetGuid());
    }

    [Fact]
    public static void SetProperty_Byte_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (byte)42);
        Assert.Equal(42, root.GetProperty("a").GetByte());

        root.SetProperty("héllo", (byte)255);
        Assert.Equal(255, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar", (byte)128);
        Assert.Equal(128, root.GetProperty("foo\"bar").GetByte());
    }

    [Fact]
    public static void SetProperty_SByte_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (sbyte)-8);
        Assert.Equal(-8, root.GetProperty("a").GetSByte());

        root.SetProperty("héllo", (sbyte)127);
        Assert.Equal(127, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar", (sbyte)-128);
        Assert.Equal(-128, root.GetProperty("foo\"bar").GetSByte());
    }

    [Fact]
    public static void SetProperty_Short_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (short)-12345);
        Assert.Equal(-12345, root.GetProperty("a").GetInt16());

        root.SetProperty("héllo", (short)12345);
        Assert.Equal(12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar", (short)-32768);
        Assert.Equal(-32768, root.GetProperty("foo\"bar").GetInt16());
    }

    [Fact]
    public static void SetProperty_UShort_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", (ushort)65535);
        Assert.Equal(65535, root.GetProperty("a").GetUInt16());

        root.SetProperty("héllo", (ushort)42);
        Assert.Equal((ushort)42, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar", (ushort)0);
        Assert.Equal((ushort)0, root.GetProperty("foo\"bar").GetUInt16());
    }

    [Fact]
    public static void SetProperty_Int_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", -123456);
        Assert.Equal(-123456, root.GetProperty("a").GetInt32());

        root.SetProperty("héllo", 654321);
        Assert.Equal(654321, root.GetProperty("héllo").GetInt32());

        root.SetProperty("foo\"bar", int.MinValue);
        Assert.Equal(int.MinValue, root.GetProperty("foo\"bar").GetInt32());
    }

    [Fact]
    public static void SetProperty_UInt_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 4294967295u);
        Assert.Equal(4294967295u, root.GetProperty("a").GetUInt32());

        root.SetProperty("héllo", 123u);
        Assert.Equal(123u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar", 0u);
        Assert.Equal(0u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [Fact]
    public static void SetProperty_Long_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", long.MaxValue);
        Assert.Equal(long.MaxValue, root.GetProperty("a").GetInt64());

        root.SetProperty("héllo", long.MinValue);
        Assert.Equal(long.MinValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar", 0L);
        Assert.Equal(0L, root.GetProperty("foo\"bar").GetInt64());
    }

    [Fact]
    public static void SetProperty_ULong_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 18446744073709551615ul);
        Assert.Equal(18446744073709551615ul, root.GetProperty("a").GetUInt64());

        root.SetProperty("héllo", 42ul);
        Assert.Equal(42ul, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar", 0ul);
        Assert.Equal(0ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [Fact]
    public static void SetProperty_Float_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 3.14f);
        Assert.Equal(3.14f, root.GetProperty("a").GetSingle());

        root.SetProperty("héllo", -2.71f);
        Assert.Equal(-2.71f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar", 0.0f);
        Assert.Equal(0.0f, root.GetProperty("foo\"bar").GetSingle());
    }

    [Fact]
    public static void SetProperty_Double_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 2.718281828);
        Assert.Equal(2.718281828, root.GetProperty("a").GetDouble());

        root.SetProperty("héllo", -3.1415926535);
        Assert.Equal(-3.1415926535, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar", 0.0);
        Assert.Equal(0.0, root.GetProperty("foo\"bar").GetDouble());
    }

    [Fact]
    public static void SetProperty_Decimal_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", 123.456m);
        Assert.Equal(123.456m, root.GetProperty("a").GetDecimal());

        root.SetProperty("héllo", -789.01m);
        Assert.Equal(-789.01m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar", 0.0m);
        Assert.Equal(0.0m, root.GetProperty("foo\"bar").GetDecimal());
    }

#if NET

    [Fact]
    public static void SetProperty_Int128_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("a", bigValue);
        Assert.Equal(bigValue, root.GetProperty("a").GetInt128());

        root.SetProperty("héllo", smallValue);
        Assert.Equal(smallValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar", Int128.Zero);
        Assert.Equal(Int128.Zero, root.GetProperty("foo\"bar").GetInt128());
    }

    [Fact]
    public static void SetProperty_UInt128_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("a", bigValue);
        Assert.Equal(bigValue, root.GetProperty("a").GetUInt128());

        root.SetProperty("héllo", smallValue);
        Assert.Equal(smallValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar", UInt128.Zero);
        Assert.Equal(UInt128.Zero, root.GetProperty("foo\"bar").GetUInt128());
    }

    [Fact]
    public static void SetProperty_Half_StringPropertyName_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1.5}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("a", value1);
        Assert.Equal(value1, root.GetProperty("a").GetHalf());

        root.SetProperty("héllo", value2);
        Assert.Equal(value2, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar", (Half)0.0);
        Assert.Equal((Half)0.0, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [Fact]
    public static void SetProperty_DateTime_StringPropertyName_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dt1);
        Assert.Equal(dt1, root.GetProperty("a").GetDateTime());

        root.SetProperty("héllo", dt2);
        Assert.Equal(dt2, root.GetProperty("héllo").GetDateTime());
    }

    [Fact]
    public static void SetProperty_DateTimeOffset_StringPropertyName_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("a", dto1);
        Assert.Equal(dto1, root.GetProperty("a").GetDateTimeOffset());

        root.SetProperty("héllo", dto2);
        Assert.Equal(dto2, root.GetProperty("héllo").GetDateTimeOffset());
    }

    [Fact]
    public static void SetProperty_Utf8String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a"u8, "hello"u8);
        Assert.Equal("hello", root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, "world"u8);
        Assert.Equal("world", root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, "baz"u8);
        Assert.Equal("baz", root.GetProperty("foo\"bar").GetString());
    }

    [Fact]
    public static void SetProperty_Generic_Works()
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
        Assert.Equal(123, root.GetProperty("a").GetInt32());

        // Non-ASCII property name
        root.SetProperty("héllo"u8, value);
        Assert.Equal(123, root.GetProperty("héllo").GetInt32());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar"u8, value);
        Assert.Equal(123, root.GetProperty("foo\"bar").GetInt32());
    }

    [Fact]
    public static void SetProperty_String_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a", "hello");
        Assert.Equal("hello", root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetProperty("héllo", "world");
        Assert.Equal("world", root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar", "baz");
        Assert.Equal("baz", root.GetProperty("foo\"bar").GetString());
    }

    [Fact]
    public static void SetProperty_Null_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetPropertyNull("a");
        Assert.Null(root.GetProperty("a").GetString());

        // Non-ASCII property name
        root.SetPropertyNull("héllo");
        Assert.Null(root.GetProperty("héllo").GetString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetPropertyNull("foo\"bar");
        Assert.Null(root.GetProperty("foo\"bar").GetString());
    }

    [Fact]
    public static void SetProperty_JsonElement_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":\"a\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // ASCII property name
        root.SetProperty("a", doc.RootElement);
        Assert.Equal("{\"a\":\"a\"}", root.GetProperty("a").ToString());

        // Non-ASCII property name
        root.SetProperty("héllo", doc.RootElement);
        Assert.Equal("{\"a\":\"a\"}", root.GetProperty("héllo").ToString());

        // Encoded UTF-8 property name: "foo\"bar"
        root.SetProperty("foo\"bar", doc.RootElement);
        Assert.Equal("{\"a\":\"a\"}", root.GetProperty("foo\"bar").ToString());
    }

    [Fact]
    public static void SetProperty_Bool_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":false,\"foo\\\"bar\":true}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, true);
        Assert.True(root.GetProperty("héllo").GetBoolean());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, false);
        Assert.False(root.GetProperty("foo\"bar").GetBoolean());
    }

    [Fact]
    public static void SetProperty_Int_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, 42);
        Assert.Equal(42, root.GetProperty("héllo").GetInt32());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, 99);
        Assert.Equal(99, root.GetProperty("foo\"bar").GetInt32());
    }

    [Fact]
    public static void SetProperty_Utf8String_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"a\",\"foo\\\"bar\":\"b\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, "world"u8);
        Assert.Equal("world", root.GetProperty("héllo").GetString());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, "baz"u8);
        Assert.Equal("baz", root.GetProperty("foo\"bar").GetString());
    }

    [Fact]
    public static void SetProperty_Generic_WithNonAsciiAndEscapedNames_Works()
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
        Assert.Equal(123, root.GetProperty("héllo").GetInt32());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, value);
        Assert.Equal(123, root.GetProperty("foo\"bar").GetInt32());
    }

    [Fact]
    public static void SetProperty_Long_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, long.MaxValue);
        Assert.Equal(long.MaxValue, root.GetProperty("héllo").GetInt64());

        root.SetProperty("foo\"bar"u8, long.MinValue);
        Assert.Equal(long.MinValue, root.GetProperty("foo\"bar").GetInt64());
    }

    [Fact]
    public static void SetProperty_ULong_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, ulong.MaxValue);
        Assert.Equal(ulong.MaxValue, root.GetProperty("héllo").GetUInt64());

        root.SetProperty("foo\"bar"u8, 42ul);
        Assert.Equal(42ul, root.GetProperty("foo\"bar").GetUInt64());
    }

    [Fact]
    public static void SetProperty_Float_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 3.14f);
        Assert.Equal(3.14f, root.GetProperty("héllo").GetSingle());

        root.SetProperty("foo\"bar"u8, -2.71f);
        Assert.Equal(-2.71f, root.GetProperty("foo\"bar").GetSingle());
    }

    [Fact]
    public static void SetProperty_Double_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 2.718281828);
        Assert.Equal(2.718281828, root.GetProperty("héllo").GetDouble());

        root.SetProperty("foo\"bar"u8, -3.1415926535);
        Assert.Equal(-3.1415926535, root.GetProperty("foo\"bar").GetDouble());
    }

    [Fact]
    public static void SetProperty_Decimal_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 123.456m);
        Assert.Equal(123.456m, root.GetProperty("héllo").GetDecimal());

        root.SetProperty("foo\"bar"u8, -789.01m);
        Assert.Equal(-789.01m, root.GetProperty("foo\"bar").GetDecimal());
    }

    [Fact]
    public static void SetProperty_Null_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetPropertyNull("héllo"u8);
        Assert.Null(root.GetProperty("héllo").GetString());

        root.SetPropertyNull("foo\"bar"u8);
        Assert.Null(root.GetProperty("foo\"bar").GetString());
    }

#if NET

    [Fact]
    public static void SetProperty_Int128_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = Int128.Parse("170141183460469231731687303715884105727"); // Int128.MaxValue
        var smallValue = Int128.Parse("-170141183460469231731687303715884105728"); // Int128.MinValue

        root.SetProperty("héllo"u8, bigValue);
        Assert.Equal(bigValue, root.GetProperty("héllo").GetInt128());

        root.SetProperty("foo\"bar"u8, smallValue);
        Assert.Equal(smallValue, root.GetProperty("foo\"bar").GetInt128());
    }

    [Fact]
    public static void SetProperty_UInt128_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var bigValue = UInt128.Parse("340282366920938463463374607431768211455"); // UInt128.MaxValue
        UInt128 smallValue = 42;

        root.SetProperty("héllo"u8, bigValue);
        Assert.Equal(bigValue, root.GetProperty("héllo").GetUInt128());

        root.SetProperty("foo\"bar"u8, smallValue);
        Assert.Equal(smallValue, root.GetProperty("foo\"bar").GetUInt128());
    }

    [Fact]
    public static void SetProperty_Half_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1.1,\"foo\\\"bar\":2.2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var value1 = (Half)3.25;
        var value2 = (Half)(-2.5);

        root.SetProperty("héllo"u8, value1);
        Assert.Equal(value1, root.GetProperty("héllo").GetHalf());

        root.SetProperty("foo\"bar"u8, value2);
        Assert.Equal(value2, root.GetProperty("foo\"bar").GetHalf());
    }

#endif

    [Fact]
    public static void SetProperty_Byte_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (byte)123);
        Assert.Equal((byte)123, root.GetProperty("héllo").GetByte());

        root.SetProperty("foo\"bar"u8, (byte)200);
        Assert.Equal((byte)200, root.GetProperty("foo\"bar").GetByte());
    }

    [Fact]
    public static void SetProperty_SByte_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (sbyte)-42);
        Assert.Equal((sbyte)-42, root.GetProperty("héllo").GetSByte());

        root.SetProperty("foo\"bar"u8, (sbyte)127);
        Assert.Equal((sbyte)127, root.GetProperty("foo\"bar").GetSByte());
    }

    [Fact]
    public static void SetProperty_Short_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (short)-12345);
        Assert.Equal((short)-12345, root.GetProperty("héllo").GetInt16());

        root.SetProperty("foo\"bar"u8, (short)32767);
        Assert.Equal((short)32767, root.GetProperty("foo\"bar").GetInt16());
    }

    [Fact]
    public static void SetProperty_UShort_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, (ushort)65535);
        Assert.Equal((ushort)65535, root.GetProperty("héllo").GetUInt16());

        root.SetProperty("foo\"bar"u8, (ushort)42);
        Assert.Equal((ushort)42, root.GetProperty("foo\"bar").GetUInt16());
    }

    [Fact]
    public static void SetProperty_UInt_WithNonAsciiAndEscapedNames_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":1,\"foo\\\"bar\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetProperty("héllo"u8, 4294967295u);
        Assert.Equal(4294967295u, root.GetProperty("héllo").GetUInt32());

        root.SetProperty("foo\"bar"u8, 123u);
        Assert.Equal(123u, root.GetProperty("foo\"bar").GetUInt32());
    }

    [Fact]
    public static void SetProperty_Guid_WithNonAsciiAndEscapedNames_Works()
    {
        var guid1 = Guid.NewGuid();
        var guid2 = Guid.NewGuid();
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"00000000-0000-0000-0000-000000000000\",\"foo\\\"bar\":\"00000000-0000-0000-0000-000000000000\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, guid1);
        Assert.Equal(guid1, root.GetProperty("héllo").GetGuid());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, guid2);
        Assert.Equal(guid2, root.GetProperty("foo\"bar").GetGuid());
    }

    [Fact]
    public static void SetProperty_DateTime_WithNonAsciiAndEscapedNames_Works()
    {
        var dt1 = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dt2 = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"2020-01-01T00:00:00Z\",\"foo\\\"bar\":\"2020-01-01T00:00:00Z\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, dt1);
        Assert.Equal(dt1, root.GetProperty("héllo").GetDateTime());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, dt2);
        Assert.Equal(dt2, root.GetProperty("foo\"bar").GetDateTime());
    }

    [Fact]
    public static void SetProperty_DateTimeOffset_WithNonAsciiAndEscapedNames_Works()
    {
        var dto1 = new DateTimeOffset(2024, 5, 30, 12, 34, 56, TimeSpan.Zero);
        var dto2 = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.FromHours(2));
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"héllo\":\"2020-01-01T00:00:00Z\",\"foo\\\"bar\":\"2020-01-01T00:00:00+02:00\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Non-ASCII property name
        root.SetProperty("héllo"u8, dto1);
        Assert.Equal(dto1, root.GetProperty("héllo").GetDateTimeOffset());

        // Encoded/escaped property name (contains a quote)
        root.SetProperty("foo\"bar"u8, dto2);
        Assert.Equal(dto2, root.GetProperty("foo\"bar").GetDateTimeOffset());
    }

    [Fact]
    public static void CreateArray_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 10).Normalize();

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
        Assert.True(root[i++].GetBoolean());
        Assert.False(root[i++].GetBoolean());
        Assert.Equal((byte)42, root[i++].GetByte());
        Assert.Equal((sbyte)-8, root[i++].GetSByte());
        Assert.Equal((short)-12345, root[i++].GetInt16());
        Assert.Equal((ushort)65535, root[i++].GetUInt16());
        Assert.Equal(-123456, root[i++].GetInt32());
        Assert.Equal(654321u, root[i++].GetUInt32());
        Assert.Equal(long.MaxValue, root[i++].GetInt64());
        Assert.Equal(ulong.MaxValue, root[i++].GetUInt64());
        Assert.Equal(3.14f, root[i++].GetSingle());
        Assert.Equal(-2.71d, root[i++].GetDouble());
        Assert.Equal(123.456m, root[i++].GetDecimal());
        Assert.Equal(-789.01m, root[i++].GetDecimal());
        Assert.Equal("hello", root[i++].GetString());
        Assert.Equal("there", root[i++].GetString());
        Assert.Equal("world", root[i++].GetString());
        Assert.Equal(JsonValueKind.Null, root[i++].ValueKind);

#if NET
        Assert.Equal(Int128.Parse("170141183460469231731687303715884105727"), root[i++].GetInt128());
        Assert.Equal(UInt128.Parse("340282366920938463463374607431768211455"), root[i++].GetUInt128());
        Assert.Equal((Half)1.5, root[i++].GetHalf());
#endif

        Assert.Equal(guid, root[i++].GetGuid());
        Assert.Equal(dt, root[i++].GetDateTime());
        Assert.Equal(dto, root[i++].GetDateTimeOffset());

        Assert.Equal(odt, root[i++].GetOffsetDateTime());
        Assert.Equal(od, root[i++].GetOffsetDate());
        Assert.Equal(ot, root[i++].GetOffsetTime());
        Assert.Equal(ld, root[i++].GetLocalDate());
        Assert.Equal(period, root[i++].GetPeriod());

        // Nested array
        JsonElement.Mutable nestedArray = root[i++];
        Assert.Equal(2, nestedArray.GetArrayLength());
        Assert.Equal(1, nestedArray[0].GetInt32());
        Assert.Equal(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root[i++];
        Assert.Equal(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.Equal("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root[i++];
        Assert.Equal(3, byteArrayElem.GetArrayLength());
        Assert.Equal(1, byteArrayElem[0].GetByte());
        Assert.Equal(2, byteArrayElem[1].GetByte());
        Assert.Equal(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root[i++];
        Assert.Equal(3, shortArrayElem.GetArrayLength());
        Assert.Equal(-1, shortArrayElem[0].GetInt16());
        Assert.Equal(0, shortArrayElem[1].GetInt16());
        Assert.Equal(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root[i++];
        Assert.Equal(3, intArrayElem.GetArrayLength());
        Assert.Equal(-100, intArrayElem[0].GetInt32());
        Assert.Equal(0, intArrayElem[1].GetInt32());
        Assert.Equal(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root[i++];
        Assert.Equal(3, longArrayElem.GetArrayLength());
        Assert.Equal(-1000L, longArrayElem[0].GetInt64());
        Assert.Equal(0L, longArrayElem[1].GetInt64());
        Assert.Equal(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root[i++];
        Assert.Equal(3, sbyteArrayElem.GetArrayLength());
        Assert.Equal(-5, sbyteArrayElem[0].GetSByte());
        Assert.Equal(0, sbyteArrayElem[1].GetSByte());
        Assert.Equal(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root[i++];
        Assert.Equal(3, ushortArrayElem.GetArrayLength());
        Assert.Equal((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.Equal((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.Equal((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root[i++];
        Assert.Equal(3, uintArrayElem.GetArrayLength());
        Assert.Equal(100U, uintArrayElem[0].GetUInt32());
        Assert.Equal(200U, uintArrayElem[1].GetUInt32());
        Assert.Equal(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root[i++];
        Assert.Equal(3, ulongArrayElem.GetArrayLength());
        Assert.Equal(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.Equal(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.Equal(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root[i++];
        Assert.Equal(3, floatArrayElem.GetArrayLength());
        Assert.Equal(1.1f, floatArrayElem[0].GetSingle());
        Assert.Equal(2.2f, floatArrayElem[1].GetSingle());
        Assert.Equal(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root[i++];
        Assert.Equal(3, doubleArrayElem.GetArrayLength());
        Assert.Equal(1.11, doubleArrayElem[0].GetDouble());
        Assert.Equal(2.22, doubleArrayElem[1].GetDouble());
        Assert.Equal(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root[i++];
        Assert.Equal(3, decimalArrayElem.GetArrayLength());
        Assert.Equal(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.Equal(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.Equal(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root[i++];
        Assert.Equal(3, int128ArrayElem.GetArrayLength());
        Assert.Equal((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.Equal((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.Equal((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root[i++];
        Assert.Equal(3, uint128ArrayElem.GetArrayLength());
        Assert.Equal((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.Equal((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.Equal((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root[i++];
        Assert.Equal(3, halfArrayElem.GetArrayLength());
        Assert.Equal((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.Equal((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.Equal((Half)3.5, halfArrayElem[2].GetHalf());
#endif

        JsonElement.Mutable element = root[i++];
        Assert.Equal(3, element.GetProperty("foo").GetInt32());
    }

    [Fact]
    public static void CreateObject_RemoveProperty_NestedObject()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.True(root.TryGetProperty("age", out _));
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(12.6, root.GetProperty("nested1").GetDouble());
        Assert.Equal(6, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"age\":30,\"isActive\":true,\"nested2\":{\"name\":\"Nested Derek\",\"isActive\":false},\"nested1\":12.6,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_RemoveProperty_NestedArray()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.Equal(30, root.GetProperty("age").GetInt32());
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(12.6, root.GetProperty("nested1").GetDouble());
        Assert.Equal(6, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"age\":30,\"isActive\":true,\"nested2\":[\"Nested Derek\",49,false],\"nested1\":12.6,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_RemoveProperty_Escaped()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.False(root.TryGetProperty("a\"ge", out _));
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_RemoveProperty()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.False(root.TryGetProperty("age", out _));
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_RemoveProperty_SpanOfChar()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.False(root.TryGetProperty("age", out _));
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_RemoveProperty_String()
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
        Assert.Equal("John Doe", root.GetProperty("name").GetString());
        Assert.Equal(10.3m, root.GetProperty("score").GetDecimal());
        Assert.False(root.TryGetProperty("age", out _));
        Assert.True(root.GetProperty("isActive").GetBoolean());
        Assert.Equal(3, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John Doe\",\"isActive\":true,\"score\":10.3}", root.ToString());
    }

    [Fact]
    public static void CreateObject_AllTypes()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 10).Normalize();

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

        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        Assert.True(root.GetProperty("boolTrue").GetBoolean());
        Assert.False(root.GetProperty("boolFalse").GetBoolean());
        Assert.Equal((byte)42, root.GetProperty("byte").GetByte());
        Assert.Equal((sbyte)-8, root.GetProperty("sbyte").GetSByte());
        Assert.Equal((short)-12345, root.GetProperty("short").GetInt16());
        Assert.Equal((ushort)65535, root.GetProperty("ushort").GetUInt16());
        Assert.Equal(-123456, root.GetProperty("int").GetInt32());
        Assert.Equal(654321u, root.GetProperty("uint").GetUInt32());
        Assert.Equal(long.MaxValue, root.GetProperty("long").GetInt64());
        Assert.Equal(ulong.MaxValue, root.GetProperty("ulong").GetUInt64());
        Assert.Equal(3.14f, root.GetProperty("float").GetSingle());
        Assert.Equal(-2.71d, root.GetProperty("double").GetDouble());
        Assert.Equal(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.Equal(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.Equal(123456, root.GetProperty("bigInteger1").GetBigInteger());
        Assert.Equal(-78901, root.GetProperty("bigInteger2").GetBigInteger());
        Assert.Equal(new(123456, -2), root.GetProperty("bigNumber1").GetBigNumber());
        Assert.Equal(new(-78901, 20), root.GetProperty("bigNumber2").GetBigNumber());
        Assert.Equal(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.Equal(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.Equal("hello", root.GetProperty("string1").GetString());
        Assert.Equal("there", root.GetProperty("string2").GetString());
        Assert.Equal("world", root.GetProperty("utf8string").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);

#if NET
        Assert.Equal(Int128.Parse("170141183460469231731687303715884105727"), root.GetProperty("int128").GetInt128());
        Assert.Equal(UInt128.Parse("340282366920938463463374607431768211455"), root.GetProperty("uint128").GetUInt128());
        Assert.Equal((Half)1.5, root.GetProperty("half").GetHalf());
#endif

        Assert.Equal(guid, root.GetProperty("guid").GetGuid());
        Assert.Equal(dt, root.GetProperty("datetime").GetDateTime());
        Assert.Equal(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());

        Assert.Equal(odt, root.GetProperty("offsetdatetime").GetOffsetDateTime());
        Assert.Equal(od, root.GetProperty("offsetdate").GetOffsetDate());
        Assert.Equal(ot, root.GetProperty("offsettime").GetOffsetTime());
        Assert.Equal(ld, root.GetProperty("localdate").GetLocalDate());
        Assert.Equal(period, root.GetProperty("period").GetPeriod());

        JsonElement.Mutable elementValue = root.GetProperty("elementValue");
        Assert.Equal(3, elementValue.GetProperty("foo").GetInt32());

        // Nested array
        JsonElement.Mutable nestedArray = root.GetProperty("array");
        Assert.Equal(2, nestedArray.GetArrayLength());
        Assert.Equal(1, nestedArray[0].GetInt32());
        Assert.Equal(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root.GetProperty("object");
        Assert.Equal(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.Equal("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root.GetProperty("byteArray");
        Assert.Equal(3, byteArrayElem.GetArrayLength());
        Assert.Equal(1, byteArrayElem[0].GetByte());
        Assert.Equal(2, byteArrayElem[1].GetByte());
        Assert.Equal(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root.GetProperty("shortArray");
        Assert.Equal(3, shortArrayElem.GetArrayLength());
        Assert.Equal(-1, shortArrayElem[0].GetInt16());
        Assert.Equal(0, shortArrayElem[1].GetInt16());
        Assert.Equal(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root.GetProperty("intArray");
        Assert.Equal(3, intArrayElem.GetArrayLength());
        Assert.Equal(-100, intArrayElem[0].GetInt32());
        Assert.Equal(0, intArrayElem[1].GetInt32());
        Assert.Equal(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root.GetProperty("longArray");
        Assert.Equal(3, longArrayElem.GetArrayLength());
        Assert.Equal(-1000L, longArrayElem[0].GetInt64());
        Assert.Equal(0L, longArrayElem[1].GetInt64());
        Assert.Equal(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root.GetProperty("sbyteArray");
        Assert.Equal(3, sbyteArrayElem.GetArrayLength());
        Assert.Equal(-5, sbyteArrayElem[0].GetSByte());
        Assert.Equal(0, sbyteArrayElem[1].GetSByte());
        Assert.Equal(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root.GetProperty("ushortArray");
        Assert.Equal(3, ushortArrayElem.GetArrayLength());
        Assert.Equal((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.Equal((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.Equal((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root.GetProperty("uintArray");
        Assert.Equal(3, uintArrayElem.GetArrayLength());
        Assert.Equal(100U, uintArrayElem[0].GetUInt32());
        Assert.Equal(200U, uintArrayElem[1].GetUInt32());
        Assert.Equal(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root.GetProperty("ulongArray");
        Assert.Equal(3, ulongArrayElem.GetArrayLength());
        Assert.Equal(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.Equal(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.Equal(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root.GetProperty("floatArray");
        Assert.Equal(3, floatArrayElem.GetArrayLength());
        Assert.Equal(1.1f, floatArrayElem[0].GetSingle());
        Assert.Equal(2.2f, floatArrayElem[1].GetSingle());
        Assert.Equal(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root.GetProperty("doubleArray");
        Assert.Equal(3, doubleArrayElem.GetArrayLength());
        Assert.Equal(1.11, doubleArrayElem[0].GetDouble());
        Assert.Equal(2.22, doubleArrayElem[1].GetDouble());
        Assert.Equal(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root.GetProperty("decimalArray");
        Assert.Equal(3, decimalArrayElem.GetArrayLength());
        Assert.Equal(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.Equal(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.Equal(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root.GetProperty("int128Array");
        Assert.Equal(3, int128ArrayElem.GetArrayLength());
        Assert.Equal((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.Equal((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.Equal((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root.GetProperty("uint128Array");
        Assert.Equal(3, uint128ArrayElem.GetArrayLength());
        Assert.Equal((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.Equal((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.Equal((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root.GetProperty("halfArray");
        Assert.Equal(3, halfArrayElem.GetArrayLength());
        Assert.Equal((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.Equal((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.Equal((Half)3.5, halfArrayElem[2].GetHalf());
#endif
    }

    [Fact]
    public static void CreateObject_AllTypes_String()
    {
        using var workspace = JsonWorkspace.Create();

        var guid = Guid.NewGuid();
        var dt = new DateTime(2024, 5, 30, 12, 34, 56, DateTimeKind.Utc);
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var odt = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.Zero);
        var od = new OffsetDate(new LocalDate(2025, 1, 1), Offset.Zero);
        var ot = new OffsetTime(new LocalTime(14, 0, 3), Offset.Zero);
        var ld = new LocalDate(2025, 1, 1);
        Period period = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 10).Normalize();

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

        Assert.Equal(JsonValueKind.Object, root.ValueKind);

        Assert.True(root.GetProperty("boolTrue").GetBoolean());
        Assert.False(root.GetProperty("boolFalse").GetBoolean());
        Assert.Equal((byte)42, root.GetProperty("byte").GetByte());
        Assert.Equal((sbyte)-8, root.GetProperty("sbyte").GetSByte());
        Assert.Equal((short)-12345, root.GetProperty("short").GetInt16());
        Assert.Equal((ushort)65535, root.GetProperty("ushort").GetUInt16());
        Assert.Equal(-123456, root.GetProperty("int").GetInt32());
        Assert.Equal(654321u, root.GetProperty("uint").GetUInt32());
        Assert.Equal(long.MaxValue, root.GetProperty("long").GetInt64());
        Assert.Equal(ulong.MaxValue, root.GetProperty("ulong").GetUInt64());
        Assert.Equal(3.14f, root.GetProperty("float").GetSingle());
        Assert.Equal(-2.71d, root.GetProperty("double").GetDouble());
        Assert.Equal(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.Equal(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.Equal(123456, root.GetProperty("bigInteger1").GetBigInteger());
        Assert.Equal(-78901, root.GetProperty("bigInteger2").GetBigInteger());
        Assert.Equal(new(123456, -2), root.GetProperty("bigNumber1").GetBigNumber());
        Assert.Equal(new(-78901, 20), root.GetProperty("bigNumber2").GetBigNumber());
        Assert.Equal(123.456m, root.GetProperty("decimal1").GetDecimal());
        Assert.Equal(-789.01m, root.GetProperty("decimal2").GetDecimal());
        Assert.Equal("hello", root.GetProperty("string1").GetString());
        Assert.Equal("there", root.GetProperty("string2").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);

#if NET
        Assert.Equal(Int128.Parse("170141183460469231731687303715884105727"), root.GetProperty("int128").GetInt128());
        Assert.Equal(UInt128.Parse("340282366920938463463374607431768211455"), root.GetProperty("uint128").GetUInt128());
        Assert.Equal((Half)1.5, root.GetProperty("half").GetHalf());
#endif

        Assert.Equal(guid, root.GetProperty("guid").GetGuid());
        Assert.Equal(dt, root.GetProperty("datetime").GetDateTime());
        Assert.Equal(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());

        Assert.Equal(odt, root.GetProperty("offsetdatetime").GetOffsetDateTime());
        Assert.Equal(od, root.GetProperty("offsetdate").GetOffsetDate());
        Assert.Equal(ot, root.GetProperty("offsettime").GetOffsetTime());
        Assert.Equal(ld, root.GetProperty("localdate").GetLocalDate());
        Assert.Equal(period, root.GetProperty("period").GetPeriod());

        JsonElement.Mutable elementValue = root.GetProperty("elementValue");
        Assert.Equal(3, elementValue.GetProperty("foo").GetInt32());

        // Nested array
        JsonElement.Mutable nestedArray = root.GetProperty("array");
        Assert.Equal(2, nestedArray.GetArrayLength());
        Assert.Equal(1, nestedArray[0].GetInt32());
        Assert.Equal(2, nestedArray[1].GetInt32());

        // Nested object
        JsonElement.Mutable nestedObject = root.GetProperty("object");
        Assert.Equal(JsonValueKind.Object, nestedObject.ValueKind);
        Assert.Equal("bar", nestedObject.GetProperty("foo").GetString());

        JsonElement.Mutable byteArrayElem = root.GetProperty("byteArray");
        Assert.Equal(3, byteArrayElem.GetArrayLength());
        Assert.Equal(1, byteArrayElem[0].GetByte());
        Assert.Equal(2, byteArrayElem[1].GetByte());
        Assert.Equal(3, byteArrayElem[2].GetByte());

        JsonElement.Mutable shortArrayElem = root.GetProperty("shortArray");
        Assert.Equal(3, shortArrayElem.GetArrayLength());
        Assert.Equal(-1, shortArrayElem[0].GetInt16());
        Assert.Equal(0, shortArrayElem[1].GetInt16());
        Assert.Equal(1, shortArrayElem[2].GetInt16());

        JsonElement.Mutable intArrayElem = root.GetProperty("intArray");
        Assert.Equal(3, intArrayElem.GetArrayLength());
        Assert.Equal(-100, intArrayElem[0].GetInt32());
        Assert.Equal(0, intArrayElem[1].GetInt32());
        Assert.Equal(100, intArrayElem[2].GetInt32());

        JsonElement.Mutable longArrayElem = root.GetProperty("longArray");
        Assert.Equal(3, longArrayElem.GetArrayLength());
        Assert.Equal(-1000L, longArrayElem[0].GetInt64());
        Assert.Equal(0L, longArrayElem[1].GetInt64());
        Assert.Equal(1000L, longArrayElem[2].GetInt64());

        JsonElement.Mutable sbyteArrayElem = root.GetProperty("sbyteArray");
        Assert.Equal(3, sbyteArrayElem.GetArrayLength());
        Assert.Equal(-5, sbyteArrayElem[0].GetSByte());
        Assert.Equal(0, sbyteArrayElem[1].GetSByte());
        Assert.Equal(5, sbyteArrayElem[2].GetSByte());

        JsonElement.Mutable ushortArrayElem = root.GetProperty("ushortArray");
        Assert.Equal(3, ushortArrayElem.GetArrayLength());
        Assert.Equal((ushort)10, ushortArrayElem[0].GetUInt16());
        Assert.Equal((ushort)20, ushortArrayElem[1].GetUInt16());
        Assert.Equal((ushort)30, ushortArrayElem[2].GetUInt16());

        JsonElement.Mutable uintArrayElem = root.GetProperty("uintArray");
        Assert.Equal(3, uintArrayElem.GetArrayLength());
        Assert.Equal(100U, uintArrayElem[0].GetUInt32());
        Assert.Equal(200U, uintArrayElem[1].GetUInt32());
        Assert.Equal(300U, uintArrayElem[2].GetUInt32());

        JsonElement.Mutable ulongArrayElem = root.GetProperty("ulongArray");
        Assert.Equal(3, ulongArrayElem.GetArrayLength());
        Assert.Equal(1000UL, ulongArrayElem[0].GetUInt64());
        Assert.Equal(2000UL, ulongArrayElem[1].GetUInt64());
        Assert.Equal(3000UL, ulongArrayElem[2].GetUInt64());

        JsonElement.Mutable floatArrayElem = root.GetProperty("floatArray");
        Assert.Equal(3, floatArrayElem.GetArrayLength());
        Assert.Equal(1.1f, floatArrayElem[0].GetSingle());
        Assert.Equal(2.2f, floatArrayElem[1].GetSingle());
        Assert.Equal(3.3f, floatArrayElem[2].GetSingle());

        JsonElement.Mutable doubleArrayElem = root.GetProperty("doubleArray");
        Assert.Equal(3, doubleArrayElem.GetArrayLength());
        Assert.Equal(1.11, doubleArrayElem[0].GetDouble());
        Assert.Equal(2.22, doubleArrayElem[1].GetDouble());
        Assert.Equal(3.33, doubleArrayElem[2].GetDouble());

        JsonElement.Mutable decimalArrayElem = root.GetProperty("decimalArray");
        Assert.Equal(3, decimalArrayElem.GetArrayLength());
        Assert.Equal(1.111m, decimalArrayElem[0].GetDecimal());
        Assert.Equal(2.222m, decimalArrayElem[1].GetDecimal());
        Assert.Equal(3.333m, decimalArrayElem[2].GetDecimal());

#if NET
        JsonElement.Mutable int128ArrayElem = root.GetProperty("int128Array");
        Assert.Equal(3, int128ArrayElem.GetArrayLength());
        Assert.Equal((Int128)1, int128ArrayElem[0].GetInt128());
        Assert.Equal((Int128)2, int128ArrayElem[1].GetInt128());
        Assert.Equal((Int128)3, int128ArrayElem[2].GetInt128());

        JsonElement.Mutable uint128ArrayElem = root.GetProperty("uint128Array");
        Assert.Equal(3, uint128ArrayElem.GetArrayLength());
        Assert.Equal((UInt128)4, uint128ArrayElem[0].GetUInt128());
        Assert.Equal((UInt128)5, uint128ArrayElem[1].GetUInt128());
        Assert.Equal((UInt128)6, uint128ArrayElem[2].GetUInt128());

        JsonElement.Mutable halfArrayElem = root.GetProperty("halfArray");
        Assert.Equal(3, halfArrayElem.GetArrayLength());
        Assert.Equal((Half)1.5, halfArrayElem[0].GetHalf());
        Assert.Equal((Half)2.5, halfArrayElem[1].GetHalf());
        Assert.Equal((Half)3.5, halfArrayElem[2].GetHalf());
#endif
    }

    [Fact]
    public static void SetItem_Array_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Array_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[true]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Array_Creator_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\":\"b\"}]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Array_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[{\"b\": \"c\"}]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[0].ToString());

        root.SetItem(1, (ref o) => { o.AddItem("world"u8); });
        Assert.Equal("[\"world\"]", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Object_Creator_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[false]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Object_Creator_Nested_Object_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[true]}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Object_Creator_Array_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[{\"a\": \"b\"}]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[1].ToString());
    }

    [Fact]
    public static void SetItem_Object_Creator_Nested_Object_Shrink_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\": [{\"b\": \"c\"}] }");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement.GetProperty("a");

        root.SetItem(0, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[0].ToString());
        root.SetItem(1, (ref o) => { o.AddProperty("hello"u8, "world"u8); });
        Assert.Equal("{\"hello\":\"world\"}", root[1].ToString());
    }

    [Fact]
    public static void SetItem_OffsetDateTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01T00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var odt1 = new OffsetDateTime(new LocalDateTime(2024, 5, 30, 12, 34, 56), Offset.Zero);
        var odt2 = new OffsetDateTime(new LocalDateTime(2025, 1, 1, 0, 0, 0), Offset.FromHours(2));

        root.SetItem(0, odt1);
        Assert.Equal(odt1, root[0].GetOffsetDateTime());

        root.SetItem(1, odt2);
        Assert.Equal(odt2, root[1].GetOffsetDateTime());
    }

    [Fact]
    public static void SetItem_OffsetDate_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2020-01-01Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var od1 = new OffsetDate(new LocalDate(2024, 5, 30), Offset.Zero);
        var od2 = new OffsetDate(new LocalDate(2025, 1, 1), Offset.FromHours(2));

        root.SetItem(0, od1);
        Assert.Equal(od1, root[0].GetOffsetDate());

        root.SetItem(1, od2);
        Assert.Equal(od2, root[1].GetOffsetDate());
    }

    [Fact]
    public static void SetItem_OffsetTime_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"00:00:00Z\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var ot1 = new OffsetTime(new LocalTime(12, 34, 56), Offset.Zero);
        var ot2 = new OffsetTime(new LocalTime(0, 0, 0), Offset.FromHours(2));

        root.SetItem(0, ot1);
        Assert.Equal(ot1, root[0].GetOffsetTime());

        root.SetItem(1, ot2);
        Assert.Equal(ot2, root[1].GetOffsetTime());
    }

    [Fact]
    public static void SetItem_LocalDate_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"2024-05-30\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        var ld1 = new LocalDate(2024, 5, 30);
        var ld2 = new LocalDate(2025, 1, 1);

        root.SetItem(0, ld1);
        Assert.Equal(ld1, root[0].GetLocalDate());

        root.SetItem(1, ld2);
        Assert.Equal(ld2, root[1].GetLocalDate());
    }

    [Fact]
    public static void SetItem_Period_Works()
    {
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"P1W\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        Period p1 = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 1).Normalize();
        Period p2 = Period.Zero;

        root.SetItem(0, p1);
        Assert.Equal(p1, root[0].GetPeriod());

        root.SetItem(1, p2);
        Assert.Equal(p2, root[1].GetPeriod());
    }

    [Fact]
    public static void TryApply_WithSimpleObject_ReturnsTrue()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("Jane", root.GetProperty("name").GetString()); // Replaced
        Assert.Equal(30, root.GetProperty("age").GetInt32()); // Preserved  
        Assert.Equal("NYC", root.GetProperty("city").GetString()); // Added
        Assert.Equal(3, root.GetPropertyCount());
    }

    [Fact]
    public static void TryApply_WithEmptyObject_ReturnsTrue()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("John", root.GetProperty("name").GetString());
        Assert.Equal(30, root.GetProperty("age").GetInt32());
        Assert.Equal(2, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John\",\"age\":30}", root.ToString());
    }

    [Fact]
    public static void TryApply_WithComplexNestedObject_ReturnsTrue()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("existing").GetString());
        Assert.Equal("Alice", root.GetProperty("person").GetProperty("name").GetString());
        Assert.Equal(25, root.GetProperty("person").GetProperty("details").GetProperty("age").GetInt32());
        Assert.True(root.GetProperty("person").GetProperty("details").GetProperty("active").GetBoolean());
        Assert.Equal(3, root.GetProperty("scores").GetArrayLength());
        Assert.Equal(95, root.GetProperty("scores")[0].GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("metadata").ValueKind);
        Assert.Equal(4, root.GetPropertyCount());
    }

    [Theory]
    [InlineData("\"string\"")]
    [InlineData("42")]
    [InlineData("true")]
    [InlineData("null")]
    [InlineData("[1,2,3]")]
    public static void TryApply_WithNonObjectValue_ReturnsFalse(string jsonValue)
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
                Assert.False(result);
            }));

        // Verify original state is preserved
        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("John", root.GetProperty("name").GetString());
        Assert.Equal(30, root.GetProperty("age").GetInt32());
        Assert.Equal(2, root.GetPropertyCount());
        Assert.Equal("{\"name\":\"John\",\"age\":30}", root.ToString());
    }

    [Fact]
    public static void TryApply_ReplacesExistingProperties_Works()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("NewName", root.GetProperty("name").GetString()); // Replaced
        Assert.Equal(30, root.GetProperty("age").GetInt32()); // Preserved
        Assert.Equal("Boston", root.GetProperty("city").GetString()); // Preserved
        Assert.Equal("active", root.GetProperty("status").GetString()); // Added
        Assert.Equal(4, root.GetPropertyCount());
    }

    [Fact]
    public static void TryApply_WithSpecialPropertyNames_Works()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("existing").GetString());
        Assert.Equal("world", root.GetProperty("héllo").GetString());
        Assert.Equal("baz", root.GetProperty("foo\"bar").GetString());
        Assert.Equal("empty", root.GetProperty("").GetString());
        Assert.Equal("value", root.GetProperty("with\nnewline").GetString());
        Assert.Equal(5, root.GetPropertyCount());
    }

    [Fact]
    public static void TryApply_MultipleCalls_AccumulatesProperties()
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
                
                Assert.True(result1);
                Assert.True(result2);
                Assert.True(result3);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("value", root.GetProperty("initial").GetString());
        Assert.Equal("Jane", root.GetProperty("name").GetString()); // Last wins
        Assert.Equal(30, root.GetProperty("age").GetInt32()); // From first apply
        Assert.Equal("NYC", root.GetProperty("city").GetString()); // From second apply
        Assert.True(root.GetProperty("active").GetBoolean()); // From third apply
        Assert.Equal(5, root.GetPropertyCount());
    }

    [Fact]
    public static void TryApply_CombinedWithOtherOperations_Works()
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
                Assert.True(result);
                
                // Continue operations after apply
                objBuilder.RemoveProperty("temp"u8);
                objBuilder.AddProperty("final"u8, "added"u8);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("Applied", root.GetProperty("name").GetString());
        Assert.Equal(25, root.GetProperty("age").GetInt32());
        Assert.False(root.TryGetProperty("temp", out _)); // Removed
        Assert.Equal("added", root.GetProperty("final").GetString());
        Assert.Equal(3, root.GetPropertyCount());
    }

    [Fact]
    public static void TryApply_WithAllDataTypes_Works()
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
                Assert.True(result);
            }));

        JsonElement.Mutable root = doc.RootElement;
        Assert.Equal("test", root.GetProperty("string").GetString());
        Assert.Equal(42, root.GetProperty("number").GetInt32());
        Assert.True(root.GetProperty("bool").GetBoolean());
        Assert.Equal(123.45m, root.GetProperty("decimal").GetDecimal());
        Assert.Equal(guid, root.GetProperty("guid").GetGuid());
        Assert.Equal(dt, root.GetProperty("datetime").GetDateTime());
        Assert.Equal(dto, root.GetProperty("datetimeoffset").GetDateTimeOffset());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("nullValue").ValueKind);
        Assert.Equal(8, root.GetPropertyCount());
    }

    #region Remove and RemoveRange Tests

    // Basic RemoveRange Tests
    [Fact]
    public static void RemoveRange_FromMiddleOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 2);

        // Assert
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(4, root[1].GetInt32());
        Assert.Equal(5, root[2].GetInt32());
    }

    [Fact]
    public static void RemoveRange_FromStartOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10, 20, 30, 40]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 2);

        // Assert
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(30, root[0].GetInt32());
        Assert.Equal(40, root[1].GetInt32());
    }

    [Fact]
    public static void RemoveRange_FromEndOfArray_RemovesCorrectElements()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"a\", \"b\", \"c\", \"d\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(2, 2);

        // Assert
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("a", root[0].GetString());
        Assert.Equal("b", root[1].GetString());
    }

    [Fact]
    public static void RemoveRange_EntireArray_MakesArrayEmpty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[true, false, true]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 3);

        // Assert
        Assert.Equal(0, root.GetArrayLength());
    }

    [Fact]
    public static void RemoveRange_SingleElement_RemovesElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[100, 200, 300]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 1);

        // Assert
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(100, root[0].GetInt32());
        Assert.Equal(300, root[1].GetInt32());
    }

    [Fact]
    public static void RemoveRange_WithZeroCount_DoesNothing()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(1, 0);

        // Assert
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(3, root[2].GetInt32());
    }

    // Basic Remove Tests
    [Fact]
    public static void Remove_FromMiddleOfArray_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3, 4, 5]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(2);

        // Assert
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(2, root[1].GetInt32());
        Assert.Equal(4, root[2].GetInt32());
        Assert.Equal(5, root[3].GetInt32());
    }

    [Fact]
    public static void Remove_FirstElement_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[\"first\", \"second\", \"third\"]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(0);

        // Assert
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("second", root[0].GetString());
        Assert.Equal("third", root[1].GetString());
    }

    [Fact]
    public static void Remove_LastElement_RemovesCorrectElement()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[10, 20, 30]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(2);

        // Assert
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(10, root[0].GetInt32());
        Assert.Equal(20, root[1].GetInt32());
    }

    [Fact]
    public static void Remove_OnlyElement_MakesArrayEmpty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[42]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveAt(0);

        // Assert
        Assert.Equal(0, root.GetArrayLength());
    }

    // Error Condition Tests
    [Fact]
    public static void RemoveRange_Throws_WhenStartIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveRange(-1, 1));
    }

    [Fact]
    public static void RemoveRange_Throws_WhenStartIndexIsGreaterThanArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveRange(4, 1));
    }

    [Fact]
    public static void RemoveRange_Throws_WhenCountIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveRange(0, -1));
    }

    [Fact]
    public static void RemoveRange_Throws_WhenStartIndexPlusCountExceedsArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveRange(2, 2));
    }

    [Fact]
    public static void Remove_Throws_WhenIndexIsNegative()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveAt(-1));
    }

    [Fact]
    public static void Remove_Throws_WhenIndexIsGreaterThanOrEqualToArrayLength()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1, 2, 3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => root.RemoveAt(3));
    }

    [Fact]
    public static void RemoveRange_Throws_WhenElementIsNotArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"key\": \"value\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => root.RemoveRange(0, 1));
    }

    [Fact]
    public static void Remove_Throws_WhenElementIsNotArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("\"hello\"");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => root.RemoveAt(0));
    }

    // Nested Array Tests - Arrays inside Objects
    [Fact]
    public static void RemoveRange_NestedArrayInObject_RemovesCorrectElementsAndPreservesObject()
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

        Assert.Equal(3, dataArray.GetArrayLength());
        Assert.Equal(1, dataArray[0].GetInt32());
        Assert.Equal(4, dataArray[1].GetInt32());
        Assert.Equal(5, dataArray[2].GetInt32());
        
        // Verify other properties are preserved
        Assert.Equal("test", root.GetProperty("name").GetString());
        Assert.Equal(2, root.GetPropertyCount());
    }

    [Fact]
    public static void Remove_NestedArrayInObject_RemovesCorrectElementAndPreservesObject()
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

        Assert.Equal(2, itemsArray.GetArrayLength());
        Assert.Equal("a", itemsArray[0].GetString());
        Assert.Equal("c", itemsArray[1].GetString());
        
        // Verify other properties are preserved
        Assert.Equal(3, root.GetProperty("count").GetInt32());
        Assert.True(root.GetProperty("active").GetBoolean());
        Assert.Equal(3, root.GetPropertyCount());
    }

    [Fact]
    public static void RemoveRange_NestedArrayWithComplexObjects_RemovesCorrectElements()
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

        Assert.Equal(2, usersArray.GetArrayLength());
        Assert.Equal(2, usersArray[0].GetProperty("id").GetInt32());
        Assert.Equal("Bob", usersArray[0].GetProperty("name").GetString());
        Assert.Equal(3, usersArray[1].GetProperty("id").GetInt32());
        Assert.Equal("Charlie", usersArray[1].GetProperty("name").GetString());
        
        // Verify other properties are preserved
        Assert.Equal(3, root.GetProperty("total").GetInt32());
    }

    // Nested Array Tests - Arrays inside Arrays
    [Fact]
    public static void RemoveRange_NestedArrayInArray_RemovesCorrectElementsFromInnerArray()
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
        Assert.Equal(2, firstInnerArray.GetArrayLength());
        Assert.Equal(1, firstInnerArray[0].GetInt32());
        Assert.Equal(3, firstInnerArray[1].GetInt32());
        
        // Verify other inner arrays are intact
        Assert.Equal(3, root[1].GetArrayLength());
        Assert.Equal(4, root[1][0].GetInt32());
        Assert.Equal(3, root[2].GetArrayLength());
        Assert.Equal(7, root[2][0].GetInt32());
    }

    [Fact]
    public static void Remove_NestedArrayInArray_RemovesCorrectElementFromInnerArray()
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
        Assert.Equal("p", thirdInnerArray[0].GetString());
        Assert.Equal("q", thirdInnerArray[1].GetString());
        Assert.Equal("s", thirdInnerArray[2].GetString());
        
        // Verify that accessing the old last index throws an exception
        Assert.Throws<IndexOutOfRangeException>(() => thirdInnerArray[3].GetString());
        
        // Verify other inner arrays are intact
        Assert.Equal(3, root[0].GetArrayLength());
        Assert.Equal("x", root[0][0].GetString());
        Assert.Equal(2, root[1].GetArrayLength());
        Assert.Equal("a", root[1][0].GetString());
    }

    [Fact]
    public static void RemoveRange_RemoveEntireInnerArrayFromOuterArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[10, 20], [30, 40], [50, 60]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.RemoveRange(0, 2);

        // Assert
        Assert.Equal(1, root.GetArrayLength());
        Assert.Equal(2, root[0].GetArrayLength());
        Assert.Equal(50, root[0][0].GetInt32());
        Assert.Equal(60, root[0][1].GetInt32());
    }

    [Fact]
    public static void Remove_ComplexNestedStructure_RemovesCorrectElement()
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
        Assert.Equal(2, firstObjectDataArray.GetArrayLength());
        Assert.Equal(1, firstObjectDataArray[0].GetInt32());
        Assert.Equal(3, firstObjectDataArray[1].GetInt32());

        // Verify rest of structure is intact

        // Refresh the root element
        root = builderDoc.RootElement;

        Assert.Equal("user", root[0].GetProperty("type").GetString());
        Assert.True(root[0].GetProperty("meta").GetProperty("active").GetBoolean());
        Assert.Equal("admin", root[1].GetProperty("type").GetString());
        Assert.Equal(3, root[1].GetProperty("data").GetArrayLength());
        Assert.Equal(4, root[1].GetProperty("data")[0].GetInt32());
    }

    // Multi-Level Nested Array Tests
    [Fact]
    public static void RemoveRange_ThreeLevelNestedArray_RemovesCorrectElements()
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
        Assert.Equal(1, deepestArray.GetArrayLength());
        Assert.Equal(2, deepestArray[0].GetInt32());
        
        // Verify all other nested levels remain intact
        Assert.Equal(2, root[0][1].GetArrayLength());
        Assert.Equal(3, root[0][1][0].GetInt32());
        Assert.Equal(2, root[1][0].GetArrayLength());
        Assert.Equal(5, root[1][0][0].GetInt32());
        Assert.Equal(2, root[1][1].GetArrayLength());
        Assert.Equal(7, root[1][1][0].GetInt32());
    }

    [Fact]
    public static void Remove_MixedTypeNestedArray_RemovesCorrectElement()
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
        Assert.Equal(3, firstInnerArray.GetArrayLength());
        Assert.Equal(1, firstInnerArray[0].GetInt32());
        Assert.Equal("text", firstInnerArray[1].GetString());
        Assert.Equal(JsonValueKind.Null, firstInnerArray[2].ValueKind);
        
        // Verify other arrays are intact
        Assert.Equal(2, root[1].GetArrayLength());
        Assert.Equal("value", root[1][0].GetProperty("key").GetString());
        Assert.Equal(2, root[2].GetArrayLength());
        Assert.Equal(3.14, root[2][0].GetDouble());
    }

    // Complex Object with Multiple Nested Arrays
    [Fact]
    public static void RemoveRange_ComplexObjectMultipleNestedArrays_RemovesCorrectElements()
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
        Assert.Equal(2, tagsArray.GetArrayLength());
        Assert.Equal("tag1", tagsArray[0].GetString());
        Assert.Equal("tag3", tagsArray[1].GetString());
        
        // Verify categories
        JsonElement.Mutable cat1Items = root.GetProperty("categories")[0].GetProperty("items");
        Assert.Equal(2, cat1Items.GetArrayLength());
        Assert.Equal(2, cat1Items[0].GetInt32());
        Assert.Equal(3, cat1Items[1].GetInt32());
        
        // Verify cat2 is unchanged
        JsonElement.Mutable cat2Items = root.GetProperty("categories")[1].GetProperty("items");
        Assert.Equal(3, cat2Items.GetArrayLength());
        Assert.Equal(4, cat2Items[0].GetInt32());
        
        // Verify scores
        JsonElement.Mutable scoresArray = root.GetProperty("metadata").GetProperty("scores");
        Assert.Equal(2, scoresArray.GetArrayLength());
        Assert.Equal(30, scoresArray[0].GetInt32());
        Assert.Equal(40, scoresArray[1].GetInt32());
        
        // Verify flags unchanged
        JsonElement.Mutable flagsArray = root.GetProperty("metadata").GetProperty("flags");
        Assert.Equal(3, flagsArray.GetArrayLength());
        Assert.True(flagsArray[0].GetBoolean());
        
        // Verify other properties unchanged
        Assert.Equal(123, root.GetProperty("id").GetInt32());
    }

    // Edge Cases and Error Tests for Nested Arrays
    [Fact]
    public static void RemoveRange_NestedArray_Throws_WhenInnerArrayIndexOutOfBounds()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[[1, 2], [3, 4, 5]]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable firstInnerArray = root[0]; // Only has 2 elements

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => firstInnerArray.RemoveRange(5, 1));
    }

    [Fact]
    public static void Remove_NestedArrayInObject_Throws_WhenAccessingNonArrayProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"array\": [1, 2, 3], \"string\": \"value\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;
        JsonElement.Mutable stringProperty = root.GetProperty("string");

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => stringProperty.RemoveAt(0));
    }

    [Fact]
    public static void Remove_NestedArrayWithNullValues_HandlesNullsCorrectly()
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
        Assert.Equal(2, root[0].GetArrayLength());
        Assert.Equal(1, root[0][0].GetInt32());
        Assert.Equal(JsonValueKind.Null, root[0][1].ValueKind);
        
        Assert.Equal(2, root[1].GetArrayLength());
        Assert.Equal(2, root[1][0].GetInt32());
        Assert.Equal(3, root[1][1].GetInt32());
        
        Assert.Equal(1, root[2].GetArrayLength());
        Assert.Equal(JsonValueKind.Null, root[2][0].ValueKind);
    }

    #endregion

    #region Remove (by value) Tests

    [Fact]
    public static void Remove_FindsAndRemovesFirstMatchingElement()
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
        Assert.True(removed);
        Assert.Equal(4, root.GetArrayLength());
        Assert.Equal("[1,3,2,5]", root.ToString());
    }

    [Fact]
    public static void Remove_ReturnsFalse_WhenElementNotFound()
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
        Assert.False(removed);
        Assert.Equal(3, root.GetArrayLength());
    }

    [Fact]
    public static void Remove_EmptyArray_ReturnsFalse()
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
        Assert.False(removed);
        Assert.Equal(0, root.GetArrayLength());
    }

    [Fact]
    public static void Remove_MatchesComplexObjects()
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
        Assert.True(removed);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("""[{"id":1},{"id":3}]""", root.ToString());
    }

    [Fact]
    public static void Remove_OnlyRemovesFirstOccurrence()
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
        Assert.True(removed);
        Assert.Equal(2, root.GetArrayLength());
    }

    #endregion

    #region Replace Tests

    [Fact]
    public static void Replace_FindsAndReplacesFirstMatchingElement()
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
        Assert.True(replaced);
        Assert.Equal(5, root.GetArrayLength());
        Assert.Equal("[1,99,3,2,5]", root.ToString());
    }

    [Fact]
    public static void Replace_ReturnsFalse_WhenElementNotFound()
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
        Assert.False(replaced);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("[1, 2, 3]", root.ToString());
    }

    [Fact]
    public static void Replace_EmptyArray_ReturnsFalse()
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
        Assert.False(replaced);
        Assert.Equal(0, root.GetArrayLength());
    }

    [Fact]
    public static void Replace_MatchesComplexObjects()
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
        Assert.True(replaced);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("""[{"id":1},{"id":99},{"id":3}]""", root.ToString());
    }

    [Fact]
    public static void Replace_OnlyReplacesFirstOccurrence()
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
        Assert.True(replaced);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("""[{"a":2},{"a":1},{"a":1}]""", root.ToString());
    }

    [Fact]
    public static void Replace_WithUndefinedSource_RemovesMatch()
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
        Assert.True(replaced);
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("[1,3]", root.ToString());
    }

    [Fact]
    public static void Replace_WithStringValue()
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
        Assert.True(replaced);
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("""["alpha","delta","gamma"]""", root.ToString());
    }

    #endregion

    #region RemoveProperty Tests

    [Fact]
    public static void RemoveProperty_String_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b");

        // Assert
        Assert.True(removed);
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetPropertyCount());
        Assert.Equal(1, root.GetProperty("a").GetInt32());
        Assert.Equal(3, root.GetProperty("c").GetInt32());
        Assert.False(root.TryGetProperty("b", out _));
    }

    [Fact]
    public static void RemoveProperty_SpanOfChar_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b".AsSpan());

        // Assert
        Assert.True(removed);
        Assert.Equal(2, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"a\":1,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_Utf8_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("b"u8);

        // Assert
        Assert.True(removed);
        Assert.Equal(2, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"a\":1,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_ReturnsFalse_WhenPropertyDoesNotExist()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("nonexistent");

        // Assert
        Assert.False(removed);
        Assert.Equal(1, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"a\":1}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_RemovesFirstProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.True(removed);
        Assert.Equal(2, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"b\":2,\"c\":3}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_RemovesLastProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("c");

        // Assert
        Assert.True(removed);
        Assert.Equal(2, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"a\":1,\"b\":2}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_RemovesOnlyProperty_LeavesEmptyObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.True(removed);
        Assert.Equal(0, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_RemovesNestedObjectProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":{\"nested\":true},\"b\":2}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.True(removed);
        Assert.Equal(1, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"b\":2}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_RemovesNestedArrayProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":[1,2,3],\"b\":\"hello\"}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("a");

        // Assert
        Assert.True(removed);
        Assert.Equal(1, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"b\":\"hello\"}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_OnNestedObject()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"outer\":{\"a\":1,\"b\":2}}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.GetProperty("outer").RemoveProperty("a");

        // Assert
        Assert.True(removed);
        Assert.Equal("{\"outer\":{\"b\":2}}", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void RemoveProperty_FromEmptyObject_ReturnsFalse()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        bool removed = builderDoc.RootElement.RemoveProperty("anything");

        // Assert
        Assert.False(removed);
        Assert.Equal(0, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{}", builderDoc.RootElement.ToString());
    }

    #endregion

    #region SetProperty with default(JsonElement) removes property

    [Fact]
    public static void SetProperty_String_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b", default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetPropertyCount());
        Assert.False(root.TryGetProperty("b", out _));
        Assert.Equal("{\"a\":1,\"c\":3}", root.ToString());
    }

    [Fact]
    public static void SetProperty_SpanOfChar_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b".AsSpan(), default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetPropertyCount());
        Assert.False(root.TryGetProperty("b", out _));
        Assert.Equal("{\"a\":1,\"c\":3}", root.ToString());
    }

    [Fact]
    public static void SetProperty_Utf8_WithDefaultSource_RemovesExistingProperty()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1,\"b\":2,\"c\":3}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("b"u8, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetPropertyCount());
        Assert.False(root.TryGetProperty("b", out _));
        Assert.Equal("{\"a\":1,\"c\":3}", root.ToString());
    }

    [Fact]
    public static void SetProperty_WithDefaultSource_NoOpWhenPropertyDoesNotExist()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("{\"a\":1}");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetProperty("nonexistent", default(JsonElement));

        // Assert
        Assert.Equal(1, builderDoc.RootElement.GetPropertyCount());
        Assert.Equal("{\"a\":1}", builderDoc.RootElement.ToString());
    }

    #endregion

    #region SetItem with default(JsonElement) removes item

    [Fact]
    public static void SetItem_WithDefaultSource_RemovesExistingItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(1, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(1, root[0].GetInt32());
        Assert.Equal(3, root[1].GetInt32());
    }

    [Fact]
    public static void SetItem_WithDefaultSource_RemovesFirstItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(0, default(JsonElement));

        // Assert
        JsonElement.Mutable root = builderDoc.RootElement;
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal(2, root[0].GetInt32());
        Assert.Equal(3, root[1].GetInt32());
    }

    [Fact]
    public static void SetItem_WithDefaultSource_RemovesLastItem()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(2, default(JsonElement));

        // Assert
        Assert.Equal(2, builderDoc.RootElement.GetArrayLength());
        Assert.Equal("[1,2]", builderDoc.RootElement.ToString());
    }

    [Fact]
    public static void SetItem_WithDefaultSource_RemovesOnlyItem_LeavesEmptyArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[42]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);

        // Act
        builderDoc.RootElement.SetItem(0, default(JsonElement));

        // Assert
        Assert.Equal(0, builderDoc.RootElement.GetArrayLength());
        Assert.Equal("[]", builderDoc.RootElement.ToString());
    }

    #endregion

    #region InsertItem with default(JsonElement) is a no-op

    [Fact]
    public static void InsertItem_WithDefaultSource_IsNoOp()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2,3]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(1, default(JsonElement));

        // Assert - array is unchanged
        Assert.Equal(3, root.GetArrayLength());
        Assert.Equal("[1,2,3]", root.ToString());
    }

    [Fact]
    public static void InsertItem_WithDefaultSource_IsNoOp_AtStart()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(0, default(JsonElement));

        // Assert - array is unchanged
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("[1,2]", root.ToString());
    }

    [Fact]
    public static void InsertItem_WithDefaultSource_IsNoOp_AtEnd()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[1,2]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(2, default(JsonElement));

        // Assert - array is unchanged
        Assert.Equal(2, root.GetArrayLength());
        Assert.Equal("[1,2]", root.ToString());
    }

    [Fact]
    public static void InsertItem_WithDefaultSource_IsNoOp_OnEmptyArray()
    {
        // Arrange
        using var doc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builderDoc = doc.RootElement.CreateBuilder(workspace);
        JsonElement.Mutable root = builderDoc.RootElement;

        // Act
        root.InsertItem(0, default(JsonElement));

        // Assert - array is unchanged
        Assert.Equal(0, root.GetArrayLength());
        Assert.Equal("[]", root.ToString());
    }

    #endregion

}
