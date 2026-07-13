// <copyright file="EnvironmentAdministrationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using EnvKind = Corvus.Text.Json.Arazzo.Durability.Security.EnvironmentAdministrators.AdministratorIdentity.KindEntity;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// Tests the environment administration mutation gate (design §7.8): an environment's administrator set is established
/// then reassigned / shared, always authorized by current-administrator <em>membership</em> (§16.5.4) — a caller whose
/// canonical identity contains a current administrator's identity may mutate the set — never orphanable. Add-idempotency,
/// dedupe, and digest removal remain exact identity operations.
/// </summary>
[TestClass]
public sealed class EnvironmentAdministrationTests
{
    private static readonly SecurityTagSet Acme = SecurityTagSet.FromTags([new SecurityTag("tenant", "acme")]);
    private static readonly SecurityTagSet Globex = SecurityTagSet.FromTags([new SecurityTag("tenant", "globex")]);

    // A richer caller identity that strictly CONTAINS Acme (a superset): the acme tenant plus a person sub-claim.
    private static readonly SecurityTagSet AcmeAlice = SecurityTagSet.FromTags([new SecurityTag("tenant", "acme"), new SecurityTag("sub", "alice")]);

    [TestMethod]
    public async Task A_caller_whose_identity_contains_an_administrator_may_change_administration()
    {
        SecuredEnvironmentAdministration admin = await NewEstablishedAsync("production", Acme);

        // Membership (§16.5.4): AcmeAlice strictly CONTAINS the founder acme identity, so it administers the environment
        // and may add a co-administrator — though its identity is not set-equal to acme. Under the superseded
        // set-equality mutation gate this threw EnvironmentAdministrationException.
        using ParsedJsonDocument<EnvironmentAdministrators> admins = await AddAdministratorAsync(admin, "production", Globex, caller: AcmeAlice);
        admins.RootElement.AdministratorCount.ShouldBe(2);
    }

    [TestMethod]
    public async Task A_caller_whose_identity_contains_an_administrator_may_transfer()
    {
        SecuredEnvironmentAdministration admin = await NewEstablishedAsync("production", Acme);

        // The same membership gate governs transfer: AcmeAlice hands the environment off to globex entirely.
        using ParsedJsonDocument<EnvironmentAdministrators> admins = await admin.TransferAdministrationAsync("production", [Globex], callerIdentity: AcmeAlice, default);
        admins.RootElement.AdministratorCount.ShouldBe(1);
        admins.RootElement.IsAdministeredBy(Globex).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_disjoint_caller_may_not_change_administration()
    {
        SecuredEnvironmentAdministration admin = await NewEstablishedAsync("production", Acme);

        // globex neither equals nor contains acme, so it is not an administrator and cannot mutate the set.
        await Should.ThrowAsync<EnvironmentAdministrationException>(async () =>
            await AddAdministratorAsync(admin, "production", Globex, caller: Globex));
    }

    [TestMethod]
    public async Task Adding_a_more_specific_identity_than_an_administrator_is_a_genuine_addition()
    {
        SecuredEnvironmentAdministration admin = await NewEstablishedAsync("production", Acme);

        // Add-idempotency stays EXACT set-equality (an identity operation), not membership: AcmeAlice is a different,
        // more specific identity, so adding it is a real second administrator, never an idempotent no-op.
        using ParsedJsonDocument<EnvironmentAdministrators> admins = await AddAdministratorAsync(admin, "production", AcmeAlice, caller: Acme);
        admins.RootElement.AdministratorCount.ShouldBe(2);
    }

    // Establishes a fresh in-memory environment with a single founder administrator.
    private static async Task<SecuredEnvironmentAdministration> NewEstablishedAsync(string environmentName, SecurityTagSet founder)
    {
        var admin = new SecuredEnvironmentAdministration(new InMemoryEnvironmentAdministratorStore());
        await admin.EstablishAsync(environmentName, founder, default(EnvKind), hasKind: false, default, hasLabel: false, default);
        return admin;
    }

    // Adds a resolved identity with no kind/label (the bare identity form) and returns the resulting record.
    private static ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> AddAdministratorAsync(SecuredEnvironmentAdministration admin, string environmentName, SecurityTagSet identity, SecurityTagSet caller)
        => admin.AddAdministratorAsync(environmentName, identity, default(EnvKind), hasKind: false, default, hasLabel: false, caller, default);
}
