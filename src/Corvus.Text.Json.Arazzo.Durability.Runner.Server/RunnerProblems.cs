// <copyright file="RunnerProblems.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Runner.Server.Models;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Server;

/// <summary>
/// The problem documents the runner API answers with. They are built here rather than at each call site so the type
/// URIs are declared once and match the contract's.
/// </summary>
internal static class RunnerProblems
{
    /// <summary>The problem type for a lease that is no longer current.</summary>
    internal const string LeaseLostType = "https://corvus-oss.org/arazzo/runner/problems/lease-lost";

    /// <summary>The problem type for a save whose sequence was not the persisted sequence plus one.</summary>
    internal const string CheckpointSupersededType = "https://corvus-oss.org/arazzo/runner/problems/checkpoint-superseded";

    /// <summary>
    /// The refusal for a lease that is not current. A run outside the principal's bindings, one held by another runner,
    /// and one that does not exist all produce exactly this, which is what keeps them indistinguishable.
    /// </summary>
    /// <returns>The problem body.</returns>
    internal static ProblemDetails.Source LeaseLost()
        => ProblemDetails.Build(
            detail: "The lease presented for this run is not current. It may have expired, been released, or been revoked, and the run may already be held by another runner.",
            status: 409,
            title: "Lease is no longer held",
            type: LeaseLostType);

    /// <summary>The refusal for a request whose machine principal cannot be established.</summary>
    /// <returns>The problem body.</returns>
    internal static ProblemDetails.Source NoPrincipal()
        => ProblemDetails.Build(
            detail: "The request carries no machine principal, so no lease could be owned by it.",
            status: 403,
            title: "Forbidden",
            type: "about:blank");

    /// <summary>The refusal for a version document the principal may not have, or that does not exist.</summary>
    /// <returns>The problem body.</returns>
    /// <remarks>
    /// One answer covers both, deliberately. Distinguishing them would confirm which versions exist outside the
    /// environments the runner serves.
    /// </remarks>
    internal static ProblemDetails.Source NoSuchDocument()
        => ProblemDetails.Build(
            detail: "No such document for a version this runner may execute.",
            status: 404,
            title: "Not Found",
            type: "about:blank");

    /// <summary>The refusal for a run the principal holds whose row is nonetheless absent.</summary>
    /// <returns>The problem body.</returns>
    internal static ProblemDetails.Source NoCheckpoint()
        => ProblemDetails.Build(
            detail: "The run has no stored checkpoint. A runner holding a non-terminal anchor entry for it should treat this as a fault rather than a fresh run.",
            status: 404,
            title: "Not Found",
            type: "about:blank");

    /// <summary>The refusal for a body that is not a checkpoint document.</summary>
    /// <returns>The problem body.</returns>
    internal static ProblemDetails.Source MalformedCheckpoint()
        => ProblemDetails.Build(
            detail: "The request body is not a valid checkpoint document.",
            status: 400,
            title: "Bad Request",
            type: "about:blank");

    /// <summary>The refusal for a checkpoint over the deployment's size cap.</summary>
    /// <returns>The problem body.</returns>
    internal static ProblemDetails.Source CheckpointTooLarge()
        => ProblemDetails.Build(
            detail: "The checkpoint exceeds the deployment's maximum size.",
            status: 413,
            title: "Payload Too Large",
            type: "about:blank");

    /// <summary>
    /// The refusal for a save that lost the compare-and-swap, carrying the sequence the store will accept next so the
    /// runner can tell a duplicate resend from a genuine divergence without a second round trip.
    /// </summary>
    /// <param name="acceptedSequence">The sequence the store will accept next.</param>
    /// <returns>The problem body.</returns>
    internal static CheckpointWriteProblem.Source Superseded(long acceptedSequence)
        => CheckpointWriteProblem.Build(
            acceptedSequence: acceptedSequence,
            detail: "The proposed sequence was not the persisted sequence plus one. Nothing was written.",
            status: 409,
            title: "Checkpoint superseded",
            type: CheckpointSupersededType);

    /// <summary>The refusal for a save the sole-writer invariant rejected: a peer advanced the run under a lost or stolen lease.</summary>
    /// <param name="acceptedSequence">The sequence the store will accept next.</param>
    /// <returns>The problem body.</returns>
    internal static CheckpointWriteProblem.Source WriterConflict(long acceptedSequence)
        => CheckpointWriteProblem.Build(
            acceptedSequence: acceptedSequence,
            detail: "The run was advanced by another writer, so this save was not applied. Reload the run before authoring another checkpoint.",
            status: 409,
            title: "Checkpoint superseded",
            type: CheckpointSupersededType);

    /// <summary>
    /// The lease-lost refusal in the shape the save operation answers with. The save's <c>409</c> carries either
    /// refusal, told apart by <c>type</c>; no accepted sequence is carried here, because a lease that is no longer
    /// current says nothing about which sequence the store would take.
    /// </summary>
    /// <returns>The problem body.</returns>
    internal static CheckpointWriteProblem.Source LeaseLostOnWrite()
        => CheckpointWriteProblem.Build(
            detail: "The lease presented for this run is not current. It may have expired, been released, or been revoked, and the run may already be held by another runner.",
            status: 409,
            title: "Lease is no longer held",
            type: LeaseLostType);
}