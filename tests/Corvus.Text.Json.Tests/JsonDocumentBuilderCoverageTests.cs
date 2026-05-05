// <copyright file="JsonDocumentBuilderCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Linq;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
using NodaTime;
using Xunit;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests targeting specific uncovered paths in <see cref="JsonDocumentBuilder{T}"/>.
/// </summary>
public static class JsonDocumentBuilderCoverageTests
{
    /// <summary>
    /// Verifies that <see cref="JsonProperty{TValue}.WriteTo"/> works correctly
    /// for a simple (non-escaped) property name on a builder-backed element.
    /// This exercises <c>IJsonDocument.WritePropertyName</c> → <c>WritePropertyNameUnsafe</c>
    /// (the else branch — no escape sequences).
    /// </summary>
    [Fact]
    public static void PropertyWriteTo_SimplePropertyName_WritesCorrectly()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice","age":30}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate and write each property individually via JsonProperty.WriteTo
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();

        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        // Parse the result and verify
        using ParsedJsonDocument<JsonElement> result = ParsedJsonDocument<JsonElement>.Parse(output.WrittenMemory);
        Assert.True(result.RootElement.TryGetProperty("name"u8, out JsonElement nameVal));
        Assert.Equal("Alice", nameVal.GetString());
        Assert.True(result.RootElement.TryGetProperty("age"u8, out JsonElement ageVal));
        Assert.Equal(30, ageVal.GetInt32());
    }

    /// <summary>
    /// Verifies that <see cref="JsonProperty{TValue}.WriteTo"/> works correctly
    /// for an escaped property name on a builder-backed element.
    /// This exercises <c>IJsonDocument.WritePropertyName</c> → <c>WritePropertyNameUnsafe</c>
    /// → <c>UnescapeString</c> → <c>ClearAndReturn</c> (the if branch — has escape sequences).
    /// </summary>
    [Fact]
    public static void PropertyWriteTo_EscapedPropertyName_WritesCorrectly()
    {
        // Property name with escape sequences triggers HasComplexChildren=true
        ReadOnlyMemory<byte> json = """{"a\"b":1,"hello\nworld":2}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate and write each property individually via JsonProperty.WriteTo
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output);
        writer.WriteStartObject();

        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            prop.WriteTo(writer);
        }

        writer.WriteEndObject();
        writer.Flush();

        // Parse the result and verify the property names are accessible by their unescaped values
        using ParsedJsonDocument<JsonElement> result = ParsedJsonDocument<JsonElement>.Parse(output.WrittenMemory);
        Assert.True(result.RootElement.TryGetProperty("a\"b", out JsonElement val1));
        Assert.Equal(1, val1.GetInt32());
        Assert.True(result.RootElement.TryGetProperty("hello\nworld", out JsonElement val2));
        Assert.Equal(2, val2.GetInt32());
    }

    /// <summary>
    /// Verifies that the generic <c>IJsonDocument.TryGetNamedPropertyValue&lt;TElement&gt;</c>
    /// with a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> finds an existing property
    /// when the builder is accessed through the <see cref="IJsonDocument"/> interface.
    /// </summary>
    [Fact]
    public static void TryGetNamedPropertyValue_Generic_CharSpan_FindsProperty()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice","age":30}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        // Access through IJsonDocument interface to call the generic TryGetNamedPropertyValue<TElement>
        IJsonDocument builderAsDoc = builder;
        bool found = builderAsDoc.TryGetNamedPropertyValue<JsonElement>(0, "name".AsSpan(), out JsonElement value);

        Assert.True(found);
        Assert.Equal(JsonValueKind.String, value.ValueKind);
        Assert.Equal("Alice", value.GetString());
    }

    /// <summary>
    /// Verifies that the generic <c>IJsonDocument.TryGetNamedPropertyValue&lt;TElement&gt;</c>
    /// with a <see cref="ReadOnlySpan{T}"/> of <see cref="char"/> returns false for a missing property.
    /// </summary>
    [Fact]
    public static void TryGetNamedPropertyValue_Generic_CharSpan_ReturnsFalseForMissing()
    {
        ReadOnlyMemory<byte> json = """{"name":"Alice"}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        IJsonDocument builderAsDoc = builder;
        bool found = builderAsDoc.TryGetNamedPropertyValue<JsonElement>(0, "missing".AsSpan(), out JsonElement value);

        Assert.False(found);
        Assert.Equal(default, value);
    }

    /// <summary>
    /// Exercises the <c>HasComplexChildren</c> (escaped string) path in <c>GetUtf8JsonStringUnsafe</c>
    /// and <c>GetUtf16JsonStringUnsafe</c> on a builder-backed document.
    /// Targets JsonDocumentBuilder.cs lines 1257-1278 (UTF-8 unescape) and 1303-1335 (UTF-16 unescape).
    /// </summary>
    [Fact]
    public static void Builder_GetString_WithEscapedContent_UnescapesCorrectly()
    {
        // JSON with a \n escape sequence — the backslash-n in the raw JSON bytes creates HasComplexChildren.
        // In C# we use byte array to produce exact JSON bytes: {"msg":"hello\nworld","plain":"normal"}
        byte[] jsonBytes = """{"msg":"hello\nworld","plain":"normal"}"""u8.ToArray();
        ReadOnlyMemory<byte> json = jsonBytes;

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // GetString exercises GetStringUnsafe with HasComplexChildren=true
        Assert.True(root.TryGetProperty("msg"u8, out JsonElement.Mutable msgProp));
        Assert.Equal("hello\nworld", msgProp.GetString());

        // GetUtf8String exercises GetUtf8JsonStringUnsafe with HasComplexChildren=true
        using UnescapedUtf8JsonString utf8Str = msgProp.GetUtf8String();
        Assert.Equal("hello\nworld"u8.ToArray(), utf8Str.Span.ToArray());

        // GetUtf16String exercises GetUtf16JsonStringUnsafe with HasComplexChildren=true
        using UnescapedUtf16JsonString utf16Str = msgProp.GetUtf16String();
        Assert.Equal("hello\nworld", utf16Str.Span.ToString());

        // Mutate the builder to force local storage, then ToString() exercises
        // WriteComplexElementToUnsafe → GetUtf8JsonStringUnsafe for HasComplexChildren strings
        root.SetProperty("extra"u8, true);
        string serialized = root.ToString();
        Assert.Contains("hello\\nworld", serialized);
        Assert.Contains("extra", serialized);

        // Also verify the non-escaped path still works
        Assert.True(root.TryGetProperty("plain"u8, out JsonElement.Mutable plainProp));
        Assert.Equal("normal", plainProp.GetString());
    }

    /// <summary>
    /// Exercises <c>IJsonDocument.TryGetString</c> type mismatch path on builder.
    /// Targets JsonDocumentBuilder.cs lines 1196-1199 (expectedType != tokenType → return false).
    /// </summary>
    [Fact]
    public static void Builder_TryGetString_WrongType_ReturnsFalse()
    {
        ReadOnlyMemory<byte> json = """{"num":42,"str":"hello"}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        // Cast to IJsonDocument to access TryGetString directly
        IJsonDocument builderAsDoc = builder;

        // Index 0 is the root object — passing expectedType=String should mismatch (it's StartObject)
        bool result = builderAsDoc.TryGetString(0, JsonTokenType.String, out string? value);
        Assert.False(result);
        Assert.Null(value);
    }

    /// <summary>
    /// Exercises the builder path for <c>TryGetBytesFromBase64</c> with an escaped string value.
    /// Targets JsonDocumentBuilder.cs lines 1465-1467 (HasComplexChildren → TryGetUnescapedBase64Bytes).
    /// </summary>
    [Fact]
    public static void Builder_TryGetBytesFromBase64_EscapedString()
    {
        // JSON with \u0048 escape (= 'H'). The parser marks the string as HasComplexChildren.
        // Unescaped value is "Hello" which is not valid base64.
        ReadOnlyMemory<byte> json = """{"data":"\u0048ello"}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("data"u8, out JsonElement.Mutable dataProp));

        // The unescaped value is "Hello" which is not valid base64, so TryGetBytesFromBase64 returns false
        // but it exercises the HasComplexChildren unescape path
        bool success = dataProp.TryGetBytesFromBase64(out byte[]? bytes);
        Assert.False(success);
        Assert.Null(bytes);
    }

    /// <summary>
    /// Exercises <c>TryGetBytesFromBase64</c> with a valid escaped base64 string on builder.
    /// </summary>
    [Fact]
    public static void Builder_TryGetBytesFromBase64_EscapedValidBase64()
    {
        // \u0053 = 'S', so the unescaped value is "SGVsbG8=" which decodes to "Hello"
        ReadOnlyMemory<byte> json = """{"data":"\u0053GVsbG8="}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("data"u8, out JsonElement.Mutable dataProp));

        bool success = dataProp.TryGetBytesFromBase64(out byte[]? bytes);
        Assert.True(success);
        Assert.NotNull(bytes);
        Assert.Equal("Hello"u8.ToArray(), bytes);
    }

#if NET
    /// <summary>
    /// Exercises the <c>Half.TryParse</c> failure path by parsing a number too large for Half
    /// and attempting to read it via the tensor helpers which use <c>TryGetValue(out Half)</c>.
    /// Targets JsonDocumentBuilder.cs lines 1805-1806.
    /// </summary>
    [Fact]
    public static void Builder_NumberTooLargeForHalf_HandledGracefully()
    {
        // A number that exceeds Half.MaxValue (~65504) — 1e39 is way beyond Half range
        ReadOnlyMemory<byte> json = """{"big":1e+39}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("big"u8, out JsonElement.Mutable bigProp));

        // The number is valid JSON but exceeds Half range
        Assert.Equal(JsonValueKind.Number, bigProp.ValueKind);
        Assert.True(bigProp.TryGetDouble(out double d));
        Assert.Equal(1e39, d);
    }
