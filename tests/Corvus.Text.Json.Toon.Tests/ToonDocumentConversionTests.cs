using Corvus.Text;

#if STJ && TOON
using Corvus.Toon;
using Corvus.Toon.Internal;

namespace Corvus.Toon.Tests;
#else
using Corvus.Text.Json.Toon;
using Corvus.Text.Json.Toon.Internal;

namespace Corvus.Text.Json.Toon.Tests;
#endif

[TestClass]
public sealed class ToonDocumentConversionTests
{
    [TestMethod]
    public void ConvertToJsonStringConvertsObject()
    {
        string actual = ToonDocument.ConvertToJsonString("name: Alice");

        Assert.AreEqual("""{"name":"Alice"}""", actual);
    }

    [TestMethod]
    public void ConvertWritesDirectlyToJsonWriter()
    {
        using System.IO.MemoryStream stream = new();
#if STJ && TOON
        using System.Text.Json.Utf8JsonWriter writer = new(stream);
#else
        using Corvus.Text.Json.Utf8JsonWriter writer = new(stream);
#endif

        ToonDocument.Convert("name: Alice"u8, writer);
        writer.Flush();

        string actual = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.AreEqual("""{"name":"Alice"}""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringAcceptsUtf8Span()
    {
        ReadOnlySpan<byte> toon = "name: Alice"u8;

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""{"name":"Alice"}""", actual);
    }

    [TestMethod]
    public void ParseAcceptsUtf8Memory()
    {
        byte[] toon = "name: Alice"u8.ToArray();

#if STJ && TOON
        using System.Text.Json.JsonDocument document = ToonDocument.Parse(toon);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            ToonDocument.Parse<Corvus.Text.Json.JsonElement>(toon);
#endif

        Assert.AreEqual("""{"name":"Alice"}""", document.RootElement.ToString());
    }

    [TestMethod]
    public void ConvertToJsonStringAcceptsUtf8Memory()
    {
        byte[] toon = "name: Alice"u8.ToArray();

        string actual = ToonDocument.ConvertToJsonString(toon.AsMemory());

        Assert.AreEqual("""{"name":"Alice"}""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringConvertsEmptyInputToEmptyObject()
    {
        string actual = ToonDocument.ConvertToJsonString(string.Empty);

        Assert.AreEqual("{}", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringAcceptsUtf8Bom()
    {
        byte[] toon = [0xEF, 0xBB, 0xBF, .. "name: Alice"u8.ToArray()];

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""{"name":"Alice"}""", actual);
    }

    [TestMethod]
    public void StringOverloadsUseRentedUtf8BuffersForLargeInputs()
    {
        string value = new('A', 300);
        string toon = string.Concat("name: ", value);
        string json = string.Concat("{\"name\":\"", value, "\"}");

        string actualJson = ToonDocument.ConvertToJsonString(toon);
        string actualToon = ToonDocument.ConvertToToonString(json);

#if STJ && TOON
        using System.Text.Json.JsonDocument document = ToonDocument.Parse(toon);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            ToonDocument.Parse<Corvus.Text.Json.JsonElement>(toon);
#endif

        Assert.AreEqual(json, actualJson);
        Assert.AreEqual(toon, actualToon);
        Assert.AreEqual(json, document.RootElement.ToString());
    }

