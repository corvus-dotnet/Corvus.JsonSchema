// <copyright file="JsonArraySteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Steps
{
    using System;
    using Corvus.Json;
    using NUnit.Framework;
    using TechTalk.SpecFlow;

    /// <summary>
    /// Steps for Json value types.
    /// </summary>
    [Binding]
    public class JsonArraySteps
    {
        private const string ArrayValueResultkey = "ArrayValueResult";
        private const string ArrayExceptionKey = "ArrayException";

        private readonly ScenarioContext scenarioContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonArraySteps"/> class.
        /// </summary>
        /// <param name="scenarioContext">The scenario context.</param>
        public JsonArraySteps(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and removes the item at the given index, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        [When(@"I remove the item at index (.*) from the JsonArray")]
        public void WhenIRemoveTheItemAtIndexFromTheJsonArray(int index)
        {
            try
            {
                this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).RemoveAt(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and removes the item at the given index, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        [When(@"I remove the item at index (.*) from the JsonAny")]
        public void WhenIRemoveTheItemAtIndexFromTheJsonAny(int index)
        {
            try
            {
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveAt(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and removes the item at the given index, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        [When(@"I remove the item at index (.*) from the JsonNotAny")]
        public void WhenIRemoveTheItemAtIndexFromTheJsonNotAny(int index)
        {
            try
            {
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveAt(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonArray at index (.*) should be (.*) of type JsonObject")]
        public void ThenTheItemAtShouldBeTheObject(int index, string expected)
        {
            JsonArray value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonObject>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonArray at index (.*) should be (.*) of type JsonArray")]
        public void ThenTheItemAtShouldBeTheArray(int index, string expected)
        {
            JsonArray value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonArray>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonArray at index (.*) should be (.*) of type JsonNumber")]
        public void ThenTheItemAtShouldBeTheNumber(int index, string expected)
        {
            JsonArray value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonNumber>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonArray at index (.*) should be (.*) of type JsonBoolean")]
        public void ThenTheItemAtShouldBeTheBoolean(int index, string expected)
        {
            JsonArray value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonBoolean>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonArray at index (.*) should be (.*) of type JsonString")]
        public void ThenTheItemAtShouldBeTheString(int index, string expected)
        {
            JsonArray value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonString>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and sets the item at the given index to the provided value storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        /// <param name="value">The serialized value to set.</param>
        [When(@"I set the item in the JsonArray at index (.*) to the value (.*)")]
        public void WhenISetTheItemToTheValue(int index, string value)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.SetItem(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonAny at index (.*) should be (.*) of type JsonObject")]
        public void ThenTheItemInTheJsonAnyAtShouldBeTheObject(int index, string expected)
        {
            JsonAny value = this.scenarioContext.Get<JsonAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonObject>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonAny at index (.*) should be (.*) of type JsonArray")]
        public void ThenTheItemInTheJsonAnyAtShouldBeTheArray(int index, string expected)
        {
            JsonAny value = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonArray>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonAny at index (.*) should be (.*) of type JsonNumber")]
        public void ThenTheItemInTheJsonAnyAtShouldBeTheNumber(int index, string expected)
        {
            JsonAny value = this.scenarioContext.Get<JsonAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonNumber>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonAny at index (.*) should be (.*) of type JsonBoolean")]
        public void ThenTheItemInTheJsonAnyAtShouldBeTheBoolean(int index, string expected)
        {
            JsonAny value = this.scenarioContext.Get<JsonAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonBoolean>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonAny at index (.*) should be (.*) of type JsonString")]
        public void ThenTheItemInTheJsonAnyAtShouldBeTheString(int index, string expected)
        {
            JsonAny value = this.scenarioContext.Get<JsonAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonString>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and sets the item at the given index to the provided value storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        /// <param name="value">The serialized value to set.</param>
        [When(@"I set the item in the JsonAny at index (.*) to the value (.*)")]
        public void WhenISetTheItemInTheJsonAnyToTheValue(int index, string value)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.SetItem(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type JsonObject")]
        public void ThenTheItemInTheJsonNotAnyAtShouldBeTheObject(int index, string expected)
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonObject>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type JsonNotAny")]
        public void ThenTheItemInTheJsonNotAnyAtShouldBeTheArray(int index, string expected)
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonNotAny>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type JsonNumber")]
        public void ThenTheItemInTheJsonNotAnyAtShouldBeTheNumber(int index, string expected)
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonNumber>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type JsonBoolean")]
        public void ThenTheItemInTheJsonNotAnyAtShouldBeTheBoolean(int index, string expected)
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonBoolean>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type JsonString")]
        public void ThenTheItemInTheJsonNotAnyAtShouldBeTheString(int index, string expected)
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), value.GetItem<JsonString>(index));
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares the item at the given index with the given value.
        /// </summary>
        /// <param name="index">The index at which to test the item.</param>
        /// <param name="expected">The serialized value expected to be found at the given index.</param>
        [Then(@"the item in the JsonNotAny at index (.*) should be (.*) of type <undefined>")]
#pragma warning disable IDE0060 // Remove parameter if not shipped
        public void ThenTheItemInTheJsonNotAnyAtShouldBeUndefined(int index, string expected)
#pragma warning restore IDE0060 // Remove parameter if not shipped
        {
            JsonNotAny value = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.IsTrue(value.Length <= index);
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and sets the item at the given index to the provided value storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to remove the item.</param>
        /// <param name="value">The serialized value to set.</param>
        [When(@"I set the item in the JsonNotAny at index (.*) to the value (.*)")]
        public void WhenISetTheItemInTheJsonNotAnyToTheValue(int index, string value)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.SetItem(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Checks the the entity in the context with key <see cref="ArrayExceptionKey"/> is an ArgumentOutOfRangeException.
        /// </summary>
        [Then(@"the array operation should produce an ArgumentOutOfRangeException")]
        public void ThenTheArrayOperationShouldProduceAndArgumentOutOfRangeException()
        {
            Assert.IsTrue(this.scenarioContext.ContainsKey(ArrayExceptionKey));
            Assert.IsAssignableFrom<ArgumentOutOfRangeException>(this.scenarioContext.Get<object>(ArrayExceptionKey));
        }

        /// <summary>
        /// Checks the the entity in the context with key <see cref="ArrayExceptionKey"/> is an IndexOutOfRangeException.
        /// </summary>
        [Then(@"the array operation should produce an IndexOutOfRangeException")]
        public void ThenTheArrayOperationShouldProduceAndIndexOutOfRangeException()
        {
            Assert.IsTrue(this.scenarioContext.ContainsKey(ArrayExceptionKey));
            Assert.IsAssignableFrom<IndexOutOfRangeException>(this.scenarioContext.Get<object>(ArrayExceptionKey));
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonNumber in the JsonArray at index (.*)")]
        public void WhenIGetTheNumberInTheJsonArrayAtIndex(int index)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonNumber>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonNumber in the JsonAny at index (.*)")]
        public void WhenIGetTheNumberInTheJsonAnyAtIndex(int index)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonNumber>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonNumber in the JsonNotAny at index (.*)")]
        public void WhenIGetTheNumberInTheJsonNotAnyAtIndex(int index)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonNumber>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonString in the JsonArray at index (.*)")]
        public void WhenIGetTheStringInTheJsonArrayAtIndex(int index)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonString>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonString in the JsonAny at index (.*)")]
        public void WhenIGetTheStringInTheJsonAnyAtIndex(int index)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonString>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// and gets the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I get the JsonString in the JsonNotAny at index (.*)")]
        public void WhenIGetTheStringInTheJsonNotAnyAtIndex(int index)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.GetItem<JsonString>(index), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// inserts the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to insert.</param>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I insert the item (.*) in the JsonArray at index (.*)")]
        public void WhenIInsertTheItemInTheJsonArrayAtIndex(string value, int index)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Insert(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// replaces the item with the given value, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="oldValue">The serialized value to replace.</param>
        /// <param name="newValue">The serialized replacement value.</param>
        [When(@"I replace the item (.*) in the JsonArray with the value (.*)")]
        public void WhenIReplaceTheItemInTheJsonArrayAtIndex(string oldValue, string newValue)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Replace(JsonAny.ParseUriValue(oldValue), JsonAny.ParseUriValue(newValue)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares it to the expected value.
        /// </summary>
        /// <param name="expected">The serialized value.</param>
        [Then(@"the JsonArray should equal (.*)")]
        public void ThenTheJsonArrayShouldEqual(string expected)
        {
            JsonArray actual = this.scenarioContext.Get<JsonArray>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), actual);
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// inserts the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to insert.</param>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I insert the item (.*) in the JsonAny at index (.*)")]
        public void WhenIInsertTheItemInTheJsonAnyAtIndex(string value, int index)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Insert(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// replaces the item with the given value, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="oldValue">The serialized value to replace.</param>
        /// <param name="newValue">The serialized replacement value.</param>
        [When(@"I replace the item (.*) in the JsonAny with the value (.*)")]
        public void WhenIReplaceTheItemInTheJsonAnyAtIndex(string oldValue, string newValue)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Replace(JsonAny.ParseUriValue(oldValue), JsonAny.ParseUriValue(newValue)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares it to the expected value.
        /// </summary>
        /// <param name="expected">The serialized value.</param>
        [Then(@"the JsonAny should equal (.*)")]
        public void ThenTheJsonAnyShouldEqual(string expected)
        {
            JsonAny actual = this.scenarioContext.Get<JsonAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), actual);
        }

        /// <summary>
        /// Gets the <see cref="JsonString"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares it to the expected value.
        /// </summary>
        /// <param name="expected">The serialized value.</param>
        [Then(@"the JsonString should equal (.*)")]
        public void ThenTheJsonStringShouldEqual(string expected)
        {
            JsonAny actual = this.scenarioContext.Get<JsonString>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), actual);
        }

        /// <summary>
        /// Gets the <see cref="JsonNumber"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares it to the expected value.
        /// </summary>
        /// <param name="expected">The serialized value.</param>
        [Then(@"the JsonNumber should equal (.*)")]
        public void ThenTheJsonNumberShouldEqual(string expected)
        {
            JsonAny actual = this.scenarioContext.Get<JsonNumber>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), actual);
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// inserts the item at the given index storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to insert.</param>
        /// <param name="index">The index at which to get the item.</param>
        [When(@"I insert the item (.*) in the JsonNotAny at index (.*)")]
        public void WhenIInsertTheItemInTheJsonNotAnyAtIndex(string value, int index)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Insert(index, JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>,
        /// replaces the item with the given value, storing the result in <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="oldValue">The serialized value to replace.</param>
        /// <param name="newValue">The serialized replacement value.</param>
        [When(@"I replace the item (.*) in the JsonNotAny with the value (.*)")]
        public void WhenIReplaceTheItemInTheJsonNotAnyAtIndex(string oldValue, string newValue)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Replace(JsonAny.ParseUriValue(oldValue), JsonAny.ParseUriValue(newValue)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="ArrayValueResultkey"/>
        /// and compares it to the expected value.
        /// </summary>
        /// <param name="expected">The serialized value.</param>
        [Then(@"the JsonNotAny should equal (.*)")]
        public void ThenTheJsonNotAnyShouldEqual(string expected)
        {
            JsonNotAny actual = this.scenarioContext.Get<JsonNotAny>(ArrayValueResultkey);
            Assert.AreEqual(JsonAny.ParseUriValue(expected), actual);
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to add.</param>
        [When(@"I add the item (.*) to the JsonAny")]
        public void WhenIAddTheItemToTheJsonAny(string value)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        [When(@"I add the 2 items (.*) and (.*) to the JsonAny")]
        public void WhenIAddTheItemsToTheJsonAny(string value1, string value2)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        [When(@"I add the 3 items (.*), (.*) and (.*) to the JsonAny")]
        public void WhenIAddTheItemsToTheJsonAny(string value1, string value2, string value3)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        [When(@"I add the 4 items (.*), (.*), (.*) and (.*) to the JsonAny")]
        public void WhenIAddTheItemsToTheJsonAny(string value1, string value2, string value3, string value4)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        /// <param name="value5">The fifth serialized value to add.</param>
        [When(@"I add the 5 items (.*), (.*), (.*), (.*) and (.*) to the JsonAny")]
        public void WhenIAddTheItemsToTheJsonAny(string value1, string value2, string value3, string value4, string value5)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4), JsonAny.ParseUriValue(value5)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="values">The serialized value to add.</param>
        [When(@"I add the range (.*) to the JsonAny")]
        public void WhenIAddTheRangeToTheJsonAny(string values)
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.AddRange(JsonAny.ParseUriValue(values).AsArray.AsItemsList), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to add.</param>
        [When(@"I add the item (.*) to the JsonArray")]
        public void WhenIAddTheItemToTheJsonArray(string value)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        [When(@"I add the 2 items (.*) and (.*) to the JsonArray")]
        public void WhenIAddTheItemsToTheJsonArray(string value1, string value2)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        [When(@"I add the 3 items (.*), (.*) and (.*) to the JsonArray")]
        public void WhenIAddTheItemsToTheJsonArray(string value1, string value2, string value3)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        [When(@"I add the 4 items (.*), (.*), (.*) and (.*) to the JsonArray")]
        public void WhenIAddTheItemsToTheJsonArray(string value1, string value2, string value3, string value4)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        /// <param name="value5">The fifth serialized value to add.</param>
        [When(@"I add the 5 items (.*), (.*), (.*), (.*) and (.*) to the JsonArray")]
        public void WhenIAddTheItemsToTheJsonArray(string value1, string value2, string value3, string value4, string value5)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4), JsonAny.ParseUriValue(value5)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="values">The serialized value to add.</param>
        [When(@"I add the range (.*) to the JsonArray")]
        public void WhenIAddTheRangeToTheJsonArray(string values)
        {
            JsonArray sut = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.AddRange(JsonAny.ParseUriValue(values).AsArray.AsItemsList), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonArray"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value">The serialized value to add.</param>
        [When(@"I add the item (.*) to the JsonNotAny")]
        public void WhenIAddTheItemToTheJsonNotAny(string value)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        [When(@"I add the 2 items (.*) and (.*) to the JsonNotAny")]
        public void WhenIAddTheItemsToTheJsonNotAny(string value1, string value2)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        [When(@"I add the 3 items (.*), (.*) and (.*) to the JsonNotAny")]
        public void WhenIAddTheItemsToTheJsonNotAny(string value1, string value2, string value3)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        [When(@"I add the 4 items (.*), (.*), (.*) and (.*) to the JsonNotAny")]
        public void WhenIAddTheItemsToTheJsonNotAny(string value1, string value2, string value3, string value4)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given values to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="value1">The first serialized value to add.</param>
        /// <param name="value2">The second serialized value to add.</param>
        /// <param name="value3">The third serialized value to add.</param>
        /// <param name="value4">The fourth serialized value to add.</param>
        /// <param name="value5">The fifth serialized value to add.</param>
        [When(@"I add the 5 items (.*), (.*), (.*), (.*) and (.*) to the JsonNotAny")]
        public void WhenIAddTheItemsToTheJsonNotAny(string value1, string value2, string value3, string value4, string value5)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.Add(JsonAny.ParseUriValue(value1), JsonAny.ParseUriValue(value2), JsonAny.ParseUriValue(value3), JsonAny.ParseUriValue(value4), JsonAny.ParseUriValue(value5)), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }

        /// <summary>
        /// Gets the <see cref="JsonNotAny"/> from the context with key <see cref="JsonValueSteps.SubjectUnderTest"/>
        /// adds the given value to it, and stores it in the <see cref="ArrayValueResultkey"/>.
        /// </summary>
        /// <param name="values">The serialized value to add.</param>
        [When(@"I add the range (.*) to the JsonNotAny")]
        public void WhenIAddTheRangeToTheJsonNotAny(string values)
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest);
            try
            {
                this.scenarioContext.Set(sut.AddRange(JsonAny.ParseUriValue(values).AsArray.AsItemsList), ArrayValueResultkey);
            }
            catch (Exception ex)
            {
                this.scenarioContext.Set(ex, ArrayExceptionKey);
            }
        }
    }
}
