// <copyright file="WorkspaceSourceJson.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo.Durability;

namespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server;

/// <summary>
/// JSON assembly for the workspace sources surface: the attach/detach drafts (the replacement
/// attachment set composed inline — stored entries carried bytes-to-bytes, the changed entry
/// written from the request), the attachment list/entry response bodies (the stored attachment
/// minus its inline <c>document</c>), and the operation-surface body from
/// <see cref="SourceOperationSurface"/>'s projection. Every seam is a single pooled write+parse
/// pass (<see cref="PersistedJson.ToPooledDocument{T, TContext}"/>) — no intermediate owned
/// buffer between the write and the (schema-validated) response/store document.
/// </summary>
internal static class WorkspaceSourceJson
{
    /// <summary>Builds the attachment-list response body (<c>{"sources": […]}</c>, each entry minus its
    /// inline document) as a pooled document — one pooled write+parse pass, no intermediate owned buffer.</summary>
    /// <param name="sources">The working copy's stored attachments (possibly undefined).</param>
    /// <returns>A pooled response document; hand it to the request workspace.</returns>
    public static ParsedJsonDocument<Models.AttachedSourceList> AttachmentListResponse(in JsonElement sources)
        => PersistedJson.ToPooledDocument<Models.AttachedSourceList, JsonElement>(
            sources,
            static (Utf8JsonWriter writer, in JsonElement s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("sources"u8);
                if (s.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in s.EnumerateArray())
                    {
                        WriteEntryWithoutDocument(writer, entry);
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });

    /// <summary>Builds the attach response body (the saved entry minus its inline document, carrying the
    /// working copy's NEW etag — the attach bumped it, and the client presents the fresh token on its next
    /// save) as a pooled document.</summary>
    /// <param name="savedSources">The saved working copy's attachments.</param>
    /// <param name="name">The attached sourceDescriptions name.</param>
    /// <param name="etag">The working copy's new concurrency token.</param>
    /// <returns>A pooled response document; hand it to the request workspace.</returns>
    public static ParsedJsonDocument<Models.AttachedSource> AttachmentResponse(in JsonElement savedSources, string name, string? etag)
    {
        var state = new AttachmentResponseState(FindAttachment(savedSources, name), etag);
        return PersistedJson.ToPooledDocument<Models.AttachedSource, AttachmentResponseState>(
            state,
            static (Utf8JsonWriter writer, in AttachmentResponseState s) => WriteEntryWithoutDocument(writer, s.Entry, s.Etag));
    }

    /// <summary>Builds the attach draft — a working-copy draft carrying ONLY the replacement attachment
    /// set (the stored entries bytes-to-bytes, minus any existing entry for the name, plus the new entry) —
    /// in one pooled write+parse pass. The store carries everything else forward.</summary>
    /// <param name="currentSources">The working copy's stored attachments (possibly undefined).</param>
    /// <param name="name">The sourceDescriptions name being attached.</param>
    /// <param name="kind"><c>registry</c> or <c>inline</c>.</param>
    /// <param name="sourceName">registry only: the registered source's name.</param>
    /// <param name="document">inline only: the document value (copied bytes-to-bytes).</param>
    /// <param name="type">The recorded source type.</param>
    /// <param name="actor">The attaching identity.</param>
    /// <param name="attachedAt">The attachment instant.</param>
    /// <returns>A pooled draft; dispose once the store call returns (the store reads it synchronously).</returns>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> DraftReplacingAttachment(in JsonElement currentSources, string name, string kind, string? sourceName, in JsonElement document, string? type, string actor, DateTimeOffset attachedAt)
    {
        // The generated contextful Create() realises the draft (text + parse metadata) in one pooled pass: the kept
        // stored entries blit in as elements, the new entry folds in from properties, and every other property is
        // omitted via default Sources (the store carries them forward).
        var state = new AttachState(currentSources, name, kind, sourceName, document, type, actor, attachedAt);
        return WorkspaceWorkflows.WorkspaceWorkflow.Create(
            context: state,
            createdAt: default,
            createdBy: default,
            document: default,
            etag: default,
            id: default,
            name: default,
            sources: WorkspaceWorkflows.WorkspaceWorkflow.AttachedSourceArray.Build(
                state,
                static (in AttachState s, ref WorkspaceWorkflows.WorkspaceWorkflow.AttachedSourceArray.Builder b) =>
                {
                    if (s.Current.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement entry in s.Current.EnumerateArray())
                        {
                            if (!EntryHasName(entry, s.Name))
                            {
                                b.AddItem(WorkspaceWorkflows.WorkspaceWorkflow.AttachedSource.From(entry));
                            }
                        }
                    }

                    b.AddItem(WorkspaceWorkflows.WorkspaceWorkflow.AttachedSource.Build(
                        attachedAt: s.AttachedAt,
                        attachedBy: s.Actor,
                        kind: s.Kind,
                        name: s.Name,
                        document: s.Document.ValueKind != JsonValueKind.Undefined ? (WorkspaceWorkflows.WorkspaceWorkflow.AttachedSource.DocumentEntity.Source)WorkspaceWorkflows.WorkspaceWorkflow.AttachedSource.DocumentEntity.From(s.Document) : default,
                        sourceName: s.SourceName is { } sn ? (JsonString.Source)sn : default,
                        type: s.Type is { } t ? (JsonString.Source)t : default));
                }));
    }

