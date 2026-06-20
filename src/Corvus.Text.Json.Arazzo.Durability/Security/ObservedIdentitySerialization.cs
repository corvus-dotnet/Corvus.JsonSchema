// <copyright file="ObservedIdentitySerialization.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// The one place an <see cref="ObservedIdentity"/> sighting becomes its persisted JSON document, shared by every
/// <see cref="IObservedIdentityStore"/> backend (the <see cref="SourceCredentialSerialization"/> idiom). A new sighting
/// stamps first/last-seen now with a single-source provenance; an upsert over the existing document preserves
/// first-seen, bumps last-seen, unions the new provenance source, and refreshes the label/identity/completeness — so the
/// merge semantics are identical across the in-memory reference and all durable backends.
/// </summary>
/// <remarks>
/// The grantee <c>value</c> and <c>label</c> are taken as their <strong>JSON values</strong> and written bytes-to-bytes
/// through a pooled writer — no reification, no managed string per field (the kind/provenance tokens are interned literals,
/// not per-call allocations). This writer is the genuine leaf for the document body. The result is one owned <c>byte[]</c>
/// (the persisted document); the upsert form additionally parses the existing record (to preserve first-seen, union
/// provenance, and fall back to its label) — the occasional write path.
/// </remarks>
public static class ObservedIdentitySerialization
{
    /// <summary>Serializes a first sighting: first-seen = last-seen = <paramref name="now"/>, provenance = [<paramref name="provenance"/>].</summary>
    /// <param name="kind">The grantee kind as a JSON value — written bytes-to-bytes (the store reifies it only at its own key leaf).</param>
    /// <param name="value">The grantee value (the prefix-searched subject key) as a JSON value.</param>
    /// <param name="label">An optional display label as a JSON value (undefined for none).</param>
    /// <param name="identity">The grantee's exact <c>sys:</c> identity.</param>
    /// <param name="complete">Whether <paramref name="identity"/> is the principal's whole stamped identity (§17.2).</param>
    /// <param name="now">The sighting instant.</param>
    /// <param name="provenance">Where this identity was seen (an interned provenance literal).</param>
    /// <returns>The owned UTF-8 JSON document.</returns>
    public static byte[] SerializeNew(in ObservedIdentity.GranteeKind kind, in JsonString value, in JsonString label, SecurityTagSet identity, bool complete, DateTimeOffset now, string provenance)
        => Serialize(kind, value, label, identity, complete, now, now, [provenance]);

    /// <summary>
    /// Serializes an upsert over an existing record: preserves its first-seen, bumps last-seen to <paramref name="now"/>,
    /// unions <paramref name="provenance"/> into its sighting sources, and refreshes the label (falling back to the
    /// existing one when none is supplied), identity, and completeness.
    /// </summary>
    /// <param name="existing">The existing persisted document for this (kind, value).</param>
    /// <param name="kind">The grantee kind as a JSON value — written bytes-to-bytes (the store reifies it only at its own key leaf).</param>
    /// <param name="value">The grantee value as unescaped UTF-8.</param>
    /// <param name="label">An optional display label as a JSON value; undefined keeps the existing label.</param>
    /// <param name="identity">The grantee's exact <c>sys:</c> identity.</param>
    /// <param name="complete">Whether <paramref name="identity"/> is the principal's whole stamped identity (§17.2).</param>
    /// <param name="now">The sighting instant.</param>
    /// <param name="provenance">Where this identity was seen (unioned into the existing sources).</param>
    /// <returns>The owned UTF-8 JSON document.</returns>
    public static byte[] SerializeUpserted(byte[] existing, in ObservedIdentity.GranteeKind kind, in JsonString value, in JsonString label, SecurityTagSet identity, bool complete, DateTimeOffset now, string provenance)
    {
        ArgumentNullException.ThrowIfNull(existing);
        using ParsedJsonDocument<ObservedIdentity> current = PersistedJson.ToPooledDocument<ObservedIdentity>(existing);
        ObservedIdentity e = current.RootElement;

        // Keep the existing label (its own JSON value, written bytes-to-bytes) when no new one is supplied — the existing
        // document lease stays alive through the synchronous Serialize, then is returned to the pool. From() rewraps it as
        // a JsonString without reifying.
        JsonString effectiveLabel = label.IsNotUndefined() ? label : JsonString.From(e.Label);
        return Serialize(kind, value, effectiveLabel, identity, complete, e.FirstSeenAtValue, now, MergeProvenance(e, provenance));
    }

    // Unions the new provenance source into the existing record's sources (order-preserving, deduped). Provenance tokens
    // are a small bounded set of interned literals; this realizes them into a small list on the occasional upsert.
    private static List<string> MergeProvenance(ObservedIdentity existing, string add)
    {
        var list = new List<string>();
        if (existing.Provenance.IsNotUndefined())
        {
            foreach (JsonString source in existing.Provenance.EnumerateArray())
            {
                list.Add((string)source);
            }
        }

        if (!list.Contains(add))
        {
            list.Add(add);
        }

        return list;
    }

    // Serializes an observed-identity record to its owned UTF-8 document through a pooled scratch buffer, threading the
    // kind/value/label JSON values via a state struct; WriteNew writes them bytes-to-bytes.
    private static byte[] Serialize(in ObservedIdentity.GranteeKind kind, in JsonString value, in JsonString label, SecurityTagSet identity, bool complete, DateTimeOffset firstSeen, DateTimeOffset lastSeen, IReadOnlyList<string> provenance)
    {
        var state = new WriteState(kind, value, label, identity, complete, firstSeen, lastSeen, provenance);
        return PersistedJson.ToArray(
            in state,
            static (Utf8JsonWriter writer, in WriteState s)
                => ObservedIdentity.WriteNew(writer, s.Kind, s.Value, s.Label, s.Identity, s.Complete, s.First, s.Last, s.Prov));
    }

    private readonly ref struct WriteState(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, DateTimeOffset first, DateTimeOffset last, IReadOnlyList<string> prov)
    {
        public ObservedIdentity.GranteeKind Kind { get; } = kind;

        public JsonString Value { get; } = value;

        public JsonString Label { get; } = label;

        public SecurityTagSet Identity { get; } = identity;

        public bool Complete { get; } = complete;

        public DateTimeOffset First { get; } = first;

        public DateTimeOffset Last { get; } = last;

        public IReadOnlyList<string> Prov { get; } = prov;
    }
}