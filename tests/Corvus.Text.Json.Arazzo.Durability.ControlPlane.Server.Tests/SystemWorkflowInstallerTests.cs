// <copyright file="SystemWorkflowInstallerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server.Tests;

/// <summary>
/// Coverage of <see cref="SystemWorkflowInstaller"/> (design §16.5.1): a deployment install catalogues the
/// access-approval workflow, makes it available in the internal environment, and provisions the system runner's
/// credential usage-scoped to the workflow identity — idempotently, so a re-install is a no-op.
/// </summary>
[TestClass]
public sealed class SystemWorkflowInstallerTests
{
    private static readonly SecurityTagSet Admins = SecurityTagSet.FromTags(
    [
        new SecurityTag("sys:group", "arazzo-admins"),
        new SecurityTag("sys:iss", "arazzo-keycloak"),
    ]);

    private static SystemWorkflowInstallOptions Options() => new()
    {
        AdministratorIdentity = Admins,
        Owner = new CatalogOwner("Control Plane", "controlplane@example.com", null, null),
        CredentialTokenUrl = "https://keycloak.example/realms/arazzo/protocol/openid-connect/token",
        CredentialClientId = "arazzo-access-approval",
        CredentialClientSecretRef = "vault://secret/arazzo/controlplane#client-secret",
        WorkflowTags = ["system", "approval"],
    };