    /// <summary>Builds the detach draft — the replacement attachment set minus the named entry — in one
    /// pooled write+parse pass. Callers 404-check the entry exists first (<see cref="FindAttachment"/>).</summary>
    /// <param name="currentSources">The working copy's stored attachments.</param>
    /// <param name="name">The sourceDescriptions name being detached.</param>
    /// <returns>A pooled draft; dispose once the store call returns.</returns>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> DraftRemovingAttachment(in JsonElement currentSources, string name)
    {
        // The generated contextful Create() realises the draft in one pooled pass: the surviving stored entries blit
        // in as elements; every other property is omitted via default Sources (the store carries them forward).
        var state = new DetachState(currentSources, name);
        return WorkspaceWorkflows.WorkspaceWorkflow.Create(
            context: state,
            createdAt: default,
            createdBy: default,
            document: default,
            etag: default,
            id: default,
            name: default,
            sources: WorkspaceWorkflows.WorkspaceWorkflow.AttachedSourceArray.Build(
                state,
                static (in DetachState s, ref WorkspaceWorkflows.WorkspaceWorkflow.AttachedSourceArray.Builder b) =>
                {
                    if (s.Current.ValueKind == JsonValueKind.Array)
                    {
                        foreach (JsonElement entry in s.Current.EnumerateArray())
                        {
                            if (!EntryHasName(entry, s.Name))
                            {
                                b.AddItem(WorkspaceWorkflows.WorkspaceWorkflow.AttachedSource.From(entry));
                            }
                        }
                    }
                }));
    }

