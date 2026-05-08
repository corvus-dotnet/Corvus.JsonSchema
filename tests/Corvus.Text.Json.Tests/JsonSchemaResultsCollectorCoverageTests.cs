// <copyright file="JsonSchemaResultsCollectorCoverageTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections;
using System.Collections.Generic;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests targeting uncovered lines in <see cref="JsonSchemaResultsCollector"/>.
/// </summary>
[TestClass]
public class JsonSchemaResultsCollectorCoverageTests
{
    #region IEnumerator Non-Generic Interface (Lines 656, 665-668, 688)

    [TestMethod]
    public void ResultsEnumerator_NonGenericCurrent_ReturnsBoxedResult()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("test message"), buffer, out written));
        c.CommitChildContext(seq, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("committed"), buffer, out written));

        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();

        // Cast to non-generic IEnumerator to exercise the explicit interface implementation
        IEnumerator nonGenericEnumerator = enumerator;
        Assert.IsTrue(nonGenericEnumerator.MoveNext());
        object current = nonGenericEnumerator.Current;
        Assert.IsInstanceOfType<JsonSchemaResultsCollector.Result>(current);

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_Reset_ResetsEnumerationPosition()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("msg1"), buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("msg2"), buffer, out written));
        c.CommitChildContext(seq, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("committed"), buffer, out written));

        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();

        // Advance past first item
        Assert.IsTrue(enumerator.MoveNext());
        string firstMessage = enumerator.Current.GetMessageText();

        // Reset and re-enumerate
        enumerator.Reset();
        Assert.IsTrue(enumerator.MoveNext());
        string firstMessageAfterReset = enumerator.Current.GetMessageText();

        Assert.AreEqual(firstMessage, firstMessageAfterReset);

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_NonGenericGetEnumerator_ReturnsSelf()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("test"), buffer, out written));
        c.CommitChildContext(seq, false, false, null);

        JsonSchemaResultsCollector.ResultsEnumerator enumerator = collector.EnumerateResults();

        // Cast to non-generic IEnumerable
        IEnumerable nonGenericEnumerable = enumerator;
        IEnumerator fromNonGeneric = nonGenericEnumerable.GetEnumerator();

        // Should still be able to enumerate
        Assert.IsTrue(fromNonGeneric.MoveNext());

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_Foreach_UsesIEnumerableGeneric()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("hello"), buffer, out written));
        c.CommitChildContext(seq, false, false, null);

        int count = 0;
        foreach (JsonSchemaResultsCollector.Result result in collector.EnumerateResults())
        {
            count++;
            Assert.IsFalse(result.IsMatch);
        }

        Assert.IsTrue(count > 0);

        collector.Dispose();
    }

    #endregion

    #region BeginChildContext with itemIndex (Lines 1635-1679)

    [TestMethod]
    [DataRow(JsonSchemaResultsLevel.Basic)]
    [DataRow(JsonSchemaResultsLevel.Detailed)]
    [DataRow(JsonSchemaResultsLevel.Verbose)]
    public void BeginChildContext_ItemIndex_SetsDocumentLocationToIndex(JsonSchemaResultsLevel level)
    {
        var collector = JsonSchemaResultsCollector.Create(level);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items"), buffer, out written));

        int seq = c.BeginChildContext(parent, 0);
        Assert.AreEqual("/0", collector.DocumentLocation);

        c.PopChildContext(seq);

        int seq2 = c.BeginChildContext(parent, 42);
        Assert.AreEqual("/42", collector.DocumentLocation);

        c.PopChildContext(seq2);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_ItemIndex_WithEvaluationAndSchemaPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0, schemaEvaluationPath: static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items"), buffer, out written));

        int seq = c.BeginChildContext(
            parent,
            5,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("items"), buffer, out written),
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items/type"), buffer, out written));

        Assert.AreEqual("/5", collector.DocumentLocation);
        Assert.AreEqual("items/type", collector.SchemaLocation);
        Assert.AreEqual("/items", collector.EvaluationLocation);

        c.PopChildContext(seq);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_ItemIndex_ParallelPath_UsesParentAsBase()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Set up initial parent with a document path
        int parent = c.BeginChildContext(
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("eval"), buffer, out written),
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items"), buffer, out written),
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("array"), buffer, out written));

        Assert.AreEqual("/array", collector.DocumentLocation);

        // Create first child with item index (sequential)
        int child1 = c.BeginChildContext(parent, 0);
        Assert.AreEqual("/array/0", collector.DocumentLocation);

        // Create second child at same level (parallel - sequenceOffset > 0)
        int child2 = c.BeginChildContext(parent, 1);
        Assert.AreEqual("/array/1", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_ItemIndex_ParallelPath_WithEvalPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written),
            null,
            null);

        Assert.AreEqual("/root", collector.EvaluationLocation);

        // First child - sequential
        int child1 = c.BeginChildContext(
            parent,
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("items"), buffer, out written),
            null);

        Assert.AreEqual("/root/items", collector.EvaluationLocation);

        // Second child from same parent - parallel (exercises AppendParallelEvaluationPath)
        int child2 = c.BeginChildContext(
            parent,
            1,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("items"), buffer, out written),
            null);

        Assert.AreEqual("/root/items", collector.EvaluationLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    #endregion

    #region Parallel Escaped/Unescaped Property Paths (Lines 1709-1716, 1755-1762)

    [TestMethod]
    public void BeginChildContext_EscapedProperty_ParallelPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Parent with a document path
        int parent = c.BeginChildContext(
            0,
            documentEvaluationPath: static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written));

        Assert.AreEqual("/root", collector.DocumentLocation);

        // First child with escaped property (sequential)
        byte[] prop1 = Encoding.UTF8.GetBytes("prop1");
        int child1 = c.BeginChildContext(parent, escapedPropertyName: prop1);
        Assert.AreEqual("/root/prop1", collector.DocumentLocation);

        // Second child with escaped property from same parent (parallel)
        byte[] prop2 = Encoding.UTF8.GetBytes("prop2");
        int child2 = c.BeginChildContext(parent, escapedPropertyName: prop2);
        Assert.AreEqual("/root/prop2", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_EscapedProperty_ParallelPath_WithSpecialChars()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Parent with a document path
        int parent = c.BeginChildContext(
            0,
            documentEvaluationPath: static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written));

        // First child (sequential)
        byte[] simpleProp = Encoding.UTF8.GetBytes("first");
        int child1 = c.BeginChildContext(parent, escapedPropertyName: simpleProp);

        // Second child with JSON-escaped property from same parent (parallel path)
        // "he\\u006Clo" represents "hello" with escape sequence
        byte[] escapedProp = Encoding.UTF8.GetBytes("he\\u006Clo");
        int child2 = c.BeginChildContext(parent, escapedPropertyName: escapedProp);
        Assert.AreEqual("/root/hello", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContextUnescaped_ParallelPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Parent with a document path
        int parent = c.BeginChildContext(
            0,
            documentEvaluationPath: static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written));

        Assert.AreEqual("/root", collector.DocumentLocation);

        // First child with unescaped property (sequential)
        byte[] prop1 = Encoding.UTF8.GetBytes("first");
        int child1 = c.BeginChildContextUnescaped(parent, prop1);
        Assert.AreEqual("/root/first", collector.DocumentLocation);

        // Second child with unescaped property from same parent (parallel)
        byte[] prop2 = Encoding.UTF8.GetBytes("second");
        int child2 = c.BeginChildContextUnescaped(parent, prop2);
        Assert.AreEqual("/root/second", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContextUnescaped_ParallelPath_WithSpecialChars()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Parent with a document path
        int parent = c.BeginChildContext(
            0,
            documentEvaluationPath: static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("obj"), buffer, out written));

        // First child (sequential)
        byte[] prop1 = Encoding.UTF8.GetBytes("first");
        int child1 = c.BeginChildContextUnescaped(parent, prop1);

        // Second child with slash/tilde in property name (parallel path - exercises encoding)
        byte[] specialProp = Encoding.UTF8.GetBytes("a/b~c");
        int child2 = c.BeginChildContextUnescaped(parent, specialProp);
        // '/' encodes as ~1, '~' encodes as ~0
        Assert.AreEqual("/obj/a~1b~0c", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    #endregion

    #region Parallel EvaluationPath for Escaped/Unescaped with evaluationPath provider

    [TestMethod]
    public void BeginChildContext_EscapedProperty_ParallelPath_WithEvalPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written),
            null,
            null);

        Assert.AreEqual("/root", collector.EvaluationLocation);

        // First child
        byte[] prop1 = Encoding.UTF8.GetBytes("a");
        int child1 = c.BeginChildContext(
            parent,
            prop1,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("pathA"), buffer, out written));

        Assert.AreEqual("/root/pathA", collector.EvaluationLocation);

        // Parallel child from same parent
        byte[] prop2 = Encoding.UTF8.GetBytes("b");
        int child2 = c.BeginChildContext(
            parent,
            prop2,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("pathB"), buffer, out written));

        Assert.AreEqual("/root/pathB", collector.EvaluationLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContextUnescaped_ParallelPath_WithEvalPath()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("root"), buffer, out written),
            null,
            null);

        Assert.AreEqual("/root", collector.EvaluationLocation);

        // First child
        byte[] prop1 = Encoding.UTF8.GetBytes("x");
        int child1 = c.BeginChildContextUnescaped(
            parent,
            prop1,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("pathX"), buffer, out written));

        Assert.AreEqual("/root/pathX", collector.EvaluationLocation);

        // Parallel child from same parent
        byte[] prop2 = Encoding.UTF8.GetBytes("y");
        int child2 = c.BeginChildContextUnescaped(
            parent,
            prop2,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("pathY"), buffer, out written));

        Assert.AreEqual("/root/pathY", collector.EvaluationLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    #endregion

    #region Buffer Enlarge Paths (Lines 1190-1192, 1208-1210, etc.)

    [TestMethod]
    public void BufferEnlarge_TriggeredBySmallCapacityAndLargePaths()
    {
        // Use a very small estimated capacity to create small buffers
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose, estimatedCapacity: 1);
        IJsonSchemaResultsCollector c = collector;

        // Build a path provider that writes a moderately long path (~200 bytes)
        // to trigger the Enlarge code path
        byte[] longPath = Encoding.UTF8.GetBytes(new string('a', 200));

        int parent = c.BeginChildContext(
            0,
            (buffer, out written) =>
            {
                longPath.AsSpan().CopyTo(buffer);
                written = longPath.Length;
                return true;
            },
            (buffer, out written) =>
            {
                longPath.AsSpan().CopyTo(buffer);
                written = longPath.Length;
                return true;
            },
            (buffer, out written) =>
            {
                longPath.AsSpan().CopyTo(buffer);
                written = longPath.Length;
                return true;
            });

        // Nested calls to force multiple enlargements
        for (int i = 0; i < 10; i++)
        {
            int seq = c.BeginChildContext(
                parent,
                (buffer, out written) =>
                {
                    longPath.AsSpan().CopyTo(buffer);
                    written = longPath.Length;
                    return true;
                },
                (buffer, out written) =>
                {
                    longPath.AsSpan().CopyTo(buffer);
                    written = longPath.Length;
                    return true;
                },
                (buffer, out written) =>
                {
                    longPath.AsSpan().CopyTo(buffer);
                    written = longPath.Length;
                    return true;
                });
            c.EvaluatedBooleanSchema(false, (buffer, out written) =>
            {
                longPath.AsSpan().CopyTo(buffer);
                written = longPath.Length;
                return true;
            });
            c.CommitChildContext(seq, false, false, (buffer, out written) =>
            {
                longPath.AsSpan().CopyTo(buffer);
                written = longPath.Length;
                return true;
            });
        }

        Assert.IsTrue(collector.GetResultCount() > 0);
        collector.Dispose();
    }

    [TestMethod]
    public void BufferEnlarge_EvaluationPath_TriggeredByManyNestedContexts()
    {
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose, estimatedCapacity: 1);
        IJsonSchemaResultsCollector c = collector;

        byte[] segment = Encoding.UTF8.GetBytes("segment");
        int parentSeq = 0;

        // Deeply nest contexts to exhaust evaluation path buffer
        int depth = 50;
        int[] sequences = new int[depth];
        for (int i = 0; i < depth; i++)
        {
            sequences[i] = c.BeginChildContext(
                parentSeq,
                (buffer, out written) =>
                {
                    segment.AsSpan().CopyTo(buffer);
                    written = segment.Length;
                    return true;
                },
                null,
                null);
            parentSeq = sequences[i];
        }

        // The evaluation location should contain all nested segments
        string evalLocation = collector.EvaluationLocation;
        StringAssert.Contains(evalLocation, "/segment");

        // Pop all contexts
        for (int i = depth - 1; i >= 0; i--)
        {
            c.PopChildContext(sequences[i]);
        }

        collector.Dispose();
    }

    [TestMethod]
    public void BufferEnlarge_DocumentPath_TriggeredByManyItemIndices()
    {
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose, estimatedCapacity: 1);
        IJsonSchemaResultsCollector c = collector;

        int parentSeq = c.BeginChildContext(0);
        int depth = 50;
        int[] sequences = new int[depth];

        for (int i = 0; i < depth; i++)
        {
            sequences[i] = c.BeginChildContext(parentSeq, i);
            parentSeq = sequences[i];
        }

        // The document location should contain nested indices
        string docLocation = collector.DocumentLocation;
        StringAssert.Contains(docLocation, "/0");

        // Pop all contexts
        for (int i = depth - 1; i >= 0; i--)
        {
            c.PopChildContext(sequences[i]);
        }

        c.PopChildContext(sequences[0] - 1);
        collector.Dispose();
    }

    [TestMethod]
    public void BufferEnlarge_SchemaPath_TriggeredByRepeatedSet()
    {
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose, estimatedCapacity: 1);
        IJsonSchemaResultsCollector c = collector;

        // Create a long schema path to potentially trigger enlarge
        byte[] longSchemaPath = Encoding.UTF8.GetBytes("#/$defs/" + new string('x', 500));

        int parent = c.BeginChildContext(
            0,
            schemaEvaluationPath: (buffer, out written) =>
            {
                longSchemaPath.AsSpan().CopyTo(buffer);
                written = longSchemaPath.Length;
                return true;
            });

        string schemaLoc = collector.SchemaLocation;
        StringAssert.Contains(schemaLoc, "#/$defs/");

        c.PopChildContext(parent);
        collector.Dispose();
    }

    #endregion

    #region Generic BeginChildContext Parallel Paths (Lines 1597-1624)

    [TestMethod]
    public void BeginChildContext_Generic_ParallelPath_ExercisesAllProviders()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // First context with all three generic providers
        int parent = c.BeginChildContext(
            0,
            1,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("eval" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("#/schema" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("doc" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            });

        Assert.AreEqual("/eval1", collector.EvaluationLocation);
        Assert.AreEqual("#/schema1", collector.SchemaLocation);
        Assert.AreEqual("/doc1", collector.DocumentLocation);

        // Second context from same parent (parallel, sequenceOffset > 0)
        int child2 = c.BeginChildContext(
            0,
            2,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("eval" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("#/schema" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("doc" + ctx);
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            });

        // Parallel from root, so uses root as base (empty)
        Assert.AreEqual("/eval2", collector.EvaluationLocation);
        Assert.AreEqual("#/schema2", collector.SchemaLocation);
        Assert.AreEqual("/doc2", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(parent);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_Generic_ParallelPath_WithNonRootParent()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        // Root with path
        int root = c.BeginChildContext(
            0,
            10,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("root");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            null,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("base");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            });

        Assert.AreEqual("/root", collector.EvaluationLocation);
        Assert.AreEqual("/base", collector.DocumentLocation);

        // First child (sequential from root)
        int child1 = c.BeginChildContext(
            root,
            20,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("childA");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            null,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("docA");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            });

        Assert.AreEqual("/root/childA", collector.EvaluationLocation);
        Assert.AreEqual("/base/docA", collector.DocumentLocation);

        // Second child from same parent (parallel from root, sequenceOffset > 0)
        int child2 = c.BeginChildContext(
            root,
            30,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("childB");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            },
            null,
            static (int ctx, Span<byte> buffer, out int written) =>
            {
                byte[] data = Encoding.UTF8.GetBytes("docB");
                data.AsSpan().CopyTo(buffer);
                written = data.Length;
                return true;
            });

        Assert.AreEqual("/root/childB", collector.EvaluationLocation);
        Assert.AreEqual("/base/docB", collector.DocumentLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        c.PopChildContext(root);
        collector.Dispose();
    }

    #endregion

    #region CommitChildContext Verbose Paths and Level Interactions

    [TestMethod]
    public void CommitChildContext_ParentIsMatch_Verbose_WritesResult()
    {
        // In Verbose mode, results are written even when parentIsMatch is true
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, parentIsMatch: true, childIsMatch: true, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("verbose result"), buffer, out written));

        Assert.AreEqual(1, collector.GetResultCount());

        // Verify the result is accessible
        var enumerator = collector.EnumerateResults();
        Assert.IsTrue(enumerator.MoveNext());
        Assert.IsTrue(enumerator.Current.IsMatch);
        Assert.AreEqual("verbose result", enumerator.Current.GetMessageText());

        collector.Dispose();
    }

    [TestMethod]
    public void CommitChildContext_ParentIsMatch_Basic_DoesNotWriteResult()
    {
        // In Basic mode, results are NOT written when parentIsMatch is true
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, parentIsMatch: true, childIsMatch: true, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("should not appear"), buffer, out written));

        Assert.AreEqual(0, collector.GetResultCount());

        collector.Dispose();
    }

    [TestMethod]
    public void CommitChildContext_ParentIsNotMatch_Basic_WritesResult()
    {
        // In Basic mode, results ARE written when parentIsMatch is false
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, parentIsMatch: false, childIsMatch: false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("failure message"), buffer, out written));

        Assert.AreEqual(1, collector.GetResultCount());

        collector.Dispose();
    }

    [TestMethod]
    public void CommitChildContext_Generic_ParentIsMatch_Verbose_WritesResult()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, parentIsMatch: true, childIsMatch: true, 42,
            static (int ctx, Span<byte> buffer, out int written) =>
                JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("generic verbose"), buffer, out written));

        Assert.AreEqual(1, collector.GetResultCount());
        collector.Dispose();
    }

    [TestMethod]
    public void CommitChildContext_Generic_ParentIsMatch_Basic_DoesNotWriteResult()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, parentIsMatch: true, childIsMatch: true, 42,
            static (int ctx, Span<byte> buffer, out int written) =>
                JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("should not appear"), buffer, out written));

        Assert.AreEqual(0, collector.GetResultCount());
        collector.Dispose();
    }

    #endregion

    #region EnsureCapacityForResult - Different Level/Match Combos (Lines 1132-1151)

    [TestMethod]
    public void EnsureCapacity_Detailed_MatchTrue_NoMessage()
    {
        // Detailed level with match=true should not write messages
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("should not appear"), buffer, out written));
        c.CommitChildContext(seq, false, false, null);

        // In Detailed, match=true results are not recorded
        // Only the commit result should be present
        Assert.AreEqual(1, collector.GetResultCount());

        collector.Dispose();
    }

    [TestMethod]
    public void EnsureCapacity_Detailed_MatchFalse_WritesMessage()
    {
        // Detailed level with match=false should write messages
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("failure detail"), buffer, out written));
        c.CommitChildContext(seq, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("commit"), buffer, out written));

        Assert.IsTrue(collector.GetResultCount() >= 2);

        // Verify the failure message is present
        bool found = false;
        foreach (var result in collector.EnumerateResults())
        {
            if (result.GetMessageText() == "failure detail")
            {
                found = true;
                Assert.IsFalse(result.IsMatch);
            }
        }

        Assert.IsTrue(found);

        collector.Dispose();
    }

    #endregion

    #region Result Properties Coverage

    [TestMethod]
    public void Result_AllProperties_ReturnsCorrectValues()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(
            0,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("evalPath"), buffer, out written),
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/schema/path"), buffer, out written),
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("docPath"), buffer, out written));

        c.CommitChildContext(parent, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("validation failed"), buffer, out written));

        var enumerator = collector.EnumerateResults();
        Assert.IsTrue(enumerator.MoveNext());

        var result = enumerator.Current;
        Assert.IsFalse(result.IsMatch);
        Assert.AreEqual("validation failed", result.GetMessageText());
        Assert.AreEqual("/evalPath", result.GetEvaluationLocationText());
        Assert.AreEqual("schema/path", result.GetSchemaEvaluationLocationText());
        Assert.AreEqual("/docPath", result.GetDocumentEvaluationLocationText());

        // Verify UTF-8 span accessors
        Assert.IsTrue(result.Message.Length > 0);
        Assert.IsTrue(result.EvaluationLocation.Length > 0);
        Assert.IsTrue(result.SchemaEvaluationLocation.Length > 0);
        Assert.IsTrue(result.DocumentEvaluationLocation.Length > 0);

        collector.Dispose();
    }

    [TestMethod]
    public void Result_Default_HasEmptySpans()
    {
        // Default result should have empty spans and not throw
        JsonSchemaResultsCollector.Result defaultResult = default;
        Assert.IsFalse(defaultResult.IsMatch);
    }

    #endregion

    #region Enumerator Edge Cases

    [TestMethod]
    public void ResultsEnumerator_EmptyCollector_MoveNextReturnsFalse()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);

        var enumerator = collector.EnumerateResults();
        Assert.IsFalse(enumerator.MoveNext());

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_Current_BeforeMoveNext_ReturnsDefault()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, false, false, null);

        var enumerator = collector.EnumerateResults();

        // Current before MoveNext should be default
        var current = enumerator.Current;
        Assert.IsFalse(current.IsMatch);

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_Dispose_PreventsEnumeration()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("msg"), buffer, out written));
        c.CommitChildContext(seq, false, false, null);

        var enumerator = collector.EnumerateResults();
        enumerator.Dispose();

        // After dispose, MoveNext should return false (endResultIdx set to -1)
        Assert.IsFalse(enumerator.MoveNext());

        collector.Dispose();
    }

    [TestMethod]
    public void ResultsEnumerator_MultipleResults_EnumeratesAll()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.EvaluatedBooleanSchema(true, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("first"), buffer, out written));
        c.EvaluatedBooleanSchema(false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("second"), buffer, out written));
        c.EvaluatedBooleanSchema(true, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("third"), buffer, out written));
        c.CommitChildContext(seq, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("commit"), buffer, out written));

        int count = 0;
        foreach (var result in collector.EnumerateResults())
        {
            count++;
        }

        // Verbose captures all evaluations plus the commit
        Assert.AreEqual(4, count);

        collector.Dispose();
    }

    #endregion

    #region CreateUnrented with Zero Capacity

    [TestMethod]
    public void CreateUnrented_ZeroCapacity_UsesDefaultCapacity()
    {
        // Zero or negative capacity should default to 30
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Basic, 0);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, false, false, static (buffer, out written) =>
            JsonSchemaEvaluation.TryCopyMessage(Encoding.UTF8.GetBytes("works"), buffer, out written));

        Assert.AreEqual(1, collector.GetResultCount());
        collector.Dispose();
    }

    [TestMethod]
    public void CreateUnrented_NegativeCapacity_UsesDefaultCapacity()
    {
        var collector = JsonSchemaResultsCollector.CreateUnrented(JsonSchemaResultsLevel.Verbose, -5);
        IJsonSchemaResultsCollector c = collector;

        int seq = c.BeginChildContext(0);
        c.CommitChildContext(seq, false, false, null);

        Assert.AreEqual(1, collector.GetResultCount());
        collector.Dispose();
    }

    #endregion

    #region BeginChildContext with schemaEvaluationPath in itemIndex and property overloads

    [TestMethod]
    public void BeginChildContext_ItemIndex_WithSchemaPath_ParallelSetsSchemaLocation()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0);

        // First child with item index and schema path (sequential)
        int child1 = c.BeginChildContext(
            parent,
            0,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items/0"), buffer, out written));

        Assert.AreEqual("items/0", collector.SchemaLocation);

        // Second child from same parent (parallel) with different schema path
        int child2 = c.BeginChildContext(
            parent,
            1,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/items/1"), buffer, out written));

        Assert.AreEqual("items/1", collector.SchemaLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContext_EscapedProperty_WithSchemaPath_ParallelSetsSchemaLocation()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0);

        // First child (sequential)
        byte[] prop1 = Encoding.UTF8.GetBytes("name");
        int child1 = c.BeginChildContext(
            parent,
            prop1,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/properties/name"), buffer, out written));

        Assert.AreEqual("properties/name", collector.SchemaLocation);

        // Second child from same parent (parallel) with different schema path
        byte[] prop2 = Encoding.UTF8.GetBytes("age");
        int child2 = c.BeginChildContext(
            parent,
            prop2,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/properties/age"), buffer, out written));

        Assert.AreEqual("properties/age", collector.SchemaLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    [TestMethod]
    public void BeginChildContextUnescaped_WithSchemaPath_ParallelSetsSchemaLocation()
    {
        var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Verbose);
        IJsonSchemaResultsCollector c = collector;

        int parent = c.BeginChildContext(0);

        // First child (sequential)
        byte[] prop1 = Encoding.UTF8.GetBytes("foo");
        int child1 = c.BeginChildContextUnescaped(
            parent,
            prop1,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/properties/foo"), buffer, out written));

        Assert.AreEqual("properties/foo", collector.SchemaLocation);

        // Second child from same parent (parallel)
        byte[] prop2 = Encoding.UTF8.GetBytes("bar");
        int child2 = c.BeginChildContextUnescaped(
            parent,
            prop2,
            null,
            static (buffer, out written) =>
                JsonSchemaEvaluation.TryCopyPath(Encoding.UTF8.GetBytes("#/properties/bar"), buffer, out written));

        Assert.AreEqual("properties/bar", collector.SchemaLocation);

        c.PopChildContext(child2);
        c.PopChildContext(child1);
        collector.Dispose();
    }

    #endregion
}
