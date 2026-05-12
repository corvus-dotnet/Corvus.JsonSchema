// <copyright file="ValidationContextMergeResultsTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600

using Corvus.Json;

namespace Corvus.Json.Specs.Tests.CoverageGap;

/// <summary>
/// Tests for <see cref="ValidationContext.MergeResults"/> overloads.
/// </summary>
[TestClass]
public class ValidationContextMergeResultsTests
{
    [TestMethod]
    public void MergeResults_2_FlagLevel_Valid_ReturnsThis()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child1 = ValidationContext.ValidContext;
        ValidationContext child2 = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child1, child2);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_2_FlagLevel_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child1 = ValidationContext.ValidContext;
        ValidationContext child2 = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Flag, child1, child2);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_2_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child1 = ctx.WithResult(isValid: true, "Test passed 1");
        ValidationContext child2 = ctx.WithResult(isValid: false, "Test failed 2");

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child1, child2);
        Assert.IsFalse(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 2);
    }

    [TestMethod]
    public void MergeResults_2_NoResults_SetsValidity()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child1 = ValidationContext.ValidContext;
        ValidationContext child2 = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child1, child2);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_3_FlagLevel_Valid_ReturnsThis()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_3_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_3_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child1 = ctx.WithResult(isValid: true, "Pass 1");
        ValidationContext child2 = ctx.WithResult(isValid: false, "Fail 2");
        ValidationContext child3 = ctx.WithResult(isValid: true, "Pass 3");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child1, child2, child3);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 3);
    }

    [TestMethod]
    public void MergeResults_4_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_4_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_4_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Pass");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child, child, child, child);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 4);
    }

    [TestMethod]
    public void MergeResults_5_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_5_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_5_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Pass");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 5);
    }

    [TestMethod]
    public void MergeResults_6_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_6_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_6_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Pass");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 6);
    }

    [TestMethod]
    public void MergeResults_7_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_7_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_7_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Pass");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 7);
    }

    [TestMethod]
    public void MergeResults_8_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_8_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_8_WithResults_MergesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Pass");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child, child, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
        Assert.IsTrue(result.Results.Count >= 8);
    }

    [TestMethod]
    public void MergeResults_Params_FlagLevel_Valid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Flag, child, child, child, child, child, child, child, child, child);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_Params_NoResults_Invalid()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.ValidContext;

        ValidationContext result = ctx.MergeResults(false, ValidationLevel.Verbose, child, child, child, child, child, child, child, child, child);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeChildContext_IncludeResults_Valid()
    {
        ValidationContext parent = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = parent.WithResult(isValid: true, "Child pass");

        ValidationContext result = parent.MergeChildContext(child, true);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeChildContext_IncludeResults_Invalid()
    {
        ValidationContext parent = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ValidationContext.InvalidContext.UsingResults().UsingStack()
            .WithResult(isValid: false, "Child fail");

        ValidationContext result = parent.MergeChildContext(child, true);
        Assert.IsFalse(result.IsValid);
    }

    [TestMethod]
    public void MergeChildContext_ExcludeResults_KeepsParentValid()
    {
        ValidationContext parent = ValidationContext.ValidContext;
        ValidationContext child = ValidationContext.InvalidContext;

        ValidationContext result = parent.MergeChildContext(child, false);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void MergeResults_1_WithEmptyParentResults_TakesChildResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext child = ctx.WithResult(isValid: true, "Only child result");

        ValidationContext result = ctx.MergeResults(true, ValidationLevel.Verbose, child);
        Assert.IsTrue(result.Results.Count >= 1);
    }

    [TestMethod]
    public void MergeResults_1_BothHaveResults_MergesAll()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext parentWithResult = ctx.WithResult(isValid: true, "Parent result");
        ValidationContext child = ctx.WithResult(isValid: true, "Child result");

        ValidationContext result = parentWithResult.MergeResults(true, ValidationLevel.Verbose, child);
        Assert.IsTrue(result.Results.Count >= 2);
    }

    [TestMethod]
    public void MergeResults_1_ChildEmptyResults_KeepsParentResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        ValidationContext parentWithResult = ctx.WithResult(isValid: true, "Parent result");
        ValidationContext emptyChild = ctx;

        ValidationContext result = parentWithResult.MergeResults(true, ValidationLevel.Verbose, emptyChild);
        Assert.IsTrue(result.Results.Count >= 1);
    }

    [TestMethod]
    public void PushSchemaLocation_NotUsingStack_ReturnsThis()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext result = ctx.PushSchemaLocation("test");
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void PushDocumentProperty_NotUsingStack_ReturnsThis()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext result = ctx.PushDocumentProperty("properties", "name");
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void PushDocumentArrayIndex_NotUsingStack_ReturnsThis()
    {
        ValidationContext ctx = ValidationContext.ValidContext;
        ValidationContext result = ctx.PushDocumentArrayIndex(0);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void PushSchemaLocation_UsingStack_PushesLocation()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingStack();
        ValidationContext result = ctx.PushSchemaLocation("test/schema");
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushDocumentProperty_UsingStack_PushesLocation()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingStack();
        ValidationContext result = ctx.PushDocumentProperty("properties", "name");
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushDocumentArrayIndex_UsingStack_PushesLocation()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingStack();
        ValidationContext result = ctx.PushDocumentArrayIndex(5);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void UsingResults_EnablesResults()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingResults();
        Assert.IsTrue(ctx.IsUsingResults);
    }

    [TestMethod]
    public void UsingEvaluatedProperties_Enables()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingEvaluatedProperties();
        Assert.IsTrue(ctx.IsUsingEvaluatedProperties);
    }

    [TestMethod]
    public void UsingEvaluatedItems_Enables()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingEvaluatedItems();
        Assert.IsTrue(ctx.IsUsingEvaluatedItems);
    }

    [TestMethod]
    public void WithLocalProperty_AddsProperty()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingEvaluatedProperties();
        ValidationContext result = ctx.WithLocalProperty(0);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void WithLocalItemIndex_AddsItem()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingEvaluatedItems();
        ValidationContext result = ctx.WithLocalItemIndex(0);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void ForceUsingStack_ResetsStack()
    {
        ValidationContext ctx = ValidationContext.ValidContext.UsingStack().PushSchemaLocation("deep/path");
        ValidationContext result = ctx.ForceUsingStack();
        Assert.IsTrue(result.IsUsingStack);
    }
}