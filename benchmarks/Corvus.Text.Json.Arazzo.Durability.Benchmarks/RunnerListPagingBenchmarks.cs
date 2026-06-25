// <copyright file="RunnerListPagingBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The runner LIST projection (<c>GET /runners</c>), paged vs unpaged, with the production-faithful lifecycle (a workspace
/// rented and disposed per op). Each runner is a congruent whole-document <c>Runner.From</c> wrap of the detached
/// <see cref="RunnerRegistration"/>, so the projection is allocation-clean either way; paging bounds CPU and response size
/// O(total) → O(page).
/// </summary>
[MemoryDiagnoser]
public class RunnerListPagingBenchmarks
{
    private const int PageSize = 50;

    [Params(50, 500)]
    public int RunnerCount { get; set; }

    private ParsedJsonDocument<RunnerRegistration>[] documents = null!;
    private RunnerRegistration[] runners = null!;
    private RunnerRegistration[] pageRunners = null!;
    private ReadOnlyMemory<byte> pageTokenUtf8;

    [GlobalSetup]
    public void Setup()
    {
        this.documents = new ParsedJsonDocument<RunnerRegistration>[this.RunnerCount];
        this.runners = new RunnerRegistration[this.RunnerCount];
        for (int i = 0; i < this.RunnerCount; i++)
        {
            string json = $$"""{"runnerId":"runner-{{i:D5}}","startedAt":"1970-01-01T00:00:00+00:00","lastSeenAt":"1970-01-01T00:00:00+00:00","maxConcurrency":4,"transports":["http"],"hostedVersions":[]}""";
            this.documents[i] = ParsedJsonDocument<RunnerRegistration>.Parse(System.Text.Encoding.UTF8.GetBytes(json).AsMemory());
            this.runners[i] = this.documents[i].RootElement;
        }

        // The first page is a separate array (a RunnerRegistration[] projects to IReadOnlyList box-free).
        this.pageRunners = this.runners[..Math.Min(PageSize, this.RunnerCount)];

        // When more than one page exists, the first page carries a continuation token for its last row's runnerId.
        if (this.RunnerCount > PageSize)
        {
            byte[] boundaryId = System.Text.Encoding.UTF8.GetBytes($"runner-{PageSize - 1:D5}");
            byte[] token = new byte[RunnerRegistryContinuationToken.GetMaxEncodedLength(boundaryId.Length)];
            int written = RunnerRegistryContinuationToken.EncodeToUtf8(boundaryId, token);
            this.pageTokenUtf8 = token.AsMemory(0, written);
        }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (ParsedJsonDocument<RunnerRegistration> d in this.documents)
        {
            d.Dispose();
        }
    }

    /// <summary>The unpaged list projection: every registered runner materialised into one response body — grows O(total).</summary>
    /// <returns>The projected runner count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Unpaged_All()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<RunnerRegistration> list = this.runners;
        Models.RunnerPage.Source<IReadOnlyList<RunnerRegistration>> body = Models.RunnerPage.Build(
            in list,
            runners: Models.RunnerPage.RunnerArray.Build(in list, BuildRunners));
        return Models.RunnerPage.CreateBuilder(workspace, in body, 30).RootElement.Runners.GetArrayLength();
    }

    /// <summary>The paged list projection: only the first keyset page (<see cref="PageSize"/> rows) plus the continuation
    /// token — the response bytes stay bounded as the registry grows.</summary>
    /// <returns>The projected runner count (defeats dead-code elimination).</returns>
    [Benchmark]
    public int Paged_FirstPage()
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        IReadOnlyList<RunnerRegistration> page = this.pageRunners;
        Models.RunnerPage.Source<IReadOnlyList<RunnerRegistration>> body = Models.RunnerPage.Build(
            in page,
            runners: Models.RunnerPage.RunnerArray.Build(in page, BuildRunners),
            nextPageToken: this.pageTokenUtf8.IsEmpty ? default : (Models.JsonString.Source)this.pageTokenUtf8.Span);
        return Models.RunnerPage.CreateBuilder(workspace, in body, 30).RootElement.Runners.GetArrayLength();
    }

    // The handler's congruent whole-document projection (each runner a Runner.From wrap of its registration).
    private static void BuildRunners(in IReadOnlyList<RunnerRegistration> registered, ref Models.RunnerPage.RunnerArray.Builder array)
    {
        foreach (RunnerRegistration runner in registered)
        {
            array.AddItem(Models.Runner.From(runner));
        }
    }
}