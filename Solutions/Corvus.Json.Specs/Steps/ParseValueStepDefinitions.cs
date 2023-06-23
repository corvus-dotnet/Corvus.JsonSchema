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
            _ => throw new InvalidOperationException($"Unsupported type name: {typeName}"),
        };

        this.scenarioContext.Set(jsonValue, ResultKey);
    }
}