// <copyright file="TenancyLedgerSerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// Shared, pooled serialization for the tenancy ledger every <see cref="IEnvironmentStore"/> implementation persists
/// (ADR 0065): one row per deployment, so the "carry the admitted owner groups forward under a new etag" step lives here
/// once rather than being re-spelled per backend. The document is built through a pooled scratch buffer
/// (<see cref="PersistedJson.ToArray{TContext}"/>) and returned as the owned UTF-8 bytes the driver persists.
/// </summary>
public static class TenancyLedgerSerialization
{
    /// <summary>Serializes the ledger's next state to owned JSON bytes (pooled scratch, no detached clone) — the
    /// already-admitted groups are carried bytes-to-bytes from <paramref name="current"/>.</summary>
    /// <param name="current">The ledger being replaced, or an undefined value when no row exists yet.</param>
    /// <param name="admitting">The owner group this write introduces, as UTF-8; empty for an interlock-only commit.</param>
    /// <param name="actor">The actor whose governed write this is (audit).</param>
    /// <param name="committedAt">The commit instant.</param>
    /// <param name="etag">The new record etag.</param>
    /// <returns>The owned UTF-8 JSON bytes.</returns>
    public static byte[] SerializeCommitted(in TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, DateTimeOffset committedAt, WorkflowEtag etag)
        => PersistedJson.ToArray(
            (current, admitting, actor, committedAt, etag),
            static (Utf8JsonWriter writer, in (TenancyLedger Current, ReadOnlyMemory<byte> Admitting, string Actor, DateTimeOffset At, WorkflowEtag Tag) c)
                => TenancyLedger.WriteCommitted(writer, c.Current, c.Admitting.Span, c.Actor, c.At, c.Tag));

    /// <summary>Whether a stored ledger still carries the etag <paramref name="expected"/> was read under — the compare
    /// half of the compare-and-swap, for a backend that expresses it over the stored document rather than over an etag
    /// column.</summary>
    /// <param name="document">The stored ledger's current UTF-8 JSON bytes.</param>
    /// <param name="expected">The ledger the caller decided against.</param>
    /// <returns><see langword="true"/> when the stored etag is exactly the expected one.</returns>
    /// <remarks>Both etags are compared over their stored UTF-8 through a pooled, disposed document. Realising either as
    /// a managed <see cref="string"/> would put two heap allocations either side of a boolean whose inputs are both
    /// already bytes.</remarks>
    public static bool CarriesEtagOf(ReadOnlySpan<byte> document, in TenancyLedger expected)
    {
        using ParsedJsonDocument<TenancyLedger> stored = PersistedJson.ToPooledDocument<TenancyLedger>(document);
        using UnescapedUtf8JsonString etag = expected.Etag.GetUtf8String();
        return stored.RootElement.Etag.ValueEquals(etag.Span);
    }
}