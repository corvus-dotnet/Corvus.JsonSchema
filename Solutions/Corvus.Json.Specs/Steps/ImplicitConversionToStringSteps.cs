// <copyright file="ImplicitConversionToStringSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for implicit conversion to string.
/// </summary>
[Binding]
public class ImplicitConversionToStringSteps
{
    private readonly ScenarioContext scenarioContext;
    private string? assignedInstance;

    public ImplicitConversionToStringSteps(ScenarioContext scenarioContext)
    {
        this.scenarioContext = scenarioContext;
    }

    [When(@"I assign the instance to a string")]
    public void WhenIAssignTheInstanceToAString()
    {
        this.assignedInstance = JsonSchemaSteps.GetTheValueAsStringImplicit(this.scenarioContext);
    }

    [Then(@"the assigned string will be ""([^""]*)""")]
    public void ThenTheAssignedStringWillBe(string foo)
    {
        Assert.AreEqual(foo, this.assignedInstance);
    }
}