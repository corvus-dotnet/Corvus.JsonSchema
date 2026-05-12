// <copyright file="ParseValueTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using System.Text;
using Corvus.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.ParseValue;

/// <summary>
/// Tests for ParseValue.
/// </summary>
[TestClass]
public class ParseValueTests
{
    [TestMethod]
    public void Utf8Span_JsonBoolean_true()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("true");
        IJsonValue result = JsonBoolean.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonBoolean_false()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("false");
        IJsonValue result = JsonBoolean.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonNull_null()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("null");
        IJsonValue result = JsonNull.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonNumber_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonNumber.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonNumber_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        IJsonValue result = JsonNumber.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonInteger_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonNumber.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonSingle_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        IJsonValue result = JsonSingle.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonDouble_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        IJsonValue result = JsonDouble.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonDecimal_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        IJsonValue result = JsonDecimal.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonByte.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonSByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonSByte.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonInt16.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonUInt16.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonInt32.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonUInt32.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonInt64.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        IJsonValue result = JsonUInt64.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonString_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonString.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonAny_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonAny.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonArray_123_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"[123,""Hello""]");
        IJsonValue result = JsonArray.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonObject_foo_123_bar_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        IJsonValue result = JsonArray.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonBase64Content_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonBase64Content.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonBase64ContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonBase64ContentPre201909.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonBase64String_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonBase64String.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonBase64StringPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonBase64StringPre201909.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonContent_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonContent.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonContentPre201909.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonDate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonDate.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonDateTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonDateTime.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonDuration_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonDuration.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonEmail.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonHostname.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIdnEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIdnEmail.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIdnHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIdnHostname.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIpV4_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIpV4.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIpV6_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIpV6.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIri.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonIriReference_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonIriReference.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonPointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonPointer.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonRegex_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonRegex.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonRelativePointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonRelativePointer.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonTime.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonUri.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUriTemplate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonUriTemplate.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_JsonUuid_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        IJsonValue result = JsonUuid.ParseValue(utf8bytes);
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBoolean_true()
    {
        IJsonValue result = JsonBoolean.ParseValue("true");
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBoolean_false()
    {
        IJsonValue result = JsonBoolean.ParseValue("false");
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonNull_null()
    {
        IJsonValue result = JsonNull.ParseValue("null");
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonString_Hello()
    {
        IJsonValue result = JsonString.ParseValue(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonNumber_123()
    {
        IJsonValue result = JsonNumber.ParseValue("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonNumber_123_4()
    {
        IJsonValue result = JsonNumber.ParseValue("123.4");
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonInteger_123()
    {
        IJsonValue result = JsonNumber.ParseValue("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonSingle_123_4()
    {
        IJsonValue result = JsonSingle.Parse("123.4");
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonDouble_123_4()
    {
        IJsonValue result = JsonDouble.Parse("123.4");
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonDecimal_123_4()
    {
        IJsonValue result = JsonDecimal.Parse("123.4");
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonByte_123()
    {
        IJsonValue result = JsonByte.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonSByte_123()
    {
        IJsonValue result = JsonSByte.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonInt16_123()
    {
        IJsonValue result = JsonInt16.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUInt16_123()
    {
        IJsonValue result = JsonUInt16.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonInt32_123()
    {
        IJsonValue result = JsonInt32.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUInt32_123()
    {
        IJsonValue result = JsonUInt32.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonInt64_123()
    {
        IJsonValue result = JsonInt64.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUInt64_123()
    {
        IJsonValue result = JsonUInt64.Parse("123");
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonAny_Hello()
    {
        IJsonValue result = JsonAny.ParseValue(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonArray_123_Hello()
    {
        IJsonValue result = JsonArray.ParseValue(@"[123,""Hello""]");
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonObject_foo_123_bar_Hello()
    {
        IJsonValue result = JsonArray.ParseValue(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBase64Content_Hello()
    {
        IJsonValue result = JsonBase64Content.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBase64ContentPre201909_Hello()
    {
        IJsonValue result = JsonBase64ContentPre201909.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBase64String_Hello()
    {
        IJsonValue result = JsonBase64String.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonBase64StringPre201909_Hello()
    {
        IJsonValue result = JsonBase64StringPre201909.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonContent_Hello()
    {
        IJsonValue result = JsonContent.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonContentPre201909_Hello()
    {
        IJsonValue result = JsonContentPre201909.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonDate_Hello()
    {
        IJsonValue result = JsonDate.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonDateTime_Hello()
    {
        IJsonValue result = JsonDateTime.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonDuration_Hello()
    {
        IJsonValue result = JsonDuration.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonEmail_Hello()
    {
        IJsonValue result = JsonEmail.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonHostname_Hello()
    {
        IJsonValue result = JsonHostname.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIdnEmail_Hello()
    {
        IJsonValue result = JsonIdnEmail.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIdnHostname_Hello()
    {
        IJsonValue result = JsonIdnHostname.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIpV4_Hello()
    {
        IJsonValue result = JsonIpV4.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIpV6_Hello()
    {
        IJsonValue result = JsonIpV6.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIri_Hello()
    {
        IJsonValue result = JsonIri.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonIriReference_Hello()
    {
        IJsonValue result = JsonIriReference.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonPointer_Hello()
    {
        IJsonValue result = JsonPointer.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonRegex_Hello()
    {
        IJsonValue result = JsonRegex.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonRelativePointer_Hello()
    {
        IJsonValue result = JsonRelativePointer.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonTime_Hello()
    {
        IJsonValue result = JsonTime.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUri_Hello()
    {
        IJsonValue result = JsonUri.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUriTemplate_Hello()
    {
        IJsonValue result = JsonUriTemplate.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_JsonUuid_Hello()
    {
        IJsonValue result = JsonUuid.Parse(@"""Hello""");
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBoolean_true()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("true");
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBoolean_false()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("false");
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonNull_null()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("null");
        using ParsedValue<JsonNull> parsed = ParsedValue<JsonNull>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonNumber_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonNumber_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonInteger_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonString_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonString> parsed = ParsedValue<JsonString>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonSingle_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonSingle> parsed = ParsedValue<JsonSingle>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonDouble_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonDouble> parsed = ParsedValue<JsonDouble>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonDecimal_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonDecimal> parsed = ParsedValue<JsonDecimal>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonByte> parsed = ParsedValue<JsonByte>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonSByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonSByte> parsed = ParsedValue<JsonSByte>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt16> parsed = ParsedValue<JsonInt16>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt16> parsed = ParsedValue<JsonUInt16>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt32> parsed = ParsedValue<JsonInt32>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt32> parsed = ParsedValue<JsonUInt32>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt64> parsed = ParsedValue<JsonInt64>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt64> parsed = ParsedValue<JsonUInt64>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonAny_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonAny> parsed = ParsedValue<JsonAny>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonArray_123_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"[123,""Hello""]");
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonObject_foo_123_bar_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBase64Content_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64Content> parsed = ParsedValue<JsonBase64Content>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBase64ContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64ContentPre201909> parsed = ParsedValue<JsonBase64ContentPre201909>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBase64String_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64String> parsed = ParsedValue<JsonBase64String>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonBase64StringPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64StringPre201909> parsed = ParsedValue<JsonBase64StringPre201909>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonContent_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonContent> parsed = ParsedValue<JsonContent>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonContentPre201909> parsed = ParsedValue<JsonContentPre201909>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonDate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDate> parsed = ParsedValue<JsonDate>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonDateTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDateTime> parsed = ParsedValue<JsonDateTime>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonDuration_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDuration> parsed = ParsedValue<JsonDuration>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonEmail> parsed = ParsedValue<JsonEmail>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonHostname> parsed = ParsedValue<JsonHostname>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIdnEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIdnEmail> parsed = ParsedValue<JsonIdnEmail>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIdnHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIdnHostname> parsed = ParsedValue<JsonIdnHostname>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIpV4_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIpV4> parsed = ParsedValue<JsonIpV4>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIpV6_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIpV6> parsed = ParsedValue<JsonIpV6>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIri> parsed = ParsedValue<JsonIri>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonIriReference_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIriReference> parsed = ParsedValue<JsonIriReference>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonPointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonPointer> parsed = ParsedValue<JsonPointer>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonRegex_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonRegex> parsed = ParsedValue<JsonRegex>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonRelativePointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonRelativePointer> parsed = ParsedValue<JsonRelativePointer>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonTime> parsed = ParsedValue<JsonTime>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUri> parsed = ParsedValue<JsonUri>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUriTemplate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUriTemplate> parsed = ParsedValue<JsonUriTemplate>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Span_ParsedValue_JsonUuid_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUuid> parsed = ParsedValue<JsonUuid>.Parse(utf8bytes);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBoolean_true()
    {
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse("true");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBoolean_false()
    {
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse("false");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonNull_null()
    {
        using ParsedValue<JsonNull> parsed = ParsedValue<JsonNull>.Parse("null");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonNumber_123()
    {
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonNumber_123_4()
    {
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse("123.4");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonInteger_123()
    {
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonString_Hello()
    {
        using ParsedValue<JsonString> parsed = ParsedValue<JsonString>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonSingle_123_4()
    {
        using ParsedValue<JsonSingle> parsed = ParsedValue<JsonSingle>.Parse("123.4");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonDouble_123_4()
    {
        using ParsedValue<JsonDouble> parsed = ParsedValue<JsonDouble>.Parse("123.4");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonDecimal_123_4()
    {
        using ParsedValue<JsonDecimal> parsed = ParsedValue<JsonDecimal>.Parse("123.4");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonByte_123()
    {
        using ParsedValue<JsonByte> parsed = ParsedValue<JsonByte>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonSByte_123()
    {
        using ParsedValue<JsonSByte> parsed = ParsedValue<JsonSByte>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonInt16_123()
    {
        using ParsedValue<JsonInt16> parsed = ParsedValue<JsonInt16>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUInt16_123()
    {
        using ParsedValue<JsonUInt16> parsed = ParsedValue<JsonUInt16>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonInt32_123()
    {
        using ParsedValue<JsonInt32> parsed = ParsedValue<JsonInt32>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUInt32_123()
    {
        using ParsedValue<JsonUInt32> parsed = ParsedValue<JsonUInt32>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonInt64_123()
    {
        using ParsedValue<JsonInt64> parsed = ParsedValue<JsonInt64>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUInt64_123()
    {
        using ParsedValue<JsonUInt64> parsed = ParsedValue<JsonUInt64>.Parse("123");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonAny_Hello()
    {
        using ParsedValue<JsonAny> parsed = ParsedValue<JsonAny>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonArray_123_Hello()
    {
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(@"[123,""Hello""]");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonObject_foo_123_bar_Hello()
    {
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBase64Content_Hello()
    {
        using ParsedValue<JsonBase64Content> parsed = ParsedValue<JsonBase64Content>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBase64ContentPre201909_Hello()
    {
        using ParsedValue<JsonBase64ContentPre201909> parsed = ParsedValue<JsonBase64ContentPre201909>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBase64String_Hello()
    {
        using ParsedValue<JsonBase64String> parsed = ParsedValue<JsonBase64String>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonBase64StringPre201909_Hello()
    {
        using ParsedValue<JsonBase64StringPre201909> parsed = ParsedValue<JsonBase64StringPre201909>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonContent_Hello()
    {
        using ParsedValue<JsonContent> parsed = ParsedValue<JsonContent>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonContentPre201909_Hello()
    {
        using ParsedValue<JsonContentPre201909> parsed = ParsedValue<JsonContentPre201909>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonDate_Hello()
    {
        using ParsedValue<JsonDate> parsed = ParsedValue<JsonDate>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonDateTime_Hello()
    {
        using ParsedValue<JsonDateTime> parsed = ParsedValue<JsonDateTime>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonDuration_Hello()
    {
        using ParsedValue<JsonDuration> parsed = ParsedValue<JsonDuration>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonEmail_Hello()
    {
        using ParsedValue<JsonEmail> parsed = ParsedValue<JsonEmail>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonHostname_Hello()
    {
        using ParsedValue<JsonHostname> parsed = ParsedValue<JsonHostname>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIdnEmail_Hello()
    {
        using ParsedValue<JsonIdnEmail> parsed = ParsedValue<JsonIdnEmail>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIdnHostname_Hello()
    {
        using ParsedValue<JsonIdnHostname> parsed = ParsedValue<JsonIdnHostname>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIpV4_Hello()
    {
        using ParsedValue<JsonIpV4> parsed = ParsedValue<JsonIpV4>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIpV6_Hello()
    {
        using ParsedValue<JsonIpV6> parsed = ParsedValue<JsonIpV6>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIri_Hello()
    {
        using ParsedValue<JsonIri> parsed = ParsedValue<JsonIri>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonIriReference_Hello()
    {
        using ParsedValue<JsonIriReference> parsed = ParsedValue<JsonIriReference>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonPointer_Hello()
    {
        using ParsedValue<JsonPointer> parsed = ParsedValue<JsonPointer>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonRegex_Hello()
    {
        using ParsedValue<JsonRegex> parsed = ParsedValue<JsonRegex>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonRelativePointer_Hello()
    {
        using ParsedValue<JsonRelativePointer> parsed = ParsedValue<JsonRelativePointer>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonTime_Hello()
    {
        using ParsedValue<JsonTime> parsed = ParsedValue<JsonTime>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUri_Hello()
    {
        using ParsedValue<JsonUri> parsed = ParsedValue<JsonUri>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUriTemplate_Hello()
    {
        using ParsedValue<JsonUriTemplate> parsed = ParsedValue<JsonUriTemplate>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharSpan_ParsedValue_JsonUuid_Hello()
    {
        using ParsedValue<JsonUuid> parsed = ParsedValue<JsonUuid>.Parse(@"""Hello""");
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBoolean_true()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("true");
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBoolean_false()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("false");
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonNull_null()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("null");
        using ParsedValue<JsonNull> parsed = ParsedValue<JsonNull>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonNumber_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonNumber_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonInteger_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonSingle_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonSingle> parsed = ParsedValue<JsonSingle>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonDouble_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonDouble> parsed = ParsedValue<JsonDouble>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonDecimal_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using ParsedValue<JsonDecimal> parsed = ParsedValue<JsonDecimal>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonByte> parsed = ParsedValue<JsonByte>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonSByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonSByte> parsed = ParsedValue<JsonSByte>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt16> parsed = ParsedValue<JsonInt16>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt16> parsed = ParsedValue<JsonUInt16>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt32> parsed = ParsedValue<JsonInt32>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt32> parsed = ParsedValue<JsonUInt32>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonInt64> parsed = ParsedValue<JsonInt64>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using ParsedValue<JsonUInt64> parsed = ParsedValue<JsonUInt64>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonString_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonString> parsed = ParsedValue<JsonString>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonAny_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonAny> parsed = ParsedValue<JsonAny>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonArray_123_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"[123,""Hello""]");
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonObject_foo_123_bar_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBase64Content_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64Content> parsed = ParsedValue<JsonBase64Content>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBase64ContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64ContentPre201909> parsed = ParsedValue<JsonBase64ContentPre201909>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBase64String_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64String> parsed = ParsedValue<JsonBase64String>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonBase64StringPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonBase64StringPre201909> parsed = ParsedValue<JsonBase64StringPre201909>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonContent_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonContent> parsed = ParsedValue<JsonContent>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonContentPre201909> parsed = ParsedValue<JsonContentPre201909>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonDate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDate> parsed = ParsedValue<JsonDate>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonDateTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDateTime> parsed = ParsedValue<JsonDateTime>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonDuration_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonDuration> parsed = ParsedValue<JsonDuration>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonEmail> parsed = ParsedValue<JsonEmail>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonHostname> parsed = ParsedValue<JsonHostname>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIdnEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIdnEmail> parsed = ParsedValue<JsonIdnEmail>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIdnHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIdnHostname> parsed = ParsedValue<JsonIdnHostname>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIpV4_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIpV4> parsed = ParsedValue<JsonIpV4>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIpV6_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIpV6> parsed = ParsedValue<JsonIpV6>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIri> parsed = ParsedValue<JsonIri>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonIriReference_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonIriReference> parsed = ParsedValue<JsonIriReference>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonPointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonPointer> parsed = ParsedValue<JsonPointer>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonRegex_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonRegex> parsed = ParsedValue<JsonRegex>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonRelativePointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonRelativePointer> parsed = ParsedValue<JsonRelativePointer>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonTime> parsed = ParsedValue<JsonTime>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUri> parsed = ParsedValue<JsonUri>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUriTemplate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUriTemplate> parsed = ParsedValue<JsonUriTemplate>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Memory_ParsedValue_JsonUuid_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using ParsedValue<JsonUuid> parsed = ParsedValue<JsonUuid>.Parse(utf8bytes.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBoolean_true()
    {
        string charValue = "true";
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBoolean_false()
    {
        string charValue = "false";
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonNull_null()
    {
        string charValue = "null";
        using ParsedValue<JsonNull> parsed = ParsedValue<JsonNull>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonNumber_123()
    {
        string charValue = "123";
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonNumber_123_4()
    {
        string charValue = "123.4";
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonInteger_123()
    {
        string charValue = "123";
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonSingle_123_4()
    {
        string charValue = "123.4";
        using ParsedValue<JsonSingle> parsed = ParsedValue<JsonSingle>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonDouble_123_4()
    {
        string charValue = "123.4";
        using ParsedValue<JsonDouble> parsed = ParsedValue<JsonDouble>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonDecimal_123_4()
    {
        string charValue = "123.4";
        using ParsedValue<JsonDecimal> parsed = ParsedValue<JsonDecimal>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonByte_123()
    {
        string charValue = "123";
        using ParsedValue<JsonByte> parsed = ParsedValue<JsonByte>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonSByte_123()
    {
        string charValue = "123";
        using ParsedValue<JsonSByte> parsed = ParsedValue<JsonSByte>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonInt16_123()
    {
        string charValue = "123";
        using ParsedValue<JsonInt16> parsed = ParsedValue<JsonInt16>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUInt16_123()
    {
        string charValue = "123";
        using ParsedValue<JsonUInt16> parsed = ParsedValue<JsonUInt16>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonInt32_123()
    {
        string charValue = "123";
        using ParsedValue<JsonInt32> parsed = ParsedValue<JsonInt32>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUInt32_123()
    {
        string charValue = "123";
        using ParsedValue<JsonUInt32> parsed = ParsedValue<JsonUInt32>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonInt64_123()
    {
        string charValue = "123";
        using ParsedValue<JsonInt64> parsed = ParsedValue<JsonInt64>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUInt64_123()
    {
        string charValue = "123";
        using ParsedValue<JsonUInt64> parsed = ParsedValue<JsonUInt64>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonString_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonString> parsed = ParsedValue<JsonString>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonAny_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonAny> parsed = ParsedValue<JsonAny>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonArray_123_Hello()
    {
        string charValue = @"[123,""Hello""]";
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonObject_foo_123_bar_Hello()
    {
        string charValue = @"{ ""foo"": 123, ""bar"": ""Hello"" }";
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBase64Content_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonBase64Content> parsed = ParsedValue<JsonBase64Content>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBase64ContentPre201909_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonBase64ContentPre201909> parsed = ParsedValue<JsonBase64ContentPre201909>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBase64String_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonBase64String> parsed = ParsedValue<JsonBase64String>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonBase64StringPre201909_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonBase64StringPre201909> parsed = ParsedValue<JsonBase64StringPre201909>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonContent_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonContent> parsed = ParsedValue<JsonContent>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonContentPre201909_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonContentPre201909> parsed = ParsedValue<JsonContentPre201909>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonDate_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonDate> parsed = ParsedValue<JsonDate>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonDateTime_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonDateTime> parsed = ParsedValue<JsonDateTime>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonDuration_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonDuration> parsed = ParsedValue<JsonDuration>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonEmail_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonEmail> parsed = ParsedValue<JsonEmail>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonHostname_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonHostname> parsed = ParsedValue<JsonHostname>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIdnEmail_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIdnEmail> parsed = ParsedValue<JsonIdnEmail>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIdnHostname_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIdnHostname> parsed = ParsedValue<JsonIdnHostname>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIpV4_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIpV4> parsed = ParsedValue<JsonIpV4>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIpV6_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIpV6> parsed = ParsedValue<JsonIpV6>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIri_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIri> parsed = ParsedValue<JsonIri>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonIriReference_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonIriReference> parsed = ParsedValue<JsonIriReference>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonPointer_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonPointer> parsed = ParsedValue<JsonPointer>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonRegex_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonRegex> parsed = ParsedValue<JsonRegex>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonRelativePointer_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonRelativePointer> parsed = ParsedValue<JsonRelativePointer>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonTime_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonTime> parsed = ParsedValue<JsonTime>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUri_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonUri> parsed = ParsedValue<JsonUri>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUriTemplate_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonUriTemplate> parsed = ParsedValue<JsonUriTemplate>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void CharMemory_ParsedValue_JsonUuid_Hello()
    {
        string charValue = @"""Hello""";
        using ParsedValue<JsonUuid> parsed = ParsedValue<JsonUuid>.Parse(charValue.AsMemory());
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBoolean_true()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("true");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("true"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBoolean_false()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("false");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBoolean> parsed = ParsedValue<JsonBoolean>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("false"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonNull_null()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("null");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonNull> parsed = ParsedValue<JsonNull>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("null"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonNumber_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonNumber_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonInteger_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonNumber> parsed = ParsedValue<JsonNumber>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonSingle_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonSingle> parsed = ParsedValue<JsonSingle>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonDouble_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonDouble> parsed = ParsedValue<JsonDouble>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonDecimal_123_4()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123.4");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonDecimal> parsed = ParsedValue<JsonDecimal>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123.4"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonByte> parsed = ParsedValue<JsonByte>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonSByte_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonSByte> parsed = ParsedValue<JsonSByte>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonInt16> parsed = ParsedValue<JsonInt16>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUInt16_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUInt16> parsed = ParsedValue<JsonUInt16>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonInt32> parsed = ParsedValue<JsonInt32>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUInt32_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUInt32> parsed = ParsedValue<JsonUInt32>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonInt64> parsed = ParsedValue<JsonInt64>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUInt64_123()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes("123");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUInt64> parsed = ParsedValue<JsonUInt64>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse("123"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonString_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonString> parsed = ParsedValue<JsonString>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonAny_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonAny> parsed = ParsedValue<JsonAny>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonArray_123_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"[123,""Hello""]");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"[123, ""Hello""]"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonObject_foo_123_bar_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"{ ""foo"": 123, ""bar"": ""Hello"" }");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonArray> parsed = ParsedValue<JsonArray>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"{ ""foo"": 123, ""bar"": ""Hello"" }"), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBase64Content_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBase64Content> parsed = ParsedValue<JsonBase64Content>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBase64ContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBase64ContentPre201909> parsed = ParsedValue<JsonBase64ContentPre201909>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBase64String_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBase64String> parsed = ParsedValue<JsonBase64String>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonBase64StringPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonBase64StringPre201909> parsed = ParsedValue<JsonBase64StringPre201909>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonContent_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonContent> parsed = ParsedValue<JsonContent>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonContentPre201909_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonContentPre201909> parsed = ParsedValue<JsonContentPre201909>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonDate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonDate> parsed = ParsedValue<JsonDate>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonDateTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonDateTime> parsed = ParsedValue<JsonDateTime>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonDuration_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonDuration> parsed = ParsedValue<JsonDuration>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonEmail> parsed = ParsedValue<JsonEmail>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonHostname> parsed = ParsedValue<JsonHostname>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIdnEmail_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIdnEmail> parsed = ParsedValue<JsonIdnEmail>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIdnHostname_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIdnHostname> parsed = ParsedValue<JsonIdnHostname>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIpV4_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIpV4> parsed = ParsedValue<JsonIpV4>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIpV6_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIpV6> parsed = ParsedValue<JsonIpV6>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIri> parsed = ParsedValue<JsonIri>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonIriReference_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonIriReference> parsed = ParsedValue<JsonIriReference>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonPointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonPointer> parsed = ParsedValue<JsonPointer>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonRegex_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonRegex> parsed = ParsedValue<JsonRegex>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonRelativePointer_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonRelativePointer> parsed = ParsedValue<JsonRelativePointer>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonTime_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonTime> parsed = ParsedValue<JsonTime>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUri_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUri> parsed = ParsedValue<JsonUri>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUriTemplate_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUriTemplate> parsed = ParsedValue<JsonUriTemplate>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }

    [TestMethod]
    public void Utf8Stream_ParsedValue_JsonUuid_Hello()
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(@"""Hello""");
        using MemoryStream stream = new(utf8bytes);
        using ParsedValue<JsonUuid> parsed = ParsedValue<JsonUuid>.Parse(stream);
        IJsonValue result = parsed.Instance;
        Assert.AreEqual(JsonAny.Parse(@"""Hello"""), result.AsAny);
    }
}