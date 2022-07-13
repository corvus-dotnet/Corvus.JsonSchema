// <copyright file="JsonValueSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Steps
{
    using System;
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
        [Then(@"the round-tripped result should be an Object")]
        public void ThenTheRound_TrippedResultShouldBeAnObject()
        {
            Assert.AreEqual(JsonValueKind.Object, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
        }

        /// <summary>
        /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.Array"/>.
        /// </summary>
        [Then(@"the round-tripped result should be an Array")]
        public void ThenTheRound_TrippedResultShouldBeAnArray()
        {
            Assert.AreEqual(JsonValueKind.Array, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
        }

        /// <summary>
        /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.Number"/>.
        /// </summary>
        [Then(@"the round-tripped result should be a Number")]
        public void ThenTheRound_TrippedResultShouldBeANumber()
        {
            Assert.AreEqual(JsonValueKind.Number, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
        }

        /// <summary>
        /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.String"/>.
        /// </summary>
        [Then(@"the round-tripped result should be a String")]
        public void ThenTheRound_TrippedResultShouldBeAString()
        {
            Assert.AreEqual(JsonValueKind.String, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
        }

        /// <summary>
        /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.
        /// </summary>
        [Then(@"the round-tripped result should be a Boolean")]
        public void ThenTheRound_TrippedResultShouldBeABoolean()
        {
            JsonValueKind actual = this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind;
            Assert.IsTrue(actual == JsonValueKind.True || actual == JsonValueKind.False);
        }

        /// <summary>
        /// Validates that the object in the context variable <see cref="SerializationResult"/> is a <see cref="JsonValueKind.True"/> or <see cref="JsonValueKind.False"/>.
        /// </summary>
        [Then(@"the round-tripped result should be Null")]
        public void ThenTheRound_TrippedResultShouldBeNull()
        {
            Assert.AreEqual(JsonValueKind.Null, this.scenarioContext.Get<JsonAny>(SerializationResult).ValueKind);
        }

        /* serialization */

        /// <summary>
        /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, deserializaes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
        /// </summary>
        [When(@"the json value is round-tripped via a string")]
        public void WhenTheJsonValueIsRound_TrippedViaAString()
        {
            IJsonValue sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest);
            ArrayBufferWriter<byte> abw = new();
            using Utf8JsonWriter writer = new(abw);
            sut.WriteTo(writer);
            writer.Flush();

            this.scenarioContext.Set(JsonAny.ParseUriValue(abw.WrittenMemory), SerializationResult);
        }

        /// <summary>
        /// Serializes an <see cref="IJsonValue"/> from the context variable <see cref="SubjectUnderTest"/>, using <see cref="JsonValueExtensions.Serialize{TValue}(TValue)"/>, and deserializes and stores the resulting <see cref="JsonAny"/> in the context variable <see cref="SerializationResult"/>.
        /// </summary>
        [When(@"the json value is round-trip serialized via a string")]
        public void WhenTheJsonValueIsRound_TripSerializedViaAString()
        {
            JsonAny sut = this.scenarioContext.Get<IJsonValue>(SubjectUnderTest).AsAny;
            this.scenarioContext.Set(JsonAny.ParseUriValue(sut.Serialize()), SerializationResult);
        }

        /// <summary>
        /// Compares the string from the context variable <see cref="SerializationResult"/> with the expected value.
        /// </summary>
        /// <param name="expected">The expected value.</param>
        [Then(@"the round-tripped result should be equal to the JsonAny (.*)")]
        public void ThenTheRound_TrippedResultShouldBeEqualToTheJsonAny(string expected)
        {
            Assert.AreEqual(JsonAny.ParseUriValue(expected), this.scenarioContext.Get<JsonAny>(SerializationResult));
        }

        /* notAny */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonNotAny (.*)")]
        public void GivenTheJsonElementBackedJsonNotAny(string value)
        {
            if (value == "<undefined>")
            {
                this.scenarioContext.Set<JsonNotAny>(default, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set((JsonNotAny)JsonAny.ParseUriValue(value), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the object backed JsonNotAny (.*)")]
        public void GivenTheObjectBackedJsonNotAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonNotAny)JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNotAny(JsonAny.ParseUriValue(value).AsObject.AsPropertyDictionary), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the dotnet backed JsonNotAny (.*)")]
        public void GivenTheDotnetBackedJsonNotAny(string value)
        {
            if (value == "<undefined>")
            {
                this.scenarioContext.Set<JsonNotAny>(default, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set<JsonNotAny>(JsonAny.ParseUriValue(value).AsDotnetBackedValue, SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the number backed JsonNotAny (.*)")]
        public void GivenTheNumberBackedJsonNotAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonNotAny)JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNotAny((double)JsonAny.ParseUriValue(value).AsNumber), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the array backed JsonNotAny (.*)")]
        public void GivenTheArrayBackedJsonNotAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonNotAny)JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNotAny(JsonAny.ParseUriValue(value).AsArray.AsItemsList), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the string backed JsonNotAny (.*)")]
        public void GivenTheStringBackedJsonNotAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonNotAny)JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNotAny((string)JsonAny.ParseUriValue(value).AsString), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the boolean backed JsonNotAny (.*)")]
        public void GivenTheBooleanBackedJsonNotAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonNotAny)JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNotAny((bool)JsonAny.ParseUriValue(value).AsBoolean), SubjectUnderTest);
            }
        }

        /* any */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonAny (.*)")]
        public void GivenTheJsonElementBackedJsonAny(string value)
        {
            if (value == "<undefined>")
            {
                this.scenarioContext.Set<JsonAny>(default, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(JsonAny.ParseUriValue(value), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the dotnet backed JsonAny (.*)")]
        public void GivenTheDotnetBackedJsonAny(string value)
        {
            if (value == "<undefined>")
            {
                this.scenarioContext.Set<JsonAny>(default, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(JsonAny.ParseUriValue(value).AsDotnetBackedValue, SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the object backed JsonAny (.*)")]
        public void GivenTheObjectBackedJsonAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonAny(JsonAny.ParseUriValue(value).AsObject.AsPropertyDictionary), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the number backed JsonAny (.*)")]
        public void GivenTheNumberBackedJsonAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonAny((double)JsonAny.ParseUriValue(value).AsNumber), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the array backed JsonAny (.*)")]
        public void GivenTheArrayBackedJsonAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonAny(JsonAny.ParseUriValue(value).AsArray.AsItemsList), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the string backed JsonAny (.*)")]
        public void GivenTheStringBackedJsonAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonAny((string)JsonAny.ParseUriValue(value).AsString), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the boolean backed JsonAny (.*)")]
        public void GivenTheBooleanBackedJsonAny(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonAny((bool)JsonAny.ParseUriValue(value).AsBoolean), SubjectUnderTest);
            }
        }

        /* string */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonString (.*)")]
        public void GivenTheJsonElementBackedJsonString(string value)
        {
            this.scenarioContext.Set<JsonString>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonString (.*)")]
        public void GivenTheDotnetBackedJsonString(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonString((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* boolean */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonBoolean (.*)")]
        public void GivenTheJsonElementBackedJsonBoolean(string value)
        {
            this.scenarioContext.Set<JsonBoolean>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonBoolean (.*)")]
        public void GivenTheDotnetBackedJsonBoolean(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny.AsBoolean, SubjectUnderTest);
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
        [Given(@"the JsonElement backed JsonArray (.*)")]
        public void GivenTheJsonElementBackedJsonArray(string value)
        {
            this.scenarioContext.Set<JsonArray>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonArray (.*)")]
        public void GivenTheDotnetBackedJsonArray(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny.AsArray, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonArray(new JsonArray(((JsonArray)JsonAny.ParseUriValue(value)).AsItemsList)), SubjectUnderTest);
            }
        }

        /* base64content */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonBase64Content (.*)")]
        public void GivenTheJsonElementBackedJsonBase64Content(string value)
        {
            this.scenarioContext.Set<JsonBase64Content>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonBase64Content (.*)")]
        public void GivenTheDotnetBackedJsonBase64Content(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonBase64Content)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonBase64Content(JsonAny.ParseUriValue(value).AsString.GetString()), SubjectUnderTest);
            }
        }

        /* base64string */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonBase64String (.*)")]
        public void GivenTheJsonElementBackedJsonBase64String(string value)
        {
            this.scenarioContext.Set<JsonBase64String>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonBase64String (.*)")]
        public void GivenTheDotnetBackedJsonBase64String(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonBase64String)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonBase64String(JsonAny.ParseUriValue(value).AsString.GetString()), SubjectUnderTest);
            }
        }

        /* content */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonContent (.*)")]
        public void GivenTheJsonElementBackedJsonContent(string value)
        {
            this.scenarioContext.Set<JsonContent>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonContent (.*)")]
        public void GivenTheDotnetBackedJsonContent(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonContent)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonContent((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* date */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonDate (.*)")]
        public void GivenTheJsonElementBackedJsonDate(string value)
        {
            this.scenarioContext.Set<JsonDate>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonDate (.*)")]
        public void GivenTheDotnetBackedJsonDate(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonDate)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonDate((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* dateTime */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonDateTime (.*)")]
        public void GivenTheJsonElementBackedJsonDateTime(string value)
        {
            this.scenarioContext.Set<JsonDateTime>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonDateTime (.*)")]
        public void GivenTheDotnetBackedJsonDateTime(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonDateTime)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonDateTime((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* duration */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonDuration (.*)")]
        public void GivenTheJsonElementBackedJsonDuration(string value)
        {
            this.scenarioContext.Set<JsonDuration>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonDuration (.*)")]
        public void GivenTheDotnetBackedJsonDuration(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonDuration)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonDuration((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* email */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonEmail (.*)")]
        public void GivenTheJsonElementBackedJsonEmail(string value)
        {
            this.scenarioContext.Set<JsonEmail>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonEmail (.*)")]
        public void GivenTheDotnetBackedJsonEmail(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonEmail)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonEmail((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* idnEmail */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIdnEmail (.*)")]
        public void GivenTheJsonElementBackedJsonIdnEmail(string value)
        {
            this.scenarioContext.Set<JsonIdnEmail>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIdnEmail (.*)")]
        public void GivenTheDotnetBackedJsonIdnEmail(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIdnEmail)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIdnEmail((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* hostname */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonHostname (.*)")]
        public void GivenTheJsonElementBackedJsonHostname(string value)
        {
            this.scenarioContext.Set<JsonHostname>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonHostname (.*)")]
        public void GivenTheDotnetBackedJsonHostname(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonHostname)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonHostname((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* idnHostname */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIdnHostname (.*)")]
        public void GivenTheJsonElementBackedJsonIdnHostname(string value)
        {
            this.scenarioContext.Set<JsonIdnHostname>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIdnHostname (.*)")]
        public void GivenTheDotnetBackedJsonIdnHostname(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIdnHostname)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIdnHostname((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* integer */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonInteger (.*)")]
        public void GivenTheJsonElementBackedJsonInteger(string value)
        {
            this.scenarioContext.Set<JsonInteger>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonInteger (.*)")]
        public void GivenTheDotnetBackedJsonInteger(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonInteger)JsonNull.Instance.AsAny.AsNumber, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonInteger((long)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* number */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonNumber (.*)")]
        public void GivenTheJsonElementBackedJsonNumber(string value)
        {
            this.scenarioContext.Set<JsonNumber>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonNumber (.*)")]
        public void GivenTheDotnetBackedJsonNumber(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny.AsNumber, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonNumber((double)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* ipV4 */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIpV4 (.*)")]
        public void GivenTheJsonElementBackedJsonIpV4(string value)
        {
            this.scenarioContext.Set<JsonIpV4>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIpV4 (.*)")]
        public void GivenTheDotnetBackedJsonIpV4(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIpV4)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIpV4((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* ipV6 */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIpV6 (.*)")]
        public void GivenTheJsonElementBackedJsonIpV6(string value)
        {
            this.scenarioContext.Set<JsonIpV6>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIpV6 (.*)")]
        public void GivenTheDotnetBackedJsonIpV6(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIpV6)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIpV6((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* uri */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonUri (.*)")]
        public void GivenTheJsonElementBackedJsonUri(string value)
        {
            this.scenarioContext.Set<JsonUri>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonUri (.*)")]
        public void GivenTheDotnetBackedJsonUri(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonUri)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonUri((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* uriReference */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonUriReference (.*)")]
        public void GivenTheJsonElementBackedJsonUriReference(string value)
        {
            this.scenarioContext.Set<JsonUriReference>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonUriReference (.*)")]
        public void GivenTheDotnetBackedJsonUriReference(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonUriReference)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonUriReference((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* iri */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIri (.*)")]
        public void GivenTheJsonElementBackedJsonIri(string value)
        {
            this.scenarioContext.Set<JsonIri>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIri (.*)")]
        public void GivenTheDotnetBackedJsonIri(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIri)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIri((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* iriReference */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonIriReference (.*)")]
        public void GivenTheJsonElementBackedJsonIriReference(string value)
        {
            this.scenarioContext.Set<JsonIriReference>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonIriReference (.*)")]
        public void GivenTheDotnetBackedJsonIriReference(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonIriReference)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonIriReference((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* object */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonObject (.*)")]
        public void GivenTheJsonElementBackedJsonObject(string value)
        {
            if (value == "<undefined>")
            {
                this.scenarioContext.Set<JsonObject>(default, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set<JsonObject>(JsonAny.ParseUriValue(value), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonObject (.*)")]
        public void GivenTheDotnetBackedJsonObject(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set(JsonNull.Instance.AsAny.AsObject, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonObject(((JsonObject)JsonAny.ParseUriValue(value)).AsPropertyDictionary), SubjectUnderTest);
            }
        }

        /* pointer */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonPointer (.*)")]
        public void GivenTheJsonElementBackedJsonPointer(string value)
        {
            this.scenarioContext.Set<JsonPointer>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonPointer (.*)")]
        public void GivenTheDotnetBackedJsonPointer(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonPointer)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonPointer((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* relativePointer */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonRelativePointer (.*)")]
        public void GivenTheJsonElementBackedJsonRelativePointer(string value)
        {
            this.scenarioContext.Set<JsonRelativePointer>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonRelativePointer (.*)")]
        public void GivenTheDotnetBackedJsonRelativePointer(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonRelativePointer)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonRelativePointer((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* regex */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonRegex (.*)")]
        public void GivenTheJsonElementBackedJsonRegex(string value)
        {
            this.scenarioContext.Set<JsonRegex>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonRegex (.*)")]
        public void GivenTheDotnetBackedJsonRegex(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonRegex)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonRegex((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* uriTemplate */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonUriTemplate (.*)")]
        public void GivenTheJsonElementBackedJsonUriTemplate(string value)
        {
            this.scenarioContext.Set<JsonUriTemplate>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonUriTemplate (.*)")]
        public void GivenTheDotnetBackedJsonUriTemplate(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonUriTemplate)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonUriTemplate((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* time */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonTime (.*)")]
        public void GivenTheJsonElementBackedJsonTime(string value)
        {
            this.scenarioContext.Set<JsonTime>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonTime (.*)")]
        public void GivenTheDotnetBackedJsonTime(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonTime)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonTime((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /* uuid */

        /// <summary>
        /// Store a <see cref="JsonElement"/>-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The json value.</param>
        [Given(@"the JsonElement backed JsonUuid (.*)")]
        public void GivenTheJsonElementBackedJsonUuid(string value)
        {
            this.scenarioContext.Set<JsonUuid>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Store a dotnet-type-backed value in the context variable <c>Value</c>.
        /// </summary>
        /// <param name="value">The value.</param>
        [Given(@"the dotnet backed JsonUuid (.*)")]
        public void GivenTheDotnetBackedJsonUuid(string value)
        {
            if (value == "null")
            {
                this.scenarioContext.Set((JsonUuid)JsonNull.Instance.AsAny.AsString, SubjectUnderTest);
            }
            else
            {
                this.scenarioContext.Set(new JsonUuid((string)JsonAny.ParseUriValue(value)), SubjectUnderTest);
            }
        }

        /// <summary>
        /// Gets a JsonAny for the given <paramref name="value"/> and stores it in the context variable <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The serialized value to parse.</param>
        [Given(@"the JsonAny for (.*)")]
        public void GivenTheJsonAnyFor(string value)
        {
            this.scenarioContext.Set(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Gets a dictionary for the given <paramref name="value"/> and stores it in the context variable <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The serialized value to parse.</param>
        [Given(@"the ImmutableDictionary<string,JsonAny> for (.*)")]
        public void GivenTheImmutableDictionaryOfStringToJsonAnyFor(string value)
        {
            this.scenarioContext.Set(JsonAny.ParseUriValue(value).AsObject.AsPropertyDictionary, SubjectUnderTest);
        }

        /// <summary>
        /// Gets the <see cref="ImmutableList{JsonAny}"/> for the <paramref name="list"/> and stores it in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="list">The serialized from of the immutable list.</param>
        [Given(@"the ImmutableList<JsonAny> for (.*)")]
        public void GivenTheImmutableListOfJsonAnyFor(string list)
        {
            this.scenarioContext.Set(JsonAny.ParseUriValue(list).AsArray.AsItemsList, SubjectUnderTest);
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
        [Given(@"the Regex for (.*)")]
        public void GivenTheRegexFor(string value)
        {
            this.scenarioContext.Set(new Regex(value), SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="JsonString"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value.</param>
        [Given(@"the JsonString for (.*)")]
        public void GivenTheJsonStringFor(string value)
        {
            this.scenarioContext.Set<JsonString>(JsonAny.ParseUriValue(value), SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="IPAddress"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value.</param>
        [Given(@"the IPAddress for (.*)")]
        public void GivenTheIpAddressFor(string value)
        {
            this.scenarioContext.Set(IPAddress.Parse(value), SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="Guid"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value.</param>
        [Given(@"the Guid for (.*)")]
        public void GivenTheGuidFor(string value)
        {
            this.scenarioContext.Set(Guid.Parse(value), SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="Uri"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value.</param>
        [Given(@"the Uri for (.*)")]
        public void GivenTheUriFor(string value)
        {
            this.scenarioContext.Set(new Uri(value, UriKind.RelativeOrAbsolute), SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="bool"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The boolean value.</param>
        [Given(@"the bool for (.*)")]
        public void GivenTheBoolForTrue(bool value)
        {
            this.scenarioContext.Set(value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="long"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The int64 value.</param>
        [Given(@"the long for (.*)")]
        public void GivenTheLongFor(long value)
        {
            this.scenarioContext.Set(value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="int"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The int32 value.</param>
        [Given(@"the int for (.*)")]
        public void GivenTheIntFor(int value)
        {
            this.scenarioContext.Set(value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="double"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The float value.</param>
        [Given(@"the double for (.*)")]
        public void GivenTheDoubleFor(double value)
        {
            this.scenarioContext.Set(value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="float"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The float value.</param>
        [Given(@"the float for (.*)")]
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
            this.scenarioContext.Set(value.AsMemory(), SubjectUnderTest);
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
            this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="Period"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value of the <see cref="LocalDate"/>.</param>
        [Given(@"the Period for ""(.*)""")]
        public void GivenThePeriodFor(string value)
        {
            ParseResult<Period> parseResult = PeriodPattern.NormalizingIso.Parse(value);
            this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
        }

        /// <summary>
        /// Stores the <see cref="OffsetDateTime"/> <paramref name="value"/> in the context key <see cref="SubjectUnderTest"/>.
        /// </summary>
        /// <param name="value">The string value of the <see cref="OffsetDateTime"/>.</param>
        [Given(@"the OffsetDateTime for ""(.*)""")]
        public void GivenTheOffsetDateTimeFor(string value)
        {
            ParseResult<OffsetDateTime> parseResult = OffsetDateTimePattern.ExtendedIso.Parse(value);
            this.scenarioContext.Set(parseResult.Value, SubjectUnderTest);
        }
    }
}