    [TestMethod]
    public void ConvertToToonWritesElementToBufferWriter()
    {
        TestBufferWriter output = new();

#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"name":"Alice","age":4}""");
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"name":"Alice","age":4}""");
#endif

        ToonDocument.ConvertToToon(document.RootElement, output);
        string actual = System.Text.Encoding.UTF8.GetString(output.Buffer, 0, output.WrittenCount);

        Assert.AreEqual("name: Alice\nage: 4", actual);
    }

    [TestMethod]
    public void ConvertToToonWritesUtf8JsonToStream()
    {
        using System.IO.MemoryStream stream = new();

        ToonDocument.ConvertToToon("""{"name":"Alice","age":4}"""u8, stream);

        string actual = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.AreEqual("name: Alice\nage: 4", actual);
    }

    [TestMethod]
    [DataRow("true", "true")]
    [DataRow("123", "123")]
    [DataRow("\"hello\"", "\"hello\"")]
    public void ConvertToJsonStringConvertsPrimitiveRoot(string toon, string expected)
    {
        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("[]", "[]")]
    [DataRow("{}", "{}")]
    [DataRow("items: []", """{"items":[]}""")]
    [DataRow("meta: {}", """{"meta":{}}""")]
    public void ConvertToJsonStringConvertsEmptyContainers(string toon, string expected)
    {
        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToJsonStringConvertsInlineArray()
    {
        string actual = ToonDocument.ConvertToJsonString("[3]: 1,true,Alice");

        Assert.AreEqual("""[1,true,"Alice"]""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringUsesHeaderDelimiterForInlineArrays()
    {
        string actual = ToonDocument.ConvertToJsonString("[3|]: 1|true|Alice");

        Assert.AreEqual("""[1,true,"Alice"]""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringDoesNotSplitQuotedDelimiters()
    {
        string toon = """
            [1]{name,note}:
              Alice,"one,two"
            """;

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""[{"name":"Alice","note":"one,two"}]""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringDecodesQuotedKeys()
    {
        string actual = ToonDocument.ConvertToJsonString("\"full:name\": Alice");

        Assert.AreEqual("""{"full:name":"Alice"}""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringConvertsTabularArray()
    {
        string toon = """
            [2]{id,name}:
              1,Alice
              2,Bob
            """;

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringConvertsObjectWithMoreThanInitialKeySetEstimate()
    {
        string toon = """
            p00: 0
            p01: 1
            p02: 2
            p03: 3
            p04: 4
            p05: 5
            p06: 6
            p07: 7
            p08: 8
            p09: 9
            p10: 10
            p11: 11
            p12: 12
            p13: 13
            p14: 14
            p15: 15
            p16: 16
            p17: 17
            p18: 18
            p19: 19
            """;

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""{"p00":0,"p01":1,"p02":2,"p03":3,"p04":4,"p05":5,"p06":6,"p07":7,"p08":8,"p09":9,"p10":10,"p11":11,"p12":12,"p13":13,"p14":14,"p15":15,"p16":16,"p17":17,"p18":18,"p19":19}""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsDuplicateKeyAfterKeySetGrows()
    {
        string toon = """
            p00: 0
            p01: 1
            p02: 2
            p03: 3
            p04: 4
            p05: 5
            p06: 6
            p07: 7
            p08: 8
            p09: 9
            p10: 10
            p11: 11
            p12: 12
            p13: 13
            p14: 14
            p15: 15
            p16: 16
            p17: 17
            p00: duplicate
            """;

        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("(19,1): Duplicate TOON key 'p00'.", ex.Message);
    }

    [TestMethod]
    public void ConvertToToonStringConvertsJsonObject()
    {
        string actual = ToonDocument.ConvertToToonString("""{"name":"Alice","age":4,"active":true}""");

        string expected = "name: Alice\nage: 4\nactive: true";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonAliasConvertsJsonElement()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"name":"Alice","age":4}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"name":"Alice","age":4}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("name: Alice\nage: 4", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesUniformJsonElementArrayAsTable()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[2]{id,name}:\n  1,Alice\n  2,Bob", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesEscapedJsonElementArrayNamesAsTable()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"na\u006de":"Alice"},{"name":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"na\u006de":"Alice"},{"name":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[2]{name}:\n  Alice\n  Bob", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasMatchesEscapedLaterJsonElementArrayNamesAsTable()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"name":"Alice"},{"na\u006de":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"name":"Alice"},{"na\u006de":"Bob"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[2]{name}:\n  Alice\n  Bob", actual);
    }

    [TestMethod]
    [DataRow("""[{"value":"plain"}]""", "[1]{value}:\n  plain")]
    [DataRow("""[{"value":""}]""", "[1]{value}:\n  \"\"")]
    [DataRow("""[{"value":"true"}]""", "[1]{value}:\n  \"true\"")]
    [DataRow("""[{"value":"42"}]""", "[1]{value}:\n  \"42\"")]
    [DataRow("""[{"value":"one,two"}]""", "[1]{value}:\n  \"one,two\"")]
    [DataRow("""[{"value":"na\u006de"}]""", "[1]{value}:\n  name")]
    public void ConvertToToonAliasWritesJsonElementTableStringCells(string json, string expected)
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesEscapedJsonElementTabularStringCells()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"value":"a\"b"},{"value":"a\\b"},{"value":"a\nb"},{"value":"\u0001"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"value":"a\"b"},{"value":"a\\b"},{"value":"a\nb"},{"value":"\u0001"}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[4]{value}:\n  \"a\\\"b\"\n  \"a\\\\b\"\n  \"a\\nb\"\n  \"\\u0001\"", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesExpandedMixedJsonElementArrayItems()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"items":[1,[2,3],{"a":1},[]]}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"items":[1,[2,3],{"a":1},[]]}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("items[4]:\n  - 1\n  - [2]: 2,3\n  - a: 1\n  - [0]:", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesExpandedJsonElementObjectListItemProperties()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""{"items":[{"empty":{},"values":[],"nested":{"a":1},"array":[1,2]}]}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"items":[{"empty":{},"values":[],"nested":{"a":1},"array":[1,2]}]}""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("items[1]:\n  - empty:\n    values: []\n    nested:\n      a: 1\n    array[2]: 1,2", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasWritesReorderedJsonElementArrayAsTable()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"id":1,"name":"Alice"},{"name":"Bob","id":2}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"id":1,"name":"Alice"},{"name":"Bob","id":2}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[2]{id,name}:\n  1,Alice\n  2,Bob", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasCanonicalizesJsonElementTableNumbers()
    {
#if STJ && TOON
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse("""[{"value":1.5000},{"value":1e3}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#else
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""[{"value":1.5000},{"value":1e3}]""");
        string actual = ToonDocument.ConvertToToon(document.RootElement);
#endif

        Assert.AreEqual("[2]{value}:\n  1.5\n  1000", actual);
    }

