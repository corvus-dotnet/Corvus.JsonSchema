﻿// <copyright file="JsonValueCastSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Corvus.Json;
using NodaTime;
using NodaTime.Text;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for Json value types.
/// </summary>
[Binding]
public class JsonValueCastSteps
{
    private const string CastResultKey = "CastResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueCastSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonValueCastSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /* array */

    /// <summary>
    /// Cast the value stored in the context variable <see cref="JsonValueSteps.SubjectUnderTest"/> to a JsonAny and store in the <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonArray to JsonAny")]
    public void WhenICastTheJsonArrayToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonNotAny serialized in <paramref name="match"/>.
    /// </summary>
    /// <param name="match">The serialized form of the result to match.</param>
    [Then("the result should equal the JsonNotAny (.*)")]
    public void ThenTheResultShouldEqualTheJsonNotAny(string match)
    {
        Assert.AreEqual(JsonNotAny.ParseValue(match.AsSpan()), this.scenarioContext.Get<JsonNotAny>(CastResultKey));
    }

    /// <summary>
    /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonNotAny serialized in <paramref name="match"/>, as a double, within the given margin.
    /// </summary>
    /// <param name="match">The serialized form of the result to match.</param>
    /// <param name="margin">The precision with which to match the JsonAny value.</param>
    [Then("the result should equal within (.*) the JsonNotAny (.*)")]
    public void ThenTheResultShouldEqualTheJsonNotAnyWithin(string match, double margin)
    {
        Assert.AreEqual((double)JsonNotAny.ParseValue(match.AsSpan()).AsNumber, (double)this.scenarioContext.Get<JsonNotAny>(CastResultKey).AsNumber, margin);
    }

    /// <summary>
    /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonAny serialized in <paramref name="match"/>.
    /// </summary>
    /// <param name="match">The serialized form of the result to match.</param>
    [Then("the result should equal the JsonAny (.*)")]
    public void ThenTheResultShouldEqualTheJsonAny(string match)
    {
        Assert.AreEqual(JsonAny.ParseValue(match.AsSpan()), this.scenarioContext.Get<JsonAny>(CastResultKey));
    }

    [Then("the result should equal within (.*) the JsonAny (.*)")]
    public void ThenTheResultShouldEqualTheJsonAnyWithin(double margin, string match)
    {
        Assert.AreEqual((double)JsonNumber.ParseValue(match.AsSpan()), (double)this.scenarioContext.Get<JsonAny>(CastResultKey).AsNumber, margin);
    }

