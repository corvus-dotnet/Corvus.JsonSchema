// <copyright file="WorkflowAdministratorStoreTimestampTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The audit-timestamp progression for the administrator record (design §15) — driven by the controllable
/// <see cref="TestTimeProvider"/>, which the cross-backend conformance suite cannot use (it runs over
/// <see cref="TimeProvider.System"/>). Verifies materialization stamps <c>createdAt</c> and leaves <c>lastUpdatedAt</c>
/// unset, and that a later replace preserves <c>createdAt</c> while stamping <c>lastUpdatedAt</c> at the new instant.
/// </summary>
[TestClass]
public sealed class WorkflowAdministratorStoreTimestampTests
{
    [TestMethod]
    public async Task A_replace_preserves_created_at_and_stamps_last_updated_at_at_the_new_instant()
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));
        var store = new InMemoryWorkflowAdministratorStore(time);
        using JsonWorkspace workspace = JsonWorkspace.CreateUnrented();
        WorkflowAdministrators.AdministratorIdentity acme = Identity(workspace, "acme");

        WorkflowEtag firstEtag;
        using (ParsedJsonDocument<WorkflowAdministrators> created = await store.PutAsync("nightly-reconcile", [acme], WorkflowEtag.None, "alice", default))
        {
            firstEtag = created.RootElement.EtagValue;
            created.RootElement.CreatedAtValue.ShouldBe(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));
            created.RootElement.LastUpdatedAtValue.ShouldBeNull();
        }

        time.Advance(TimeSpan.FromMinutes(90));
        using ParsedJsonDocument<WorkflowAdministrators> replaced = await store.PutAsync(
            "nightly-reconcile",
            [acme, Identity(workspace, "globex")],
            firstEtag,
            "bob",
            default);

        replaced.RootElement.CreatedAtValue.ShouldBe(new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero));
        replaced.RootElement.LastUpdatedAtValue.ShouldBe(new DateTimeOffset(2026, 6, 15, 10, 30, 0, TimeSpan.Zero));
    }

    // Materializes a single-tenant administrator identity (tags only) in the workspace for the store's PutAsync.
    private static WorkflowAdministrators.AdministratorIdentity Identity(JsonWorkspace workspace, string tenant)
        => WorkflowAdministrators.BuildIdentity(workspace, SecurityTagSet.FromTags([new SecurityTag("sys:tenant", tenant)]), default, hasKind: false, default, hasLabel: false);
}
