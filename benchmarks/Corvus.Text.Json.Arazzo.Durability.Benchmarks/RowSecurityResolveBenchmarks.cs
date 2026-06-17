// <copyright file="RowSecurityResolveBenchmarks.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using BenchmarkDotNet.Attributes;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Benchmarks;

/// <summary>
/// Measures the per-request row-security warm path (§14.2 / §16.5): the existing reach <see
/// cref="PersistentRowSecurityPolicy.Resolve"/> baseline versus the new <see
/// cref="PersistentRowSecurityPolicy.ResolveGrantedScopes"/> capability resolution. The headline result the
/// access-request work must hold is that the <em>common</em> scope path — a principal with no per-principal
/// capability grant, which is every standing/membership-driven request — allocates <b>zero</b> bytes (the
/// pre-computed <c>AnyScopeGrants</c> flag early-outs to the shared empty array). Only an actually-elevated
/// principal (one holding a stored grant) pays a small allocation.
/// </summary>
[MemoryDiagnoser]
public class RowSecurityResolveBenchmarks
{
    private PersistentRowSecurityPolicy noGrantPolicy = null!;
    private PersistentRowSecurityPolicy grantedPolicy = null!;
    private ClaimsPrincipal principal = null!;

    [GlobalSetup]
    public async Task Setup()
    {
        this.principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("sub", "alice"), new Claim("team", "payments")],
            "bench"));

        // The common deployment shape: a standing reach-only rule + binding, NO per-principal capability grant.
        var noGrantStore = new InMemorySecurityPolicyStore();
        await noGrantStore.AddRuleAsync("team-payments", new SecurityRuleDefinition("team == 'payments'"), "admin", default);
        await noGrantStore.AddBindingAsync(
            new SecurityBindingDefinition("team", "payments", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None),
            "admin",
            default);
        this.noGrantPolicy = new PersistentRowSecurityPolicy(noGrantStore);
        await this.noGrantPolicy.RefreshAsync();

        // The same, plus the per-principal, time-bound capability grant an access-request approval writes (the
        // elevated state). The far-future expiry makes the grant active while forcing the resolver's time-bound path
        // (AnyExpiringBindings → a single clock read) — proving that path stays allocation-free.
        var grantedStore = new InMemorySecurityPolicyStore();
        await grantedStore.AddRuleAsync("team-payments", new SecurityRuleDefinition("team == 'payments'"), "admin", default);
        await grantedStore.AddBindingAsync(
            new SecurityBindingDefinition("team", "payments", VerbGrant.Rules("team-payments"), VerbGrant.None, VerbGrant.None),
            "admin",
            default);
        await grantedStore.AddBindingAsync(
            new SecurityBindingDefinition("sub", "alice", VerbGrant.Rules("team-payments"), VerbGrant.Rules("team-payments"), VerbGrant.None, Scopes: [ControlPlaneScopes.RunsWrite], ExpiresAt: new DateTimeOffset(2999, 1, 1, 0, 0, 0, TimeSpan.Zero)),
            "approver",
            default);
        this.grantedPolicy = new PersistentRowSecurityPolicy(grantedStore);
        await this.grantedPolicy.RefreshAsync();
    }

    /// <summary>The existing per-request reach resolution — the baseline.</summary>
    /// <returns>The resolved access context.</returns>
    [Benchmark(Baseline = true)]
    public AccessContext Resolve_reach() => this.noGrantPolicy.Resolve(this.principal);

    /// <summary>The new capability resolution on the common (no per-principal grant) path — must be zero-allocation.</summary>
    /// <returns>The granted scopes (empty).</returns>
    [Benchmark]
    public IReadOnlyList<string> ResolveGrantedScopes_commonPath() => this.noGrantPolicy.ResolveGrantedScopes(this.principal);

    /// <summary>The capability resolution on the elevated (post-approval) path — allocates a small list.</summary>
    /// <returns>The granted scopes.</returns>
    [Benchmark]
    public IReadOnlyList<string> ResolveGrantedScopes_elevatedPath() => this.grantedPolicy.ResolveGrantedScopes(this.principal);
}