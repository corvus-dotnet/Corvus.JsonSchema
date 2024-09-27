// <copyright file="JsonValueSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;
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
    internal const string DecodedBase64Bytes = "DecodedBase64Bytes";
    internal const string DecodedBase64ByteCount = "DecodedBase64ByteCount";

    /// <summary>
    /// The key for a serialization result.
    /// </summary>
    internal const string SerializationResult = "SerializationResult";
    internal const string SerializationException = "SerializationException";

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
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, deserializes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is round-tripped via a string")]
    public void WhenTheJsonValueIsRound_TrippedViaAString()
    {
        IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        sut.WriteTo(writer);
        writer.Flush();

        this.scenarioContext.Set(JsonAny.ParseValue(abw.WrittenSpan), SerializationResult);
    }

    /// <summary>
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, stores the resulting value in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is serialized to a string using pretty formatting")]
    public void WhenTheJsonValueIsSerializedToAStringUsingPrettyFormatting()
    {
        try
        {
            IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
            string json = sut.AsAny.Serialize(new JsonSerializerOptions { WriteIndented = true });
            this.scenarioContext.Set(json, SerializationResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, SerializationException);
        }
    }

    /// <summary>
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, deserializes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is round-tripped via Serialization without enabling inefficient serialization")]
    public void WhenTheJsonValueIsRound_TrippedViaSerializationNoInefficientSerialization()
    {
        try
        {
            JsonValueConverter.EnableInefficientDeserializationSupport = false;
            IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
            string json = JsonSerializer.Serialize(sut);
            this.scenarioContext.Set(JsonSerializer.Deserialize<JsonAny>(json), SerializationResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(ex, SerializationException);
        }
    }

    /// <summary>
    /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, deserializes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
    /// </summary>
    [When("the json value is round-tripped via Serialization enabling inefficient serialization")]
    public void WhenTheJsonValueIsRound_TrippedViaSerializationWithInefficientSerialization()
    {
        JsonValueConverter.EnableInefficientDeserializationSupport = true;
        IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
        string json = JsonSerializer.Serialize(sut, sut.GetType());
        this.scenarioContext.Set(JsonSerializer.Deserialize<JsonAny>(json), SerializationResult);
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
    /// Ensures that the <see cref="SerializationException"/> has been set with an <see cref="InvalidOperationException"/>.
    /// </summary>
    [Then("the deserialization operation should throw an InvalidOperationException")]
    public void ThenASerializationInvalidOperationExceptionShouldBeThrown()
    {
        Assert.IsTrue(this.scenarioContext.ContainsKey(SerializationException));
        object ex = this.scenarioContext.Get<object>(SerializationException);
        Assert.IsInstanceOf<InvalidOperationException>(ex);
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

    /// <summary>
    /// Compares the string from the context variable <see cref="SerializationResult"/> with the expected value.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [Then("the serialized string should equal (.*)")]
    public void ThenTheSerializedStringShouldEqual(string expected)
    {
        Assert.AreEqual(expected.Replace("\\r", "\r").Replace("\\n", "\n"), this.scenarioContext.Get<string>(SerializationResult));
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
            this.scenarioContext.Set(JsonNotAny.Undefined, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonNotAny.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonNotAny.Undefined, SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Undefined, SubjectUnderTest);
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
            JsonAny result = JsonAny.Parse(value).AsDotnetBackedValue();
            if (result.ValueKind == JsonValueKind.Object)
            {
                JsonObject interim = result.AsObject;
                foreach (JsonObjectProperty property in interim.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.String &&
                        property.Value.Equals("\u003cundefined\u003e"))
                    {
                        interim = interim.SetProperty(property.Name, JsonString.Undefined);
                    }
                }

                result = interim;
            }

            this.scenarioContext.Set(result, SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonAny.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonString.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonString.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonString.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonBoolean.Parse(value), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonArray.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonArray.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonBase64Content.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonBase64Content.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* base64content */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBase64ContentPre201909 (.*)")]
    public void GivenTheJsonElementBackedJsonBase64ContentPre201909(string value)
    {
        this.scenarioContext.Set(JsonBase64ContentPre201909.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonBase64ContentPre201909 (.*)")]
    public void GivenTheDotnetBackedJsonBase64ContentPre201909(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonBase64ContentPre201909.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonBase64ContentPre201909.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* base64string */

    [When("I get the decoded value for the JsonBase64String with a buffer size of (.*)")]
    public void WhenIGetTheDecodedValueForTheJsonBase64String(int bufferSize)
    {
        JsonBase64String base64String = this.scenarioContext.Get<JsonBase64String>(SubjectUnderTest);

        byte[] buffer = new byte[bufferSize];
        if (base64String.TryGetDecodedBase64Bytes(buffer.AsSpan(), out int written))
        {
            // Just slice off what we need. Don't worry too much about allocations :)
            this.scenarioContext.Set(buffer.AsSpan()[..written].ToArray(), DecodedBase64Bytes);
        }

        this.scenarioContext.Set(written, DecodedBase64ByteCount);
    }

    [Then("the decoded value should be the UTF8 bytes for '([^']*)'")]
    public void ThenTheDecodedValueShouldBeTheUTFBytesFor(string expected)
    {
        byte[] bytes = this.scenarioContext.Get<byte[]>(DecodedBase64Bytes);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(expected), bytes);
    }

    [Then("the decoded byte count should be ([0-9]*)")]
    public void ThenTheDecodedByteCountShouldBe(int expected)
    {
        Assert.AreEqual(expected, this.scenarioContext.Get<int>(DecodedBase64ByteCount));
    }

    [Then("the decoded byte count should be greater than or equal to (.*)")]
    public void ThenTheDecodedByteCountShouldBeGreaterThanOrEqualTo(int expected)
    {
        Assert.GreaterOrEqual(this.scenarioContext.Get<int>(DecodedBase64ByteCount), expected);
    }

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBase64String (.*)")]
    public void GivenTheJsonElementBackedJsonBase64String(string value)
    {
        this.scenarioContext.Set(JsonBase64String.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonBase64String.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    [Then("the JsonBase64String has base64 bytes")]
    public void ThenTheJsonBaseStringHasBase64Bytes()
    {
        Assert.IsTrue(this.scenarioContext.Get<JsonBase64String>(SubjectUnderTest).HasBase64Bytes());
    }

    [Then("the JsonBase64String does not have base64 bytes")]
    public void ThenTheJsonBaseStringDoesNotHaveBase64Bytes()
    {
        Assert.IsFalse(this.scenarioContext.Get<JsonBase64String>(SubjectUnderTest).HasBase64Bytes());
    }

    /* base64StringPre201909 */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonBase64StringPre201909 (.*)")]
    public void GivenTheJsonElementBackedJsonBase64StringPre201909(string value)
    {
        this.scenarioContext.Set(JsonBase64StringPre201909.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonBase64StringPre201909 (.*)")]
    public void GivenTheDotnetBackedJsonBase64StringPre201909(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonBase64StringPre201909.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonBase64StringPre201909.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonContent.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonContent.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* content-pre201909 */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonContentPre201909 (.*)")]
    public void GivenTheJsonElementBackedJsonContentPre201909(string value)
    {
        this.scenarioContext.Set(JsonContentPre201909.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonContentPre201909 (.*)")]
    public void GivenTheDotnetBackedJsonContentPre201909(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonContentPre201909.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonContentPre201909.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonDate.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonDate.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonDateTime.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonDateTime.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonDuration.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonDuration.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonEmail.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonEmail.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIdnEmail.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIdnEmail.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonHostname.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonHostname.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIdnHostname.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIdnHostname.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonInteger.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonInteger.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUInt128 (.*)")]
    public void GivenTheJsonElementBackedJsonUInt128(string value)
    {
        this.scenarioContext.Set(JsonUInt128.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUInt128 (.*)")]
    public void GivenTheDotnetBackedJsonUInt128(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUInt128.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonUInt128.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonInt128 (.*)")]
    public void GivenTheJsonElementBackedJsonInt128(string value)
    {
        this.scenarioContext.Set(JsonInt128.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonInt128 (.*)")]
    public void GivenTheDotnetBackedJsonInt128(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonInt128.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonInt128.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonInt64 (.*)")]
    public void GivenTheJsonElementBackedJsonInt64(string value)
    {
        this.scenarioContext.Set(JsonInt64.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonInt64 (.*)")]
    public void GivenTheDotnetBackedJsonInt64(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonInt64.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonInt64.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonInt32 (.*)")]
    public void GivenTheJsonElementBackedJsonInt32(string value)
    {
        this.scenarioContext.Set(JsonInt32.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonInt32 (.*)")]
    public void GivenTheDotnetBackedJsonInt32(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonInt32.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonInt32.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonInt16 (.*)")]
    public void GivenTheJsonElementBackedJsonInt16(string value)
    {
        this.scenarioContext.Set(JsonInt16.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonInt16 (.*)")]
    public void GivenTheDotnetBackedJsonInt16(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonInt16.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonInt16.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonSByte (.*)")]
    public void GivenTheJsonElementBackedJsonSByte(string value)
    {
        this.scenarioContext.Set(JsonSByte.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonSByte (.*)")]
    public void GivenTheDotnetBackedJsonSByte(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonSByte.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonSByte.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUInt64 (.*)")]
    public void GivenTheJsonElementBackedJsonUInt64(string value)
    {
        this.scenarioContext.Set(JsonUInt64.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUInt64 (.*)")]
    public void GivenTheDotnetBackedJsonUInt64(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUInt64.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonUInt64.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUInt32 (.*)")]
    public void GivenTheJsonElementBackedJsonUInt32(string value)
    {
        this.scenarioContext.Set(JsonUInt32.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUInt32 (.*)")]
    public void GivenTheDotnetBackedJsonUInt32(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUInt32.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonUInt32.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonUInt16 (.*)")]
    public void GivenTheJsonElementBackedJsonUInt16(string value)
    {
        this.scenarioContext.Set(JsonUInt16.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonUInt16 (.*)")]
    public void GivenTheDotnetBackedJsonUInt16(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonUInt16.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonUInt16.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* integer */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonByte (.*)")]
    public void GivenTheJsonElementBackedJsonByte(string value)
    {
        this.scenarioContext.Set(JsonByte.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonByte (.*)")]
    public void GivenTheDotnetBackedJsonByte(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonByte.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonByte.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonNumber.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonNumber.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* number */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonDecimal (.*)")]
    public void GivenTheJsonElementBackedJsonDecimal(string value)
    {
        this.scenarioContext.Set(JsonDecimal.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonDecimal (.*)")]
    public void GivenTheDotnetBackedJsonDecimal(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonDecimal.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonDecimal.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* number */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonDouble (.*)")]
    public void GivenTheJsonElementBackedJsonDouble(string value)
    {
        this.scenarioContext.Set(JsonDouble.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonDouble (.*)")]
    public void GivenTheDotnetBackedJsonDouble(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonDouble.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonDouble.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /* number */

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonSingle (.*)")]
    public void GivenTheJsonElementBackedJsonSingle(string value)
    {
        this.scenarioContext.Set(JsonSingle.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonSingle (.*)")]
    public void GivenTheDotnetBackedJsonSingle(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonSingle.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonSingle.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
        }
    }

    /// <summary>
    /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The json value.</param>
    [Given("the JsonElement backed JsonHalf (.*)")]
    public void GivenTheJsonElementBackedJsonHalf(string value)
    {
        this.scenarioContext.Set(JsonHalf.Parse(value), SubjectUnderTest);
    }

    /// <summary>
    /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
    /// </summary>
    /// <param name="value">The value.</param>
    [Given("the dotnet backed JsonHalf (.*)")]
    public void GivenTheDotnetBackedJsonHalf(string value)
    {
        if (value == "null")
        {
            this.scenarioContext.Set(JsonHalf.Null, SubjectUnderTest);
        }
        else
        {
            this.scenarioContext.Set(JsonHalf.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIpV4.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIpV4.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIpV6.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIpV6.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonUri.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonUri.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonUriReference.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonUriReference.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIri.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIri.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonIriReference.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonIriReference.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonObject.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonObject.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonPointer.Parse(value), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonRelativePointer.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonRelativePointer.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonRegex.Parse(value), SubjectUnderTest);
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
            this.scenarioContext.Set(JsonRegex.Parse(value).AsDotnetBackedValue(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonUriTemplate.Parse(value), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonTime.Parse(value), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonUuid.Parse(value), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonAny.Parse(value).AsObject.AsPropertyBacking(), SubjectUnderTest);
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
        this.scenarioContext.Set(JsonString.Parse(value), SubjectUnderTest);
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
    /// Stores the <see cref="decimal"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
    /// </summary>
    /// <param name="value">The float value.</param>
    [Given("the decimal for (.*)")]
    public void GivenTheDecimalFor(decimal value)
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