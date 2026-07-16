// <copyright file="MembershipExpansionBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Locks the allocation + time floor of <see cref="DirectoryPrincipalProjector.EnrichWithMemberships"/> (design §16.5.4) —
/// the CPU cost of expanding a directory-resolved person to its full membership identity, isolated from the async
/// membership fetch (which the adapter caches). This is pure, string-free work: each membership resolves to a
/// <see cref="SecurityTagSet"/> whose UTF-8 tags are written straight into a pooled buffer and deduplicated on the spans,
/// so the only owned allocation is the one expanded-identity byte array.
/// </summary>
/// <remarks>
/// The span path (<see cref="Enrich_Span"/>) is the hot one — every UTF-8/JSON-sourced adapter (Keycloak, Graph, Google,
/// Okta, SCIM) resolves through a span mapper, and it projects each synthetic membership without a throwaway
/// <see cref="ResolvedPrincipal"/> (no per-membership value byte array). The string path (<see cref="Enrich_String"/>,
/// LDAP-style) pays one <see cref="ResolvedPrincipal"/> per membership — the mapper contract returns one — so it is the
/// A/B contrast, not a regression. Both are dominated by the single owned byte array of the expanded set; the projections
/// table and the per-membership name scratch are pooled. The benchmark's value is the regression guard: a change that
/// reintroduces a managed <c>SecurityTag</c> list, a per-tag string realization, or a per-membership DOM moves the number.
/// </remarks>
public class MembershipExpansionBenchmarks
{
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> Empty = new Dictionary<string, IReadOnlyList<string>>();
    private static readonly string[] NoRoles = [];

    // The bytes-to-bytes mapper the HTTP/JSON adapters use: Person→sys:sub, Team→sys:group, Role→sys:role. Built once — it
    // is configuration, not per-call work.
    private static readonly DirectoryPrincipalProjector SpanProjector = new(
        DirectorySpanIdentityMapper.FromIdentity([], static (DirectoryRecordView record, ref IdentityBuilder identity) =>
        {
            switch (record.Kind)
            {
                case GranteeKind.Team:
                    identity.Add("sys:group"u8, record.ValueUtf8);
                    return true;
                case GranteeKind.Role:
                    identity.Add("sys:role"u8, record.ValueUtf8);
                    return true;
                default:
                    identity.Add("sys:sub"u8, record.ValueUtf8);
                    return true;
            }
        }),
        "bench-issuer");

    // The string mapper (LDAP shape), same dimensions — one ResolvedPrincipal per membership by the mapper contract.
    private static readonly DirectoryPrincipalProjector StringProjector = new(
        DirectoryIdentityMapper.FromFunc(static record => record.Kind switch
        {
            GranteeKind.Team => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:group", record.Id)])),
            GranteeKind.Role => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:role", record.Id)])),
            _ => new ResolvedPrincipal(record.Kind, record.Id, record.DisplayName, SecurityTagSet.FromTags([new SecurityTag("sys:sub", record.Id)])),
        }),
        "bench-issuer");

    private ResolvedPrincipal spanPerson;
    private ResolvedPrincipal stringPerson;
    private string[] groups = [];

    /// <summary>Gets or sets the number of group memberships to expand (a person in 1, 3, or 8 groups — the realistic spread).</summary>
    [Params(1, 3, 8)]
    public int GroupCount { get; set; }

    /// <summary>Resolves the person once through each projector and builds the membership-name inputs, so the benchmark measures only the expansion.</summary>
    [GlobalSetup]
    public void Setup()
    {
        this.spanPerson = SpanProjector.TryProjectIdentity(GranteeKind.Person, "alice"u8, "alice"u8, hasLabel: true, new DirectoryRecordView(GranteeKind.Person, "alice"u8, default, default))!.Value;
        this.stringPerson = StringProjector.Project(new DirectoryRecord(GranteeKind.Person, "alice", "Alice", Empty, []))!.Value;

        this.groups = new string[this.GroupCount];
        for (int i = 0; i < this.GroupCount; i++)
        {
            this.groups[i] = $"group-{i}";
        }
    }

    /// <summary>The hot path — expand a person's memberships through the bytes-to-bytes span projector.</summary>
    /// <returns>The expanded identity's byte length (prevents dead-code elimination without a JSON walk).</returns>
    [Benchmark(Baseline = true)]
    public int Enrich_Span() => SpanProjector.EnrichWithMemberships(this.spanPerson, this.groups, NoRoles).Identity.RawJson.Length;

    /// <summary>The string-mapper path (LDAP shape) — one ResolvedPrincipal per membership by the mapper contract.</summary>
    /// <returns>The expanded identity's byte length.</returns>
    [Benchmark]
    public int Enrich_String() => StringProjector.EnrichWithMemberships(this.stringPerson, this.groups, NoRoles).Identity.RawJson.Length;
}
