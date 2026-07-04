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
        var state = new AttachState(currentSources, name, kind, sourceName, document, type, actor, attachedAt);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflows.WorkspaceWorkflow, AttachState>(
            state,
            static (Utf8JsonWriter writer, in AttachState s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("sources"u8);
                if (s.Current.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in s.Current.EnumerateArray())
                    {
                        if (!EntryHasName(entry, s.Name))
                        {
                            entry.WriteTo(writer);
                        }
                    }
                }

                writer.WriteStartObject();
                writer.WriteString("name"u8, s.Name);
                writer.WriteString("kind"u8, s.Kind);
                if (s.SourceName is not null)
                {
                    writer.WriteString("sourceName"u8, s.SourceName);
                }

                if (s.Type is not null)
                {
                    writer.WriteString("type"u8, s.Type);
                }

                if (s.Document.ValueKind != JsonValueKind.Undefined)
                {
                    writer.WritePropertyName("document"u8);
                    s.Document.WriteTo(writer);
                }

                writer.WriteString("attachedBy"u8, s.Actor);
                writer.WriteString("attachedAt"u8, s.AttachedAt);
                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();
            });
    }

    /// <summary>Builds the detach draft — the replacement attachment set minus the named entry — in one
    /// pooled write+parse pass. Callers 404-check the entry exists first (<see cref="FindAttachment"/>).</summary>
    /// <param name="currentSources">The working copy's stored attachments.</param>
    /// <param name="name">The sourceDescriptions name being detached.</param>
    /// <returns>A pooled draft; dispose once the store call returns.</returns>
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> DraftRemovingAttachment(in JsonElement currentSources, string name)
    {
        var state = new DetachState(currentSources, name);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflows.WorkspaceWorkflow, DetachState>(
            state,
            static (Utf8JsonWriter writer, in DetachState s) =>
            {
                writer.WriteStartObject();
                writer.WriteStartArray("sources"u8);
                if (s.Current.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement entry in s.Current.EnumerateArray())
                    {
                        if (!EntryHasName(entry, s.Name))
                        {
                            entry.WriteTo(writer);
                        }
                    }
                }

                writer.WriteEndArray();
                writer.WriteEndObject();
            });
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
    public static ParsedJsonDocument<WorkspaceWorkflows.WorkspaceWorkflow> CreateDraft(in JsonElement name, in JsonElement document, ReadOnlyMemory<byte> catalogDocumentUtf8, in JsonElement designerState, string? baseWorkflowId, int? basedOnVersion, SecurityTagSet managementTags)
    {
        var state = new CreateState(name, document, catalogDocumentUtf8, designerState, baseWorkflowId, basedOnVersion, managementTags);
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
                    writer.WriteRawValue(s.CatalogDocument.Span);
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

    // The create draft's write context.
    private readonly struct CreateState(JsonElement name, JsonElement document, ReadOnlyMemory<byte> catalogDocument, JsonElement designerState, string? baseWorkflowId, int? basedOnVersion, SecurityTagSet tags)
    {
        public JsonElement Name { get; } = name;

        public JsonElement Document { get; } = document;

        public ReadOnlyMemory<byte> CatalogDocument { get; } = catalogDocument;

        public JsonElement DesignerState { get; } = designerState;

        public string? BaseWorkflowId { get; } = baseWorkflowId;

        public int? BasedOnVersion { get; } = basedOnVersion;

        public SecurityTagSet Tags { get; } = tags;
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