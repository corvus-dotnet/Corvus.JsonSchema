// <copyright file="FormattingSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration.CSharp;
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
        Span<char> value = stackalloc char[Formatting.GetBufferLength(input.Length, "Entity".AsSpan(), ReadOnlySpan<char>.Empty)];
        input.AsSpan().CopyTo(value);
        int written = Formatting.ToPascalCase(value[..input.Length]);
        written = Formatting.FixReservedWords(value, written, "Entity".AsSpan(), ReadOnlySpan<char>.Empty);
        this.formatted = value[..written].ToString();
    }

    [Then(@"The output will be ""([^""]*)""")]
    public void ThenTheOutputWillBe(string expected)
    {
        Assert.AreEqual(expected, this.formatted);
    }
}