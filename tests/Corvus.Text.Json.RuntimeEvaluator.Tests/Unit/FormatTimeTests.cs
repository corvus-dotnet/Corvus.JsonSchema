// <copyright file="FormatTimeTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.RuntimeEvaluator;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.RuntimeEvaluator.Tests.Unit;

/// <summary>
/// A format check has no limit of time, so it has to take time in proportion to the string it reads. The checks
/// are scanners, most with a bound of length from the grammar of the format, and one of them was not linear:
/// <c>format: regex</c> took a pattern of groups time in proportion to the square of its length.
/// </summary>
[TestClass]
public class FormatTimeTests
{
    // Each instance below took the check more than 30 seconds before the change, in the build these tests run in.
    // The bound is far above what it takes now on a machine with every core busy, and far below that.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(6);

    [TestMethod]
    [DataRow(JsonSchemaCodeGeneration.Disabled, "groups", true)]
    [DataRow(JsonSchemaCodeGeneration.Disabled, "open groups", false)]
    [DataRow(JsonSchemaCodeGeneration.Disabled, "named groups", true)]
    [DataRow(JsonSchemaCodeGeneration.Eager, "groups", true)]
    [DataRow(JsonSchemaCodeGeneration.Eager, "open groups", false)]
    [DataRow(JsonSchemaCodeGeneration.Eager, "named groups", true)]
    public void FormatRegexReadsAPatternOfManyGroupsInTimeInProportionToItsLength(JsonSchemaCodeGeneration generation, string name, bool expected)
    {
        string pattern = name switch
        {
            "groups" => Repeat("(a)", 100_000),
            "open groups" => new string('(', 100_000),
            "named groups" => Numbered("(?<n", ">a)", 40_000),
            _ => throw new ArgumentException(name),
        };

        var options = new JsonSchemaEvaluatorOptions { AssertFormat = true, CodeGeneration = generation };
        using JsonSchemaEvaluator evaluator = JsonSchemaEvaluator.Compile("""{"format": "regex"}""", options);
        Assert.IsTrue(evaluator.Evaluate("\"(a)(?<n>b)\""));
        byte[] instance = Encoding.UTF8.GetBytes("\"" + pattern + "\"");

        var stopwatch = Stopwatch.StartNew();
        bool actual = evaluator.Evaluate(instance);
        stopwatch.Stop();

        Assert.AreEqual(expected, actual, name);
        Assert.IsTrue(stopwatch.Elapsed < Bound, $"{name}: {pattern.Length:N0} characters took {stopwatch.Elapsed.TotalSeconds:F1} s.");
    }

    private static string Repeat(string unit, int count)
    {
        var builder = new StringBuilder(unit.Length * count);
        for (int i = 0; i < count; i++)
        {
            builder.Append(unit);
        }

        return builder.ToString();
    }

    private static string Numbered(string before, string after, int count)
    {
        var builder = new StringBuilder();
        for (int i = 1; i <= count; i++)
        {
            builder.Append(before).Append(i).Append(after);
        }

        return builder.ToString();
    }
}