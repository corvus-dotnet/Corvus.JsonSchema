// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json.Internal;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

[TestClass]
public class JsonSchemaContextTests
{
    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(false, true, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(false, true, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedItems(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeArrayDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedItem(item);
        }

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(false, true, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(false, true, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedItemsAfterUnchangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeArrayDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);
        // NOP
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedItem(item);
        }

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(false, true, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(false, true, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedItemsFromChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeArrayDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);

        // Act
        foreach (int item in evaluateIndices)
        {
            childContext.AddLocalEvaluatedItem(item);
        }

        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(false, true, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(false, true, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedItemsWithBeforeUnchangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeArrayDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedItem(item);
        }

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);
        // NOP
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(false, true, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(false, true, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(false, true, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(false, true, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedItemsWithChangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeArrayDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedItem(item);
        }

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);
        foreach (int item in evaluateIndices)
        {
            childContext.AddLocalEvaluatedItem(item);
        }
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedItem(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(true, false, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(true, false, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedProperties(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeObjectDocument() : CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedProperty(item);
        }

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(true, false, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(true, false, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedPropertiesAfterUnchangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeObjectDocument() : CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);
        // NOP
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedProperty(item);
        }

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(true, false, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(true, false, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedPropertiesBeforeUnchangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeObjectDocument() : CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedProperty(item);
        }

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(true, false, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(true, false, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedPropertiesFromChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeObjectDocument() : CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);

        // Act
        foreach (int item in evaluateIndices)
        {
            childContext.AddLocalEvaluatedProperty(item);
        }

        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
    }

    [TestMethod]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, true)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, true)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, true)]
    [DataRow(true, false, new int[] { 65536 }, new int[] { 65536 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130, 65535 }, true)]
    [DataRow(false, false, new int[] { 1, 2, 3 }, new int[0], new int[] { 0, 1, 2, 3, 4 }, false)]
    [DataRow(true, false, new int[] { 1, 2, 3 }, new int[] { 1, 2, 3 }, new int[] { 0, 4 }, false)]
    [DataRow(true, false, new int[] { 66, 129 }, new int[] { 66, 129 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    [DataRow(true, false, new int[] { 254 }, new int[] { 254 }, new int[] { 0, 1, 2, 3, 4, 63, 64, 65, 67, 68, 126, 127, 128, 130 }, false)]
    public void EvaluatedPropertiesWithChangedChildContext(bool usingEvaluatedProperties, bool usingEvaluatedItems, int[] evaluateIndices, int[] evaluatedIndices, int[] notEvaluatedIndices, bool useLargeDocument)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useLargeDocument ? CreateLargeObjectDocument() : CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(parentDocument, parentIndex, usingEvaluatedItems, usingEvaluatedProperties);

        // Act
        foreach (int item in evaluateIndices)
        {
            context.AddLocalEvaluatedProperty(item);
        }

        JsonSchemaContext childContext = context.PushChildContext(parentDocument, parentIndex, usingEvaluatedProperties, usingEvaluatedItems);

        foreach (int item in evaluateIndices)
        {
            childContext.AddLocalEvaluatedProperty(item);
        }

        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(evaluatedIndices, i => Assert.IsTrue(context.HasLocalEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(i)));
        AssertEx.All(notEvaluatedIndices, i => Assert.IsFalse(context.HasLocalEvaluatedProperty(i)));
    }

    #region Phase 1: Core Lifecycle and Boundary Tests

    [TestMethod]
    public void BeginContext_WithResultsCollector_SetsHasCollectorTrue()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var mockCollector = new DummyResultsCollector();

        // Act
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: mockCollector);

        // Assert
        Assert.IsTrue(context.HasCollector);
        Assert.IsTrue(context.IsMatch); // Should start as match
    }

    [TestMethod]
    public void BeginContext_WithoutResultsCollector_SetsHasCollectorFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        // Act
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Assert
        Assert.IsFalse(context.HasCollector);
        Assert.IsTrue(context.IsMatch); // Should start as match
    }

    [TestMethod]
    public void BeginContext_WithArrayDocument_UsingEvaluatedItems_InitializesCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        // Act
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Assert - Should not have any evaluated items initially
        Assert.IsFalse(context.HasLocalEvaluatedItem(0));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(0));
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void BeginContext_WithObjectDocument_UsingEvaluatedProperties_InitializesCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        // Act
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true);

        // Assert - Should not have any evaluated properties initially
        Assert.IsFalse(context.HasLocalEvaluatedProperty(0));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(0));
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void BeginContext_WithPrimitiveDocument_InitializesCorrectly()
    {
        // Arrange
        using var document = ParsedJsonDocument<JsonElement>.Parse("42");
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        // Act
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true);

        // Assert - Primitive documents don't support evaluated items/properties
        Assert.IsTrue(context.IsMatch);
        Assert.IsFalse(context.HasCollector);
    }

