// <copyright file="Environment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Environments;

/// <summary>
/// The persisted form of a deployment environment (design §7.7): a first-class, governed, reach-scoped resource a
/// workflow version is made available in and whose source credentials form a per-environment set. Generated from
/// <c>Schemas/Environment.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>
/// An environment is governed exactly like a workflow (§15): it carries an administrator set (stored separately, the
/// <c>IEnvironmentAdministratorStore</c>) and an audit trail, and creating one grants the creator administration.
/// <see cref="ManagementTagsValue"/> are the reach scope the deployment stamps at create — a caller sees and manages
/// only the environments their <c>AccessContext</c> admits.
/// </para>
/// <para>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteUpdated"/>): a store passes the
/// buffer it owns and the environment is realised and written in one pass — no interim detached clone, and every
/// carried-forward field is copied <strong>bytes-to-bytes</strong>. The leaf accessors realise only the three values a
/// store genuinely needs as managed forms — the key (<see cref="NameValue"/>), the concurrency token
/// (<see cref="EtagValue"/>), and the reach set (<see cref="ManagementTagsValue"/>); everything else stays JSON.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/Environment.json")]
public readonly partial struct Environment
{
    /// <summary>Gets the environment's name (its stable identity) — the store key.</summary>
    public string NameValue => (string)this.Name;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the security tags (KVP labels) scoping who may <strong>manage and see</strong> this environment
    /// (§14.2) as a deferred holder over the persisted bytes — empty on an unscoped environment. Drives the management
    /// reach check.</summary>
    public SecurityTagSet ManagementTagsValue => SecurityTagSet.CopyFrom(this.ManagementTags);

    /// <summary>Parses an environment from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The environment.</returns>
    public static Environment FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this environment to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in Environment v) => v.WriteTo(writer));

    /// <summary>Builds a draft environment from already-parsed JSON values (carried bytes-to-bytes — no per-field
    /// strings) plus the resolved management tags, for a store to complete with the server-stamped
    /// createdBy/createdAt/etag. Pass <see langword="default"/> (an undefined element) for any field the body omits; for
    /// an update, pass <see langword="default"/> for the immutable <paramref name="name"/> and tags — the store carries
    /// those forward from the stored environment.</summary>
    /// <param name="name">The environment name value (or undefined for an update).</param>
    /// <param name="displayName">The display-name value (or undefined).</param>
    /// <param name="description">The description value (or undefined).</param>
    /// <param name="managementTags">The resolved management tags (empty for an update).</param>
    /// <returns>A pooled, disposable draft document; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<Environment> Draft(
        in JsonElement name,
        in JsonElement displayName,
        in JsonElement description,
        in SecurityTagSet managementTags)
    {
        DraftElements state = new(name, displayName, description, managementTags);
        return PersistedJson.ToPooledDocument<Environment, DraftElements>(
            state,
            static (Utf8JsonWriter writer, in DraftElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, s.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, s.DisplayName);
                WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, s.Description);
                if (!s.ManagementTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.ManagementTags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Builds a draft environment from primitive values — the cold-path / test convenience over the bytes-native
    /// <see cref="Draft(in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>: the name and optional
    /// display name/description are written straight into the draft document (the genuine construction leaf), plus the
    /// resolved management tags. No intermediate record.</summary>
    /// <param name="name">The environment name.</param>
    /// <param name="displayName">The optional display name (omitted when <see langword="null"/>).</param>
    /// <param name="description">The optional description (omitted when <see langword="null"/>).</param>
    /// <param name="managementTags">The resolved management tags (omitted when empty).</param>
    /// <returns>A pooled, disposable draft document; the store reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<Environment> Draft(string name, string? displayName, string? description, SecurityTagSet managementTags)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PersistedJson.ToPooledDocument<Environment, (string Name, string? Display, string? Desc, SecurityTagSet Tags)>(
            (name, displayName, description, managementTags),
            static (Utf8JsonWriter writer, in (string Name, string? Display, string? Desc, SecurityTagSet Tags) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.NameUtf8, s.Name);
                if (s.Display is { } display)
                {
                    writer.WriteString(JsonPropertyNames.DisplayNameUtf8, display);
                }

                if (s.Desc is { } description)
                {
                    writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
                }

                if (!s.Tags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.Tags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Writes a brand-new environment's JSON into the caller's (pooled) writer in one pass — the draft's
    /// operator content is carried bytes-to-bytes and the server fields (createdBy/createdAt/etag) are stamped here.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor creating the environment (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in Environment draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireIdentity(draft);
        writer.WriteStartObject();
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)draft.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, (JsonElement)draft.DisplayName);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes an updated copy of this environment. The immutable identity (<c>name</c>), the immutable security
    /// tags, and the created-* audit fields are carried through from the stored environment <strong>bytes-to-bytes</strong>
    /// (the original tokens, copied verbatim — never parsed-and-reformatted); the draft's mutable content (display name,
    /// description) is carried bytes-to-bytes; only the genuinely-new audit/concurrency values are written from params.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in Environment draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();

        // Immutable identity carried forward from the stored environment bytes-to-bytes, never from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)this.Name);

        // Mutable content carried bytes-to-bytes from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, (JsonElement)draft.DisplayName);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);

        // Reach scope (§14.2): an administrator re-tag supplies managementTags on the draft (already merged with the
        // preserved deployment-internal tags by the handler) → take the draft's; an update that omits them carries the
        // stored tags forward bytes-to-bytes.
        WriteValuePreferringDraft(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags, (JsonElement)this.ManagementTags);

        // created-* audit carried forward bytes-to-bytes (copy the stored tokens verbatim — no parse/reformat).
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedByUtf8, (JsonElement)this.CreatedBy);
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedAtUtf8, (JsonElement)this.CreatedAt);

        // Genuinely-new values from typed params.
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    // Copies a draft/source property to the writer bytes-to-bytes when present (skips an undefined element).
    private static void WriteValueIfPresent(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    // Writes the draft's value when it supplies one (a re-tag), else the stored value carried forward — for a field that
    // is replaced only when the update includes it (managementTags). Both undefined → the property is omitted.
    private static void WriteValuePreferringDraft(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement draftValue, in JsonElement storedValue)
    {
        JsonElement chosen = draftValue.ValueKind != JsonValueKind.Undefined ? draftValue : storedValue;
        if (chosen.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            chosen.WriteTo(writer);
        }
    }

    // A create draft must carry the immutable identity (name); an update draft omits it (the store carries it forward).
    private static void RequireIdentity(in Environment draft)
    {
        if (!draft.Name.IsNotUndefined())
        {
            throw new ArgumentException("An environment requires a 'name'.", nameof(draft));
        }

        using UnescapedUtf8JsonString name = draft.Name.GetUtf8String();
        if (name.Span.IsEmpty)
        {
            throw new ArgumentException("An environment requires a non-empty 'name'.", nameof(draft));
        }
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values plus the resolved tag set.
    private readonly struct DraftElements(
        JsonElement name,
        JsonElement displayName,
        JsonElement description,
        SecurityTagSet managementTags)
    {
        public JsonElement Name { get; } = name;

        public JsonElement DisplayName { get; } = displayName;

        public JsonElement Description { get; } = description;

        public SecurityTagSet ManagementTags { get; } = managementTags;
    }
}