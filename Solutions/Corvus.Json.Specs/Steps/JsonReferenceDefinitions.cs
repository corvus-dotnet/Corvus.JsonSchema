// <copyright file="JsonReferenceDefinitions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Corvus.Specs.Steps;

/// <summary>
/// The Step definitions for <see cref="JsonReference"/> specs.
/// </summary>
[Binding]
public sealed class JsonReferenceDefinitions
{
    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonReferenceDefinitions"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context for the run.</param>
    public JsonReferenceDefinitions(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    /// <summary>
    /// Applies a reference to the base the default (strict) and puts the result cast to a string in "Result".
    /// </summary>
    /// <param name="appliedReference">The applied reference.</param>
    /// <param name="baseReference">The base reference.</param>
    [When(@"I apply ""(.*)"" to the base reference ""(.*)""")]
    public void WhenIApplyTheReferenceToTheBase(string appliedReference, string baseReference)
    {
        var baseRef = new JsonReference(baseReference);
        var appliedRef = new JsonReference(appliedReference);

        this.scenarioContext.Add("Result", (string)baseRef.Apply(appliedRef));
    }

    /// <summary>
    /// Applies a reference to the base using the default (strict) and puts the result cast to a string in "Result".
    /// </summary>
    /// <param name="appliedReference">The applied reference.</param>
    /// <param name="baseReference">The base reference.</param>
    /// <param name="strict">A value indicating whether to be strict (true) or not.</param>
    [When(@"I apply ""(.*)"" to the base reference ""(.*)"" using (.*)")]
    public void WhenIApplyTheReferenceToTheBase(string appliedReference, string baseReference, bool strict)
    {
        var baseRef = new JsonReference(baseReference);
        var appliedRef = new JsonReference(appliedReference);

        this.scenarioContext.Add("Result", (string)baseRef.Apply(appliedRef, strict));
    }

    /// <summary>
    /// Verifies that the string value in "Result" equals the expected value.
    /// </summary>
    /// <param name="expectedResult">The expected result of merging the reference.</param>
    [Then(@"the applied reference will be ""(.*)""")]
    public void ThenTheAppliedReferenceWillBe(string expectedResult)
    {
        Assert.AreEqual(expectedResult, this.scenarioContext.Get<string>("Result"));
    }
}