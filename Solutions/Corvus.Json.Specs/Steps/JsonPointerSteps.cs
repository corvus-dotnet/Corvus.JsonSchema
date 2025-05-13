// <copyright file="JsonPointerSteps.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using System.Text.Json;

using Corvus.Json;

using NUnit.Framework;

using TechTalk.SpecFlow;

namespace Steps;

[Binding]
public class JsonPointerSteps
{
    private byte[]? jsonFile;
    private bool tryGetLineAndOffsetForPointerResult;
    private int tryGetLineAndOffsetForPointerLine;
    private int tryGetLineAndOffsetForPointerCharOffset;
    private long tryGetLineAndOffsetForPointerLineOffset;

    [Given("a JSON file")]
    public void GivenAJSONFile(string multilineText)
    {
        this.jsonFile = Encoding.UTF8.GetBytes(multilineText);
    }

    [When("I try to get the line and offset for pointer '([^']*)'")]
    public void WhenITryToGetTheLineAndOffsetForPointer(string pointer)
    {
        this.tryGetLineAndOffsetForPointerResult = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                this.jsonFile,
                pointer.AsSpan(),
                out this.tryGetLineAndOffsetForPointerLine,
                out this.tryGetLineAndOffsetForPointerCharOffset,
                out this.tryGetLineAndOffsetForPointerLineOffset);
    }

    [When("I try to get the line and offset for pointer '([^']*)' with skip comments enabled")]
    public void WhenITryToGetTheLineAndOffsetForPointerWithSkipCommentsEnabled(string pointer)
    {
        JsonReaderOptions options = new()
        {
            CommentHandling = JsonCommentHandling.Skip,
        };
        this.tryGetLineAndOffsetForPointerResult = JsonPointerUtilities.TryGetLineAndOffsetForPointer(
                this.jsonFile,
                pointer.AsSpan(),
                options,
                out this.tryGetLineAndOffsetForPointerLine,
                out this.tryGetLineAndOffsetForPointerCharOffset,
                out this.tryGetLineAndOffsetForPointerLineOffset);
    }

    [Then("TryGetLineAndOffsetForPointer returns (.*)")]
    public void ThenTryGetLineAndOffsetForPointerReturnsTrue(bool result)
    {
        Assert.AreEqual(result, this.tryGetLineAndOffsetForPointerResult);
    }

    [Then(@"the pointer resolves to line (\d*), character (\d.*)")]
    public void ThenThePointerResolvesToLineOffset(int line, int characterOffset)
    {
        Assert.AreEqual(line, this.tryGetLineAndOffsetForPointerLine, "Line");
        Assert.AreEqual(characterOffset, this.tryGetLineAndOffsetForPointerCharOffset, "Character Offset");
    }
}