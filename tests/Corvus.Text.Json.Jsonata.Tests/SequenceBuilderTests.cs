// <copyright file="SequenceBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for <see cref="SequenceBuilder"/> lifecycle: count transitions, array pool returns,
/// double-to-element materialization, and Clear vs ReturnArray semantics.
/// </summary>
[TestClass]
public class SequenceBuilderTests
{
    // ----- Count = 0 -----

    [TestMethod]
    public void ToSequence_Empty_ReturnsUndefined()
    {
        var builder = new SequenceBuilder();
        Sequence result = builder.ToSequence();
        Assert.IsTrue(result.IsUndefined);
        Assert.AreEqual(0, result.Count);
        builder.ReturnArray();
    }

    [TestMethod]
    public void ReturnArray_Empty_DoesNotThrow()
    {
        var builder = new SequenceBuilder();
        builder.ReturnArray();
        Assert.AreEqual(0, builder.Count);
    }

    // ----- Count = 1 (singleton) -----

    [TestMethod]
    public void ToSequence_SingleElement_ReturnsSingleton()
    {
        var builder = new SequenceBuilder();
        JsonElement elem = JsonElement.ParseValue("""42"""u8);
        builder.Add(elem);

        Assert.AreEqual(1, builder.Count);
        Sequence result = builder.ToSequence();

        Assert.IsTrue(result.IsSingleton);
        Assert.AreEqual(1, result.Count);
        Assert.AreEqual(42, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    [TestMethod]
    public void ToSequence_SingleString_ReturnsSingleton()
    {
        var builder = new SequenceBuilder();
        JsonElement elem = JsonElement.ParseValue("\"hello\""u8);
        builder.Add(elem);

        Sequence result = builder.ToSequence();
        Assert.IsTrue(result.IsSingleton);
        Assert.AreEqual("hello", result.FirstOrDefault.GetString());
        builder.ReturnArray();
    }

    // ----- Count = 2+ (multi) -----

    [TestMethod]
    public void ToSequence_MultipleElements_ReturnsMulti()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));
        builder.Add(JsonElement.ParseValue("3"u8));

        Assert.AreEqual(3, builder.Count);
        Sequence result = builder.ToSequence();

        Assert.IsFalse(result.IsSingleton);
        Assert.AreEqual(3, result.Count);
        Assert.AreEqual(1, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
        Assert.AreEqual(3, result[2].GetDouble());
        builder.ReturnArray();
    }

    // ----- Clear -----

    [TestMethod]
    public void Clear_ResetsCountToZero()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));
        Assert.AreEqual(2, builder.Count);

        builder.Clear();
        Assert.AreEqual(0, builder.Count);
    }

    [TestMethod]
    public void Clear_AllowsReuse()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Clear();

        builder.Add(JsonElement.ParseValue("""99"""u8));
        Sequence result = builder.ToSequence();

        Assert.IsTrue(result.IsSingleton);
        Assert.AreEqual(99, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    // ----- ReturnArray -----

    [TestMethod]
    public void ReturnArray_AfterMultipleAdds_ResetsToEmpty()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));

        builder.ReturnArray();
        Assert.AreEqual(0, builder.Count);

        // Should be safe to reuse after ReturnArray
        builder.Add(JsonElement.ParseValue("3"u8));
        Sequence result = builder.ToSequence();
        Assert.IsTrue(result.IsSingleton);
        Assert.AreEqual(3, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    // ----- AddRange -----

    [TestMethod]
    public void AddRange_FromSingleton_IncreasesCount()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));

        Sequence singleton = new(JsonElement.ParseValue("2"u8));
        builder.AddRange(singleton);

        Assert.AreEqual(2, builder.Count);
        Sequence result = builder.ToSequence();
        Assert.AreEqual(2, result.Count);
        Assert.AreEqual(1, result[0].GetDouble());
        Assert.AreEqual(2, result[1].GetDouble());
        builder.ReturnArray();
    }

    [TestMethod]
    public void AddRange_FromUndefined_DoesNotChangeCount()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));

        builder.AddRange(Sequence.Undefined);
        Assert.AreEqual(1, builder.Count);

        Sequence result = builder.ToSequence();
        Assert.IsTrue(result.IsSingleton);
        builder.ReturnArray();
    }

    // ----- Transitions (count 0→1→2) -----

    [TestMethod]
    public void TransitionFromEmptyToSingletonToMulti()
    {
        var builder = new SequenceBuilder();

        // Empty
        Assert.AreEqual(0, builder.Count);

        // Add first element → will be singleton
        builder.Add(JsonElement.ParseValue("1"u8));
        Assert.AreEqual(1, builder.Count);

        // Add second element → multi
        builder.Add(JsonElement.ParseValue("2"u8));
        Assert.AreEqual(2, builder.Count);

        Sequence result = builder.ToSequence();
        Assert.AreEqual(2, result.Count);
        builder.ReturnArray();
    }

    // ----- Repeated ToSequence/ReturnArray cycles -----

    [TestMethod]
    public void RepeatedCycles_DoNotLeak()
    {
        var builder = new SequenceBuilder();

        for (int i = 0; i < 100; i++)
        {
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));
            Sequence seq = builder.ToSequence();
            Assert.AreEqual(2, seq.Count);
            builder.ReturnArray();
        }
    }

    [TestMethod]
    public void RepeatedSingletonCycles_DoNotLeak()
    {
        var builder = new SequenceBuilder();

        for (int i = 0; i < 100; i++)
        {
            builder.Add(JsonElement.ParseValue("42"u8));
            Sequence seq = builder.ToSequence();
            Assert.IsTrue(seq.IsSingleton);
            builder.ReturnArray();
        }
    }

    // ----- Mixed types -----

    [TestMethod]
    public void MixedTypes_StringNumberBoolNull()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("\"hello\""u8));
        builder.Add(JsonElement.ParseValue("42"u8));
        builder.Add(JsonElement.ParseValue("true"u8));
        builder.Add(JsonElement.ParseValue("null"u8));

        Sequence result = builder.ToSequence();
        Assert.AreEqual(4, result.Count);
        Assert.AreEqual("hello", result[0].GetString());
        Assert.AreEqual(42, result[1].GetDouble());
        builder.ReturnArray();
    }
}
