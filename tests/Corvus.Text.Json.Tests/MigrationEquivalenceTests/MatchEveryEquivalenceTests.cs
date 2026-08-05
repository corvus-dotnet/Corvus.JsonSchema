// Copyright (c) William Adams. All rights reserved.
// Licensed under the MIT License.

namespace Corvus.Text.Json.Tests.MigrationEquivalenceTests;

using System.Collections.Generic;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using V4 = MigrationModels.V4;
using V5 = MigrationModels.V5;

/// <summary>
/// Verifies the <c>MatchEvery()</c> method generated for <c>anyOf</c> compositions (issue #905).
/// </summary>
/// <remarks>
/// <para>
/// <c>Match()</c> dispatches to the first subschema that matches, in declaration order. For an
/// <c>anyOf</c> whose arms overlap (here a bare string arm and a pattern-constrained string arm),
/// the bare string arm always wins and the pattern arm is unreachable. That is correct for
/// <c>anyOf</c> validation, but it makes value-based dispatch on the more specific arm impossible.
/// </para>
/// <para>
/// <c>MatchEvery()</c> calls the match function for every subschema that matches, in declaration
/// order, threading an accumulator through the calls and returning the final accumulator.
/// <c>defaultMatch</c> receives the seed accumulator only when no subschema matched.
/// </para>
/// <para>
/// The model is generated from <c>migration-pattern-union.json</c>, an <c>anyOf</c> of a bare
/// string and a string constrained by the pattern <c>^corvus-[0-9]+$</c>.
/// </para>
/// </remarks>
[TestClass]
public class MatchEveryEquivalenceTests
{
    private const string PatternMatchingValue = "\"corvus-42\"";
    private const string PlainStringValue = "\"hello\"";
    private const string NonStringValue = "42";

    [TestMethod]
    public void V5_Match_PatternMatchingValue_DispatchesFirstArm()
    {
        // Pins the existing (correct) Match() behaviour that motivated MatchEvery(): the bare
        // string arm matches first, so the pattern arm is never called.
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V5.MigrationPatternUnion v5 = parsedV5.RootElement;
        string result = v5.Match(
            static (in s) => $"string:{(string)s}",
            static (in p) => $"pattern:{(string)p}",
            static (in v) => "none");
        Assert.AreEqual("string:corvus-42", result);
    }

    [TestMethod]
    public void V5_MatchEvery_PatternMatchingValue_CallsEveryMatchingArmInOrder()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V5.MigrationPatternUnion v5 = parsedV5.RootElement;
        string result = v5.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|string:corvus-42|pattern:corvus-42", result);
    }

    [TestMethod]
    public void V5_MatchEvery_PlainString_CallsOnlyTheBareStringArm()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(PlainStringValue);
        V5.MigrationPatternUnion v5 = parsedV5.RootElement;
        string result = v5.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|string:hello", result);
    }

    [TestMethod]
    public void V5_MatchEvery_NonString_CallsDefaultMatchWithSeed()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(NonStringValue);
        V5.MigrationPatternUnion v5 = parsedV5.RootElement;
        string result = v5.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|none", result);
    }

    [TestMethod]
    public void V5_MatchEvery_AccumulatorRecordsInvocationOrderAndCount()
    {
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V5.MigrationPatternUnion v5 = parsedV5.RootElement;
        List<string> calls = v5.MatchEvery(
            new List<string>(),
            static (in s, in acc) => { acc.Add("string"); return acc; },
            static (in p, in acc) => { acc.Add("pattern"); return acc; },
            static (in v, in acc) => { acc.Add("none"); return acc; });
        CollectionAssert.AreEqual(new[] { "string", "pattern" }, calls);
    }

    [TestMethod]
    public void V5_Mutable_MatchEvery_PatternMatchingValue_CallsEveryMatchingArmInOrder()
    {
        using var workspace = JsonWorkspace.Create();
        using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(PatternMatchingValue);
        using JsonDocumentBuilder<V5.MigrationPatternUnion.Mutable> builder =
            parsedV5.RootElement.CreateBuilder(workspace);
        V5.MigrationPatternUnion.Mutable root = builder.RootElement;
        string result = root.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|string:corvus-42|pattern:corvus-42", result);
    }

    [TestMethod]
    public void V4_Match_PatternMatchingValue_DispatchesFirstArm()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V4.MigrationPatternUnion v4 = parsedV4.Instance;
        string result = v4.Match(
            static (in s) => $"string:{(string)s}",
            static (in p) => $"pattern:{(string)p}",
            static (in v) => "none");
        Assert.AreEqual("string:corvus-42", result);
    }

    [TestMethod]
    public void V4_MatchEvery_PatternMatchingValue_CallsEveryMatchingArmInOrder()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V4.MigrationPatternUnion v4 = parsedV4.Instance;
        string result = v4.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|string:corvus-42|pattern:corvus-42", result);
    }

    [TestMethod]
    public void V4_MatchEvery_PlainString_CallsOnlyTheBareStringArm()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(PlainStringValue);
        V4.MigrationPatternUnion v4 = parsedV4.Instance;
        string result = v4.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|string:hello", result);
    }

    [TestMethod]
    public void V4_MatchEvery_NonString_CallsDefaultMatchWithSeed()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(NonStringValue);
        V4.MigrationPatternUnion v4 = parsedV4.Instance;
        string result = v4.MatchEvery(
            "seed",
            static (in s, in acc) => $"{acc}|string:{(string)s}",
            static (in p, in acc) => $"{acc}|pattern:{(string)p}",
            static (in v, in acc) => $"{acc}|none");
        Assert.AreEqual("seed|none", result);
    }

    [TestMethod]
    public void V4_MatchEvery_AccumulatorRecordsInvocationOrderAndCount()
    {
        using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(PatternMatchingValue);
        V4.MigrationPatternUnion v4 = parsedV4.Instance;
        List<string> calls = v4.MatchEvery(
            new List<string>(),
            static (in s, in acc) => { acc.Add("string"); return acc; },
            static (in p, in acc) => { acc.Add("pattern"); return acc; },
            static (in v, in acc) => { acc.Add("none"); return acc; });
        CollectionAssert.AreEqual(new[] { "string", "pattern" }, calls);
    }

    [TestMethod]
    public void BothEngines_MatchEvery_SameResults()
    {
        string[] jsons = [PatternMatchingValue, PlainStringValue, NonStringValue];
        string[] expected = ["seed|string:corvus-42|pattern:corvus-42", "seed|string:hello", "seed|none"];

        for (int i = 0; i < jsons.Length; i++)
        {
            using var parsedV4 = Corvus.Json.ParsedValue<V4.MigrationPatternUnion>.Parse(jsons[i]);
            V4.MigrationPatternUnion v4 = parsedV4.Instance;
            string v4Result = v4.MatchEvery(
                "seed",
                static (in s, in acc) => $"{acc}|string:{(string)s}",
                static (in p, in acc) => $"{acc}|pattern:{(string)p}",
                static (in v, in acc) => $"{acc}|none");

            using var parsedV5 = Corvus.Text.Json.ParsedJsonDocument<V5.MigrationPatternUnion>.Parse(jsons[i]);
            V5.MigrationPatternUnion v5 = parsedV5.RootElement;
            string v5Result = v5.MatchEvery(
                "seed",
                static (in s, in acc) => $"{acc}|string:{(string)s}",
                static (in p, in acc) => $"{acc}|pattern:{(string)p}",
                static (in v, in acc) => $"{acc}|none");

            Assert.AreEqual(expected[i], v4Result);
            Assert.AreEqual(v4Result, v5Result);
        }
    }
}