    /// <summary>
    /// Compare the IJsonValue in the <see cref="CastResultKey"/> with the serialized <paramref name="jsonArray"/>.
    /// </summary>
    /// <param name="jsonArray">The serialized JsonArray with which to compare the result.</param>
    [Then("the result should equal the JsonArray (.*)")]
    public void ThenTheResultShouldEqualTheJsonArray(string jsonArray)
    {
        Assert.AreEqual(JsonAny.ParseValue(jsonArray.AsSpan()).AsArray, this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Compares the two lists.
    /// </summary>
    /// <param name="immutableList">The immutable list with which to compare the result.</param>
    [Then("the result should equal the ImmutableList<JsonAny> (.*)")]
    public void ThenTheResultShouldEqualTheImmutableList(string immutableList)
    {
        ImmutableList<JsonAny> expected = JsonArray.ParseValue(immutableList.AsSpan()).AsImmutableList();
        ImmutableList<JsonAny> actual = this.scenarioContext.Get<ImmutableList<JsonAny>>(CastResultKey);
        CollectionAssert.AreEqual(expected, actual);
    }

    /* any */

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonAny")]
    public void WhenICastTheLongToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonAny")]
    public void WhenICastTheIntToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonAny")]
    public void WhenICastTheDoubleToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonAny")]
    public void WhenICastTheFloatToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    [When("I cast the JsonObject to JsonAny")]
    public void WhenICastTheJsonObjectToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compare the IJsonValue in the <see cref="CastResultKey"/> with the serialized <paramref name="jsonObject"/>.
    /// </summary>
    /// <param name="jsonObject">The serialized JsonObject with which to compare the result.</param>
    [Then("the result should equal the JsonObject (.*)")]
    public void ThenTheResultShouldEqualTheJsonObject(string jsonObject)
    {
        Assert.AreEqual(JsonAny.ParseValue(jsonObject).AsObject, this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Compares the two dictionaries.
    /// </summary>
    /// <param name="immutableDictionary">The immutable dictionary with which to compare the result.</param>
    [Then("the result should equal the ImmutableDictionary<JsonPropertyName,JsonAny> (.*)")]
    public void ThenTheResultShouldEqualTheImmutableDictionary(string immutableDictionary)
    {
        ImmutableList<JsonObjectProperty> expected = JsonAny.ParseValue(immutableDictionary).AsObject.AsPropertyBacking();
        ImmutableList<JsonObjectProperty> actual = this.scenarioContext.Get<ImmutableList<JsonObjectProperty>>(CastResultKey);
        CollectionAssert.AreEqual(expected, actual);
    }

    /* string */

    /// <summary>
    /// Compares the two <see cref="JsonString"/> instances.
    /// </summary>
    /// <param name="expectedString">The string with which to compare the result.</param>
    [Then("the result should equal the JsonString (.*)")]
    public void ThenTheResultShouldEqualTheJsonString(string expectedString)
    {
        var expected = JsonString.ParseValue(expectedString);
        JsonString actual = this.scenarioContext.Get<JsonString>(CastResultKey);
        Assert.AreEqual(expected, actual);
    }

    /// <summary>
    /// Compares a <see cref="ReadOnlyMemory{Char}"/> built from the <see cref="ReadOnlySpan{Char}"/> and stored in <see cref="CastResultKey"/> against the <see cref="ReadOnlyMemory{Char}"/> built from the expected string.
    /// </summary>
    /// <param name="expectedString">The string with which to compare the result.</param>
    [Then("the result should equal the ReadOnlySpan<char> \"(.*)\"")]
    public void ThenTheResultShouldEqualTheReadOnlySpanOfChar(string expectedString)
    {
        ReadOnlyMemory<char> expected = expectedString.AsMemory();
        ReadOnlyMemory<char> actual = this.scenarioContext.Get<ReadOnlyMemory<char>>(CastResultKey);
        CollectionAssert.AreEqual(expected.ToArray(), actual.ToArray());
    }

    /// <summary>
    /// Compares a <see cref="ReadOnlyMemory{Byte}"/> built from the <see cref="ReadOnlySpan{Byte}"/> and stored in <see cref="CastResultKey"/> against the <see cref="ReadOnlyMemory{Byte}"/> built from the expected string.
    /// </summary>
    /// <param name="expectedString">The string with which to compare the result.</param>
    [Then("the result should equal the ReadOnlySpan<byte> \"(.*)\"")]
    public void ThenTheResultShouldEqualTheReadOnlySpanOfByte(string expectedString)
    {
        byte[] expected = Encoding.UTF8.GetBytes(expectedString);
        ReadOnlyMemory<byte> actual = this.scenarioContext.Get<ReadOnlyMemory<byte>>(CastResultKey);
        CollectionAssert.AreEqual(expected, actual.ToArray());
    }

    /// <summary>
    /// Compares a string stored in <see cref="CastResultKey"/> against the expected string.
    /// </summary>
    /// <param name="expected">The string with which to compare the result.</param>
    [Then("the result should equal the string \"(.*)\"")]
    public void ThenTheResultShouldEqualTheString(string expected)
    {
        string actual = this.scenarioContext.Get<string>(CastResultKey);
        Assert.AreEqual(expected, actual);
    }

    /* base64content */

    /// <summary>
    /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64Content to JsonAny")]
    public void WhenICastTheJsonBase64ContentToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonBase64Content"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64Content.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64Content"/>.</param>
    [Then("the result should equal the JsonBase64Content (.*)")]
    public void ThenTheResultShouldEqualTheJsonBase64Content(string expectedValue)
    {
        var expected = JsonBase64Content.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64Content>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64Content to JsonString")]
    public void WhenICastTheJsonBase64ContentToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonBase64Content")]
    public void WhenICastTheJsonStringToJsonBase64Content()
    {
        this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64Content to string")]
    public void WhenICastTheJsonBase64ContentToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonBase64Content")]
    public void WhenICastTheStringToJsonBase64Content()
    {
        this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* base64content */

    /// <summary>
    /// Casts the <see cref="JsonBase64ContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64ContentPre201909 to JsonAny")]
    public void WhenICastTheJsonBase64ContentPre201909ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonBase64ContentPre201909"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64ContentPre201909.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64ContentPre201909"/>.</param>
    [Then("the result should equal the JsonBase64ContentPre201909 (.*)")]
    public void ThenTheResultShouldEqualTheJsonBase64ContentPre201909(string expectedValue)
    {
        var expected = JsonBase64ContentPre201909.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64ContentPre201909>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64ContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64ContentPre201909 to JsonString")]
    public void WhenICastTheJsonBase64ContentPre201909ToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64ContentPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonBase64ContentPre201909")]
    public void WhenICastTheJsonStringToJsonBase64ContentPre201909()
    {
        this.scenarioContext.Set((JsonBase64ContentPre201909)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64ContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64ContentPre201909 to string")]
    public void WhenICastTheJsonBase64ContentPre201909ToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64ContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64ContentPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonBase64ContentPre201909")]
    public void WhenICastTheStringToJsonBase64ContentPre201909()
    {
        this.scenarioContext.Set((JsonBase64ContentPre201909)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* base64String */

    /// <summary>
    /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64String to JsonAny")]
    public void WhenICastTheJsonBase64StringToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonBase64String"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64String.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64String"/>.</param>
    [Then("the result should equal the JsonBase64String (.*)")]
    public void ThenTheResultShouldEqualTheJsonBase64String(string expectedValue)
    {
        var expected = JsonBase64String.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64String>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64String to JsonString")]
    public void WhenICastTheJsonBase64StringToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonBase64String")]
    public void WhenICastTheJsonStringToJsonBase64String()
    {
        this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64String to string")]
    public void WhenICastTheJsonBase64StringToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonBase64String")]
    public void WhenICastTheStringToJsonBase64String()
    {
        this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* base64StringPre201909 */

    /// <summary>
    /// Casts the <see cref="JsonBase64StringPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64StringPre201909 to JsonAny")]
    public void WhenICastTheJsonBase64StringPre201909ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonBase64StringPre201909"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64StringPre201909.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64StringPre201909"/>.</param>
    [Then("the result should equal the JsonBase64StringPre201909 (.*)")]
    public void ThenTheResultShouldEqualTheJsonBase64StringPre201909(string expectedValue)
    {
        var expected = JsonBase64StringPre201909.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64StringPre201909>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64StringPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64StringPre201909 to JsonString")]
    public void WhenICastTheJsonBase64StringPre201909ToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64StringPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonBase64StringPre201909")]
    public void WhenICastTheJsonStringToJsonBase64StringPre201909()
    {
        this.scenarioContext.Set((JsonBase64StringPre201909)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonBase64StringPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBase64StringPre201909 to string")]
    public void WhenICastTheJsonBase64StringPre201909ToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64StringPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64StringPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonBase64StringPre201909")]
    public void WhenICastTheStringToJsonBase64StringPre201909()
    {
        this.scenarioContext.Set((JsonBase64StringPre201909)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* content */

    /// <summary>
    /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContent to JsonAny")]
    public void WhenICastTheJsonContentToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonContent"/> in the context value <see cref="CastResultKey"/> with the given JsonContent.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonContent (.*)")]
    public void ThenTheResultShouldEqualTheJsonContent(string expectedValue)
    {
        var expected = JsonContent.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonContent>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContent to JsonString")]
    public void WhenICastTheJsonContentToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonContent")]
    public void WhenICastTheJsonStringToJsonContent()
    {
        this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContent to string")]
    public void WhenICastTheJsonContentToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonContent")]
    public void WhenICastTheStringToJsonContent()
    {
        this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* content */

    /// <summary>
    /// Casts the <see cref="JsonContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContentPre201909 to JsonAny")]
    public void WhenICastTheJsonContentPre201909ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonContentPre201909"/> in the context value <see cref="CastResultKey"/> with the given JsonContentPre201909.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContentPre201909"/>.</param>
    [Then("the result should equal the JsonContentPre201909 (.*)")]
    public void ThenTheResultShouldEqualTheJsonContentPre201909(string expectedValue)
    {
        var expected = JsonContentPre201909.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonContentPre201909>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContentPre201909 to JsonString")]
    public void WhenICastTheJsonContentPre201909ToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContentPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonContentPre201909")]
    public void WhenICastTheJsonStringToJsonContentPre201909()
    {
        this.scenarioContext.Set((JsonContentPre201909)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonContentPre201909"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonContentPre201909 to string")]
    public void WhenICastTheJsonContentPre201909ToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonContentPre201909>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContentPre201909"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonContentPre201909")]
    public void WhenICastTheStringToJsonContentPre201909()
    {
        this.scenarioContext.Set((JsonContentPre201909)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* email */

    /// <summary>
    /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonEmail to JsonAny")]
    public void WhenICastTheJsonEmailToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonEmail"/> in the context value <see cref="CastResultKey"/> with the given JsonEmail.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonEmail"/>.</param>
    [Then("the result should equal the JsonEmail (.*)")]
    public void ThenTheResultShouldEqualTheJsonEmail(string expectedValue)
    {
        var expected = JsonEmail.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonEmail>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonEmail to JsonString")]
    public void WhenICastTheJsonEmailToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonEmail")]
    public void WhenICastTheJsonStringToJsonEmail()
    {
        this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonEmail to string")]
    public void WhenICastTheJsonEmailToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonEmail")]
    public void WhenICastTheStringToJsonEmail()
    {
        this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* uriTemplate */

    /// <summary>
    /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriTemplate to JsonAny")]
    public void WhenICastTheJsonUriTemplateToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUriTemplate"/> in the context value <see cref="CastResultKey"/> with the given JsonUriTemplate.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonUriTemplate"/>.</param>
    [Then("the result should equal the JsonUriTemplate (.*)")]
    public void ThenTheResultShouldEqualTheJsonUriTemplate(string expectedValue)
    {
        var expected = JsonUriTemplate.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonUriTemplate>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriTemplate to JsonString")]
    public void WhenICastTheJsonUriTemplateToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonUriTemplate")]
    public void WhenICastTheJsonStringToJsonUriTemplate()
    {
        this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriTemplate to string")]
    public void WhenICastTheJsonUriTemplateToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonUriTemplate")]
    public void WhenICastTheStringToJsonUriTemplate()
    {
        this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* pointer */

    /// <summary>
    /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonPointer to JsonAny")]
    public void WhenICastTheJsonPointerToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonPointer"/> in the context value <see cref="CastResultKey"/> with the given JsonPointer.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonPointer"/>.</param>
    [Then("the result should equal the JsonPointer (.*)")]
    public void ThenTheResultShouldEqualTheJsonPointer(string expectedValue)
    {
        var expected = JsonPointer.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonPointer>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonPointer to JsonString")]
    public void WhenICastTheJsonPointerToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonPointer")]
    public void WhenICastTheJsonStringToJsonPointer()
    {
        this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonPointer to string")]
    public void WhenICastTheJsonPointerToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonPointer")]
    public void WhenICastTheStringToJsonPointer()
    {
        this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* relativePointer */

    /// <summary>
    /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRelativePointer to JsonAny")]
    public void WhenICastTheJsonRelativePointerToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonRelativePointer"/> in the context value <see cref="CastResultKey"/> with the given JsonRelativePointer.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonRelativePointer"/>.</param>
    [Then("the result should equal the JsonRelativePointer (.*)")]
    public void ThenTheResultShouldEqualTheJsonRelativePointer(string expectedValue)
    {
        var expected = JsonRelativePointer.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonRelativePointer>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRelativePointer to JsonString")]
    public void WhenICastTheJsonRelativePointerToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonRelativePointer")]
    public void WhenICastTheJsonStringToJsonRelativePointer()
    {
        this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRelativePointer to string")]
    public void WhenICastTheJsonRelativePointerToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonRelativePointer")]
    public void WhenICastTheStringToJsonRelativePointer()
    {
        this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* boolean */

    /// <summary>
    /// Casts the <see cref="JsonBoolean"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBoolean to JsonAny")]
    public void WhenICastTheJsonBooleanToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonBoolean"/> in the context value <see cref="CastResultKey"/> with the given JsonBoolean.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonBoolean (.*)")]
    public void ThenTheResultShouldEqualTheJsonBoolean(string expectedValue)
    {
        Assert.AreEqual(JsonAny.ParseValue(expectedValue).AsBoolean, this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonBoolean"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="bool"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonBoolean to bool")]
    public void WhenICastTheJsonBooleanToBool()
    {
        this.scenarioContext.Set((bool)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="bool"/> in the context value <see cref="CastResultKey"/> with the given bool.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the bool (.*)")]
    public void ThenTheResultShouldEqualTheBool(bool expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<bool>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="bool"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBoolean"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the bool to JsonBoolean")]
    public void WhenICastTheBoolToJsonBoolean()
    {
        this.scenarioContext.Set<IJsonValue>((JsonBoolean)this.scenarioContext.Get<bool>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* date */

    /// <summary>
    /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDate to JsonAny")]
    public void WhenICastTheJsonDateToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDate"/> in the context value <see cref="CastResultKey"/> with the given JsonDate.
    /// </summary>
    /// <param name="value">The string representation of the date.</param>
    [Then("the result should equal the JsonDate (.*)")]
    public void ThenTheResultShouldEqualTheJsonDate(string value)
    {
        Assert.AreEqual(JsonDate.ParseValue(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDate to JsonString")]
    public void WhenICastTheJsonDateToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonDate")]
    public void WhenICastTheJsonStringToJsonDate()
    {
        this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="LocalDate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDate to LocalDate")]
    public void WhenICastTheJsonDateToLocalDate()
    {
        this.scenarioContext.Set((LocalDate)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDate"/> in the context value <see cref="CastResultKey"/> with the given LocalDate.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="LocalDate"/>.</param>
    [Then("the result should equal the LocalDate (.*)")]
    public void ThenTheResultShouldEqualTheLocalDate(string expectedValue)
    {
        Assert.AreEqual(LocalDatePattern.Iso.Parse(expectedValue.Trim('"')).Value, this.scenarioContext.Get<LocalDate>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="LocalDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the LocalDate to JsonDate")]
    public void WhenICastTheLocalDateToJsonDate()
    {
        this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<LocalDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDate to string")]
    public void WhenICastTheJsonDateToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonDate")]
    public void WhenICastTheStringToJsonDate()
    {
        this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* time */

    /// <summary>
    /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonTime to JsonAny")]
    public void WhenICastTheJsonTimeToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonTime"/> in the context value <see cref="CastResultKey"/> with the given JsonTime.
    /// </summary>
    /// <param name="value">The string representation of the time.</param>
    [Then("the result should equal the JsonTime (.*)")]
    public void ThenTheResultShouldEqualTheJsonTime(string value)
    {
        Assert.AreEqual(JsonTime.ParseValue(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonTime to JsonString")]
    public void WhenICastTheJsonTimeToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonTime")]
    public void WhenICastTheJsonStringToJsonTime()
    {
        this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="OffsetTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonTime to OffsetTime")]
    public void WhenICastTheJsonTimeToOffsetTime()
    {
        this.scenarioContext.Set((OffsetTime)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonTime"/> in the context value <see cref="CastResultKey"/> with the given OffsetTime.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="OffsetTime"/>.</param>
    [Then("the result should equal the OffsetTime (.*)")]
    public void ThenTheResultShouldEqualTheOffsetTime(string expectedValue)
    {
        Assert.AreEqual(OffsetTimePattern.ExtendedIso.Parse(expectedValue.Trim('"')).Value, this.scenarioContext.Get<OffsetTime>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="OffsetTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the OffsetTime to JsonTime")]
    public void WhenICastTheOffsetTimeToJsonTime()
    {
        this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<OffsetTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonTime to string")]
    public void WhenICastTheJsonTimeToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonTime")]
    public void WhenICastTheStringToJsonTime()
    {
        this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* datetime */

    /// <summary>
    /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDateTime to JsonAny")]
    public void WhenICastTheJsonDateTimeToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDateTime"/> in the context value <see cref="CastResultKey"/> with the given JsonDateTime.
    /// </summary>
    /// <param name="value">The string representation of the dateTime.</param>
    [Then("the result should equal the JsonDateTime (.*)")]
    public void ThenTheResultShouldEqualTheJsonDateTime(string value)
    {
        Assert.AreEqual(JsonDateTime.ParseValue(value), this.scenarioContext.Get<JsonDateTime>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDateTime to JsonString")]
    public void WhenICastTheJsonDateTimeToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonDateTime")]
    public void WhenICastTheJsonStringToJsonDateTime()
    {
        this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="OffsetDateTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDateTime to OffsetDateTime")]
    public void WhenICastTheJsonDateTimeToOffsetDateTime()
    {
        this.scenarioContext.Set((OffsetDateTime)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDateTime"/> in the context value <see cref="CastResultKey"/> with the given OffsetDateTime.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="OffsetDateTime"/>.</param>
    [Then("the result should equal the OffsetDateTime (.*)")]
    public void ThenTheResultShouldEqualTheOffsetDateTime(string expectedValue)
    {
        Assert.AreEqual(OffsetDateTimePattern.ExtendedIso.Parse(expectedValue.Trim('"')).Value, this.scenarioContext.Get<OffsetDateTime>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="OffsetDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the OffsetDateTime to JsonDateTime")]
    public void WhenICastTheOffsetDateTimeToJsonDateTime()
    {
        this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<OffsetDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDateTime to string")]
    public void WhenICastTheJsonDateTimeToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonDateTime")]
    public void WhenICastTheStringToJsonDateTime()
    {
        this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDuration to JsonAny")]
    public void WhenICastTheJsonDurationToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDuration"/> in the context value <see cref="CastResultKey"/> with the given JsonDuration.
    /// </summary>
    /// <param name="value">The string representation of the duration.</param>
    [Then("the result should equal the JsonDuration (.*)")]
    public void ThenTheResultShouldEqualTheJsonDuration(string value)
    {
        Assert.AreEqual(JsonDuration.ParseValue(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDuration to JsonString")]
    public void WhenICastTheJsonDurationToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonDuration")]
    public void WhenICastTheJsonStringToJsonDuration()
    {
        this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="NodaTime.Period"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDuration to Period")]
    public void WhenICastTheJsonDurationToPeriod()
    {
        this.scenarioContext.Set((NodaTime.Period)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDuration"/> in the context value <see cref="CastResultKey"/> with the given Period.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="NodaTime.Period"/>.</param>
    [Then("the result should equal the Period (.*)")]
    public void ThenTheResultShouldEqualThePeriod(string expectedValue)
    {
        Assert.AreEqual(PeriodPattern.NormalizingIso.Parse(expectedValue.Trim('"')).Value, this.scenarioContext.Get<NodaTime.Period>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="NodaTime.Period"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Period to JsonDuration")]
    public void WhenICastThePeriodToJsonDuration()
    {
        this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<NodaTime.Period>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Corvus.Json.Period"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDuration to Corvus Period")]
    public void WhenICastTheJsonDurationToCorvusPeriod()
    {
        this.scenarioContext.Set((Corvus.Json.Period)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonDuration"/> in the context value <see cref="CastResultKey"/> with the given Period.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="Corvus.Json.Period"/>.</param>
    [Then("the result should equal the Corvus Period (.*)")]
    public void ThenTheResultShouldEqualTheCorvusPeriod(string expectedValue)
    {
        Assert.AreEqual(Corvus.Json.Period.Parse(expectedValue.Trim('"').AsSpan()), this.scenarioContext.Get<Corvus.Json.Period>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="Corvus.Json.Period"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Corvus Period to JsonDuration")]
    public void WhenICastTheCorvusPeriodToJsonDuration()
    {
        this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<Corvus.Json.Period>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDuration to string")]
    public void WhenICastTheJsonDurationToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonDuration")]
    public void WhenICastTheStringToJsonDuration()
    {
        this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* string */

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonAny")]
    public void WhenICastTheJsonStringToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonString")]
    public void WhenICastTheJsonStringToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to string")]
    public void WhenICastTheJsonStringToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonString")]
    public void WhenICastTheStringToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* any */

    /// <summary>
    /// Casts the <see cref="bool"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the bool to JsonAny")]
    public void WhenICastTheBoolToJsonAny()
    {
        this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<bool>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonAny")]
    public void WhenICastTheStringToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* hostname */

    /// <summary>
    /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHostname to JsonAny")]
    public void WhenICastTheJsonHostnameToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonHostname"/> in the context value <see cref="CastResultKey"/> with the given JsonHostname.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonHostname"/>.</param>
    [Then("the result should equal the JsonHostname (.*)")]
    public void ThenTheResultShouldEqualTheJsonHostname(string expectedValue)
    {
        var expected = JsonHostname.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonHostname>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHostname to JsonString")]
    public void WhenICastTheJsonHostnameToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonHostname")]
    public void WhenICastTheJsonStringToJsonHostname()
    {
        this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHostname to string")]
    public void WhenICastTheJsonHostnameToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonHostname")]
    public void WhenICastTheStringToJsonHostname()
    {
        this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* idnIdnEmail */

    /// <summary>
    /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnEmail to JsonAny")]
    public void WhenICastTheJsonIdnEmailToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIdnEmail"/> in the context value <see cref="CastResultKey"/> with the given JsonIdnEmail.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIdnEmail"/>.</param>
    [Then("the result should equal the JsonIdnEmail (.*)")]
    public void ThenTheResultShouldEqualTheJsonIdnEmail(string expectedValue)
    {
        var expected = JsonIdnEmail.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIdnEmail>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnEmail to JsonString")]
    public void WhenICastTheJsonIdnEmailToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIdnEmail")]
    public void WhenICastTheJsonStringToJsonIdnEmail()
    {
        this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnEmail to string")]
    public void WhenICastTheJsonIdnEmailToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIdnEmail")]
    public void WhenICastTheStringToJsonIdnEmail()
    {
        this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* idnHostname */

    /// <summary>
    /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnHostname to JsonAny")]
    public void WhenICastTheJsonIdnHostnameToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIdnHostname"/> in the context value <see cref="CastResultKey"/> with the given JsonIdnHostname.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIdnHostname"/>.</param>
    [Then("the result should equal the JsonIdnHostname (.*)")]
    public void ThenTheResultShouldEqualTheJsonIdnHostname(string expectedValue)
    {
        var expected = JsonIdnHostname.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIdnHostname>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnHostname to JsonString")]
    public void WhenICastTheJsonIdnHostnameToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIdnHostname")]
    public void WhenICastTheJsonStringToJsonIdnHostname()
    {
        this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIdnHostname to string")]
    public void WhenICastTheJsonIdnHostnameToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIdnHostname")]
    public void WhenICastTheStringToJsonIdnHostname()
    {
        this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to JsonAny")]
    public void WhenICastTheJsonIntegerToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonInteger"/> in the context value <see cref="CastResultKey"/> with the given JsonInteger.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonInteger (.*)")]
    public void ThenTheResultShouldEqualTheJsonInteger(string expectedValue)
    {
        Assert.AreEqual(JsonInteger.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to long")]
    public void WhenICastTheJsonIntegerToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonInteger")]
    public void WhenICastTheLongToJsonInteger()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to int")]
    public void WhenICastTheJsonIntegerToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonInteger")]
    public void WhenICastTheIntToJsonInteger()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to double")]
    public void WhenICastTheJsonIntegerToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonInteger")]
    public void WhenICastTheDoubleToJsonInteger()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to float")]
    public void WhenICastTheJsonIntegerToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonInteger")]
    public void WhenICastTheFloatToJsonInteger()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to ulong")]
    public void WhenICastTheJsonIntegerToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to uint")]
    public void WhenICastTheJsonIntegerToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to ushort")]
    public void WhenICastTheJsonIntegerToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to byte")]
    public void WhenICastTheJsonIntegerToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to short")]
    public void WhenICastTheJsonIntegerToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to sbyte")]
    public void WhenICastTheJsonIntegerToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to JsonAny")]
    public void WhenICastTheJsonUInt128ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUInt128"/> in the context value <see cref="CastResultKey"/> with the given JsonUInt128.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonUInt128 (.*)")]
    public void ThenTheResultShouldEqualTheJsonUInt128(string expectedValue)
    {
        Assert.AreEqual(JsonUInt128.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to long")]
    public void WhenICastTheJsonUInt128ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonUInt128")]
    public void WhenICastTheLongToJsonUInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt128)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to int")]
    public void WhenICastTheJsonUInt128ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonUInt128")]
    public void WhenICastTheIntToJsonUInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt128)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to double")]
    public void WhenICastTheJsonUInt128ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonUInt128")]
    public void WhenICastTheDoubleToJsonUInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt128)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to float")]
    public void WhenICastTheJsonUInt128ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonUInt128")]
    public void WhenICastTheFloatToJsonUInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt128)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to ulong")]
    public void WhenICastTheJsonUInt128ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to uint")]
    public void WhenICastTheJsonUInt128ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to ushort")]
    public void WhenICastTheJsonUInt128ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to byte")]
    public void WhenICastTheJsonUInt128ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to short")]
    public void WhenICastTheJsonUInt128ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to sbyte")]
    public void WhenICastTheJsonUInt128ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to JsonAny")]
    public void WhenICastTheJsonInt128ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonInt128"/> in the context value <see cref="CastResultKey"/> with the given JsonInt128.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonInt128 (.*)")]
    public void ThenTheResultShouldEqualTheJsonInt128(string expectedValue)
    {
        Assert.AreEqual(JsonInt128.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to long")]
    public void WhenICastTheJsonInt128ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonInt128")]
    public void WhenICastTheLongToJsonInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt128)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to int")]
    public void WhenICastTheJsonInt128ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonInt128")]
    public void WhenICastTheIntToJsonInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt128)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to double")]
    public void WhenICastTheJsonInt128ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonInt128")]
    public void WhenICastTheDoubleToJsonInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt128)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to float")]
    public void WhenICastTheJsonInt128ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt128"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonInt128")]
    public void WhenICastTheFloatToJsonInt128()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt128)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to ulong")]
    public void WhenICastTheJsonInt128ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to uint")]
    public void WhenICastTheJsonInt128ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to ushort")]
    public void WhenICastTheJsonInt128ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to byte")]
    public void WhenICastTheJsonInt128ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to short")]
    public void WhenICastTheJsonInt128ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to sbyte")]
    public void WhenICastTheJsonInt128ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to JsonAny")]
    public void WhenICastTheJsonInt64ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonInt64"/> in the context value <see cref="CastResultKey"/> with the given JsonInt64.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonInt64 (.*)")]
    public void ThenTheResultShouldEqualTheJsonInt64(string expectedValue)
    {
        Assert.AreEqual(JsonInt64.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to long")]
    public void WhenICastTheJsonInt64ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonInt64")]
    public void WhenICastTheLongToJsonInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt64)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to int")]
    public void WhenICastTheJsonInt64ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonInt64")]
    public void WhenICastTheIntToJsonInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt64)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to double")]
    public void WhenICastTheJsonInt64ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonInt64")]
    public void WhenICastTheDoubleToJsonInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt64)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to float")]
    public void WhenICastTheJsonInt64ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonInt64")]
    public void WhenICastTheFloatToJsonInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt64)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to ulong")]
    public void WhenICastTheJsonInt64ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to uint")]
    public void WhenICastTheJsonInt64ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to ushort")]
    public void WhenICastTheJsonInt64ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to byte")]
    public void WhenICastTheJsonInt64ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to short")]
    public void WhenICastTheJsonInt64ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to sbyte")]
    public void WhenICastTheJsonInt64ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to JsonAny")]
    public void WhenICastTheJsonInt32ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonInt32"/> in the context value <see cref="CastResultKey"/> with the given JsonInt32.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonInt32 (.*)")]
    public void ThenTheResultShouldEqualTheJsonInt32(string expectedValue)
    {
        Assert.AreEqual(JsonInt32.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to long")]
    public void WhenICastTheJsonInt32ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonInt32")]
    public void WhenICastTheLongToJsonInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt32)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to int")]
    public void WhenICastTheJsonInt32ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonInt32")]
    public void WhenICastTheIntToJsonInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt32)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to double")]
    public void WhenICastTheJsonInt32ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonInt32")]
    public void WhenICastTheDoubleToJsonInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt32)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to float")]
    public void WhenICastTheJsonInt32ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonInt32")]
    public void WhenICastTheFloatToJsonInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt32)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to ulong")]
    public void WhenICastTheJsonInt32ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to uint")]
    public void WhenICastTheJsonInt32ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to ushort")]
    public void WhenICastTheJsonInt32ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to byte")]
    public void WhenICastTheJsonInt32ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to short")]
    public void WhenICastTheJsonInt32ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to sbyte")]
    public void WhenICastTheJsonInt32ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to JsonAny")]
    public void WhenICastTheJsonInt16ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonInt16"/> in the context value <see cref="CastResultKey"/> with the given JsonInt16.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonInt16 (.*)")]
    public void ThenTheResultShouldEqualTheJsonInt16(string expectedValue)
    {
        Assert.AreEqual(JsonInt16.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to long")]
    public void WhenICastTheJsonInt16ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonInt16")]
    public void WhenICastTheLongToJsonInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt16)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to int")]
    public void WhenICastTheJsonInt16ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonInt16")]
    public void WhenICastTheIntToJsonInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt16)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to double")]
    public void WhenICastTheJsonInt16ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonInt16")]
    public void WhenICastTheDoubleToJsonInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt16)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to float")]
    public void WhenICastTheJsonInt16ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonInt16")]
    public void WhenICastTheFloatToJsonInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonInt16)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to ulong")]
    public void WhenICastTheJsonInt16ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to uint")]
    public void WhenICastTheJsonInt16ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to ushort")]
    public void WhenICastTheJsonInt16ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to byte")]
    public void WhenICastTheJsonInt16ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to short")]
    public void WhenICastTheJsonInt16ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to sbyte")]
    public void WhenICastTheJsonInt16ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to JsonAny")]
    public void WhenICastTheJsonSByteToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonSByte"/> in the context value <see cref="CastResultKey"/> with the given JsonSByte.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonSByte (.*)")]
    public void ThenTheResultShouldEqualTheJsonSByte(string expectedValue)
    {
        Assert.AreEqual(JsonSByte.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to long")]
    public void WhenICastTheJsonSByteToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonSByte")]
    public void WhenICastTheLongToJsonSByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSByte)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to int")]
    public void WhenICastTheJsonSByteToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonSByte")]
    public void WhenICastTheIntToJsonSByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSByte)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to double")]
    public void WhenICastTheJsonSByteToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonSByte")]
    public void WhenICastTheDoubleToJsonSByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSByte)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to float")]
    public void WhenICastTheJsonSByteToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonSByte")]
    public void WhenICastTheFloatToJsonSByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSByte)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to ulong")]
    public void WhenICastTheJsonSByteToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to uint")]
    public void WhenICastTheJsonSByteToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to ushort")]
    public void WhenICastTheJsonSByteToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to byte")]
    public void WhenICastTheJsonSByteToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to short")]
    public void WhenICastTheJsonSByteToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to sbyte")]
    public void WhenICastTheJsonSByteToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to JsonAny")]
    public void WhenICastTheJsonUInt64ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUInt64"/> in the context value <see cref="CastResultKey"/> with the given JsonUInt64.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonUInt64 (.*)")]
    public void ThenTheResultShouldEqualTheJsonUInt64(string expectedValue)
    {
        Assert.AreEqual(JsonUInt64.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to long")]
    public void WhenICastTheJsonUInt64ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonUInt64")]
    public void WhenICastTheLongToJsonUInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt64)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to int")]
    public void WhenICastTheJsonUInt64ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonUInt64")]
    public void WhenICastTheIntToJsonUInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt64)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to double")]
    public void WhenICastTheJsonUInt64ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonUInt64")]
    public void WhenICastTheDoubleToJsonUInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt64)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to float")]
    public void WhenICastTheJsonUInt64ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt64"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonUInt64")]
    public void WhenICastTheFloatToJsonUInt64()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt64)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to ulong")]
    public void WhenICastTheJsonUInt64ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to uint")]
    public void WhenICastTheJsonUInt64ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to ushort")]
    public void WhenICastTheJsonUInt64ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to byte")]
    public void WhenICastTheJsonUInt64ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to short")]
    public void WhenICastTheJsonUInt64ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to sbyte")]
    public void WhenICastTheJsonUInt64ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to JsonAny")]
    public void WhenICastTheJsonUInt32ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUInt32"/> in the context value <see cref="CastResultKey"/> with the given JsonUInt32.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonUInt32 (.*)")]
    public void ThenTheResultShouldEqualTheJsonUInt32(string expectedValue)
    {
        Assert.AreEqual(JsonUInt32.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to long")]
    public void WhenICastTheJsonUInt32ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonUInt32")]
    public void WhenICastTheLongToJsonUInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt32)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to int")]
    public void WhenICastTheJsonUInt32ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonUInt32")]
    public void WhenICastTheIntToJsonUInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt32)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to double")]
    public void WhenICastTheJsonUInt32ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonUInt32")]
    public void WhenICastTheDoubleToJsonUInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt32)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to float")]
    public void WhenICastTheJsonUInt32ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt32"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonUInt32")]
    public void WhenICastTheFloatToJsonUInt32()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt32)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to ulong")]
    public void WhenICastTheJsonUInt32ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to uint")]
    public void WhenICastTheJsonUInt32ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to ushort")]
    public void WhenICastTheJsonUInt32ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to byte")]
    public void WhenICastTheJsonUInt32ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to short")]
    public void WhenICastTheJsonUInt32ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to sbyte")]
    public void WhenICastTheJsonUInt32ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to JsonAny")]
    public void WhenICastTheJsonUInt16ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUInt16"/> in the context value <see cref="CastResultKey"/> with the given JsonUInt16.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonUInt16 (.*)")]
    public void ThenTheResultShouldEqualTheJsonUInt16(string expectedValue)
    {
        Assert.AreEqual(JsonUInt16.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to long")]
    public void WhenICastTheJsonUInt16ToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonUInt16")]
    public void WhenICastTheLongToJsonUInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt16)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to int")]
    public void WhenICastTheJsonUInt16ToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonUInt16")]
    public void WhenICastTheIntToJsonUInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt16)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to double")]
    public void WhenICastTheJsonUInt16ToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonUInt16")]
    public void WhenICastTheDoubleToJsonUInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt16)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to float")]
    public void WhenICastTheJsonUInt16ToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUInt16"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonUInt16")]
    public void WhenICastTheFloatToJsonUInt16()
    {
        this.scenarioContext.Set<IJsonValue>((JsonUInt16)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to ulong")]
    public void WhenICastTheJsonUInt16ToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to uint")]
    public void WhenICastTheJsonUInt16ToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to ushort")]
    public void WhenICastTheJsonUInt16ToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to byte")]
    public void WhenICastTheJsonUInt16ToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to short")]
    public void WhenICastTheJsonUInt16ToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to sbyte")]
    public void WhenICastTheJsonUInt16ToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* integer */

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to JsonAny")]
    public void WhenICastTheJsonByteToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonByte"/> in the context value <see cref="CastResultKey"/> with the given JsonByte.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonByte (.*)")]
    public void ThenTheResultShouldEqualTheJsonByte(string expectedValue)
    {
        Assert.AreEqual(JsonByte.ParseValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to long")]
    public void WhenICastTheJsonByteToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonByte")]
    public void WhenICastTheLongToJsonByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonByte)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to int")]
    public void WhenICastTheJsonByteToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonByte")]
    public void WhenICastTheIntToJsonByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonByte)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to double")]
    public void WhenICastTheJsonByteToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonByte")]
    public void WhenICastTheDoubleToJsonByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonByte)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to float")]
    public void WhenICastTheJsonByteToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonByte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonByte")]
    public void WhenICastTheFloatToJsonByte()
    {
        this.scenarioContext.Set<IJsonValue>((JsonByte)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* additional cast */

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ulong"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to ulong")]
    public void WhenICastTheJsonByteToUlong()
    {
        this.scenarioContext.Set((ulong)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="uint"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to uint")]
    public void WhenICastTheJsonByteToUint()
    {
        this.scenarioContext.Set((uint)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ushort"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to ushort")]
    public void WhenICastTheJsonByteToUshort()
    {
        this.scenarioContext.Set((ushort)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="byte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to byte")]
    public void WhenICastTheJsonByteToByte()
    {
        this.scenarioContext.Set((byte)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="short"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to short")]
    public void WhenICastTheJsonByteToShort()
    {
        this.scenarioContext.Set((short)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="sbyte"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to sbyte")]
    public void WhenICastTheJsonByteToSbyte()
    {
        this.scenarioContext.Set((sbyte)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* number */

    /// <summary>
    /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInteger to JsonNumber")]
    public void WhenICastTheJsonIntegerToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt128 to JsonNumber")]
    public void WhenICastTheJsonUInt128ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonUInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt128"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt128 to JsonNumber")]
    public void WhenICastTheJsonInt128ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInt128>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt64 to JsonNumber")]
    public void WhenICastTheJsonInt64ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt32 to JsonNumber")]
    public void WhenICastTheJsonInt32ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonInt16 to JsonNumber")]
    public void WhenICastTheJsonInt16ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSByte to JsonNumber")]
    public void WhenICastTheJsonSByteToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonSByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt64"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt64 to JsonNumber")]
    public void WhenICastTheJsonUInt64ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonUInt64>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt32"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt32 to JsonNumber")]
    public void WhenICastTheJsonUInt32ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonUInt32>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUInt16"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUInt16 to JsonNumber")]
    public void WhenICastTheJsonUInt16ToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonUInt16>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonByte"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonByte to JsonNumber")]
    public void WhenICastTheJsonByteToJsonNumber()
    {
        this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonByte>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to JsonAny")]
    public void WhenICastTheJsonNumberToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to JsonAny")]
    public void WhenICastTheJsonDoubleToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to JsonAny")]
    public void WhenICastTheJsonSingleToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to JsonAny")]
    public void WhenICastTheJsonHalfToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to JsonAny")]
    public void WhenICastTheJsonDecimalToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonNumber"/> in the context value <see cref="CastResultKey"/> with the given JsonNumber.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonNumber (.*)")]
    public void ThenTheResultShouldEqualTheJsonNumber(string expectedValue)
    {
        Assert.AreEqual((double)JsonNumber.ParseValue(expectedValue), (double)this.scenarioContext.Get<IJsonValue>(CastResultKey).AsNumber, 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="JsonSingle"/> in the context value <see cref="CastResultKey"/> with the given JsonSingle.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonSingle (.*)")]
    public void ThenTheResultShouldEqualTheJsonSingle(string expectedValue)
    {
        Assert.AreEqual((double)JsonSingle.ParseValue(expectedValue), (double)this.scenarioContext.Get<IJsonValue>(CastResultKey).AsNumber, 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="JsonHalf"/> in the context value <see cref="CastResultKey"/> with the given JsonHalf.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonHalf (.*)")]
    public void ThenTheResultShouldEqualTheJsonHalf(string expectedValue)
    {
        Assert.AreEqual((double)JsonHalf.ParseValue(expectedValue), (double)this.scenarioContext.Get<IJsonValue>(CastResultKey).AsNumber, 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="JsonDouble"/> in the context value <see cref="CastResultKey"/> with the given JsonDouble.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonDouble (.*)")]
    public void ThenTheResultShouldEqualTheJsonDouble(string expectedValue)
    {
        Assert.AreEqual((double)JsonDouble.ParseValue(expectedValue), (double)this.scenarioContext.Get<IJsonValue>(CastResultKey).AsNumber, 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="JsonDecimal"/> in the context value <see cref="CastResultKey"/> with the given JsonDecimal.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the JsonDecimal (.*)")]
    public void ThenTheResultShouldEqualTheJsonDecimal(string expectedValue)
    {
        Assert.AreEqual((double)JsonDecimal.ParseValue(expectedValue), (double)this.scenarioContext.Get<IJsonValue>(CastResultKey).AsNumber, 0.00001);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to long")]
    public void WhenICastTheJsonNumberToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonNumber")]
    public void WhenICastTheLongToJsonNumber()
    {
        this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to int")]
    public void WhenICastTheJsonNumberToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonNumber")]
    public void WhenICastTheIntToJsonNumber()
    {
        this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to double")]
    public void WhenICastTheJsonNumberToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonNumber")]
    public void WhenICastTheDoubleToJsonNumber()
    {
        this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to float")]
    public void WhenICastTheJsonNumberToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonNumber")]
    public void WhenICastTheFloatToJsonNumber()
    {
        this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="decimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonNumber to decimal")]
    public void WhenICastTheJsonNumberToDecimal()
    {
        this.scenarioContext.Set((decimal)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="decimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the decimal to JsonNumber")]
    public void WhenICastTheDecimalToJsonNumber()
    {
        this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<decimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* cast */

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to long")]
    public void WhenICastTheJsonDecimalToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDecimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonDecimal")]
    public void WhenICastTheLongToJsonDecimal()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDecimal)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to int")]
    public void WhenICastTheJsonDecimalToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDecimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonDecimal")]
    public void WhenICastTheIntToJsonDecimal()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDecimal)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to double")]
    public void WhenICastTheJsonDecimalToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDecimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonDecimal")]
    public void WhenICastTheDoubleToJsonDecimal()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDecimal)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to float")]
    public void WhenICastTheJsonDecimalToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDecimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonDecimal")]
    public void WhenICastTheFloatToJsonDecimal()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDecimal)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDecimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="decimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDecimal to decimal")]
    public void WhenICastTheJsonDecimalToDecimal()
    {
        this.scenarioContext.Set((decimal)this.scenarioContext.Get<JsonDecimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="decimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDecimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the decimal to JsonDecimal")]
    public void WhenICastTheDecimalToJsonDecimal()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDecimal)this.scenarioContext.Get<decimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* cast */

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to long")]
    public void WhenICastTheJsonDoubleToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDouble"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonDouble")]
    public void WhenICastTheLongToJsonDouble()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDouble)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to int")]
    public void WhenICastTheJsonDoubleToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDouble"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonDouble")]
    public void WhenICastTheIntToJsonDouble()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDouble)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to double")]
    public void WhenICastTheJsonDoubleToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDouble"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonDouble")]
    public void WhenICastTheDoubleToJsonDouble()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDouble)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to float")]
    public void WhenICastTheJsonDoubleToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDouble"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonDouble")]
    public void WhenICastTheFloatToJsonDouble()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDouble)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonDouble"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="decimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonDouble to decimal")]
    public void WhenICastTheJsonDoubleToDecimal()
    {
        this.scenarioContext.Set((decimal)this.scenarioContext.Get<JsonDouble>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="decimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDouble"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the decimal to JsonDouble")]
    public void WhenICastTheDecimalToJsonDouble()
    {
        this.scenarioContext.Set<IJsonValue>((JsonDouble)this.scenarioContext.Get<decimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* cast */

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to long")]
    public void WhenICastTheJsonSingleToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSingle"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonSingle")]
    public void WhenICastTheLongToJsonSingle()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSingle)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to int")]
    public void WhenICastTheJsonSingleToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSingle"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonSingle")]
    public void WhenICastTheIntToJsonSingle()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSingle)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to double")]
    public void WhenICastTheJsonSingleToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSingle"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonSingle")]
    public void WhenICastTheDoubleToJsonSingle()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSingle)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to float")]
    public void WhenICastTheJsonSingleToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSingle"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonSingle")]
    public void WhenICastTheFloatToJsonSingle()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSingle)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonSingle"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="decimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonSingle to decimal")]
    public void WhenICastTheJsonSingleToDecimal()
    {
        this.scenarioContext.Set((decimal)this.scenarioContext.Get<JsonSingle>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="decimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonSingle"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the decimal to JsonSingle")]
    public void WhenICastTheDecimalToJsonSingle()
    {
        this.scenarioContext.Set<IJsonValue>((JsonSingle)this.scenarioContext.Get<decimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* half */

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to long")]
    public void WhenICastTheJsonHalfToLong()
    {
        this.scenarioContext.Set((long)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHalf"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the long to JsonHalf")]
    public void WhenICastTheLongToJsonHalf()
    {
        this.scenarioContext.Set<IJsonValue>((JsonHalf)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to int")]
    public void WhenICastTheJsonHalfToInt()
    {
        this.scenarioContext.Set((int)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHalf"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the int to JsonHalf")]
    public void WhenICastTheIntToJsonHalf()
    {
        this.scenarioContext.Set<IJsonValue>((JsonHalf)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to double")]
    public void WhenICastTheJsonHalfToDouble()
    {
        this.scenarioContext.Set((double)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHalf"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the double to JsonHalf")]
    public void WhenICastTheDoubleToJsonHalf()
    {
        this.scenarioContext.Set<IJsonValue>((JsonHalf)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to float")]
    public void WhenICastTheJsonHalfToFloat()
    {
        this.scenarioContext.Set((float)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHalf"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the float to JsonHalf")]
    public void WhenICastTheFloatToJsonHalf()
    {
        this.scenarioContext.Set<IJsonValue>((JsonHalf)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonHalf"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="decimal"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonHalf to decimal")]
    public void WhenICastTheJsonHalfToDecimal()
    {
        this.scenarioContext.Set((decimal)this.scenarioContext.Get<JsonHalf>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="decimal"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHalf"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the decimal to JsonHalf")]
    public void WhenICastTheDecimalToJsonHalf()
    {
        this.scenarioContext.Set<IJsonValue>((JsonHalf)this.scenarioContext.Get<decimal>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* uuid */

    /// <summary>
    /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUuid to JsonAny")]
    public void WhenICastTheJsonUuidToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUuid"/> in the context value <see cref="CastResultKey"/> with the given JsonUuid.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonUuid"/>.</param>
    [Then("the result should equal the JsonUuid (.*)")]
    public void ThenTheResultShouldEqualTheJsonUuid(string expectedValue)
    {
        var expected = JsonUuid.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonUuid>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUuid to JsonString")]
    public void WhenICastTheJsonUuidToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonUuid")]
    public void WhenICastTheJsonStringToJsonUuid()
    {
        this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUuid to string")]
    public void WhenICastTheJsonUuidToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonUuid")]
    public void WhenICastTheStringToJsonUuid()
    {
        this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Guid"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUuid to Guid")]
    public void WhenICastTheJsonUuidToGuid()
    {
        this.scenarioContext.Set((Guid)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Guid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Guid to JsonUuid")]
    public void WhenICastTheGuidToJsonUuid()
    {
        this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<Guid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="Guid"/> in the context value <see cref="CastResultKey"/> with the given long.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the Guid \"(.*)\"")]
    public void ThenTheResultShouldEqualTheGuid(Guid expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<Guid>(CastResultKey));
    }

    /* ipV4 */

    /// <summary>
    /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV4 to JsonAny")]
    public void WhenICastTheJsonIpV4ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIpV4"/> in the context value <see cref="CastResultKey"/> with the given JsonIpV4.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIpV4"/>.</param>
    [Then("the result should equal the JsonIpV4 (.*)")]
    public void ThenTheResultShouldEqualTheJsonIpV4(string expectedValue)
    {
        var expected = JsonIpV4.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIpV4>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV4 to JsonString")]
    public void WhenICastTheJsonIpV4ToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIpV4")]
    public void WhenICastTheJsonStringToJsonIpV4()
    {
        this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV4 to string")]
    public void WhenICastTheJsonIpV4ToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIpV4")]
    public void WhenICastTheStringToJsonIpV4()
    {
        this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="IPAddress"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV4 to IPAddress")]
    public void WhenICastTheJsonIpV4ToIPAddress()
    {
        this.scenarioContext.Set((IPAddress)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="IPAddress"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the IPAddress to JsonIpV4")]
    public void WhenICastTheIPAddressToJsonIpV4()
    {
        this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<IPAddress>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* ipV6 */

    /// <summary>
    /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV6 to JsonAny")]
    public void WhenICastTheJsonIpV6ToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIpV6"/> in the context value <see cref="CastResultKey"/> with the given JsonIpV6.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIpV6"/>.</param>
    [Then("the result should equal the JsonIpV6 (.*)")]
    public void ThenTheResultShouldEqualTheJsonIpV6(string expectedValue)
    {
        var expected = JsonIpV6.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIpV6>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV6 to JsonString")]
    public void WhenICastTheJsonIpV6ToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIpV6")]
    public void WhenICastTheJsonStringToJsonIpV6()
    {
        this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV6 to string")]
    public void WhenICastTheJsonIpV6ToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIpV6")]
    public void WhenICastTheStringToJsonIpV6()
    {
        this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="IPAddress"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIpV6 to IPAddress")]
    public void WhenICastTheJsonIpV6ToIPAddress()
    {
        this.scenarioContext.Set((IPAddress)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="IPAddress"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the IPAddress to JsonIpV6")]
    public void WhenICastTheIPAddressToJsonIpV6()
    {
        this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<IPAddress>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* regex */

    /// <summary>
    /// Gets the value store in <see cref="CastResultKey"/> and matches it against the Regex serialized in <paramref name="expected"/>.
    /// </summary>
    /// <param name="expected">The serialized form of the result to match.</param>
    [Then("the result should equal the Regex (.*)")]
    public void ThenTheResultShouldEqualTheRegex(string expected)
    {
        Assert.AreEqual(new Regex(expected).ToString(), this.scenarioContext.Get<Regex>(CastResultKey).ToString());
    }

    /// <summary>
    /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRegex to JsonAny")]
    public void WhenICastTheJsonRegexToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonRegex"/> in the context value <see cref="CastResultKey"/> with the given JsonRegex.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonRegex"/>.</param>
    [Then("the result should equal the JsonRegex (.*)")]
    public void ThenTheResultShouldEqualTheJsonRegex(string expectedValue)
    {
        var expected = JsonRegex.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonRegex>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRegex to JsonString")]
    public void WhenICastTheJsonRegexToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonRegex")]
    public void WhenICastTheJsonStringToJsonRegex()
    {
        this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRegex to string")]
    public void WhenICastTheJsonRegexToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonRegex")]
    public void WhenICastTheStringToJsonRegex()
    {
        this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Regex"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonRegex to Regex")]
    public void WhenICastTheJsonRegexToRegex()
    {
        this.scenarioContext.Set((Regex)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Regex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Regex to JsonRegex")]
    public void WhenICastTheRegexToJsonRegex()
    {
        this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<Regex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* iri */

    /// <summary>
    /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIri to JsonAny")]
    public void WhenICastTheJsonIriToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIri"/> in the context value <see cref="CastResultKey"/> with the given JsonIri.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIri"/>.</param>
    [Then("the result should equal the JsonIri (.*)")]
    public void ThenTheResultShouldEqualTheJsonIri(string expectedValue)
    {
        var expected = JsonIri.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIri>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIri to JsonString")]
    public void WhenICastTheJsonIriToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIri")]
    public void WhenICastTheJsonStringToJsonIri()
    {
        this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIri to string")]
    public void WhenICastTheJsonIriToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIri")]
    public void WhenICastTheStringToJsonIri()
    {
        this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIri to Uri")]
    public void WhenICastTheJsonIriToUri()
    {
        this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Uri to JsonIri")]
    public void WhenICastTheUriToJsonIri()
    {
        this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* iriReference */

    /// <summary>
    /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIriReference to JsonAny")]
    public void WhenICastTheJsonIriReferenceToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonIriReference"/> in the context value <see cref="CastResultKey"/> with the given JsonIriReference.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonIriReference"/>.</param>
    [Then("the result should equal the JsonIriReference (.*)")]
    public void ThenTheResultShouldEqualTheJsonIriReference(string expectedValue)
    {
        var expected = JsonIriReference.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonIriReference>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIriReference to JsonString")]
    public void WhenICastTheJsonIriReferenceToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonIriReference")]
    public void WhenICastTheJsonStringToJsonIriReference()
    {
        this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIriReference to string")]
    public void WhenICastTheJsonIriReferenceToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonIriReference")]
    public void WhenICastTheStringToJsonIriReference()
    {
        this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonIriReference to Uri")]
    public void WhenICastTheJsonIriReferenceToUri()
    {
        this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Uri to JsonIriReference")]
    public void WhenICastTheUriToJsonIriReference()
    {
        this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* uri */

    /// <summary>
    /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUri to JsonAny")]
    public void WhenICastTheJsonUriToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUri"/> in the context value <see cref="CastResultKey"/> with the given JsonUri.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonUri"/>.</param>
    [Then("the result should equal the JsonUri (.*)")]
    public void ThenTheResultShouldEqualTheJsonUri(string expectedValue)
    {
        var expected = JsonUri.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonUri>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUri to JsonString")]
    public void WhenICastTheJsonUriToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonUri")]
    public void WhenICastTheJsonStringToJsonUri()
    {
        this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUri to string")]
    public void WhenICastTheJsonUriToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonUri")]
    public void WhenICastTheStringToJsonUri()
    {
        this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUri to Uri")]
    public void WhenICastTheJsonUriToUri()
    {
        this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Uri to JsonUri")]
    public void WhenICastTheUriToJsonUri()
    {
        this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /* uriReference */

    /// <summary>
    /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriReference to JsonAny")]
    public void WhenICastTheJsonUriReferenceToJsonAny()
    {
        this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="JsonUriReference"/> in the context value <see cref="CastResultKey"/> with the given JsonUriReference.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonUriReference"/>.</param>
    [Then("the result should equal the JsonUriReference (.*)")]
    public void ThenTheResultShouldEqualTheJsonUriReference(string expectedValue)
    {
        var expected = JsonUriReference.ParseValue(expectedValue);
        Assert.AreEqual(expected, this.scenarioContext.Get<JsonUriReference>(CastResultKey));
    }

    /// <summary>
    /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriReference to JsonString")]
    public void WhenICastTheJsonUriReferenceToJsonString()
    {
        this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonString to JsonUriReference")]
    public void WhenICastTheJsonStringToJsonUriReference()
    {
        this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriReference to string")]
    public void WhenICastTheJsonUriReferenceToString()
    {
        this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the string to JsonUriReference")]
    public void WhenICastTheStringToJsonUriReference()
    {
        this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the JsonUriReference to Uri")]
    public void WhenICastTheJsonUriReferenceToUri()
    {
        this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
    /// </summary>
    [When("I cast the Uri to JsonUriReference")]
    public void WhenICastTheUriToJsonUriReference()
    {
        this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
    }

    /// <summary>
    /// Compares the <see cref="long"/> in the context value <see cref="CastResultKey"/> with the given long.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the long (.*)")]
    public void ThenTheResultShouldEqualTheLong(long expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<long>(CastResultKey));
    }

    /// <summary>
    /// Compares the <see cref="int"/> in the context value <see cref="CastResultKey"/> with the given int.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="int"/>.</param>
    [Then("the result should equal the int (.*)")]
    public void ThenTheResultShouldEqualTheInt(int expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<int>(CastResultKey));
    }

    /// <summary>
    /// Compares the <see cref="IPAddress"/> in the context value <see cref="CastResultKey"/> with the given IPAddress.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="IPAddress"/>.</param>
    [Then("the result should equal the IPAddress \"(.*)\"")]
    public void ThenTheResultShouldEqualTheIPAddress(string expectedValue)
    {
        Assert.AreEqual(IPAddress.Parse(expectedValue), this.scenarioContext.Get<IPAddress>(CastResultKey));
    }

    /// <summary>
    /// Compares the <see cref="Uri"/> in the context value <see cref="CastResultKey"/> with the given IPAddress.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="Uri"/>.</param>
    [Then("the result should equal the Uri \"(.*)\"")]
    public void ThenTheResultShouldEqualTheUri(string expectedValue)
    {
        Assert.AreEqual(new Uri(expectedValue, UriKind.RelativeOrAbsolute), this.scenarioContext.Get<Uri>(CastResultKey));
    }

    [Then("the result should equal within (.*) the double (.*)")]
    public void ThenTheResultShouldEqualTheDouble(double delta, double expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<double>(CastResultKey), delta);
    }

    /// <summary>
    /// Compares the <see cref="double"/> in the context value <see cref="CastResultKey"/> with the given double.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the double (.*)")]
    public void ThenTheResultShouldEqualTheDouble(double expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<double>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="decimal"/> in the context value <see cref="CastResultKey"/> with the given decimal.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the decimal (.*)")]
    public void ThenTheResultShouldEqualTheDecimal(decimal expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<decimal>(CastResultKey));
    }

    [Then("the result should equal within (.*) the float (.*)")]
    public void ThenTheResultShouldEqualTheFloat(float delta, float expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<float>(CastResultKey), delta);
    }

    [Then("the result should equal within (.*) the decimal (.*)")]
    public void ThenTheResultShouldEqualTheDecimal(decimal delta, decimal expectedValue)
    {
        Assert.LessOrEqual(expectedValue - delta, this.scenarioContext.Get<decimal>(CastResultKey));
        Assert.GreaterOrEqual(expectedValue + delta, this.scenarioContext.Get<decimal>(CastResultKey));
    }

    /// <summary>
    /// Compares the <see cref="float"/> in the context value <see cref="CastResultKey"/> with the given float.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the float (.*)")]
    public void ThenTheResultShouldEqualTheFloat(float expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<float>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="short"/> in the context value <see cref="CastResultKey"/> with the given short.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the short (.*)")]
    public void ThenTheResultShouldEqualTheInt16(short expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<short>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="ushort"/> in the context value <see cref="CastResultKey"/> with the given ushort.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the ushort (.*)")]
    public void ThenTheResultShouldEqualTheUInt16(ushort expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<ushort>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="ulong"/> in the context value <see cref="CastResultKey"/> with the given ulong.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the ulong (.*)")]
    public void ThenTheResultShouldEqualTheUInt64(ulong expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<ulong>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="uint"/> in the context value <see cref="CastResultKey"/> with the given uint.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the uint (.*)")]
    public void ThenTheResultShouldEqualTheUInt32(uint expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<uint>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="sbyte"/> in the context value <see cref="CastResultKey"/> with the given sbyte.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the sbyte (.*)")]
    public void ThenTheResultShouldEqualTheSByte(sbyte expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<sbyte>(CastResultKey), 0.00001);
    }

    /// <summary>
    /// Compares the <see cref="byte"/> in the context value <see cref="CastResultKey"/> with the given byte.
    /// </summary>
    /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
    [Then("the result should equal the byte (.*)")]
    public void ThenTheResultShouldEqualTheByte(byte expectedValue)
    {
        Assert.AreEqual(expectedValue, this.scenarioContext.Get<byte>(CastResultKey), 0.00001);
    }
}