// <copyright file="RunnerRegistryReadPagingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The runner-registry first-page READ through a real embedded driver (SQLite over an in-memory database — the one
/// backend whose store read path is benchmarkable without a container). This isolates the Phase-2 win: the default paged
/// seam reads <em>every</em> registration then pages in memory (O(total) driver rows + parses), whereas a native keyset
/// query (<c>WHERE runner_id &gt; @after ORDER BY runner_id LIMIT @limit</c>) fetches only the page (O(page)). The
/// projection is already allocation-clean; this measures the store read that paging is supposed to bound, so the figure
/// to watch is how the cost tracks <see cref="RunnerCount"/> (flat = native; growing = full-read default).
/// </summary>
[MemoryDiagnoser]
public class RunnerRegistryReadPagingBenchmarks
{
    private const int PageSize = 50;

    [Params(50, 500)]
    public int RunnerCount { get; set; }

    private SqliteRunnerRegistry sqlite = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.sqlite = SqliteRunnerRegistry.ConnectAsync("Data Source=:memory:").AsTask().GetAwaiter().GetResult();
        for (int i = 0; i < this.RunnerCount; i++)
        {
            string json = $$"""{"runnerId":"runner-{{i:D5}}","startedAt":"1970-01-01T00:00:00+00:00","lastSeenAt":"1970-01-01T00:00:00+00:00","maxConcurrency":4,"transports":["http"],"hostedVersions":[]}""";
            using ParsedJsonDocument<RunnerRegistration> doc = ParsedJsonDocument<RunnerRegistration>.Parse(Encoding.UTF8.GetBytes(json).AsMemory());
            this.sqlite.RegisterAsync(doc.RootElement, default).AsTask().GetAwaiter().GetResult();
        }
    }

    [GlobalCleanup]
    public void Cleanup() => this.sqlite.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>The first keyset page read from the SQLite registry. Until the native override lands this dispatches to the
    /// default in-memory pager (a full <c>SELECT doc</c> + parse of every registration); after it lands it is a bounded
    /// <c>LIMIT</c> query. The page count is returned to defeat dead-code elimination.</summary>
    /// <returns>The number of runners on the first page.</returns>
    [Benchmark]
    public int Sqlite_FirstPage()
    {
        // Through the interface: the paged seam is a default interface method until the native override lands, so it is
        // only reachable via IRunnerRegistry (virtual dispatch then picks the override once it exists).
        using RunnerRegistryPage page = ((IRunnerRegistry)this.sqlite).ListAsync(PageSize, default, default).AsTask().GetAwaiter().GetResult();
        return page.Runners.Count;
    }
}