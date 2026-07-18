// <copyright file="EligibilityScanBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of the self-elevation stored-eligibility lookup (design §16.5.3). Resolving whether a
/// requester may self-elevate matches one <c>eligibleOnly</c> binding for their subject among the whole policy set.
/// <see cref="Old_FullScan_MaterializeClaims"/> is the pre-change path: read every binding and realise each one's claim
/// type and value as managed strings to compare (two strings per binding, for every binding).
/// <see cref="New_BySubjectQuery"/> is the by-subject reverse index: the store returns only this subject's bindings,
/// comparing the claim bytes span-to-span with no per-binding string. Both reach the identical set; only the per-scan
/// allocation differs. (The in-memory store still reads the full set behind the default filter, so this isolates the
/// string-realisation win; a native backend additionally never reads the non-matching rows.)
/// </summary>
[MemoryDiagnoser]
public class EligibilityScanBenchmarks
{
    private const int BindingCount = 200;
    private const string SubjectClaimType = "sub";
    private const string SubjectClaimValue = "alice";

    // Typed as the interface: ListBindingsForSubjectAsync is a default interface method (the in-memory store inherits
    // it), so it is invoked through ISecurityPolicyStore, exactly as the approval service calls it.
    private ISecurityPolicyStore store = null!;
    private ParsedJsonDocument<JsonString> claimType = null!;
    private ParsedJsonDocument<JsonString> claimValue = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.store = new InMemorySecurityPolicyStore();

        // A realistic policy set: one requester's few eligibility bindings among many other subjects' bindings.
        for (int i = 0; i < BindingCount; i++)
        {
            bool match = i % 50 == 0;
            string type = match ? SubjectClaimType : "group";
            string value = match ? SubjectClaimValue : "team-" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft(
                type,
                value,
                VerbGrant.Rules("workflow-access:flow"),
                VerbGrant.None,
                VerbGrant.None,
                order: i,
                eligibleOnly: true);
            this.store.AddBindingAsync(draft.RootElement, "seed", default).AsTask().GetAwaiter().GetResult().Dispose();
        }

        this.claimType = ParseJsonString(SubjectClaimType);
        this.claimValue = ParseJsonString(SubjectClaimValue);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.claimType.Dispose();
        this.claimValue.Dispose();
    }

    /// <summary>The pre-change path: read every binding and realise each one's claim type/value as managed strings to
    /// match the subject (two strings per binding, for every binding in the store).</summary>
    /// <returns>The matched-binding count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public async Task<int> Old_FullScan_MaterializeClaims()
    {
        int matched = 0;
        using PooledDocumentList<SecurityBindingDocument> bindings = await this.store.ListBindingsAsync(default).ConfigureAwait(false);
        foreach (SecurityBindingDocument binding in bindings)
        {
            if (string.Equals(binding.ClaimTypeValue, SubjectClaimType, StringComparison.Ordinal)
                && string.Equals(binding.ClaimValueOrNull, SubjectClaimValue, StringComparison.Ordinal))
            {
                matched++;
            }
        }

        return matched;
    }

    /// <summary>The by-subject reverse index: the store returns only this subject's bindings, compared span-to-span with
    /// no per-binding claim string.</summary>
    /// <returns>The matched-binding count.</returns>
    [Benchmark]
    public async Task<int> New_BySubjectQuery()
    {
        using PooledDocumentList<SecurityBindingDocument> bindings = await this.store.ListBindingsForSubjectAsync(
            this.claimType.RootElement,
            this.claimValue.RootElement,
            default).ConfigureAwait(false);
        return bindings.Count;
    }

    private static ParsedJsonDocument<JsonString> ParseJsonString(string value)
        => ParsedJsonDocument<JsonString>.Parse(System.Text.Encoding.UTF8.GetBytes("\"" + value + "\""));
}