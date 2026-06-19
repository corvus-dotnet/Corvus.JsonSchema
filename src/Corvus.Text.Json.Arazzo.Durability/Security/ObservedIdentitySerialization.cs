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
/// Each result is one owned <c>byte[]</c> produced through a pooled scratch buffer (the "push JSON to the store" shape);
/// the upsert form realises the existing record's provenance into a small list to union it (the occasional write path).
/// </remarks>
public static class ObservedIdentitySerialization
{
    /// <summary>Serializes a first sighting: first-seen = last-seen = <paramref name="now"/>, provenance = [<paramref name="provenance"/>].</summary>
    /// <param name="kindToken">The grantee kind's canonical lower-case token (<see cref="GranteeKind"/>.<c>ToToken()</c>).</param>
    /// <param name="value">The grantee value (the prefix-searched subject key).</param>
    /// <param name="label">An optional display label, or <see langword="null"/>.</param>
    /// <param name="identity">The grantee's exact <c>sys:</c> identity.</param>
    /// <param name="complete">Whether <paramref name="identity"/> is the principal's whole stamped identity (§17.2).</param>
    /// <param name="now">The sighting instant.</param>
    /// <param name="provenance">Where this identity was seen.</param>
    /// <returns>The owned UTF-8 JSON document.</returns>
    public static byte[] SerializeNew(string kindToken, string value, string? label, SecurityTagSet identity, bool complete, DateTimeOffset now, string provenance)
        => Serialize(kindToken, value, label, identity, complete, now, now, [provenance]);

    /// <summary>
    /// Serializes an upsert over an existing record: preserves its first-seen, bumps last-seen to <paramref name="now"/>,
    /// unions <paramref name="provenance"/> into its sighting sources, and refreshes the label (falling back to the
    /// existing one when none is supplied), identity, and completeness.
    /// </summary>
    /// <param name="existing">The existing persisted document for this (kind, value).</param>
    /// <param name="kindToken">The grantee kind's canonical lower-case token.</param>
    /// <param name="value">The grantee value.</param>
    /// <param name="label">An optional display label; when <see langword="null"/> the existing label is kept.</param>
    /// <param name="identity">The grantee's exact <c>sys:</c> identity.</param>
    /// <param name="complete">Whether <paramref name="identity"/> is the principal's whole stamped identity (§17.2).</param>
    /// <param name="now">The sighting instant.</param>
    /// <param name="provenance">Where this identity was seen (unioned into the existing sources).</param>
    /// <returns>The owned UTF-8 JSON document.</returns>
    public static byte[] SerializeUpserted(byte[] existing, string kindToken, string value, string? label, SecurityTagSet identity, bool complete, DateTimeOffset now, string provenance)
    {
        ArgumentNullException.ThrowIfNull(existing);
        using ParsedJsonDocument<ObservedIdentity> current = PersistedJson.ToPooledDocument<ObservedIdentity>(existing);
        ObservedIdentity e = current.RootElement;
        return Serialize(kindToken, value, label ?? e.LabelOrNull, identity, complete, e.FirstSeenAtValue, now, MergeProvenance(e, provenance));
    }

    // Unions the new provenance source into the existing record's sources (order-preserving, deduped).
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

    // Serializes an observed-identity record to its owned UTF-8 document through a pooled scratch buffer.
    private static byte[] Serialize(string kind, string value, string? label, SecurityTagSet identity, bool complete, DateTimeOffset firstSeen, DateTimeOffset lastSeen, IReadOnlyList<string> provenance)
        => PersistedJson.ToArray(
            (Kind: kind, Value: value, Label: label, Identity: identity, Complete: complete, First: firstSeen, Last: lastSeen, Prov: provenance),
            static (Utf8JsonWriter writer, in (string Kind, string Value, string? Label, SecurityTagSet Identity, bool Complete, DateTimeOffset First, DateTimeOffset Last, IReadOnlyList<string> Prov) c)
                => ObservedIdentity.WriteNew(writer, c.Kind, c.Value, c.Label, c.Identity, c.Complete, c.First, c.Last, c.Prov));
}