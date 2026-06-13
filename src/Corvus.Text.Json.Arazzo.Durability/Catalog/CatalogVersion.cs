// <copyright file="CatalogVersion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Corvus.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability;

/// <summary>
/// A catalog version's metadata — the immutable identity/hash/title/description/sources derived from the
/// package, plus the mutable governance fields and audit attribution. The package documents themselves are
/// never embedded here; they are fetched via <see cref="IWorkflowCatalogStore.GetPackageAsync"/> /
/// <see cref="IWorkflowCatalogStore.GetDocumentAsync"/>.
/// </summary>
/// <remarks>
/// This is the persisted catalog entity. JSON/KV backends (Cosmos, Mongo, NATS, Redis) store it as its JSON
/// document verbatim (see <see cref="ToJsonBytes"/> / <see cref="FromJson"/>); SQL and Azure backends keep
/// queryable columns and reconstruct the value via <see cref="Create"/>.
/// </remarks>
[JsonSchemaTypeGenerator("../Schemas/CatalogVersion.json")]
public readonly partial struct CatalogVersion
{
    private static readonly JsonWriterOptions WriterOptions = new() { Indented = false, SkipValidation = true };

    /// <summary>Gets the minimal identity reference for this version.</summary>
    public CatalogVersionRef Ref => new((string)this.BaseWorkflowId, this.VersionNumber, (string)this.WorkflowId);

    /// <summary>Gets the lifecycle status as the <see cref="CatalogStatus"/> enum.</summary>
    public CatalogStatus StatusValue => Enum.Parse<CatalogStatus>((string)this.Status);

    /// <summary>Gets the description, or <see langword="null"/> if the version has none.</summary>
    public string? DescriptionOrNull => this.Description.IsNotUndefined() ? (string)this.Description : null;

    /// <summary>Gets the last-updater, or <see langword="null"/> if the version has never been updated.</summary>
    public string? LastUpdatedByOrNull => this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null;

    /// <summary>Gets the obsoleter, or <see langword="null"/> if the version is not obsolete.</summary>
    public string? ObsoletedByOrNull => this.ObsoletedBy.IsNotUndefined() ? (string)this.ObsoletedBy : null;

    /// <summary>Gets the creation instant.</summary>
    public DateTimeOffset CreatedAtValue => ParseDate(this.CreatedAt);

    /// <summary>Gets the last-update instant, or <see langword="null"/> if the version has never been updated.</summary>
    public DateTimeOffset? LastUpdatedAtValue => this.LastUpdatedAt.IsNotUndefined() ? ParseDate(this.LastUpdatedAt) : null;

    /// <summary>Gets the obsoletion instant, or <see langword="null"/> if the version is not obsolete.</summary>
    public DateTimeOffset? ObsoletedAtValue => this.ObsoletedAt.IsNotUndefined() ? ParseDate(this.ObsoletedAt) : null;

    /// <summary>Gets the governance owner as a <see cref="CatalogOwner"/> record.</summary>
    public CatalogOwner OwnerValue
    {
        get
        {
            CatalogOwnerInfo owner = this.Owner;
            return new CatalogOwner(
                (string)owner.Name,
                (string)owner.Email,
                owner.Team.IsNotUndefined() ? (string)owner.Team : null,
                owner.Url.IsNotUndefined() ? (string)owner.Url : null);
        }
    }

    /// <summary>Gets the tags as a list of strings.</summary>
    public IReadOnlyList<string> TagsValue
    {
        get
        {
            var list = new List<string>();
            foreach (JsonString tag in this.Tags.EnumerateArray())
            {
                list.Add((string)tag);
            }

            return list;
        }
    }

    /// <summary>Gets the security tags (KVP labels) as a list, distinct from the free-form <see cref="TagsValue"/> (§14.2).</summary>
    public IReadOnlyList<SecurityTag> SecurityTagsValue
    {
        get
        {
            var list = new List<SecurityTag>();
            if (this.SecurityTags.IsNotUndefined())
            {
                foreach (SecurityTagInfo securityTag in this.SecurityTags.EnumerateArray())
                {
                    list.Add(new SecurityTag((string)securityTag.Key, (string)securityTag.Value));
                }
            }

            return list;
        }
    }

    /// <summary>Gets the package source documents as a list of <see cref="CatalogSourceRef"/> records.</summary>
    public IReadOnlyList<CatalogSourceRef> SourcesValue
    {
        get
        {
            var list = new List<CatalogSourceRef>();
            foreach (CatalogSourceInfo source in this.Sources.EnumerateArray())
            {
                list.Add(new CatalogSourceRef(
                    (string)source.Name,
                    source.Type.IsNotUndefined() ? (string)source.Type : null));
            }

            return list;
        }
    }

    /// <summary>Builds a <see cref="CatalogVersion"/> from its constituent fields, detached and ready to persist or return.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The 1-based version number.</param>
    /// <param name="workflowId">The versioned workflow id.</param>
    /// <param name="title">The title.</param>
    /// <param name="description">The description, if any.</param>
    /// <param name="status">The lifecycle status.</param>
    /// <param name="tags">The free-form tags.</param>
    /// <param name="owner">The governance owner.</param>
    /// <param name="sources">The package source documents.</param>
    /// <param name="hash">The canonical package hash.</param>
    /// <param name="createdBy">The actor that added the version.</param>
    /// <param name="createdAt">When the version was added.</param>
    /// <param name="lastUpdatedBy">The actor of the last metadata change, if any.</param>
    /// <param name="lastUpdatedAt">When the metadata was last changed, if ever.</param>
    /// <param name="obsoletedBy">The actor that marked the version obsolete, if it is.</param>
    /// <param name="obsoletedAt">When the version was marked obsolete, if it is.</param>
    /// <param name="runnable">Whether the package carries a runnable executor assembly.</param>
    /// <returns>The catalog version.</returns>
    public static CatalogVersion Create(
        string baseWorkflowId,
        int versionNumber,
        string workflowId,
        string title,
        string? description,
        CatalogStatus status,
        IReadOnlyList<string> tags,
        CatalogOwner owner,
        IReadOnlyList<CatalogSourceRef> sources,
        string hash,
        string createdBy,
        DateTimeOffset createdAt,
        string? lastUpdatedBy = null,
        DateTimeOffset? lastUpdatedAt = null,
        string? obsoletedBy = null,
        DateTimeOffset? obsoletedAt = null,
        bool runnable = false,
        IReadOnlyList<SecurityTag>? securityTags = null)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            writer.WriteStartObject();
            writer.WriteString(JsonPropertyNames.BaseWorkflowIdUtf8, baseWorkflowId);
            writer.WriteNumber(JsonPropertyNames.VersionNumberUtf8, versionNumber);
            writer.WriteString(JsonPropertyNames.WorkflowIdUtf8, workflowId);
            writer.WriteString(JsonPropertyNames.TitleUtf8, title);
            if (description is not null)
            {
                writer.WriteString(JsonPropertyNames.DescriptionUtf8, description);
            }

            writer.WriteString(JsonPropertyNames.StatusUtf8, status.ToString());

            writer.WriteStartArray(JsonPropertyNames.TagsUtf8);
            foreach (string tag in tags)
            {
                writer.WriteStringValue(tag);
            }

            writer.WriteEndArray();

            if (securityTags is { Count: > 0 })
            {
                writer.WriteStartArray(JsonPropertyNames.SecurityTagsUtf8);
                foreach (SecurityTag securityTag in securityTags)
                {
                    writer.WriteStartObject();
                    writer.WriteString(SecurityTagInfo.JsonPropertyNames.KeyUtf8, securityTag.Key);
                    writer.WriteString(SecurityTagInfo.JsonPropertyNames.ValueUtf8, securityTag.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
            }

            writer.WriteStartObject(JsonPropertyNames.OwnerUtf8);
            writer.WriteString(CatalogOwnerInfo.JsonPropertyNames.NameUtf8, owner.Name);
            writer.WriteString(CatalogOwnerInfo.JsonPropertyNames.EmailUtf8, owner.Email);
            if (owner.Team is not null)
            {
                writer.WriteString(CatalogOwnerInfo.JsonPropertyNames.TeamUtf8, owner.Team);
            }

            if (owner.Url is not null)
            {
                writer.WriteString(CatalogOwnerInfo.JsonPropertyNames.UrlUtf8, owner.Url);
            }

            writer.WriteEndObject();

            writer.WriteStartArray(JsonPropertyNames.SourcesUtf8);
            foreach (CatalogSourceRef source in sources)
            {
                writer.WriteStartObject();
                writer.WriteString(CatalogSourceInfo.JsonPropertyNames.NameUtf8, source.Name);
                if (source.Type is not null)
                {
                    writer.WriteString(CatalogSourceInfo.JsonPropertyNames.TypeUtf8, source.Type);
                }

                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteString(JsonPropertyNames.HashUtf8, hash);
            writer.WriteString(JsonPropertyNames.CreatedByUtf8, createdBy);
            writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt.ToString("O", CultureInfo.InvariantCulture));
            if (lastUpdatedBy is not null)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, lastUpdatedBy);
            }

            if (lastUpdatedAt is { } lua)
            {
                writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, lua.ToString("O", CultureInfo.InvariantCulture));
            }

            if (obsoletedBy is not null)
            {
                writer.WriteString(JsonPropertyNames.ObsoletedByUtf8, obsoletedBy);
            }

            if (obsoletedAt is { } oa)
            {
                writer.WriteString(JsonPropertyNames.ObsoletedAtUtf8, oa.ToString("O", CultureInfo.InvariantCulture));
            }

            writer.WriteBoolean(JsonPropertyNames.RunnableUtf8, runnable);
            writer.WriteEndObject();
        }

        using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(buffer.WrittenMemory);
        return doc.RootElement.Clone();
    }

    /// <summary>Parses a <see cref="CatalogVersion"/> from its persisted JSON document, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The catalog version.</returns>
    public static CatalogVersion FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<CatalogVersion> doc = ParsedJsonDocument<CatalogVersion>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Serializes this version to its persisted JSON document.</summary>
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

    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => DateTimeOffset.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
}