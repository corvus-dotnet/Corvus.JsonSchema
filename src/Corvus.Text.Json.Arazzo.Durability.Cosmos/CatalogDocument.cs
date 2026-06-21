// <copyright file="CatalogDocument.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// The Azure Cosmos DB document shape for a workflow catalog version, generated from
/// <c>Schemas/CatalogDocument.json</c>. The store writes it straight to a Cosmos stream from a
/// <see cref="CatalogVersion"/> + package (<see cref="WriteJson"/>) and reads it back through a stream response or
/// query page (<see cref="FromJson"/> → <see cref="ToVersion"/>) entirely through Corvus.Text.Json — no reflection serializer.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/CatalogDocument.json")]
public readonly partial struct CatalogDocument
{
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
    public byte[] PackageBytes()
    {
        // Get the base64 as raw UTF-8 (no intermediate managed string) and decode via the UTF-8 buffer decoder.
        using UnescapedUtf8JsonString utf8 = this.Package.GetUtf8String();
        return CosmosJson.DecodeBase64Utf8(utf8.Span);
    }

    /// <summary>
    /// Writes the catalog document's persisted JSON straight to <paramref name="writer"/> from a catalog version and its
    /// package — no intermediate <see cref="CatalogDocument"/> value and no re-serialization (the store hands this to
    /// <c>CosmosJson.WriteToStream</c> so the document is serialized exactly once, into a pooled stream).
    /// </summary>
    /// <param name="writer">The writer to write the document to.</param>
    /// <param name="version">The catalog version.</param>
    /// <param name="package">The canonical package bytes.</param>
    public static void WriteJson(Utf8JsonWriter writer, CatalogVersion version, byte[] package)
    {
        CatalogVersionRef reference = version.Ref;
        CatalogOwner owner = version.OwnerValue;

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

        TagSet tags = version.TagsValue;
        if (!tags.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.TagsUtf8);
            tags.WriteTo(writer);
        }

        SecurityTagSet securityTagsValue = version.SecurityTagsValue;
        if (!securityTagsValue.IsEmpty)
        {
            writer.WriteStartArray(JsonPropertyNames.SecurityTagsUtf8);
            foreach (SecurityTag tag in securityTagsValue)
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

        SourceSet sources = version.SourcesValue;
        if (!sources.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.SourcesUtf8);
            sources.WriteTo(writer);
        }

        writer.WriteString(JsonPropertyNames.HashUtf8, (string)version.Hash);
        writer.WriteBoolean(JsonPropertyNames.RunnableUtf8, (bool)version.Runnable);

        // Base64-encode the package straight into the writer — no intermediate base64 string (which would scale with
        // package size on every write). Read back by CatalogDocument.PackageBytes via Convert.FromBase64String.
        writer.WriteBase64String(JsonPropertyNames.PackageUtf8, package);
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

    /// <summary>Parses a document from its persisted JSON, detached from the parse buffer.</summary>
    /// <param name="utf8">The UTF-8 JSON document.</param>
    /// <returns>The document.</returns>
    public static CatalogDocument FromJson(ReadOnlyMemory<byte> utf8)
    {
        using ParsedJsonDocument<CatalogDocument> doc = ParsedJsonDocument<CatalogDocument>.Parse(utf8);
        return doc.RootElement.Clone();
    }

    /// <summary>Projects this document back to a catalog version, as a pooled, disposable document the caller owns.</summary>
    /// <returns>A pooled, disposable document of the catalog version; the caller disposes it (or transfers ownership to a
    /// <see cref="JsonWorkspace"/>) so its rented backing returns to the pool.</returns>
    public ParsedJsonDocument<CatalogVersion> ToVersion()
    {
        SecurityTagSet securityTags = this.ReadSecurityTags();

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
            tags: TagSet.CopyFrom(this.Tags),
            owner: ownerValue,
            sources: SourceSet.CopyFrom(this.Sources),
            hash: (string)this.Hash,
            createdBy: (string)this.CreatedBy,
            createdAt: DateTimeOffset.FromUnixTimeMilliseconds((long)this.CreatedAt),
            lastUpdatedBy: this.LastUpdatedBy.IsNotUndefined() ? (string)this.LastUpdatedBy : null,
            lastUpdatedAt: this.LastUpdatedAt.IsNotUndefined() ? DateTimeOffset.FromUnixTimeMilliseconds((long)this.LastUpdatedAt) : null,
            obsoletedBy: this.ObsoletedBy.IsNotUndefined() ? (string)this.ObsoletedBy : null,
            obsoletedAt: this.ObsoletedAt.IsNotUndefined() ? DateTimeOffset.FromUnixTimeMilliseconds((long)this.ObsoletedAt) : null,
            runnable: (bool)this.Runnable,
            securityTags: securityTags);
    }

    // Normalize the embedded { k, v } tags into the holder's canonical { key, value } bytes (Cosmos keeps the
    // short property names so the indexed reach-filter predicate over c.securityTags stays unchanged).
    private SecurityTagSet ReadSecurityTags()
    {
        if (!this.SecurityTags.IsNotUndefined())
        {
            return default;
        }

        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(256, out IByteBufferWriter buffer);
        try
        {
            writer.WriteStartArray();
            bool any = false;
            foreach (EmbeddedSecurityTag tag in this.SecurityTags.EnumerateArray())
            {
                writer.WriteStartObject();
                writer.WritePropertyName("key"u8);
                tag.K.WriteTo(writer);
                writer.WritePropertyName("value"u8);
                tag.V.WriteTo(writer);
                writer.WriteEndObject();
                any = true;
            }

            writer.WriteEndArray();
            writer.Flush();
            return any ? SecurityTagSet.CopyFromJsonArray(buffer.WrittenSpan) : default;
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }
}