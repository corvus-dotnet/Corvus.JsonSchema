// <copyright file="ParseValueStepDefinitions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for the ParseValue feature.
/// </summary>
[Binding]
public class ParseValueStepDefinitions
{
    private const string ResultKey = "Result";

    private readonly ScenarioContext scenarioContext;

    public ParseValueStepDefinitions(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When("the utf8 span '([^']*)' is parsed into a (.*)")]
    public void WhenTheUtfSpanIsParsed(string span, string typeName)
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(span);
        IJsonValue jsonValue = typeName switch
        {
            "JsonBoolean" => JsonBoolean.ParseValue(utf8bytes),
            "JsonNumber" => JsonNumber.ParseValue(utf8bytes),
            "JsonInteger" => JsonNumber.ParseValue(utf8bytes),
            "JsonNull" => JsonNull.ParseValue(utf8bytes),
            "JsonString" => JsonString.ParseValue(utf8bytes),
            "JsonArray" => JsonArray.ParseValue(utf8bytes),
            "JsonObject" => JsonArray.ParseValue(utf8bytes),
            "JsonAny" => JsonAny.ParseValue(utf8bytes),
            "JsonByte" => JsonByte.ParseValue(utf8bytes),
            "JsonSByte" => JsonSByte.ParseValue(utf8bytes),
            "JsonUInt16" => JsonUInt16.ParseValue(utf8bytes),
            "JsonInt16" => JsonInt16.ParseValue(utf8bytes),
            "JsonUInt32" => JsonUInt32.ParseValue(utf8bytes),
            "JsonInt32" => JsonInt32.ParseValue(utf8bytes),
            "JsonUInt64" => JsonUInt64.ParseValue(utf8bytes),
            "JsonInt64" => JsonInt64.ParseValue(utf8bytes),
            "JsonSingle" => JsonSingle.ParseValue(utf8bytes),
            "JsonDouble" => JsonDouble.ParseValue(utf8bytes),
            "JsonDecimal" => JsonDecimal.ParseValue(utf8bytes),
            "JsonBase64Content" => JsonBase64Content.ParseValue(utf8bytes),
            "JsonBase64ContentPre201909" => JsonBase64ContentPre201909.ParseValue(utf8bytes),
            "JsonBase64String" => JsonBase64String.ParseValue(utf8bytes),
            "JsonBase64StringPre201909" => JsonBase64StringPre201909.ParseValue(utf8bytes),
            "JsonContent" => JsonContent.ParseValue(utf8bytes),
            "JsonContentPre201909" => JsonContentPre201909.ParseValue(utf8bytes),
            "JsonDate" => JsonDate.ParseValue(utf8bytes),
            "JsonDateTime" => JsonDateTime.ParseValue(utf8bytes),
            "JsonDuration" => JsonDuration.ParseValue(utf8bytes),
            "JsonEmail" => JsonEmail.ParseValue(utf8bytes),
            "JsonHostname" => JsonHostname.ParseValue(utf8bytes),
            "JsonIdnEmail" => JsonIdnEmail.ParseValue(utf8bytes),
            "JsonIdnHostname" => JsonIdnHostname.ParseValue(utf8bytes),
            "JsonIpV4" => JsonIpV4.ParseValue(utf8bytes),
            "JsonIpV6" => JsonIpV6.ParseValue(utf8bytes),
            "JsonIri" => JsonIri.ParseValue(utf8bytes),
            "JsonIriReference" => JsonIriReference.ParseValue(utf8bytes),
            "JsonPointer" => JsonPointer.ParseValue(utf8bytes),
            "JsonRegex" => JsonRegex.ParseValue(utf8bytes),
            "JsonRelativePointer" => JsonRelativePointer.ParseValue(utf8bytes),
            "JsonTime" => JsonTime.ParseValue(utf8bytes),
            "JsonUri" => JsonUri.ParseValue(utf8bytes),
            "JsonUriTemplate" => JsonUriTemplate.ParseValue(utf8bytes),
            "JsonUuid" => JsonUuid.ParseValue(utf8bytes),
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(jsonValue, ResultKey);
    }

