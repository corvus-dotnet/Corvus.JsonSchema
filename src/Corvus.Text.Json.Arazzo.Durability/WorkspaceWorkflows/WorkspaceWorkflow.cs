// <copyright file="WorkspaceWorkflow.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;

/// <summary>
/// The persisted form of a designer working copy (workflow-designer design §4.1): a mutable Arazzo document (plus its
/// designer UI state) saved as many times as needed during development without ever minting a catalog version.
/// Generated from <c>Schemas/WorkspaceWorkflow.json</c> and used as the domain value <em>and</em> the persisted form.
/// </summary>
/// <remarks>
/// <para>
/// A working copy is keyed by its server-assigned <see cref="IdValue"/> and reach-scoped by the
/// <see cref="ManagementTagsValue"/> the deployment stamps at create — a caller sees and manages only the working
/// copies their <c>AccessContext</c> admits. Provenance (<c>baseWorkflowId</c>/<c>basedOnVersion</c>) records the
/// catalog version it was opened from, if any; publish mints an immutable catalog version from the document.
/// </para>
/// <para>
/// Construction threads the destination through (<see cref="WriteNew"/>/<see cref="WriteUpdated"/>): a store passes the
/// buffer it owns and the working copy is realised and written in one pass — no interim detached clone, and every
/// carried-forward field is copied <strong>bytes-to-bytes</strong> (the heavy <c>document</c>/<c>designerState</c>
/// included). The leaf accessors realise only the values a store genuinely needs as managed forms — the key
/// (<see cref="IdValue"/>), the concurrency token (<see cref="EtagValue"/>), and the reach set
/// (<see cref="ManagementTagsValue"/>); everything else, including the document, stays JSON.
/// </para>
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/WorkspaceWorkflow.json")]
public readonly partial struct WorkspaceWorkflow
{
    /// <summary>Gets the working copy's server-assigned id (its stable identity) — the store key.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Gets the optimistic-concurrency token.</summary>
    public WorkflowEtag EtagValue => new((string)this.Etag);

    /// <summary>Gets the security tags (KVP labels) scoping who may <strong>manage and see</strong> this working copy
    /// (§14.2) as a deferred holder over the persisted bytes — empty on an unscoped working copy. Drives the management
    /// reach check.</summary>
    public SecurityTagSet ManagementTagsValue => SecurityTagSet.CopyFrom(this.ManagementTags);

    /// <summary>Parses a working copy from its persisted JSON as a detached value (one owned copy).</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The working copy.</returns>
    public static WorkspaceWorkflow FromJson(ReadOnlyMemory<byte> utf8) => ParseValue(utf8.Span);

    /// <summary>Serializes this working copy to its persisted JSON document.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in WorkspaceWorkflow v) => v.WriteTo(writer));

    /// <summary>Builds a draft working copy from already-parsed JSON values (carried bytes-to-bytes — no per-field
    /// strings, the document/designer state included) plus the resolved management tags, for a store to complete with the
    /// server-minted id and the stamped createdBy/createdAt/etag. Pass <see langword="default"/> (an undefined element)
    /// for any field the body omits; for a save (update), pass <see langword="default"/> for the immutable provenance and
    /// tags — the store carries those forward from the stored working copy.</summary>
    /// <param name="name">The display-name value (or undefined to carry the stored one forward on a save).</param>
    /// <param name="baseWorkflowId">The provenance base workflow id value (or undefined).</param>
    /// <param name="basedOnVersion">The provenance version-number value (or undefined).</param>
    /// <param name="document">The Arazzo document value (required on create; a save that omits it keeps the stored one).</param>
    /// <param name="designerState">The designer UI state value (or undefined).</param>
    /// <param name="sources">The attached-sources value (or undefined — attachments are edited through their own endpoint; a save that omits them keeps the stored ones).</param>
    /// <param name="managementTags">The resolved management tags (empty for a save).</param>
    /// <returns>A pooled, disposable draft document; <c>using</c> it and pass its
    /// <see cref="ParsedJsonDocument{T}.RootElement"/> to the store, which reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<WorkspaceWorkflow> Draft(
        in JsonElement name,
        in JsonElement baseWorkflowId,
        in JsonElement basedOnVersion,
        in JsonElement document,
        in JsonElement designerState,
        in JsonElement sources,
        in SecurityTagSet managementTags)
    {
        DraftElements state = new(name, baseWorkflowId, basedOnVersion, document, designerState, sources, managementTags);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow, DraftElements>(
            state,
            static (Utf8JsonWriter writer, in DraftElements s) =>
            {
                writer.WriteStartObject();
                WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, s.Name);
                WriteValueIfPresent(writer, JsonPropertyNames.BaseWorkflowIdUtf8, s.BaseWorkflowId);
                WriteValueIfPresent(writer, JsonPropertyNames.BasedOnVersionUtf8, s.BasedOnVersion);
                WriteValueIfPresent(writer, JsonPropertyNames.DocumentUtf8, s.Document);
                WriteValueIfPresent(writer, JsonPropertyNames.DesignerStateUtf8, s.DesignerState);
                WriteValueIfPresent(writer, JsonPropertyNames.SourcesUtf8, s.Sources);
                if (!s.ManagementTags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.ManagementTags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Builds a draft working copy from primitive values — the cold-path / test convenience over the
    /// bytes-native <see cref="Draft(in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in JsonElement, in SecurityTagSet)"/>:
    /// the display name, the raw Arazzo document JSON, and the optional provenance are written straight into the draft
    /// document (the genuine construction leaf), plus the resolved management tags. No intermediate record.</summary>
    /// <param name="name">The display name.</param>
    /// <param name="documentUtf8">The Arazzo document as a raw UTF-8 JSON value (omitted when empty — for a save that keeps the stored document).</param>
    /// <param name="designerStateUtf8">The designer UI state as a raw UTF-8 JSON value (omitted when empty).</param>
    /// <param name="baseWorkflowId">The optional provenance base workflow id (omitted when <see langword="null"/>).</param>
    /// <param name="basedOnVersion">The optional provenance version (omitted when <see langword="null"/>).</param>
    /// <param name="managementTags">The resolved management tags (omitted when empty).</param>
    /// <returns>A pooled, disposable draft document; the store reads it synchronously before it is disposed.</returns>
    public static ParsedJsonDocument<WorkspaceWorkflow> Draft(string name, ReadOnlyMemory<byte> documentUtf8, ReadOnlyMemory<byte> designerStateUtf8, string? baseWorkflowId, int? basedOnVersion, SecurityTagSet managementTags)
    {
        ArgumentNullException.ThrowIfNull(name);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow, (string Name, ReadOnlyMemory<byte> Doc, ReadOnlyMemory<byte> State, string? Base, int? Version, SecurityTagSet Tags)>(
            (name, documentUtf8, designerStateUtf8, baseWorkflowId, basedOnVersion, managementTags),
            static (Utf8JsonWriter writer, in (string Name, ReadOnlyMemory<byte> Doc, ReadOnlyMemory<byte> State, string? Base, int? Version, SecurityTagSet Tags) s) =>
            {
                writer.WriteStartObject();
                writer.WriteString(JsonPropertyNames.NameUtf8, s.Name);
                if (s.Base is { } baseId)
                {
                    writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, baseId);
                }

                if (s.Version is { } version)
                {
                    writer.WriteNumber(JsonPropertyNames.BasedOnVersionUtf8, version);
                }

                if (!s.Doc.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.DocumentUtf8);
                    writer.WriteRawValue(s.Doc.Span);
                }

                if (!s.State.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.DesignerStateUtf8);
                    writer.WriteRawValue(s.State.Span);
                }

                if (!s.Tags.IsEmpty)
                {
                    writer.WritePropertyName(JsonPropertyNames.ManagementTagsUtf8);
                    s.Tags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Writes a brand-new working copy's JSON into the caller's (pooled) writer in one pass — the draft's
    /// operator content (the document included) is carried bytes-to-bytes; the server-minted id and the server fields
    /// (createdBy/createdAt/etag) are stamped here.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the operator-supplied content as JSON values (read bytes-to-bytes).</param>
    /// <param name="id">The server-minted working-copy id.</param>
    /// <param name="actor">The actor creating the working copy (audit).</param>
    /// <param name="createdAt">The creation instant.</param>
    /// <param name="etag">The optimistic-concurrency token to assign.</param>
    public static void WriteNew(Utf8JsonWriter writer, in WorkspaceWorkflow draft, string id, string actor, DateTimeOffset createdAt, WorkflowEtag etag)
    {
        RequireCreation(draft);
        writer.WriteStartObject();
        writer.WriteString(JsonPropertyNames.IdUtf8, id);
        WriteValueIfPresent(writer, JsonPropertyNames.NameUtf8, (JsonElement)draft.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.BaseWorkflowIdUtf8, (JsonElement)draft.BaseWorkflowId);
        WriteValueIfPresent(writer, JsonPropertyNames.BasedOnVersionUtf8, (JsonElement)draft.BasedOnVersion);
        WriteValueIfPresent(writer, JsonPropertyNames.DocumentUtf8, (JsonElement)draft.Document);
        WriteValueIfPresent(writer, JsonPropertyNames.DesignerStateUtf8, (JsonElement)draft.DesignerState);
        WriteValueIfPresent(writer, JsonPropertyNames.SourcesUtf8, (JsonElement)draft.Sources);
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)draft.ManagementTags);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    /// <summary>Writes a saved copy of this working copy. The immutable identity (<c>id</c>), the provenance, the
    /// immutable security tags, and the created-* audit fields are carried through from the stored working copy
    /// <strong>bytes-to-bytes</strong> (the original tokens, copied verbatim — never parsed-and-reformatted); the
    /// document/designer state/name are taken from the draft when it supplies them (a save) and otherwise carried
    /// forward; only the genuinely-new audit/concurrency values are written from params.</summary>
    /// <param name="writer">The writer to serialize into.</param>
    /// <param name="draft">The draft carrying the new mutable content as JSON values (read bytes-to-bytes).</param>
    /// <param name="actor">The actor performing the save (audit).</param>
    /// <param name="updatedAt">The save instant.</param>
    /// <param name="etag">The new optimistic-concurrency token to assign.</param>
    public void WriteUpdated(Utf8JsonWriter writer, in WorkspaceWorkflow draft, string actor, DateTimeOffset updatedAt, WorkflowEtag etag)
    {
        writer.WriteStartObject();

        // Immutable identity + provenance carried forward from the stored working copy bytes-to-bytes, never the draft.
        WriteValueIfPresent(writer, JsonPropertyNames.IdUtf8, (JsonElement)this.Id);
        WriteValuePreferringDraft(writer, JsonPropertyNames.NameUtf8, (JsonElement)draft.Name, (JsonElement)this.Name);
        WriteValueIfPresent(writer, JsonPropertyNames.BaseWorkflowIdUtf8, (JsonElement)this.BaseWorkflowId);
        WriteValueIfPresent(writer, JsonPropertyNames.BasedOnVersionUtf8, (JsonElement)this.BasedOnVersion);

        // The document/designer state are replaced when the draft supplies them (the save), otherwise carried forward —
        // either way bytes-to-bytes (no parse/reformat of the potentially large document).
        WriteValuePreferringDraft(writer, JsonPropertyNames.DocumentUtf8, (JsonElement)draft.Document, (JsonElement)this.Document);
        WriteValuePreferringDraft(writer, JsonPropertyNames.DesignerStateUtf8, (JsonElement)draft.DesignerState, (JsonElement)this.DesignerState);

        // Attachments are edited through their own endpoint (an attach/detach supplies the whole
        // replacement set); a plain save omits them and the stored set carries forward.
        WriteValuePreferringDraft(writer, JsonPropertyNames.SourcesUtf8, (JsonElement)draft.Sources, (JsonElement)this.Sources);

        // Reach scope (§14.2) is immutable: carried forward from the stored working copy bytes-to-bytes.
        WriteValueIfPresent(writer, JsonPropertyNames.ManagementTagsUtf8, (JsonElement)this.ManagementTags);

        // created-* audit carried forward bytes-to-bytes (copy the stored tokens verbatim — no parse/reformat).
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedByUtf8, (JsonElement)this.CreatedBy);
        WriteValueIfPresent(writer, JsonPropertyNames.CreatedAtUtf8, (JsonElement)this.CreatedAt);

        // Genuinely-new values from typed params.
        writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, actor);
        writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, updatedAt);
        writer.WriteString(JsonPropertyNames.EtagUtf8, etag.Value ?? string.Empty);
        writer.WriteEndObject();
    }

    // Copies a draft/working-copy property to the writer bytes-to-bytes when present (skips an undefined element).
    private static void WriteValueIfPresent(Utf8JsonWriter writer, ReadOnlySpan<byte> name, in JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Undefined)
        {
            writer.WritePropertyName(name);
            value.WriteTo(writer);
        }
    }

    // Writes the draft's value when present (a save), else the stored value carried forward — both bytes-to-bytes.
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

    // A create draft must carry a display name and the Arazzo document; a save draft may omit the name (carried forward)
    // but the API contract requires its document.
    private static void RequireCreation(in WorkspaceWorkflow draft)
    {
        if (!draft.Name.IsNotUndefined())
        {
            throw new ArgumentException("A working copy requires a 'name'.", nameof(draft));
        }

        using UnescapedUtf8JsonString name = draft.Name.GetUtf8String();
        if (name.Span.IsEmpty)
        {
            throw new ArgumentException("A working copy requires a non-empty 'name'.", nameof(draft));
        }

        if (!draft.Document.IsNotUndefined())
        {
            throw new ArgumentException("A working copy requires a 'document'.", nameof(draft));
        }
    }

    // The bytes-to-bytes draft context: the request body's already-parsed JSON values (the document included) plus the
    // resolved tag set.
    private readonly struct DraftElements(
        JsonElement name,
        JsonElement baseWorkflowId,
        JsonElement basedOnVersion,
        JsonElement document,
        JsonElement designerState,
        JsonElement sources,
        SecurityTagSet managementTags)
    {
        public JsonElement Name { get; } = name;

        public JsonElement BaseWorkflowId { get; } = baseWorkflowId;

        public JsonElement BasedOnVersion { get; } = basedOnVersion;

        public JsonElement Document { get; } = document;

        public JsonElement DesignerState { get; } = designerState;

        public JsonElement Sources { get; } = sources;

        public SecurityTagSet ManagementTags { get; } = managementTags;
    }
}