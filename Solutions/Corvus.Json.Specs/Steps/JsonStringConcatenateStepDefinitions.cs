// <copyright file="JsonStringConcatenateStepDefinitions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class JsonStringConcatenateStepDefinitions
{
    /// <summary>
    /// The key for a concatenation result.
    /// </summary>
    internal const string ConcatenateResult = "ConcatenateResult";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonStringConcatenateStepDefinitions"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public JsonStringConcatenateStepDefinitions(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When("the values are concatenated to a JsonString")]
    public void WhenTheValuesAreConcatenatedToAJsonString()
    {
        JsonArray subjectUnderTest = this.scenarioContext.Get<JsonArray>(JsonValueSteps.SubjectUnderTest);
        Span<byte> buffer = stackalloc byte[256];
        JsonString result = subjectUnderTest.GetArrayLength() switch
        {
            2 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1]),
            3 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2]),
            4 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3]),
            5 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4]),
            6 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5]),
            7 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6]),
            8 => JsonString.Concatenate(buffer, subjectUnderTest[0], subjectUnderTest[1], subjectUnderTest[2], subjectUnderTest[3], subjectUnderTest[4], subjectUnderTest[5], subjectUnderTest[6], subjectUnderTest[7]),
            _ => throw new InvalidOperationException("Unsupported number of items"),
        };
        this.scenarioContext.Set(result, ConcatenateResult);
    }

    [When("the result should be equal to the JsonString (.*)")]
    public void WhenTheResultShouldBeEqualToTheJsonString(string result)
    {
        var stringValue = new JsonString(result);
        Assert.AreEqual(stringValue, this.scenarioContext.Get<JsonString>(ConcatenateResult));
    }
}