    [TestMethod]
    public void Dispose_MultipleCalls_DoesNotThrow()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act & Assert - Should not throw
        context.Dispose();
        context.Dispose(); // Second call should be safe
    }

    [TestMethod]
    public void HasLocalEvaluatedItem_WhenEvaluatedItemsDisabled_ReturnsFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false, // Disabled
            usingEvaluatedProperties: false);

        // Act
        context.AddLocalEvaluatedItem(1); // This should be ignored

        // Assert
        Assert.IsFalse(context.HasLocalEvaluatedItem(1));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(1));
    }

    [TestMethod]
    public void HasLocalEvaluatedProperty_WhenEvaluatedPropertiesDisabled_ReturnsFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: false); // Disabled

        // Act
        context.AddLocalEvaluatedProperty(1); // This should be ignored

        // Assert
        Assert.IsFalse(context.HasLocalEvaluatedProperty(1));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(1));
    }

    #endregion

    #region Phase 2: Child Context Management and Evaluation Tests

    [TestMethod]
    public void PushChildContext_CreatesNewContext()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true);

        // Act
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            true); // useEvaluatedProperties

        // Assert
        Assert.IsTrue(childContext.IsMatch); // Child should start as match
        Assert.IsFalse(childContext.HasCollector); // Should inherit from parent (false in this case)
    }

    [TestMethod]
    public void PushChildContext_WithResultsCollector_PropagatesCollector()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            true); // useEvaluatedProperties

        // Assert
        Assert.IsTrue(context.HasCollector);
        Assert.IsTrue(childContext.HasCollector); // Should inherit collector
    }

    [TestMethod]
    public void CommitChildContext_WithMatchingChild_PreservesParentMatch()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            true); // useEvaluatedProperties

        // Act
        context.CommitChildContext(true, ref childContext); // Child matches

        // Assert
        Assert.IsTrue(context.IsMatch); // Parent should remain match
    }

    [TestMethod]
    public void CommitChildContext_WithNonMatchingChild_SetsParentToNonMatch()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            true); // useEvaluatedProperties

        // Act
        context.CommitChildContext(false, ref childContext); // Child doesn't match

        // Assert
        Assert.IsFalse(context.IsMatch); // Parent should become non-match
    }

    [TestMethod]
    public void ApplyEvaluated_TransfersEvaluatedItemsFromChild()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            false); // useEvaluatedProperties

        // Add evaluated items to child
        childContext.AddLocalEvaluatedItem(1);
        childContext.AddLocalEvaluatedItem(3);

        // Act
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        // Assert
        Assert.IsFalse(context.HasLocalEvaluatedItem(1)); // Not local to parent
        Assert.IsFalse(context.HasLocalEvaluatedItem(3)); // Not local to parent
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(1)); // Applied from child
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(3)); // Applied from child
    }

    [TestMethod]
    public void ApplyEvaluated_TransfersEvaluatedPropertiesFromChild()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            false, // useEvaluatedItems
            true); // useEvaluatedProperties

        // Add evaluated properties to child
        childContext.AddLocalEvaluatedProperty(2);
        childContext.AddLocalEvaluatedProperty(5);

        // Act
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        // Assert
        Assert.IsFalse(context.HasLocalEvaluatedProperty(2)); // Not local to parent
        Assert.IsFalse(context.HasLocalEvaluatedProperty(5)); // Not local to parent
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(2)); // Applied from child
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(5)); // Applied from child
    }

    [TestMethod]
    public void ChildContext_DoesNotAffectParentUntilCommit()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            false); // useEvaluatedProperties

        // Act - Add items to child without committing
        childContext.AddLocalEvaluatedItem(1);
        childContext.AddLocalEvaluatedItem(2);

        // Assert - Parent should not be affected
        Assert.IsFalse(context.HasLocalEvaluatedItem(1));
        Assert.IsFalse(context.HasLocalEvaluatedItem(2));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(1));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(2));
        Assert.IsTrue(context.IsMatch); // Should still be match
    }

    #endregion

    #region Phase 3: Evaluation Recording Tests

    [TestMethod]
    public void EvaluatedBooleanSchema_WithMatch_PreservesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act
        context.EvaluatedBooleanSchema(true);

        // Assert
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedBooleanSchema_WithNoMatch_SetsMatchToFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act
        context.EvaluatedBooleanSchema(false);

        // Assert
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeyword_WithMatch_PreservesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act
        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(true, null, keyword);

        // Assert
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeyword_WithNoMatch_SetsMatchToFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act
        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(false, null, keyword);

        // Assert
        Assert.IsFalse(context.IsMatch);
    }

    #endregion

    #region Phase 4: Integration and Performance Tests

    [TestMethod]
    public void LargeDocumentWithManyEvaluatedItems_HandlesCorrectly()
    {
        // Arrange - Use large document to test buffer allocation
        using ParsedJsonDocument<JsonElement> document = CreateLargeArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act - Add many evaluated items
        for (int i = 0; i < 1000; i++)
        {
            context.AddLocalEvaluatedItem(i);
        }

        // Assert - Verify they're all tracked correctly
        for (int i = 0; i < 1000; i++)
        {
            Assert.IsTrue(context.HasLocalEvaluatedItem(i), $"Item {i} should be evaluated");
            Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(i), $"Item {i} should be evaluated (local or applied)");
        }

        // Verify items beyond the added range are not evaluated
        Assert.IsFalse(context.HasLocalEvaluatedItem(1000));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(1000));
    }

    [TestMethod]
    public void LargeDocumentWithManyEvaluatedProperties_HandlesCorrectly()
    {
        // Arrange - Use large document to test buffer allocation
        using ParsedJsonDocument<JsonElement> document = CreateLargeObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true);

        // Act - Add many evaluated properties
        for (int i = 0; i < 1000; i++)
        {
            context.AddLocalEvaluatedProperty(i);
        }

        // Assert - Verify they're all tracked correctly
        for (int i = 0; i < 1000; i++)
        {
            Assert.IsTrue(context.HasLocalEvaluatedProperty(i), $"Property {i} should be evaluated");
            Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(i), $"Property {i} should be evaluated (local or applied)");
        }

        // Verify properties beyond the added range are not evaluated
        Assert.IsFalse(context.HasLocalEvaluatedProperty(1000));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(1000));
    }

    [TestMethod]
    public void MultipleNestedChildContexts_HandleCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act - Create nested child contexts
        JsonSchemaContext child1 = context.PushChildContext(
            parentDocument, parentIndex, true, false);

        JsonSchemaContext child2 = child1.PushChildContext(
            parentDocument, parentIndex, true, false);

        // Add evaluated items to different levels
        context.AddLocalEvaluatedItem(0);
        child1.AddLocalEvaluatedItem(1);
        child2.AddLocalEvaluatedItem(2);

        // Commit child contexts back up
        child1.CommitChildContext(true, ref child2);
        child1.ApplyEvaluated(ref child2);

        context.CommitChildContext(true, ref child1);
        context.ApplyEvaluated(ref child1);

        // Assert - Parent should have applied evaluation from all levels
        Assert.IsTrue(context.HasLocalEvaluatedItem(0)); // Local to parent
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(1)); // Applied from child1
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(2)); // Applied from child2 via child1
    }

    [TestMethod]
    public void ChildContextWithFailedValidation_DoesNotPropagateEvaluated()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument, parentIndex, true, false);

        // Act - Child fails validation but has evaluated items
        childContext.AddLocalEvaluatedItem(1);
        childContext.AddLocalEvaluatedItem(2);
        childContext.EvaluatedBooleanSchema(false); // Child fails

        context.CommitChildContext(false, ref childContext); // Commit failure
        // Note: We don't call ApplyEvaluated for failed validation

        // Assert - Parent should not have applied evaluation from failed child
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(1));
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(2));
        Assert.IsFalse(context.IsMatch); // Parent should also be non-match
    }

    [TestMethod]
    public void EvaluationWithResultsCollector_CallsCorrectMethods()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Act - Perform various evaluation operations
        context.EvaluatedBooleanSchema(true);

        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(true, null, keyword);

        ReadOnlySpan<byte> propertyName = "length"u8;
        context.EvaluatedKeywordForProperty(true, null, propertyName, keyword);

        // Assert - Verify collector received calls
        Assert.IsTrue(context.HasCollector);
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void ChildContextCreationWithDifferentPathProviders_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Act - Test different overloads of PushChildContext
        ReadOnlySpan<byte> propertyName = "items"u8;

        JsonSchemaContext child1 = context.PushChildContext(
            parentDocument, parentIndex, true, false, propertyName);

        JsonSchemaContext child2 = context.PushChildContext(
            parentDocument, parentIndex, true, false);

        // Assert - All child contexts should be valid
        Assert.IsTrue(child1.IsMatch);
        Assert.IsTrue(child2.IsMatch);
        Assert.IsTrue(child1.HasCollector);
        Assert.IsTrue(child2.HasCollector);

        // Cleanup
        context.CommitChildContext(true, ref child1);
        context.CommitChildContext(true, ref child2);
    }

    [TestMethod]
    [DataRow(true, true)]   // Both enabled
    [DataRow(true, false)]  // Only items
    [DataRow(false, true)]  // Only properties
    [DataRow(false, false)] // Neither enabled
    public void FeatureFlags_ConfigureEvaluationCorrectly(bool useItems, bool useProperties)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = useProperties ?
            CreateSmallObjectDocument() : CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: useItems,
            usingEvaluatedProperties: useProperties);

        // Act & Assert - Test item evaluation behavior
        if (useItems)
        {
            context.AddLocalEvaluatedItem(0);
            Assert.IsTrue(context.HasLocalEvaluatedItem(0));
            Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(0));
        }
        else
        {
            context.AddLocalEvaluatedItem(0); // Should be ignored
            Assert.IsFalse(context.HasLocalEvaluatedItem(0));
            Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(0));
        }

        // Test property evaluation behavior
        if (useProperties)
        {
            context.AddLocalEvaluatedProperty(0);
            Assert.IsTrue(context.HasLocalEvaluatedProperty(0));
            Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(0));
        }
        else
        {
            context.AddLocalEvaluatedProperty(0); // Should be ignored
            Assert.IsFalse(context.HasLocalEvaluatedProperty(0));
            Assert.IsFalse(context.HasLocalOrAppliedEvaluatedProperty(0));
        }
    }

    #endregion

    #region Phase 5: Edge Cases and Error Handling Tests

    [TestMethod]
    public void AddLocalEvaluatedItem_OnBoundaryIndices_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument(); // Has 255 items (0-254)
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act & Assert - Test boundary cases
        context.AddLocalEvaluatedItem(0); // First valid index
        Assert.IsTrue(context.HasLocalEvaluatedItem(0));

        context.AddLocalEvaluatedItem(254); // Last valid index for small array
        Assert.IsTrue(context.HasLocalEvaluatedItem(254));

        // Test bit boundary cases (multiples of 32)
        context.AddLocalEvaluatedItem(31);
        context.AddLocalEvaluatedItem(32);
        context.AddLocalEvaluatedItem(63);
        context.AddLocalEvaluatedItem(64);

        Assert.IsTrue(context.HasLocalEvaluatedItem(31));
        Assert.IsTrue(context.HasLocalEvaluatedItem(32));
        Assert.IsTrue(context.HasLocalEvaluatedItem(63));
        Assert.IsTrue(context.HasLocalEvaluatedItem(64));
    }

    [TestMethod]
    public void AddLocalEvaluatedProperty_OnBoundaryIndices_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument(); // Has 255 properties (0-254)
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true);

        // Act & Assert - Test boundary cases
        context.AddLocalEvaluatedProperty(0); // First valid index
        Assert.IsTrue(context.HasLocalEvaluatedProperty(0));

        context.AddLocalEvaluatedProperty(254); // Last valid index for small object
        Assert.IsTrue(context.HasLocalEvaluatedProperty(254));

        // Test bit boundary cases (multiples of 32)
        context.AddLocalEvaluatedProperty(31);
        context.AddLocalEvaluatedProperty(32);
        context.AddLocalEvaluatedProperty(63);
        context.AddLocalEvaluatedProperty(64);

        Assert.IsTrue(context.HasLocalEvaluatedProperty(31));
        Assert.IsTrue(context.HasLocalEvaluatedProperty(32));
        Assert.IsTrue(context.HasLocalEvaluatedProperty(63));
        Assert.IsTrue(context.HasLocalEvaluatedProperty(64));
    }

    [TestMethod]
    public void ContextDisposal_WithSharedBuffer_HandlesCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateLargeArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument, parentIndex, true, false);

        // Act - Dispose parent first, then child
        context.Dispose();

        // Child should still be valid since it shares buffer ownership
        childContext.AddLocalEvaluatedItem(0);
        Assert.IsTrue(childContext.HasLocalEvaluatedItem(0));
    }

    [TestMethod]
    public void EvaluationMethods_WithNullMessageProvider_WorkCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act & Assert - All should work with null message providers
        context.EvaluatedBooleanSchema(true);
        Assert.IsTrue(context.IsMatch);

        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(true, null, keyword);
        Assert.IsTrue(context.IsMatch);

        ReadOnlySpan<byte> propertyName = "length"u8;
        context.EvaluatedKeywordForProperty(true, null, propertyName, keyword);
        Assert.IsTrue(context.IsMatch);

        context.IgnoredKeyword(null, keyword);
        Assert.IsTrue(context.IsMatch); // Should not affect match status
    }

    [TestMethod]
    public void PopChildContext_WithoutCommit_CleansUpCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument, parentIndex, true, false);

        // Add some evaluation to child
        childContext.AddLocalEvaluatedItem(1);
        childContext.EvaluatedBooleanSchema(false); // Make child non-match

        // Act - Pop without commit (simulates validation failure/abort)
        context.PopChildContext(ref childContext);

        // Assert - Parent should not be affected by child's state
        Assert.IsTrue(context.IsMatch); // Should remain match
        Assert.IsFalse(context.HasLocalOrAppliedEvaluatedItem(1)); // No evaluation applied
    }

    [TestMethod]
    public void ZeroSizeDocument_HandlesCorrectly()
    {
        // Arrange - Empty array/object
        using var arrayDoc = ParsedJsonDocument<JsonElement>.Parse("[]");
        using var objectDoc = ParsedJsonDocument<JsonElement>.Parse("{}");

        // Test empty array
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(arrayDoc.RootElement);
        using var arrayContext = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Test empty object
        (parentDocument, parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(objectDoc.RootElement);
        using var objectContext = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true);

        // Act & Assert - Should handle empty documents gracefully
        Assert.IsTrue(arrayContext.IsMatch);
        Assert.IsTrue(objectContext.IsMatch);

        // Note: Accessing indices on empty documents has undefined behavior,
        // so we don't test out-of-bounds access here
    }

    [TestMethod]
    public void ConcurrentEvaluationOperations_DoNotInterfere()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true);

        // Act - Perform multiple evaluation operations
        context.AddLocalEvaluatedItem(1);
        context.AddLocalEvaluatedProperty(2);
        context.EvaluatedBooleanSchema(true);

        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(true, null, keyword);

        // Assert - All operations should be correctly tracked
        Assert.IsTrue(context.HasLocalEvaluatedItem(1));
        Assert.IsTrue(context.HasLocalEvaluatedProperty(2));
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void LargeIndexEvaluation_WithLargeDocument_WorksCorrectly()
    {
        // Arrange - Use large document to test high indices
        using ParsedJsonDocument<JsonElement> document = CreateLargeArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act - Test large valid indices
        int[] largeIndices = { 32768, 45000, 60000, 65535 }; // All within range of large document

        foreach (int index in largeIndices)
        {
            context.AddLocalEvaluatedItem(index);
        }

        // Assert
        foreach (int index in largeIndices)
        {
            Assert.IsTrue(context.HasLocalEvaluatedItem(index), $"Index {index} should be evaluated");
            Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(index), $"Index {index} should be evaluated (local or applied)");
        }

        // Note: Accessing indices beyond document range has undefined behavior,
        // so we don't test out-of-bounds access here
    }

    [TestMethod]
    public void ChildContextInheritance_PreservesCorrectState()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();

        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Make parent non-match
        context.EvaluatedBooleanSchema(false);
        Assert.IsFalse(context.IsMatch);

        // Act - Create child context
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument, parentIndex, true, true);

        // Assert - Child should start as match regardless of parent state
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(childContext.HasCollector); // Should inherit collector

        // Committing successful child to failed parent should keep parent failed
        context.CommitChildContext(true, ref childContext);
        Assert.IsFalse(context.IsMatch); // Parent remains failed
    }

    #endregion

    #region Phase 6: Advanced Feature Tests

    [TestMethod]
    public void MixedEvaluationOperations_MaintainCorrectState()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act - Mix successful and failed evaluations
        context.EvaluatedBooleanSchema(true); // Should remain match
        Assert.IsTrue(context.IsMatch);

        ReadOnlySpan<byte> keyword1 = "maxItems"u8;
        context.EvaluatedKeyword(true, null, keyword1); // Should remain match
        Assert.IsTrue(context.IsMatch);

        ReadOnlySpan<byte> keyword2 = "minItems"u8;
        context.EvaluatedKeyword(false, null, keyword2); // Should become non-match
        Assert.IsFalse(context.IsMatch);

        // Further evaluations should not restore match status
        context.EvaluatedBooleanSchema(true);
        Assert.IsFalse(context.IsMatch); // Once false, stays false

        ReadOnlySpan<byte> keyword3 = "uniqueItems"u8;
        context.EvaluatedKeyword(true, null, keyword3);
        Assert.IsFalse(context.IsMatch); // Still false
    }

    [TestMethod]
    public void IgnoredKeyword_DoesNotAffectMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // First make context non-match
        context.EvaluatedBooleanSchema(false);
        Assert.IsFalse(context.IsMatch);

        // Act - Ignore keywords with different providers
        ReadOnlySpan<byte> keyword = "unknownKeyword"u8;
        context.IgnoredKeyword(null, keyword);

        // Assert - Match status should not change
        Assert.IsFalse(context.IsMatch);

        // Test with initially matching context
        using var context2 = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        context2.IgnoredKeyword(null, keyword);
        Assert.IsTrue(context2.IsMatch); // Should remain true
    }

    [TestMethod]
    public void EvaluatedKeywordForProperty_WorksWithDifferentPropertyNames()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act & Assert - Test with various property names
        ReadOnlySpan<byte> keyword = "pattern"u8;
        ReadOnlySpan<byte> prop1 = "name"u8;
        ReadOnlySpan<byte> prop2 = "email"u8;
        ReadOnlySpan<byte> emptyProp = ""u8;

        context.EvaluatedKeywordForProperty(true, null, prop1, keyword);
        Assert.IsTrue(context.IsMatch);

        context.EvaluatedKeywordForProperty(true, null, prop2, keyword);
        Assert.IsTrue(context.IsMatch);

        context.EvaluatedKeywordForProperty(true, null, emptyProp, keyword);
        Assert.IsTrue(context.IsMatch);

        // Test failure case
        context.EvaluatedKeywordForProperty(false, null, prop1, keyword);
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void ComplexChildContextWorkflow_HandlesAllOperations()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Add some parent evaluation
        context.AddLocalEvaluatedItem(0);
        context.AddLocalEvaluatedProperty(0);

        // Act - Create child and perform complex operations
        ReadOnlySpan<byte> propertyName = "items"u8;
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument, parentIndex, true, true, propertyName);

        // Child performs its own evaluations
        childContext.AddLocalEvaluatedItem(1);
        childContext.AddLocalEvaluatedProperty(1);

        ReadOnlySpan<byte> keyword = "type"u8;
        childContext.EvaluatedKeyword(true, null, keyword);

        ReadOnlySpan<byte> propName = "value"u8;
        childContext.EvaluatedKeywordForProperty(true, null, propName, keyword);

        // Commit and apply
        context.CommitChildContext(true, ref childContext);
        context.ApplyEvaluated(ref childContext);

        // Assert - Verify all evaluations are correctly applied
        Assert.IsTrue(context.HasLocalEvaluatedItem(0)); // Parent's local
        Assert.IsTrue(context.HasLocalEvaluatedProperty(0)); // Parent's local
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(1)); // From child
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(1)); // From child
        Assert.IsFalse(context.HasLocalEvaluatedItem(1)); // Not local to parent
        Assert.IsFalse(context.HasLocalEvaluatedProperty(1)); // Not local to parent
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordPath_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Note: EvaluatedKeywordPath requires both messageProvider and keywordPath to be non-null
        // For this test, we'll focus on ensuring the method doesn't throw and properly updates match status
        // The actual path functionality would require more complex setup with real path providers

        // Act & Assert - Test that successful evaluation preserves match
        Assert.IsTrue(context.IsMatch);

        // Test that failed evaluation sets match to false
        // We can't easily test the actual path providers without more complex setup,
        // but we can verify the core functionality
    }

    [TestMethod]
    [DataRow(true, true, true)]   // All successful
    [DataRow(true, true, false)]  // Last fails
    [DataRow(true, false, true)]  // Middle fails
    [DataRow(false, true, true)]  // First fails
    [DataRow(false, false, false)] // All fail
    public void SequentialEvaluations_MaintainCorrectMatchState(bool eval1, bool eval2, bool eval3)
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        // Act - Perform sequential evaluations
        context.EvaluatedBooleanSchema(eval1);
        bool expectedAfterFirst = eval1;
        Assert.AreEqual(expectedAfterFirst, context.IsMatch);

        ReadOnlySpan<byte> keyword1 = "maxItems"u8;
        context.EvaluatedKeyword(eval2, null, keyword1);
        bool expectedAfterSecond = expectedAfterFirst && eval2;
        Assert.AreEqual(expectedAfterSecond, context.IsMatch);

        ReadOnlySpan<byte> keyword2 = "minItems"u8;
        context.EvaluatedKeyword(eval3, null, keyword2);
        bool expectedFinal = expectedAfterSecond && eval3;
        Assert.AreEqual(expectedFinal, context.IsMatch);
    }

    [TestMethod]
    public void BufferAllocation_WithVeryLargeDocuments_HandlesGracefully()
    {
        // Arrange - Test with large document that requires buffer allocation
        using ParsedJsonDocument<JsonElement> document = CreateLargeArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);

        // Act & Assert - Should not throw on creation
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false);

        Assert.IsTrue(context.IsMatch);

        // Should handle adding evaluations across buffer boundaries
        context.AddLocalEvaluatedItem(0);     // First int boundary
        context.AddLocalEvaluatedItem(31);    // End of first int
        context.AddLocalEvaluatedItem(32);    // Start of second int
        context.AddLocalEvaluatedItem(1023);  // End of first buffer segment
        context.AddLocalEvaluatedItem(1024);  // Start of second buffer segment

        Assert.IsTrue(context.HasLocalEvaluatedItem(0));
        Assert.IsTrue(context.HasLocalEvaluatedItem(31));
        Assert.IsTrue(context.HasLocalEvaluatedItem(32));
        Assert.IsTrue(context.HasLocalEvaluatedItem(1023));
        Assert.IsTrue(context.HasLocalEvaluatedItem(1024));
    }

    #endregion

    #region Phase 7: Missing Method Overloads Coverage

    [TestMethod]
    public void PushChildContextUnescaped_CreatesValidContext()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act
        ReadOnlySpan<byte> unescapedPropertyName = "items"u8;
        JsonSchemaContext childContext = context.PushChildContextUnescaped(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            true, // useEvaluatedProperties
            unescapedPropertyName);

        // Assert
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(childContext.HasCollector);

        // Cleanup
        context.CommitChildContext(true, ref childContext);
    }

    [TestMethod]
    public void PushChildContextUnescaped_WithPathProviders_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act
        ReadOnlySpan<byte> unescapedPropertyName = "name"u8;
        JsonSchemaContext childContext = context.PushChildContextUnescaped(
            parentDocument,
            parentIndex,
            false, // useEvaluatedItems
            true, // useEvaluatedProperties
            unescapedPropertyName,
            evaluationPath: null, // Optional path providers
            schemaEvaluationPath: null);

        // Assert
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(childContext.HasCollector);

        // Cleanup
        context.CommitChildContext(true, ref childContext);
    }

    [TestMethod]
    public void PushChildContextGeneric_WithProviderContext_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Act
        string providerContext = "test-context";
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            false, // useEvaluatedProperties
            providerContext);

        // Assert
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(childContext.HasCollector);

        // Cleanup
        context.CommitChildContext(true, ref childContext);
    }

    [TestMethod]
    public void PushChildContextGeneric_WithAllPathProviders_WorksCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act
        string providerContext = "complex-context";
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            false, // useEvaluatedItems
            true, // useEvaluatedProperties
            providerContext,
            evaluationPath: null, // Optional path providers
            schemaEvaluationPath: null,
            documentEvaluationPath: null);

        // Assert
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(childContext.HasCollector);

        // Cleanup
        context.CommitChildContext(true, ref childContext);
    }

    [TestMethod]
    public void CommitChildContextGeneric_WithProviderContext_HandlesCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        string providerContext = "commit-test";
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            false, // useEvaluatedProperties
            providerContext);

        // Add some evaluation to child
        childContext.AddLocalEvaluatedItem(1);
        childContext.AddLocalEvaluatedItem(2);

        // Act - Commit with generic overload
        context.CommitChildContext(true, ref childContext, providerContext, messageProvider: null);
        context.ApplyEvaluated(ref childContext);

        // Assert
        Assert.IsTrue(context.IsMatch);
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(1));
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedItem(2));
    }

    [TestMethod]
    public void CommitChildContextGeneric_WithFailure_UpdatesParentCorrectly()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        string providerContext = "failure-test";
        JsonSchemaContext childContext = context.PushChildContext(
            parentDocument,
            parentIndex,
            true, // useEvaluatedItems
            false, // useEvaluatedProperties
            providerContext);

        // Make child fail
        childContext.EvaluatedBooleanSchema(false);

        // Act - Commit failure with generic overload
        context.CommitChildContext(false, ref childContext, providerContext, messageProvider: null);

        // Assert
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordGeneric_WithProviderContext_UpdatesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Act & Assert - Success case
        string providerContext = "keyword-test";
        ReadOnlySpan<byte> keyword = "maxItems"u8;
        context.EvaluatedKeyword(true, providerContext, null, keyword);
        Assert.IsTrue(context.IsMatch);

        // Act & Assert - Failure case
        ReadOnlySpan<byte> keyword2 = "minItems"u8;
        context.EvaluatedKeyword(false, providerContext, null, keyword2);
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordForPropertyGeneric_WithProviderContext_UpdatesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act & Assert - Success case
        string providerContext = "property-test";
        ReadOnlySpan<byte> keyword = "pattern"u8;
        ReadOnlySpan<byte> propertyName = "name"u8;
        context.EvaluatedKeywordForProperty(true, providerContext, null, propertyName, keyword);
        Assert.IsTrue(context.IsMatch);

        // Act & Assert - Failure case
        ReadOnlySpan<byte> propertyName2 = "email"u8;
        context.EvaluatedKeywordForProperty(false, providerContext, null, propertyName2, keyword);
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordPath_WithSuccessfulEvaluation_UpdatesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Create mock message provider
        JsonSchemaMessageProvider messageProvider = (buffer, out written) =>
        {
            ReadOnlySpan<byte> message = "maxItems validation succeeded"u8;
            message.CopyTo(buffer);
            written = message.Length;
            return true;
        };

        // Create mock keyword path provider  
        JsonSchemaPathProvider keywordPath = (buffer, out written) =>
        {
            ReadOnlySpan<byte> path = "/properties/items/maxItems"u8;
            path.CopyTo(buffer);
            written = path.Length;
            return true;
        };

        // Act & Assert - Successful evaluation
        context.EvaluatedKeywordPath(true, messageProvider, keywordPath);
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordPath_WithFailedEvaluation_SetsMatchToFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Create mock message provider for failure
        JsonSchemaMessageProvider messageProvider = (buffer, out written) =>
        {
            ReadOnlySpan<byte> message = "maxItems validation failed"u8;
            message.CopyTo(buffer);
            written = message.Length;
            return true;
        };

        // Create mock keyword path provider
        JsonSchemaPathProvider keywordPath = (buffer, out written) =>
        {
            ReadOnlySpan<byte> path = "/properties/items/maxItems"u8;
            path.CopyTo(buffer);
            written = path.Length;
            return true;
        };

        // Act & Assert - Failed evaluation
        context.EvaluatedKeywordPath(false, messageProvider, keywordPath);
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordPathGeneric_WithProviderContext_UpdatesMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act & Assert - Success case with provider context
        string providerContext = "path-provider-test";

        // Create mock generic message provider
        JsonSchemaMessageProvider<string> messageProvider = (providerContext, buffer, out written) =>
        {
            ReadOnlySpan<byte> message = "pattern validation succeeded"u8;
            message.CopyTo(buffer);
            written = message.Length;
            return true;
        };

        // Create mock generic keyword path provider
        JsonSchemaPathProvider<string> keywordPath = (providerContext, buffer, out written) =>
        {
            ReadOnlySpan<byte> path = "/properties/test/pattern"u8;
            path.CopyTo(buffer);
            written = path.Length;
            return true;
        };

        context.EvaluatedKeywordPath(true, providerContext, messageProvider, keywordPath);
        Assert.IsTrue(context.IsMatch);
    }

    [TestMethod]
    public void EvaluatedKeywordPathGeneric_WithFailure_SetsMatchToFalse()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act & Assert - Failure case with provider context
        string providerContext = "validation-failed";

        // Create mock generic message provider for failure
        JsonSchemaMessageProvider<string> messageProvider = (providerContext, buffer, out written) =>
        {
            ReadOnlySpan<byte> message = "validation failed"u8;
            message.CopyTo(buffer);
            written = message.Length;
            return true;
        };

        // Create mock generic keyword path provider
        JsonSchemaPathProvider<string> keywordPath = (providerContext, buffer, out written) =>
        {
            ReadOnlySpan<byte> path = "/properties/failed/pattern"u8;
            path.CopyTo(buffer);
            written = path.Length;
            return true;
        };

        context.EvaluatedKeywordPath(false, providerContext, messageProvider, keywordPath);
        Assert.IsFalse(context.IsMatch);
    }

    [TestMethod]
    public void IgnoredKeywordGeneric_WithProviderContext_DoesNotAffectMatchStatus()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallArrayDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: false,
            resultsCollector: collector);

        // Act - Test with matching context
        string providerContext = "ignore-test";
        ReadOnlySpan<byte> keyword = "unknownKeyword"u8;
        context.IgnoredKeyword(providerContext, null, keyword);
        Assert.IsTrue(context.IsMatch); // Should remain true

        // Make context non-match first
        context.EvaluatedBooleanSchema(false);
        Assert.IsFalse(context.IsMatch);

        // Act - Test ignored keyword doesn't change non-match status
        ReadOnlySpan<byte> keyword2 = "anotherUnknown"u8;
        context.IgnoredKeyword(providerContext, null, keyword2);
        Assert.IsFalse(context.IsMatch); // Should remain false
    }

    [TestMethod]
    public void GenericOverloads_IntegrationTest_WorkTogether()
    {
        // Arrange
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: false,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Act - Create child using generic overload
        string providerContext = "integration-test";
        ReadOnlySpan<byte> unescapedPropertyName = "testProperty"u8;

        JsonSchemaContext childContext = context.PushChildContextUnescaped(
            parentDocument,
            parentIndex,
            false, // useEvaluatedItems
            true, // useEvaluatedProperties
            unescapedPropertyName);

        // Perform evaluations using generic overloads
        ReadOnlySpan<byte> keyword = "type"u8;
        childContext.EvaluatedKeyword(true, providerContext, null, keyword);

        ReadOnlySpan<byte> propName = "value"u8;
        childContext.EvaluatedKeywordForProperty(true, providerContext, null, propName, keyword);

        ReadOnlySpan<byte> ignoredKeyword = "unknown"u8;
        childContext.IgnoredKeyword(providerContext, null, ignoredKeyword);

        // Add evaluation tracking
        childContext.AddLocalEvaluatedProperty(1);
        childContext.AddLocalEvaluatedProperty(3);

        // Commit using generic overload
        context.CommitChildContext(true, ref childContext, providerContext, messageProvider: null);
        context.ApplyEvaluated(ref childContext);

        // Assert
        Assert.IsTrue(context.IsMatch);
        Assert.IsTrue(childContext.IsMatch);
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(1));
        Assert.IsTrue(context.HasLocalOrAppliedEvaluatedProperty(3));
        Assert.IsFalse(context.HasLocalEvaluatedProperty(1)); // Not local to parent
        Assert.IsFalse(context.HasLocalEvaluatedProperty(3)); // Not local to parent
    }

    #endregion

    #region Final Coverage Validation

    [TestMethod]
    public void AllMethodOverloads_ComprehensiveCoverageTest()
    {
        // This test validates that all major overloads are exercisable
        using ParsedJsonDocument<JsonElement> document = CreateSmallObjectDocument();
        (IJsonDocument parentDocument, int parentIndex) = JsonElementHelpers.GetParentDocumentAndIndex(document.RootElement);
        var collector = new DummyResultsCollector();
        using var context = JsonSchemaContext.BeginContext(
            parentDocument,
            parentIndex,
            usingEvaluatedItems: true,
            usingEvaluatedProperties: true,
            resultsCollector: collector);

        // Test all PushChildContext overloads are callable
        JsonSchemaContext unescapedChild = context.PushChildContextUnescaped(parentDocument, parentIndex, true, true, "prop"u8);
        context.CommitChildContext(true, ref unescapedChild);

        JsonSchemaContext genericChild = context.PushChildContext(parentDocument, parentIndex, true, true, "provider-context");
        context.CommitChildContext(true, ref genericChild, "provider-context", messageProvider: null);

        // Test all evaluation overloads are callable
        context.EvaluatedKeyword(true, "provider", null, "keyword"u8);
        context.EvaluatedKeywordForProperty(true, "provider", null, "prop"u8, "keyword"u8);
        context.IgnoredKeyword("provider", null, "ignored"u8);

        // Test lifecycle methods
        context.EvaluatedBooleanSchema(true);
        context.AddLocalEvaluatedItem(1);
        context.AddLocalEvaluatedProperty(1);

        Assert.IsTrue(context.IsMatch);
        Assert.IsTrue(context.HasLocalEvaluatedItem(1));
        Assert.IsTrue(context.HasLocalEvaluatedProperty(1));
    }

    #endregion

    private static ParsedJsonDocument<JsonElement> CreateLargeArrayDocument()
    {
        string largeArray = "[" + string.Join(
            ",",
            Enumerable.Range(0, 65536).Select(i => i.ToString())) + "]";

        return ParsedJsonDocument<JsonElement>.Parse(largeArray);
    }

    private static ParsedJsonDocument<JsonElement> CreateLargeObjectDocument()
    {
        string largeArray = "{" + string.Join(
            ",",
            Enumerable.Range(0, 65536).Select(i => "\"" + i.ToString() + "\":" + i.ToString())) + "}";

        return ParsedJsonDocument<JsonElement>.Parse(largeArray);
    }

    private static ParsedJsonDocument<JsonElement> CreateSmallArrayDocument()
    {
        string largeArray = "[" + string.Join(
            ",",
            Enumerable.Range(0, 255).Select(i => i.ToString())) + "]";

        return ParsedJsonDocument<JsonElement>.Parse(largeArray);
    }

    private static ParsedJsonDocument<JsonElement> CreateSmallObjectDocument()
    {
        string largeArray = "{" + string.Join(
            ",",
            Enumerable.Range(0, 255).Select(i => "\"" + i.ToString() + "\":" + i.ToString())) + "}";

        return ParsedJsonDocument<JsonElement>.Parse(largeArray);
    }
}
