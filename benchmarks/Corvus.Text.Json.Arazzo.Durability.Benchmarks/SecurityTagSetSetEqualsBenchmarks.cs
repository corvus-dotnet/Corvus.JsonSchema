// <copyright file="SecurityTagSetSetEqualsBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures <see cref="SecurityTagSet.SetEquals"/> — the administration/entitlement membership comparison (design §14.2),
/// run in nested loops when authorizing/mutating administration — against the pre-optimisation <c>ToList()</c> ×2 +
/// <c>All(Contains)</c> baseline. <see cref="SecurityTagSet.SetEquals"/> compares the unescaped UTF-8 tag bytes directly
/// over pooled/stack buffers, so it should allocate <strong>nothing</strong>; the baseline materializes two tag lists and
/// their strings per call. The two sets are the same tags in different order, so the full compare path runs.
/// </summary>
public class SecurityTagSetSetEqualsBenchmarks
{
    private static readonly SecurityTagSet A = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:sub", "alice"),
        new SecurityTag("sys:role", "operator"),
    ]);

    private static readonly SecurityTagSet B = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:role", "operator"),
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:sub", "alice"),
    ]);

    /// <summary>The pre-optimisation membership comparison (two ToList + All/Contains) — the A/B reference.</summary>
    /// <returns>Whether the sets are equal.</returns>
    [Benchmark(Baseline = true)]
    public bool SetEquals_Naive()
    {
        List<SecurityTag> left = A.ToList();
        List<SecurityTag> right = B.ToList();
        return left.Count == right.Count && left.All(right.Contains);
    }

    /// <summary>The UTF-8 byte-span set comparison — the production path; should be zero-allocation.</summary>
    /// <returns>Whether the sets are equal.</returns>
    [Benchmark]
    public bool SetEquals() => A.SetEquals(B);
}