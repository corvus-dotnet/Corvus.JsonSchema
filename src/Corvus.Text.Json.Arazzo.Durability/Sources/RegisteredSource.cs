// <copyright file="RegisteredSource.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Sources;

/// <summary>
/// The persisted form of a registered source (design §7.6): a first-class, reach-scoped resource a workflow references by
/// name in its <c>sourceDescriptions</c>. Generated from <c>Schemas/Source.json</c> and used as the domain value
/// <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>
/// A source carries its OpenAPI/AsyncAPI <see cref="RegisteredSource.Document"/> verbatim plus the reach-scoping
/// <see cref="ManagementTagsValue"/> the deployment stamps at create — a caller sees and manages only the sources their
/// <c>AccessContext</c> admits. The per-environment credentials that authenticate calls to it are a separate resource
/// (the source-credential store); there is no administrator set — reach membership is the management gate.
/// </para>
/// <para>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteUpdated"/>): a store passes the
/// buffer it owns and the source is realised and written in one pass — no interim detached clone, and every
/// carried-forward field is copied <strong>bytes-to-bytes</strong> (the heavy <c>document</c> included). The leaf
/// accessors realise only the three values a store genuinely needs as managed forms — the key (<see cref="NameValue"/>),
/// the concurrency token (<see cref="EtagValue"/>), and the reach set (<see cref="ManagementTagsValue"/>); everything
/// else, including the document, stays JSON.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/Source.json")]
public readonly partial struct RegisteredSource
{
    /// <summary>Gets the source's name (its stable identity) — the store key.</summary>
    public string NameValue => (string)this.Name;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the security tags (KVP labels) scoping who may <strong>manage and see</strong> this source (§14.2)
    /// as a deferred holder over the persisted bytes — empty on an unscoped source. Drives the management reach check.</summary>
    public SecurityTagSet ManagementTagsValue => SecurityTagSet.CopyFrom(this.ManagementTags);

    /// <summary>Parses a source from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The source.</returns>
    public static RegisteredSource FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this source to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in RegisteredSource v) => v.WriteTo(writer));

    /// <summary>Builds a draft source from already-parsed JSON values (carried bytes-to-bytes — no per-field strings, the
    /// document included) plus the resolved management tags, for a store to complete with the server-stamped
    /// createdBy/createdAt/etag. Pass <see langword="default"/> (an undefined element) for any field the body omits; for
    /// an update, pass <see langword="default"/> for the immutable <paramref name="name"/>/<paramref name="type"/> and
    /// tags — the store carries those forward from the stored source — and pass <see langword="default"/> for
    /// <paramref name="document"/> to keep the stored document unchanged.</summary>
    /// <param name="name">The source name value (or undefined for an update).</param>
    /// <param name="type">The source type value (or undefined for an update).</param>
    /// <param name="document">The source document value (or undefined to keep the stored one on an update).</param>
    /// <param name="displayName">The display-name value (or undefined).</param>
    /// <param name="description">The description value (or undefined).</param>
    /// <param name="managementTags">The resolved management tags (empty for an update).</param>
    /// <returns>A pooled, disposable draft document; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<RegisteredSource> Draft(
        in JsonElement name,
        in JsonElement type,
        in JsonElement document,
        in JsonElement displayName,
        in JsonElement description,
        in SecurityTagSet managementTags)
    {
        DraftElements state = new(name, type, document, displayName, description, managementTags);
        return PersistedJson.ToPooledDocument<RegisteredSource, DraftElements>(
            state,
            static (Utf8JsonWriter writer, in DraftElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, s.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.TypeUtf8, s.Type);
                WriteValueIfPresent(writer, JsonPropertyNames.DocumentUtf8, s.Document);
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

    /// <summary>Builds a draft source from primitive values — the cold-path / test convenience over the bytes-native
    /// <see cref="Draft(in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>:
    /// the name, type, the raw document JSON, and the optional display name/description are written straight into the
    /// draft document (the genuine construction leaf), plus the resolved management tags. No intermediate record.</summary>
    /// <param name="name">The source name.</param>
    /// <param name="type">The source type (e.g. <c>openapi</c>).</param>
    /// <param name="documentUtf8">The source document as a raw UTF-8 JSON value (omitted when empty — for an update that keeps the stored document).</param>
    /// <param name="displayName">The optional display name (omitted when <see langword="null"/>).</param>
    /// <param name="description">The optional description (omitted when <see langword="null"/>).</param>
    /// <param name="managementTags">The resolved management tags (omitted when empty).</param>
    /// <returns>A pooled, disposable draft document; the store reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<RegisteredSource> Draft(string name, string? type, ReadOnlyMemory<byte> documentUtf8, string? displayName, string? description, SecurityTagSet managementTags)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PersistedJson.ToPooledDocument<RegisteredSource, (string Name, string? Type, ReadOnlyMemory<byte> Doc, string? Display, string? Desc, SecurityTagSet Tags)>(
            (name, type, documentUtf8, displayName, description, managementTags),
            static (Utf8JsonWriter writer, in (string Name, string? Type, ReadOnlyMemory<byte> Doc, string? Display, string? Desc, SecurityTagSet Tags) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.NameUtf8, s.Name);
                if (s.Type is { } type)
                {
                    writer.WriteString(JsonPropertyNames.TypeUtf8, type);
                }

                if (!s.Doc.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.DocumentUtf8);
                    writer.WriteRawValue(s.Doc.Span);
                }

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

    /// <summary>Writes a brand-new source's JSON into the caller's (pooled) writer in one pass — the draft's operator
    /// content (the document included) is carried bytes-to-bytes and the server fields (createdBy/createdAt/etag) are
    /// stamped here.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor registering the source (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in RegisteredSource draft, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireRegistration(draft);
        writer.WriteStartObject();
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)draft.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.TypeUtf8, (JsonElement)draft.Type);
        WriteValueIfPresent(writer, JsonPropertyNames.DocumentUtf8, (JsonElement)draft.Document);
        WriteValueIfPresent(writer, JsonPropertyNames.DisplayNameUtf8, (JsonElement)draft.DisplayName);
        WriteValueIfPresent(writer, JsonPropertyNames.DescriptionUtf8, (JsonElement)draft.Description);
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes an updated copy of this source. The immutable identity (<c>name</c>, <c>type</c>), the immutable
    /// security tags, and the created-* audit fields are carried through from the stored source <strong>bytes-to-bytes</strong>
    /// (the original tokens, copied verbatim — never parsed-and-reformatted); the draft's mutable content (display name,
    /// description) is carried bytes-to-bytes; the document is taken from the draft when it supplies one (a rotation) and
    /// otherwise carried forward from the stored source bytes-to-bytes; only the genuinely-new audit/concurrency values
    /// are written from params.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor performing the update (audit).</param>
    /// <param name="updatedAt">The update instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in RegisteredSource draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();

        // Immutable identity + type carried forward from the stored source bytes-to-bytes, never from the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)this.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.TypeUtf8, (JsonElement)this.Type);

        // The document is rotated when the draft supplies one, otherwise carried forward from the stored source — either
        // way bytes-to-bytes (no parse/reformat of the potentially large document).
        WriteValuePreferringDraft(writer, JsonPropertyNames.DocumentUtf8, (JsonElement)draft.Document, (JsonElement)this.Document);

        // Mutable metadata carried bytes-to-bytes from the draft.
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

    // Writes the draft's value when present (a rotation), else the stored value carried forward — both bytes-to-bytes.
    private static void WriteValuePreferringDraft(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement draftValue, in JsonElement storedValue)
    {
        if (draftValue.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            draftValue.WriteTo(writer);
        }
        else if (storedValue.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            storedValue.WriteTo(writer);
        }
    }

    // A create draft must carry the immutable identity (name + type) and the document; an update draft omits name/type
    // (the store carries them forward) and may omit the document (carried forward).
    private static void RequireRegistration(in RegisteredSource draft)
    {
        if (!draft.Name.IsNotUndefined())
        {
            throw new ArgumentException("A source requires a 'name'.", nameof(draft));
        }

        using UnescapedUtf8JsonString name = draft.Name.GetUtf8String();
        if (name.Span.IsEmpty)
        {
            throw new ArgumentException("A source requires a non-empty 'name'.", nameof(draft));
        }

        if (!draft.Type.IsNotUndefined())
        {
            throw new ArgumentException("A source requires a 'type'.", nameof(draft));
        }

        if (!draft.Document.IsNotUndefined())
        {
            throw new ArgumentException("A source requires a 'document'.", nameof(draft));
        }
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values (the document included) plus the
    // resolved tag set.
    private readonly struct DraftElements(
        JsonElement name,
        JsonElement type,
        JsonElement document,
        JsonElement displayName,
        JsonElement description,
        SecurityTagSet managementTags)
    {
        public JsonElement Name { get; } = name;

        public JsonElement Type { get; } = type;

        public JsonElement Document { get; } = document;

        public JsonElement DisplayName { get; } = displayName;

        public JsonElement Description { get; } = description;

        public SecurityTagSet ManagementTags { get; } = managementTags;
    }
}