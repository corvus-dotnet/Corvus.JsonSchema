// <copyright file="JsonPropertiesSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Steps
{
    using System;
    using System.Text;
    using Corvus.Json;
    using NUnit.Framework;
    using TechTalk.SpecFlow;

    /// <summary>
    /// Steps for Json value types.
    /// </summary>
    [Binding]
    public class JsonPropertiesSteps
    {
        private const string ObjectResult = "ObjectResult";
        private const string PropertyValueResult = "PropertyValueResult";
        private const string PropertyExistsResult = "PropertyExistsResult";

        private readonly ScenarioContext scenarioContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="JsonPropertiesSteps"/> class.
        /// </summary>
        /// <param name="scenarioContext">The scenario context.</param>
        public JsonPropertiesSteps(ScenarioContext scenarioContext)
        {
            this.scenarioContext = scenarioContext;
        }

        /// <summary>
        /// Set a property to an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonObject using a string")]
        public void WhenISetThePropertyNamedWithValueToTheJsonObject(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonAny using a string")]
        public void WhenISetThePropertyNamedWithValueToTheJsonAny(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonNotAny using a string")]
        public void WhenISetThePropertyNamedWithValueToTheJsonNotAny(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonObject using a ReadOnlySpan<char>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonObjectUsingAReadOnlySpanChar(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonAny using a ReadOnlySpan<char>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonAnyUsingAReadOnlySpanChar(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonNotAny using a ReadOnlySpan<char>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonNotAnyUsingAReadOnlySpanChar(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonObject using a ReadOnlySpan<byte>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonObjectUsingAReadOnlySpanByte(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonAny using a ReadOnlySpan<byte>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonAnyUsingAReadOnlySpanByte(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [When(@"I set the property (.*) to the value (.*) on the JsonNotAny using a ReadOnlySpan<byte>")]
        public void WhenISetThePropertyNamedWithValueToTheJsonNotAnyUsingAReadOnlySpanByte(string propertyName, string value)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonAny.ParseUriValue(value)), ObjectResult);
        }

        /// <summary>
        /// Validate the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [Then(@"the property (.*) should be (.*) using string")]
        public void ThenThePropertyNamedShouldBe(string propertyName, string value)
        {
            if (value == "<undefined>")
            {
                Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(propertyName, out _));
            }
            else
            {
                Assert.IsTrue(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(propertyName, out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }

        /// <summary>
        /// Validate the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [Then(@"the property (.*) should be (.*) using ReadOnlySpan<char>")]
        public void ThenThePropertyNamedShouldBeUsingReadOnlySpanOfChar(string propertyName, string value)
        {
            if (value == "<undefined>")
            {
                Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out _));
            }
            else
            {
                Assert.IsTrue(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }

        /// <summary>
        /// Validate the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        /// <param name="value">The serialized value of the property.</param>
        [Then(@"the property (.*) should be (.*) using ReadOnlySpan<byte>")]
        public void ThenThePropertyNamedShouldBeUsingReadOnlySpanOfByte(string propertyName, string value)
        {
            if (value == "<undefined>")
            {
                Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out _));
            }
            else
            {
                Assert.IsTrue(this.scenarioContext.Get<IJsonObject>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonObject using a string")]
        public void WhenIRemoveThePropertyNamedFromTheJsonObject(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonAny using a string")]
        public void WhenIRemoveThePropertyNamedFromTheJsonAny(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonNotAny using a string")]
        public void WhenIRemoveThePropertyNamedFromTheJsonNotAny(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonObject using a ReadOnlySpan<char>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonObjectUsingAReadOnlySpanOfChar(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonAny using a ReadOnlySpan<char>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonAnyUsingAReadOnlySpanOfChar(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonNotAny using a ReadOnlySpan<char>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonNotAnyUsingAReadOnlySpanOfChar(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonObject using a ReadOnlySpan<byte>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonObjectUsingAReadOnlySpanOfByte(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonAny using a ReadOnlySpan<byte>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonAnyUsingAReadOnlySpanOfByte(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }

        /// <summary>
        /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [When(@"I remove the property (.*) from the JsonNotAny using a ReadOnlySpan<byte>")]
        public void WhenIRemoveThePropertyNamedFromTheJsonNotAnyUsingAReadOnlySpanOfByte(string propertyName)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }

        /// <summary>
        /// Validate that the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [Then(@"the property (.*) should not be defined using string")]
        public void ThenThePropertyNamedShouldNotBeDefined(string propertyName)
        {
            Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).HasProperty(propertyName));
        }

        /// <summary>
        /// Validate that the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [Then(@"the property (.*) should not be defined using ReadOnlySpan<char>")]
        public void ThenThePropertyNamedShouldNotBeDefinedUsingReadOnlySpanOfChar(string propertyName)
        {
            Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).HasProperty(propertyName.AsSpan()));
        }

        /// <summary>
        /// Validate that the given property on a <see cref="IJsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
        /// </summary>
        /// <param name="propertyName">The name of the property.</param>
        [Then(@"the property (.*) should not be defined using ReadOnlySpan<byte>")]
        public void ThenThePropertyNamedShouldNotBeDefinedUsingReadOnlySpanOfByte(string propertyName)
        {
            Assert.IsFalse(this.scenarioContext.Get<IJsonObject>(ObjectResult).HasProperty(Encoding.UTF8.GetBytes(propertyName)));
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I try to get the property (.*) using string")]
        public void WhenITryToGetThePropertyFooUsingString(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName, out JsonAny actualValue);
            this.scenarioContext.Set(canGet, PropertyExistsResult);
            if (canGet)
            {
                this.scenarioContext.Set(actualValue, PropertyValueResult);
            }
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I try to get the property (.*) using ReadOnlySpan<char>")]
        public void WhenITryToGetThePropertyFooUsingReadOnlySpanOfChar(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue);
            this.scenarioContext.Set(canGet, PropertyExistsResult);
            if (canGet)
            {
                this.scenarioContext.Set(actualValue, PropertyValueResult);
            }
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I try to get the property (.*) using ReadOnlySpan<byte>")]
        public void WhenITryToGetThePropertyFooUsingReadOnlySpanOfByte(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue);
            this.scenarioContext.Set(canGet, PropertyExistsResult);
            if (canGet)
            {
                this.scenarioContext.Set(actualValue, PropertyValueResult);
            }
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I check the existence of the property (.*) using string")]
        public void WhenICheckTheExistenceOfThePropertyFooUsingString(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName);
            this.scenarioContext.Set(canGet, PropertyExistsResult);
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I check the existence of the property (.*) using ReadOnlySpan<char>")]
        public void WhenICheckTheExistenceOfThePropertyFooUsingReadOnlySpanOfChar(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName.AsSpan());
            this.scenarioContext.Set(canGet, PropertyExistsResult);
        }

        /// <summary>
        /// Tries to get the value on the <see cref="IJsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
        /// </summary>
        /// <param name="propertyName">The name of the property to check.</param>
        [When(@"I check the existence of the property (.*) using ReadOnlySpan<byte>")]
        public void WhenICheckTheExistenceOfThePropertyFooUsingReadOnlySpanOfByte(string propertyName)
        {
            bool canGet = this.scenarioContext.Get<IJsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(Encoding.UTF8.GetBytes(propertyName));
            this.scenarioContext.Set(canGet, PropertyExistsResult);
        }

        /// <summary>
        /// Validates that the value stored in the context with key <see cref="PropertyExistsResult"/> is true.
        /// </summary>
        [Then(@"the property should be found")]
        public void ThenThePropertyShouldBeFound()
        {
            Assert.IsTrue(this.scenarioContext.Get<bool>(PropertyExistsResult));
        }

        /// <summary>
        /// Validates that the value stored in the context with key <see cref="PropertyExistsResult"/> is false.
        /// </summary>
        [Then(@"the property should not be found")]
        public void ThenThePropertyShouldNotBeFound()
        {
            Assert.IsFalse(this.scenarioContext.Get<bool>(PropertyExistsResult));
        }

        /// <summary>
        /// Validates that the value stored in the context with key <see cref="PropertyValueResult"/> matches the given value.
        /// </summary>
        /// <param name="value">The serialized JSON value to match.</param>
        [Then(@"the property value should be (.*)")]
        public void ThenThePropertyValueShouldBe(string value)
        {
            if (value != "undefined")
            {
                Assert.IsTrue(this.scenarioContext.ContainsKey(PropertyValueResult));
                Assert.AreEqual(JsonAny.ParseUriValue(value), this.scenarioContext.Get<JsonAny>(PropertyValueResult));
            }
            else
            {
                Assert.IsFalse(this.scenarioContext.ContainsKey(PropertyValueResult));
            }
        }
    }
}
