// <copyright file="RunnerAuthorizationStatusCheckBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures branching on an <see cref="EnvironmentRunnerAuthorization"/>'s status (§5.5) — the check the dispatch
/// authorization gate runs every poll cycle, and the handler runs on each authorize/revoke. Starts from an
/// already-parsed document so the benchmark isolates the comparison, not the parse.
/// <list type="bullet">
/// <item><see cref="Status_StringRealise"/> — the regression: <c>(string)Status</c> realises a managed C# string from the
/// JSON value, then ordinal-compares it to the wire name (one string allocation per call).</item>
/// <item><see cref="Status_StringFree"/> — the fix (<see cref="EnvironmentRunnerAuthorization.IsAuthorized"/>): the JSON
/// value's bytes are compared against the UTF-8 literal via <c>ValueEquals</c> — no string is realised.</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class RunnerAuthorizationStatusCheckBenchmarks
{
    private static readonly byte[] Json =
        """
        {
          "environment": "production",
          "runnerId": "runner-eu-1",
          "status": "Authorized",
          "createdBy": "runner-eu-1",
          "createdAt": "2026-01-01T00:00:00+00:00",
          "decidedBy": "alice",
          "decidedAt": "2026-01-01T01:00:00+00:00",
          "etag": "\"1\""
        }
        """u8.ToArray();

    private ParsedJsonDocument<EnvironmentRunnerAuthorization> doc = null!;

    [GlobalSetup]
    public void Setup() => this.doc = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(Json);

    [GlobalCleanup]
    public void Cleanup() => this.doc.Dispose();

    /// <summary>The regression: realise the JSON status to a C# string, then ordinal-compare to the wire name.</summary>
    [Benchmark(Baseline = true)]
    public bool Status_StringRealise() => string.Equals((string)this.doc.RootElement.Status, "Authorized", StringComparison.Ordinal);

    /// <summary>The fix: compare the JSON value's bytes against the UTF-8 literal — no string realised.</summary>
    [Benchmark]
    public bool Status_StringFree() => this.doc.RootElement.IsAuthorized;
}
