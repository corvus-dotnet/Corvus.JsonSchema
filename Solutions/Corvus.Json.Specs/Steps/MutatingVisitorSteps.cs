// <copyright file="MutatingVisitorSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Visitor;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for Mutating Visitor steps.
/// </summary>
[Binding]
public class MutatingVisitorSteps
{
    private const string JsonDocumentKey = "JsonDocument";
    private const string ResultKey = "Result";
    private const string IsTransformedKey = "IsTransformed";

    private readonly ScenarioContext scenarioContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UriTemplateSteps"/> class.
    /// </summary>
    /// <param name="scenarioContext">The scenario context.</param>
    public MutatingVisitorSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [Given("the json document (.*)")]
    public void GivenTheJsonDocument(string sut)
    {
        this.scenarioContext.Set(JsonAny.Parse(sut), JsonDocumentKey);
    }

    [When("I visit with the RemoveThereString")]
    public void WhenIVisitWithTheRemoveThereString()
    {
        JsonAny value = this.scenarioContext.Get<JsonAny>(JsonDocumentKey);

        bool transformed = value.Visit(this.VisitRemoveThereString, out JsonAny result);

        this.scenarioContext.Set(result, ResultKey);
        this.scenarioContext.Set(transformed, IsTransformedKey);
    }

    [When("I visit with the RemoveThereProperty")]
    public void WhenIVisitWithTheRemoveThereProperty()
    {
        JsonAny value = this.scenarioContext.Get<JsonAny>(JsonDocumentKey);

        bool transformed = value.Visit(this.VisitRemoveThereProperty, out JsonAny result);

        this.scenarioContext.Set(result, ResultKey);
        this.scenarioContext.Set(transformed, IsTransformedKey);
    }

    [Then("I expect the result to be modified")]
    public void ThenIExpectTheResultToBeModified()
    {
        Assert.IsTrue(this.scenarioContext.Get<bool>(IsTransformedKey));
    }

    [Then("I expect the result not to be modified")]
    public void ThenIExpectTheResultNotToBeModified()
    {
        Assert.IsFalse(this.scenarioContext.Get<bool>(IsTransformedKey));
    }

    [Then("the resulting document to be (.*)")]
    public void ThenTheResultingDocumentToBe(string expected)
    {
        Assert.AreEqual(expected == "<undefined>" ? JsonAny.Undefined : JsonAny.Parse(expected), this.scenarioContext.Get<JsonAny>(ResultKey));
    }

    private void VisitRemoveThereString(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
    {
        if (nodeToVisit.ValueKind == JsonValueKind.String)
        {
            if (nodeToVisit == "there")
            {
                result.Walk = Walk.RemoveAndContinue;
                result.Transformed = Transformed.Yes;
                result.Output = JsonAny.Undefined;
                return;
            }
        }

        result.Walk = Walk.Continue;
        result.Transformed = Transformed.No;
        result.Output = nodeToVisit;
    }

    private void VisitRemoveThereProperty(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
    {
        if (path.EndsWith("/there"))
        {
            result.Walk = Walk.RemoveAndContinue;
            result.Transformed = Transformed.Yes;
            result.Output = JsonAny.Undefined;
        }
        else
        {
            result.Walk = Walk.Continue;
            result.Transformed = Transformed.No;
            result.Output = nodeToVisit;
        }
    }
}