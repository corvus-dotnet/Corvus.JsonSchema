// <copyright file="JsonRegexValidatorCaptureTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text;
using Corvus.Text.Json.Internal;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// The validator behind <c>format: regex</c> finds the groups of a pattern by number and by name through an index.
/// It read every group it had met so far for each new one, and a pattern of groups took it time in proportion to
/// the square of its length: 26 seconds for 333,333 groups in 1,000,000 characters, where it now takes a few
/// hundredths of a second.
/// </summary>
[TestClass]
public class JsonRegexValidatorCaptureTests
{
    // Each pattern below took the validator more than 20 seconds before the index. The bound is far above what it
    // takes now on a machine with every core busy, and far below that.
    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(6);

    [TestMethod]
    [DataRow("groups", true)]
    [DataRow("open groups", false)]
    [DataRow("nested groups", true)]
    [DataRow("named groups", true)]
    [DataRow("numbered groups", false, DisplayName = "numbered groups, which the ECMAScript option refuses once it has noted them all")]
    [DataRow("named groups and references", true)]
    [DataRow("groups and references to the last", true)]
    public void APatternOfManyGroupsIsReadInTimeInProportionToItsLength(string name, bool expected)
    {
        string pattern = name switch
        {
            "groups" => Repeat("(a)", 333_333),
            "open groups" => new string('(', 300_000),
            "nested groups" => new string('(', 300_000) + new string(')', 300_000),
            "named groups" => Numbered("(?<n", ">a)", 100_000),
            "numbered groups" => Numbered("(?<", ">a)", 110_000),
            "named groups and references" => Numbered("(?<n", ">a)", 60_000) + Numbered("\\k<n", ">", 60_000),
            "groups and references to the last" => Repeat("(a)", 150_000) + Repeat("\\150000", 50_000),
            _ => throw new ArgumentException(name),
        };

        // Once for whatever the first use of the validator costs.
        JsonRegexValidator.Validate("(a)(?<n>b)\\1\\k<n>", JsonRegexOptions.ECMAScript);

        var stopwatch = Stopwatch.StartNew();
        bool actual = JsonRegexValidator.Validate(pattern, JsonRegexOptions.ECMAScript);
        stopwatch.Stop();

        Assert.AreEqual(expected, actual, name);
        Assert.IsTrue(stopwatch.Elapsed < Bound, $"{name}: {pattern.Length:N0} characters took {stopwatch.Elapsed.TotalSeconds:F1} s.");
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(63)]
    [DataRow(64)]
    [DataRow(65)]
    [DataRow(127)]
    [DataRow(128)]
    [DataRow(129)]
    [DataRow(1_000)]
    [DataRow(5_000)]
    public void EveryGroupIsFoundByItsNumberAndByItsName(int count)
    {
        // The counts are either side of where the index is made and made larger.
        string groups = Numbered("(?<n", ">a)", count);
        for (int i = 0; i < count; i += Math.Max(1, count / 50))
        {
            Assert.IsTrue(JsonRegexValidator.Validate(groups + "\\k<n" + (i + 1) + ">", JsonRegexOptions.ECMAScript), $"name {i + 1} of {count}");
            Assert.IsTrue(JsonRegexValidator.Validate(groups + "(?(n" + (i + 1) + ")a|b)", JsonRegexOptions.None), $"name {i + 1} of {count} in a condition");
        }

        Assert.IsTrue(JsonRegexValidator.Validate(groups + "\\k<n" + count + ">", JsonRegexOptions.ECMAScript));
        Assert.IsFalse(JsonRegexValidator.Validate(groups + "\\k<n" + (count + 1) + ">", JsonRegexOptions.ECMAScript));
        Assert.IsFalse(JsonRegexValidator.Validate(groups + "\\k<n0>", JsonRegexOptions.ECMAScript));
        Assert.IsFalse(JsonRegexValidator.Validate(groups + "\\k<m1>", JsonRegexOptions.ECMAScript));

        string numbered = Repeat("(a)", count);
        Assert.IsTrue(JsonRegexValidator.Validate(numbered + "\\k<" + count + ">", JsonRegexOptions.None));
        Assert.IsFalse(JsonRegexValidator.Validate(numbered + "\\k<" + (count + 1) + ">", JsonRegexOptions.None));
        Assert.IsTrue(JsonRegexValidator.Validate(numbered + "(?<-" + count + ">a)", JsonRegexOptions.None));
        Assert.IsFalse(JsonRegexValidator.Validate(numbered + "(?<-" + (count + 1) + ">a)", JsonRegexOptions.None));
    }

    [TestMethod]
    [DataRow("(?<a>x)(?<a>y)\\k<a>", 0x0000, true, DisplayName = "a name given twice is one name")]
    [DataRow("(?<ab>x)(?<ba>y)\\k<ab>\\k<ba>", 0x0000, true, DisplayName = "names of the same letters")]
    [DataRow("(?<ab>x)(?<ba>y)\\k<aa>", 0x0000, false)]
    [DataRow("(?<a>x)(?<aa>y)(?<aaa>z)\\k<aa>", 0x0000, true, DisplayName = "a name that begins another")]
    [DataRow("(?<a>x)(?<aaa>z)\\k<aa>", 0x0000, false)]
    [DataRow("(?<5>x)(?<5>y)\\5", 0x0000, true, DisplayName = "a number given twice is one group")]
    [DataRow("(?<5>x)\\k<4>", 0x0000, false, DisplayName = "a number between is no group")]
    [DataRow("(?<5>x)(a)(b)\\k<2>", 0x0000, true)]
    [DataRow("(?<2147483647>x)\\k<2147483647>", 0x0000, true, DisplayName = "the highest number")]
    [DataRow("(?<65536>x)(?<131072>y)(?<196608>z)\\k<131072>", 0x0000, true, DisplayName = "numbers far apart")]
    [DataRow("(?<65536>x)(?<196608>z)\\k<131072>", 0x0000, false)]
    [DataRow("(a)(?<n>b)(c)\\3", 0x0000, true, DisplayName = "a name takes the number after the groups with none")]
    [DataRow("(a)(?<n>b)(c)\\4", 0x0000, false)]
    [DataRow("(a)(?<n>b)\\k<n>\\1", 0x0100, true)]
    [DataRow("(?<n>b)\\k<N>", 0x0100, false, DisplayName = "a name in another case is another name")]
    public void AGroupIsTheOneItWasBeforeTheIndex(string pattern, int optionsValue, bool expected)
    {
        Assert.AreEqual(expected, JsonRegexValidator.Validate(pattern, (JsonRegexOptions)optionsValue));
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