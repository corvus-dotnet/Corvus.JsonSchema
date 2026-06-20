// <copyright file="GranteeProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the grantee-picker projection (design §16.5.4): a page of <see cref="ResolvedPrincipal"/>s
/// (each carrying its value/label as owned UTF-8 from the bytes-to-bytes adapter path) is projected into the
/// <c>GranteeList</c> response body and materialised into the response workspace exactly as the handler does (the
/// terminal <c>SearchGranteesResult.Ok</c> step — a <c>CreateBuilder(ws, body)</c> materialisation — is modelled in
/// every arm). All three arms emit the identical response bytes; only the projection mechanism differs, so the deltas
/// isolate (a) the bytes-vs-string cost and (b) the closure-vs-apparatus cost.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="Baseline_StringClosures"/> — the naive original: each value/label read as a managed
/// <see cref="string"/> (a per-row <c>GetString</c>) and the model built through capturing builder closures. One
/// materialisation.</item>
/// <item><see cref="Apparatus_SpanThreaded"/> — the (reverted) <c>Build&lt;TContext&gt;</c> apparatus: value/label spans
/// threaded through a ref-struct context, no per-item closure, but <c>CreateBuilder&lt;TContext&gt;().RootElement</c>
/// realises the body eagerly and <c>Ok</c> then re-materialises it — two materialisations.</item>
/// <item><see cref="Canonical_SpanClosures"/> — the canonical handler pattern: value/label spans read bytes-to-bytes
/// inside a lazy <c>GranteeList.Source</c> built through builder closures (the same shape as <c>WhoAmI</c> / the catalog
/// and security list projections), materialised once by <c>Ok</c>. Closures per item, one materialisation.</item>
/// </list>
/// The <c>{dimension, value}</c> grant sub-array is the genuine response leaf and is built identically in all three.
/// </remarks>
public class GranteeProjectionBenchmarks
{
    private const int PageSize = 20;

    // The grants a grantee's sys: identity describes back to — the genuine response leaf, fixed and identical for all paths.
    private static readonly IReadOnlyList<CredentialUsageGrant> Grants = [new CredentialUsageGrant("tenant", "acme")];

    private ResolvedPrincipal[] principals = null!;

