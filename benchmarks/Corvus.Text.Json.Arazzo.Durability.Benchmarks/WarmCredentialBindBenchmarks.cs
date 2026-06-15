// <copyright file="WarmCredentialBindBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.SourceCredentials.Http;
using Corvus.Text.Json.OpenApi.HttpTransport;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// The §13.4 dual gate (allocation half): proves secure-by-default is ~free on the warm path. <c>Warm_GetProvider</c>
/// is the per-call cost of obtaining the source's authentication provider from the runner's
/// <see cref="SourceCredentialCache"/> once warm — exactly what <c>SourceCredentialAuthenticationProvider</c> does on
/// each outbound request: a precomputed run-tags key plus a lock-free dictionary read of the cached, built provider.
/// It must be ≈0 B (no secret-store I/O, no resolve, no build, and — crucially — no per-request canonicalization of the
/// run's tags). The run carries realistic <c>sys:tenant</c>/<c>sys:workflow</c> tags so the benchmark would catch a
/// regression that canonicalizes them on the warm path. <c>Warm_GetProvider_PerCallCanonicalize</c> is the
/// convenience overload that re-canonicalizes every call — the regression we removed, kept for contrast.
/// <c>Cold_ResolveAndBuild</c> is the one-time miss cost the warm path amortizes away.
/// </summary>
public class WarmCredentialBindBenchmarks
{
    private const string EnvVar = "CORVUS_ARAZZO_BENCH_SECRET";

    // A realistic run carries its deployment-stamped identity tags (§14.2): the tenant and the immutable base workflow
    // id. These are non-empty and multi-tag, so canonicalizing them is real work — which the warm path must not do.
    private static readonly SecurityTagSet RunTags = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:tenant", "contoso"),
        new SecurityTag("sys:workflow", "nightly-reconcile"),
    ]);

    private SourceCredentialCache cache = null!;
    private string runTagsKey = null!;

    [GlobalSetup]
    public void Setup()
    {
        Environment.SetEnvironmentVariable(EnvVar, "benchmark-api-key");
        var store = new InMemorySourceCredentialStore();
        store.AddAsync(
            new SourceCredentialDefinition(
                "petstore",
                "production",
                SourceCredentialKind.ApiKey,
                [new SecretReferenceDefinition("value", $"env://{EnvVar}")],
                [new CredentialConfigDefinition("parameterName", "X-Api-Key")]),
            "bench",
            default).AsTask().GetAwaiter().GetResult();

        var factory = new SourceCredentialProviderFactory(new EnvSecretResolver());
        this.cache = new SourceCredentialCache(store, factory, ttl: TimeSpan.FromHours(1));

        // The per-source provider canonicalizes the run's tags once at bind time; the warm path reuses this key.
        this.runTagsKey = SourceCredentialKey.CanonicalTags(RunTags);

        // Pre-warm so Warm_GetProvider hits the cached entry.
        this.cache.GetAsync("petstore", "production", this.runTagsKey, RunTags, default).AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.cache.Dispose();
        Environment.SetEnvironmentVariable(EnvVar, null);
    }

    /// <summary>The production warm path: a precomputed run-tags key plus a dictionary read. Must be 0 B.</summary>
    /// <returns>The cached provider.</returns>
    [Benchmark]
    public IHttpAuthenticationProvider? Warm_GetProvider()
        => this.cache.GetAsync("petstore", "production", this.runTagsKey, RunTags, default).GetAwaiter().GetResult();

    /// <summary>The convenience overload that canonicalizes the run's tags every call — the regression we eliminated by
    /// precomputing the key in the provider. Shown for contrast; this one allocates.</summary>
    /// <returns>The cached provider.</returns>
    [Benchmark]
    public IHttpAuthenticationProvider? Warm_GetProvider_PerCallCanonicalize()
        => this.cache.GetAsync("petstore", "production", RunTags, default).GetAwaiter().GetResult();

    /// <summary>The one-time miss: invalidate then resolve+build. Amortized away by the warm path.</summary>
    /// <returns>The freshly built provider.</returns>
    [Benchmark(Baseline = true)]
    public IHttpAuthenticationProvider? Cold_ResolveAndBuild()
    {
        this.cache.Invalidate("petstore", "production");
        return this.cache.GetAsync("petstore", "production", this.runTagsKey, RunTags, default).GetAwaiter().GetResult();
    }
}