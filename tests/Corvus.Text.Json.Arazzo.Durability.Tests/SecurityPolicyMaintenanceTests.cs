// <copyright file="SecurityPolicyMaintenanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Coverage of <see cref="SecurityPolicyMaintenance"/> (§16.5.2): the reaper deletes expired time-bound bindings as
/// housekeeping, leaving still-valid and standing (never-expiring) grants untouched.
/// </summary>
[TestClass]
public sealed class SecurityPolicyMaintenanceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task Reaping_removes_only_expired_bindings()
    {
        var store = new InMemorySecurityPolicyStore();

        string expiredId;
        using (ParsedJsonDocument<SecurityBindingDocument> expired = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("sub", "alice", VerbGrant.Full, VerbGrant.None, VerbGrant.None, expiresAt: Now.AddMinutes(-5)),
            "admin",
            default))
        {
            expiredId = expired.RootElement.IdValue;
        }

        string validId;
        using (ParsedJsonDocument<SecurityBindingDocument> valid = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("sub", "bob", VerbGrant.Full, VerbGrant.None, VerbGrant.None, expiresAt: Now.AddHours(1)),
            "admin",
            default))
        {
            validId = valid.RootElement.IdValue;
        }

        string standingId;
        using (ParsedJsonDocument<SecurityBindingDocument> standing = await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full),
            "admin",
            default))
        {
            standingId = standing.RootElement.IdValue;
        }

        int reaped = await SecurityPolicyMaintenance.ReapExpiredBindingsAsync(store, new FixedClock(Now));

        reaped.ShouldBe(1);
        (await store.GetBindingAsync(expiredId, default)).ShouldBeNull(); // expired → reaped

        using (ParsedJsonDocument<SecurityBindingDocument>? stillValid = await store.GetBindingAsync(validId, default))
        {
            stillValid.ShouldNotBeNull(); // future expiry → kept
        }

        using (ParsedJsonDocument<SecurityBindingDocument>? stillStanding = await store.GetBindingAsync(standingId, default))
        {
            stillStanding.ShouldNotBeNull(); // no expiry → kept
        }
    }

    [TestMethod]
    public async Task Reaping_an_unexpired_policy_deletes_nothing()
    {
        var store = new InMemorySecurityPolicyStore();
        using (await AddBindingDraftAsync(store, SecurityBindingDocument.Draft("role", "operator", VerbGrant.Full, VerbGrant.Full, VerbGrant.Full), "admin", default))
        {
        }

        (await SecurityPolicyMaintenance.ReapExpiredBindingsAsync(store, new FixedClock(Now))).ShouldBe(0);
    }

    // Adds the (pooled, disposable) draft binding, disposing the draft once the store has read it; the created document
    // is returned for the caller to assert on and dispose.
    private static async Task<ParsedJsonDocument<SecurityBindingDocument>> AddBindingDraftAsync(InMemorySecurityPolicyStore store, ParsedJsonDocument<SecurityBindingDocument> draft, string actor, CancellationToken cancellationToken = default)
    {
        using (draft)
        {
            return await store.AddBindingAsync(draft.RootElement, actor, cancellationToken);
        }
    }

    // A clock fixed at a known instant so a binding's expiry is deterministic relative to "now".
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}