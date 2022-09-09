// <copyright file="JsonValueSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
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
public class JsonValueSteps
{
    /// <summary>
    /// The key for the subject under test.
    /// </summary>
    internal const string SubjectUnderTest = "Value";

    /// <summary>
    /// The key for a serialization result.
    /// </summary>
    internal const string SerializationResult = "SerializationResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonValueSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /* validation */

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.Object"/>.
    /// </summary>
    [Then("the round-tripped result should be an Object")]
    public void ThenTheRound_TrippedResultShouldBeAnObject()
    {
        Assert.AreEqual(JsonValueKind.Object, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
    }

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.Array"/>.
    /// </summary>
    [Then("the round-tripped result should be an Array")]
    public void ThenTheRound_TrippedResultShouldBeAnArray()
    {
        Assert.AreEqual(JsonValueKind.Array, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
    }

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.Number"/>.
    /// </summary>
    [Then("the round-tripped result should be a Number")]
    public void ThenTheRound_TrippedResultShouldBeANumber()
    {
        Assert.AreEqual(JsonValueKind.Number, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
    }

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.String"/>.
    /// </summary>
    [Then("the round-tripped result should be a String")]
    public void ThenTheRound_TrippedResultShouldBeAString()
    {
        Assert.AreEqual(JsonValueKind.String, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
    }

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.
    /// </summary>
    [Then("the round-tripped result should be a Boolean")]
    public void ThenTheRound_TrippedResultShouldBeABoolean()
    {
        JsonValueKind actual = this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind;
        Assert.IsTrue(actual == JsonValueKind.True || actual == JsonValueKind.False);
    }

    /// <summary>
    /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.
    /// </summary>
    [Then("the round-tripped result should be Null")]
    public void ThenTheRound_TrippedResultShouldBeNull()
    {
        Assert.AreEqual(JsonValueKind.Null, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
    }

    /* serialization */

    /// <summary>
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, deserializaes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is round-tripped via a string")]
    public void WhenTheJsonValueIsRound_TrippedViaAString()
    {
        IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        sut.WriteTo(writer);
        writer.Flush();

        this.scenarioContext.Set(JsonAny.Parse(abw.WrittenMemory), SerializationResult);
    }

    /// <summary>
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, using <see cref="JsonValueExtensions.Serialize{TValue}(TValue)"/>, and deserializes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is round-trip serialized via a string")]
    public void WhenTheJsonValueIsRound_TripSerializedViaAString()
    {
        JsonAny sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest).AsAny;
        this.scenarioContext.Set(JsonAny.Parse(sut.Serialize()), SerializationResult);
    }

    /// <summary>
    /// Compares the string from the context variable <see cref="SerializationResult"/> with the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [Then("the round-tripped result should be equal to the JsonAny (.*)")]
    public void ThenTheRound_TrippedResultShouldBeEqualToTheJsonAny(string expected)
    {
        Assert.AreEqual(JsonAny.Parse(expected), this.scenarioContext.Get<JsonAny>(SerializationResult));
    }

    /* notAny */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonNotAny (.*)")]
    public void GivenTheJsonElementBackedJsonNotAny(string value)
    {
        if (value == "<undefined>")
        {
            this.scenarioContext.Set<JsonNotAny>(JsonNotAny.Undefined, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set((JsonNotAny)JsonAny.Parse(value), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the object backed JsonNotAny (.*)")]
    public void GivenTheObjectBackedJsonNotAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNotAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the dotnet backed JsonNotAny (.*)")]
    public void GivenTheDotnetBackedJsonNotAny(string value)
    {
        if (value == "<undefined>")
        {
            this.scenarioContext.Set<JsonNotAny>(JsonNotAny.Undefined, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set<JsonNotAny>(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the number backed JsonNotAny (.*)")]
    public void GivenTheNumberBackedJsonNotAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNotAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the array backed JsonNotAny (.*)")]
    public void GivenTheArrayBackedJsonNotAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNotAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the string backed JsonNotAny (.*)")]
    public void GivenTheStringBackedJsonNotAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNotAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the boolean backed JsonNotAny (.*)")]
    public void GivenTheBooleanBackedJsonNotAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNotAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* any */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonAny (.*)")]
    public void GivenTheJsonElementBackedJsonAny(string value)
    {
        if (value == "<undefined>")
        {
            this.scenarioContext.Set<JsonAny>(JsonAny.Undefined, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the dotnet backed JsonAny (.*)")]
    public void GivenTheDotnetBackedJsonAny(string value)
    {
        if (value == "<undefined>")
        {
            this.scenarioContext.Set<JsonAny>(default, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the object backed JsonAny (.*)")]
    public void GivenTheObjectBackedJsonAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonAny(JsonAny.Parse(value).AsImmutableDictionary()), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the number backed JsonAny (.*)")]
    public void GivenTheNumberBackedJsonAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonAny((double)JsonAny.Parse(value).AsNumber), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the array backed JsonAny (.*)")]
    public void GivenTheArrayBackedJsonAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonAny(JsonAny.Parse(value).AsArray.AsImmutableList()), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the string backed JsonAny (.*)")]
    public void GivenTheStringBackedJsonAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonAny((string)JsonAny.Parse(value).AsString), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the boolean backed JsonAny (.*)")]
    public void GivenTheBooleanBackedJsonAny(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonAny((bool)JsonAny.Parse(value).AsBoolean), SubjectUnderTest);
        }
    }

    /* string */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonString (.*)")]
    public void GivenTheJsonElementBackedJsonString(string value)
    {
        this.scenarioContext.Set<JsonString>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonString (.*)")]
    public void GivenTheDotnetBackedJsonString(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null.AsString, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).AsString.AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* boolean */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBoolean (.*)")]
    public void GivenTheJsonElementBackedJsonBoolean(string value)
    {
        this.scenarioContext.Set<JsonBoolean>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonBoolean (.*)")]
    public void GivenTheDotnetBackedJsonBoolean(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonAny.Null.AsBoolean, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(new JsonBoolean(value == "true"), SubjectUnderTest);
        }
    }

    /* array */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonArray (.*)")]
    public void GivenTheJsonElementBackedJsonArray(string value)
    {
        this.scenarioContext.Set<JsonArray>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonArray (.*)")]
    public void GivenTheDotnetBackedJsonArray(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonArray.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).AsArray.AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* base64content */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBase64Content (.*)")]
    public void GivenTheJsonElementBackedJsonBase64Content(string value)
    {
        this.scenarioContext.Set<JsonBase64Content>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonBase64Content (.*)")]
    public void GivenTheDotnetBackedJsonBase64Content(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonBase64Content.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonBase64Content>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* base64string */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBase64String (.*)")]
    public void GivenTheJsonElementBackedJsonBase64String(string value)
    {
        this.scenarioContext.Set<JsonBase64String>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonBase64String (.*)")]
    public void GivenTheDotnetBackedJsonBase64String(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonBase64String.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonBase64String>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* content */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonContent (.*)")]
    public void GivenTheJsonElementBackedJsonContent(string value)
    {
        this.scenarioContext.Set<JsonContent>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonContent (.*)")]
    public void GivenTheDotnetBackedJsonContent(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonContent.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonContent>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* date */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonDate (.*)")]
    public void GivenTheJsonElementBackedJsonDate(string value)
    {
        this.scenarioContext.Set<JsonDate>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonDate (.*)")]
    public void GivenTheDotnetBackedJsonDate(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonDate.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonDate>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* dateTime */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonDateTime (.*)")]
    public void GivenTheJsonElementBackedJsonDateTime(string value)
    {
        this.scenarioContext.Set<JsonDateTime>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonDateTime (.*)")]
    public void GivenTheDotnetBackedJsonDateTime(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonDateTime.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonDateTime>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* duration */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonDuration (.*)")]
    public void GivenTheJsonElementBackedJsonDuration(string value)
    {
        this.scenarioContext.Set<JsonDuration>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonDuration (.*)")]
    public void GivenTheDotnetBackedJsonDuration(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonDuration.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonDuration>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* email */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonEmail (.*)")]
    public void GivenTheJsonElementBackedJsonEmail(string value)
    {
        this.scenarioContext.Set<JsonEmail>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonEmail (.*)")]
    public void GivenTheDotnetBackedJsonEmail(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonEmail.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonEmail>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* idnEmail */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIdnEmail (.*)")]
    public void GivenTheJsonElementBackedJsonIdnEmail(string value)
    {
        this.scenarioContext.Set<JsonIdnEmail>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIdnEmail (.*)")]
    public void GivenTheDotnetBackedJsonIdnEmail(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIdnEmail.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIdnEmail>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* hostname */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonHostname (.*)")]
    public void GivenTheJsonElementBackedJsonHostname(string value)
    {
        this.scenarioContext.Set<JsonHostname>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonHostname (.*)")]
    public void GivenTheDotnetBackedJsonHostname(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonHostname.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonHostname>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* idnHostname */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIdnHostname (.*)")]
    public void GivenTheJsonElementBackedJsonIdnHostname(string value)
    {
        this.scenarioContext.Set<JsonIdnHostname>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIdnHostname (.*)")]
    public void GivenTheDotnetBackedJsonIdnHostname(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIdnHostname.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIdnHostname>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonInteger (.*)")]
    public void GivenTheJsonElementBackedJsonInteger(string value)
    {
        this.scenarioContext.Set<JsonInteger>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonInteger (.*)")]
    public void GivenTheDotnetBackedJsonInteger(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonInteger.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonInteger>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* number */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonNumber (.*)")]
    public void GivenTheJsonElementBackedJsonNumber(string value)
    {
        this.scenarioContext.Set<JsonNumber>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonNumber (.*)")]
    public void GivenTheDotnetBackedJsonNumber(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonNumber.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonNumber>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* ipV4 */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIpV4 (.*)")]
    public void GivenTheJsonElementBackedJsonIpV4(string value)
    {
        this.scenarioContext.Set<JsonIpV4>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIpV4 (.*)")]
    public void GivenTheDotnetBackedJsonIpV4(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIpV4.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIpV4>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* ipV6 */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIpV6 (.*)")]
    public void GivenTheJsonElementBackedJsonIpV6(string value)
    {
        this.scenarioContext.Set<JsonIpV6>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIpV6 (.*)")]
    public void GivenTheDotnetBackedJsonIpV6(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIpV6.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIpV6>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* uri */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUri (.*)")]
    public void GivenTheJsonElementBackedJsonUri(string value)
    {
        this.scenarioContext.Set<JsonUri>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUri (.*)")]
    public void GivenTheDotnetBackedJsonUri(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUri.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonUri>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* uriReference */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUriReference (.*)")]
    public void GivenTheJsonElementBackedJsonUriReference(string value)
    {
        this.scenarioContext.Set<JsonUriReference>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUriReference (.*)")]
    public void GivenTheDotnetBackedJsonUriReference(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUriReference.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonUriReference>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* iri */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIri (.*)")]
    public void GivenTheJsonElementBackedJsonIri(string value)
    {
        this.scenarioContext.Set<JsonIri>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIri (.*)")]
    public void GivenTheDotnetBackedJsonIri(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIri.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIri>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* iriReference */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonIriReference (.*)")]
    public void GivenTheJsonElementBackedJsonIriReference(string value)
    {
        this.scenarioContext.Set<JsonIriReference>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonIriReference (.*)")]
    public void GivenTheDotnetBackedJsonIriReference(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonIriReference.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonIriReference>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* object */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonObject (.*)")]
    public void GivenTheJsonElementBackedJsonObject(string value)
    {
        if (value == "<undefined>")
        {
            this.scenarioContext.Set<JsonObject>(default, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set<JsonObject>(JsonAny.Parse(value), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonObject (.*)")]
    public void GivenTheDotnetBackedJsonObject(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonObject.Null, SubjectUnderTest);
        }
        else if (value == "<undefined>")
        {
            this.scenarioContext.Set(JsonObject.Undefined, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).AsObject.AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* pointer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonPointer (.*)")]
    public void GivenTheJsonElementBackedJsonPointer(string value)
    {
        this.scenarioContext.Set<JsonPointer>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonPointer (.*)")]
    public void GivenTheDotnetBackedJsonPointer(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonPointer.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonPointer>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* relativePointer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonRelativePointer (.*)")]
    public void GivenTheJsonElementBackedJsonRelativePointer(string value)
    {
        this.scenarioContext.Set<JsonRelativePointer>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonRelativePointer (.*)")]
    public void GivenTheDotnetBackedJsonRelativePointer(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonRelativePointer.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonRelativePointer>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* regex */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonRegex (.*)")]
    public void GivenTheJsonElementBackedJsonRegex(string value)
    {
        this.scenarioContext.Set<JsonRegex>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonRegex (.*)")]
    public void GivenTheDotnetBackedJsonRegex(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonRegex.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonRegex>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* uriTemplate */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUriTemplate (.*)")]
    public void GivenTheJsonElementBackedJsonUriTemplate(string value)
    {
        this.scenarioContext.Set<JsonUriTemplate>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUriTemplate (.*)")]
    public void GivenTheDotnetBackedJsonUriTemplate(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUriTemplate.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonUriTemplate>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* time */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonTime (.*)")]
    public void GivenTheJsonElementBackedJsonTime(string value)
    {
        this.scenarioContext.Set<JsonTime>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonTime (.*)")]
    public void GivenTheDotnetBackedJsonTime(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonTime.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonTime>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* uuid */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUuid (.*)")]
    public void GivenTheJsonElementBackedJsonUuid(string value)
    {
        this.scenarioContext.Set<JsonUuid>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUuid (.*)")]
    public void GivenTheDotnetBackedJsonUuid(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUuid.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonAny.Parse(value).As<JsonUuid>().AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Gets a JsonAny for the given <paramref name="value"/> and stores it in the context variable <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The serialized value to parse.</param>
    [Given("the JsonAny for (.*)")]
    public void GivenTheJsonAnyFor(string value)
    {
        this.scenarioContext.Set(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Gets a dictionary for the given <paramref name="value"/> and stores it in the context variable <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The serialized value to parse.</param>
    [Given("the ImmutableDictionary<JsonPropertyName,JsonAny> for (.*)")]
    public void GivenTheImmutableDictionaryOfStringToJsonAnyFor(string value)
    {
        this.scenarioContext.Set(JsonAny.Parse(value).AsObject.AsImmutableDictionary(), SubjectUnderTest);
    }

    /// <summary>
    /// Gets the <see cref="ImmutableList{JsonAny}"/> for the <paramref name="list"/> and stores it in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="list">The serialized from of the immutable list.</param>
    [Given("the ImmutableList<JsonAny> for (.*)")]
    public void GivenTheImmutableListOfJsonAnyFor(string list)
    {
        this.scenarioContext.Set(JsonAny.Parse(list).AsArray.AsImmutableList(), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the string <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given(@"the string for ""(.*)""")]
    public void GivenTheStringFor(string value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="Regex"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given("the Regex for \"(.*)\"")]
    public void GivenTheRegexFor(string value)
    {
        this.scenarioContext.Set(new Regex(value), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="JsonString"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given("the JsonString for (.*)")]
    public void GivenTheJsonStringFor(string value)
    {
        this.scenarioContext.Set<JsonString>(JsonAny.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="IPAddress"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given("the IPAddress for \"(.*)\"")]
    public void GivenTheIpAddressFor(string value)
    {
        this.scenarioContext.Set(IPAddress.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="Guid"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given("the Guid for \"(.*)\"")]
    public void GivenTheGuidFor(string value)
    {
        this.scenarioContext.Set(Guid.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="Uri"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given("the Uri for \"(.*)\"")]
    public void GivenTheUriFor(string value)
    {
        this.scenarioContext.Set(new Uri(value, UriKind.RelativeOrAbsolute), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="bool"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The boolean value.</param>
    [Given("the bool for (.*)")]
    public void GivenTheBoolForTrue(bool value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="long"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The int64 value.</param>
    [Given("the long for (.*)")]
    public void GivenTheLongFor(long value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="int"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The int32 value.</param>
    [Given("the int for (.*)")]
    public void GivenTheIntFor(int value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="double"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The float value.</param>
    [Given("the double for (.*)")]
    public void GivenTheDoubleFor(double value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="float"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The float value.</param>
    [Given("the float for (.*)")]
    public void GivenTheFloatFor(float value)
    {
        this.scenarioContext.Set(value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="JsonString"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given(@"the ReadOnlyMemory<byte> for ""(.*)""")]
    public void GivenTheReadOnlyMemoryOfByteFor(string value)
    {
        byte[] utf8Bytes = Encoding.UTF8.GetBytes(value);
        this.scenarioContext.Set<ReadOnlyMemory<byte>>(utf8Bytes.AsMemory(), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="JsonString"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value.</param>
    [Given(@"the ReadOnlyMemory<char> for ""(.*)""")]
    public void GivenTheReadOnlyMemoryOfCharFor(string value)
    {
        char[] valueArray = value.ToCharArray();
        this.scenarioContext.Set<ReadOnlyMemory<char>>(valueArray.AsMemory(), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="LocalDate"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value of the <see cref="LocalDate"/>.</param>
    [Given(@"the LocalDate for ""(.*)""")]
    public void GivenTheLocalDateFor(string value)
    {
        ParseResult<LocalDate> parseResult = LocalDatePattern.Iso.Parse(value);
        this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="OffsetTime"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value of the <see cref="OffsetTime"/>.</param>
    [Given(@"the OffsetTime for ""(.*)""")]
    public void GivenTheOffsetTimeFor(string value)
    {
        ParseResult<OffsetTime> parseResult = OffsetTimePattern.ExtendedIso.Parse(value);

        // Aggravatingly, NodaTime rejects a lowercase Z to indicate a 0 offset, and its custom
        // pattern language doesn't seem to enable us to specify "either Z or z". It also
        // doesn't seem to be possible to produce a pattern that only accepts 'z' and which
        // otherwise reproduces the OffsetTimePattern.ExtendedIso behaviour, because that is
        // defined in terms of the "G" standard pattern, and you don't get to refer to a
        // standard pattern from inside a custom pattern.
        // https://nodatime.org/3.1.x/userguide/offset-patterns
        // It might be possible to use RegEx instead of NodeTime. But that's a relatively
        // complex alternative that requires some research.
        if (!parseResult.Success && value.Contains('z'))
        {
            value = value.Replace('z', 'Z');
            parseResult = OffsetTimePattern.ExtendedIso.Parse(value);
        }

        this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="NodaTime.Period"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value of the <see cref="LocalDate"/>.</param>
    [Given(@"the Period for ""(.*)""")]
    public void GivenThePeriodFor(string value)
    {
        ParseResult<NodaTime.Period> parseResult = PeriodPattern.NormalizingIso.Parse(value.ToUpperInvariant());
        this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="Corvus.Json.Period"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value of the <see cref="LocalDate"/>.</param>
    [Given(@"the Corvus Period for ""(.*)""")]
    public void GivenTheCorvusPeriodFor(string value)
    {
        this.scenarioContext.Set(Corvus.Json.Period.Parse(value.ToUpperInvariant()), SubjectUnderTest);
    }

    /// <summary>
    /// Stores the <see cref="OffsetDateTime"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The string value of the <see cref="OffsetDateTime"/>.</param>
    [Given(@"the OffsetDateTime for ""(.*)""")]
    public void GivenTheOffsetDateTimeFor(string value)
    {
        ParseResult<OffsetDateTime> parseResult = OffsetDateTimePattern.ExtendedIso.Parse(value.ToUpperInvariant());
        this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
    }
}