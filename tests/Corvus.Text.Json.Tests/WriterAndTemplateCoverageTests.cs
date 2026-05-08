// <copyright file="WriterAndTemplateCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Linq;
using System.Text;
using Corvus.Text.Json.Compatibility;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using NodaTime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Coverage tests for Utf8JsonWriter minimized paths, Utf8UriTemplate Unicode paths,
/// and PeriodBuilder indexer.
/// </summary>
[TestClass]
public class WriterAndTemplateCoverageTests
{
    #region Utf8JsonWriter Minimized Paths

    [TestMethod]
    public void WriteGuidMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteStringValue(System.Guid.Parse("12345678-1234-1234-1234-123456789abc"));
        writer.WriteStringValue(System.Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"));
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.AreEqual("""["12345678-1234-1234-1234-123456789abc","aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"]""", result);
    }

    [TestMethod]
    public void WriteDecimalMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(123.456m);
        writer.WriteNumberValue(789.012m);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.AreEqual("[123.456,789.012]", result);
    }

    [TestMethod]
    public void WriteFloatMinimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(1.5f);
        writer.WriteNumberValue(2.5f);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.AreEqual("[1.5,2.5]", result);
    }

    [TestMethod]
    public void WriteUInt64Minimized()
    {
        var output = new ArrayBufferWriter<byte>();
        using var writer = new Utf8JsonWriter(output, new JsonWriterOptions { SkipValidation = true });

        writer.WriteStartArray();
        writer.WriteNumberValue(18446744073709551615UL);
        writer.WriteNumberValue(42UL);
        writer.WriteEndArray();
        writer.Flush();

        string result = JsonReaderHelper.TranscodeHelper(output.WrittenSpan);
        Assert.AreEqual("[18446744073709551615,42]", result);
    }

    #endregion

    #region Utf8UriTemplate Unicode (ParseUcsChar is dead code — lookup table handles all > 0x7F)

    [TestMethod]
    [DataRow("caf\u00e9", true)]                  // é = U+00E9 (2-byte UTF-8: 0xC3 0xA9)
    [DataRow("\u00A0literal", true)]              // U+00A0 = non-breaking space
    [DataRow("path/\u4e2d\u6587/file", true)]    // Chinese characters
    [DataRow("\uFDF0end", true)]                  // U+FDF0
    public void Validate_UnicodeInLiterals_Valid(string template, bool expected)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(template);
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.AreEqual(expected, result);
    }

    [TestMethod]
    public void Validate_SupplementaryCodePoint_Valid()
    {
        // U+10000 (LINEAR B SYLLABLE B008 A) — 4-byte UTF-8: F0 90 80 80
        byte[] utf8 = [0x70, 0x61, 0x74, 0x68, 0xF0, 0x90, 0x80, 0x80]; // "path" + U+10000
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_HighBytesAcceptedByLookupTable()
    {
        // The IsLiteralLookup table accepts all bytes >= 0x80 individually,
        // so even a lone continuation byte is accepted as a valid literal.
        byte[] utf8 = [0x70, 0x61, 0x74, 0x68, 0x80]; // "path" + lone continuation byte
        bool result = Utf8UriTemplate.Validate(utf8);
        Assert.IsTrue(result);
    }

    #endregion

    #region PeriodBuilder Indexer

    [TestMethod]
    [DataRow(PeriodUnits.Years, 5)]
    [DataRow(PeriodUnits.Months, 3)]
    [DataRow(PeriodUnits.Weeks, 2)]
    [DataRow(PeriodUnits.Days, 10)]
    [DataRow(PeriodUnits.Hours, 8)]
    [DataRow(PeriodUnits.Minutes, 30)]
    [DataRow(PeriodUnits.Seconds, 45)]
    [DataRow(PeriodUnits.Milliseconds, 500)]
    [DataRow(PeriodUnits.Ticks, 1000)]
    [DataRow(PeriodUnits.Nanoseconds, 999)]
    public void PeriodBuilder_IndexerGetter_ReturnsCorrectValue(PeriodUnits unit, long value)
    {
        var builder = new PeriodBuilder();
        builder[unit] = value;
        Assert.AreEqual(value, builder[unit]);
    }

    [TestMethod]
    public void PeriodBuilder_IndexerGetter_InvalidUnit_Throws()
    {
        var builder = new PeriodBuilder();
        Assert.ThrowsExactly<System.ArgumentOutOfRangeException>(() => builder[(PeriodUnits)0]);
    }

    #endregion

    #region Period.Equals(NodaTime.Period)

    [TestMethod]
    public void Period_Equals_NodaTimePeriod_EqualValues()
    {
        var corvusPeriod = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        NodaTime.Period nodaPeriod = new NodaTime.PeriodBuilder
        {
            Years = 1,
            Months = 2,
            Weeks = 3,
            Days = 4,
            Hours = 5,
            Minutes = 6,
            Seconds = 7,
            Milliseconds = 8,
            Ticks = 9,
            Nanoseconds = 10,
        }.Build();

        Assert.IsTrue(corvusPeriod.Equals(nodaPeriod));
    }

    [TestMethod]
    public void Period_Equals_NodaTimePeriod_DifferentValues()
    {
        var corvusPeriod = new Period(1, 2, 3, 4, 5, 6, 7, 8, 9, 10);
        NodaTime.Period nodaPeriod = new NodaTime.PeriodBuilder
        {
            Years = 1,
            Months = 2,
            Weeks = 3,
            Days = 4,
            Hours = 5,
            Minutes = 6,
            Seconds = 7,
            Milliseconds = 8,
            Ticks = 9,
            Nanoseconds = 99,
        }.Build();

        Assert.IsFalse(corvusPeriod.Equals(nodaPeriod));
    }

    #endregion

    #region ParsedJsonDocument IJsonDocument TryGetNamedPropertyValue (char overload with elementParent)

    [TestMethod]
    public void ParsedJsonDocument_IJsonDocument_TryGetNamedPropertyValue_Char_Found()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"hello","value":42}"""u8.ToArray());
        IJsonDocument iDoc = doc;

        bool found = iDoc.TryGetNamedPropertyValue(0, "name".AsSpan(), out IJsonDocument? parent, out int idx);
        Assert.IsTrue(found);
        Assert.AreSame(doc, parent);
        Assert.IsTrue(idx > 0);
    }

    [TestMethod]
    public void ParsedJsonDocument_IJsonDocument_TryGetNamedPropertyValue_Char_NotFound()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"hello"}"""u8.ToArray());
        IJsonDocument iDoc = doc;

        bool found = iDoc.TryGetNamedPropertyValue(0, "missing".AsSpan(), out IJsonDocument? parent, out int idx);
        Assert.IsFalse(found);
        Assert.IsNull(parent);
        Assert.AreEqual(-1, idx);
    }

    #endregion

    #region JsonWorkspace.Reset()

    [TestMethod]
    public void JsonWorkspace_Reset_ClearsDocuments()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"a":1}"""u8.ToArray());

        using JsonDocumentBuilder<JsonElement.Mutable> builder = sourceDoc.RootElement.CreateBuilder(workspace);

        // Reset clears the workspace state
        workspace.Reset();

        // After reset, workspace can be reused for new builders
        using ParsedJsonDocument<JsonElement> sourceDoc2 = ParsedJsonDocument<JsonElement>.Parse("""{"b":2}"""u8.ToArray());
        using JsonDocumentBuilder<JsonElement.Mutable> builder2 = sourceDoc2.RootElement.CreateBuilder(workspace);
        Assert.AreEqual("""{"b":2}""", builder2.RootElement.ToString());
    }

    #endregion

    #region ValidationContext.Results (non-null collector path)

    [TestMethod]
    public void ValidationContext_Results_WithCollector_ReturnsResults()
    {
        // Validate an invalid instance to produce validation results with a non-empty collector
        using var doc = ParsedJsonDocument<ClosedObjectNoPatterns>.Parse("{}");

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        // Access Results to trigger BuildResults with non-null collector containing failures
        var results = result.Results;
        Assert.IsNotNull(results);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue((results).Any());
    }

    [TestMethod]
    public void ValidationContext_Results_NullCollector_ReturnsEmpty()
    {
        // The static ValidContext has no collector — should return empty
        var results = ValidationContext.ValidContext.Results;
        Assert.IsNotNull(results);
        Assert.AreEqual(0, (results).Count);
    }

    #endregion

    #region Utf8JsonWriterCache.RentWriter / ReturnWriter

    [TestMethod]
    public void JsonWorkspace_RentWriter_ReturnWriter()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        var bufferWriter = new ArrayBufferWriter<byte>();

        Utf8JsonWriter writer = workspace.RentWriter(bufferWriter);
        writer.WriteStartObject();
        writer.WriteNumber("x"u8, 42);
        writer.WriteEndObject();
        writer.Flush();

        workspace.ReturnWriter(writer);

        string result = JsonReaderHelper.TranscodeHelper(bufferWriter.WrittenSpan);
        Assert.AreEqual("""{"x":42}""", result);
    }

    #endregion

    #region ParsedJsonDocument generic TryGetNamedPropertyValue<TElement>(char overload)

    [TestMethod]
    public void ParsedJsonDocument_GenericTryGetNamedPropertyValue_Char_Found()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"hello","value":42}"""u8.ToArray());
        IJsonDocument iDoc = doc;

        bool found = iDoc.TryGetNamedPropertyValue<JsonElement>(0, "name".AsSpan(), out JsonElement value);
        Assert.IsTrue(found);
        Assert.AreEqual("hello", value.GetString());
    }

    [TestMethod]
    public void ParsedJsonDocument_GenericTryGetNamedPropertyValue_Char_NotFound()
    {
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"hello"}"""u8.ToArray());
        IJsonDocument iDoc = doc;

        bool found = iDoc.TryGetNamedPropertyValue<JsonElement>(0, "missing".AsSpan(), out JsonElement value);
        Assert.IsFalse(found);
        Assert.AreEqual(JsonValueKind.Undefined, value.ValueKind);
    }

    #endregion

    #region Format validation error messages at Detailed level

    [TestMethod]
    [DataRow("""{"date":"not-a-date"}""")]
    [DataRow("""{"dateTime":"not-a-datetime"}""")]
    [DataRow("""{"time":"not-a-time"}""")]
    [DataRow("""{"duration":"not-a-duration"}""")]
    [DataRow("""{"email":"not an email"}""")]
    [DataRow("""{"hostname":"not a host!name"}""")]
    [DataRow("""{"idnEmail":"not an email"}""")]
    [DataRow("""{"uuid":"not-a-uuid"}""")]
    [DataRow("""{"uri":":::not a uri"}""")]
    [DataRow("""{"uriReference":":::not valid"}""")]
    [DataRow("""{"iri":":::not a iri"}""")]
    [DataRow("""{"iriReference":":::not valid"}""")]
    [DataRow("""{"jsonPointer":"no-leading-slash"}""")]
    [DataRow("""{"relativeJsonPointer":"not/valid"}""")]
    [DataRow("""{"regex":"[invalid"}""")]
    public void FormatTypes_StringFormat_InvalidValue_ProducesDetailedError(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue((result.Results).Any());
    }

    [TestMethod]
    [DataRow("""{"byte":999}""")]
    [DataRow("""{"uint16":99999}""")]
    [DataRow("""{"uint32":5000000000}""")]
    [DataRow("""{"uint64":-1}""")]
    [DataRow("""{"uint128":-1}""")]
    [DataRow("""{"sbyte":200}""")]
    [DataRow("""{"int16":40000}""")]
    [DataRow("""{"int32":3000000000}""")]
    [DataRow("""{"int64":1.5}""")]
    [DataRow("""{"int128":1.5}""")]
    [DataRow("""{"half":100000}""")]
    [DataRow("""{"single":3.5e39}""")]
    [DataRow("""{"double":1.8e309}""")]
    [DataRow("""{"decimal":1e30}""")]
    public void FormatTypes_NumericFormat_InvalidValue_ProducesDetailedError(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
        Assert.IsTrue((result.Results).Any());
    }

    #endregion

    #region Int64/Int128 format validation correctness (BUG: codegen was calling MatchUInt64/MatchUInt128)

    [TestMethod]
    [DataRow("""{"int64":-1}""")]
    [DataRow("""{"int64":-9223372036854775808}""")]
    [DataRow("""{"int64":9223372036854775807}""")]
    public void FormatTypes_Int64_AcceptsNegativeAndMaxRange(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("""{"int64":9223372036854775808}""")]
    [DataRow("""{"int64":18446744073709551615}""")]
    public void FormatTypes_Int64_RejectsAboveMaxInt64(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    [DataRow("""{"int128":-1}""")]
    [DataRow("""{"int128":-170141183460469231731687303715884105728}""")]
    [DataRow("""{"int128":170141183460469231731687303715884105727}""")]
    public void FormatTypes_Int128_AcceptsNegativeAndMaxRange(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    [DataRow("""{"int128":170141183460469231731687303715884105728}""")]
    public void FormatTypes_Int128_RejectsAboveMaxInt128(string json)
    {
        using var doc = ParsedJsonDocument<FormatTypes>.Parse(Encoding.UTF8.GetBytes(json));

        ValidationContext result = doc.RootElement.Validate(
            ValidationContext.ValidContext,
            ValidationLevel.Detailed);

        Assert.IsFalse(result.IsValid);
    }

    #endregion

    #region GetHashCodeForString with long strings (>2048 chars triggers ArrayPool path)

    [TestMethod]
    public void GetHashCode_LongString_UsesArrayPoolPath()
    {
        // Build a JSON string with >2048 characters to exercise the ArrayPool<char> rent path
        // in JsonDocument.GetHashCodeForString
        string longValue = new string('a', 2100);
        string json = $$"""{"key":"{{longValue}}"}""";
        using var doc = ParsedJsonDocument<JsonElement>.Parse(Encoding.UTF8.GetBytes(json));

        JsonElement root = doc.RootElement;
        JsonElement value = root.GetProperty("key"u8);

        // Calling GetHashCode exercises the GetHashCodeForString path
        int hash = value.GetHashCode();
        Assert.AreNotEqual(0, hash);
    }

    #endregion
}
