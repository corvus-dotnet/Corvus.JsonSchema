// <copyright file="RunnerExecutorAdmission.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Concurrent;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Runner.Client.Models;
using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// The runner's executor policy in force (ADR 0065 phase C): for every run, after the loader has verified the
/// executor against the platform's signature, the executor is admitted in the run's environment only when its
/// assembly digest is on the environment's allowlist or the tenant's countersignature over it, fetched from the
/// runner API, verifies under a key the environment's ring entry pins. The check is made against the manifest of the
/// executor actually loaded, so a manifest the control plane served twice with different contents cannot pass; the
/// control plane's countersignature is a claim the pin checks, never the other way round.
/// </summary>
/// <remarks>
/// A verdict is cached per (environment, version, package hash, digest) for a minute, refusals included, so a sweep does not pay a
/// round trip per claim and a countersignature the operator records shows up within the window. An environment with
/// no policy admits whatever the loader verified; the runner client refuses to start with a keyed entry in that
/// state, so on a runner it is only a clear environment that runs unpoliced.
/// </remarks>
public sealed class RunnerExecutorAdmission : IExecutorAdmission
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

    private readonly RunnerKeyRing ring;
    private readonly IApiEnvironmentsClient environments;
    private readonly TimeProvider timeProvider;
    private readonly ConcurrentDictionary<(string Environment, string BaseWorkflowId, int VersionNumber, string PackageHash, string AssemblyDigest), Verdict> verdicts = new();

    /// <summary>Initializes a new instance of the <see cref="RunnerExecutorAdmission"/> class.</summary>
    /// <param name="ring">The runner's key ring, whose entries carry the executor policy per environment.</param>
    /// <param name="environments">The runner API's environments client, which serves the advertised countersignatures.</param>
    /// <param name="timeProvider">The time source for the verdict cache.</param>
    public RunnerExecutorAdmission(RunnerKeyRing ring, IApiEnvironmentsClient environments, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(ring);
        ArgumentNullException.ThrowIfNull(environments);
        this.ring = ring;
        this.environments = environments;
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc/>
    public async ValueTask<ExecutorAdmission> AdmitAsync(string environment, string baseWorkflowId, int versionNumber, WorkflowExecutorManifest manifest, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);

        if (this.ring.ExecutorPolicyOf(environment) is not { } policy)
        {
            return ExecutorAdmission.Admitted;
        }

        // The cheap partial first: a digest the tenant listed on the runner needs no round trip and no signature.
        if (policy.Lists(manifest.AssemblyDigest))
        {
            return ExecutorAdmission.Admitted;
        }

        if (policy.Signers.Count == 0)
        {
            return ExecutorAdmission.NotListed;
        }

        // The verdict is about one executor of one version in one environment, and the executor is named by both the
        // package hash and the assembly digest, so both are in the key: a verdict for one manifest must never answer
        // for another that shares its digest.
        (string, string, int, string, string) key = (environment, baseWorkflowId, versionNumber, manifest.PackageHash, manifest.AssemblyDigest);
        long now = this.timeProvider.GetTimestamp();
        if (this.verdicts.TryGetValue(key, out Verdict cached) && this.timeProvider.GetElapsedTime(cached.CheckedAt, now) < CheckInterval)
        {
            return cached.Admission;
        }

        ExecutorAdmission admission = await this.CheckCountersignatureAsync(environment, baseWorkflowId, versionNumber, manifest, policy, cancellationToken).ConfigureAwait(false);
        this.verdicts[key] = new Verdict(now, admission);
        return admission;
    }

    // The advertised countersignature, compared to the loaded manifest and verified under the pinned keys: the
    // signed package hash and assembly digest have to be the manifest's own, and the signature has to verify under
    // some pinned key over the framed tuple naming this environment and this version.
    private async ValueTask<ExecutorAdmission> CheckCountersignatureAsync(string environment, string baseWorkflowId, int versionNumber, WorkflowExecutorManifest manifest, RunnerExecutorPolicy policy, CancellationToken cancellationToken)
    {
        try
        {
            await using GetEnvironmentExecutorCountersignatureResponse response = await this.environments.GetEnvironmentExecutorCountersignatureAsync(environment, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == 404)
            {
                return ExecutorAdmission.NotCountersigned;
            }

            if (response.StatusCode != 200)
            {
                return ExecutorAdmission.CountersignatureUnavailable;
            }

            EnvironmentExecutorCountersignature countersignature = response.OkBody;
            if (!countersignature.PackageHash.ValueEquals(manifest.PackageHash) || !countersignature.AssemblyDigest.ValueEquals(manifest.AssemblyDigest))
            {
                // Signed for another executor of the version: a repack or a recompile since the operator reviewed it.
                return ExecutorAdmission.CountersignatureInvalid;
            }

            byte[] signature = ((JsonElement)countersignature.Signature).GetBytesFromBase64();
            foreach (byte[] signer in policy.Signers)
            {
                if (ExecutorCountersignature.Verify(environment, baseWorkflowId, versionNumber, manifest.PackageHash, manifest.AssemblyDigest, signer, signature) == ExecutorCountersignatureResult.Verified)
                {
                    return ExecutorAdmission.Admitted;
                }
            }

            return ExecutorAdmission.CountersignatureInvalid;
        }
        catch (Exception ex) when (ex is HttpRequestException or RunnerApiException or FormatException)
        {
            return ExecutorAdmission.CountersignatureUnavailable;
        }
    }

    private readonly record struct Verdict(long CheckedAt, ExecutorAdmission Admission);
}