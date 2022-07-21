// <copyright file="JsonValueEqualitySteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for Json value types.
/// </summary>
[Binding]
public class JsonValueEqualitySteps
{
    private const string EqualsResultKey = "EqualsResult";
    private const string EqualsObjectBackedResultKey = "EqualsObjectBackedResult";
    private const string EqualityResultKey = "EqualityResult";
    private const string InequalityResultKey = "InequalityResult";
    private const string HashCodeResultKey = "HashCodeResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonValueEqualitySteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonValueEqualitySteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /* string */

    /// <summary>
    /// Compares the value in JsonString in the context variable <c>Value</c> with the expected string, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the string (.*)")]
    public void WhenICompareItToTheString(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest).Equals(new JsonString((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest).Equals((JsonString)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest) == (JsonString)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest) != (JsonString)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonString)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonString in the context variable <c>Value</c> with the expected string, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the string to the IJsonValue (.*)")]
    public void WhenICompareTheStringToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonString in the context variable <c>Value</c> with the expected string, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the string to the object (.*)")]
    public void WhenICompareTheStringToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? JsonString.Undefined : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonString>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* boolean */

    /// <summary>
    /// Compares the value in JsonBoolean in the context variable <c>Value</c> with the expected boolean, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the boolean (.*)")]
    public void WhenICompareItToTheBoolean(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest).Equals(new JsonBoolean((bool)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest).Equals((JsonBoolean)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest) == (JsonBoolean)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest) != (JsonBoolean)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonBoolean)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBoolean in the context variable <c>Value</c> with the expected boolean, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the boolean to the IJsonValue (.*)")]
    public void WhenICompareTheBooleanToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBoolean in the context variable <c>Value</c> with the expected boolean, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the boolean to the object (.*)")]
    public void WhenICompareTheBooleanToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonBoolean) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonBoolean>(JsonValueSteps.SubjectUnderTest).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* any */

    /// <summary>
    /// Compares the value in JsonAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the any (.*)")]
    public void WhenICompareItToTheAny(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected).AsDotnetBackedValue()), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals((JsonAny)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest) == (JsonAny)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest) != (JsonAny)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonAny)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the any to the IJsonValue (.*)")]
    public void WhenICompareTheAnyToTheIJsonValue(string expected)
    {
        var value = JsonAny.ParseUriValue(expected);
        JsonValueKind valueKind = value.ValueKind;

        switch (valueKind)
        {
            case JsonValueKind.Object:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsObject), EqualsResultKey);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsBoolean), EqualsResultKey);
                break;
            case JsonValueKind.Number:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsNumber), EqualsResultKey);
                break;
            case JsonValueKind.Null:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value), EqualsResultKey);
                break;
            case JsonValueKind.Undefined:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value), EqualsResultKey);
                break;
            case JsonValueKind.String:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsString), EqualsResultKey);
                break;
            case JsonValueKind.Array:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsArray), EqualsResultKey);
                break;
        }
    }

    /// <summary>
    /// Compares the value in JsonAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the any to the object (.*)")]
    public void WhenICompareTheAnyToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonAny) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonAny>(JsonValueSteps.SubjectUnderTest).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* notAny */

    /// <summary>
    /// Compares the value in JsonNotAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the notAny (.*)")]
    public void WhenICompareItToTheNotAny(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(JsonNotAny.ParseUriValue(expected).AsDotnetBackedValue()), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(JsonNotAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest) == JsonNotAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest) != JsonNotAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).GetHashCode() == JsonNotAny.ParseUriValue(expected).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonNotAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the notAny to the IJsonValue (.*)")]
    public void WhenICompareTheNotAnyToTheIJsonValue(string expected)
    {
        var value = JsonAny.ParseUriValue(expected);
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsObject), EqualsResultKey);
                break;
            case JsonValueKind.True:
            case JsonValueKind.False:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsBoolean), EqualsResultKey);
                break;
            case JsonValueKind.Number:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsNumber), EqualsResultKey);
                break;
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value), EqualsResultKey);
                break;
            case JsonValueKind.String:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsString), EqualsResultKey);
                break;
            case JsonValueKind.Array:
                this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).Equals(value.AsArray), EqualsResultKey);
                break;
        }
    }

    /// <summary>
    /// Compares the value in JsonNotAny in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the notAny to the object (.*)")]
    public void WhenICompareTheNotAnyToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonNotAny) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNotAny>(JsonValueSteps.SubjectUnderTest).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* array */

    /// <summary>
    /// Compares the value in JsonArray in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the array (.*)")]
    public void WhenICompareItToTheArray(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected).AsArray.AsDotnetBackedValue()), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).Equals((JsonArray)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest) == (JsonArray)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest) != (JsonArray)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonArray)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonArray in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the array to the IJsonValue (.*)")]
    public void WhenICompareTheArrayToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonArray in the context variable <c>Value</c> with the expected array, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the array to the object (.*)")]
    public void WhenICompareTheArrayToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? JsonArray.Undefined : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* base64content */

    /// <summary>
    /// Compares the value in JsonBase64Content in the context variable <c>Value</c> with the expected base64Content, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the base64content (.*)")]
    public void WhenICompareItToTheBase64Content(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest).Equals(new JsonBase64Content((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest).Equals((JsonBase64Content)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest) == (JsonBase64Content)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest) != (JsonBase64Content)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonBase64Content)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBase64Content in the context variable <c>Value</c> with the expected base64Content, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the base64content to the IJsonValue (.*)")]
    public void WhenICompareTheBase64ContentToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBase64Content in the context variable <c>Value</c> with the expected base64Content, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the base64content to the object (.*)")]
    public void WhenICompareTheBase64ContentToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonBase64Content) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonBase64Content>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* base64string */

    /// <summary>
    /// Compares the value in JsonBase64String in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the base64string (.*)")]
    public void WhenICompareItToTheBase64String(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest).Equals(new JsonBase64String((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest).Equals((JsonBase64String)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest) == (JsonBase64String)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest) != (JsonBase64String)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonBase64String)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBase64String in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the base64string to the IJsonValue (.*)")]
    public void WhenICompareTheBase64StringToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonBase64String in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the base64string to the object (.*)")]
    public void WhenICompareTheBase64StringToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonBase64String) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonBase64String>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* content */

    /// <summary>
    /// Compares the value in JsonContent in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the content (.*)")]
    public void WhenICompareItToTheContent(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest).Equals(new JsonContent((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest).Equals((JsonContent)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest) == (JsonContent)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest) != (JsonContent)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonContent)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonContent in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the content to the IJsonValue (.*)")]
    public void WhenICompareTheContentToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonContent in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the content to the object (.*)")]
    public void WhenICompareTheContentToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonContent) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonContent>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* date */

    /// <summary>
    /// Compares the value in JsonDate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the date (.*)")]
    public void WhenICompareItToTheDate(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest).Equals(new JsonDate((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest).Equals((JsonDate)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest) == (JsonDate)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest) != (JsonDate)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonDate)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the date to the IJsonValue (.*)")]
    public void WhenICompareTheDateToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the date to the object (.*)")]
    public void WhenICompareTheDateToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonDate) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDate>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* dateTime */

    /// <summary>
    /// Compares the value in JsonDateTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the dateTime (.*)")]
    public void WhenICompareItToTheDateTime(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest).Equals(new JsonDateTime((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest).Equals((JsonDateTime)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest) == (JsonDateTime)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest) != (JsonDateTime)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonDateTime)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDateTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the dateTime to the IJsonValue (.*)")]
    public void WhenICompareTheDateTimeToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDateTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the dateTime to the object (.*)")]
    public void WhenICompareTheDateTimeToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonDateTime) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDateTime>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* duration */

    /// <summary>
    /// Compares the value in JsonDuration in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the duration (.*)")]
    public void WhenICompareItToTheDuration(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest).Equals(new JsonDuration((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest).Equals((JsonDuration)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest) == (JsonDuration)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest) != (JsonDuration)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonDuration)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDuration in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the duration to the IJsonValue (.*)")]
    public void WhenICompareTheDurationToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonDuration in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the duration to the object (.*)")]
    public void WhenICompareTheDurationToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonDuration) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonDuration>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* email */

    /// <summary>
    /// Compares the value in JsonEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the email (.*)")]
    public void WhenICompareItToTheEmail(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest).Equals(new JsonEmail((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest).Equals((JsonEmail)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest) == (JsonEmail)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest) != (JsonEmail)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonEmail)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the email to the IJsonValue (.*)")]
    public void WhenICompareTheEmailToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the email to the object (.*)")]
    public void WhenICompareTheEmailToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonEmail) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonEmail>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* idnEmail */

    /// <summary>
    /// Compares the value in JsonIdnEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the idnEmail (.*)")]
    public void WhenICompareItToTheIdnEmail(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIdnEmail((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest).Equals((JsonIdnEmail)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest) == (JsonIdnEmail)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest) != (JsonIdnEmail)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIdnEmail)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIdnEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the idnEmail to the IJsonValue (.*)")]
    public void WhenICompareTheIdnEmailToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIdnEmail in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the idnEmail to the object (.*)")]
    public void WhenICompareTheIdnEmailToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIdnEmail) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIdnEmail>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* hostname */

    /// <summary>
    /// Compares the value in JsonHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the hostname (.*)")]
    public void WhenICompareItToTheHostname(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest).Equals(new JsonHostname((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest).Equals((JsonHostname)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest) == (JsonHostname)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest) != (JsonHostname)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonHostname)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the hostname to the IJsonValue (.*)")]
    public void WhenICompareTheHostnameToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the hostname to the object (.*)")]
    public void WhenICompareTheHostnameToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonHostname) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonHostname>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* idnHostname */

    /// <summary>
    /// Compares the value in JsonIdnHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the idnHostname (.*)")]
    public void WhenICompareItToTheIdnHostname(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIdnHostname((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest).Equals((JsonIdnHostname)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest) == (JsonIdnHostname)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest) != (JsonIdnHostname)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIdnHostname)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIdnHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the idnHostname to the IJsonValue (.*)")]
    public void WhenICompareTheIdnHostnameToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIdnHostname in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the idnHostname to the object (.*)")]
    public void WhenICompareTheIdnHostnameToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIdnHostname) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIdnHostname>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* integer */

    /// <summary>
    /// Compares the value in JsonInteger in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the integer (.*)")]
    public void WhenICompareItToTheInteger(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest).Equals(new JsonInteger((long)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest).Equals((JsonInteger)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest) == (JsonInteger)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest) != (JsonInteger)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonInteger)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonInteger in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the integer to the IJsonValue (.*)")]
    public void WhenICompareTheIntegerToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonInteger in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the integer to the object (.*)")]
    public void WhenICompareTheIntegerToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonInteger) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonInteger>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* number */

    /// <summary>
    /// Compares the value in JsonNumber in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the number (.*)")]
    public void WhenICompareItToTheNumber(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest).Equals(new JsonNumber((double)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest).Equals((JsonNumber)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest) == (JsonNumber)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest) != (JsonNumber)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonNumber)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonNumber in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the number to the IJsonValue (.*)")]
    public void WhenICompareTheNumberToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonNumber in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the number to the object (.*)")]
    public void WhenICompareTheNumberToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? JsonNumber.Undefined : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonNumber>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* ipV4 */

    /// <summary>
    /// Compares the value in JsonIpV4 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the ipV4 (.*)")]
    public void WhenICompareItToTheIpV4(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIpV4((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest).Equals((JsonIpV4)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest) == (JsonIpV4)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest) != (JsonIpV4)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIpV4)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIpV4 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the ipV4 to the IJsonValue (.*)")]
    public void WhenICompareTheIpV4ToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIpV4 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the ipV4 to the object (.*)")]
    public void WhenICompareTheIpV4ToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIpV4) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIpV4>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* ipV6 */

    /// <summary>
    /// Compares the value in JsonIpV6 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the ipV6 (.*)")]
    public void WhenICompareItToTheIpV6(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIpV6((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest).Equals((JsonIpV6)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest) == (JsonIpV6)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest) != (JsonIpV6)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIpV6)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIpV6 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the ipV6 to the IJsonValue (.*)")]
    public void WhenICompareTheIpV6ToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIpV6 in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the ipV6 to the object (.*)")]
    public void WhenICompareTheIpV6ToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIpV6) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIpV6>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* uri */

    /// <summary>
    /// Compares the value in JsonUri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the uri (.*)")]
    public void WhenICompareItToTheUri(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest).Equals(new JsonUri((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest).Equals((JsonUri)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest) == (JsonUri)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest) != (JsonUri)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonUri)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uri to the IJsonValue (.*)")]
    public void WhenICompareTheUriToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uri to the object (.*)")]
    public void WhenICompareTheUriToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonUri) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUri>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* uriReference */

    /// <summary>
    /// Compares the value in JsonUriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the uriReference (.*)")]
    public void WhenICompareItToTheUriReference(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest).Equals(new JsonUriReference((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest).Equals((JsonUriReference)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest) == (JsonUriReference)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest) != (JsonUriReference)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonUriReference)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uriReference to the IJsonValue (.*)")]
    public void WhenICompareTheUriReferenceToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uriReference to the object (.*)")]
    public void WhenICompareTheUriReferenceToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonUriReference) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUriReference>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* iri */

    /// <summary>
    /// Compares the value in JsonIri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the iri (.*)")]
    public void WhenICompareItToTheIri(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIri((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest).Equals((JsonIri)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest) == (JsonIri)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest) != (JsonIri)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIri)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the iri to the IJsonValue (.*)")]
    public void WhenICompareTheIriToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIri in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the iri to the object (.*)")]
    public void WhenICompareTheIriToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIri) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIri>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* iriReference */

    /// <summary>
    /// Compares the value in JsonIriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the iriReference (.*)")]
    public void WhenICompareItToTheIriReference(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest).Equals(new JsonIriReference((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest).Equals((JsonIriReference)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest) == (JsonIriReference)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest) != (JsonIriReference)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonIriReference)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the iriReference to the IJsonValue (.*)")]
    public void WhenICompareTheIriReferenceToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonIriReference in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the iriReference to the object (.*)")]
    public void WhenICompareTheIriReferenceToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonIriReference) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonIriReference>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* object */

    /// <summary>
    /// Compares the value in JsonObject in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the object (.*)")]
    public void WhenICompareItToTheObject(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).Equals(new JsonObject(((JsonObject)JsonAny.ParseUriValue(expected)).AsImmutableDictionary())), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).Equals((JsonObject)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest) == (JsonObject)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest) != (JsonObject)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonObject)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonObject in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the object to the IJsonValue (.*)")]
    public void WhenICompareTheObjectToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonObject in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the object to the object (.*)")]
    public void WhenICompareTheObjectToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? JsonObject.Undefined : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonObject>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* pointer */

    /// <summary>
    /// Compares the value in JsonPointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the pointer (.*)")]
    public void WhenICompareItToThePointer(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest).Equals(new JsonPointer((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest).Equals((JsonPointer)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest) == (JsonPointer)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest) != (JsonPointer)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonPointer)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonPointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the pointer to the IJsonValue (.*)")]
    public void WhenICompareThePointerToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonPointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the pointer to the object (.*)")]
    public void WhenICompareThePointerToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonPointer) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonPointer>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* relativePointer */

    /// <summary>
    /// Compares the value in JsonRelativePointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the relativePointer (.*)")]
    public void WhenICompareItToTheRelativePointer(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest).Equals(new JsonRelativePointer((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest).Equals((JsonRelativePointer)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest) == (JsonRelativePointer)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest) != (JsonRelativePointer)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonRelativePointer)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonRelativePointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the relativePointer to the IJsonValue (.*)")]
    public void WhenICompareTheRelativePointerToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonRelativePointer in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the relativePointer to the object (.*)")]
    public void WhenICompareTheRelativePointerToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonRelativePointer) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonRelativePointer>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* regex */

    /// <summary>
    /// Compares the value in JsonRegex in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the regex (.*)")]
    public void WhenICompareItToTheRegex(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest).Equals(new JsonRegex((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest).Equals((JsonRegex)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest) == (JsonRegex)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest) != (JsonRegex)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonRegex)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonRegex in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the regex to the IJsonValue (.*)")]
    public void WhenICompareTheRegexToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonRegex in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the regex to the object (.*)")]
    public void WhenICompareTheRegexToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonRegex) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonRegex>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* uriTemplate */

    /// <summary>
    /// Compares the value in JsonUriTemplate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the uriTemplate (.*)")]
    public void WhenICompareItToTheUriTemplate(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest).Equals(new JsonUriTemplate((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest).Equals((JsonUriTemplate)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest) == (JsonUriTemplate)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest) != (JsonUriTemplate)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonUriTemplate)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUriTemplate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uriTemplate to the IJsonValue (.*)")]
    public void WhenICompareTheUriTemplateToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUriTemplate in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uriTemplate to the object (.*)")]
    public void WhenICompareTheUriTemplateToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? JsonUriTemplate.Undefined : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUriTemplate>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* time */

    /// <summary>
    /// Compares the value in JsonTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the time (.*)")]
    public void WhenICompareItToTheTime(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest).Equals(new JsonTime((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest).Equals((JsonTime)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest) == (JsonTime)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest) != (JsonTime)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonTime)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the time to the IJsonValue (.*)")]
    public void WhenICompareTheTimeToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonTime in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the time to the object (.*)")]
    public void WhenICompareTheTimeToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonTime) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonTime>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /* uuid */

    /// <summary>
    /// Compares the value in JsonUuid in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare it to the uuid (.*)")]
    public void WhenICompareItToTheUuid(string expected)
    {
        if (expected != "null")
        {
            this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest).Equals(new JsonUuid((string)JsonAny.ParseUriValue(expected))), EqualsObjectBackedResultKey);
        }

        this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest).Equals((JsonUuid)JsonAny.ParseUriValue(expected)), EqualsResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest) == (JsonUuid)JsonAny.ParseUriValue(expected), EqualityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest) != (JsonUuid)JsonAny.ParseUriValue(expected), InequalityResultKey);
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest).GetHashCode() == ((JsonUuid)JsonAny.ParseUriValue(expected)).GetHashCode(), HashCodeResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUuid in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uuid to the IJsonValue (.*)")]
    public void WhenICompareTheUuidToTheIJsonValue(string expected)
    {
        this.scenarioContext.Set(this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest).Equals(JsonAny.ParseUriValue(expected)), EqualsResultKey);
    }

    /// <summary>
    /// Compares the value in JsonUuid in the context variable <c>Value</c> with the expected base64String, and set it into the context variable <c>Result</c>.
    /// </summary>
    /// <param name="expected">The expected value.</param>
    [When("I compare the uuid to the object (.*)")]
    public void WhenICompareTheUuidToTheObject(string expected)
    {
        object? obj = expected == "<undefined>" ? default(JsonUuid) : expected == "<null>" ? null : expected == "<new object()>" ? new object() : JsonAny.ParseUriValue(expected);
        this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest)).Equals(obj), EqualsResultKey);
        if (obj is not null)
        {
            this.scenarioContext.Set(((object)this.scenarioContext.Get<JsonUuid>(JsonValueSteps.SubjectUnderTest)).GetHashCode() == obj.GetHashCode(), HashCodeResultKey);
        }
    }

    /// <summary>
    /// Asserts that the result from a previous comparison stored in the context variable <c>Result</c> is as expected.
    /// </summary>
    [Then("the result should be true")]
    private void ThenTheResultShouldBeTrue()
    {
        this.ThenTheResultShouldBe(true);
    }

    /// <summary>
    /// Asserts that the result from a previous comparison stored in the context variable <c>Result</c> is as expected.
    /// </summary>
    [Then("the result should be false")]
    private void ThenTheResultShouldBeFalse()
    {
        this.ThenTheResultShouldBe(false);
    }

    private void ThenTheResultShouldBe(bool expected)
    {
        Assert.AreEqual(expected, this.scenarioContext.Get<bool>(EqualsResultKey));
        if (this.scenarioContext.ContainsKey(EqualityResultKey))
        {
            Assert.AreEqual(expected, this.scenarioContext.Get<bool>(EqualityResultKey));
        }

        if (this.scenarioContext.ContainsKey(InequalityResultKey))
        {
            Assert.AreNotEqual(expected, this.scenarioContext.Get<bool>(InequalityResultKey));
        }

        if (this.scenarioContext.ContainsKey(HashCodeResultKey))
        {
            Assert.AreEqual(expected, this.scenarioContext.Get<bool>(HashCodeResultKey));
        }

        if (this.scenarioContext.ContainsKey(EqualsObjectBackedResultKey))
        {
            Assert.AreEqual(expected, this.scenarioContext.Get<bool>(EqualsObjectBackedResultKey));
        }
    }
}