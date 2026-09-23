// <copyright file="RowSecurityPolicyRefreshServiceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Claims;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// P1-14: a grant revoked through one replica takes effect on every other within the policy refresh bound, because each
/// replica's <see cref="RowSecurityPolicyRefreshService"/> reloads the shared store on that bound.
/// </summary>
[TestClass]
public sealed class RowSecurityPolicyRefreshServiceTests
{
    private static readonly SecurityTagSet AcmeRow = SecurityTagSet.FromTags([new SecurityTag("sys:tenant", "acme")]);

    [TestMethod]
    [Timeout(30000)]
    public async Task A_binding_revoked_through_one_replica_stops_on_another_within_the_bound()
    {
        var store = new InMemorySecurityPolicyStore();
        (string id, WorkflowEtag etag) = await GrantOperatorAsync(store);
        ClaimsPrincipal operatorPrincipal = Principal(("role", "operator"));

        // Two replicas over one store: the writer is the replica the revocation goes through (the security API refreshes
        // that one itself after the write); the other learns of it only through its hosted refresh.
        var writer = new PersistentRowSecurityPolicy(store, internalTagResolver: ClaimsToTags);
        await writer.RefreshAsync();
        var replica = new PersistentRowSecurityPolicy(store, internalTagResolver: ClaimsToTags);
        await replica.RefreshAsync();
        replica.Resolve(operatorPrincipal).Admits(AccessVerb.Read, AcmeRow).ShouldBeTrue();

        var refresh = new RowSecurityPolicyRefreshService(replica, TimeSpan.FromMilliseconds(50), logger: null);
        await refresh.StartAsync(default);
        try
        {
            (await store.DeleteBindingAsync(id, etag, default)).ShouldBeTrue();
            await writer.RefreshAsync();
            writer.Resolve(operatorPrincipal).Admits(AccessVerb.Read, AcmeRow).ShouldBeFalse();

            // The replica denies within the bound; five seconds is the default bound and a hundred times this test's.
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (replica.Resolve(operatorPrincipal).Admits(AccessVerb.Read, AcmeRow))
            {
                (DateTime.UtcNow < deadline).ShouldBeTrue("the replica kept honouring the revoked binding past the bound");
                await Task.Delay(10);
            }
        }
        finally
        {
            await refresh.StopAsync(default);
        }
    }

    [TestMethod]
    public async Task Without_the_refresh_a_replica_keeps_honouring_a_revoked_binding()
    {
        // The reason the mapping requires the service: nothing else carries another replica's revocation across.
        var store = new InMemorySecurityPolicyStore();
        (string id, WorkflowEtag etag) = await GrantOperatorAsync(store);
        ClaimsPrincipal operatorPrincipal = Principal(("role", "operator"));
        var replica = new PersistentRowSecurityPolicy(store, internalTagResolver: ClaimsToTags);
        await replica.RefreshAsync();

        (await store.DeleteBindingAsync(id, etag, default)).ShouldBeTrue();
        await Task.Delay(300);

        replica.Resolve(operatorPrincipal).Admits(AccessVerb.Read, AcmeRow).ShouldBeTrue();
        await replica.RefreshAsync();
        replica.Resolve(operatorPrincipal).Admits(AccessVerb.Read, AcmeRow).ShouldBeFalse();
    }

    [TestMethod]
    public void The_bound_must_be_positive()
    {
        var policy = new PersistentRowSecurityPolicy(new InMemorySecurityPolicyStore());
        Should.Throw<ArgumentOutOfRangeException>(() => new RowSecurityPolicyRefreshService(policy, TimeSpan.Zero, logger: null));
        Should.Throw<ArgumentOutOfRangeException>(() => new RowSecurityPolicyRefreshService(policy, TimeSpan.FromSeconds(-1), logger: null));
        RowSecurityPolicyRefreshService.DefaultInterval.ShouldBe(TimeSpan.FromSeconds(5));
    }

    private static async Task<(string Id, WorkflowEtag Etag)> GrantOperatorAsync(InMemorySecurityPolicyStore store)
    {
        using ParsedJsonDocument<SecurityBindingDocument> draft = SecurityBindingDocument.Draft("role", "operator", VerbGrant.Full, VerbGrant.None, VerbGrant.None);
        using ParsedJsonDocument<SecurityBindingDocument> added = await store.AddBindingAsync(draft.RootElement, "ops", default);
        return (added.RootElement.IdValue, added.RootElement.EtagValue);
    }

    private static ClaimsPrincipal Principal(params (string Type, string Value)[] claims)
        => new(new ClaimsIdentity(claims.Select(c => new Claim(c.Type, c.Value)).ToList(), "test"));

    private static IReadOnlyList<SecurityTag> ClaimsToTags(ClaimsPrincipal? principal)
        => principal?.Claims.Select(c => new SecurityTag(SecurityShell.DefaultInternalPrefix + c.Type, c.Value)).ToList() ?? [];
}