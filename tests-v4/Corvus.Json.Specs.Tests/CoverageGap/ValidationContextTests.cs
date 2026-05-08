// <copyright file="ValidationContextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

namespace Corvus.Json.Specs.Tests.CoverageGap;

[TestClass]
public class ValidationContextTests
{
    // =====================
    // Feature toggles
    // =====================
    [TestMethod]
    public void UsingResults_EnablesResults()
    {
        var ctx = ValidationContext.ValidContext.UsingResults();
        Assert.IsTrue(ctx.IsUsingResults);
        Assert.IsTrue(ctx.IsValid);
    }

    [TestMethod]
    public void UsingStack_EnablesStack()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        Assert.IsTrue(ctx.IsUsingStack);
    }

    [TestMethod]
    public void ForceUsingStack_EnablesStack()
    {
        var ctx = ValidationContext.ValidContext.ForceUsingStack();
        Assert.IsTrue(ctx.IsUsingStack);
    }

    [TestMethod]
    public void UsingStack_CalledTwice_KeepsExistingStack()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var ctx2 = ctx.PushValidationLocationProperty("foo").UsingStack();
        Assert.IsTrue(ctx2.IsUsingStack);
    }

    [TestMethod]
    public void UsingEvaluatedProperties_EnablesFlag()
    {
        var ctx = ValidationContext.ValidContext.UsingEvaluatedProperties();
        Assert.IsTrue(ctx.IsUsingEvaluatedProperties);
    }

    [TestMethod]
    public void UsingEvaluatedItems_EnablesFlag()
    {
        var ctx = ValidationContext.ValidContext.UsingEvaluatedItems();
        Assert.IsTrue(ctx.IsUsingEvaluatedItems);
    }

    // =====================
    // Push/Pop with Stack off (should return this)
    // =====================
    [TestMethod]
    public void PushSchemaLocation_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.IsUsingStack);
        var result = ctx.PushSchemaLocation("http://example.com/schema");
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushDocumentProperty_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushDocumentProperty("properties", "name");
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushDocumentArrayIndex_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushDocumentArrayIndex(0);
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushValidationLocationProperty_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushValidationLocationProperty("type");
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushValidationLocationReducedPathModifier_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushValidationLocationReducedPathModifier(new JsonReference("#/defs/MyType"));
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void ReplaceValidationLocationReducedPathModifier_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.ReplaceValidationLocationReducedPathModifier(new JsonReference("#/defs/Other"));
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushValidationLocationReducedPathModifierAndProperty_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushValidationLocationReducedPathModifierAndProperty(new JsonReference("#/defs/MyType"), "name");
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PushValidationLocationArrayIndex_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PushValidationLocationArrayIndex(3);
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    [TestMethod]
    public void PopLocation_StackOff_ReturnsSame()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.PopLocation();
        Assert.AreEqual(ctx.IsValid, result.IsValid);
    }

    // =====================
    // Push/Pop with Stack on (should modify location)
    // =====================
    [TestMethod]
    public void PushSchemaLocation_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushSchemaLocation("http://example.com/schema");
        Assert.IsTrue(result.IsUsingStack);
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void PushDocumentProperty_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushDocumentProperty("properties", "name");
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushDocumentArrayIndex_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushDocumentArrayIndex(0);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushValidationLocationProperty_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushValidationLocationProperty("type");
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushValidationLocationReducedPathModifier_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushValidationLocationReducedPathModifier(new JsonReference("#/defs/MyType"));
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void ReplaceValidationLocationReducedPathModifier_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack()
            .PushValidationLocationReducedPathModifier(new JsonReference("#/defs/First"));
        var result = ctx.ReplaceValidationLocationReducedPathModifier(new JsonReference("#/defs/Second"));
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushValidationLocationReducedPathModifierAndProperty_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushValidationLocationReducedPathModifierAndProperty(new JsonReference("#/defs/MyType"), "name");
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PushValidationLocationArrayIndex_StackOn_ModifiesContext()
    {
        var ctx = ValidationContext.ValidContext.UsingStack();
        var result = ctx.PushValidationLocationArrayIndex(3);
        Assert.IsTrue(result.IsUsingStack);
    }

    [TestMethod]
    public void PopLocation_StackOn_PopsBack()
    {
        var ctx = ValidationContext.ValidContext.UsingStack()
            .PushValidationLocationProperty("type");
        var popped = ctx.PopLocation();
        Assert.IsTrue(popped.IsUsingStack);
    }

    // =====================
    // WithResult — results off
    // =====================
    [TestMethod]
    public void WithResult_ResultsOff_Valid_StaysValid()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.IsUsingResults);
        var result = ctx.WithResult(isValid: true, "ok");
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void WithResult_ResultsOff_Invalid_BecomesInvalid()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.WithResult(isValid: false, "bad");
        Assert.IsFalse(result.IsValid);
    }

    // =====================
    // WithResult — results on, stack off
    // =====================
    [TestMethod]
    public void WithResult_ResultsOn_StackOff_AddsResult()
    {
        var ctx = ValidationContext.ValidContext.UsingResults();
        var result = ctx.WithResult(isValid: true, "ok");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    [TestMethod]
    public void WithResult_ResultsOn_StackOff_Invalid_AddsResult()
    {
        var ctx = ValidationContext.ValidContext.UsingResults();
        var result = ctx.WithResult(isValid: false, "bad");
        Assert.IsFalse(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    // =====================
    // WithResult — results on, stack on
    // =====================
    [TestMethod]
    public void WithResult_ResultsOn_StackOn_WithKeyword_AddsResult()
    {
        var ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        var result = ctx.WithResult(isValid: true, "ok", "type");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    [TestMethod]
    public void WithResult_ResultsOn_StackOn_NoKeyword_AddsResult()
    {
        var ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        var result = ctx.WithResult(isValid: true, "ok");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    // =====================
    // WithResult(isValid, reducedPathModifier, message) overload
    // =====================
    [TestMethod]
    public void WithResult_ReducedPath_ResultsOff()
    {
        var ctx = ValidationContext.ValidContext;
        var result = ctx.WithResult(isValid: true, new JsonReference("#/defs/Foo"), "ok");
        Assert.IsTrue(result.IsValid);
    }

    [TestMethod]
    public void WithResult_ReducedPath_ResultsOn_StackOff()
    {
        var ctx = ValidationContext.ValidContext.UsingResults();
        var result = ctx.WithResult(isValid: true, new JsonReference("#/defs/Foo"), "ok");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    [TestMethod]
    public void WithResult_ReducedPath_ResultsOn_StackOn()
    {
        var ctx = ValidationContext.ValidContext.UsingResults().UsingStack();
        var result = ctx.WithResult(isValid: true, new JsonReference("#/defs/Foo"), "ok");
        Assert.IsTrue(result.IsValid);
        Assert.AreEqual(1, result.Results.Count);
    }

    // =====================
    // MergeChildContext
    // =====================
    [TestMethod]
    public void MergeChildContext_IncludeResults_ChildInvalid()
    {
        var parent = ValidationContext.ValidContext.UsingResults();
        var child = parent.WithResult(isValid: false, "child bad");
        var merged = parent.MergeChildContext(child, includeResults: true);
        Assert.IsFalse(merged.IsValid);
    }

    [TestMethod]
    public void MergeChildContext_IncludeResults_ChildValid()
    {
        var parent = ValidationContext.ValidContext.UsingResults();
        var child = parent.WithResult(isValid: true, "child ok");
        var merged = parent.MergeChildContext(child, includeResults: true);
        Assert.IsTrue(merged.IsValid);
    }

    [TestMethod]
    public void MergeChildContext_ExcludeResults_ChildInvalid()
    {
        var parent = ValidationContext.ValidContext.UsingResults();
        var child = parent.WithResult(isValid: false, "child bad");
        var merged = parent.MergeChildContext(child, includeResults: false);
        Assert.IsTrue(merged.IsValid);
    }

    // =====================
    // Evaluated properties and items
    // =====================
    [TestMethod]
    public void WithLocalProperty_ThenHasEvaluatedLocalProperty()
    {
        var ctx = ValidationContext.ValidContext.UsingEvaluatedProperties();
        var updated = ctx.WithLocalProperty(0);
        Assert.IsTrue(updated.HasEvaluatedLocalProperty(0));
        Assert.IsFalse(updated.HasEvaluatedLocalProperty(1));
    }

    [TestMethod]
    public void WithLocalItemIndex_ThenHasEvaluatedLocalItemIndex()
    {
        var ctx = ValidationContext.ValidContext.UsingEvaluatedItems();
        var updated = ctx.WithLocalItemIndex(0);
        Assert.IsTrue(updated.HasEvaluatedLocalItemIndex(0));
        Assert.IsFalse(updated.HasEvaluatedLocalItemIndex(1));
    }

    [TestMethod]
    public void HasEvaluatedLocalProperty_NotUsingProperties_ReturnsFalse()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.IsUsingEvaluatedProperties);
        Assert.IsFalse(ctx.HasEvaluatedLocalProperty(0));
    }

    [TestMethod]
    public void HasEvaluatedLocalItemIndex_NotUsingItems_ReturnsFalse()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.IsUsingEvaluatedItems);
        Assert.IsFalse(ctx.HasEvaluatedLocalItemIndex(0));
    }

    [TestMethod]
    public void HasEvaluatedLocalOrAppliedProperty_NotUsingProperties_ReturnsFalse()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.HasEvaluatedLocalOrAppliedProperty(0));
    }

    [TestMethod]
    public void HasEvaluatedLocalOrAppliedItemIndex_NotUsingItems_ReturnsFalse()
    {
        var ctx = ValidationContext.ValidContext;
        Assert.IsFalse(ctx.HasEvaluatedLocalOrAppliedItemIndex(0));
    }

    [TestMethod]
    public void MergeChildContext_CombinesEvaluatedProperties()
    {
        var parent = ValidationContext.ValidContext.UsingEvaluatedProperties();
        var child = parent.WithLocalProperty(0);
        var merged = parent.MergeChildContext(child, includeResults: false);
        Assert.IsTrue(merged.HasEvaluatedLocalOrAppliedProperty(0));
    }

    [TestMethod]
    public void MergeChildContext_CombinesEvaluatedItems()
    {
        var parent = ValidationContext.ValidContext.UsingEvaluatedItems();
        var child = parent.WithLocalItemIndex(0);
        var merged = parent.MergeChildContext(child, includeResults: false);
        Assert.IsTrue(merged.HasEvaluatedLocalOrAppliedItemIndex(0));
    }

    // =====================
    // ValidContext and InvalidContext
    // =====================
    [TestMethod]
    public void ValidContext_IsValid() =>
        Assert.IsTrue(ValidationContext.ValidContext.IsValid);

    [TestMethod]
    public void InvalidContext_IsNotValid() =>
        Assert.IsFalse(ValidationContext.InvalidContext.IsValid);
}