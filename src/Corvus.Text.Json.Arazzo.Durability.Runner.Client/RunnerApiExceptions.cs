// <copyright file="RunnerApiExceptions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Net;

namespace Corvus.Text.Json.Arazzo.Durability.Runner.Client;

/// <summary>
/// The runner API refused or failed a request in a way the caller has to see. A refusal the runner is expected to act
/// on differently has its own type below; this is everything else.
/// </summary>
public class RunnerApiException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="RunnerApiException"/> class.</summary>
    /// <param name="status">The response status.</param>
    /// <param name="message">The message.</param>
    public RunnerApiException(HttpStatusCode status, string message)
        : base(message)
    {
        this.Status = status;
    }

    /// <summary>Gets the response status.</summary>
    public HttpStatusCode Status { get; }
}

/// <summary>
/// The presented lease is no longer current: it expired, was released, or was revoked, and the run may already be held
/// by another runner.
/// </summary>
/// <remarks>
/// A runner seeing this stops advancing the run. It does not hold the run any more, so continuing would mean writing
/// over a run another runner is now responsible for — and the writes would be refused anyway, one at a time, which is a
/// slower and less clear way to arrive at the same place.
/// </remarks>
public sealed class RunnerLeaseLostException : RunnerApiException
{
    /// <summary>Initializes a new instance of the <see cref="RunnerLeaseLostException"/> class.</summary>
    /// <param name="runId">The run whose lease is no longer held.</param>
    public RunnerLeaseLostException(WorkflowRunId runId)
        : base(HttpStatusCode.Conflict, $"The lease for run '{runId.Value}' is no longer current.")
    {
        this.RunId = runId;
    }

    /// <summary>Gets the run whose lease is no longer held.</summary>
    public WorkflowRunId RunId { get; }
}

/// <summary>
/// A quota refused the request, and this advance has spent the allowance it is permitted to wait one out.
/// </summary>
/// <remarks>
/// The advance fails here exactly as it would on any other non-2xx, which is the bound on ADR 0065 decision 3's
/// exemption. Holding indefinitely would be a silent stall: a fabricated refusal looks the same as a real one, the
/// background renewer keeps the lease alive so the run never fails over, and a quota hold raises no audit event. This
/// exception is the loud ending that makes the exemption safe to have.
/// </remarks>
public sealed class RunnerQuotaExhaustedException : RunnerApiException
{
    /// <summary>Initializes a new instance of the <see cref="RunnerQuotaExhaustedException"/> class.</summary>
    /// <param name="what">What the runner was trying to do.</param>
    /// <param name="quota">The quota that refused, as the refusal named it.</param>
    /// <param name="counter">The counter it was measured against, as the refusal named it.</param>
    public RunnerQuotaExhaustedException(string what, string quota, string counter)
        : base(HttpStatusCode.TooManyRequests, $"A quota refused the attempt to {what}, and this advance has spent its allowance for waiting one out. Quota '{quota}' on counter '{counter}'.")
    {
        this.Quota = quota;
        this.Counter = counter;
    }

    /// <summary>Gets the quota that refused.</summary>
    public string Quota { get; }

    /// <summary>Gets the counter it was measured against.</summary>
    public string Counter { get; }
}

/// <summary>
/// The proposed checkpoint sequence was not the persisted sequence plus one, so nothing was written.
/// </summary>
/// <remarks>
/// This is the refusal the save operation exists to make possible. Reporting a superseded save as durable would leave a
/// runner committed to a checkpoint the store does not have, so it is raised rather than swallowed, and it carries the
/// sequence the store will accept next: a runner can tell its own duplicate resend (the accepted sequence is one past
/// what it sent) from a genuine divergence without another round trip.
/// </remarks>
public sealed class CheckpointSupersededException : RunnerApiException
{
    /// <summary>Initializes a new instance of the <see cref="CheckpointSupersededException"/> class.</summary>
    /// <param name="runId">The run whose save was refused.</param>
    /// <param name="proposedSequence">The sequence the caller proposed.</param>
    /// <param name="acceptedSequence">The sequence the store will accept next.</param>
    public CheckpointSupersededException(WorkflowRunId runId, long proposedSequence, long acceptedSequence)
        : base(HttpStatusCode.Conflict, $"Checkpoint sequence {proposedSequence} for run '{runId.Value}' was superseded; the store accepts {acceptedSequence} next. Nothing was written.")
    {
        this.RunId = runId;
        this.ProposedSequence = proposedSequence;
        this.AcceptedSequence = acceptedSequence;
    }

    /// <summary>Gets the run whose save was refused.</summary>
    public WorkflowRunId RunId { get; }

    /// <summary>Gets the sequence the caller proposed.</summary>
    public long ProposedSequence { get; }

    /// <summary>Gets the sequence the store will accept next, which is its persisted sequence plus one.</summary>
    public long AcceptedSequence { get; }
}