// <copyright file="TruncatePathNamesSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET8_0_OR_GREATER
using Corvus.Json.Internal;
using NUnit.Framework;
using TechTalk.SpecFlow;

namespace Steps;

/// <summary>
/// Steps for path name truncation.
/// </summary>
[Binding]
public class TruncatePathNamesSteps
{
    private string? pathUnderTest;
    private string? result;

    /// <summary>
    /// Sets a path to test.
    /// </summary>
    /// <param name="pathUnderTest">The path under test.</param>
    [Given(@"a path ""([^""]*)""")]
    public void GivenAPath(string pathUnderTest)
    {
        this.pathUnderTest = pathUnderTest;
    }

    [When("the path is truncated to the maximum length of (.*)")]
    public void WhenThePathIsTruncatedToTheMaximumLengthOf(int maxLength)
    {
        Assert.IsNotNull(this.pathUnderTest);
        this.result = PathTruncator.TruncatePath(this.pathUnderTest!, maxLength);
    }

    [Then(@"the result should be ""([^""]*)""")]
    public void ThenTheResultShouldBe(string expectedResult)
    {
        // Normalize to the local directory separator.
        string expected = expectedResult.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        Assert.AreEqual(expected, this.result);
    }
}
#endif