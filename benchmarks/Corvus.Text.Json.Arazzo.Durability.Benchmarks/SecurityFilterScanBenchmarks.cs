// <copyright file="SecurityFilterScanBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of the per-row row-security scan (design §14.2) the five non-SQL backends run in process
/// over a list/search page (the SQL backends push this into a <c>WHERE</c> predicate). The question is whether each
/// candidate row's tags make a u-turn through the managed heap to be authorized. <see cref="Materialized_ToList"/> is the
/// pre-change path — <see cref="SecurityTagSet.ToList"/> per row (a <see cref="List{T}"/> plus a managed
/// <see cref="SecurityTag"/>, i.e. two strings, per tag) handed to the filter. <see cref="Deferred_Holder"/> is the
/// bytes-to-bytes path: the filter parses each row's tags once into a pooled scratch + slice table and evaluates over the
/// unescaped UTF-8. Both reach the identical verdict; only the per-row allocation differs.
/// </summary>
public class SecurityFilterScanBenchmarks
{
    private const int PageSize = 50;

    private SecurityFilter filter = null!;
    private SecurityTagSet[] rows = null!;

    [GlobalSetup]
    public void Setup()
    {
        // A representative scoped filter: a deployment wrapper (tenant-scoped to the principal's claim) plus a standing
        // membership rule, resolved against the principal's claims — the shape every list/search scan evaluates.
        var rules = new[]
        {
            SecurityRule.Compile("tenant == $claim.tenant"),
            SecurityRule.Compile("team == 'payments' || env == 'prod'"),
        };
        var claims = new Dictionary<string, IReadOnlyList<string>>
        {
            ["tenant"] = ["acme"],
        };
        this.filter = new SecurityFilter(rules, claims);

        // A page of candidate rows, each carrying a handful of tags (the warm scan sees one of these per result row).
        this.rows = new SecurityTagSet[PageSize];
        for (int i = 0; i < PageSize; i++)
        {
            this.rows[i] = SecurityTagSet.FromTags(
            [
                new SecurityTag("tenant", "acme"),
                new SecurityTag("team", "payments"),
                new SecurityTag("env", "prod"),
            ]);
        }
    }

    /// <summary>The pre-change path: materialise each row's tags to a <see cref="List{T}"/> of <see cref="SecurityTag"/> before the filter check.</summary>
    /// <returns>The admitted-row count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Materialized_ToList()
    {
        int admitted = 0;
        foreach (SecurityTagSet row in this.rows)
        {
            if (this.filter.IsSatisfiedBy(row.ToList()))
            {
                admitted++;
            }
        }

        return admitted;
    }

    /// <summary>The bytes-to-bytes path: hand the deferred <see cref="SecurityTagSet"/> holder straight to the filter (parse once, no managed tag per row).</summary>
    /// <returns>The admitted-row count.</returns>
    [Benchmark]
    public int Deferred_Holder()
    {
        int admitted = 0;
        foreach (SecurityTagSet row in this.rows)
        {
            if (this.filter.IsSatisfiedBy(row))
            {
                admitted++;
            }
        }

        return admitted;
    }
}