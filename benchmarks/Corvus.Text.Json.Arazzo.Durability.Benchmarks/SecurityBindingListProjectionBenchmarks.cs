// <copyright file="SecurityBindingListProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the security-binding-summary LIST projection (design §14.2): a page of stored
/// <see cref="SecurityBindingDocument"/>s is projected into the <c>SecurityBindingList</c> response body and materialised
/// into the response workspace exactly as the handler does (the terminal <c>ListSecurityBindingsResult.Ok</c> step — a
/// single <c>CreateBuilder</c> materialisation — is modelled in both arms). Both arms emit the identical response bytes
/// and carry every leaf bytes-native (<c>Models.JsonString.From</c> / <c>Models.VerbGrant.From</c> — the field bridge
/// FIX #3 already measures in <see cref="SecurityBindingSummaryProjectionBenchmarks"/>); only the projection MECHANISM
/// differs, so the delta here isolates the closure-vs-context cost the <c>ToBindingSource</c> refactor removes.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="PerItemClosure"/> — the pre-refactor shape: the list body, the nested array and each summary are
/// built through capturing builder closures (an outer list closure + the nested array closure + a per-binding summary
/// closure), and the optional scalars realised via the <c>IsNotUndefined</c>/<c>UpdatedAtValue</c> guards. One
/// materialisation.</item>
/// <item><see cref="ContextThreaded"/> — the shipped shape: the whole body is a context-threaded
/// <c>SecurityBindingList.Source&lt;TContext&gt;</c> (the generated <c>Build&lt;TContext&gt;</c> + static build methods,
/// the binding itself threaded as the context — no closure; the optional scalars carried bare through <c>From()</c>,
/// which propagates Undefined) materialised once via the generic <c>CreateBuilder&lt;TContext&gt;</c>.</item>
/// </list>
/// </remarks>
[MemoryDiagnoser]
public class SecurityBindingListProjectionBenchmarks
{
    private const int PageSize = 10;

    // A representative stored binding: claim + claimValue, the three verb grants (rules/rules/none, each carrying
    // unrestricted), order, and the optional description/lastUpdated* audit fields.
    private static readonly byte[] StoredJson =
        """
        {
          "id": "binding-1",
          "claimType": "role",
          "claimValue": "tenant-admin",
          "read": { "unrestricted": false, "ruleNames": [ "tenant-scoped" ] },
          "write": { "unrestricted": false, "ruleNames": [ "tenant-scoped" ] },
          "purge": { "unrestricted": false },
          "order": 10,
          "createdBy": "alice",
          "createdAt": "1970-01-01T00:00:00+00:00",
          "lastUpdatedBy": "bob",
          "lastUpdatedAt": "1970-01-01T08:00:00+00:00",
          "description": "Tenant admin binding.",
          "etag": "etag-1"
        }
        """u8.ToArray();

    private ParsedJsonDocument<SecurityBindingDocument> document = null!;
    private SecurityBindingDocument[] bindings = null!;
    private JsonWorkspace workspace = null!;

    [GlobalSetup]
    public void Setup()
    {
        this.document = ParsedJsonDocument<SecurityBindingDocument>.Parse(StoredJson);
        this.bindings = new SecurityBindingDocument[PageSize];
        for (int i = 0; i < PageSize; i++)
        {
            this.bindings[i] = this.document.RootElement;
        }

        this.workspace = JsonWorkspace.Create();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.document.Dispose();
        this.workspace.Dispose();
    }

    /// <summary>The pre-refactor shape: the list, the nested array and each summary are built through capturing builder
    /// closures; one materialisation into the response workspace.</summary>
    /// <returns>The summary count (defeats dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int PerItemClosure()
    {
        Models.SecurityBindingList.Source body = Models.SecurityBindingList.Build((ref Models.SecurityBindingList.Builder b) => b.Create(
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build((ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder ab) =>
            {
                foreach (SecurityBindingDocument binding in this.bindings)
                {
                    ab.AddItem(ClosureBindingSummary(binding));
                }
            })));
        return Models.SecurityBindingList.CreateBuilder(this.workspace, body, 30).RootElement.Bindings.GetArrayLength();
    }

