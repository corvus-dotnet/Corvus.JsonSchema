// <copyright file="FormattingSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for the formatting scenarios.
/// </summary>
[Binding]
public class FormattingSteps
{
    private string formatted = string.Empty;

    [When(@"I format the input string ""([^""]*)"" with Pascal casing")]
    public void WhenIFormatTheInputStringWithPascalCasing(string input)
    {
        this.formatted = Formatting.ToPascalCaseWithReservedWords(input).ToString();
    }

    [Then(@"The output will be ""([^""]*)""")]
    public void ThenTheOutputWillBe(string expected)
    {
        Assert.AreEqual(expected, this.formatted);
    }
}