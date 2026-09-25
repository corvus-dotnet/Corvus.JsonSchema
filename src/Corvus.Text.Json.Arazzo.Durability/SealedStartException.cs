// <copyright file="SealedStartException.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A sealed start the runner could not open at first claim (ADR 0065 decision 9): the ring holds no seal key for the
/// environment, the row names a generation other than the one held, the initiator's signature verifies under no pinned
/// key, or the inputs do not open under the binding re-derived from the run's own address. The genesis row is carried
/// so the runner can record the refusal on the run itself, envelope-only, and the run is never claimed again.
/// </summary>
public sealed class SealedStartException : Exception
{
    /// <summary>Initializes a new instance of the <see cref="SealedStartException"/> class.</summary>
    /// <param name="address">The run.</param>
    /// <param name="refusal">Why the start did not open.</param>
    /// <param name="row">The genesis row as the store holds it.</param>
    /// <param name="etag">The etag the row was read at.</param>
    /// <param name="message">The message.</param>
    public SealedStartException(in WorkflowRunAddress address, SealedStartRefusal refusal, ReadOnlyMemory<byte> row, WorkflowEtag etag, string message)
        : base(message)
    {
        this.Address = address;
        this.Refusal = refusal;
        this.Row = row;
        this.Etag = etag;
    }

    /// <summary>Gets the run.</summary>
    public WorkflowRunAddress Address { get; }

    /// <summary>Gets why the start did not open.</summary>
    public SealedStartRefusal Refusal { get; }

    /// <summary>Gets the genesis row as the store holds it: its envelope is readable, its inputs are not.</summary>
    public ReadOnlyMemory<byte> Row { get; }

    /// <summary>Gets the etag the row was read at, which a save that records the refusal is conditioned on.</summary>
    public WorkflowEtag Etag { get; }
}

/// <summary>Why a sealed start did not open.</summary>
public enum SealedStartRefusal
{
    /// <summary>The runner's ring holds no seal key for the environment, so it cannot open any sealed start there.</summary>
    NoSealKey,

    /// <summary>The row names a seal key generation other than the one the runner holds.</summary>
    UnknownGeneration,

    /// <summary>The initiator's signature verifies under none of the pinned initiator keys.</summary>
    UnpinnedInitiator,

    /// <summary>The inputs do not open under the seal key and the binding re-derived from the run's own address.</summary>
    Unopenable,
}

/// <summary>
/// The fault a runner records on a sealed start it refuses (ADR 0065 decision 9): the control plane's <c>422</c> has
/// nowhere to be answered from once inputs are ciphertext to it, so the run is faulted at its start instead, sealed
/// like any other save, and the fault's error type says which. Neither is resumable: a resume would re-open and
/// re-validate the same inputs and fault the same way.
/// </summary>
public static class SealedStartFault
{
    /// <summary>The step id a start fault is recorded against: the run's start, before any step.</summary>
    public const string StepId = "$start";

    /// <summary>The inputs opened and do not validate against the version's inputs schema.</summary>
    public const string InputsInvalid = "sealed-start-inputs-invalid";

    /// <summary>The sealed start did not open: no seal key, another generation, an unpinned initiator, or a seal that does not verify.</summary>
    public const string Unopenable = "sealed-start-unopenable";

    /// <summary>Whether an error type is one of the sealed-start faults.</summary>
    /// <param name="errorType">The run's error type, as its index or fault record carries it.</param>
    /// <returns><see langword="true"/> for <see cref="InputsInvalid"/> or <see cref="Unopenable"/>.</returns>
    public static bool IsSealedStartFault(string? errorType)
        => errorType is InputsInvalid or Unopenable;
}