#if !(STJ && TOON)
    [TestMethod]
    public void ConvertToToonAliasFoldsCorvusJsonElementKeys()
    {
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"root":{"child":1},"plain":2}""");
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToon(document.RootElement, options);

        Assert.AreEqual("root.child: 1\nplain: 2", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasStopsCorvusJsonElementKeyFoldingForCollisions()
    {
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"root":{"child":1},"root.child":2}""");
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToon(document.RootElement, options);

        Assert.AreEqual("root:\n  child: 1\nroot.child: 2", actual);
    }

    [TestMethod]
    public void ConvertToToonAliasStopsCorvusJsonElementKeyFoldingForNonBareSegments()
    {
        using Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement> document =
            Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse("""{"root":{"not-bare":1}}""");
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToon(document.RootElement, options);

        Assert.AreEqual("root:\n  \"not-bare\": 1", actual);
    }
#endif

    [TestMethod]
    public void ConvertToToonStringWritesUniformObjectArrayAsTable()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""");

        string expected = "[2]{id,name}:\n  1,Alice\n  2,Bob";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringUsesConfiguredDelimiterForTable()
    {
        ToonWriterOptions options = new() { Delimiter = ToonDelimiter.Pipe };

        string actual = ToonDocument.ConvertToToonString("""[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]""", options);

        string expected = "[2|]{id|name}:\n  1|Alice\n  2|Bob";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringQuotesDelimitedTableCells()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"name":"Alice","note":"one,two"}]""");

        string expected = "[1]{name,note}:\n  Alice,\"one,two\"";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringQuotesEscapedTableKeysAndCells()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"full:name":"Al\u0069ce","note":"one\ntwo"}]""");

        string expected = "[1]{\"full:name\",note}:\n  Alice,\"one\\ntwo\"";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringQuotesScalarLookingStringTableCells()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"number":"123","truth":"true","missing":"null"}]""");

        string expected = "[1]{number,truth,missing}:\n  \"123\",\"true\",\"null\"";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringWritesEscapedTabularStringCells()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"value":"a\"b"},{"value":"a\\b"},{"value":"a\nb"},{"value":"\u0001"}]""");

        string expected = "[4]{value}:\n  \"a\\\"b\"\n  \"a\\\\b\"\n  \"a\\nb\"\n  \"\\u0001\"";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringWritesExpandedMixedArrayItems()
    {
        string actual = ToonDocument.ConvertToToonString("""{"items":[1,[2,3],{"a":1},[]]}""");

        string expected = "items[4]:\n  - 1\n  - [2]: 2,3\n  - a: 1\n  - [0]:";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringWritesExpandedObjectListItemProperties()
    {
        string actual = ToonDocument.ConvertToToonString("""{"items":[{"empty":{},"values":[],"nested":{"a":1},"array":[1,2]}]}""");

        string expected = "items[1]:\n  - empty:\n    values: []\n    nested:\n      a: 1\n    array[2]: 1,2";
        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringCanonicalizesRootNumbers()
    {
        string actual = ToonDocument.ConvertToToonString("1.5000e+3"u8);

        Assert.AreEqual("1500", actual);
    }

    [TestMethod]
    public void ConvertToToonStringRejectsEmptyUtf8Json()
    {
#if STJ && TOON
        Assert.Throws<System.Text.Json.JsonException>(() => ToonDocument.ConvertToToonString(ReadOnlySpan<byte>.Empty));
#else
        Assert.Throws<JsonException>(() => ToonDocument.ConvertToToonString(ReadOnlySpan<byte>.Empty));
#endif
    }

    [TestMethod]
    public void ConvertToToonStringWithKeyFoldingConvertsNonObjectRoots()
    {
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToonString("[1,true]"u8, options);

        Assert.AreEqual("[2]: 1,true", actual);
    }

    [TestMethod]
    public void ConvertToToonStringWithKeyFoldingConvertsEmptyObject()
    {
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToonString("{}"u8, options);

        Assert.AreEqual(string.Empty, actual);
    }

    [TestMethod]
    [DataRow("""{"n\u0061me":{"child":1}}""", "name.child: 1")]
    [DataRow("""{"root":{"not-bare":1}}""", "root:\n  \"not-bare\": 1")]
    [DataRow("""{"root":{"child":1},"root.child":2}""", "root:\n  child: 1\nroot.child: 2")]
    public void ConvertToToonStringWithKeyFoldingHandlesEscapedAndUnfoldableKeys(string json, string expected)
    {
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToonString(json, options);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringWithKeyFoldingRentsLargeEscapedPropertyNameBuffer()
    {
        string suffix = new('x', 300);
        string json = "{\"n\\u0061me" + suffix + "\":{\"child\":1}}";
        ToonWriterOptions options = new() { KeyFolding = ToonKeyFolding.Safe };

        string actual = ToonDocument.ConvertToToonString(json, options);

        Assert.AreEqual("name" + suffix + ".child: 1", actual);
    }

    [TestMethod]
    [DataRow("0.0000", "0")]
    [DataRow("-1.20e+2", "-120")]
    [DataRow("1e-2", "0.01")]
    [DataRow("1.2e1", "12")]
    public void ConvertToToonStringCanonicalizesAdditionalRootNumberShapes(string json, string expected)
    {
        string actual = ToonDocument.ConvertToToonString(System.Text.Encoding.UTF8.GetBytes(json));

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ConvertToToonStringCanonicalizesObjectNumbers()
    {
        string actual = ToonDocument.ConvertToToonString("""{"small":1e-6,"trim":1.2300,"zero":-0.0}"""u8);

        Assert.AreEqual("small: 0.000001\ntrim: 1.23\nzero: 0", actual);
    }

    [TestMethod]
    public void ConvertToToonStringCanonicalizesTableNumbers()
    {
        string actual = ToonDocument.ConvertToToonString("""[{"value":1.5000},{"value":1e3}]"""u8);

        Assert.AreEqual("[2]{value}:\n  1.5\n  1000", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringDecodesQuotedTabularFields()
    {
        string toon = """
            [1]{"full:name",note}:
              Alice,"one,two"
            """;

        string actual = ToonDocument.ConvertToJsonString(toon);

        Assert.AreEqual("""[{"full:name":"Alice","note":"one,two"}]""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsTabularRowWidthErrorLocation()
    {
        string toon = """
            [2]{id,name}:
              1,Alice,extra
            """;

        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("(2,3): TOON tabular row width does not match the header field count.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsStrictArrayCountMismatch()
    {
        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString("[2]: 1"));

        Assert.AreEqual("(1,1): TOON array header declared 2 item(s), but 1 item(s) were read.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsDuplicateKeysInStrictMode()
    {
        string toon = """
            name: Alice
            name: Bob
            """;

        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("(2,1): Duplicate TOON key 'name'.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsDuplicateQuotedKeysInStrictMode()
    {
        string toon = """
            name: Alice
            "name": Bob
            """;

        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("(2,1): Duplicate TOON key 'name'.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsUnexpectedRootContentAfterScalar()
    {
        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString("true\nfalse"));

        Assert.AreEqual("(2,1): Unexpected additional content at the root.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringReportsExpectedObjectMemberForNestedScalar()
    {
        string toon = """
            parent:
              child
            """;

        ToonException ex = Assert.ThrowsExactly<ToonException>(() => ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("(2,3): Expected object member.", ex.Message);
    }

    [TestMethod]
    public void ConvertToJsonStringCanDisableStrictArrayCountValidation()
    {
        ToonReaderOptions options = new() { Strict = false };

        string actual = ToonDocument.ConvertToJsonString("[2]: 1", options);

        Assert.AreEqual("[1]", actual);
    }

    [TestMethod]
    public void JsonToToonToJsonRoundTripsObjectAndTabularArray()
    {
        string json = """{"items":[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}],"active":true}""";

        string toon = ToonDocument.ConvertToToonString(json);
        string actual = NormalizeJson(ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual(NormalizeJson(json), actual);
    }

    [TestMethod]
    public void JsonToToonToJsonRoundTripsConfiguredDelimiter()
    {
        string json = """{"items":[{"id":1,"name":"Alice"},{"id":2,"name":"Bob"}]}""";
        ToonWriterOptions options = new() { Delimiter = ToonDelimiter.Pipe };

        string toon = ToonDocument.ConvertToToonString(json, options);
        string actual = NormalizeJson(ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("items[2|]{id|name}:\n  1|Alice\n  2|Bob", toon);
        Assert.AreEqual(NormalizeJson(json), actual);
    }

    [TestMethod]
    public void JsonToToonToJsonRoundTripsCanonicalNumbers()
    {
        string json = """{"small":1e-6,"trim":1.2300,"zero":-0.0}""";

        string toon = ToonDocument.ConvertToToonString(json);
        string actual = NormalizeJson(ToonDocument.ConvertToJsonString(toon));

        Assert.AreEqual("small: 0.000001\ntrim: 1.23\nzero: 0", toon);
        Assert.AreEqual("""{"small":0.000001,"trim":1.23,"zero":0}""", actual);
    }

    [TestMethod]
    public void ToonToJsonToToonRoundTripsTabularArray()
    {
        string toon = "[2]{id,name}:\n  1,Alice\n  2,Bob";

        string json = ToonDocument.ConvertToJsonString(toon);
        string actual = ToonDocument.ConvertToToonString(json);

        Assert.AreEqual(toon, actual);
    }

    [TestMethod]
    public void ToonToJsonToToonRoundTripsExpandedPathsWithKeyFolding()
    {
        string toon = "a.b.c: 1";
        ToonReaderOptions readerOptions = new() { ExpandPaths = ToonPathExpansion.Safe };
        ToonWriterOptions writerOptions = new() { KeyFolding = ToonKeyFolding.Safe };

        string json = ToonDocument.ConvertToJsonString(toon, readerOptions);
        string actual = ToonDocument.ConvertToToonString(json, writerOptions);

        Assert.AreEqual(toon, actual);
    }

    [TestMethod]
    public void ConvertToJsonStringExpandsUnquotedPathsButPreservesQuotedPaths()
    {
        string toon = """
            "a.b": 1
            a.c: 2
            """;
        ToonReaderOptions options = new() { ExpandPaths = ToonPathExpansion.Safe };

        string actual = ToonDocument.ConvertToJsonString(toon, options);

        Assert.AreEqual("""{"a.b":1,"a":{"c":2}}""", actual);
    }

    [TestMethod]
    public void ConvertToJsonStringPreservesManyQuotedLiteralPaths()
    {
        System.Text.StringBuilder toon = new();
        System.Text.StringBuilder expected = new("""{""");
        for (int i = 0; i < 18; i++)
        {
            if (i > 0)
            {
                expected.Append(',');
            }

            toon.Append('"').Append("literal").Append(i).Append(".path").Append('"').Append(": ").Append(i).Append('\n');
            expected.Append('"').Append("literal").Append(i).Append(".path").Append('"').Append(':').Append(i);
        }

        expected.Append('}');
        ToonReaderOptions options = new() { ExpandPaths = ToonPathExpansion.Safe };

        string actual = ToonDocument.ConvertToJsonString(toon.ToString(), options);

        Assert.AreEqual(expected.ToString(), actual);
    }

    [TestMethod]
    public void ConvertToJsonStringPreservesLargeQuotedLiteralPath()
    {
        string key = new string('a', 300) + ".path";
        string toon = "\"" + key + "\": 1";
        ToonReaderOptions options = new() { ExpandPaths = ToonPathExpansion.Safe };

        string actual = ToonDocument.ConvertToJsonString(toon, options);

        Assert.AreEqual("{\"" + key + "\":1}", actual);
    }

    [TestMethod]
    public void Utf8ToonWriterWritesDirectObjectAndList()
    {
        using System.IO.MemoryStream stream = new();
        Utf8ToonWriter writer = new(stream);
        writer.WriteObjectStart();
        writer.WriteKeyValue("name"u8, "Alice"u8);
        writer.WriteNewLine();
        writer.WriteArrayHeader("items"u8, 2);
        writer.WriteNewLine();
        writer.WriteListItem("one"u8, 1);
        writer.WriteNewLine();
        writer.WriteListItem("two"u8, 1);
        writer.WriteObjectEnd();
        writer.Dispose();

        string actual = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.AreEqual("name: Alice\nitems[2]:\n  - one\n  - two", actual);
    }

    [TestMethod]
    public void Utf8ToonWriterWritesDirectTabularArray()
    {
        using System.IO.MemoryStream stream = new();
        Utf8ToonWriter writer = new(stream);
        writer.WriteTabularHeader("items"u8, 2, "id,name"u8);
        writer.WriteNewLine();
        writer.WriteRow("1,Alice"u8);
        writer.WriteNewLine();
        writer.WriteRow("2,Bob"u8);
        writer.Dispose();

        string actual = System.Text.Encoding.UTF8.GetString(stream.ToArray());

        Assert.AreEqual("items[2]{id,name}:\n  1,Alice\n  2,Bob", actual);
    }

    [TestMethod]
    public void Utf8ToonWriterWritesEscapedValuesAndTabDelimitedHeaders()
    {
        TestBufferWriter output = new();
        Utf8ToonWriter writer = new(output, new ToonWriterOptions { Delimiter = ToonDelimiter.Tab });
        writer.WriteArrayHeader(2);
        writer.WriteNewLine();
        writer.WriteKeyValue("full:name"u8, " value\t\"\\\r\n\u0001"u8);
        writer.Dispose();

        string actual = System.Text.Encoding.UTF8.GetString(output.Buffer, 0, output.WrittenCount);

        Assert.AreEqual("[2\t]:\n\"full:name\": \" value\\t\\\"\\\\\\r\\n\\u0001\"", actual);
    }

    [TestMethod]
    public void Utf8ToonWriterWritesRootAndNamedTabularHeaders()
    {
        TestBufferWriter output = new();
        Utf8ToonWriter writer = new(output, new ToonWriterOptions { Delimiter = ToonDelimiter.Pipe });
        writer.WriteTabularHeader(3, "id|name"u8, 1);
        writer.WriteNewLine();
        writer.WriteTabularHeader("items"u8, 2, "id|name"u8, 2);
        writer.Dispose();

        string actual = System.Text.Encoding.UTF8.GetString(output.Buffer, 0, output.WrittenCount);

        Assert.AreEqual("  [3|]{id|name}:\n    items[2|]{id|name}:", actual);
    }

    [TestMethod]
    public void Utf8ToonWriterRejectsNullAndNonWritableStreams()
    {
        Assert.ThrowsExactly<ArgumentNullException>(static () => CreateWriterForBuffer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(static () => CreateWriterForStream(null!));
        using NonWritableStream stream = new();
        Assert.ThrowsExactly<ArgumentException>(() => CreateWriterForStream(stream));
    }

    [TestMethod]
    public void Utf8ToonWriterThrowsAfterDispose()
    {
        TestBufferWriter output = new();
        Utf8ToonWriter writer = new(output);
        writer.Dispose();

        try
        {
            writer.WriteNewLine();
            Assert.Fail("Expected ObjectDisposedException.");
        }
        catch (ObjectDisposedException)
        {
        }

        try
        {
            writer.WriteKeyValue("name"u8, "Alice"u8);
            Assert.Fail("Expected ObjectDisposedException.");
        }
        catch (ObjectDisposedException)
        {
        }
    }

    [TestMethod]
    [DataRow("", ToonDelimiter.Comma, false)]
    [DataRow(" Alice", ToonDelimiter.Comma, false)]
    [DataRow("Alice ", ToonDelimiter.Comma, false)]
    [DataRow("-1", ToonDelimiter.Comma, false)]
    [DataRow("true", ToonDelimiter.Comma, false)]
    [DataRow("false", ToonDelimiter.Comma, false)]
    [DataRow("null", ToonDelimiter.Comma, false)]
    [DataRow("1", ToonDelimiter.Comma, false)]
    [DataRow("1.2", ToonDelimiter.Comma, false)]
    [DataRow("1e2", ToonDelimiter.Comma, false)]
    [DataRow("1.2e3", ToonDelimiter.Comma, false)]
    [DataRow("1.", ToonDelimiter.Comma, true)]
    [DataRow("1e+", ToonDelimiter.Comma, true)]
    [DataRow("1.2e+", ToonDelimiter.Comma, true)]
    [DataRow("1e2x", ToonDelimiter.Comma, true)]
    [DataRow("Alice", ToonDelimiter.Comma, true)]
    [DataRow("one,two", ToonDelimiter.Comma, false)]
    [DataRow("one,two", ToonDelimiter.Pipe, true)]
    [DataRow("one|two", ToonDelimiter.Pipe, false)]
    [DataRow("one\ttwo", ToonDelimiter.Tab, false)]
    [DataRow("a:b", ToonDelimiter.Comma, false)]
    [DataRow("a\"b", ToonDelimiter.Comma, false)]
    [DataRow("\u0001", ToonDelimiter.Comma, false)]
    public void Utf8ToonWriterClassifiesBareStrings(string value, ToonDelimiter delimiter, bool expected)
    {
        byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);

        bool actual = Utf8ToonWriter.IsBareString(utf8, delimiter);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    [DataRow("", "")]
    [DataRow("abc", "abc")]
    [DataRow("-01", "-1")]
    [DataRow("1e", "1e")]
    [DataRow("1.2x", "1.2x")]
    [DataRow("000.000", "0")]
    [DataRow("-0.000", "0")]
    [DataRow("1.2300", "1.23")]
    [DataRow("0.0012300", "0.00123")]
    [DataRow("123e-5", "0.00123")]
    [DataRow("123e+5", "12300000")]
    [DataRow("123456789012345678901234567890e1000001", "123456789012345678901234567890e1000001")]
    public void Utf8ToonWriterCanonicalizesTrustedNumbers(string value, string expected)
    {
        string actual = CanonicalizeTrustedNumber(value);

        Assert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ArrayPoolBufferWriterGrowsAndSupportsDefaultSizeHints()
    {
        using ArrayPoolBufferWriter writer = new(1);

        Span<byte> first = writer.GetSpan();
        first[0] = 1;
        writer.Advance(1);

        Span<byte> second = writer.GetSpan(300);
        second.Fill(2);
        writer.Advance(300);

        ReadOnlySpan<byte> written = writer.WrittenSpan;
        Assert.AreEqual(301, written.Length);
        Assert.AreEqual(1, written[0]);
        Assert.AreEqual(2, written[300]);
    }

    private static string NormalizeJson(string json)
    {
        using System.Text.Json.JsonDocument document = System.Text.Json.JsonDocument.Parse(json);

        return System.Text.Json.JsonSerializer.Serialize(document.RootElement);
    }

    private static string CanonicalizeTrustedNumber(string value)
    {
        Span<byte> initialBuffer = stackalloc byte[64];
        Utf8ValueStringBuilder builder = new(initialBuffer);
        try
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            Utf8ToonWriter.AppendCanonicalNumber(ref builder, utf8);
            return System.Text.Encoding.UTF8.GetString(builder.AsSpan().ToArray());
        }
        finally
        {
            builder.Dispose();
        }
    }

    private static void CreateWriterForStream(System.IO.Stream stream)
    {
        Utf8ToonWriter writer = new(stream);
        writer.Dispose();
    }

    private static void CreateWriterForBuffer(System.Buffers.IBufferWriter<byte> buffer)
    {
        Utf8ToonWriter writer = new(buffer);
        writer.Dispose();
    }

    private sealed class TestBufferWriter : System.Buffers.IBufferWriter<byte>
    {
        private byte[] buffer = new byte[256];

        public byte[] Buffer => this.buffer;

        public int WrittenCount { get; private set; }

        public void Advance(int count)
        {
            this.WrittenCount += count;
        }

        public Memory<byte> GetMemory(int sizeHint = 0)
        {
            this.Ensure(sizeHint);
            return this.Buffer.AsMemory(this.WrittenCount);
        }

        public Span<byte> GetSpan(int sizeHint = 0)
        {
            this.Ensure(sizeHint);
            return this.Buffer.AsSpan(this.WrittenCount);
        }

        private void Ensure(int sizeHint)
        {
            int required = this.WrittenCount + Math.Max(sizeHint, 1);
            if (required <= this.Buffer.Length)
            {
                return;
            }

            Array.Resize(ref this.buffer, Math.Max(required, this.buffer.Length * 2));
        }
    }

    private sealed class NonWritableStream : System.IO.Stream
    {
        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => 0;

        public override long Position
        {
            get => 0;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, System.IO.SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}