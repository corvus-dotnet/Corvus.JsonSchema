// <copyright file="GranteeProjectionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation floor of the grantee-picker directory projection (design §16.5.4): a page of
/// <see cref="ResolvedPrincipal"/>s (each carrying its value/label as owned UTF-8 from the bytes-to-bytes adapter path) is
/// written into the <c>GranteeList</c> response. <see cref="Old_String_Closures"/> is the pre-change path — read each
/// principal's <see cref="ResolvedPrincipal.Value"/>/<c>Label</c> as a managed <see cref="string"/> (a per-row
/// <c>GetString</c>) and build the model through capturing builder lambdas (a closure per grantee). <see cref="New_Bytes_Threaded"/>
/// is the bytes-to-bytes path: the value/label spans are threaded straight into the generated builder via its
/// context-threading form (static lambdas + a ref-struct state) — no managed string, no closure. Both emit the identical
/// response bytes; only the per-grantee projection allocation differs. (The <c>{dimension, value}</c> grant sub-array is
/// the genuine response leaf and is built identically by both, so the delta isolates the value/label + closure cost.)
/// </summary>
public class GranteeProjectionBenchmarks
{
    private const int PageSize = 20;

    // The grants a grantee's sys: identity describes back to — the genuine response leaf, fixed and identical for both paths.
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
            this.principals[i] = new ResolvedPrincipal(GranteeKind.Team, Encoding.UTF8.GetBytes($"acme-team-{i:D2}"), "Acme Team"u8, hasLabel: true, identity);
        }
    }

    /// <summary>The pre-change projection: each value/label is read as a managed string (per-row <c>GetString</c>) and the model is built through capturing lambdas (a closure per grantee).</summary>
    /// <returns>The grantee count (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Old_String_Closures()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        Models.GranteeList.Source body = Models.GranteeList.Build((ref Models.GranteeList.Builder b) => b.Create(
            grantees: Models.GranteeList.ResolvedGranteeArray.Build((ref Models.GranteeList.ResolvedGranteeArray.Builder ab) =>
            {
                foreach (ResolvedPrincipal p in this.principals)
                {
                    ab.AddItem(OldToGrantee(p));
                }
            })));
        return Models.GranteeList.CreateBuilder(ws, body).RootElement.Grantees.GetArrayLength();
    }

    /// <summary>The bytes-to-bytes projection: the value/label spans are threaded into the generated builder via its context-threading form — no managed string, no closure.</summary>
    /// <returns>The grantee count.</returns>
    [Benchmark]
    public int New_Bytes_Threaded()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        var state = new ProjectState(this.principals);
        return Models.GranteeList.CreateBuilder(
            ws,
            in state,
            Models.GranteeList.ResolvedGranteeArray.Build(in state, BuildGrantees)).RootElement.Grantees.GetArrayLength();
    }

    private static Models.ResolvedGrantee.Source OldToGrantee(ResolvedPrincipal p)
    {
        string kindToken = p.Kind.ToToken();
        string value = p.Value;
        string? label = p.Label;
        return Models.ResolvedGrantee.Build((ref Models.ResolvedGrantee.Builder b) =>
        {
            var identityArray = Models.ResolvedGrantee.AdministratorIdentityArray.Build((ref Models.ResolvedGrantee.AdministratorIdentityArray.Builder ab) =>
            {
                foreach (CredentialUsageGrant grant in Grants)
                {
                    ab.AddItem(Models.AdministratorIdentity.Build((ref Models.AdministratorIdentity.Builder ib) => ib.Create(grant.Dimension, grant.Value)));
                }
            });

            if (label is null)
            {
                b.Create(complete: true, identity: identityArray, kind: kindToken, source: "directory", value: value);
            }
            else
            {
                b.Create(complete: true, identity: identityArray, kind: kindToken, source: "directory", value: value, label: label);
            }
        });
    }

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