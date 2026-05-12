// <copyright file="MutatingVisitorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Visitor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Json.Specs.Tests.JsonModel.Visitor;

[TestClass]
public class MutatingVisitorTests
{
    [TestMethod]
    [DataRow("""["hello","there","everyone"]""", true, """["hello","everyone"]""")]
    [DataRow("""["there", "hello","everyone"]""", true, """["hello","everyone"]""")]
    [DataRow("""["hello", "everyone", "there"]""", true, """["hello","everyone"]""")]
    [DataRow("""["there", "hello", "there", "everyone", "there"]""", true, """["hello","everyone"]""")]
    [DataRow("""["there", "hello", "there", ["Fi", "Fi", "there"], "everyone", "there"]""", true, """["hello", ["Fi", "Fi"],"everyone"]""")]
    [DataRow("""["there"]""", true, """[]""")]
    [DataRow("""["hello","you","all"]""", false, """["hello","you","all"]""")]
    [DataRow("\"there\"", true, "<undefined>")]
    public void RemoveThereString(string document, bool expectModified, string expected)
    {
        JsonAny value = JsonAny.Parse(document);

        bool transformed = value.Visit(VisitRemoveThereString, out JsonAny result);

        Assert.AreEqual(expectModified, transformed);
        Assert.AreEqual(expected == "<undefined>" ? JsonAny.Undefined : JsonAny.Parse(expected), result);
    }

    [TestMethod]
    [DataRow("""{"hello":1,"there":2,"everyone":3}""", true, """{"hello":1,"everyone":3}""")]
    [DataRow("""{"there":2,"hello":1,"everyone":3}""", true, """{"hello":1,"everyone":3}""")]
    [DataRow("""{"hello":1,"everyone":3,"there":2}""", true, """{"hello":1,"everyone":3}""")]
    [DataRow("""{"there":2}""", true, """{}""")]
    [DataRow("""{"hello":1,"you":2,"all":3}""", false, """{"hello":1,"you":2,"all":3}""")]
    public void RemoveThereProperty(string document, bool expectModified, string expected)
    {
        JsonAny value = JsonAny.Parse(document);

        bool transformed = value.Visit(VisitRemoveThereProperty, out JsonAny result);

        Assert.AreEqual(expectModified, transformed);
        Assert.AreEqual(expected == "<undefined>" ? JsonAny.Undefined : JsonAny.Parse(expected), result);
    }

    private static void VisitRemoveThereString(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
    {
        if (nodeToVisit.ValueKind == JsonValueKind.String)
        {
            if (nodeToVisit == "there")
            {
                result.Walk = Walk.RemoveAndContinue;
                result.Transformed = Transformed.Yes;
                result.Output = JsonAny.Undefined;
                return;
            }
        }

        result.Walk = Walk.Continue;
        result.Transformed = Transformed.No;
        result.Output = nodeToVisit;
    }

    private static void VisitRemoveThereProperty(ReadOnlySpan<char> path, in JsonAny nodeToVisit, ref VisitResult result)
    {
        if (path.EndsWith("/there".AsSpan()))
        {
            result.Walk = Walk.RemoveAndContinue;
            result.Transformed = Transformed.Yes;
            result.Output = JsonAny.Undefined;
        }
        else
        {
            result.Walk = Walk.Continue;
            result.Transformed = Transformed.No;
            result.Output = nodeToVisit;
        }
    }
}