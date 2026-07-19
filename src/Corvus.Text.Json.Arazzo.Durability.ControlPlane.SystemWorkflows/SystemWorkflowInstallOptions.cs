// <copyright file="SystemWorkflowInstallOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.SystemWorkflows;

/// <summary>
/// The deployment-supplied parameters <see cref="SystemWorkflowInstaller"/> installs the bootstrapped access-approval
/// system workflow with (design §16.5.1): the founding administrator identity, the catalog owner, the runner's OAuth2
/// client-credentials identity, and the internal environment to make the workflow available in.
/// </summary>
/// <remarks>
/// A deployment builds these from its own configuration: the administrator group's resolved internal identity, the
/// Keycloak token endpoint and the runner client (id + secret reference), and the Vault locator the client secret is
/// stored at. The runner credential's USAGE scoping is not a parameter — the installer fixes it to the approval
/// workflow's own identity (<c>sys:workflow=access-approval</c>) so only runs of this workflow may present it.
/// </remarks>
public sealed class SystemWorkflowInstallOptions
{
    /// <summary>Gets the §15 founding administrator identity stamped on the catalogued approval version. Its holders may
    /// administer the workflow and approve access requests it governs; it establishes administration on the first install.</summary>
    public required SecurityTagSet AdministratorIdentity { get; init; }

    /// <summary>Gets the catalog owner metadata recorded against the approval version.</summary>
    public required CatalogOwner Owner { get; init; }

    /// <summary>Gets the Keycloak token endpoint the runner's OAuth2 client-credentials grant is fetched from (an https URL).</summary>
    public required string CredentialTokenUrl { get; init; }

    /// <summary>Gets the OAuth2 client id the system runner presents — the Keycloak client whose issued token carries the
    /// <c>accessRequests:grant</c> scope the grant call is gated on.</summary>
    public required string CredentialClientId { get; init; }

    /// <summary>Gets the secret reference (<c>scheme://locator[#field]</c>) resolving the runner client's secret; a
    /// pointer into the external secret store, never the secret itself.</summary>
    public required string CredentialClientSecretRef { get; init; }

    /// <summary>Gets the control-plane internal environment the approval workflow executes and is made available in.
    /// Created if absent. Defaults to <c>system</c>.</summary>
    public string Environment { get; init; } = "system";

    /// <summary>Gets the display name for the internal environment when the installer creates it.</summary>
    public string? EnvironmentDisplayName { get; init; } = "System";

    /// <summary>Gets the description for the internal environment when the installer creates it.</summary>
    public string? EnvironmentDescription { get; init; }
        = "Control-plane internal environment for the bootstrapped system workflows (design §16.5.1).";

    /// <summary>Gets the reach tags scoping who may SEE the internal environment and MANAGE the runner credential. When
    /// empty (the default) the installer falls back to <see cref="AdministratorIdentity"/>, so the system surfaces are
    /// administrator-scoped rather than unscoped.</summary>
    public SecurityTagSet ManagementTags { get; init; }

    /// <summary>Gets the classification tags recorded on the catalogued approval version (the catalog <see cref="TagSet"/>).</summary>
    public IReadOnlyList<string>? WorkflowTags { get; init; }

    /// <summary>Gets the audit actor recorded against the installed records. Defaults to <c>system</c>.</summary>
    public string Actor { get; init; } = "system";
}