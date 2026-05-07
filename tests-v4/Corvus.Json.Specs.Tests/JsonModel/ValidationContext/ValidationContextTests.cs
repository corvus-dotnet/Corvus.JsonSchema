// <copyright file="ValidationContextTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1600 // Elements should be documented

using Corvus.Json;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonModel.ValidationContext;

public class ValidationContextTests
{
    [Theory]
    [InlineData(true, false, true, false, false, true, null, true)]
    [InlineData(true, false, false, false, false, true, null, true)]
    [InlineData(true, false, true, false, false, false, null, false)]
    [InlineData(true, false, false, false, false, false, null, false)]
    [InlineData(false, false, true, false, false, true, null, false)]
    [InlineData(false, false, false, false, false, true, null, false)]
    [InlineData(false, false, true, false, false, false, null, false)]
    [InlineData(false, false, false, false, false, false, null, false)]
    [InlineData(true, false, true, false, false, true, "A message", true)]
    [InlineData(true, false, false, false, false, true, "A message", true)]
    [InlineData(true, false, true, false, false, false, "A message", false)]
    [InlineData(true, false, false, false, false, false, "A message", false)]
    [InlineData(false, false, true, false, false, true, "A message", false)]
    [InlineData(false, false, false, false, false, true, "A message", false)]
    [InlineData(false, false, true, false, false, false, "A message", false)]
    [InlineData(false, false, false, false, false, false, "A message", false)]
    [InlineData(true, true, true, false, false, true, null, true)]
    [InlineData(true, true, false, false, false, true, null, true)]
    [InlineData(true, true, true, false, false, false, null, false)]
    [InlineData(true, true, false, false, false, false, null, false)]
    [InlineData(false, true, true, false, false, true, null, false)]
    [InlineData(false, true, false, false, false, true, null, false)]
    [InlineData(false, true, true, false, false, false, null, false)]
    [InlineData(false, true, false, false, false, false, null, false)]
    [InlineData(true, true, true, false, false, true, "A message", true)]
    [InlineData(true, true, false, false, false, true, "A message", true)]
    [InlineData(true, true, true, false, false, false, "A message", false)]
    [InlineData(true, true, false, false, false, false, "A message", false)]
    [InlineData(false, true, true, false, false, true, "A message", false)]
    [InlineData(false, true, false, false, false, true, "A message", false)]
    [InlineData(false, true, true, false, false, false, "A message", false)]
    [InlineData(false, true, false, false, false, false, "A message", false)]
    public void AddResultsToTheStack(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        bool addValidResult,
        string? message,
        bool expectedValid)
    {
        Corvus.Json.ValidationContext context = startValid
            ? Corvus.Json.ValidationContext.ValidContext
            : Corvus.Json.ValidationContext.InvalidContext;

        if (useResults)
        {
            context = context.UsingResults();
        }

        if (useStack)
        {
            context = context.UsingStack();
        }

        if (useEvaluatedProperties)
        {
            context = context.UsingEvaluatedProperties();
        }

        if (useEvaluatedItems)
        {
            context = context.UsingEvaluatedItems();
        }

        context = context.WithResult(addValidResult, message);

        Assert.Equal(expectedValid, context.IsValid);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, true, false, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, true, false, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, true, false, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithEvaluatedProperties(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        context = EvaluateProperties(context, evaluateIndices);

        AssertPropertiesNotEvaluatedLocally(context, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocally(context, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, false, true, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, false, true, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, false, true, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithEvaluatedItems(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        context = EvaluateItems(context, evaluateIndices);

        AssertItemsNotEvaluatedLocally(context, notEvaluatedIndices);
        AssertItemsEvaluatedLocally(context, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, true, false, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, true, false, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, true, false, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithMergedChildContextProperties(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        Corvus.Json.ValidationContext childContext = context.CreateChildContext();

        childContext = EvaluateProperties(childContext, evaluateIndices);

        Corvus.Json.ValidationContext merged = context.MergeChildContext(childContext, false);

        AssertPropertiesNotEvaluatedLocally(merged, notEvaluatedIndices);
        AssertPropertiesNotEvaluatedLocally(merged, evaluatedIndices);
        AssertPropertiesNotEvaluatedLocallyOrApplied(merged, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocallyOrApplied(merged, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, false, true, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, false, true, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, false, true, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithMergedChildContextItems(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        Corvus.Json.ValidationContext childContext = context.CreateChildContext();

        childContext = EvaluateItems(childContext, evaluateIndices);

        Corvus.Json.ValidationContext merged = context.MergeChildContext(childContext, false);

        AssertItemsNotEvaluatedLocally(merged, notEvaluatedIndices);
        AssertItemsNotEvaluatedLocally(merged, evaluatedIndices);
        AssertItemsNotEvaluatedLocallyOrApplied(merged, notEvaluatedIndices);
        AssertItemsEvaluatedLocallyOrApplied(merged, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, true, false, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, true, false, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, true, false, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithMergedChildContextPropertiesEvaluatedBeforeMerging(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        context = EvaluateProperties(context, evaluateIndices);

        Corvus.Json.ValidationContext childContext = context.CreateChildContext();

        Corvus.Json.ValidationContext merged = context.MergeChildContext(childContext, false);

        AssertPropertiesNotEvaluatedLocally(merged, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocally(merged, evaluatedIndices);
        AssertPropertiesNotEvaluatedLocallyOrApplied(merged, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocallyOrApplied(merged, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "<none>", "0,1,2,3,4")]
    [InlineData(true, false, false, false, true, "1,2,3", "1,2,3", "0,4")]
    [InlineData(true, false, false, false, true, "66,129", "66,129", "0,1,2,3,4,63,64,65,67,68,126,127,128,130")]
    [InlineData(true, false, false, false, true, "65536", "65536", "0,1,2,3,4,63,64,65,67,68,126,127,128,130,65535,262144")]
    public void WithMergedChildContextItemsEvaluatedBeforeMerging(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluatedIndices,
        string notEvaluatedIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        context = EvaluateItems(context, evaluateIndices);

        Corvus.Json.ValidationContext childContext = context.CreateChildContext();

        Corvus.Json.ValidationContext merged = context.MergeChildContext(childContext, false);

        AssertItemsNotEvaluatedLocally(merged, notEvaluatedIndices);
        AssertItemsEvaluatedLocally(merged, evaluatedIndices);
        AssertItemsNotEvaluatedLocallyOrApplied(merged, notEvaluatedIndices);
        AssertItemsEvaluatedLocallyOrApplied(merged, evaluatedIndices);
    }

    [Theory]
    [InlineData(true, false, false, false, false, "1,2,3", "4", "<none>", "0,1,2,3,4", "<none>")]
    [InlineData(true, false, false, true, false, "1,2,3", "4", "1,2,3", "0", "4")]
    [InlineData(true, false, false, true, false, "66,129", "4", "66,129", "0,1,2,3,63,64,65,67,68,126,127,128,130", "4")]
    [InlineData(true, false, false, true, false, "65536", "4", "65536", "0,1,2,3,63,64,65,67,68,126,127,128,130,65535,262144", "4")]
    public void WithMergedChildContextPropertiesEvaluatedBeforeAndAfterMerging(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems,
        string evaluateIndices,
        string evaluateAfterIndices,
        string evaluatedIndices,
        string notEvaluatedIndices,
        string evaluatedAfterIndices)
    {
        Corvus.Json.ValidationContext context = CreateContext(startValid, useResults, useStack, useEvaluatedProperties, useEvaluatedItems);

        context = EvaluateProperties(context, evaluateIndices);

        Corvus.Json.ValidationContext childContext = context.CreateChildContext();

        childContext = EvaluateProperties(childContext, evaluateAfterIndices);

        Corvus.Json.ValidationContext merged = context.MergeChildContext(childContext, false);

        AssertPropertiesNotEvaluatedLocally(merged, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocally(merged, evaluatedIndices);
        AssertPropertiesNotEvaluatedLocally(merged, evaluatedAfterIndices);
        AssertPropertiesNotEvaluatedLocallyOrApplied(merged, notEvaluatedIndices);
        AssertPropertiesEvaluatedLocallyOrApplied(merged, evaluatedIndices);
        AssertPropertiesEvaluatedLocallyOrApplied(merged, evaluatedAfterIndices);
    }

    private static Corvus.Json.ValidationContext CreateContext(
        bool startValid,
        bool useResults,
        bool useStack,
        bool useEvaluatedProperties,
        bool useEvaluatedItems)
    {
        Corvus.Json.ValidationContext context = startValid
            ? Corvus.Json.ValidationContext.ValidContext
            : Corvus.Json.ValidationContext.InvalidContext;

        if (useResults)
        {
            context = context.UsingResults();
        }

        if (useStack)
        {
            context = context.UsingStack();
        }

        if (useEvaluatedProperties)
        {
            context = context.UsingEvaluatedProperties();
        }

        if (useEvaluatedItems)
        {
            context = context.UsingEvaluatedItems();
        }

        return context;
    }

    private static Corvus.Json.ValidationContext EvaluateProperties(Corvus.Json.ValidationContext context, string propertyIndexArray)
    {
        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            context = context.WithLocalProperty(int.Parse(propertyIndex));
        }

        return context;
    }

    private static Corvus.Json.ValidationContext EvaluateItems(Corvus.Json.ValidationContext context, string itemIndexArray)
    {
        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            context = context.WithLocalItemIndex(int.Parse(itemIndex));
        }

        return context;
    }

    private static void AssertPropertiesNotEvaluatedLocally(Corvus.Json.ValidationContext context, string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(context.HasEvaluatedLocalProperty(int.Parse(propertyIndex)));
        }
    }

    private static void AssertPropertiesEvaluatedLocally(Corvus.Json.ValidationContext context, string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(context.HasEvaluatedLocalProperty(int.Parse(propertyIndex)));
        }
    }

    private static void AssertPropertiesNotEvaluatedLocallyOrApplied(Corvus.Json.ValidationContext context, string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(context.HasEvaluatedLocalOrAppliedProperty(int.Parse(propertyIndex)));
        }
    }

    private static void AssertPropertiesEvaluatedLocallyOrApplied(Corvus.Json.ValidationContext context, string propertyIndexArray)
    {
        if (propertyIndexArray == "<none>")
        {
            return;
        }

        foreach (string propertyIndex in propertyIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(context.HasEvaluatedLocalOrAppliedProperty(int.Parse(propertyIndex)));
        }
    }

    private static void AssertItemsNotEvaluatedLocally(Corvus.Json.ValidationContext context, string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(context.HasEvaluatedLocalItemIndex(int.Parse(itemIndex)));
        }
    }

    private static void AssertItemsEvaluatedLocally(Corvus.Json.ValidationContext context, string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(context.HasEvaluatedLocalItemIndex(int.Parse(itemIndex)));
        }
    }

    private static void AssertItemsNotEvaluatedLocallyOrApplied(Corvus.Json.ValidationContext context, string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.False(context.HasEvaluatedLocalOrAppliedItemIndex(int.Parse(itemIndex)));
        }
    }

    private static void AssertItemsEvaluatedLocallyOrApplied(Corvus.Json.ValidationContext context, string itemIndexArray)
    {
        if (itemIndexArray == "<none>")
        {
            return;
        }

        foreach (string itemIndex in itemIndexArray.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            Assert.True(context.HasEvaluatedLocalOrAppliedItemIndex(int.Parse(itemIndex)));
        }
    }
}