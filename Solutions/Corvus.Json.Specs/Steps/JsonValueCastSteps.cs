// <copyright file="JsonValueCastSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Steps
{
    using System;
    using System.Collections.Immutable;
    using System.Net;
    using System.Text;
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
        [When(@"I cast the JsonArray to JsonAny")]
        public void WhenICastTheJsonArrayToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonAny serialized in <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The serialized form of the result to match.</param>
        [Then(@"the result should equal the JsonAny '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonAny(string match)
        {
            Assert.AreEqual(JsonAny.ParseUriValue(match), this.scenarioContext.Get<JsonAny>(CastResultKey));
        }

        /// <summary>
        /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonNotAny serialized in <paramref name="match"/>.
        /// </summary>
        /// <param name="match">The serialized form of the result to match.</param>
        [Then(@"the result should equal the JsonNotAny '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonNotAny(string match)
        {
            Assert.AreEqual((JsonNotAny)JsonAny.ParseUriValue(match), this.scenarioContext.Get<JsonNotAny>(CastResultKey));
        }

        /// <summary>
        /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonAny serialized in <paramref name="match"/>, as a double, within the given margin.
        /// </summary>
        /// <param name="match">The serialized form of the result to match.</param>
        /// <param name="margin">The precision with which to match the JsonAny value.</param>
        [Then(@"the result should equal the JsonAny '(.*)' within (.*)")]
        public void ThenTheResultShouldEqualTheJsonAnyWithin(string match, double margin)
        {
            Assert.AreEqual((double)JsonAny.ParseUriValue(match), (double)this.scenarioContext.Get<JsonAny>(CastResultKey), margin);
        }

        /// <summary>
        /// Gets the value store in <see cref="CastResultKey"/> and matches it against the JsonNotAny serialized in <paramref name="match"/>, as a double, within the given margin.
        /// </summary>
        /// <param name="match">The serialized form of the result to match.</param>
        /// <param name="margin">The precision with which to match the JsonAny value.</param>
        [Then(@"the result should equal the JsonNotAny '(.*)' within (.*)")]
        public void ThenTheResultShouldEqualTheJsonNotAnyWithin(string match, double margin)
        {
            Assert.AreEqual((double)(JsonNotAny)JsonAny.ParseUriValue(match), (double)this.scenarioContext.Get<JsonNotAny>(CastResultKey), margin);
        }

        /// <summary>
        /// Cast the value stored in the context variable <see cref="JsonValueSteps.SubjectUnderTest"/> to a JsonArray and store it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonArray")]
        public void WhenICastTheJsonAnyToJsonArray()
        {
            this.scenarioContext.Set<IJsonValue>((JsonArray)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Comparse the IJsonValue in the <see cref="CastResultKey"/> with the serialized <paramref name="jsonArray"/>.
        /// </summary>
        /// <param name="jsonArray">The serialized JsonArray with which to compare the result.</param>
        [Then(@"the result should equal the JsonArray '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonArray(string jsonArray)
        {
            Assert.AreEqual(JsonAny.ParseUriValue(jsonArray).AsArray, this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Compares the two lists.
        /// </summary>
        /// <param name="immutableList">The immutable list with which to compare the result.</param>
        [Then(@"the result should equal the ImmutableList<JsonAny> '(.*)'")]
        public void ThenTheResultShouldEqualTheImmutableList(string immutableList)
        {
            ImmutableList<JsonAny> expected = JsonAny.ParseUriValue(immutableList).AsArray.AsItemsList;
            ImmutableList<JsonAny> actual = this.scenarioContext.Get<ImmutableList<JsonAny>>(CastResultKey);
            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonArray to ImmutableList<JsonAny>")]
        public void WhenICastTheJsonArrayToImmutableList()
        {
            this.scenarioContext.Set((ImmutableList<JsonAny>)this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonArray"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableList<JsonAny> to JsonArray")]
        public void WhenICastTheImmutableListToJsonArray()
        {
            this.scenarioContext.Set((JsonArray)this.scenarioContext.Get<ImmutableList<JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* any */

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to long")]
        public void WhenICastTheJsonAnyToLong()
        {
            this.scenarioContext.Set((long)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the long to JsonAny")]
        public void WhenICastTheLongToJsonAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to int")]
        public void WhenICastTheJsonAnyToInt()
        {
            this.scenarioContext.Set((int)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the int to JsonAny")]
        public void WhenICastTheIntToJsonAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to double")]
        public void WhenICastTheJsonAnyToDouble()
        {
            this.scenarioContext.Set((double)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the double to JsonAny")]
        public void WhenICastTheDoubleToJsonAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to float")]
        public void WhenICastTheJsonAnyToFloat()
        {
            this.scenarioContext.Set((float)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the float to JsonAny")]
        public void WhenICastTheFloatToJsonAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to ImmutableList<JsonAny>")]
        public void WhenICastTheJsonAnyToImmutableList()
        {
            this.scenarioContext.Set((ImmutableList<JsonAny>)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonArray"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableList<JsonAny> to JsonAny")]
        public void WhenICastTheImmutableListToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<ImmutableList<JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to ImmutableDictionary<string,JsonAny>")]
        public void WhenICastTheJsonAnyToImmutableDictionary()
        {
            this.scenarioContext.Set((ImmutableDictionary<string, JsonAny>)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonAny"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableDictionary<string,JsonAny> to JsonAny")]
        public void WhenICastTheImmutableDictionaryToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* notAny */

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="bool"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to bool")]
        public void WhenICastTheJsonNotAnyToBool()
        {
            this.scenarioContext.Set((bool)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="bool"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the bool to JsonNotAny")]
        public void WhenICastTheBoolToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<bool>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBoolean"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonBoolean")]
        public void WhenICastTheJsonNotAnyToJsonBoolean()
        {
            this.scenarioContext.Set((JsonBoolean)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBoolean"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBoolean to JsonNotAny")]
        public void WhenICastTheJsonBooleanToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to JsonNotAny")]
        public void WhenICastTheJsonNumberToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonNumber")]
        public void WhenICastTheJsonNotAnyToJsonNumber()
        {
            this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonArray"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonArray to JsonNotAny")]
        public void WhenICastTheJsonArrayToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonArray"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonArray")]
        public void WhenICastTheJsonNotAnyToJsonArray()
        {
            this.scenarioContext.Set((JsonArray)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonObject"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonObject to JsonNotAny")]
        public void WhenICastTheJsonObjectToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonObject"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonObject")]
        public void WhenICastTheJsonNotAnyToJsonObject()
        {
            this.scenarioContext.Set((JsonObject)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonNotAny")]
        public void WhenICastTheJsonAnyToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonAny")]
        public void WhenICastTheJsonNotAnyToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to long")]
        public void WhenICastTheJsonNotAnyToLong()
        {
            this.scenarioContext.Set((long)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the long to JsonNotAny")]
        public void WhenICastTheLongToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to int")]
        public void WhenICastTheJsonNotAnyToInt()
        {
            this.scenarioContext.Set((int)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the int to JsonNotAny")]
        public void WhenICastTheIntToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to double")]
        public void WhenICastTheJsonNotAnyToDouble()
        {
            this.scenarioContext.Set((double)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the double to JsonNotAny")]
        public void WhenICastTheDoubleToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to float")]
        public void WhenICastTheJsonNotAnyToFloat()
        {
            this.scenarioContext.Set((float)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the float to JsonNotAny")]
        public void WhenICastTheFloatToJsonNotAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNotAny)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonNotAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to ImmutableList<JsonAny>")]
        public void WhenICastTheJsonNotAnyToImmutableList()
        {
            this.scenarioContext.Set((ImmutableList<JsonAny>)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonArray"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableList<JsonAny> to JsonNotAny")]
        public void WhenICastTheImmutableListToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<ImmutableList<JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonNotAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to ImmutableDictionary<string,JsonAny>")]
        public void WhenICastTheJsonNotAnyToImmutableDictionary()
        {
            this.scenarioContext.Set((ImmutableDictionary<string, JsonAny>)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonNotAny"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableDictionary<string,JsonAny> to JsonNotAny")]
        public void WhenICastTheImmutableDictionaryToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to JsonString")]
        public void WhenICastTheJsonNotAnyToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonNotAny")]
        public void WhenICastTheJsonStringToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to ReadOnlySpan<char>")]
        public void WhenICastTheJsonNotAnyToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonNotAny")]
        public void WhenICastTheReadOnlySpanOfCharToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonNotAnyToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonNotAny")]
        public void WhenICastTheReadOnlySpanOfByteToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNotAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNotAny to string")]
        public void WhenICastTheJsonNotAnyToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNotAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonNotAny")]
        public void WhenICastTheStringToJsonNotAny()
        {
            this.scenarioContext.Set((JsonNotAny)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* object */

        /// <summary>
        /// Cast the value stored in the context variable <see cref="JsonValueSteps.SubjectUnderTest"/> to a JsonAny and store in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonObject to JsonAny")]
        public void WhenICastTheJsonObjectToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Cast the value stored in the context variable <see cref="JsonValueSteps.SubjectUnderTest"/> to a JsonObject and store it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonObject")]
        public void WhenICastTheJsonAnyToJsonObject()
        {
            this.scenarioContext.Set<IJsonValue>((JsonObject)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Comparse the IJsonValue in the <see cref="CastResultKey"/> with the serialized <paramref name="jsonObject"/>.
        /// </summary>
        /// <param name="jsonObject">The serialized JsonObject with which to compare the result.</param>
        [Then(@"the result should equal the JsonObject '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonObject(string jsonObject)
        {
            Assert.AreEqual(JsonAny.ParseUriValue(jsonObject).AsObject, this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Compares the two dictionaries.
        /// </summary>
        /// <param name="immutableDictionary">The immutable dictionary with which to compare the result.</param>
        [Then(@"the result should equal the ImmutableDictionary<string,JsonAny> '(.*)'")]
        public void ThenTheResultShouldEqualTheImmutablDictionary(string immutableDictionary)
        {
            ImmutableDictionary<string, JsonAny> expected = JsonAny.ParseUriValue(immutableDictionary).AsObject.AsPropertyDictionary;
            ImmutableDictionary<string, JsonAny> actual = this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(CastResultKey);
            CollectionAssert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="ImmutableList{JsonAny}"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonObject to ImmutableDictionary<string,JsonAny>")]
        public void WhenICastTheJsonObjectToImmutableDictionary()
        {
            this.scenarioContext.Set((ImmutableDictionary<string, JsonAny>)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Gets the item in the <see cref="JsonValueSteps.SubjectUnderTest"/>, casts it to <see cref="JsonObject"/> and stores it in the <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ImmutableDictionary<string,JsonAny> to JsonObject")]
        public void WhenICastTheImmutableDictionaryToJsonObject()
        {
            this.scenarioContext.Set((JsonObject)this.scenarioContext.Get<ImmutableDictionary<string, JsonAny>>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* string */

        /// <summary>
        /// Compares the two <see cref="JsonString"/> instances.
        /// </summary>
        /// <param name="expectedString">The string with which to compare the result.</param>
        [Then(@"the result should equal the JsonString '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonString(string expectedString)
        {
            JsonString expected = JsonAny.ParseUriValue(expectedString);
            JsonString actual = this.scenarioContext.Get<JsonString>(CastResultKey);
            Assert.AreEqual(expected, actual);
        }

        /// <summary>
        /// Compares a <see cref="ReadOnlyMemory{Char}"/> built from the <see cref="ReadOnlySpan{Char}"/> and stored in <see cref="CastResultKey"/> against the <see cref="ReadOnlyMemory{Char}"/> built from the expected string.
        /// </summary>
        /// <param name="expectedString">The string with which to compare the result.</param>
        [Then(@"the result should equal the ReadOnlySpan<char> '(.*)'")]
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
        [Then(@"the result should equal the ReadOnlySpan<byte> '(.*)'")]
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
        [Then(@"the result should equal the string '(.*)'")]
        public void ThenTheResultShouldEqualTheString(string expected)
        {
            string actual = this.scenarioContext.Get<string>(CastResultKey);
            Assert.AreEqual(expected, actual);
        }

        /* base64content */

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64Content to JsonAny")]
        public void WhenICastTheJsonBase64ContentToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonBase64Content")]
        public void WhenICastTheJsonAnyToJsonBase64Content()
        {
            this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonBase64Content"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64Content.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64Content"/>.</param>
        [Then(@"the result should equal the JsonBase64Content '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonBase64Content(string expectedValue)
        {
            JsonBase64Content expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64Content>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64Content to JsonString")]
        public void WhenICastTheJsonBase64ContentToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonBase64Content")]
        public void WhenICastTheJsonStringToJsonBase64Content()
        {
            this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64Content to ReadOnlySpan<char>")]
        public void WhenICastTheJsonBase64ContentToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonBase64Content")]
        public void WhenICastTheReadOnlySpanOfCharToJsonBase64Content()
        {
            this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64Content to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonBase64ContentToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonBase64Content")]
        public void WhenICastTheReadOnlySpanOfByteToJsonBase64Content()
        {
            this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64Content"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64Content to string")]
        public void WhenICastTheJsonBase64ContentToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonBase64Content")]
        public void WhenICastTheStringToJsonBase64Content()
        {
            this.scenarioContext.Set((JsonBase64Content)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* base64String */

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64String to JsonAny")]
        public void WhenICastTheJsonBase64StringToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonBase64String")]
        public void WhenICastTheJsonAnyToJsonBase64String()
        {
            this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonBase64String"/> in the context value <see cref="CastResultKey"/> with the given JsonBase64String.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonBase64String"/>.</param>
        [Then(@"the result should equal the JsonBase64String '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonBase64String(string expectedValue)
        {
            JsonBase64String expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonBase64String>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64String to JsonString")]
        public void WhenICastTheJsonBase64StringToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonBase64String")]
        public void WhenICastTheJsonStringToJsonBase64String()
        {
            this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64String to ReadOnlySpan<char>")]
        public void WhenICastTheJsonBase64StringToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonBase64String")]
        public void WhenICastTheReadOnlySpanOfCharToJsonBase64String()
        {
            this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64String to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonBase64StringToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonBase64String")]
        public void WhenICastTheReadOnlySpanOfByteToJsonBase64String()
        {
            this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonBase64String"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBase64String to string")]
        public void WhenICastTheJsonBase64StringToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64String"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonBase64String")]
        public void WhenICastTheStringToJsonBase64String()
        {
            this.scenarioContext.Set((JsonBase64String)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* content */

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonContent to JsonAny")]
        public void WhenICastTheJsonContentToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonContent")]
        public void WhenICastTheJsonAnyToJsonContent()
        {
            this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonContent"/> in the context value <see cref="CastResultKey"/> with the given JsonContent.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the JsonContent '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonContent(string expectedValue)
        {
            JsonContent expected = JsonAny.ParseUriValue(expectedValue);
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonContent>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonContent to JsonString")]
        public void WhenICastTheJsonContentToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonContent")]
        public void WhenICastTheJsonStringToJsonContent()
        {
            this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonContent to ReadOnlySpan<char>")]
        public void WhenICastTheJsonContentToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonContent")]
        public void WhenICastTheReadOnlySpanOfCharToJsonContent()
        {
            this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonContent to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonContentToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonContent")]
        public void WhenICastTheReadOnlySpanOfByteToJsonContent()
        {
            this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonContent"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonContent to string")]
        public void WhenICastTheJsonContentToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonContent"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonContent")]
        public void WhenICastTheStringToJsonContent()
        {
            this.scenarioContext.Set((JsonContent)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* email */

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonEmail to JsonAny")]
        public void WhenICastTheJsonEmailToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonEmail")]
        public void WhenICastTheJsonAnyToJsonEmail()
        {
            this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonEmail"/> in the context value <see cref="CastResultKey"/> with the given JsonEmail.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonEmail"/>.</param>
        [Then(@"the result should equal the JsonEmail '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonEmail(string expectedValue)
        {
            JsonEmail expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonEmail>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonEmail to JsonString")]
        public void WhenICastTheJsonEmailToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonEmail")]
        public void WhenICastTheJsonStringToJsonEmail()
        {
            this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonEmail to ReadOnlySpan<char>")]
        public void WhenICastTheJsonEmailToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonEmail")]
        public void WhenICastTheReadOnlySpanOfCharToJsonEmail()
        {
            this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonEmail to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonEmailToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonEmail")]
        public void WhenICastTheReadOnlySpanOfByteToJsonEmail()
        {
            this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonEmail to string")]
        public void WhenICastTheJsonEmailToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonEmail")]
        public void WhenICastTheStringToJsonEmail()
        {
            this.scenarioContext.Set((JsonEmail)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* uriTemplate */

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriTemplate to JsonAny")]
        public void WhenICastTheJsonUriTemplateToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonUriTemplate")]
        public void WhenICastTheJsonAnyToJsonUriTemplate()
        {
            this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonUriTemplate"/> in the context value <see cref="CastResultKey"/> with the given JsonUriTemplate.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonUriTemplate"/>.</param>
        [Then(@"the result should equal the JsonUriTemplate '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonUriTemplate(string expectedValue)
        {
            JsonUriTemplate expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonUriTemplate>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriTemplate to JsonString")]
        public void WhenICastTheJsonUriTemplateToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonUriTemplate")]
        public void WhenICastTheJsonStringToJsonUriTemplate()
        {
            this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriTemplate to ReadOnlySpan<char>")]
        public void WhenICastTheJsonUriTemplateToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonUriTemplate")]
        public void WhenICastTheReadOnlySpanOfCharToJsonUriTemplate()
        {
            this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriTemplate to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonUriTemplateToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonUriTemplate")]
        public void WhenICastTheReadOnlySpanOfByteToJsonUriTemplate()
        {
            this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriTemplate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriTemplate to string")]
        public void WhenICastTheJsonUriTemplateToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriTemplate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonUriTemplate")]
        public void WhenICastTheStringToJsonUriTemplate()
        {
            this.scenarioContext.Set((JsonUriTemplate)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* pointer */

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonPointer to JsonAny")]
        public void WhenICastTheJsonPointerToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonPointer")]
        public void WhenICastTheJsonAnyToJsonPointer()
        {
            this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonPointer"/> in the context value <see cref="CastResultKey"/> with the given JsonPointer.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonPointer"/>.</param>
        [Then(@"the result should equal the JsonPointer '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonPointer(string expectedValue)
        {
            JsonPointer expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonPointer>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonPointer to JsonString")]
        public void WhenICastTheJsonPointerToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonPointer")]
        public void WhenICastTheJsonStringToJsonPointer()
        {
            this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonPointer to ReadOnlySpan<char>")]
        public void WhenICastTheJsonPointerToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonPointer")]
        public void WhenICastTheReadOnlySpanOfCharToJsonPointer()
        {
            this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonPointer to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonPointerToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonPointer")]
        public void WhenICastTheReadOnlySpanOfByteToJsonPointer()
        {
            this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonPointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonPointer to string")]
        public void WhenICastTheJsonPointerToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonPointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonPointer")]
        public void WhenICastTheStringToJsonPointer()
        {
            this.scenarioContext.Set((JsonPointer)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* relativePointer */

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRelativePointer to JsonAny")]
        public void WhenICastTheJsonRelativePointerToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonRelativePointer")]
        public void WhenICastTheJsonAnyToJsonRelativePointer()
        {
            this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonRelativePointer"/> in the context value <see cref="CastResultKey"/> with the given JsonRelativePointer.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonRelativePointer"/>.</param>
        [Then(@"the result should equal the JsonRelativePointer '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonRelativePointer(string expectedValue)
        {
            JsonRelativePointer expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonRelativePointer>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRelativePointer to JsonString")]
        public void WhenICastTheJsonRelativePointerToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonRelativePointer")]
        public void WhenICastTheJsonStringToJsonRelativePointer()
        {
            this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRelativePointer to ReadOnlySpan<char>")]
        public void WhenICastTheJsonRelativePointerToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonRelativePointer")]
        public void WhenICastTheReadOnlySpanOfCharToJsonRelativePointer()
        {
            this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRelativePointer to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonRelativePointerToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonRelativePointer")]
        public void WhenICastTheReadOnlySpanOfByteToJsonRelativePointer()
        {
            this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRelativePointer"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRelativePointer to string")]
        public void WhenICastTheJsonRelativePointerToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRelativePointer"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonRelativePointer")]
        public void WhenICastTheStringToJsonRelativePointer()
        {
            this.scenarioContext.Set((JsonRelativePointer)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* boolean */

        /// <summary>
        /// Casts the <see cref="JsonBoolean"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBoolean to JsonAny")]
        public void WhenICastTheJsonBooleanToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBoolean"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonBoolean")]
        public void WhenICastTheJsonAnyToJsonBoolean()
        {
            this.scenarioContext.Set<IJsonValue>((JsonBoolean)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonBoolean"/> in the context value <see cref="CastResultKey"/> with the given JsonBoolean.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the JsonBoolean '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonBoolean(string expectedValue)
        {
            Assert.AreEqual(JsonAny.ParseUriValue(expectedValue).AsBoolean, this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonBoolean"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="bool"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonBoolean to bool")]
        public void WhenICastTheJsonBooleanToBool()
        {
            this.scenarioContext.Set((bool)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="bool"/> in the context value <see cref="CastResultKey"/> with the given bool.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the bool (.*)")]
        public void ThenTheResultShouldEqualTheBool(bool expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<bool>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="bool"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBoolean"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the bool to JsonBoolean")]
        public void WhenICastTheBoolToJsonBoolean()
        {
            this.scenarioContext.Set<IJsonValue>((JsonBoolean)this.scenarioContext.Get<bool>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* date */

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to JsonAny")]
        public void WhenICastTheJsonDateToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonDate")]
        public void WhenICastTheJsonAnyToJsonDate()
        {
            this.scenarioContext.Set<IJsonValue>((JsonDate)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDate"/> in the context value <see cref="CastResultKey"/> with the given JsonDate.
        /// </summary>
        /// <param name="value">The string representation of the date.</param>
        [Then(@"the result should equal the JsonDate '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonDate(string value)
        {
            Assert.AreEqual(new JsonDate(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to JsonString")]
        public void WhenICastTheJsonDateToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonDate")]
        public void WhenICastTheJsonStringToJsonDate()
        {
            this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="LocalDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to LocalDate")]
        public void WhenICastTheJsonDateToLocalDate()
        {
            this.scenarioContext.Set((LocalDate)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDate"/> in the context value <see cref="CastResultKey"/> with the given LocalDate.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="LocalDate"/>.</param>
        [Then(@"the result should equal the LocalDate '(.*)'")]
        public void ThenTheResultShouldEqualTheLocalDate(string expectedValue)
        {
            Assert.AreEqual(LocalDatePattern.Iso.Parse(expectedValue).Value, this.scenarioContext.Get<LocalDate>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="LocalDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the LocalDate to JsonDate")]
        public void WhenICastTheLocalDateToJsonDate()
        {
            this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<LocalDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonDateToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to ReadOnlySpan<char>")]
        public void WhenICastTheJsonDateToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonDate")]
        public void WhenICastTheReadOnlySpanOfByteToJsonDate()
        {
            this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Char}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonDate")]
        public void WhenICastTheReadOnlySpanOfCharToJsonDate()
        {
            this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDate"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDate to string")]
        public void WhenICastTheJsonDateToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDate"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonDate")]
        public void WhenICastTheStringToJsonDate()
        {
            this.scenarioContext.Set((JsonDate)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* time */

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to JsonAny")]
        public void WhenICastTheJsonTimeToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonTime")]
        public void WhenICastTheJsonAnyToJsonTime()
        {
            this.scenarioContext.Set<IJsonValue>((JsonTime)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonTime"/> in the context value <see cref="CastResultKey"/> with the given JsonTime.
        /// </summary>
        /// <param name="value">The string representation of the time.</param>
        [Then(@"the result should equal the JsonTime '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonTime(string value)
        {
            Assert.AreEqual(new JsonTime(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to JsonString")]
        public void WhenICastTheJsonTimeToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonTime")]
        public void WhenICastTheJsonStringToJsonTime()
        {
            this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="OffsetTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to OffsetTime")]
        public void WhenICastTheJsonTimeToOffsetTime()
        {
            this.scenarioContext.Set((OffsetTime)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonTime"/> in the context value <see cref="CastResultKey"/> with the given OffsetTime.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="OffsetTime"/>.</param>
        [Then(@"the result should equal the OffsetTime '(.*)'")]
        public void ThenTheResultShouldEqualTheOffsetTime(string expectedValue)
        {
            Assert.AreEqual(OffsetTimePattern.ExtendedIso.Parse(expectedValue).Value, this.scenarioContext.Get<OffsetTime>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="OffsetTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the OffsetTime to JsonTime")]
        public void WhenICastTheOffsetTimeToJsonTime()
        {
            this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<OffsetTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonTimeToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to ReadOnlySpan<char>")]
        public void WhenICastTheJsonTimeToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonTime")]
        public void WhenICastTheReadOnlySpanOfByteToJsonTime()
        {
            this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Char}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonTime")]
        public void WhenICastTheReadOnlySpanOfCharToJsonTime()
        {
            this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonTime to string")]
        public void WhenICastTheJsonTimeToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonTime")]
        public void WhenICastTheStringToJsonTime()
        {
            this.scenarioContext.Set((JsonTime)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* datetime */

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to JsonAny")]
        public void WhenICastTheJsonDateTimeToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonDateTime")]
        public void WhenICastTheJsonAnyToJsonDateTime()
        {
            this.scenarioContext.Set<IJsonValue>((JsonDateTime)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDateTime"/> in the context value <see cref="CastResultKey"/> with the given JsonDateTime.
        /// </summary>
        /// <param name="value">The string representation of the dateTime.</param>
        [Then(@"the result should equal the JsonDateTime '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonDateTime(string value)
        {
            Assert.AreEqual(new JsonDateTime(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to JsonString")]
        public void WhenICastTheJsonDateTimeToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonDateTime")]
        public void WhenICastTheJsonStringToJsonDateTime()
        {
            this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="OffsetDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to OffsetDateTime")]
        public void WhenICastTheJsonDateTimeToOffsetDateTime()
        {
            this.scenarioContext.Set((OffsetDateTime)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDateTime"/> in the context value <see cref="CastResultKey"/> with the given OffsetDateTime.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="OffsetDateTime"/>.</param>
        [Then(@"the result should equal the OffsetDateTime '(.*)'")]
        public void ThenTheResultShouldEqualTheOffsetDateTime(string expectedValue)
        {
            Assert.AreEqual(OffsetDateTimePattern.ExtendedIso.Parse(expectedValue).Value, this.scenarioContext.Get<OffsetDateTime>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="OffsetDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the OffsetDateTime to JsonDateTime")]
        public void WhenICastTheOffsetDateTimeToJsonDateTime()
        {
            this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<OffsetDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonDateTimeToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to ReadOnlySpan<char>")]
        public void WhenICastTheJsonDateTimeToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonDateTime")]
        public void WhenICastTheReadOnlySpanOfByteToJsonDateTime()
        {
            this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Char}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonDateTime")]
        public void WhenICastTheReadOnlySpanOfCharToJsonDateTime()
        {
            this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDateTime"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDateTime to string")]
        public void WhenICastTheJsonDateTimeToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDateTime"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonDateTime")]
        public void WhenICastTheStringToJsonDateTime()
        {
            this.scenarioContext.Set((JsonDateTime)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to JsonAny")]
        public void WhenICastTheJsonDurationToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonDuration")]
        public void WhenICastTheJsonAnyToJsonDuration()
        {
            this.scenarioContext.Set<IJsonValue>((JsonDuration)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDuration"/> in the context value <see cref="CastResultKey"/> with the given JsonDuration.
        /// </summary>
        /// <param name="value">The string representation of the duration.</param>
        [Then(@"the result should equal the JsonDuration '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonDuration(string value)
        {
            Assert.AreEqual(new JsonDuration(value), this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to JsonString")]
        public void WhenICastTheJsonDurationToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonDuration")]
        public void WhenICastTheJsonStringToJsonDuration()
        {
            this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Period"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to Period")]
        public void WhenICastTheJsonDurationToPeriod()
        {
            this.scenarioContext.Set((Period)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonDuration"/> in the context value <see cref="CastResultKey"/> with the given Period.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="Period"/>.</param>
        [Then(@"the result should equal the Period '(.*)'")]
        public void ThenTheResultShouldEqualThePeriod(string expectedValue)
        {
            Assert.AreEqual(PeriodPattern.NormalizingIso.Parse(expectedValue).Value, this.scenarioContext.Get<Period>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="Period"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Period to JsonDuration")]
        public void WhenICastThePeriodToJsonDuration()
        {
            this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<Period>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonDurationToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to ReadOnlySpan<char>")]
        public void WhenICastTheJsonDurationToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonDuration")]
        public void WhenICastTheReadOnlySpanOfByteToJsonDuration()
        {
            this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Char}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonDuration")]
        public void WhenICastTheReadOnlySpanOfCharToJsonDuration()
        {
            this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonDuration"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see langword="string"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonDuration to string")]
        public void WhenICastTheJsonDurationToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonDuration"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonDuration")]
        public void WhenICastTheStringToJsonDuration()
        {
            this.scenarioContext.Set((JsonDuration)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* string */

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonAny")]
        public void WhenICastTheJsonStringToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonString")]
        public void WhenICastTheJsonAnyToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonString")]
        public void WhenICastTheJsonStringToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to ReadOnlySpan<char>")]
        public void WhenICastTheJsonStringToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonString")]
        public void WhenICastTheReadOnlySpanOfCharToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonStringToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonString")]
        public void WhenICastTheReadOnlySpanOfByteToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to string")]
        public void WhenICastTheJsonStringToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonString")]
        public void WhenICastTheStringToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* any */

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="bool"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to bool")]
        public void WhenICastTheJsonAnyToBool()
        {
            this.scenarioContext.Set((bool)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="bool"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the bool to JsonAny")]
        public void WhenICastTheBoolToJsonAny()
        {
            this.scenarioContext.Set<IJsonValue>((JsonAny)this.scenarioContext.Get<bool>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to ReadOnlySpan<char>")]
        public void WhenICastTheJsonAnyToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonAny")]
        public void WhenICastTheReadOnlySpanOfCharToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonAnyToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonAny")]
        public void WhenICastTheReadOnlySpanOfByteToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to string")]
        public void WhenICastTheJsonAnyToJsonAny()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonBase64Content"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonAny")]
        public void WhenICastTheStringToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the any to JsonAny")]
        public void WhenICastTheJsonAnyToString()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* hostname */

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonHostname to JsonAny")]
        public void WhenICastTheJsonHostnameToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonHostname")]
        public void WhenICastTheJsonAnyToJsonHostname()
        {
            this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonHostname"/> in the context value <see cref="CastResultKey"/> with the given JsonHostname.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonHostname"/>.</param>
        [Then(@"the result should equal the JsonHostname '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonHostname(string expectedValue)
        {
            JsonHostname expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonHostname>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonHostname to JsonString")]
        public void WhenICastTheJsonHostnameToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonHostname")]
        public void WhenICastTheJsonStringToJsonHostname()
        {
            this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonHostname to ReadOnlySpan<char>")]
        public void WhenICastTheJsonHostnameToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonHostname")]
        public void WhenICastTheReadOnlySpanOfCharToJsonHostname()
        {
            this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonHostname to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonHostnameToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonHostname")]
        public void WhenICastTheReadOnlySpanOfByteToJsonHostname()
        {
            this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonHostname to string")]
        public void WhenICastTheJsonHostnameToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonHostname")]
        public void WhenICastTheStringToJsonHostname()
        {
            this.scenarioContext.Set((JsonHostname)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* idnIdnEmail */

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnEmail to JsonAny")]
        public void WhenICastTheJsonIdnEmailToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIdnEmail")]
        public void WhenICastTheJsonAnyToJsonIdnEmail()
        {
            this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIdnEmail"/> in the context value <see cref="CastResultKey"/> with the given JsonIdnEmail.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIdnEmail"/>.</param>
        [Then(@"the result should equal the JsonIdnEmail '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIdnEmail(string expectedValue)
        {
            JsonIdnEmail expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIdnEmail>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnEmail to JsonString")]
        public void WhenICastTheJsonIdnEmailToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIdnEmail")]
        public void WhenICastTheJsonStringToJsonIdnEmail()
        {
            this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnEmail to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIdnEmailToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIdnEmail")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIdnEmail()
        {
            this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnEmail to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIdnEmailToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIdnEmail")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIdnEmail()
        {
            this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnEmail"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnEmail to string")]
        public void WhenICastTheJsonIdnEmailToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnEmail"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIdnEmail")]
        public void WhenICastTheStringToJsonIdnEmail()
        {
            this.scenarioContext.Set((JsonIdnEmail)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* idnHostname */

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnHostname to JsonAny")]
        public void WhenICastTheJsonIdnHostnameToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIdnHostname")]
        public void WhenICastTheJsonAnyToJsonIdnHostname()
        {
            this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIdnHostname"/> in the context value <see cref="CastResultKey"/> with the given JsonIdnHostname.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIdnHostname"/>.</param>
        [Then(@"the result should equal the JsonIdnHostname '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIdnHostname(string expectedValue)
        {
            JsonIdnHostname expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIdnHostname>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnHostname to JsonString")]
        public void WhenICastTheJsonIdnHostnameToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIdnHostname")]
        public void WhenICastTheJsonStringToJsonIdnHostname()
        {
            this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnHostname to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIdnHostnameToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIdnHostname")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIdnHostname()
        {
            this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnHostname to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIdnHostnameToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIdnHostname")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIdnHostname()
        {
            this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIdnHostname"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIdnHostname to string")]
        public void WhenICastTheJsonIdnHostnameToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIdnHostname"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIdnHostname")]
        public void WhenICastTheStringToJsonIdnHostname()
        {
            this.scenarioContext.Set((JsonIdnHostname)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* integer */

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to JsonAny")]
        public void WhenICastTheJsonIntegerToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonInteger")]
        public void WhenICastTheJsonAnyToJsonInteger()
        {
            this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonInteger"/> in the context value <see cref="CastResultKey"/> with the given JsonInteger.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the JsonInteger '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonInteger(string expectedValue)
        {
            Assert.AreEqual((JsonInteger)JsonAny.ParseUriValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to long")]
        public void WhenICastTheJsonIntegerToLong()
        {
            this.scenarioContext.Set((long)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="long"/> in the context value <see cref="CastResultKey"/> with the given long.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the long (.*)")]
        public void ThenTheResultShouldEqualTheLong(long expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<long>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the long to JsonInteger")]
        public void WhenICastTheLongToJsonInteger()
        {
            this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to int")]
        public void WhenICastTheJsonIntegerToInt()
        {
            this.scenarioContext.Set((int)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="int"/> in the context value <see cref="CastResultKey"/> with the given int.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="int"/>.</param>
        [Then(@"the result should equal the int (.*)")]
        public void ThenTheResultShouldEqualTheInt(int expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<int>(CastResultKey));
        }

        /// <summary>
        /// Compares the <see cref="IPAddress"/> in the context value <see cref="CastResultKey"/> with the given IPAddress.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="IPAddress"/>.</param>
        [Then(@"the result should equal the IPAddress (.*)")]
        public void ThenTheResultShouldEqualTheIPAddress(string expectedValue)
        {
            Assert.AreEqual(IPAddress.Parse(expectedValue), this.scenarioContext.Get<IPAddress>(CastResultKey));
        }

        /// <summary>
        /// Compares the <see cref="Uri"/> in the context value <see cref="CastResultKey"/> with the given IPAddress.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="Uri"/>.</param>
        [Then(@"the result should equal the Uri (.*)")]
        public void ThenTheResultShouldEqualTheUri(string expectedValue)
        {
            Assert.AreEqual(new Uri(expectedValue, UriKind.RelativeOrAbsolute), this.scenarioContext.Get<Uri>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the int to JsonInteger")]
        public void WhenICastTheIntToJsonInteger()
        {
            this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to double")]
        public void WhenICastTheJsonIntegerToDouble()
        {
            this.scenarioContext.Set((double)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="double"/> in the context value <see cref="CastResultKey"/> with the given double.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the double (.*)")]
        public void ThenTheResultShouldEqualTheDouble(double expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<double>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the double to JsonInteger")]
        public void WhenICastTheDoubleToJsonInteger()
        {
            this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to float")]
        public void WhenICastTheJsonIntegerToFloat()
        {
            this.scenarioContext.Set((float)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="float"/> in the context value <see cref="CastResultKey"/> with the given float.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the float (.*)")]
        public void ThenTheResultShouldEqualTheFloat(float expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<float>(CastResultKey), 0.00001);
        }

        /// <summary>
        /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the float to JsonInteger")]
        public void WhenICastTheFloatToJsonInteger()
        {
            this.scenarioContext.Set<IJsonValue>((JsonInteger)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* number */

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonInteger"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to JsonInteger")]
        public void WhenICastTheJsonNumberToJsonInteger()
        {
            this.scenarioContext.Set((JsonInteger)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonInteger"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonInteger to JsonNumber")]
        public void WhenICastTheJsonIntegerToJsonNumber()
        {
            this.scenarioContext.Set((JsonNumber)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to JsonAny")]
        public void WhenICastTheJsonNumberToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonNumber")]
        public void WhenICastTheJsonAnyToJsonNumber()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonNumber"/> in the context value <see cref="CastResultKey"/> with the given JsonNumber.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the JsonNumber '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonNumber(string expectedValue)
        {
            Assert.AreEqual((JsonNumber)JsonAny.ParseUriValue(expectedValue), this.scenarioContext.Get<IJsonValue>(CastResultKey).AsAny, 0.00001);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="long"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to long")]
        public void WhenICastTheJsonNumberToLong()
        {
            this.scenarioContext.Set((long)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="long"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the long to JsonNumber")]
        public void WhenICastTheLongToJsonNumber()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<long>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="int"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to int")]
        public void WhenICastTheJsonNumberToInt()
        {
            this.scenarioContext.Set((int)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="int"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the int to JsonNumber")]
        public void WhenICastTheIntToJsonNumber()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<int>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="double"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to double")]
        public void WhenICastTheJsonNumberToDouble()
        {
            this.scenarioContext.Set((double)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="double"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the double to JsonNumber")]
        public void WhenICastTheDoubleToJsonNumber()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<double>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonNumber"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="float"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonNumber to float")]
        public void WhenICastTheJsonNumberToFloat()
        {
            this.scenarioContext.Set((float)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="float"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonNumber"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the float to JsonNumber")]
        public void WhenICastTheFloatToJsonNumber()
        {
            this.scenarioContext.Set<IJsonValue>((JsonNumber)this.scenarioContext.Get<float>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* uuid */

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to JsonAny")]
        public void WhenICastTheJsonUuidToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonUuid")]
        public void WhenICastTheJsonAnyToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonUuid"/> in the context value <see cref="CastResultKey"/> with the given JsonUuid.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonUuid"/>.</param>
        [Then(@"the result should equal the JsonUuid '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonUuid(string expectedValue)
        {
            JsonUuid expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonUuid>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to JsonString")]
        public void WhenICastTheJsonUuidToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonUuid")]
        public void WhenICastTheJsonStringToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to ReadOnlySpan<char>")]
        public void WhenICastTheJsonUuidToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonUuid")]
        public void WhenICastTheReadOnlySpanOfCharToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonUuidToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonUuid")]
        public void WhenICastTheReadOnlySpanOfByteToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to string")]
        public void WhenICastTheJsonUuidToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonUuid")]
        public void WhenICastTheStringToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUuid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Guid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUuid to Guid")]
        public void WhenICastTheJsonUuidToGuid()
        {
            this.scenarioContext.Set((Guid)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Guid"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUuid"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Guid to JsonUuid")]
        public void WhenICastTheGuidToJsonUuid()
        {
            this.scenarioContext.Set((JsonUuid)this.scenarioContext.Get<Guid>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="Guid"/> in the context value <see cref="CastResultKey"/> with the given long.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonContent"/>.</param>
        [Then(@"the result should equal the Guid (.*)")]
        public void ThenTheResultShouldEqualTheGuid(Guid expectedValue)
        {
            Assert.AreEqual(expectedValue, this.scenarioContext.Get<Guid>(CastResultKey));
        }

        /* ipV4 */

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to JsonAny")]
        public void WhenICastTheJsonIpV4ToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIpV4")]
        public void WhenICastTheJsonAnyToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIpV4"/> in the context value <see cref="CastResultKey"/> with the given JsonIpV4.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIpV4"/>.</param>
        [Then(@"the result should equal the JsonIpV4 '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIpV4(string expectedValue)
        {
            JsonIpV4 expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIpV4>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to JsonString")]
        public void WhenICastTheJsonIpV4ToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIpV4")]
        public void WhenICastTheJsonStringToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIpV4ToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIpV4")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIpV4ToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIpV4")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to string")]
        public void WhenICastTheJsonIpV4ToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIpV4")]
        public void WhenICastTheStringToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV4"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="IPAddress"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV4 to IPAddress")]
        public void WhenICastTheJsonIpV4ToIPAddress()
        {
            this.scenarioContext.Set((IPAddress)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="IPAddress"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV4"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the IPAddress to JsonIpV4")]
        public void WhenICastTheIPAddressToJsonIpV4()
        {
            this.scenarioContext.Set((JsonIpV4)this.scenarioContext.Get<IPAddress>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* ipV6 */

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to JsonAny")]
        public void WhenICastTheJsonIpV6ToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIpV6")]
        public void WhenICastTheJsonAnyToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIpV6"/> in the context value <see cref="CastResultKey"/> with the given JsonIpV6.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIpV6"/>.</param>
        [Then(@"the result should equal the JsonIpV6 '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIpV6(string expectedValue)
        {
            JsonIpV6 expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIpV6>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to JsonString")]
        public void WhenICastTheJsonIpV6ToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIpV6")]
        public void WhenICastTheJsonStringToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIpV6ToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIpV6")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIpV6ToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIpV6")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to string")]
        public void WhenICastTheJsonIpV6ToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIpV6")]
        public void WhenICastTheStringToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIpV6"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="IPAddress"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIpV6 to IPAddress")]
        public void WhenICastTheJsonIpV6ToIPAddress()
        {
            this.scenarioContext.Set((IPAddress)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="IPAddress"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIpV6"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the IPAddress to JsonIpV6")]
        public void WhenICastTheIPAddressToJsonIpV6()
        {
            this.scenarioContext.Set((JsonIpV6)this.scenarioContext.Get<IPAddress>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* regex */

        /// <summary>
        /// Gets the value store in <see cref="CastResultKey"/> and matches it against the Regex serialized in <paramref name="expected"/>.
        /// </summary>
        /// <param name="expected">The serialized form of the result to match.</param>
        [Then(@"the result should equal the Regex (.*)")]
        public void ThenTheResultShouldEqualTheRegex(string expected)
        {
            Assert.AreEqual(new Regex(expected).ToString(), this.scenarioContext.Get<Regex>(CastResultKey).ToString());
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to JsonAny")]
        public void WhenICastTheJsonRegexToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonRegex")]
        public void WhenICastTheJsonAnyToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonRegex"/> in the context value <see cref="CastResultKey"/> with the given JsonRegex.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonRegex"/>.</param>
        [Then(@"the result should equal the JsonRegex '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonRegex(string expectedValue)
        {
            JsonRegex expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonRegex>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to JsonString")]
        public void WhenICastTheJsonRegexToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonRegex")]
        public void WhenICastTheJsonStringToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to ReadOnlySpan<char>")]
        public void WhenICastTheJsonRegexToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonRegex")]
        public void WhenICastTheReadOnlySpanOfCharToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonRegexToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonRegex")]
        public void WhenICastTheReadOnlySpanOfByteToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to string")]
        public void WhenICastTheJsonRegexToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonRegex")]
        public void WhenICastTheStringToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonRegex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Regex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonRegex to Regex")]
        public void WhenICastTheJsonRegexToRegex()
        {
            this.scenarioContext.Set((Regex)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Regex"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonRegex"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Regex to JsonRegex")]
        public void WhenICastTheRegexToJsonRegex()
        {
            this.scenarioContext.Set((JsonRegex)this.scenarioContext.Get<Regex>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* iri */

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to JsonAny")]
        public void WhenICastTheJsonIriToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIri")]
        public void WhenICastTheJsonAnyToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIri"/> in the context value <see cref="CastResultKey"/> with the given JsonIri.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIri"/>.</param>
        [Then(@"the result should equal the JsonIri '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIri(string expectedValue)
        {
            JsonIri expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIri>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to JsonString")]
        public void WhenICastTheJsonIriToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIri")]
        public void WhenICastTheJsonStringToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIriToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIri")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIriToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIri")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to string")]
        public void WhenICastTheJsonIriToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIri")]
        public void WhenICastTheStringToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIri to Uri")]
        public void WhenICastTheJsonIriToUri()
        {
            this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Uri to JsonIri")]
        public void WhenICastTheUriToJsonIri()
        {
            this.scenarioContext.Set((JsonIri)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* iriReference */

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to JsonAny")]
        public void WhenICastTheJsonIriReferenceToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonIriReference")]
        public void WhenICastTheJsonAnyToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonIriReference"/> in the context value <see cref="CastResultKey"/> with the given JsonIriReference.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonIriReference"/>.</param>
        [Then(@"the result should equal the JsonIriReference '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonIriReference(string expectedValue)
        {
            JsonIriReference expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonIriReference>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to JsonString")]
        public void WhenICastTheJsonIriReferenceToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonIriReference")]
        public void WhenICastTheJsonStringToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to ReadOnlySpan<char>")]
        public void WhenICastTheJsonIriReferenceToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonIriReference")]
        public void WhenICastTheReadOnlySpanOfCharToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonIriReferenceToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonIriReference")]
        public void WhenICastTheReadOnlySpanOfByteToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to string")]
        public void WhenICastTheJsonIriReferenceToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonIriReference")]
        public void WhenICastTheStringToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonIriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonIriReference to Uri")]
        public void WhenICastTheJsonIriReferenceToUri()
        {
            this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonIriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Uri to JsonIriReference")]
        public void WhenICastTheUriToJsonIriReference()
        {
            this.scenarioContext.Set((JsonIriReference)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* uri */

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to JsonAny")]
        public void WhenICastTheJsonUriToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonUri")]
        public void WhenICastTheJsonAnyToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonUri"/> in the context value <see cref="CastResultKey"/> with the given JsonUri.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonUri"/>.</param>
        [Then(@"the result should equal the JsonUri '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonUri(string expectedValue)
        {
            JsonUri expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonUri>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to JsonString")]
        public void WhenICastTheJsonUriToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonUri")]
        public void WhenICastTheJsonStringToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to ReadOnlySpan<char>")]
        public void WhenICastTheJsonUriToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonUri")]
        public void WhenICastTheReadOnlySpanOfCharToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonUriToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonUri")]
        public void WhenICastTheReadOnlySpanOfByteToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to string")]
        public void WhenICastTheJsonUriToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonUri")]
        public void WhenICastTheStringToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUri to Uri")]
        public void WhenICastTheJsonUriToUri()
        {
            this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Uri to JsonUri")]
        public void WhenICastTheUriToJsonUri()
        {
            this.scenarioContext.Set((JsonUri)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /* uriReference */

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonAny"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to JsonAny")]
        public void WhenICastTheJsonUriReferenceToJsonAny()
        {
            this.scenarioContext.Set((JsonAny)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonAny"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonAny to JsonUriReference")]
        public void WhenICastTheJsonAnyToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Compares the <see cref="JsonUriReference"/> in the context value <see cref="CastResultKey"/> with the given JsonUriReference.
        /// </summary>
        /// <param name="expectedValue">The serialized form of the <see cref="JsonUriReference"/>.</param>
        [Then(@"the result should equal the JsonUriReference '(.*)'")]
        public void ThenTheResultShouldEqualTheJsonUriReference(string expectedValue)
        {
            JsonUriReference expected = JsonAny.ParseUriValue(expectedValue).AsString;
            Assert.AreEqual(expected, this.scenarioContext.Get<JsonUriReference>(CastResultKey));
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonString"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to JsonString")]
        public void WhenICastTheJsonUriReferenceToJsonString()
        {
            this.scenarioContext.Set((JsonString)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonString"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonString to JsonUriReference")]
        public void WhenICastTheJsonStringToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to ReadOnlySpan<char>")]
        public void WhenICastTheJsonUriReferenceToReadOnlySpanOfChar()
        {
            this.scenarioContext.Set<ReadOnlyMemory<char>>(((ReadOnlySpan<char>)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Char}"/>, converts that to a <see cref="ReadOnlyMemory{Char}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<char> to JsonUriReference")]
        public void WhenICastTheReadOnlySpanOfCharToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<ReadOnlyMemory<char>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="ReadOnlySpan{Byte}"/>, converts that to a <see cref="ReadOnlyMemory{Byte}"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to ReadOnlySpan<byte>")]
        public void WhenICastTheJsonUriReferenceToReadOnlySpanOfByte()
        {
            this.scenarioContext.Set<ReadOnlyMemory<byte>>(((ReadOnlySpan<byte>)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest)).ToArray().AsMemory(), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="ReadOnlySpan{Byte}"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the ReadOnlySpan<byte> to JsonUriReference")]
        public void WhenICastTheReadOnlySpanOfByteToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<ReadOnlyMemory<byte>>(JsonValueSteps.SubjectUnderTest).Span, CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="string"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to string")]
        public void WhenICastTheJsonUriReferenceToString()
        {
            this.scenarioContext.Set((string)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="string"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the string to JsonUriReference")]
        public void WhenICastTheStringToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<string>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="JsonUriReference"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="Uri"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the JsonUriReference to Uri")]
        public void WhenICastTheJsonUriReferenceToUri()
        {
            this.scenarioContext.Set((Uri)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }

        /// <summary>
        /// Casts the <see cref="Uri"/> in the key <see cref="JsonValueSteps.SubjectUnderTest"/> to <see cref="JsonUriReference"/> and stores it in <see cref="CastResultKey"/>.
        /// </summary>
        [When(@"I cast the Uri to JsonUriReference")]
        public void WhenICastTheUriToJsonUriReference()
        {
            this.scenarioContext.Set((JsonUriReference)this.scenarioContext.Get<Uri>(JsonValueSteps.SubjectUnderTest), CastResultKey);
        }
    }
}