    /// <summary>Builds the create draft in one pooled write+parse pass: the display name resolved in
    /// place (the request's name, else the request document's first <c>workflowId</c> copied as UTF-8,
    /// else the base workflow id for a carry-over, else <c>untitled</c>), the document from the request
    /// (bytes-to-bytes), the catalog carry-over bytes, or an inline blank skeleton, plus designer
    /// state, provenance, and the resolved management tags. No intermediate name string or skeleton
    /// buffer — everything writes straight into the pooled draft.</summary>
    /// <param name="name">The request's display-name value (possibly undefined).</param>
    /// <param name="document">The request's document value (possibly undefined).</param>
    /// <param name="catalogDocumentUtf8">The catalog carry-over document bytes (empty when not a carry-over).</param>
    /// <param name="designerState">The request's designer-state value (possibly undefined).</param>
    /// <param name="baseWorkflowId">Carry-over provenance, when present.</param>
    /// <param name="basedOnVersion">Carry-over provenance, when present.</param>
    /// <param name="managementTags">The resolved management tags.</param>
    /// <returns>A pooled draft; dispose once the store call returns (the store reads it synchronously).</returns>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> CreateDraft(in JsonElement name, in JsonElement document, ReadOnlyMemory<byte> catalogDocumentUtf8, in JsonElement designerState, string? baseWorkflowId, int? basedOnVersion, SecurityTagSet managementTags, ReadOnlyMemory<byte> carriedScenariosUtf8 = default)
    {
        var state = new CreateState(name, document, catalogDocumentUtf8, designerState, baseWorkflowId, basedOnVersion, managementTags, carriedScenariosUtf8);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflows.WorkspaceWorkflow, CreateState>(
            state,
            static (Utf8JsonWriter writer, in CreateState s) =>
            {
                writer.WriteStartObject();

                // The display name: supplied, else the document's first workflowId (copied bytes-to-bytes),
                // else the carried-over base workflow id, else 'untitled'.
                if (s.Name.ValueKind == JsonValueKind.String)
                {
                    writer.WritePropertyName("name"u8);
                    s.Name.WriteTo(writer);
                }
                else if (TryFirstWorkflowId(s.Document, out JsonElement workflowId))
                {
                    using UnescapedUtf8JsonString id = workflowId.GetUtf8String();
                    writer.WriteString("name"u8, id.Span);
                }
                else if (s.BaseWorkflowId is { } derivedFromBase)
                {
                    writer.WriteString("name"u8, derivedFromBase);
                }
                else
                {
                    writer.WriteString("name"u8, "untitled"u8);
                }

                if (s.BaseWorkflowId is { } baseId)
                {
                    writer.WriteString("baseWorkflowId"u8, baseId);
                }

                if (s.BasedOnVersion is { } version)
                {
                    writer.WriteNumber("basedOnVersion"u8, version);
                }

                writer.WritePropertyName("document"u8);
                if (s.Document.ValueKind != JsonValueKind.Undefined)
                {
                    s.Document.WriteTo(writer);
                }
                else if (!s.CatalogDocument.IsEmpty)
                {
                    // A working copy opened from version N is the draft of version N+1: it must carry the BASE
                    // workflow id, not the catalog's internal {base}-v{N} stamp. CatalogPackage.Project adds that
                    // stamp on publish, and publish REJECTS a versioned id ("already carries a version suffix").
                    // Strip it on carry — otherwise a from-version working copy can never be published, and the
                    // designer surfaces the versioned id as if it were the author's. Only the carry path (both the
                    // base id and its version known) can name the stamp; any other document copies verbatim.
                    if (s.BaseWorkflowId is { } carryBaseId && s.BasedOnVersion is { } carryVersion)
                    {
                        WriteDocumentWithBaseWorkflowId(writer, s.CatalogDocument, carryBaseId, carryVersion);
                    }
                    else
                    {
                        writer.WriteRawValue(s.CatalogDocument.Span);
                    }
                }
                else
                {
                    // A blank skeleton: enough structure for the designer to open, deliberately not yet a
                    // valid Arazzo document (working copies hold work in progress; validation is on demand).
                    writer.WriteStartObject();
                    writer.WriteString("arazzo"u8, "1.1.0"u8);
                    writer.WriteStartObject("info"u8);
                    if (s.Name.ValueKind == JsonValueKind.String)
                    {
                        writer.WritePropertyName("title"u8);
                        s.Name.WriteTo(writer);
                    }
                    else
                    {
                        writer.WriteString("title"u8, "untitled"u8);
                    }

                    writer.WriteString("version"u8, "0.1.0"u8);
                    writer.WriteEndObject();
                    writer.WriteStartArray("sourceDescriptions"u8);
                    writer.WriteEndArray();
                    writer.WriteStartArray("workflows"u8);
                    writer.WriteEndArray();
                    writer.WriteEndObject();
                }

                if (s.DesignerState.ValueKind != JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("designerState"u8);
                    s.DesignerState.WriteTo(writer);
                }

                // Scenario carry-over (§9): a working copy created from a published version inherits
                // that version's scenario set (metadata/scenarios.json), so its tests travel forward.
                if (!s.CarriedScenarios.IsEmpty)
                {
                    writer.WritePropertyName("scenarios"u8);
                    writer.WriteRawValue(s.CarriedScenarios.Span);
                }

                if (!s.Tags.IsEmpty)
                {
                    writer.WritePropertyName("managementTags"u8);
                    s.Tags.WriteTo(writer);
                }

                writer.WriteEndObject();
            });
    }

