// <copyright file="AdministratorIdentityBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using BenchmarkDotNet.Attributes;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Models = Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Allocation profile of the workflow-administration resolved-identity path (design §15/§16.5.4). Two concerns are
/// proven allocation-clean:
/// <list type="bullet">
/// <item><b>The identity digest</b> (the stable removal key, recomputed on every list/add/remove sighting):
/// <see cref="Digest_Compute_String"/> is the string form (one 64-char hex GC string per identity);
/// <see cref="Digest_FormatUtf8_Span"/> is the shipped form that writes the hex straight into a stack span — the only
/// heap traffic is the pooled scratch the digest already used, so it nets zero managed allocation.</item>
/// <item><b>The administrators list response</b> (<c>GET /administrators</c> and the body every add/remove/transfer
/// returns): a record of administrators is projected to the <c>AdministratorList</c> of <c>AdministratorGrant</c>s and
/// materialised into the response workspace exactly as the handler does. <see cref="Project_List_StringDigest"/> is the
/// naive form (a per-administrator digest string + a managed string per identity grant); <see cref="Project_List_SpanDigest"/>
/// is the shipped bytes-to-bytes form (digest formatted into a pooled buffer, each grant written from the identity's
/// unescaped UTF-8). Both emit identical response bytes; the delta is the per-administrator string traffic the shipped
/// path removes, leaving only the single response-workspace materialisation.</item>
/// </list>
/// The administrators carry tags only (no <c>kind</c>/<c>label</c>); when present those flow as UTF-8 leases, the same
/// bytes-to-bytes pattern the grantee projection benchmark already covers.
/// </summary>
[MemoryDiagnoser]
public class AdministratorIdentityBenchmarks
{
    private const int AdminCount = 10;

    // One representative identity (a multi-tag person) for the digest element probes.
    private static readonly SecurityTagSet Identity =
        SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", "alice@contoso.example")]);

    private JsonWorkspace inputWorkspace = null!;
    private ParsedJsonDocument<WorkflowAdministrators> record = null!;

