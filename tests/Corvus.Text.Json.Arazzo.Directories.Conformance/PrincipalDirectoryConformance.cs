// <copyright file="PrincipalDirectoryConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Directories;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Conformance;

/// <summary>
/// The shared contract every <see cref="IPrincipalDirectory"/> adapter (LDAP, Keycloak, Entra, Okta, Google, SCIM) must
/// satisfy (design §16.5.4): a value-prefix search, optionally restricted to a grantee kind, resolves each matching
/// principal to its exact <c>sys:</c> identity (via the adapter's configured <see cref="IDirectoryIdentityMapper"/>),
/// respects the result limit, and is kind-scoped. A backend's test project provisions its directory with the
/// <see cref="DirectoryFixture"/> principals and implements <see cref="CreateAsync"/> to return the adapter over it.
/// </summary>
public abstract class PrincipalDirectoryConformance
{
    /// <summary>Provisions the backing directory with the <see cref="DirectoryFixture"/> principals and returns an adapter over it.</summary>
    /// <returns>The directory adapter (configured with a mapper that yields the fixture's expected identities).</returns>
    protected abstract ValueTask<IPrincipalDirectory> CreateAsync();

    [TestMethod]
    public async Task A_value_prefix_search_returns_every_matching_person()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        IReadOnlyList<ResolvedPrincipal> found = await directory.SearchAsync(GranteeKind.Person, "al", 10, default);

        // alice + albert share the "al" value prefix; bob does not.
        found.Select(p => p.Value).ShouldBe([DirectoryFixture.Alice.Value, DirectoryFixture.Albert.Value], ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_search_resolves_each_principal_to_its_exact_sys_identity()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        ResolvedPrincipal bob = (await directory.SearchAsync(GranteeKind.Person, "bob", 10, default)).Single();

        bob.Kind.ShouldBe(DirectoryFixture.Bob.Kind);
        bob.Value.ShouldBe(DirectoryFixture.Bob.Value);
        bob.Label.ShouldBe(DirectoryFixture.Bob.Label);
        bob.Identity.ToList().ShouldBe(DirectoryFixture.Bob.Identity.ToList(), ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_search_with_no_match_is_empty()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        (await directory.SearchAsync(GranteeKind.Person, "zzz-nobody", 10, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_search_resolves_teams()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        ResolvedPrincipal payments = (await directory.SearchAsync(GranteeKind.Team, "pay", 10, default)).Single();

        payments.Kind.ShouldBe(GranteeKind.Team);
        payments.Value.ShouldBe(DirectoryFixture.Payments.Value);
        payments.Identity.ToList().ShouldBe(DirectoryFixture.Payments.Identity.ToList(), ignoreOrder: true);
    }

    [TestMethod]
    public async Task An_empty_prefix_returns_all_of_the_kind()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        IReadOnlyList<ResolvedPrincipal> roles = await directory.SearchAsync(GranteeKind.Role, string.Empty, 10, default);

        roles.Select(p => p.Value).ShouldBe([DirectoryFixture.WorkflowAdmin.Value, DirectoryFixture.Viewer.Value], ignoreOrder: true);
    }

    [TestMethod]
    public async Task A_search_respects_the_limit()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        // Two people share the "al" prefix; a limit of 1 returns at most one.
        (await directory.SearchAsync(GranteeKind.Person, "al", 1, default)).Count.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_search_is_scoped_to_the_requested_kind()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        // "alice" is a person, never a team — a team search must not return her.
        (await directory.SearchAsync(GranteeKind.Team, "alice", 10, default)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task A_search_stamps_the_configured_issuer_on_every_resolved_identity()
    {
        IPrincipalDirectory directory = await this.CreateAsync();

        // Across every kind, each resolved identity must carry exactly one issuer tag equal to the configured issuer, so
        // this provider's identities are structurally disjoint from any other's (the adapter stamps it, mapper-immutable).
        foreach (GranteeKind kind in (GranteeKind[])[GranteeKind.Person, GranteeKind.Team, GranteeKind.Role])
        {
            IReadOnlyList<ResolvedPrincipal> found = await directory.SearchAsync(kind, string.Empty, 100, default);
            found.ShouldNotBeEmpty();
            foreach (ResolvedPrincipal principal in found)
            {
                List<SecurityTag> identity = principal.Identity.ToList();
                identity.Count(t => t.Key == DirectoryIssuer.IssuerTagKey).ShouldBe(1, $"{principal.Value} must carry exactly one issuer tag");
                identity.ShouldContain(new SecurityTag(DirectoryIssuer.IssuerTagKey, DirectoryFixture.Issuer));
            }
        }
    }
}