    /// <summary>Finds the attachment entry for a sourceDescriptions name, or undefined.</summary>
    /// <param name="sources">The working copy's stored attachments (possibly undefined).</param>
    /// <param name="name">The sourceDescriptions name.</param>
    /// <returns>The entry, or an undefined element.</returns>
    public static JsonElement FindAttachment(in JsonElement sources, string name)
    {
        if (sources.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement entry in sources.EnumerateArray())
            {
                if (EntryHasName(entry, name))
                {
                    return entry;
                }
            }
        }

        return default;
    }

    /// <summary>Builds the operation-surface response body (<c>{"operations": […]}</c>) as a pooled
    /// document: the projection writes STRAIGHT THROUGH from the source document into the pooled body
    /// (schemas $ref-inlined in place) — one pass, no descriptor records, no intermediate owned buffer.
    /// The source document is read synchronously during the write, so a pooled source may dispose once
    /// this returns.</summary>
    /// <param name="sourceDocument">The OpenAPI/AsyncAPI source document's root value.</param>
    /// <returns>A pooled response document; hand it to the request workspace.</returns>
    public static ParsedJsonDocument<Models.OperationSurface> OperationSurfaceResponse(in JsonElement sourceDocument)
        => PersistedJson.ToPooledDocument<Models.OperationSurface, JsonElement>(
            sourceDocument,
            static (Utf8JsonWriter writer, in JsonElement document) =>
            {
                writer.WriteStartObject();
                writer.WritePropertyName("operations"u8);
                SourceOperationSurface.WriteOperations(writer, document);
                writer.WriteEndObject();
            });

    /// <summary>Detects an inline document's source type from its own top-level field, or <see langword="null"/>.</summary>
    /// <param name="document">The inline document.</param>
    /// <returns><c>openapi</c>, <c>asyncapi</c>, <c>arazzo</c>, or <see langword="null"/>.</returns>
    public static string? DetectDocumentType(in JsonElement document)
    {
        if (document.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        if (document.TryGetProperty("openapi"u8, out JsonElement o) && o.ValueKind == JsonValueKind.String)
        {
            return "openapi";
        }

        if (document.TryGetProperty("asyncapi"u8, out JsonElement a) && a.ValueKind == JsonValueKind.String)
        {
            return "asyncapi";
        }

        if (document.TryGetProperty("arazzo"u8, out JsonElement z) && z.ValueKind == JsonValueKind.String)
        {
            return "arazzo";
        }

        return null;
    }

    private static void WriteEntryWithoutDocument(Utf8JsonWriter writer, in JsonElement entry, string? etag = null)
    {
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty<JsonElement> property in entry.EnumerateObject())
        {
            if (property.NameEquals("document"u8))
            {
                continue;
            }

            writer.WritePropertyName(property.Name);
            property.Value.WriteTo(writer);
        }

        if (etag is not null)
        {
            writer.WriteString("etag"u8, etag);
        }

        writer.WriteEndObject();
    }

    // The request document's first workflows[].workflowId, for name derivation.
    private static bool TryFirstWorkflowId(in JsonElement document, out JsonElement workflowId)
    {
        workflowId = default;
        if (document.ValueKind != JsonValueKind.Object
            || !document.TryGetProperty("workflows"u8, out JsonElement workflows)
            || workflows.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement workflow in workflows.EnumerateArray())
        {
            return workflow.ValueKind == JsonValueKind.Object
                && workflow.TryGetProperty("workflowId"u8, out workflowId)
                && workflowId.ValueKind == JsonValueKind.String;
        }

        return false;
    }

    private static bool EntryHasName(in JsonElement entry, string name)
        => entry.ValueKind == JsonValueKind.Object
            && entry.TryGetProperty("name"u8, out JsonElement n)
            && n.ValueKind == JsonValueKind.String
            && n.ValueEquals(name);

    // Re-emits a carried catalog document, stripping the catalog's {base}-v{N} version stamp back to {base} on
    // every workflow definition's id. This is a rare, user-initiated create-from-version carry (not a hot path).
    // A parse + re-emit is the codebase's idiom for a targeted document transform (cf. WorkspaceSimulationJson
    // .DocumentBytes) — a typed-model round-trip would realise the whole document graph. The stamp is named
    // exactly ({base}-v{version}), so the match is a string-free ValueEquals — no JSON value is realised.
    private static void WriteDocumentWithBaseWorkflowId(Utf8JsonWriter writer, ReadOnlyMemory<byte> documentUtf8, string baseWorkflowId, int basedOnVersion)
    {
        string versionStamp = string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{baseWorkflowId}-v{basedOnVersion}");
        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(documentUtf8);
        JsonElement root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
        {
            root.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty<JsonElement> property in root.EnumerateObject())
        {
            if (property.NameEquals("workflows"u8) && property.Value.ValueKind == JsonValueKind.Array)
            {
                writer.WritePropertyName("workflows"u8);
                writer.WriteStartArray();
                foreach (JsonElement workflow in property.Value.EnumerateArray())
                {
                    WriteWorkflowWithBaseId(writer, workflow, baseWorkflowId, versionStamp);
                }

                writer.WriteEndArray();
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteWorkflowWithBaseId(Utf8JsonWriter writer, in JsonElement workflow, string baseWorkflowId, string versionStamp)
    {
        if (workflow.ValueKind != JsonValueKind.Object)
        {
            workflow.WriteTo(writer);
            return;
        }

        writer.WriteStartObject();
        foreach (JsonProperty<JsonElement> property in workflow.EnumerateObject())
        {
            // String-free compare (cf. WorkspaceSimulationJson.IsWorkflow): ValueEquals matches the JSON value's
            // bytes against the known stamp without realising a string.
            if (property.NameEquals("workflowId"u8) && property.Value.ValueEquals(versionStamp))
            {
                writer.WriteString("workflowId"u8, baseWorkflowId);
            }
            else
            {
                property.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    // The create draft's write context.
    private readonly struct CreateState(JsonElement name, JsonElement document, ReadOnlyMemory<byte> catalogDocument, JsonElement designerState, string? baseWorkflowId, int? basedOnVersion, SecurityTagSet tags, ReadOnlyMemory<byte> carriedScenarios)
    {
        public JsonElement Name { get; } = name;

        public JsonElement Document { get; } = document;

        public ReadOnlyMemory<byte> CatalogDocument { get; } = catalogDocument;

        public JsonElement DesignerState { get; } = designerState;

        public string? BaseWorkflowId { get; } = baseWorkflowId;

        public int? BasedOnVersion { get; } = basedOnVersion;

        public SecurityTagSet Tags { get; } = tags;

        public ReadOnlyMemory<byte> CarriedScenarios { get; } = carriedScenarios;
    }

    // The attach draft's write context: the current attachments plus the new entry's fields.
    private readonly struct AttachState(JsonElement current, string name, string kind, string? sourceName, JsonElement document, string? type, string actor, DateTimeOffset attachedAt)
    {
        public JsonElement Current { get; } = current;

        public string Name { get; } = name;

        public string Kind { get; } = kind;

        public string? SourceName { get; } = sourceName;

        public JsonElement Document { get; } = document;

        public string? Type { get; } = type;

        public string Actor { get; } = actor;

        public DateTimeOffset AttachedAt { get; } = attachedAt;
    }

    // The detach draft's write context: the current attachments and the name being removed.
    private readonly struct DetachState(JsonElement current, string name)
    {
        public JsonElement Current { get; } = current;

        public string Name { get; } = name;
    }

    // The attach response's write context: the saved entry and the working copy's new etag.
    private readonly struct AttachmentResponseState(JsonElement entry, string? etag)
    {
        public JsonElement Entry { get; } = entry;

        public string? Etag { get; } = etag;
    }
}