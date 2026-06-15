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
/// <see cref="SourceCredentialCache"/> once warm — it must be ≈0 B (a lock-free dictionary read of the cached, built
/// provider; no secret-store I/O, no resolve, no build). <c>Cold_ResolveAndBuild</c> is the one-time miss cost
/// (resolve the secret + build the provider) the warm path amortizes away, shown for contrast.
/// </summary>
public class WarmCredentialBindBenchmarks
{
    private const string EnvVar = "CORVUS_ARAZZO_BENCH_SECRET";
    private SourceCredentialCache cache = null!;

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

        // Pre-warm so Warm_GetProvider hits the cached entry.
        this.cache.GetAsync("petstore", "production", default).AsTask().GetAwaiter().GetResult();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.cache.Dispose();
        Environment.SetEnvironmentVariable(EnvVar, null);
    }

    /// <summary>The warm path: obtain the cached provider. Must be ≈0 B.</summary>
    /// <returns>The cached provider.</returns>
    [Benchmark]
    public IHttpAuthenticationProvider? Warm_GetProvider()
        => this.cache.GetAsync("petstore", "production", default).GetAwaiter().GetResult();

    /// <summary>The one-time miss: invalidate then resolve+build. Amortized away by the warm path.</summary>
    /// <returns>The freshly built provider.</returns>
    [Benchmark(Baseline = true)]
    public IHttpAuthenticationProvider? Cold_ResolveAndBuild()
    {
        this.cache.Invalidate("petstore", "production");
        return this.cache.GetAsync("petstore", "production", default).GetAwaiter().GetResult();
    }
}