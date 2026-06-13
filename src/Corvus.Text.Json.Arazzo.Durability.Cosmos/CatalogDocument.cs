// <copyright file="CatalogDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a workflow catalog version, generated from
/// <c>Schemas/CatalogDocument.json</c>. The store builds it from a <see cref="CatalogVersion"/> + package
/// (<see cref="From"/> → <see cref="ToJsonBytes"/>) and reads it back through a stream response or query page
/// (<see cref="FromJson"/> → <see cref="ToVersion"/>) entirely through Corvus.Text.Json — no reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/CatalogDocument.json")]
public readonly partial struct CatalogDocument
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the document id.</summary>
    public string IdValue => (string)this.Id;

    /// <summary>Computes the document id for a version ({baseWorkflowId}-v{versionNumber}).</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <returns>The document id.</returns>
    public static string DocumentId(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}-v{versionNumber}");

    /// <summary>Computes the stable sort key for a version.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <returns>The sort key.</returns>
    public static string ComputeSortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    /// <summary>Decodes the canonical package bytes.</summary>
    /// <returns>The package bytes.</returns>
    public byte[] PackageBytes() => Convert.FromBase64String((string)this.Package);

    /// <summary>Builds the document from a catalog version and its package, detached and ready to persist.</summary>
    /// <param name="version">The catalog version.</param>
    /// <param name="package">The canonical package bytes.</param>
    /// <returns>The document.</returns>
    public static CatalogDocument From(CatalogVersion version, byte[] package)
    {
        CatalogVersionRef reference = version.Ref;
        CatalogOwner owner = version.OwnerValue;

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.IdUtf8, DocumentId(reference.BaseWorkflowId, reference.VersionNumber));
            writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, reference.BaseWorkflowId);
            writer.WriteNumber(JsonPropertyNames.VersionNumberUtf8, reference.VersionNumber);
            writer.WriteString(JsonPropertyNames.SortKeyUtf8, ComputeSortKey(reference.BaseWorkflowId, reference.VersionNumber));
            writer.WriteString(JsonPropertyNames.WorkflowIdUtf8, reference.WorkflowId);
            writer.WriteString(JsonPropertyNames.WorkflowIdLowerUtf8, reference.WorkflowId.ToLowerInvariant());
            writer.WriteString(JsonPropertyNames.TitleUtf8, (string)version.Title);
            if (version.DescriptionOrNull is { } description)
            {
                writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
            }

            writer.WriteString(JsonPropertyNames.StatusUtf8, version.StatusValue.ToString());

            if (version.TagsValue is { Count: > 0 } tags)
            {
                writer.WriteStartArray(JsonPropertyNames.TagsUtf8);
                foreach (string tag in tags)
                {
                    writer.WriteStringValue(tag);
                }

                writer.WriteEndArray();
            }

            if (version.SecurityTagsValue is { Count: > 0 } securityTags)
            {
                writer.WriteStartArray(JsonPropertyNames.SecurityTagsUtf8);
                foreach (SecurityTag tag in securityTags)
                {
                    writer.WriteStartObject();
                    writer.WriteString(EmbeddedSecurityTag.JsonPropertyNames.KUtf8, tag.Key);
                    writer.WriteString(EmbeddedSecurityTag.JsonPropertyNames.VUtf8, tag.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteStartObject(JsonPropertyNames.OwnerUtf8);
            writer.WriteString(OwnerInfo.JsonPropertyNames.NameUtf8, owner.Name);
            writer.WriteString(OwnerInfo.JsonPropertyNames.EmailUtf8, owner.Email);
            if (owner.Team is { } team)
            {
                writer.WriteString(OwnerInfo.JsonPropertyNames.TeamUtf8, team);
            }

            if (owner.Url is { } url)
            {
                writer.WriteString(OwnerInfo.JsonPropertyNames.UrlUtf8, url);
            }

            writer.WriteEndObject();

            if (version.SourcesValue is { Count: > 0 } sources)
            {
                writer.WriteStartArray(JsonPropertyNames.SourcesUtf8);
                foreach (CatalogSourceRef source in sources)
                {
                    writer.WriteStartObject();
                    writer.WriteString(SourceInfo.JsonPropertyNames.NameUtf8, source.Name);
                    if (source.Type is { } type)
                    {
                        writer.WriteString(SourceInfo.JsonPropertyNames.TypeUtf8, type);
                    }

                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteString(JsonPropertyNames.HashUtf8, (string)version.Hash);
            writer.WriteBoolean(JsonPropertyNames.RunnableUtf8, (bool)version.Runnable);
            writer.WriteString(JsonPropertyNames.PackageUtf8, Convert.ToBase64String(package));
            writer.WriteString(JsonPropertyNames.CreatedByUtf8, (string)version.CreatedBy);
            writer.WriteNumber(JsonPropertyNames.CreatedAtUtf8, version.CreatedAtValue.ToUnixTimeMilliseconds());
            if (version.LastUpdatedByOrNull is { } lastUpdatedBy)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, lastUpdatedBy);
            }

            if (version.LastUpdatedAtValue is { } lastUpdatedAt)
            {
                writer.WriteNumber(JsonPropertyNames.LastUpdatedAtUtf8, lastUpdatedAt.ToUnixTimeMilliseconds());
            }

            if (version.ObsoletedByOrNull is { } obsoletedBy)
            {
                writer.WriteString(JsonPropertyNames.ObsoletedByUtf8, obsoletedBy);
            }

            if (version.ObsoletedAtValue is { } obsoletedAt)
            {
                writer.WriteNumber(JsonPropertyNames.ObsoletedAtUtf8, obsoletedAt.ToUnixTimeMilliseconds());
            }

            writer.WriteEndObject();
        }

        using ParsedJsonDocument<CatalogDocument> doc = ParsedJsonDocument<CatalogDocument>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static CatalogDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<CatalogDocument> doc = ParsedJsonDocument<CatalogDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this document to its persisted JSON form.</summary>
    /// <returns>The UTF-8 JSON document.</returns>
    public byte[] ToJsonBytes()
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            this.WriteTo(writer);
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>Projects this document back to a <see cref="CatalogVersion"/>.</summary>
    /// <returns>The catalog version.</returns>
    public CatalogVersion ToVersion()
    {
        List<string> tags = [];
        if (this.Tags.IsNotUndefined())
        {
            foreach (JsonString tag in this.Tags.EnumerateArray())
            {
                tags.Add((string)tag);
            }
        }

        List<SecurityTag>? securityTags = null;
        if (this.SecurityTags.IsNotUndefined())
        {
            securityTags = [];
            foreach (EmbeddedSecurityTag tag in this.SecurityTags.EnumerateArray())
            {
                securityTags.Add(new SecurityTag((string)tag.K, (string)tag.V));
            }
        }

        List<CatalogSourceRef> sources = [];
        if (this.Sources.IsNotUndefined())
        {
            foreach (SourceInfo source in this.Sources.EnumerateArray())
            {
                sources.Add(new CatalogSourceRef((string)source.Name, source.Type.IsNotUndefined() ? (string)source.Type : null));
            }
        }

        OwnerInfo owner = this.Owner;
        var ownerValue = new CatalogOwner(
            (string)owner.Name,
            (string)owner.Email,
            owner.Team.IsNotUndefined() ? (string)owner.Team : null,
            owner.Url.IsNotUndefined() ? (string)owner.Url : null);

        return CatalogVersion.Create(
            baseWorkflowId: (string)this.BaseWorkflowId,
            versionNumber: (int)this.VersionNumber,
            workflowId: (string)this.WorkflowId,
            title: (string)this.Title,
            description: this.Description.IsNotUndefined() ? (string)this.Description : null,
            status: Enum.Parse<CatalogStatus>((string)this.Status),
            tags: tags,
            owner: ownerValue,
            sources: sources,
            hash: (string)this.Hash,
            createdBy: (string)this.CreatedBy,
            createdAt: DateTimeOffset.FromUnixTimeMilliseconds((long)this.CreatedAt),
            lastUpdatedBy: this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null,
            lastUpdatedAt: this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.FromUnixTimeMilliseconds((long)this.LastUpdatedAt) : null,
            obsoletedBy: this.ObsoletedBy.IsNotUndefined() ? (string)this.ObsoletedBy : null,
            obsoletedAt: this.ObsoletedAt.IsNotUndefined() ? DateTimeOffset.FromUnixTimeMilliseconds((long)this.ObsoletedAt) : null,
            runnable: (bool)this.Runnable,
            securityTags: securityTags is { Count: > 0 } ? securityTags : null);
    }
}