    [TestMethod]
    public async Task Install_catalogues_makes_available_and_provisions_the_credential()
    {
        Fixture f = Fixture.Create();
        await f.Installer.InstallAsync(Options(), default);

        // Catalogued as access-approval v1.
        using ParsedJsonDocument<CatalogVersion>? version = await f.Catalog.GetAsync("access-approval", 1, AccessContext.System, default);
        version.ShouldNotBeNull();

        // Available in the system environment.
        using ParsedJsonDocument<AvailabilityEntry>? entry = await f.Availability.GetAsync("access-approval", 1, "system", default);
        entry.ShouldNotBeNull();

        // The runner credential exists for the controlplane source in the system environment.
        using ParsedJsonDocument<SourceCredentialBinding>? credential = await f.Credentials.GetAsync("controlplane", "system", AccessContext.System, default);
        credential.ShouldNotBeNull();

        // The internal environment was created.
        using ParsedJsonDocument<CpEnvironment>? environment = await f.Environments.GetAsync("system", AccessContext.System, default);
        environment.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task Re_installing_is_idempotent()
    {
        Fixture f = Fixture.Create();
        await f.Installer.InstallAsync(Options(), default);
        await f.Installer.InstallAsync(Options(), default); // must not throw and must not duplicate.

        using ParsedJsonDocument<CatalogVersion>? v1 = await f.Catalog.GetAsync("access-approval", 1, AccessContext.System, default);
        v1.ShouldNotBeNull();

        // A second install did not publish a spurious v2.
        using ParsedJsonDocument<CatalogVersion>? v2 = await f.Catalog.GetAsync("access-approval", 2, AccessContext.System, default);
        v2.ShouldBeNull();
    }

    [TestMethod]
    public async Task An_unbakeable_system_workflow_fails_the_install_loudly()
    {
        // The deferred half of "surface system-workflow bake failures": a null build is the provider's
        // degraded mode for USER workflows, but a non-runnable SYSTEM workflow crash-loops its runner
        // with no visible cause — the deployment must refuse to come up instead.
        var provider = new StubExecutorProvider(bakes: false);
        Fixture fixture = Fixture.Create(provider);

        InvalidOperationException refused = await Should.ThrowAsync<InvalidOperationException>(
            async () => await fixture.Installer.InstallAsync(Options(), default));
        refused.Message.ShouldContain("failed to bake");
        refused.Message.ShouldContain("access-approval");
        provider.Builds.ShouldBe(1);

        // Nothing was catalogued: the refusal fired before any install step.
        using ParsedJsonDocument<CatalogVersion>? version = await fixture.Catalog.GetAsync("access-approval", 1, AccessContext.System, default);
        version.ShouldBeNull();
    }

    [TestMethod]
    public async Task A_bakeable_system_workflow_installs_with_the_probe_satisfied()
    {
        var provider = new StubExecutorProvider(bakes: true);
        Fixture fixture = Fixture.Create(provider);

        await fixture.Installer.InstallAsync(Options(), default);

        provider.Builds.ShouldBe(1);
        provider.LastWorkflow.Length.ShouldBeGreaterThan(0, "the probe bakes the real embedded workflow");
        provider.LastSourceCount.ShouldBe(2, "notifications + controlplane ride along");
        using ParsedJsonDocument<CatalogVersion>? version = await fixture.Catalog.GetAsync("access-approval", 1, AccessContext.System, default);
        version.ShouldNotBeNull();
    }

    [TestMethod]
    public async Task The_runner_credential_admits_only_the_approval_workflow_identity()
    {
        Fixture f = Fixture.Create();
        await f.Installer.InstallAsync(Options(), default);

        // A run carrying the approval workflow's own identity (sys:workflow=access-approval) may use the credential — the
        // same entitlement that admits the version through the catalog-time gate (§13).
        (await f.Credentials.EvaluateSourceAccessAsync(
            "controlplane",
            SecurityTagSet.FromTags([new SecurityTag(WorkflowIdentity.WorkflowTagKey, "access-approval")]),
            default)).ShouldNotBe(CredentialSourceAccess.Denied);

        // Any other workflow's run is refused: the runner credential is not a shared secret.
        (await f.Credentials.EvaluateSourceAccessAsync(
            "controlplane",
            SecurityTagSet.FromTags([new SecurityTag(WorkflowIdentity.WorkflowTagKey, "some-other-workflow")]),
            default)).ShouldBe(CredentialSourceAccess.Denied);
    }

    private sealed class Fixture
    {
        public required ISecuredWorkflowCatalog Catalog { get; init; }

        public required IAvailabilityStore Availability { get; init; }

        public required ISourceCredentialStore Credentials { get; init; }

        public required IEnvironmentStore Environments { get; init; }

        public required SystemWorkflowInstaller Installer { get; init; }

        public static Fixture Create(Corvus.Text.Json.Arazzo.IWorkflowExecutorProvider? executorProvider = null)
        {
            var credentials = new InMemorySourceCredentialStore();
            var catalog = new SecuredWorkflowCatalog(new InMemoryWorkflowCatalogStore(), new InMemoryWorkflowStateStore(), "system", credentials);
            var availability = new InMemoryAvailabilityStore();
            var environments = new InMemoryEnvironmentStore();
            var administrators = new InMemoryEnvironmentAdministratorStore();
            return new Fixture
            {
                Catalog = catalog,
                Availability = availability,
                Credentials = credentials,
                Environments = environments,
                Installer = new SystemWorkflowInstaller(catalog, availability, credentials, environments, administrators, executorProvider),
            };
        }
    }

    /// <summary>A bake probe stub: scripted success or failure, recording what it was asked to build.</summary>
    private sealed class StubExecutorProvider(bool bakes) : Corvus.Text.Json.Arazzo.IWorkflowExecutorProvider
    {
        public int Builds { get; private set; }

        public ReadOnlyMemory<byte> LastWorkflow { get; private set; }

        public int LastSourceCount { get; private set; }

        public Corvus.Text.Json.Arazzo.WorkflowExecutorArtifact? BuildExecutor(
            ReadOnlyMemory<byte> workflowUtf8,
            IReadOnlyList<KeyValuePair<string, byte[]>> sources,
            string packageHash)
        {
            this.Builds++;
            this.LastWorkflow = workflowUtf8;
            this.LastSourceCount = sources.Count;
            return bakes ? new Corvus.Text.Json.Arazzo.WorkflowExecutorArtifact(new byte[] { 1 }, new byte[] { 2 }) : null;
        }
    }
}