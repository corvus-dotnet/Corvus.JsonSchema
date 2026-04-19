// <copyright file="SequenceBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json;
using Xunit;

namespace Corvus.Text.Json.Jsonata.Tests;

/// <summary>
/// Tests for <see cref="SequenceBuilder"/> lifecycle: count transitions, array pool returns,
/// double-to-element materialization, and Clear vs ReturnArray semantics.
/// </summary>
public class SequenceBuilderTests
{
    // ----- Count = 0 -----

    [Fact]
    public void ToSequence_Empty_ReturnsUndefined()
    {
        var builder = new SequenceBuilder();
        Sequence result = builder.ToSequence();
        Assert.True(result.IsUndefined);
        Assert.Equal(0, result.Count);
        builder.ReturnArray();
    }

    [Fact]
    public void ReturnArray_Empty_DoesNotThrow()
    {
        var builder = new SequenceBuilder();
        builder.ReturnArray();
        Assert.Equal(0, builder.Count);
    }

    // ----- Count = 1 (singleton) -----

    [Fact]
    public void ToSequence_SingleElement_ReturnsSingleton()
    {
        var builder = new SequenceBuilder();
        JsonElement elem = JsonElement.ParseValue("""42"""u8);
        builder.Add(elem);

        Assert.Equal(1, builder.Count);
        Sequence result = builder.ToSequence();

        Assert.True(result.IsSingleton);
        Assert.Equal(1, result.Count);
        Assert.Equal(42, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    [Fact]
    public void ToSequence_SingleString_ReturnsSingleton()
    {
        var builder = new SequenceBuilder();
        JsonElement elem = JsonElement.ParseValue("\"hello\""u8);
        builder.Add(elem);

        Sequence result = builder.ToSequence();
        Assert.True(result.IsSingleton);
        Assert.Equal("hello", result.FirstOrDefault.GetString());
        builder.ReturnArray();
    }

    // ----- Count = 2+ (multi) -----

    [Fact]
    public void ToSequence_MultipleElements_ReturnsMulti()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));
        builder.Add(JsonElement.ParseValue("3"u8));

        Assert.Equal(3, builder.Count);
        Sequence result = builder.ToSequence();

        Assert.False(result.IsSingleton);
        Assert.Equal(3, result.Count);
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
        Assert.Equal(3, result[2].GetDouble());
        builder.ReturnArray();
    }

    // ----- Clear -----

    [Fact]
    public void Clear_ResetsCountToZero()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));
        Assert.Equal(2, builder.Count);

        builder.Clear();
        Assert.Equal(0, builder.Count);
    }

    [Fact]
    public void Clear_AllowsReuse()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Clear();

        builder.Add(JsonElement.ParseValue("""99"""u8));
        Sequence result = builder.ToSequence();

        Assert.True(result.IsSingleton);
        Assert.Equal(99, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    // ----- ReturnArray -----

    [Fact]
    public void ReturnArray_AfterMultipleAdds_ResetsToEmpty()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));
        builder.Add(JsonElement.ParseValue("2"u8));

        builder.ReturnArray();
        Assert.Equal(0, builder.Count);

        // Should be safe to reuse after ReturnArray
        builder.Add(JsonElement.ParseValue("3"u8));
        Sequence result = builder.ToSequence();
        Assert.True(result.IsSingleton);
        Assert.Equal(3, result.FirstOrDefault.GetDouble());
        builder.ReturnArray();
    }

    // ----- AddRange -----

    [Fact]
    public void AddRange_FromSingleton_IncreasesCount()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));

        Sequence singleton = new(JsonElement.ParseValue("2"u8));
        builder.AddRange(singleton);

        Assert.Equal(2, builder.Count);
        Sequence result = builder.ToSequence();
        Assert.Equal(2, result.Count);
        Assert.Equal(1, result[0].GetDouble());
        Assert.Equal(2, result[1].GetDouble());
        builder.ReturnArray();
    }

    [Fact]
    public void AddRange_FromUndefined_DoesNotChangeCount()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("1"u8));

        builder.AddRange(Sequence.Undefined);
        Assert.Equal(1, builder.Count);

        Sequence result = builder.ToSequence();
        Assert.True(result.IsSingleton);
        builder.ReturnArray();
    }

    // ----- Transitions (count 0→1→2) -----

    [Fact]
    public void TransitionFromEmptyToSingletonToMulti()
    {
        var builder = new SequenceBuilder();

        // Empty
        Assert.Equal(0, builder.Count);

        // Add first element → will be singleton
        builder.Add(JsonElement.ParseValue("1"u8));
        Assert.Equal(1, builder.Count);

        // Add second element → multi
        builder.Add(JsonElement.ParseValue("2"u8));
        Assert.Equal(2, builder.Count);

        Sequence result = builder.ToSequence();
        Assert.Equal(2, result.Count);
        builder.ReturnArray();
    }

    // ----- Repeated ToSequence/ReturnArray cycles -----

    [Fact]
    public void RepeatedCycles_DoNotLeak()
    {
        var builder = new SequenceBuilder();

        for (int i = 0; i < 100; i++)
        {
            builder.Add(JsonElement.ParseValue("1"u8));
            builder.Add(JsonElement.ParseValue("2"u8));
            Sequence seq = builder.ToSequence();
            Assert.Equal(2, seq.Count);
            builder.ReturnArray();
        }
    }

    [Fact]
    public void RepeatedSingletonCycles_DoNotLeak()
    {
        var builder = new SequenceBuilder();

        for (int i = 0; i < 100; i++)
        {
            builder.Add(JsonElement.ParseValue("42"u8));
            Sequence seq = builder.ToSequence();
            Assert.True(seq.IsSingleton);
            builder.ReturnArray();
        }
    }

    // ----- Mixed types -----

    [Fact]
    public void MixedTypes_StringNumberBoolNull()
    {
        var builder = new SequenceBuilder();
        builder.Add(JsonElement.ParseValue("\"hello\""u8));
        builder.Add(JsonElement.ParseValue("42"u8));
        builder.Add(JsonElement.ParseValue("true"u8));
        builder.Add(JsonElement.ParseValue("null"u8));

        Sequence result = builder.ToSequence();
        Assert.Equal(4, result.Count);
        Assert.Equal("hello", result[0].GetString());
        Assert.Equal(42, result[1].GetDouble());
        builder.ReturnArray();
    }
}