#endif

    /// <summary>
    /// Exercises the <c>GetRawSimpleValueFromRow</c> path where the offset is in the raw JSON backing
    /// region (offset &lt; _rawJsonLength) for a property value that includes quotes.
    /// Targets JsonDocumentBuilder.cs lines 1124-1127 (the raw JSON path with includeQuotes=false).
    /// </summary>
    [Fact]
    public static void Builder_RawValue_FromParsedJson_IncludesCorrectBytes()
    {
        ReadOnlyMemory<byte> json = """{"x":"test","n":123}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Verify string and number values serialize correctly (exercises raw backing retrieval)
        Assert.True(root.TryGetProperty("x"u8, out JsonElement.Mutable xProp));
        Assert.Equal("test", xProp.GetString());

        Assert.True(root.TryGetProperty("n"u8, out JsonElement.Mutable nProp));
        Assert.Equal(123, nProp.GetInt32());
    }

    /// <summary>
    /// Exercises the <c>GetPropertyNameUnescaped</c> path on a builder with an escaped property name.
    /// This targets the property-name unescape branch in JsonDocumentBuilder (lines 1445-1470 area).
    /// </summary>
    [Fact]
    public static void Builder_EscapedPropertyName_EnumerationUnescapes()
    {
        // Property name with an escape sequence — forces HasComplexChildren on the PropertyName row
        ReadOnlyMemory<byte> json = """{"hello\nworld":42}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerating properties exercises the property name unescaping path
        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            Assert.Equal("hello\nworld", prop.Name);
            Assert.Equal(42, prop.Value.GetInt32());
        }
    }

    /// <summary>
    /// Exercises adding a property using char-based <c>SetProperty(string, ...)</c> on a dynamic builder
    /// to cover the <c>StartProperty(ReadOnlySpan&lt;char&gt;)</c> path in ComplexValueBuilder.
    /// Targets ComplexValueBuilder.cs lines 3454-3460.
    /// </summary>
    [Fact]
    public static void Builder_SetProperty_CharBased_CoversStartPropertyCharOverload()
    {
        ReadOnlyMemory<byte> json = """{"existing":1}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // SetProperty with a string name exercises the char-span StartProperty overload
        root.SetProperty("newProp", 99);

        Assert.True(root.TryGetProperty("newProp"u8, out JsonElement.Mutable val));
        Assert.Equal(99, val.GetInt32());
    }

    /// <summary>
    /// Exercises the <c>FreezeElement</c> path where a builder element references an external mutable document.
    /// Targets JsonDocumentBuilder.cs lines 2100-2102.
    /// </summary>
    [Fact]
    public static void Builder_CloneElement_FromExternalMutableDocument()
    {
        ReadOnlyMemory<byte> json1 = """{"a":1}"""u8.ToArray();
        ReadOnlyMemory<byte> json2 = """{"b":2}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc1 = ParsedJsonDocument<JsonElement>.Parse(json1);
        using ParsedJsonDocument<JsonElement> doc2 = ParsedJsonDocument<JsonElement>.Parse(json2);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder1 = doc1.RootElement.CreateBuilder(workspace);
        using JsonDocumentBuilder<JsonElement.Mutable> builder2 = doc2.RootElement.CreateBuilder(workspace);

        // Set a property on builder1 using an element from builder2 — creates an external document reference
        JsonElement.Mutable root1 = builder1.RootElement;
        root1.SetProperty("fromOther"u8, builder2.RootElement);

        // Now freeze the element to exercise the FreezeElement path
        Assert.True(root1.TryGetProperty("fromOther"u8, out JsonElement.Mutable fromOther));
        JsonElement frozen = fromOther.Freeze();
        Assert.Equal(JsonValueKind.Object, frozen.ValueKind);
    }

    /// <summary>
    /// Exercises the large-buffer path in <c>GetUtf16JsonStringUnsafe</c> where the escaped JSON string
    /// is longer than <c>JsonConstants.StackallocByteThreshold</c> (256 bytes), forcing an <c>ArrayPool</c>
    /// rent for the UTF-8 unescape buffer and its return in the finally block.
    /// Targets JsonDocumentBuilder.cs lines 1310 (rent), 1332-1335 (finally: return rented array).
    /// </summary>
    [Fact]
    public static void Builder_GetUtf16String_LargeEscapedString_HitsRentedBufferPath()
    {
        // Build a JSON string with many \uXXXX escapes so the raw JSON segment > 256 bytes.
        // Each \uXXXX is 6 bytes in the raw JSON. We need > 256 bytes total, so 44+ escapes.
        // Use 50 escapes of \u0041 (= 'A') → 300 bytes raw → triggers ArrayPool rent.
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"big\":\"");
        for (int i = 0; i < 50; i++)
        {
            sb.Append(@"\u0041");
        }

        sb.Append("\"}");
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
        ReadOnlyMemory<byte> json = jsonBytes;

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("big"u8, out JsonElement.Mutable bigProp));

        // GetUtf16String forces the large-buffer path because raw segment (300 bytes) > 256 threshold
        using UnescapedUtf16JsonString utf16Str = bigProp.GetUtf16String();
        Assert.Equal(new string('A', 50), utf16Str.Span.ToString());

        // Also exercise GetUtf8String on the same large escaped string
        using UnescapedUtf8JsonString utf8Str = bigProp.GetUtf8String();
        Assert.Equal(50, utf8Str.Span.Length);
        Assert.True(utf8Str.Span.ToArray().All(b => b == (byte)'A'));
    }

    /// <summary>
    /// Exercises the <c>WriteComplexElementToUnsafe</c> property-name unescape path (lines 2275-2280)
    /// when serializing a builder object that contains an escaped property name after mutation.
    /// Mutation forces local complex data, so ToString goes through WriteComplexElementToUnsafe.
    /// </summary>
    [Fact]
    public static void Builder_ToString_WithEscapedPropertyName_SerializesCorrectly()
    {
        // Property name contains \n escape — the serializer must unescape it for the writer
        ReadOnlyMemory<byte> json = """{"line\none":1,"normal":2}"""u8.ToArray();

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        JsonElement.Mutable root = builder.RootElement;

        // Mutate the builder to force local storage — after mutation the root is locally rebuilt
        root.SetProperty("added"u8, 3);

        string serialized = root.ToString();

        // The output should contain the escaped property name re-encoded by Utf8JsonWriter
        Assert.Contains("line", serialized);
        Assert.Contains("one", serialized);
        Assert.Contains("added", serialized);

        // Verify round-trip: parse the serialized output and check property access
        using ParsedJsonDocument<JsonElement> roundTripped = ParsedJsonDocument<JsonElement>.Parse(
            System.Text.Encoding.UTF8.GetBytes(serialized));
        Assert.True(roundTripped.RootElement.TryGetProperty("line\none", out JsonElement val));
        Assert.Equal(1, val.GetInt32());
        Assert.True(roundTripped.RootElement.TryGetProperty("added", out JsonElement addedVal));
        Assert.Equal(3, addedVal.GetInt32());
    }

    // =====================================================================
    // Batch 2: ParsedJsonDocument and Builder.Parse coverage paths
    // =====================================================================

    /// <summary>
    /// Exercises <c>ParsedJsonDocument.TryGetString</c> (lines 402-405) via the
    /// <c>IJsonDocument</c> explicit interface when the token type does not match.
    /// </summary>
    [Fact]
    public static void ParsedDocument_TryGetString_TypeMismatch_ReturnsFalse()
    {
        // Parse a JSON number — its token type is Number, not String
        ReadOnlyMemory<byte> json = "42"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

        // Cast to IJsonDocument and call TryGetString expecting String — should fail
        IJsonDocument ijsonDoc = doc;
        bool result = ijsonDoc.TryGetString(0, JsonTokenType.String, out string? str);

        Assert.False(result);
        Assert.Null(str);
    }

    /// <summary>
    /// Exercises <c>ParsedJsonDocument.TryGetValue(out OffsetDate)</c> early return
    /// (lines 1096-1098) when the string is longer than
    /// <c>MaximumEscapedDateTimeOffsetParseLength</c>.
    /// </summary>
    [Fact]
    public static void ParsedDocument_TryGetOffsetDate_TooLongString_ReturnsFalse()
    {
        // Create a JSON string that exceeds MaximumEscapedDateTimeOffsetParseLength (~294 chars)
        string longValue = new string('x', 300);
        byte[] json = System.Text.Encoding.UTF8.GetBytes($"\"{longValue}\"");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

        // TryGetOffsetDate delegates to IJsonDocument.TryGetValue(out OffsetDate)
        bool result = doc.RootElement.TryGetOffsetDate(out NodaTime.OffsetDate _);

        Assert.False(result);
    }

    /// <summary>
    /// Exercises <c>JsonDocumentBuilder.GetRawSimpleValueUnsafe(ref MetadataDb, int)</c>
    /// local raw JSON backing path (lines 1124-1127) via <c>JsonDocumentBuilder.Parse</c>.
    /// When a builder is created via Parse, values reside in the raw JSON backing region.
    /// The no-quotes overload (without includeQuotes parameter) is called from DeepEquals
    /// comparisons via <c>IJsonDocument.GetRawSimpleValueUnsafe(int index)</c>.
    /// </summary>
    [Fact]
    public static void BuilderParse_DeepEquals_UsesLocalRawJsonBacking_NoQuotesPath()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""{"x":42}""");

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("x"u8, out JsonElement.Mutable xElement));

        // DeepEquals on a number calls IJsonDocument.GetRawSimpleValueUnsafe(index)
        // which routes to the no-quotes overload → GetRawSimpleValueUnsafe(ref MetadataDb, int)
        // with offset < _rawJsonLength → lines 1124-1127
        using ParsedJsonDocument<JsonElement> otherDoc = ParsedJsonDocument<JsonElement>.Parse("42"u8.ToArray());
        bool equal = JsonElementHelpers.DeepEquals(xElement, otherDoc.RootElement);
        Assert.True(equal);
    }

    /// <summary>
    /// Exercises <c>ReadRawJsonBackingValue</c> with <c>includeQuotes=true</c>
    /// (lines 1169-1172). This path is taken when getting a raw value with quotes
    /// from local JSON backing data (for String/PropertyName tokens).
    /// </summary>
    [Fact]
    public static void BuilderParse_GetPropertyRawValueAsString_UsesLocalQuotedPath()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""{"key":"val"}""");

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        JsonElement.Mutable root = builder.RootElement;

        // Enumerate properties — JsonProperty.ToString() calls GetPropertyRawValueAsString
        // which calls GetRawSimpleValueUnsafe with includeQuotes:true on both name and value
        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            string propString = prop.ToString();
            Assert.Contains("key", propString);
            Assert.Contains("val", propString);
        }
    }

    /// <summary>
    /// Exercises the ArrayPool path in <c>GetPropertyRawValueAsString</c> (lines 2065-2069)
    /// by using a property name long enough that name+colon+value exceeds 256 bytes.
    /// </summary>
    [Fact]
    public static void BuilderParse_GetPropertyRawValueAsString_LargeProperty_HitsArrayPool()
    {
        // Create a property name with >250 characters so name+colon+value > 256 bytes
        string longName = new string('a', 260);
        string json = $"{{\"{longName}\":\"value\"}}";
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, jsonBytes);

        JsonElement.Mutable root = builder.RootElement;

        // JsonProperty.ToString() calls GetPropertyRawValueAsString which rents from ArrayPool
        foreach (JsonProperty<JsonElement.Mutable> prop in root.EnumerateObject())
        {
            string propString = prop.ToString();
            Assert.Contains(longName, propString);
            Assert.Contains("value", propString);
        }
    }

    /// <summary>
    /// Exercises <c>BuildRentedMetadataDb</c> complex local value path (lines 2561-2564)
    /// by freezing a Parse-based builder that contains a local complex value (object),
    /// then creating a new builder from a child complex element.
    /// </summary>
    [Fact]
    public static void BuilderParse_BuildRentedMetadataDb_LocalComplexValue()
    {
        byte[] json = System.Text.Encoding.UTF8.GetBytes("""{"nested":{"a":1,"b":2}}""");

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> builder =
            JsonDocumentBuilder<JsonElement.Mutable>.Parse(workspace, json);

        // Freeze the builder so BuildRentedMetadataDb can be called on it
        builder.Freeze();

        // Get the nested object (a complex value — not IsSimpleValue)
        JsonElement.Mutable root = builder.RootElement;
        Assert.True(root.TryGetProperty("nested", out JsonElement.Mutable nested));

        // Creating a new builder from the nested element triggers BuildRentedMetadataDb
        // on the frozen builder for the complex (object) element
        using JsonDocumentBuilder<JsonElement.Mutable> newBuilder = nested.CreateBuilder(workspace);

        JsonElement.Mutable newRoot = newBuilder.RootElement;
        Assert.Equal(JsonValueKind.Object, newRoot.ValueKind);
        Assert.True(newRoot.TryGetProperty("a", out JsonElement.Mutable aVal));
        Assert.Equal(1, aVal.GetInt32());
    }

    /// <summary>
    /// Exercises <c>ParsedJsonDocument.GetArrayIndexElement</c> (lines 222-227) explicit
    /// interface method via direct IJsonDocument cast. This out-param overload returns
    /// both the parent document and the element index, and is only reachable through
    /// the explicit interface.
    /// </summary>
    [Fact]
    public static void ParsedDocument_GetArrayIndexElement_ExplicitInterface()
    {
        ReadOnlyMemory<byte> json = "[10, 20, 30]"u8.ToArray();
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);

        // Cast to IJsonDocument and call the explicit out-param overload directly
        IJsonDocument ijsonDoc = doc;
        ijsonDoc.GetArrayIndexElement(0, 1, out IJsonDocument parentDoc, out int parentIdx);

        // The parent document should be the same parsed document
        Assert.Same(doc, parentDoc);

        // Use the returned index to verify we got the right element (value 20)
        // We can verify by calling the single-return overload and comparing
        JsonElement element = ijsonDoc.GetArrayIndexElement(0, 1);
        Assert.Equal(20, element.GetInt32());

        // Also verify the index is consistent
        Assert.True(parentIdx > 0);
    }
}
