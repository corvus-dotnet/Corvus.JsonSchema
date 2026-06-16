// <copyright file="CredentialStoreReadBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;

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
    private InMemorySourceCredentialStore store = null!;
    private SecurityTagSet runTags;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemorySourceCredentialStore();
        this.store.AddAsync(
            new SourceCredentialDefinition(
                "petstore",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", "keyvault://petstore-apikey#3")],
                [new CredentialConfigDefinition("parameterName", "X-Api-Key")],
                UsageTags: SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso")])),
            "bench",
            default).AsTask().GetAwaiter().GetResult();

        this.runTags = SecurityTagSet.FromTags(
        [
            new SecurityTag("sys:tenant", "contoso"),
            new SecurityTag("sys:workflow", "nightly-reconcile"),
        ]);
    }

    /// <summary>The cache-miss resolution: pooled parse + deferred-holder usage check. Disciplined, small, no STJ.</summary>
    /// <returns>Whether a binding was resolved (prevents dead-code elimination).</returns>
    [Benchmark]
    public bool ResolveForUsage_Miss()
    {
        using ParsedJsonDocument<SourceCredentialBinding>? binding =
            this.store.ResolveForUsageAsync("petstore", "production", this.runTags, default).AsTask().GetAwaiter().GetResult();
        return binding is not null;
    }
}