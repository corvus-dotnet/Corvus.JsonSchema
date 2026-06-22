// <copyright file="AccessRequestDraftBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the access-request SUBMIT draft build (POST /accessRequests): the per-request work the handler does between
/// "the framework parsed the body" and "the draft document the approval pipeline reads", starting from an already-parsed
/// body so the benchmark isolates the seam, not the parse.
/// <list type="bullet">
/// <item><see cref="Draft_FromStrings"/> — the old seam: the body's <c>requestedScopes</c> array is read into a
/// <see cref="List{T}"/> of managed strings (one transcode per scope) and the scalars realised via <c>(string)</c> before
/// <c>AccessRequest.Draft</c> rebuilds them — a bytes→string→bytes u-turn.</item>
/// <item><see cref="Draft_FromElements"/> — the new seam: the body's already-parsed JSON values
/// (<c>baseWorkflowId</c>/<c>requestedScopes</c>/<c>reason</c>) are carried bytes-to-bytes into the draft (no list, no
/// per-field strings); only the principal-derived subject/label stay as the strings they already are.</item>
/// </list>
/// </summary>
[MemoryDiagnoser]
public class AccessRequestDraftBenchmarks
{
    private const string SubjectClaimType = "sub";
    private const string SubjectClaimValue = "alice";
    private const string RequesterLabel = "Alice Smith";

    // A representative POST /accessRequests body, already parsed (the HTTP framework parsed it before the handler runs).
    private static readonly byte[] BodyJson =
        """
        {
          "baseWorkflowId": "orders-export",
          "requestedScopes": [ "runs:write", "runs:read" ],
          "reason": "Need to run the nightly export ad hoc.",
          "requestedDurationSeconds": 3600
        }
        """u8.ToArray();

    private ParsedJsonDocument<Models.AccessRequestSubmit> body = null!;

    [GlobalSetup]
    public void Setup() => this.body = ParsedJsonDocument<Models.AccessRequestSubmit>.Parse(BodyJson);

    [GlobalCleanup]
    public void Cleanup() => this.body.Dispose();

    /// <summary>The old seam: read the body's scopes into a managed-string list + realise the scalars, then build the draft.</summary>
    [Benchmark(Baseline = true)]
    public void Draft_FromStrings()
    {
        Models.AccessRequestSubmit b = this.body.RootElement;

        var scopes = new List<string>();
        foreach (Models.JsonString scope in b.RequestedScopes.EnumerateArray())
        {
            scopes.Add((string)scope);
        }

        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(
            (string)b.BaseWorkflowId,
            scopes,
            SubjectClaimType,
            SubjectClaimValue,
            RequesterLabel,
            b.Reason.IsNotUndefined() ? (string)b.Reason : null,
            b.RequestedDurationSeconds.IsNotUndefined() ? (long)b.RequestedDurationSeconds : null);
    }

    /// <summary>The new seam: carry the body's already-parsed JSON values bytes-to-bytes into the draft (no list, no strings).</summary>
    [Benchmark]
    public void Draft_FromElements()
    {
        Models.AccessRequestSubmit b = this.body.RootElement;

        using ParsedJsonDocument<AccessRequest> draft = AccessRequest.Draft(
            (JsonElement)b.BaseWorkflowId,
            (JsonElement)b.RequestedScopes,
            SubjectClaimType,
            SubjectClaimValue,
            RequesterLabel,
            (JsonElement)b.Reason,
            b.RequestedDurationSeconds.IsNotUndefined() ? (long)b.RequestedDurationSeconds : null);
    }
}
