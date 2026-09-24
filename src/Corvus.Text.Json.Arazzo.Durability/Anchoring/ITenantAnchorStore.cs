// <copyright file="ITenantAnchorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The tenant anchor store (ADR 0065 decision 6 and the normative tenant-anchor specification): a tenant-owned,
/// environment-scoped key-value store holding one <see cref="AnchorRecord"/> per run, replaced whole under
/// compare-and-swap, and one attested store incarnation per environment. It lives where the control plane cannot
/// write, which is the whole of what makes a control-plane rollback or substitution detectable.
/// </summary>
/// <remarks>
/// <para>
/// The store enforces exactly one thing on a record write: the acceptance predicate, by delegating to
/// <see cref="AnchorAcceptance.Classify"/> against the record it holds. It is not a policy engine. It verifies no
/// signature and reads no lease token; the operator signature a <see cref="AnchorWriteKind.ReAnchor"/> or
/// <see cref="AnchorWriteKind.Abandon"/> carries is the runner's to verify before it asks for the write.
/// </para>
/// <para>
/// The incarnation is attested by the tenant, never asserted by the control plane. The first attestation is made
/// when the environment is created, and run starts in a sealed environment are blocked until it exists; a later one
/// records a restore the tenant knows about, which is what turns a backwards-moving store from an attack into an
/// explained event. It is strictly monotonic, and it never reads as zero: no attestation is <see langword="null"/>.
/// </para>
/// </remarks>
public interface ITenantAnchorStore
{
    /// <summary>Reads a run's anchor record.</summary>
    /// <param name="environment">The environment the run is pinned to.</param>
    /// <param name="runId">The run.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The record, or <see langword="null"/> when the run has none.</returns>
    ValueTask<AnchorRecord?> ReadAsync(string environment, string runId, CancellationToken cancellationToken);

    /// <summary>
    /// Replaces a run's anchor record whole, if and only if the stored record is exactly <paramref name="expected"/>
    /// and the acceptance predicate admits <paramref name="proposed"/> over it under the environment's attested
    /// incarnation. Nothing is written otherwise.
    /// </summary>
    /// <param name="environment">The environment the run is pinned to.</param>
    /// <param name="expected">The record the writer read and decided against, or <see langword="null"/> for a run with none.</param>
    /// <param name="proposed">The replacement record.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The clause that admitted the write, or <see cref="AnchorWriteKind.Rejected"/> when the stored record
    /// was not the expected one (a second writer, which the sole-writer rule forbids), no clause admits the write, or
    /// the environment has no attested incarnation.</returns>
    ValueTask<AnchorWriteKind> WriteAsync(string environment, AnchorRecord? expected, AnchorRecord proposed, CancellationToken cancellationToken);

    /// <summary>Reads the environment's tenant-attested store incarnation.</summary>
    /// <param name="environment">The environment.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The attested incarnation, or <see langword="null"/> when the tenant has attested none.</returns>
    ValueTask<ulong?> ReadAttestedIncarnationAsync(string environment, CancellationToken cancellationToken);

    /// <summary>
    /// Records a tenant attestation of the store incarnation. Accepted only when it is strictly above the current
    /// attested value, or when none has been recorded; a first attestation must be at least 1.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <param name="incarnation">The incarnation the tenant attests.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns><see langword="true"/> when the attestation was recorded.</returns>
    ValueTask<bool> AttestIncarnationAsync(string environment, ulong incarnation, CancellationToken cancellationToken);
}