    [GlobalSetup]
    public void Setup()
    {
        // The administration record the handler projects, as the catalog hands it back: AdminCount resolved identities.
        // Built in an unrented workspace (it outlives setup) and serialised into an owned, pooled document.
        this.inputWorkspace = JsonWorkspace.CreateUnrented();
        var admins = new List<WorkflowAdministrators.AdministratorIdentity>(AdminCount);
        for (int i = 0; i < AdminCount; i++)
        {
            SecurityTagSet tags = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "contoso"), new SecurityTag("sys:sub", $"user{i:D2}@contoso.example")]);
            admins.Add(WorkflowAdministrators.BuildIdentity(this.inputWorkspace, tags, default, hasKind: false, default, hasLabel: false));
        }

        this.record = WorkflowAdministratorsSerialization.SerializeNewDoc("nightly-reconcile", admins, "admin", DateTimeOffset.UnixEpoch, WorkflowEtag.None);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        this.record.Dispose();
        this.inputWorkspace.Dispose();
    }

    // ── digest element probe ────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>The digest as a managed hex string (the removal key formatted via <c>Convert.ToHexStringLower</c>).</summary>
    /// <returns>The digest length (prevents dead-code elimination).</returns>
    [Benchmark(Baseline = true)]
    public int Digest_Compute_String() => SecurityIdentityDigest.Compute(Identity)!.Length;

    /// <summary>The shipped digest: the hex written straight into a stack span — no managed string.</summary>
    /// <returns>The bytes written (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Digest_FormatUtf8_Span()
    {
        Span<byte> digest = stackalloc byte[SecurityIdentityDigest.DigestUtf8Length];
        return SecurityIdentityDigest.FormatUtf8(Identity, digest);
    }

    // ── administrators-list projection (e2e response body) ──────────────────────────────────────────────────────────

    /// <summary>The naive projection: a digest string + a managed string per identity grant, built through capturing
    /// closures; one materialisation into the response workspace.</summary>
    /// <returns>The administrator count (prevents dead-code elimination).</returns>
    [Benchmark]
    public int Project_List_StringDigest()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        Models.AdministratorList.Source body = Models.AdministratorList.Build((ref Models.AdministratorList.Builder b) => b.Create(
            administrators: Models.AdministratorList.AdministratorGrantArray.Build((ref Models.AdministratorList.AdministratorGrantArray.Builder ab) =>
            {
                foreach (WorkflowAdministrators.AdministratorIdentity admin in this.record.RootElement.Administrators.EnumerateArray())
                {
                    ab.AddItem(StringGrant(admin));
                }
            })));
        return Models.AdministratorList.CreateBuilder(ws, body, 30).RootElement.Administrators.GetArrayLength();
    }

    /// <summary>The shipped projection: the digest formatted into a pooled buffer and each grant written from the
    /// identity's unescaped UTF-8 (bytes-to-bytes), through the closure-free <c>Build&lt;TContext&gt;</c> apparatus;
    /// one materialisation into the response workspace.</summary>
    /// <returns>The administrator count.</returns>
    [Benchmark]
    public int Project_List_SpanDigest()
    {
        using JsonWorkspace ws = JsonWorkspace.Create();
        var state = new ListState(this.record.RootElement.Administrators);
        Models.AdministratorList.AdministratorGrantArray.Source<ListState> administrators =
            Models.AdministratorList.AdministratorGrantArray.Build(in state, BuildGrants);
        return Models.AdministratorList.CreateBuilder(ws, in state, administrators).RootElement.Administrators.GetArrayLength();
    }

    // ── string projection (naive) ──────────────────────────────────────────────────────────────────────────────────

    private static Models.AdministratorGrant.Source StringGrant(WorkflowAdministrators.AdministratorIdentity admin)
    {
        SecurityTagSet tags = TagsOf(admin);
        string digest = SecurityIdentityDigest.Compute(tags)!;
        var grants = new List<(string Dimension, string Value)>();
        foreach (SecurityTag tag in tags)
        {
            grants.Add((tag.Key, tag.Value));
        }

        return Models.AdministratorGrant.Build((ref Models.AdministratorGrant.Builder b) => b.Create(
            digest: digest,
            identity: Models.AdministratorGrant.AdministratorIdentityArray.Build((ref Models.AdministratorGrant.AdministratorIdentityArray.Builder ab) =>
            {
                foreach ((string dimension, string value) in grants)
                {
                    ab.AddItem(Models.AdministratorIdentity.Build((ref Models.AdministratorIdentity.Builder ib) => ib.Create(dimension, value)));
                }
            })));
    }

    // ── span projection (shipped) ──────────────────────────────────────────────────────────────────────────────────

    private static void BuildGrants(in ListState state, ref Models.AdministratorList.AdministratorGrantArray.Builder array)
    {
        foreach (WorkflowAdministrators.AdministratorIdentity admin in state.Administrators.EnumerateArray())
        {
            var item = new GrantState(TagsOf(admin));
            array.AddItem(Models.AdministratorGrant.Build(in item, BuildGrant));
        }
    }

    private static void BuildGrant(in GrantState state, ref Models.AdministratorGrant.Builder grant)
    {
        byte[] digestBuffer = ArrayPool<byte>.Shared.Rent(SecurityIdentityDigest.DigestUtf8Length);
        try
        {
            int written = SecurityIdentityDigest.FormatUtf8(state.Tags, digestBuffer);
            grant.Create(
                in state,
                digest: (Models.JsonString.Source)digestBuffer.AsSpan(0, written),
                identity: Models.AdministratorGrant.AdministratorIdentityArray.Build(in state, BuildIdentity));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(digestBuffer);
        }
    }

    private static void BuildIdentity(in GrantState state, ref Models.AdministratorGrant.AdministratorIdentityArray.Builder identities)
    {
        SecurityTagSet.Utf8Enumerator e = state.Tags.EnumerateUtf8();
        try
        {
            while (e.MoveNext())
            {
                var spans = new TagSpans(e.CurrentKey, e.CurrentValue);
                identities.AddItem(Models.AdministratorIdentity.Build(in spans, BuildIdentityGrant));
            }
        }
        finally
        {
            e.Dispose();
        }
    }

    private static void BuildIdentityGrant(in TagSpans spans, ref Models.AdministratorIdentity.Builder b)
        => b.Create((Models.JsonString.Source)spans.Dimension, (Models.JsonString.Source)spans.Value);

    // A non-owning view of a stored administrator identity's tags (valid while the backing record is alive).
    private static SecurityTagSet TagsOf(in WorkflowAdministrators.AdministratorIdentity admin)
        => SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(admin.Tags).Memory);

    private readonly ref struct ListState(WorkflowAdministrators.AdministratorIdentityArray administrators)
    {
        public WorkflowAdministrators.AdministratorIdentityArray Administrators { get; } = administrators;
    }

    private readonly ref struct GrantState(SecurityTagSet tags)
    {
        public SecurityTagSet Tags { get; } = tags;
    }

    private readonly ref struct TagSpans(ReadOnlySpan<byte> dimension, ReadOnlySpan<byte> value)
    {
        public ReadOnlySpan<byte> Dimension { get; } = dimension;

        public ReadOnlySpan<byte> Value { get; } = value;
    }
}
