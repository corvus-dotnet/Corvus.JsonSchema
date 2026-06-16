// <copyright file="CredentialStoreReadBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the shared CTJ read-and-filter primitive every <see cref="ISourceCredentialStore"/> backend runs on a
/// cache miss, isolated in-process over the in-memory reference store (no driver / no I/O noise): a credential
/// resolution (<see cref="ISourceCredentialStore.ResolveForUsageAsync"/>) is a pooled, zero-copy parse of the binding
/// document plus a deferred-holder label-superset check (<see cref="SourceCredentialBinding.IsUsableBy"/>) — never a
/// <c>System.Text.Json</c> materialization. This is the cold/miss path the §13.4 warm cache amortizes away, so it is
/// not 0 B (the parse owns one transient document); the benchmark is the regression guard that it stays small and
/// allocation-disciplined (no accidental STJ, no extra copies) across the backends that share this primitive.
/// </summary>
public class CredentialStoreReadBenchmarks
{
    private static readonly SourceCredentialDefinition Definition = new(
        "petstore",
        "production",
        SourceCredentialKind.ApiKey,
        [new SecretReferenceDefinition("value", "keyvault://petstore-apikey#3")],
        [new CredentialConfigDefinition("parameterName", "X-Api-Key")],
        UsageTags: SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")]));

    private InMemorySourceCredentialStore inMemory = null!;
    private SqliteSourceCredentialStore sqlite = null!;
    private SecurityTagSet runTags;

    [GlobalSetup]
    public void Setup()
    {
        this.runTags = SecurityTagSet.FromTags(
        [
            new SecurityTag("sys:tenant", "contoso"),
            new SecurityTag("sys:workflow", "nightly-reconcile"),
        ]);

        this.inMemory = new InMemorySourceCredentialStore();
        this.inMemory.AddAsync(Definition, "bench", default).AsTask().GetAwaiter().GetResult();

        // A real embedded ADO driver (Microsoft.Data.Sqlite) over an in-memory database — the one backend whose full
        // read path (driver byte[] materialization + our pooled parse + filter) is benchmarkable without a container.
        this.sqlite = SqliteSourceCredentialStore.ConnectAsync("Data Source=:memory:").AsTask().GetAwaiter().GetResult();
        this.sqlite.AddAsync(Definition, "bench", default).AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup() => this.sqlite.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>The shared CTJ read+filter primitive in isolation (InMemory): pooled parse + deferred-holder usage
    /// check, no driver, no STJ — every backend runs exactly this after its driver hands back the bytes.</summary>
    /// <returns>Whether a binding was resolved (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public bool InMemory_ResolveForUsage_Miss()
    {
        using ParsedJsonDocument<SourceCredentialBinding>? binding =
            this.inMemory.ResolveForUsageAsync("petstore", "production", this.runTags, default).AsTask().GetAwaiter().GetResult();
        return binding is not null;
    }

    /// <summary>The same resolution through a REAL embedded driver (SQLite): the query + driver byte[] materialization
    /// plus the shared parse+filter. The delta over InMemory is the driver-leaf cost — proving an actual backend's read
    /// path stays disciplined (byte[] at the leaf, pooled parse, no STJ), not just the InMemory reference.</summary>
    /// <returns>Whether a binding was resolved (prevents dead-code elimination).</returns>
    [Benchmark]
    public bool Sqlite_ResolveForUsage_Miss()
    {
        using ParsedJsonDocument<SourceCredentialBinding>? binding =
            this.sqlite.ResolveForUsageAsync("petstore", "production", this.runTags, default).AsTask().GetAwaiter().GetResult();
        return binding is not null;
    }
}