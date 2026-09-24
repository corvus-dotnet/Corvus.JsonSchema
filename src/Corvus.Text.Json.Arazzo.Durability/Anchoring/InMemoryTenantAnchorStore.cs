// <copyright file="InMemoryTenantAnchorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Threading;

namespace Corvus.Text.Json.Arazzo.Durability.Anchoring;

/// <summary>
/// The in-memory reference implementation of <see cref="ITenantAnchorStore"/>: the records and attestations under
/// one lock, so a write is the compare, the classification and the replace as one step, which is the whole of what
/// a durable backend has to reproduce. It is the reference the shared anchor-store conformance suite runs against,
/// and usable by a single-process tenant host that does not need the anchor to survive a restart, which is to say by
/// tests and by nothing that anchors a real environment.
/// </summary>
public sealed class InMemoryTenantAnchorStore : ITenantAnchorStore
{
    private readonly Dictionary<(string Environment, string RunId), AnchorRecord> records = [];
    private readonly Dictionary<string, ulong> incarnations = new(StringComparer.Ordinal);
    private readonly Lock gate = new();

    /// <inheritdoc/>
    public ValueTask<AnchorRecord?> ReadAsync(string environment, string runId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runId);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return ValueTask.FromResult(this.records.TryGetValue((environment, runId), out AnchorRecord record) ? record : (AnchorRecord?)null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<AnchorWriteKind> WriteAsync(string environment, AnchorRecord? expected, AnchorRecord proposed, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            // The compare half: the stored record is the one the writer decided against, or there is none and the
            // writer expected none. A miss is a second writer, and the sole-writer rule makes that a refusal rather
            // than a retry.
            AnchorRecord? stored = this.records.TryGetValue((environment, proposed.RunId), out AnchorRecord current) ? current : null;
            if (stored != expected || !string.Equals(proposed.EnvironmentId, environment, StringComparison.Ordinal))
            {
                return ValueTask.FromResult(AnchorWriteKind.Rejected);
            }

            // No attestation admits no write: every clause is stated against the attested incarnation.
            if (!this.incarnations.TryGetValue(environment, out ulong attested))
            {
                return ValueTask.FromResult(AnchorWriteKind.Rejected);
            }

            AnchorWriteKind kind = AnchorAcceptance.Classify(stored, proposed, attested);
            if (kind != AnchorWriteKind.Rejected)
            {
                this.records[(environment, proposed.RunId)] = proposed;
            }

            return ValueTask.FromResult(kind);
        }
    }

    /// <inheritdoc/>
    public ValueTask<ulong?> ReadAttestedIncarnationAsync(string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            return ValueTask.FromResult(this.incarnations.TryGetValue(environment, out ulong attested) ? attested : (ulong?)null);
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> AttestIncarnationAsync(string environment, ulong incarnation, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        cancellationToken.ThrowIfCancellationRequested();

        lock (this.gate)
        {
            // Strictly monotonic, and never zero: zero is what an unattested region reads as, so attesting it would
            // make "never attested" and "attested" the same value.
            if (incarnation == 0 || (this.incarnations.TryGetValue(environment, out ulong current) && incarnation <= current))
            {
                return ValueTask.FromResult(false);
            }

            this.incarnations[environment] = incarnation;
            return ValueTask.FromResult(true);
        }
    }
}