    [When("the char span '([^']*)' is parsed into a (.*)")]
    public void WhenTheCharSpanIsParsed(string span, string typeName)
    {
        IJsonValue jsonValue = typeName switch
        {
            "JsonBoolean" => JsonBoolean.ParseValue(span),
            "JsonNumber" => JsonNumber.ParseValue(span),
            "JsonInteger" => JsonNumber.ParseValue(span),
            "JsonNull" => JsonNull.ParseValue(span),
            "JsonString" => JsonString.ParseValue(span),
            "JsonArray" => JsonArray.ParseValue(span),
            "JsonObject" => JsonArray.ParseValue(span),
            "JsonAny" => JsonAny.ParseValue(span),
            "JsonByte" => JsonByte.Parse(span),
            "JsonSByte" => JsonSByte.Parse(span),
            "JsonUInt16" => JsonUInt16.Parse(span),
            "JsonInt16" => JsonInt16.Parse(span),
            "JsonUInt32" => JsonUInt32.Parse(span),
            "JsonInt32" => JsonInt32.Parse(span),
            "JsonUInt64" => JsonUInt64.Parse(span),
            "JsonInt64" => JsonInt64.Parse(span),
            "JsonSingle" => JsonSingle.Parse(span),
            "JsonDouble" => JsonDouble.Parse(span),
            "JsonDecimal" => JsonDecimal.Parse(span),
            "JsonBase64Content" => JsonBase64Content.Parse(span),
            "JsonBase64ContentPre201909" => JsonBase64ContentPre201909.Parse(span),
            "JsonBase64String" => JsonBase64String.Parse(span),
            "JsonBase64StringPre201909" => JsonBase64StringPre201909.Parse(span),
            "JsonContent" => JsonContent.Parse(span),
            "JsonContentPre201909" => JsonContentPre201909.Parse(span),
            "JsonDate" => JsonDate.Parse(span),
            "JsonDateTime" => JsonDateTime.Parse(span),
            "JsonDuration" => JsonDuration.Parse(span),
            "JsonEmail" => JsonEmail.Parse(span),
            "JsonHostname" => JsonHostname.Parse(span),
            "JsonIdnEmail" => JsonIdnEmail.Parse(span),
            "JsonIdnHostname" => JsonIdnHostname.Parse(span),
            "JsonIpV4" => JsonIpV4.Parse(span),
            "JsonIpV6" => JsonIpV6.Parse(span),
            "JsonIri" => JsonIri.Parse(span),
            "JsonIriReference" => JsonIriReference.Parse(span),
            "JsonPointer" => JsonPointer.Parse(span),
            "JsonRegex" => JsonRegex.Parse(span),
            "JsonRelativePointer" => JsonRelativePointer.Parse(span),
            "JsonTime" => JsonTime.Parse(span),
            "JsonUri" => JsonUri.Parse(span),
            "JsonUriTemplate" => JsonUriTemplate.Parse(span),
            "JsonUuid" => JsonUuid.Parse(span),
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(jsonValue, ResultKey);
    }

    [When("the utf8 span '([^']*)' is parsed with ParsedValue{T} into a (.*)")]
    public void WhenTheUtfSpanIsParsedWithParsedValueOfT(string span, string typeName)
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(span);
        IJsonValue result = typeName switch
        {
            "JsonBoolean" => ParsedValue<JsonBoolean>.Parse(utf8bytes).Instance,
            "JsonNumber" =>  ParsedValue<JsonNumber>.Parse(utf8bytes).Instance,
            "JsonInteger" => ParsedValue<JsonNumber>.Parse(utf8bytes).Instance,
            "JsonNull" => ParsedValue<JsonNull>.Parse(utf8bytes).Instance,
            "JsonString" => ParsedValue<JsonString>.Parse(utf8bytes).Instance,
            "JsonArray" => ParsedValue<JsonArray>.Parse(utf8bytes).Instance,
            "JsonObject" => ParsedValue<JsonArray>.Parse(utf8bytes).Instance,
            "JsonAny" => ParsedValue<JsonAny>.Parse(utf8bytes).Instance,
            "JsonByte" => ParsedValue<JsonByte>.Parse(utf8bytes).Instance,
            "JsonSByte" => ParsedValue<JsonSByte>.Parse(utf8bytes).Instance,
            "JsonUInt16" => ParsedValue<JsonUInt16>.Parse(utf8bytes).Instance,
            "JsonInt16" => ParsedValue<JsonInt16>.Parse(utf8bytes).Instance,
            "JsonUInt32" => ParsedValue<JsonUInt32>.Parse(utf8bytes).Instance,
            "JsonInt32" => ParsedValue<JsonInt32>.Parse(utf8bytes).Instance,
            "JsonUInt64" => ParsedValue<JsonUInt64>.Parse(utf8bytes).Instance,
            "JsonInt64" => ParsedValue<JsonInt64>.Parse(utf8bytes).Instance,
            "JsonSingle" => ParsedValue<JsonSingle>.Parse(utf8bytes).Instance,
            "JsonDouble" => ParsedValue<JsonDouble>.Parse(utf8bytes).Instance,
            "JsonDecimal" => ParsedValue<JsonDecimal>.Parse(utf8bytes).Instance,
            "JsonBase64Content" => ParsedValue<JsonBase64Content>.Parse(utf8bytes).Instance,
            "JsonBase64ContentPre201909" => ParsedValue<JsonBase64ContentPre201909>.Parse(utf8bytes).Instance,
            "JsonBase64String" => ParsedValue<JsonBase64String>.Parse(utf8bytes).Instance,
            "JsonBase64StringPre201909" => ParsedValue<JsonBase64StringPre201909>.Parse(utf8bytes).Instance,
            "JsonContent" => ParsedValue<JsonContent>.Parse(utf8bytes).Instance,
            "JsonContentPre201909" => ParsedValue<JsonContentPre201909>.Parse(utf8bytes).Instance,
            "JsonDate" => ParsedValue<JsonDate>.Parse(utf8bytes).Instance,
            "JsonDateTime" => ParsedValue<JsonDateTime>.Parse(utf8bytes).Instance,
            "JsonDuration" => ParsedValue<JsonDuration>.Parse(utf8bytes).Instance,
            "JsonEmail" => ParsedValue<JsonEmail>.Parse(utf8bytes).Instance,
            "JsonHostname" => ParsedValue<JsonHostname>.Parse(utf8bytes).Instance,
            "JsonIdnEmail" => ParsedValue<JsonIdnEmail>.Parse(utf8bytes).Instance,
            "JsonIdnHostname" => ParsedValue<JsonIdnHostname>.Parse(utf8bytes).Instance,
            "JsonIpV4" => ParsedValue<JsonIpV4>.Parse(utf8bytes).Instance,
            "JsonIpV6" => ParsedValue<JsonIpV6>.Parse(utf8bytes).Instance,
            "JsonIri" => ParsedValue<JsonIri>.Parse(utf8bytes).Instance,
            "JsonIriReference" => ParsedValue<JsonIriReference>.Parse(utf8bytes).Instance,
            "JsonPointer" => ParsedValue<JsonPointer>.Parse(utf8bytes).Instance,
            "JsonRegex" => ParsedValue<JsonRegex>.Parse(utf8bytes).Instance,
            "JsonRelativePointer" => ParsedValue<JsonRelativePointer>.Parse(utf8bytes).Instance,
            "JsonTime" => ParsedValue<JsonTime>.Parse(utf8bytes).Instance,
            "JsonUri" => ParsedValue<JsonUri>.Parse(utf8bytes).Instance,
            "JsonUriTemplate" => ParsedValue<JsonUriTemplate>.Parse(utf8bytes).Instance,
            "JsonUuid" => ParsedValue<JsonUuid>.Parse(utf8bytes).Instance,

            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(result, ResultKey);
    }

    [When("the utf8 ReadOnlyMemory '([^']*)' is parsed with ParsedValue{T} into a (.*)")]
    public void WhenTheUtfReadOnlyMemoryIsParsedWithParsedValueOfT(string span, string typeName)
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(span);
        IJsonValue result = typeName switch
        {
            "JsonBoolean" => ParsedValue<JsonBoolean>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonNumber" => ParsedValue<JsonNumber>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonInteger" => ParsedValue<JsonNumber>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonNull" => ParsedValue<JsonNull>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonString" => ParsedValue<JsonString>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonArray" => ParsedValue<JsonArray>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonObject" => ParsedValue<JsonArray>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonAny" => ParsedValue<JsonAny>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonByte" => ParsedValue<JsonByte>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonSByte" => ParsedValue<JsonSByte>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUInt16" => ParsedValue<JsonUInt16>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonInt16" => ParsedValue<JsonInt16>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUInt32" => ParsedValue<JsonUInt32>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonInt32" => ParsedValue<JsonInt32>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUInt64" => ParsedValue<JsonUInt64>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonInt64" => ParsedValue<JsonInt64>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonSingle" => ParsedValue<JsonSingle>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonDouble" => ParsedValue<JsonDouble>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonDecimal" => ParsedValue<JsonDecimal>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonBase64Content" => ParsedValue<JsonBase64Content>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonBase64ContentPre201909" => ParsedValue<JsonBase64ContentPre201909>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonBase64String" => ParsedValue<JsonBase64String>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonBase64StringPre201909" => ParsedValue<JsonBase64StringPre201909>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonContent" => ParsedValue<JsonContent>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonContentPre201909" => ParsedValue<JsonContentPre201909>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonDate" => ParsedValue<JsonDate>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonDateTime" => ParsedValue<JsonDateTime>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonDuration" => ParsedValue<JsonDuration>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonEmail" => ParsedValue<JsonEmail>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonHostname" => ParsedValue<JsonHostname>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIdnEmail" => ParsedValue<JsonIdnEmail>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIdnHostname" => ParsedValue<JsonIdnHostname>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIpV4" => ParsedValue<JsonIpV4>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIpV6" => ParsedValue<JsonIpV6>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIri" => ParsedValue<JsonIri>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonIriReference" => ParsedValue<JsonIriReference>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonPointer" => ParsedValue<JsonPointer>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonRegex" => ParsedValue<JsonRegex>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonRelativePointer" => ParsedValue<JsonRelativePointer>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonTime" => ParsedValue<JsonTime>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUri" => ParsedValue<JsonUri>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUriTemplate" => ParsedValue<JsonUriTemplate>.Parse(utf8bytes.AsMemory()).Instance,
            "JsonUuid" => ParsedValue<JsonUuid>.Parse(utf8bytes.AsMemory()).Instance,
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(result, ResultKey);
    }

    [When("the utf8 Stream '([^']*)' is parsed with ParsedValue{T} into a (.*)")]
    public void WhenTheUtfStreamIsParsedWithParsedValueOfT(string span, string typeName)
    {
        byte[] utf8bytes = Encoding.UTF8.GetBytes(span);
        using MemoryStream stream = new(utf8bytes);
        IJsonValue result = typeName switch
        {
            "JsonBoolean" => ParsedValue<JsonBoolean>.Parse(stream).Instance,
            "JsonNumber" => ParsedValue<JsonNumber>.Parse(stream).Instance,
            "JsonInteger" => ParsedValue<JsonNumber>.Parse(stream).Instance,
            "JsonNull" => ParsedValue<JsonNull>.Parse(stream).Instance,
            "JsonString" => ParsedValue<JsonString>.Parse(stream).Instance,
            "JsonArray" => ParsedValue<JsonArray>.Parse(stream).Instance,
            "JsonObject" => ParsedValue<JsonArray>.Parse(stream).Instance,
            "JsonAny" => ParsedValue<JsonAny>.Parse(stream).Instance,
            "JsonByte" => ParsedValue<JsonByte>.Parse(stream).Instance,
            "JsonSByte" => ParsedValue<JsonSByte>.Parse(stream).Instance,
            "JsonUInt16" => ParsedValue<JsonUInt16>.Parse(stream).Instance,
            "JsonInt16" => ParsedValue<JsonInt16>.Parse(stream).Instance,
            "JsonUInt32" => ParsedValue<JsonUInt32>.Parse(stream).Instance,
            "JsonInt32" => ParsedValue<JsonInt32>.Parse(stream).Instance,
            "JsonUInt64" => ParsedValue<JsonUInt64>.Parse(stream).Instance,
            "JsonInt64" => ParsedValue<JsonInt64>.Parse(stream).Instance,
            "JsonSingle" => ParsedValue<JsonSingle>.Parse(stream).Instance,
            "JsonDouble" => ParsedValue<JsonDouble>.Parse(stream).Instance,
            "JsonDecimal" => ParsedValue<JsonDecimal>.Parse(stream).Instance,
            "JsonBase64Content" => ParsedValue<JsonBase64Content>.Parse(stream).Instance,
            "JsonBase64ContentPre201909" => ParsedValue<JsonBase64ContentPre201909>.Parse(stream).Instance,
            "JsonBase64String" => ParsedValue<JsonBase64String>.Parse(stream).Instance,
            "JsonBase64StringPre201909" => ParsedValue<JsonBase64StringPre201909>.Parse(stream).Instance,
            "JsonContent" => ParsedValue<JsonContent>.Parse(stream).Instance,
            "JsonContentPre201909" => ParsedValue<JsonContentPre201909>.Parse(stream).Instance,
            "JsonDate" => ParsedValue<JsonDate>.Parse(stream).Instance,
            "JsonDateTime" => ParsedValue<JsonDateTime>.Parse(stream).Instance,
            "JsonDuration" => ParsedValue<JsonDuration>.Parse(stream).Instance,
            "JsonEmail" => ParsedValue<JsonEmail>.Parse(stream).Instance,
            "JsonHostname" => ParsedValue<JsonHostname>.Parse(stream).Instance,
            "JsonIdnEmail" => ParsedValue<JsonIdnEmail>.Parse(stream).Instance,
            "JsonIdnHostname" => ParsedValue<JsonIdnHostname>.Parse(stream).Instance,
            "JsonIpV4" => ParsedValue<JsonIpV4>.Parse(stream).Instance,
            "JsonIpV6" => ParsedValue<JsonIpV6>.Parse(stream).Instance,
            "JsonIri" => ParsedValue<JsonIri>.Parse(stream).Instance,
            "JsonIriReference" => ParsedValue<JsonIriReference>.Parse(stream).Instance,
            "JsonPointer" => ParsedValue<JsonPointer>.Parse(stream).Instance,
            "JsonRegex" => ParsedValue<JsonRegex>.Parse(stream).Instance,
            "JsonRelativePointer" => ParsedValue<JsonRelativePointer>.Parse(stream).Instance,
            "JsonTime" => ParsedValue<JsonTime>.Parse(stream).Instance,
            "JsonUri" => ParsedValue<JsonUri>.Parse(stream).Instance,
            "JsonUriTemplate" => ParsedValue<JsonUriTemplate>.Parse(stream).Instance,
            "JsonUuid" => ParsedValue<JsonUuid>.Parse(stream).Instance,

            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(result, ResultKey);
    }

    [When("the char span '([^']*)' is parsed with ParsedValue{T} into a (.*)")]
    public void WhenTheCharSpanIsParsedParsedValueOfT(string span, string typeName)
    {
        IJsonValue jsonValue = typeName switch
        {
            "JsonBoolean" => ParsedValue<JsonBoolean>.Parse(span).Instance,
            "JsonNumber" => ParsedValue<JsonNumber>.Parse(span).Instance,
            "JsonInteger" => ParsedValue<JsonNumber>.Parse(span).Instance,
            "JsonNull" => ParsedValue<JsonNull>.Parse(span).Instance,
            "JsonString" => ParsedValue<JsonString>.Parse(span).Instance,
            "JsonArray" => ParsedValue<JsonArray>.Parse(span).Instance,
            "JsonObject" => ParsedValue<JsonArray>.Parse(span).Instance,
            "JsonAny" => ParsedValue<JsonAny>.Parse(span).Instance,
            "JsonByte" => ParsedValue<JsonByte>.Parse(span).Instance,
            "JsonSByte" => ParsedValue<JsonSByte>.Parse(span).Instance,
            "JsonUInt16" => ParsedValue<JsonUInt16>.Parse(span).Instance,
            "JsonInt16" => ParsedValue<JsonInt16>.Parse(span).Instance,
            "JsonUInt32" => ParsedValue<JsonUInt32>.Parse(span).Instance,
            "JsonInt32" => ParsedValue<JsonInt32>.Parse(span).Instance,
            "JsonUInt64" => ParsedValue<JsonUInt64>.Parse(span).Instance,
            "JsonInt64" => ParsedValue<JsonInt64>.Parse(span).Instance,
            "JsonSingle" => ParsedValue<JsonSingle>.Parse(span).Instance,
            "JsonDouble" => ParsedValue<JsonDouble>.Parse(span).Instance,
            "JsonDecimal" => ParsedValue<JsonDecimal>.Parse(span).Instance,
            "JsonBase64Content" => ParsedValue<JsonBase64Content>.Parse(span).Instance,
            "JsonBase64ContentPre201909" => ParsedValue<JsonBase64ContentPre201909>.Parse(span).Instance,
            "JsonBase64String" => ParsedValue<JsonBase64String>.Parse(span).Instance,
            "JsonBase64StringPre201909" => ParsedValue<JsonBase64StringPre201909>.Parse(span).Instance,
            "JsonContent" => ParsedValue<JsonContent>.Parse(span).Instance,
            "JsonContentPre201909" => ParsedValue<JsonContentPre201909>.Parse(span).Instance,
            "JsonDate" => ParsedValue<JsonDate>.Parse(span).Instance,
            "JsonDateTime" => ParsedValue<JsonDateTime>.Parse(span).Instance,
            "JsonDuration" => ParsedValue<JsonDuration>.Parse(span).Instance,
            "JsonEmail" => ParsedValue<JsonEmail>.Parse(span).Instance,
            "JsonHostname" => ParsedValue<JsonHostname>.Parse(span).Instance,
            "JsonIdnEmail" => ParsedValue<JsonIdnEmail>.Parse(span).Instance,
            "JsonIdnHostname" => ParsedValue<JsonIdnHostname>.Parse(span).Instance,
            "JsonIpV4" => ParsedValue<JsonIpV4>.Parse(span).Instance,
            "JsonIpV6" => ParsedValue<JsonIpV6>.Parse(span).Instance,
            "JsonIri" => ParsedValue<JsonIri>.Parse(span).Instance,
            "JsonIriReference" => ParsedValue<JsonIriReference>.Parse(span).Instance,
            "JsonPointer" => ParsedValue<JsonPointer>.Parse(span).Instance,
            "JsonRegex" => ParsedValue<JsonRegex>.Parse(span).Instance,
            "JsonRelativePointer" => ParsedValue<JsonRelativePointer>.Parse(span).Instance,
            "JsonTime" => ParsedValue<JsonTime>.Parse(span).Instance,
            "JsonUri" => ParsedValue<JsonUri>.Parse(span).Instance,
            "JsonUriTemplate" => ParsedValue<JsonUriTemplate>.Parse(span).Instance,
            "JsonUuid" => ParsedValue<JsonUuid>.Parse(span).Instance,
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(jsonValue, ResultKey);
    }

    [When("the char ReadOnlyMemory '([^']*)' is parsed with ParsedValue{T} into a (.*)")]
    public void WhenTheCharReadOnlyMemoryIsParsedParsedValueOfT(string span, string typeName)
    {
        IJsonValue jsonValue = typeName switch
        {
            "JsonBoolean" => ParsedValue<JsonBoolean>.Parse(span.AsMemory()).Instance,
            "JsonNumber" => ParsedValue<JsonNumber>.Parse(span.AsMemory()).Instance,
            "JsonInteger" => ParsedValue<JsonNumber>.Parse(span.AsMemory()).Instance,
            "JsonNull" => ParsedValue<JsonNull>.Parse(span.AsMemory()).Instance,
            "JsonString" => ParsedValue<JsonString>.Parse(span.AsMemory()).Instance,
            "JsonArray" => ParsedValue<JsonArray>.Parse(span.AsMemory()).Instance,
            "JsonObject" => ParsedValue<JsonArray>.Parse(span.AsMemory()).Instance,
            "JsonAny" => ParsedValue<JsonAny>.Parse(span.AsMemory()).Instance,
            "JsonByte" => ParsedValue<JsonByte>.Parse(span.AsMemory()).Instance,
            "JsonSByte" => ParsedValue<JsonSByte>.Parse(span.AsMemory()).Instance,
            "JsonUInt16" => ParsedValue<JsonUInt16>.Parse(span.AsMemory()).Instance,
            "JsonInt16" => ParsedValue<JsonInt16>.Parse(span.AsMemory()).Instance,
            "JsonUInt32" => ParsedValue<JsonUInt32>.Parse(span.AsMemory()).Instance,
            "JsonInt32" => ParsedValue<JsonInt32>.Parse(span.AsMemory()).Instance,
            "JsonUInt64" => ParsedValue<JsonUInt64>.Parse(span.AsMemory()).Instance,
            "JsonInt64" => ParsedValue<JsonInt64>.Parse(span.AsMemory()).Instance,
            "JsonSingle" => ParsedValue<JsonSingle>.Parse(span.AsMemory()).Instance,
            "JsonDouble" => ParsedValue<JsonDouble>.Parse(span.AsMemory()).Instance,
            "JsonDecimal" => ParsedValue<JsonDecimal>.Parse(span.AsMemory()).Instance,
            "JsonBase64Content" => ParsedValue<JsonBase64Content>.Parse(span.AsMemory()).Instance,
            "JsonBase64ContentPre201909" => ParsedValue<JsonBase64ContentPre201909>.Parse(span.AsMemory()).Instance,
            "JsonBase64String" => ParsedValue<JsonBase64String>.Parse(span.AsMemory()).Instance,
            "JsonBase64StringPre201909" => ParsedValue<JsonBase64StringPre201909>.Parse(span.AsMemory()).Instance,
            "JsonContent" => ParsedValue<JsonContent>.Parse(span.AsMemory()).Instance,
            "JsonContentPre201909" => ParsedValue<JsonContentPre201909>.Parse(span.AsMemory()).Instance,
            "JsonDate" => ParsedValue<JsonDate>.Parse(span.AsMemory()).Instance,
            "JsonDateTime" => ParsedValue<JsonDateTime>.Parse(span.AsMemory()).Instance,
            "JsonDuration" => ParsedValue<JsonDuration>.Parse(span.AsMemory()).Instance,
            "JsonEmail" => ParsedValue<JsonEmail>.Parse(span.AsMemory()).Instance,
            "JsonHostname" => ParsedValue<JsonHostname>.Parse(span.AsMemory()).Instance,
            "JsonIdnEmail" => ParsedValue<JsonIdnEmail>.Parse(span.AsMemory()).Instance,
            "JsonIdnHostname" => ParsedValue<JsonIdnHostname>.Parse(span.AsMemory()).Instance,
            "JsonIpV4" => ParsedValue<JsonIpV4>.Parse(span.AsMemory()).Instance,
            "JsonIpV6" => ParsedValue<JsonIpV6>.Parse(span.AsMemory()).Instance,
            "JsonIri" => ParsedValue<JsonIri>.Parse(span.AsMemory()).Instance,
            "JsonIriReference" => ParsedValue<JsonIriReference>.Parse(span.AsMemory()).Instance,
            "JsonPointer" => ParsedValue<JsonPointer>.Parse(span.AsMemory()).Instance,
            "JsonRegex" => ParsedValue<JsonRegex>.Parse(span.AsMemory()).Instance,
            "JsonRelativePointer" => ParsedValue<JsonRelativePointer>.Parse(span.AsMemory()).Instance,
            "JsonTime" => ParsedValue<JsonTime>.Parse(span.AsMemory()).Instance,
            "JsonUri" => ParsedValue<JsonUri>.Parse(span.AsMemory()).Instance,
            "JsonUriTemplate" => ParsedValue<JsonUriTemplate>.Parse(span.AsMemory()).Instance,
            "JsonUuid" => ParsedValue<JsonUuid>.Parse(span.AsMemory()).Instance,
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(jsonValue, ResultKey);
    }

    [Then("the result should be equal to the JsonAny (.*)")]
    public void ThenTheResultShouldBeEqualToTheJsonAnyTrue(string value)
    {
        JsonAny result = this.scenarioContext.Get<IJsonValue>(ResultKey).AsAny;
        var expected = JsonAny.Parse(value); // Use Parse here to avoid like-for-like.
        Assert.AreEqual(expected, result);
    }
}