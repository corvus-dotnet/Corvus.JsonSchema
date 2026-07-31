// <copyright file="MicroGuestDeployerOptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;

/// <summary>
/// The sidecar address, guest layout, and per-sandbox posture a <see cref="MicroGuestDeployer"/> deploys with
/// (ADR 0063).
/// </summary>
public sealed record MicroGuestDeployerOptions
{
    /// <summary>Gets the base URL of the micro-guest sidecar's admin surface (typically loopback on the runner's own
    /// machine), e.g. <c>http://127.0.0.1:9411</c>. The deployer uploads the initrd and evolves the sandbox here, and
    /// the sidecar returns the local invoke URL recorded as the deployment's function URL.</summary>
    public required Uri SidecarBaseUrl { get; init; }

    /// <summary>Gets the runner's checkpoint surface base URL, e.g. <c>http://172.20.0.10:8199/checkpoints</c>. Its
    /// host goes on the sandbox's egress allowlist so the guest can checkpoint back (Model B); it must be a routable
    /// address — the guest's host-proxied network denies loopback by design (ADR 0063).</summary>
    public required Uri CheckpointSurfaceUrl { get; init; }

    /// <summary>Gets the environment the guest is baked with: the <c>ARAZZO_SOURCE__&lt;name&gt;</c> source base URLs
    /// the baked transport binder reads, exactly the map the cloud deployers set as function app settings (ADR 0059:
    /// the runner, not the control plane, holds it). The sidecar freezes these into the snapshot's argv, and each
    /// source URL's host joins the egress allowlist. Empty bakes no sources (their steps fault if called).</summary>
    public IReadOnlyDictionary<string, string> GuestEnvironment { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>Gets the absolute path the guest kernel executes (its baked <c>VFSEXEC_PATH</c>); the initrd places
    /// the verified binary there. Defaults to <c>/bin/guest</c>.</summary>
    public string GuestBinaryPath { get; init; } = "/bin/guest";

    /// <summary>Gets the micro-VM memory in MiB. The guest's baked <c>System.GC.HeapHardLimit</c> must fit comfortably
    /// inside it (the assembler's default 32 MiB heap pairs with this default 64 MiB VM). Defaults to 64.</summary>
    public int MemorySizeMib { get; init; } = 64;
}