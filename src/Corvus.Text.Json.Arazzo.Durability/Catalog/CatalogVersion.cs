// <copyright file="CatalogVersion.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Internal;

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
    private const int DefaultBufferSize = 1024;

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

    /// <summary>Gets the free-form tags as a deferred holder over the persisted bytes.</summary>
    public TagSet TagsValue => TagSet.CopyFrom(this.Tags);

    /// <summary>Gets the security tags (KVP labels) as a deferred holder over the persisted bytes, distinct from the free-form <see cref="TagsValue"/> (§14.2).</summary>
    public SecurityTagSet SecurityTagsValue => SecurityTagSet.CopyFrom(this.SecurityTags);

    /// <summary>Gets the package source documents as a deferred holder over the persisted bytes.</summary>
    public SourceSet SourcesValue => SourceSet.CopyFrom(this.Sources);

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
    /// <returns>A pooled, disposable document of the catalog version. The caller owns it — dispose it, or transfer
    /// ownership to a <see cref="JsonWorkspace"/> (<c>workspace.TakeOwnership(doc)</c>) when its value reaches a response —
    /// so the backing memory (and its parse metadata) returns to the pool rather than being a standalone GC allocation.</returns>
    public static ParsedJsonDocument<CatalogVersion> Create(
        string baseWorkflowId,
        int versionNumber,
        string workflowId,
        string title,
        string? description,
        CatalogStatus status,
        TagSet tags,
        CatalogOwner owner,
        SourceSet sources,
        string hash,
        string createdBy,
        DateTimeOffset createdAt,
        string? lastUpdatedBy = null,
        DateTimeOffset? lastUpdatedAt = null,
        string? obsoletedBy = null,
        DateTimeOffset? obsoletedAt = null,
        bool runnable = false,
        SecurityTagSet securityTags = default)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            WriteDocument(writer, baseWorkflowId, versionNumber, workflowId, title, description, status, tags, owner, sources, hash, createdBy, createdAt, lastUpdatedBy, lastUpdatedAt, obsoletedBy, obsoletedAt, runnable, securityTags);
            writer.Flush();

            // Pooled + disposable (rented backing + pooled parse metadata), the converted-seam idiom — not a standalone
            // GC value. Column backends (SQL/Azure) return this directly; the handler transfers it to the request workspace.
            return PersistedJson.ToPooledDocument<CatalogVersion>(buffer.WrittenSpan);
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>Serializes a version's persisted JSON document to an owned <see cref="byte"/> array — for byte-storage
    /// backends (and InMemory), which persist the bytes and realize a pooled <see cref="ParsedJsonDocument{T}"/> over them
    /// on read via <see cref="ParsedJsonDocument{T}.Parse(ReadOnlyMemory{byte}, JsonDocumentOptions)"/>.</summary>
    /// <returns>The version document's UTF-8 JSON bytes.</returns>
    public static byte[] CreateBytes(
        string baseWorkflowId,
        int versionNumber,
        string workflowId,
        string title,
        string? description,
        CatalogStatus status,
        TagSet tags,
        CatalogOwner owner,
        SourceSet sources,
        string hash,
        string createdBy,
        DateTimeOffset createdAt,
        string? lastUpdatedBy = null,
        DateTimeOffset? lastUpdatedAt = null,
        string? obsoletedBy = null,
        DateTimeOffset? obsoletedAt = null,
        bool runnable = false,
        SecurityTagSet securityTags = default)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            WriteDocument(writer, baseWorkflowId, versionNumber, workflowId, title, description, status, tags, owner, sources, hash, createdBy, createdAt, lastUpdatedBy, lastUpdatedAt, obsoletedBy, obsoletedAt, runnable, securityTags);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Builds the bytes of a metadata-patched copy of <paramref name="current"/> for the document-blob stores. Only the
    /// changed governance fields are written through the mutable builder — <c>lastUpdatedBy</c>/<c>lastUpdatedAt</c>
    /// always, <c>status</c> when the patch changes it (with the obsolete/reactivate transition on
    /// <c>obsoletedBy</c>/<c>obsoletedAt</c>), <c>owner</c>/<c>tags</c> when the patch replaces them, and
    /// <c>securityTags</c> when the patch re-tags the version (§14.2). Every other field (title, hash, sources,
    /// created*, …) is carried bytes-to-bytes from the current document — no per-field string realisation. Preserves the
    /// exact transition semantics of the field-by-field rebuild it replaces.
    /// </summary>
    /// <param name="current">The current persisted version.</param>
    /// <param name="patch">The metadata patch (updated-by + optional owner/tags/status).</param>
    /// <param name="now">The update instant (recorded as <c>lastUpdatedAt</c>, and <c>obsoletedAt</c> on newly-obsolete).</param>
    /// <returns>The patched document's UTF-8 bytes.</returns>
    public static byte[] CreatePatchedBytes(in CatalogVersion current, in CatalogMetadataPatch patch, DateTimeOffset now)
    {
        CatalogStatus currentStatus = current.StatusValue;
        CatalogStatus status = patch.Status ?? currentStatus;
        bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = current.CreateBuilder(workspace);

        builder.RootElement.SetLastUpdatedBy(patch.UpdatedBy);
        builder.RootElement.SetLastUpdatedAt(now);
        if (status != currentStatus)
        {
            builder.RootElement.SetStatus(StatusToUtf8(status));
        }

        if (newlyObsolete)
        {
            builder.RootElement.SetObsoletedBy(patch.UpdatedBy);
            builder.RootElement.SetObsoletedAt(now);
        }
        else if (reactivated)
        {
            builder.RootElement.RemoveObsoletedBy();
            builder.RootElement.RemoveObsoletedAt();
        }

        if (patch.Owner is { } owner)
        {
            builder.RootElement.SetOwner(OwnerSource(owner));
        }

        if (patch.Tags is { } tags)
        {
            builder.RootElement.SetTags(TagsSource(tags));
        }

        // Re-tag (§14.2): replace the version's security tags with the patch's effective set (already merged with the
        // preserved internal tags by the security wrapper). Absent → the tags are carried bytes-to-bytes unchanged.
        if (patch.SecurityTags is { } securityTags)
        {
            if (securityTags.IsEmpty)
            {
                builder.RootElement.RemoveSecurityTags();
            }
            else
            {
                builder.RootElement.SetSecurityTags(SecurityTagInfoArray.ParseValue(securityTags.RawJson));
            }
        }

        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// Builds the bytes of a copy of <paramref name="current"/> whose <c>securityTags</c> are replaced by
    /// <paramref name="securityTags"/> (an empty set removes the property) — every other field is carried
    /// bytes-to-bytes through the mutable builder, so no field is dropped by omission. Used for the control-plane
    /// response's internal-tag strip (the persisted document keeps its internal tags for row authorization; only the
    /// client view drops them). The replacement set's persisted JSON is spliced in through the generated array's
    /// <c>ParseValue</c>, so no per-tag managed string is created.
    /// </summary>
    /// <param name="current">The current persisted version.</param>
    /// <param name="securityTags">The replacement security-tag set (empty removes the property).</param>
    /// <returns>The rewritten document's UTF-8 bytes.</returns>
    public static byte[] CreateWithSecurityTags(in CatalogVersion current, SecurityTagSet securityTags)
    {
        using JsonWorkspace workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<Mutable> builder = current.CreateBuilder(workspace);
        if (securityTags.IsEmpty)
        {
            builder.RootElement.RemoveSecurityTags();
        }
        else
        {
            builder.RootElement.SetSecurityTags(SecurityTagInfoArray.ParseValue(securityTags.RawJson));
        }

        Utf8JsonWriter writer = workspace.RentWriterAndBuffer(WriterOptions, DefaultBufferSize, out IByteBufferWriter buffer);
        try
        {
            builder.RootElement.WriteTo(writer);
            writer.Flush();
            return buffer.WrittenSpan.ToArray();
        }
        finally
        {
            workspace.ReturnWriterAndBuffer(writer, buffer);
        }
    }

    /// <summary>
    /// The effective security-tag set for a re-tag (design §14.2): the current version's deployment-internal tags (whose
    /// key carries <paramref name="internalPrefixUtf8"/>) PRESERVED — they are never user-editable and cannot be
    /// re-derived from the caller (e.g. <c>sys:workflow</c> is the version's immutable identity) — plus the caller's new
    /// non-internal tags. Bytes-native: each preserved/added tag is copied from its unescaped UTF-8, so no per-tag
    /// managed string is created; the one owned allocation is the sealed set's backing array.
    /// </summary>
    /// <param name="current">The version's current security tags (internal + user).</param>
    /// <param name="newUserTags">The caller's new non-internal tags (already validated as not using the reserved prefix).</param>
    /// <param name="internalPrefixUtf8">The reserved internal-tag key prefix as UTF-8.</param>
    /// <returns>The effective set to persist.</returns>
    public static SecurityTagSet MergeReTaggedSecurityTags(SecurityTagSet current, SecurityTagSet newUserTags, byte[] internalPrefixUtf8)
    {
        ArgumentNullException.ThrowIfNull(internalPrefixUtf8);
        return SecurityTagSet.Build(
            (Current: current, NewUser: newUserTags, Prefix: internalPrefixUtf8),
            static (ref IdentityBuilder builder, in (SecurityTagSet Current, SecurityTagSet NewUser, byte[] Prefix) s) =>
            {
                ReadOnlySpan<byte> prefix = s.Prefix;

                // Preserve the current version's deployment-internal tags (reserved prefix).
                SecurityTagSet.Utf8Enumerator ce = s.Current.EnumerateUtf8();
                try
                {
                    while (ce.MoveNext())
                    {
                        if (ce.CurrentKey.StartsWith(prefix))
                        {
                            builder.Add(ce.CurrentKey, ce.CurrentValue);
                        }
                    }
                }
                finally
                {
                    ce.Dispose();
                }

                // Append the caller's new non-internal tags.
                SecurityTagSet.Utf8Enumerator ue = s.NewUser.EnumerateUtf8();
                try
                {
                    while (ue.MoveNext())
                    {
                        builder.Add(ue.CurrentKey, ue.CurrentValue);
                    }
                }
                finally
                {
                    ue.Dispose();
                }
            });
    }

    // A replacement owner as a builder Source (the four record fields; team/url omitted when null).
    private static CatalogOwnerInfo.Source OwnerSource(CatalogOwner owner)
        => new((ref CatalogOwnerInfo.Builder b) => b.Create(
            email: owner.Email,
            name: owner.Name,
            team: owner.Team is { } team ? (JsonString.Source)team : default,
            url: owner.Url is { } url ? (JsonString.Source)url : default));

    // A replacement tag array as a builder Source. The new tags are being set, so realising them at the AddItem leaf is
    // inherent (only on the tags-patched path; the common metadata patch leaves tags carried bytes-to-bytes).
    private static JsonStringArray.Source TagsSource(TagSet tags)
        => new((ref JsonStringArray.Builder ab) =>
        {
            foreach (string tag in tags.ToList())
            {
                ab.AddItem(tag);
            }
        });

    private static void WriteDocument(
        Utf8JsonWriter writer,
        string baseWorkflowId,
        int versionNumber,
        string workflowId,
        string title,
        string? description,
        CatalogStatus status,
        TagSet tags,
        CatalogOwner owner,
        SourceSet sources,
        string hash,
        string createdBy,
        DateTimeOffset createdAt,
        string? lastUpdatedBy,
        DateTimeOffset? lastUpdatedAt,
        string? obsoletedBy,
        DateTimeOffset? obsoletedAt,
        bool runnable,
        SecurityTagSet securityTags)
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

        writer.WriteString(JsonPropertyNames.StatusUtf8, StatusToUtf8(status));

        writer.WritePropertyName(JsonPropertyNames.TagsUtf8);
        tags.WriteTo(writer);

        if (!securityTags.IsEmpty)
        {
            writer.WritePropertyName(JsonPropertyNames.SecurityTagsUtf8);
            securityTags.WriteTo(writer);
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

        writer.WritePropertyName(JsonPropertyNames.SourcesUtf8);
        sources.WriteTo(writer);

        writer.WriteString(JsonPropertyNames.HashUtf8, hash);
        writer.WriteString(JsonPropertyNames.CreatedByUtf8, createdBy);
        writer.WriteString(JsonPropertyNames.CreatedAtUtf8, createdAt);
        if (lastUpdatedBy is not null)
        {
            writer.WriteString(JsonPropertyNames.LastUpdatedByUtf8, lastUpdatedBy);
        }

        if (lastUpdatedAt is { } lua)
        {
            writer.WriteString(JsonPropertyNames.LastUpdatedAtUtf8, lua);
        }

        if (obsoletedBy is not null)
        {
            writer.WriteString(JsonPropertyNames.ObsoletedByUtf8, obsoletedBy);
        }

        if (obsoletedAt is { } oa)
        {
            writer.WriteString(JsonPropertyNames.ObsoletedAtUtf8, oa);
        }

        writer.WriteBoolean(JsonPropertyNames.RunnableUtf8, runnable);
        writer.WriteEndObject();
    }

    // The persisted status token as a UTF-8 constant — avoids an enum.ToString() string allocation per build. Must
    // round-trip through StatusValue's Enum.Parse, so the bytes equal the enum member names.
    private static ReadOnlySpan<byte> StatusToUtf8(CatalogStatus status) => status switch
    {
        CatalogStatus.Active => "Active"u8,
        CatalogStatus.Obsolete => "Obsolete"u8,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown catalog status."),
    };

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
        => PersistedJson.ToArray(this, static (Utf8JsonWriter writer, in CatalogVersion v) => v.WriteTo(writer));

    // Read the instant from the strongly-typed date-time element via its native NodaTime value — no managed-string
    // realization and no JsonElement hop (the house idiom, e.g. RunnerRegistration, SecurityRuleDocument).
    private static DateTimeOffset ParseDate(in JsonDateTime value)
        => ((NodaTime.OffsetDateTime)value).ToDateTimeOffset();
}