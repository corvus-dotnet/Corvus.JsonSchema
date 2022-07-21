// <copyright file="JsonPropertiesSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;
using TechTalk.SpecFlow.CommonModels;

namespace Steps;

/// <summary>
/// Steps for Json value types.
/// </summary>
[Binding]
public class JsonPropertiesSteps
{
    private const string ObjectResult = "ObjectResult";
    private const string ExceptionResult = "ExceptionResult";
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
    [When("I set the property (.*) to the value (.*) on the JsonObject using a string")]
    public void WhenISetThePropertyNamedWithValueToTheJsonObject(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonAny using a string")]
    public void WhenISetThePropertyNamedWithValueToTheJsonAny(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonNotAny using a string")]
    public void WhenISetThePropertyNamedWithValueToTheJsonNotAny(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName, JsonNotAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonObject using a ReadOnlySpan<char>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonObjectUsingAReadOnlySpanChar(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonAny using a ReadOnlySpan<char>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonAnyUsingAReadOnlySpanChar(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonNotAny using a ReadOnlySpan<char>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonNotAnyUsingAReadOnlySpanChar(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(propertyName.AsSpan(), JsonNotAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonObject using a ReadOnlySpan<byte>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonObjectUsingAReadOnlySpanByte(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonAny using a ReadOnlySpan<byte>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonAnyUsingAReadOnlySpanByte(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Set a property to an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [When("I set the property (.*) to the value (.*) on the JsonNotAny using a ReadOnlySpan<byte>")]
    public void WhenISetThePropertyNamedWithValueToTheJsonNotAnyUsingAReadOnlySpanByte(string propertyName, string value)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).SetProperty(Encoding.UTF8.GetBytes(propertyName), JsonNotAny.ParseUriValue(value)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonAny should be (.*) using string")]
    public void ThenThePropertyNamedOnTheJsonAnyShouldBe(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(propertyName, out _));
        }
        else
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(propertyName, out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonAny should be (.*) using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedOnTheJsonAnyShouldBeUsingReadOnlySpanOfChar(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out _));
        }
        else
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonAny should be (.*) using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedOnTheJsonAnyShouldBeUsingReadOnlySpanOfByte(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out _));
        }
        else
        {
            JsonAny sut = this.scenarioContext.Get<JsonAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonAny>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonNotAny should be (.*) using string")]
    public void ThenThePropertyNamedOnTheJsonNotAnyShouldBe(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(propertyName, out _));
        }
        else
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(propertyName, out JsonNotAny actualValue));
                Assert.AreEqual(JsonNotAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonNotAny should be (.*) using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedOnTheJsonNotAnyShouldBeUsingReadOnlySpanOfChar(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out _));
        }
        else
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out JsonNotAny actualValue));
                Assert.AreEqual(JsonNotAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonNotAny should be (.*) using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedOnTheJsonNotAnyShouldBeUsingReadOnlySpanOfByte(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out _));
        }
        else
        {
            JsonNotAny sut = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonNotAny>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonNotAny actualValue));
                Assert.AreEqual(JsonNotAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonObject should be (.*) using string")]
    public void ThenThePropertyNamedOnTheJsonObjectShouldBe(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonObject>(ObjectResult).TryGetProperty(propertyName, out _));
        }
        else
        {
            JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(sut.TryGetProperty(propertyName, out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonObject should be (.*) using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedOnTheJsonObjectShouldBeUsingReadOnlySpanOfChar(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonObject>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out _));
        }
        else
        {
            JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonObject>(ObjectResult).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Validate the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    /// <param name="value">The serialized value of the property.</param>
    [Then("the property (.*) on the JsonObject should be (.*) using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedOnTheJsonObjectShouldBeUsingReadOnlySpanOfByte(string propertyName, string value)
    {
        if (value == "<undefined>")
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonObject>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out _));
        }
        else
        {
            JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
            if (sut.IsNotUndefined())
            {
                Assert.IsTrue(this.scenarioContext.Get<JsonObject>(ObjectResult).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue));
                Assert.AreEqual(JsonAny.ParseUriValue(value), actualValue);
            }
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonObject using a string")]
    public void WhenIRemoveThePropertyNamedFromTheJsonObject(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonAny using a string")]
    public void WhenIRemoveThePropertyNamedFromTheJsonAny(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonNotAny using a string")]
    public void WhenIRemoveThePropertyNamedFromTheJsonNotAny(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonObject using a ReadOnlySpan<char>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonObjectUsingAReadOnlySpanOfChar(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonAny using a ReadOnlySpan<char>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonAnyUsingAReadOnlySpanOfChar(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonNotAny using a ReadOnlySpan<char>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonNotAnyUsingAReadOnlySpanOfChar(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(propertyName.AsSpan()), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonObject"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonObject using a ReadOnlySpan<byte>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonObjectUsingAReadOnlySpanOfByte(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

        /// <summary>
    /// Remove a property from an <see cref="JsonAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonAny using a ReadOnlySpan<byte>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonAnyUsingAReadOnlySpanOfByte(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonAny using string")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonAny(string propertyName)
    {
        JsonAny result = this.scenarioContext.Get<JsonAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(propertyName));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonAny using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        JsonAny result = this.scenarioContext.Get<JsonAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(propertyName.AsSpan()));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonAny using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        JsonAny result = this.scenarioContext.Get<JsonAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(Encoding.UTF8.GetBytes(propertyName)));
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonAny using string")]
    public void WhenITryToGetThePropertyOnTheJsonAnyUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName, out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonAny using ReadOnlySpan<char>")]
    public void WhenITryToGetThePropertyOnTheJsonAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonAny using ReadOnlySpan<byte>")]
    public void WhenITryToGetThePropertyOnTheJsonAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonAny using string")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonAnyUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Remove a property from an <see cref="JsonNotAny"/> found in <see cref="JsonValueSteps.SubjectUnderTest"/> and store it in <see cref="ObjectResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [When("I remove the property (.*) from the JsonNotAny using a ReadOnlySpan<byte>")]
    public void WhenIRemoveThePropertyNamedFromTheJsonNotAnyUsingAReadOnlySpanOfByte(string propertyName)
    {
        try
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).RemoveProperty(Encoding.UTF8.GetBytes(propertyName)), ObjectResult);
        }
        catch (Exception ex)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest), ObjectResult);
            this.scenarioContext.Set(ex, ExceptionResult);
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonNotAny using string")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonNotAny(string propertyName)
    {
        JsonNotAny result = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(propertyName));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonNotAny using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonNotAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        JsonNotAny result = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(propertyName.AsSpan()));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonNotAny"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonNotAny using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonNotAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        JsonNotAny result = this.scenarioContext.Get<JsonNotAny>(ObjectResult);
        if (result.ValueKind == JsonValueKind.Object)
        {
            Assert.IsFalse(result.HasProperty(Encoding.UTF8.GetBytes(propertyName)));
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonNotAny using string")]
    public void WhenITryToGetThePropertyOnTheJsonNotAnyUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName, out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonNotAny using ReadOnlySpan<char>")]
    public void WhenITryToGetThePropertyOnTheJsonNotAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonNotAny using ReadOnlySpan<byte>")]
    public void WhenITryToGetThePropertyOnTheJsonNotAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonNotAny using string")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonNotAnyUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonObject using string")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonObject(string propertyName)
    {
        JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
        if (sut.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            Assert.IsFalse(sut.HasProperty(propertyName));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonObject using ReadOnlySpan<char>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonObjectUsingReadOnlySpanOfChar(string propertyName)
    {
        JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
        if (sut.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonObject>(ObjectResult).HasProperty(propertyName.AsSpan()));
        }
    }

    /// <summary>
    /// Validate that the given property on a <see cref="JsonObject"/> found in the scenario context with key <see cref="ObjectResult"/> has not been defined.
    /// </summary>
    /// <param name="propertyName">The name of the property.</param>
    [Then("the property (.*) should not be defined on the JsonObject using ReadOnlySpan<byte>")]
    public void ThenThePropertyNamedShouldNotBeDefinedOnTheJsonObjectUsingReadOnlySpanOfByte(string propertyName)
    {
        JsonObject sut = this.scenarioContext.Get<JsonObject>(ObjectResult);
        if (sut.ValueKind == System.Text.Json.JsonValueKind.Object)
        {
            Assert.IsFalse(this.scenarioContext.Get<JsonObject>(ObjectResult).HasProperty(Encoding.UTF8.GetBytes(propertyName)));
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonObject using string")]
    public void WhenITryToGetThePropertyOnTheJsonObjectUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName, out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonObject using ReadOnlySpan<char>")]
    public void WhenITryToGetThePropertyOnTheJsonObjectUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(propertyName.AsSpan(), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I try to get the property (.*) on the JsonObject using ReadOnlySpan<byte>")]
    public void WhenITryToGetThePropertyOnTheJsonObjectUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).TryGetProperty(Encoding.UTF8.GetBytes(propertyName), out JsonAny actualValue);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
        if (canGet)
        {
            this.scenarioContext.Set(actualValue, PropertyValueResult);
        }
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonObject using string")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonObjectUsingString(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName);
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonObject using ReadOnlySpan<char>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonObjectUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName.AsSpan());
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonAny using ReadOnlySpan<char>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName.AsSpan());
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonNotAny using ReadOnlySpan<char>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonNotAnyUsingReadOnlySpanOfChar(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).HasProperty(propertyName.AsSpan());
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonObject"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonObject using ReadOnlySpan<byte>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonObjectUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).HasProperty(Encoding.UTF8.GetBytes(propertyName));
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonAny using ReadOnlySpan<byte>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).HasProperty(Encoding.UTF8.GetBytes(propertyName));
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Tries to get the value on the <see cref="JsonNotAny"/> stored in the context with the key <see cref="JsonValueSteps.SubjectUnderTest"/> and stores the result in the context keys <see cref="PropertyValueResult"/> ahd <see cref="PropertyExistsResult"/>.
    /// </summary>
    /// <param name="propertyName">The name of the property to check.</param>
    [When("I check the existence of the property (.*) on the JsonNotAny using ReadOnlySpan<byte>")]
    public void WhenICheckTheExistenceOfThePropertyOnTheJsonNotAnyUsingReadOnlySpanOfByte(string propertyName)
    {
        bool canGet = this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).HasProperty(Encoding.UTF8.GetBytes(propertyName));
        this.scenarioContext.Set(canGet, PropertyExistsResult);
    }

    /// <summary>
    /// Validates that the value stored in the context with key <see cref="PropertyExistsResult"/> is true.
    /// </summary>
    [Then("the property should be found")]
    public void ThenThePropertyShouldBeFound()
    {
        Assert.IsTrue(this.scenarioContext.Get<bool>(PropertyExistsResult));
    }

    /// <summary>
    /// Validates that the value stored in the context with key <see cref="PropertyExistsResult"/> is false.
    /// </summary>
    [Then("the property should not be found")]
    public void ThenThePropertyShouldNotBeFound()
    {
        Assert.IsFalse(this.scenarioContext.Get<bool>(PropertyExistsResult));
    }

    [Then("the operation should produce no exception")]
    public void ThenTheOperationShouldProduceNoException()
    {
        if (this.scenarioContext.ContainsKey(ExceptionResult))
        {
            Assert.IsNull(this.scenarioContext.Get<Exception>(ExceptionResult));
        }
    }

    [Then("the operation should produce an InvalidOperationException")]
    public void ThenTheOperationShouldProduceAnInvalidOperationException()
    {
        Exception? ex = null;
        if (this.scenarioContext.ContainsKey(ExceptionResult))
        {
            ex = this.scenarioContext.Get<Exception>(ExceptionResult);
        }

        Assert.IsInstanceOf<InvalidOperationException>(ex);
    }

    /// <summary>
    /// Validates that the value stored in the context with key <see cref="PropertyValueResult"/> matches the given value.
    /// </summary>
    /// <param name="value">The serialized JSON value to match.</param>
    [Then("the property value should be (.*)")]
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