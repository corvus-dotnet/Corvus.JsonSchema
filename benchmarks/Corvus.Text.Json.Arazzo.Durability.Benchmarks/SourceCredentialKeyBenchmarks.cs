// <copyright file="SourceCredentialKeyBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Linq;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the allocation floor of <see cref="SourceCredentialKey.CanonicalTags"/> / <see cref="SourceCredentialKey.Discriminator"/>
/// (design §13/§14.2) — the order-independent canonical key built on every credential-binding write and per cache miss
/// across the backends. The span-based production path is contrasted with the pre-optimisation
/// <c>ToList()</c>+interpolate+<c>Join</c> shape (the baseline), so a regression that reintroduces per-tag
/// materialisation is visible. Only the result string should escape to the heap.
/// </summary>
public class SourceCredentialKeyBenchmarks
{
    private static readonly SecurityTagSet ManagementTags = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:env", "production"),
        new SecurityTag("sys:team", "payments"),
    ]);

    private static readonly SecurityTagSet UsageTags = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:sub", "alice"),
        new SecurityTag("sys:scope", "runs:write"),
    ]);

    /// <summary>The pre-optimisation shape (ToList + per-tag interpolation + join) — the A/B reference the production path must stay below.</summary>
    /// <returns>The canonical string length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Canonical_Naive()
    {
        List<SecurityTag> list = ManagementTags.ToList();
        list.Sort(static (a, b) =>
        {
            int byKey = string.CompareOrdinal(a.Key, b.Key);
            return byKey != 0 ? byKey : string.CompareOrdinal(a.Value, b.Value);
        });
        return string.Join(";", list.Select(t => $"{t.Key}={t.Value}")).Length;
    }

    /// <summary>The span-based canonical key — the production path.</summary>
    /// <returns>The canonical string length.</returns>
    [Benchmark]
    public int Canonical() => SourceCredentialKey.CanonicalTags(ManagementTags).Length;

    /// <summary>The full binding discriminator (two canonical tag sets + the separator).</summary>
    /// <returns>The discriminator string length.</returns>
    [Benchmark]
    public int Discriminator() => SourceCredentialKey.Discriminator(ManagementTags, UsageTags).Length;
}