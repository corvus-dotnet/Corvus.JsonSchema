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