    [GlobalSetup]
    public void Setup()
    {
        SecurityTagSet identity = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);
        this.principals = new ResolvedPrincipal[PageSize];
        for (int i = 0; i < PageSize; i++)
        {
            // Span-constructed (the bytes-to-bytes adapter path): the value/label are owned UTF-8, not managed strings.
            this.principals[i] = new ResolvedPrincipal(GranteeKind.Team, System.Text.Encoding.UTF8.GetBytes($"acme-team-{i:D2}"), "Acme Team"u8, hasLabel: true, identity);
        }
    }

    /// <summary>The naive original: each value/label read as a managed string (per-row <c>GetString</c>) and the model built through capturing closures; one materialisation.</summary>
    /// <returns>The grantee count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Baseline_StringClosures()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        Models.GranteeList.Source body = Models.GranteeList.Build((ref Models.GranteeList.Builder b) => b.Create(
            grantees: Models.GranteeList.ResolvedGranteeArray.Build((ref Models.GranteeList.ResolvedGranteeArray.Builder ab) =>
            {
                foreach (ResolvedPrincipal p in this.principals)
                {
                    ab.AddItem(StringGrantee(p));
                }
            })));
        return Materialize(ws, body);
    }

    /// <summary>The reverted apparatus: value/label spans threaded through a ref-struct context (no per-item closure), but the body is realised eagerly via <c>CreateBuilder&lt;TContext&gt;().RootElement</c> and re-materialised by <c>Ok</c> — two materialisations.</summary>
    /// <returns>The grantee count.</returns>
    [Benchmark]
    public int Apparatus_SpanThreaded()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        var state = new ProjectState(this.principals);
        Models.GranteeList grantees = Models.GranteeList.CreateBuilder(
            ws,
            in state,
            Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildGrantees)).RootElement;
        return Materialize(ws, grantees);
    }

    /// <summary>The canonical handler pattern: value/label spans read bytes-to-bytes inside a lazy <c>GranteeList.Source</c> built through builder closures, materialised once by <c>Ok</c>.</summary>
    /// <returns>The grantee count.</returns>
    [Benchmark]
    public int Canonical_SpanClosures()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        Models.GranteeList.Source body = Models.GranteeList.Build((ref Models.GranteeList.Builder b) => b.Create(
            grantees: Models.GranteeList.ResolvedGranteeArray.Build((ref Models.GranteeList.ResolvedGranteeArray.Builder ab) =>
            {
                foreach (ResolvedPrincipal p in this.principals)
                {
                    ab.AddItem(SpanGrantee(p));
                }
            })));
        return Materialize(ws, body);
    }

    /// <summary>The shipped target: the real handler boundary — closure-free (<c>Build&lt;TContext&gt;</c>) handed to the
    /// generated <c>SearchGranteesResult.Ok&lt;TContext&gt;</c>, which routes the context-threaded body through the new
    /// <c>CreateBuilder&lt;TContext&gt;(in Source&lt;TContext&gt;)</c> in a single pass with no re-materialisation.</summary>
    /// <returns>The grantee count.</returns>
    [Benchmark]
    public int Target_OkContextThreaded()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        var state = new ProjectState(this.principals);
        Models.GranteeList.Source<ProjectState> body = Models.GranteeList.Build(
            in state,
            grantees: Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildGrantees));
        return SearchGranteesResult.Ok(body, ws).Body.GetProperty("grantees"u8).GetArrayLength();
    }

    // Models the terminal SearchGranteesResult.Ok step: CreateBuilder(ws, body).RootElement (one materialisation into the
    // response workspace). The apparatus arm reaches this having already materialised once, so it pays this twice.
    private static int Materialize(JsonWorkspace ws, in Models.GranteeList.Source body)
        => Models.GranteeList.CreateBuilder(ws, body, 30).RootElement.Grantees.GetArrayLength();

    // ── string projection (the naive original) ─────────────────────────────────────────────────────────────────────

    private static Models.ResolvedGrantee.Source StringGrantee(ResolvedPrincipal p)
    {
        string kindToken = p.Kind.ToToken();
        string value = p.Value;
        string? label = p.Label;
        return Models.ResolvedGrantee.Build((ref Models.ResolvedGrantee.Builder b) =>
        {
            Models.ResolvedGrantee.AdministratorIdentityArray.Source identity = IdentityArray();
            if (label is null)
            {
                b.Create(complete: true, identity: identity, kind: kindToken, source: "directory", value: value);
            }
            else
            {
                b.Create(complete: true, identity: identity, kind: kindToken, source: "directory", value: value, label: label);
            }
        });
    }

    // ── canonical bytes projection (lazy Source closures, spans) ───────────────────────────────────────────────────

    private static Models.ResolvedGrantee.Source SpanGrantee(ResolvedPrincipal p)
        => Models.ResolvedGrantee.Build((ref Models.ResolvedGrantee.Builder b) =>
        {
            Models.ResolvedGrantee.AdministratorIdentityArray.Source identity = IdentityArray();
            if (p.HasLabel)
            {
                b.Create(complete: true, identity: identity, kind: p.Kind.ToTokenUtf8(), source: "directory"u8, value: p.ValueMemory.Span, label: (Models.JsonString.Source)p.LabelMemory.Span);
            }
            else
            {
                b.Create(complete: true, identity: identity, kind: p.Kind.ToTokenUtf8(), source: "directory"u8, value: p.ValueMemory.Span);
            }
        });

    private static Models.ResolvedGrantee.AdministratorIdentityArray.Source IdentityArray()
        => Models.ResolvedGrantee.AdministratorIdentityArray.Build((ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder ab) =>
        {
            foreach (CredentialUsageGrant grant in Grants)
            {
                ab.AddItem(Models.AdministratorIdentity.Build((ref Models.AdministratorIdentity.Builder ib) => ib.Create(grant.Dimension, grant.Value)));
            }
        });

    // ── apparatus projection (Build<TContext> + ref-struct context, spans) ─────────────────────────────────────────

    private static void BuildGrantees(in ProjectState s, ref Models.GranteeList.ResolvedGranteeArray.Builder array)
    {
        foreach (ResolvedPrincipal p in s.Principals)
        {
            var item = new ItemState(p);
            array.AddItem(Models.ResolvedGrantee.Build(in item, BuildGrantee));
        }
    }

    private static void BuildGrantee(in ItemState item, ref Models.ResolvedGrantee.Builder grantee)
    {
        grantee.Create(
            in item,
            complete: true,
            identity: Models.ResolvedGrantee.AdministratorIdentityArray.Build(in item, BuildIdentity),
            kind: item.Principal.Kind.ToTokenUtf8(),
            source: "directory"u8,
            value: item.Principal.ValueMemory.Span,
            label: item.Principal.HasLabel ? (Models.JsonString.Source)item.Principal.LabelMemory.Span : default);
    }

    private static void BuildIdentity(in ItemState item, ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder identities)
    {
        foreach (CredentialUsageGrant grant in Grants)
        {
            identities.AddItem(Models.AdministratorIdentity.Build((ref Models.AdministratorIdentity.Builder ib) => ib.Create(grant.Dimension, grant.Value)));
        }
    }

    private readonly ref struct ProjectState(ResolvedPrincipal[] principals)
    {
        public ResolvedPrincipal[] Principals { get; } = principals;
    }

    private readonly ref struct ItemState(ResolvedPrincipal principal)
    {
        public ResolvedPrincipal Principal { get; } = principal;
    }
}