// <copyright file="ObservedIdentity.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Security;

/// <summary>
/// A projection record of a security identity the control plane has observed (design §16.5.4) — a distinct grantee
/// (person / team / role / workflow) resolved to its exact deployment-stamped <c>sys:</c> identity, recorded so the
/// control plane can offer a store-indexed, prefix-searchable typeahead of real identities to name as grantees.
/// Generated from <c>Schemas/ObservedIdentity.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>Keyed by (<see cref="SubjectKindValue"/>, <see cref="SubjectValueValue"/>). Holds only <c>sys:</c> identity tags
/// (authorization metadata), never secrets, so it persists as plain JSON like every other entity. Backends prefix-index
/// <see cref="SubjectValueValue"/> and keyset-page the search; the identity itself is described back to operators as
/// <c>{dimension, value}</c> grants at the wire boundary, never as raw <c>sys:</c> tags.</para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/ObservedIdentity.json")]
public readonly partial struct ObservedIdentity
{
    /// <summary>Gets the grantee kind this identity names (the canonical lower-case token, e.g. <c>person</c>).</summary>
    public string SubjectKindValue => (string)this.SubjectKind;

    /// <summary>Gets the grantee value (a subject id, tenant name, role name, or workflow id).</summary>
    public string SubjectValueValue => (string)this.SubjectValue;

    /// <summary>Gets the human-friendly display label, or <see langword="null"/> if none was observed.</summary>
    public string? LabelOrNull => this.Label.IsNotUndefined() ? (string)this.Label : null;

    /// <summary>Materializes the exact <c>sys:</c> identity as a detached, owned tag set (the holder may outlive this document).</summary>
    public SecurityTagSet IdentityTagsValue => SecurityTagSet.CopyFrom(this.IdentityTags);

    /// <summary>Gets whether <see cref="IdentityTagsValue"/> is the principal's whole stamped identity (§17.2).</summary>
    public bool CompleteValue => (bool)this.Complete;

    /// <summary>Gets the instant this identity was first observed.</summary>
    public DateTimeOffset FirstSeenAtValue => ParseDate(this.FirstSeenAt);

    /// <summary>Gets the instant this identity was most recently observed.</summary>
    public DateTimeOffset LastSeenAtValue => ParseDate(this.LastSeenAt);

    /// <summary>Parses an observed-identity record from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The record.</returns>
    public static ObservedIdentity FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this record to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in ObservedIdentity v) => v.WriteTo(writer));

    /// <summary>Writes an observed-identity record into the caller's (pooled) writer in one pass — the upsert form.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="subjectKind">The grantee kind's canonical lower-case token.</param>
    /// <param name="subjectValue">The grantee value (prefix-searched).</param>
    /// <param name="label">An optional display label (omitted when <see langword="null"/>/empty).</param>
    /// <param name="identityTags">The exact <c>sys:</c> identity.</param>
    /// <param name="complete">Whether <paramref name="identityTags"/> is the principal's whole stamped identity (§17.2).</param>
    /// <param name="firstSeenAt">When this identity was first observed.</param>
    /// <param name="lastSeenAt">When this identity was most recently observed.</param>
    /// <param name="provenance">The sighting sources (omitted when empty).</param>
    public static void WriteNew(Utf8JsonWriter writer, string subjectKind, string subjectValue, string? label, SecurityTagSet identityTags, bool complete, DateTimeOffset firstSeenAt, DateTimeOffset lastSeenAt, IReadOnlyList<string>? provenance)
    {
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.SubjectKindUtf8, subjectKind);
        writer.WriteString(JsonPropertyNames.SubjectValueUtf8, subjectValue);
        if (!string.IsNullOrEmpty(label))
        {
            writer.WriteString(JsonPropertyNames.LabelUtf8, label);
        }

        writer.WritePropertyName(JsonPropertyNames.IdentityTagsUtf8);
        identityTags.WriteTo(writer);
        writer.WriteBoolean(JsonPropertyNames.CompleteUtf8, complete);
        writer.WriteString(JsonPropertyNames.FirstSeenAtUtf8, firstSeenAt);
        writer.WriteString(JsonPropertyNames.LastSeenAtUtf8, lastSeenAt);
        if (provenance is { Count: > 0 })
        {
            writer.WritePropertyName(JsonPropertyNames.ProvenanceUtf8);
            writer.WriteStartArray();
            foreach (string source in provenance)
            {
                writer.WriteStringValue(source);
            }

            writer.WriteEndArray();
        }

        writer.WriteEndObject();
    }

    // Read the instant from the strongly-typed date-time element via its native NodaTime value — no managed-string
    // realization and no JsonElement hop (the house idiom, e.g. WorkflowAdministrators, RunnerRegistration).
    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => ((NodaTime.OffsetDateTime)value).ToDateTimeOffset();
}