    /// <summary>The shipped shape: the whole body is a context-threaded <c>SecurityBindingList.Source&lt;TContext&gt;</c>
    /// (no closure) materialised once via the generic <c>CreateBuilder&lt;TContext&gt;</c>.</summary>
    /// <returns>The summary count.</returns>
    [Benchmark]
    public int ContextThreaded()
    {
        IReadOnlyList<SecurityBindingDocument> list = this.bindings;
        Models.SecurityBindingList.Source<IReadOnlyList<SecurityBindingDocument>> body = Models.SecurityBindingList.Build(
            in list,
            bindings: Models.SecurityBindingList.SecurityBindingSummaryArray.Build(in list, BuildBindingSummaries));
        return Models.SecurityBindingList.CreateBuilder(this.workspace, in body, 30).RootElement.Bindings.GetArrayLength();
    }

    // ── pre-refactor closure projection (the optional scalars realised via the IsNotUndefined/UpdatedAtValue guards) ──

    private static Models.SecurityBindingSummary.Source ClosureBindingSummary(SecurityBindingDocument binding)
        => Models.SecurityBindingSummary.Build((ref Models.SecurityBindingSummary.Builder b) =>
        {
            Models.JsonString.Source claimValue = default;
            if (binding.ClaimValue.IsNotUndefined())
            {
                claimValue = Models.JsonString.From(binding.ClaimValue);
            }

            Models.JsonString.Source description = default;
            if (binding.Description.IsNotUndefined())
            {
                description = Models.JsonString.From(binding.Description);
            }

            Models.JsonString.Source lastUpdatedBy = default;
            if (binding.LastUpdatedBy.IsNotUndefined())
            {
                lastUpdatedBy = Models.JsonString.From(binding.LastUpdatedBy);
            }

            Models.JsonDateTime.Source lastUpdatedAt = default;
            if (binding.UpdatedAtValue is { } ua)
            {
                lastUpdatedAt = ua;
            }

            b.Create(
                claimType: Models.JsonString.From(binding.ClaimType),
                createdAt: binding.CreatedAtValue,
                createdBy: Models.JsonString.From(binding.CreatedBy),
                etag: Models.JsonString.From(binding.Etag),
                id: Models.JsonString.From(binding.Id),
                order: binding.OrderValue,
                purge: Models.VerbGrant.From(binding.Purge),
                read: Models.VerbGrant.From(binding.Read),
                write: Models.VerbGrant.From(binding.Write),
                claimValue: claimValue,
                description: description,
                lastUpdatedAt: lastUpdatedAt,
                lastUpdatedBy: lastUpdatedBy);
        });

    // ── shipped context-threaded projection (mirrors ArazzoControlPlaneSecurityHandler) ──────────────────────────────

    private static void BuildBindingSummaries(in IReadOnlyList<SecurityBindingDocument> bindings, ref Models.SecurityBindingList.SecurityBindingSummaryArray.Builder array)
    {
        foreach (SecurityBindingDocument binding in bindings)
        {
            array.AddItem(Models.SecurityBindingSummary.Build(in binding, BuildBindingSummary));
        }
    }

    private static void BuildBindingSummary(in SecurityBindingDocument binding, ref Models.SecurityBindingSummary.Builder b)
        => b.Create(
            claimType: Models.JsonString.From(binding.ClaimType),
            createdAt: binding.CreatedAtValue,
            createdBy: Models.JsonString.From(binding.CreatedBy),
            etag: Models.JsonString.From(binding.Etag),
            id: Models.JsonString.From(binding.Id),
            order: binding.OrderValue,
            purge: Models.VerbGrant.From(binding.Purge),
            read: Models.VerbGrant.From(binding.Read),
            write: Models.VerbGrant.From(binding.Write),
            claimValue: Models.JsonString.From(binding.ClaimValue),
            description: Models.JsonString.From(binding.Description),
            lastUpdatedAt: Models.JsonDateTime.From(binding.LastUpdatedAt),
            lastUpdatedBy: Models.JsonString.From(binding.LastUpdatedBy));
}
