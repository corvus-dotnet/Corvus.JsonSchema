// <copyright file="IExecutorAdmission.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Execution;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// The runner's own decision, per environment, on whether an executor the loader has verified may run there (ADR 0065
/// phase C). The platform's signature over the executor manifest proves the platform produced the executor; this seam
/// is where the tenant's authority is checked: the executor's assembly digest against the tenant's allowlist, or the
/// tenant's countersignature over the executor against the executor-signing keys the runner pins. It is consulted with
/// the manifest of the executor actually loaded, never with one fetched separately, so a manifest served twice with
/// different contents cannot pass.
/// </summary>
public interface IExecutorAdmission
{
    /// <summary>Decides whether the loaded executor may run in the environment.</summary>
    /// <param name="environment">The environment the run belongs to.</param>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="manifest">The manifest of the executor the loader verified and loaded.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The admission, <see cref="ExecutorAdmission.Admitted"/> or why not.</returns>
    ValueTask<ExecutorAdmission> AdmitAsync(string environment, string baseWorkflowId, int versionNumber, WorkflowExecutorManifest manifest, CancellationToken cancellationToken);
}