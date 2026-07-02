// <copyright file="SqliteWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IWorkflowCatalogStore"/> — a single-file, zero-setup catalog for
/// local-development and embedded single-node use. Each version's canonical package is held as an opaque blob
/// alongside its projected metadata columns (the store never re-parses the blob except to slice an addressable
/// document); versions are keyed by (base workflow id, version number).
/// </summary>
/// <remarks>
/// One connection is held open for the store's lifetime and all operations are serialised through it — adequate
/// for the local/embedded use this adapter targets. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string ColumnList =
        "BaseWorkflowId, VersionNumber, WorkflowId, Title, Description, Status, Tags, OwnerName, OwnerEmail, OwnerTeam, OwnerUrl, Sources, Hash, CreatedBy, CreatedAt, LastUpdatedBy, LastUpdatedAt, ObsoletedBy, ObsoletedAt, Runnable, SecurityTags";

    // Field separators for the denormalized SecurityTags column (control chars, never present in tag text).
    private const char SecurityTagPairSeparator = (char)0x1F;
    private const char SecurityTagKeyValueSeparator = (char)0x1E;

    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowCatalogStore(SqliteConnection connection, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
    }

    /// <summary>Provisions the catalog schema (table and indexes) against a file database.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a catalog store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=catalog.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="metadataProvider">An optional provider that supplies metadata for the workflow versions added to the catalog.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<ParsedJsonDocument<CatalogVersion>> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8, metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : (ReadOnlyMemory<byte>?)package;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentName);
        byte[]? package = await this.LoadPackageAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        return package is null ? null : CatalogPackage.GetDocument(package, documentName);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken cancellationToken)
    {
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        int limit = query.Limit <= 0 ? 100 : query.Limit;
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();

            // The shared filter body (base/status/text/owner/prefix/tags/security); both query modes embed it verbatim,
            // and the {{tagPredicates}}/{{securityPredicate}} placeholders are filled below.
            const string filterWhere = """
                (@baseWorkflowId IS NULL OR BaseWorkflowId = @baseWorkflowId)
                  AND (@status IS NULL OR Status = @status)
                  AND (@text IS NULL OR Title LIKE @textLike ESCAPE '\' OR (Description IS NOT NULL AND Description LIKE @textLike ESCAPE '\'))
                  AND (@owner IS NULL OR OwnerName LIKE @ownerLike ESCAPE '\' OR OwnerEmail LIKE @ownerLike ESCAPE '\')
                  AND (@workflowIdPrefix IS NULL OR WorkflowId LIKE @workflowIdPrefixLike ESCAPE '\')
                  {{tagPredicates}}
                  {{securityPredicate}}
                """;

            // distinctWorkflows: among the filtered versions of each base, rank by (Active < Obsolete < other, then
            // newest) and keep the representative (RepRank = 1); keyset-page by base workflow id alone. Otherwise page
            // every matching version by (base, version).
            select.CommandText = query.DistinctWorkflows
                ? "WITH ranked AS (\n  SELECT " + ColumnList +
                  ",\n    ROW_NUMBER() OVER (PARTITION BY BaseWorkflowId ORDER BY CASE Status WHEN 'Active' THEN 0 WHEN 'Obsolete' THEN 1 ELSE 2 END, VersionNumber DESC) AS RepRank\n" +
                  "  FROM CatalogVersions\n  WHERE " + filterWhere + "\n)\n" +
                  "SELECT " + ColumnList + " FROM ranked\nWHERE RepRank = 1 AND (@after IS NULL OR BaseWorkflowId > @after)\nORDER BY BaseWorkflowId\nLIMIT @limit;"
                : "SELECT " + ColumnList + "\nFROM CatalogVersions\nWHERE " + filterWhere +
                  "\n  AND (@after IS NULL OR (BaseWorkflowId || printf('%010d', VersionNumber)) > @after)\nORDER BY BaseWorkflowId, VersionNumber\nLIMIT @limit;";
            select.Parameters.AddWithValue("@baseWorkflowId", (object?)query.BaseWorkflowId ?? DBNull.Value);
            select.Parameters.AddWithValue("@status", (object?)query.Status?.ToString() ?? DBNull.Value);
            select.Parameters.AddWithValue("@text", (object?)query.Text ?? DBNull.Value);
            select.Parameters.AddWithValue("@textLike", query.Text is { Length: > 0 } t ? "%" + EscapeLike(t) + "%" : (object)DBNull.Value);
            select.Parameters.AddWithValue("@owner", (object?)query.Owner ?? DBNull.Value);
            select.Parameters.AddWithValue("@ownerLike", query.Owner is { Length: > 0 } o ? "%" + EscapeLike(o) + "%" : (object)DBNull.Value);
            select.Parameters.AddWithValue("@workflowIdPrefix", (object?)query.WorkflowIdPrefix ?? DBNull.Value);
            select.Parameters.AddWithValue("@workflowIdPrefixLike", query.WorkflowIdPrefix is { Length: > 0 } p ? EscapeLike(p) + "%" : (object)DBNull.Value);
            select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", limit + 1);

            if (!query.Tags.IsEmpty)
            {
                List<string> tags = query.Tags.ToList();
                var predicates = new StringBuilder();
                for (int i = 0; i < tags.Count; i++)
                {
                    string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                    predicates.Append("AND Tags LIKE ").Append(name).Append(" ESCAPE '\\'\n                  ");
                    select.Parameters.AddWithValue(name, "%\u001F" + EscapeLike(tags[i]) + "\u001F%");
                }

                select.CommandText = select.CommandText.Replace("{{tagPredicates}}", predicates.ToString().TrimEnd());
            }
            else
            {
                select.CommandText = select.CommandText.Replace("{{tagPredicates}}", string.Empty);
            }

            // Row-security reach (§14.4): translate the filter to a correlated EXISTS over the version's security
            // tags. Reached only for a store that declares ISupportsRowSecurityFilter.
            if (query.Security is { } security)
            {
                int securityParam = 0;
                var emitter = new SqlSecurityRuleEmitter(
                    "CatalogVersionSecurityTags",
                    ["BaseWorkflowId", "VersionNumber"],
                    "TagKey",
                    "TagValue",
                    "CatalogVersions",
                    value =>
                    {
                        string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                        select.Parameters.AddWithValue(name, value);
                        return name;
                    });
                select.CommandText = select.CommandText.Replace("{{securityPredicate}}", "AND (" + security.ToSqlPredicate(emitter) + ")");
            }
            else
            {
                select.CommandText = select.CommandText.Replace("{{securityPredicate}}", string.Empty);
            }

            // The page is a pooled batch of disposable version documents (the caller disposes the page). One extra row
            // is fetched as a look-ahead to detect a further page; it is not added to the batch.
            var versions = new PooledDocumentList<CatalogVersion>(limit);
            string? nextSortKey = null;
            try
            {
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (versions.Count == limit)
                    {
                        // There is at least one more matching row beyond this page; the last kept row is the cursor.
                        // In distinct mode the cursor is the base workflow id alone (the page is one row per base).
                        CatalogVersionRef last = versions[versions.Count - 1].Ref;
                        nextSortKey = query.DistinctWorkflows ? last.BaseWorkflowId : SortKey(last.BaseWorkflowId, last.VersionNumber);
                        break;
                    }

                    versions.Add(ReadVersion(reader));
                }
            }
            catch
            {
                versions.Dispose();
                throw;
            }

            return nextSortKey is not null ? CatalogPage.Create(versions, nextSortKey) : CatalogPage.Create(versions);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<CatalogVersion>?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CatalogStatus currentStatus;
            CatalogOwner owner;
            TagSet tags;
            string? obsoletedBy;
            DateTimeOffset? obsoletedAt;
            CatalogStatus status;

            // The current row is read into a pooled, disposable document only to source the unchanged fields; its
            // field accessors return OWNED COPIES, so the values are safe after the document is disposed.
            using (ParsedJsonDocument<CatalogVersion>? currentDoc = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false))
            {
                if (currentDoc is not { } cur)
                {
                    return null;
                }

                CatalogVersion current = cur.RootElement;
                currentStatus = current.StatusValue;
                status = patch.Status ?? currentStatus;
                bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
                bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

                owner = patch.Owner ?? current.OwnerValue;
                tags = patch.Tags ?? current.TagsValue;
                obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedByOrNull;
                obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAtValue;
            }

            // Re-tag (§14.2): when the patch replaces the security tags, rewrite the denormalized column AND the indexed
            // child table alongside the metadata update (the store gate serializes writers) — otherwise the tags are
            // left untouched (a single UPDATE).
            bool reTag = patch.SecurityTags is not null;

            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = reTag
                ? """
                  UPDATE CatalogVersions
                  SET Status = @status, Tags = @tags, OwnerName = @ownerName, OwnerEmail = @ownerEmail, OwnerTeam = @ownerTeam, OwnerUrl = @ownerUrl,
                      LastUpdatedBy = @lastUpdatedBy, LastUpdatedAt = @lastUpdatedAt, ObsoletedBy = @obsoletedBy, ObsoletedAt = @obsoletedAt, SecurityTags = @securityTags
                  WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;
                  """
                : """
                  UPDATE CatalogVersions
                  SET Status = @status, Tags = @tags, OwnerName = @ownerName, OwnerEmail = @ownerEmail, OwnerTeam = @ownerTeam, OwnerUrl = @ownerUrl,
                      LastUpdatedBy = @lastUpdatedBy, LastUpdatedAt = @lastUpdatedAt, ObsoletedBy = @obsoletedBy, ObsoletedAt = @obsoletedAt
                  WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;
                  """;
            update.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            update.Parameters.AddWithValue("@versionNumber", versionNumber);
            update.Parameters.AddWithValue("@status", status.ToString());
            update.Parameters.AddWithValue("@tags", (object?)tags.ToDelimitedOrNull('\u001F') ?? DBNull.Value);
            update.Parameters.AddWithValue("@ownerName", owner.Name);
            update.Parameters.AddWithValue("@ownerEmail", owner.Email);
            update.Parameters.AddWithValue("@ownerTeam", (object?)owner.Team ?? DBNull.Value);
            update.Parameters.AddWithValue("@ownerUrl", (object?)owner.Url ?? DBNull.Value);
            update.Parameters.AddWithValue("@lastUpdatedBy", patch.UpdatedBy);
            update.Parameters.AddWithValue("@lastUpdatedAt", now.ToUnixTimeMilliseconds());
            update.Parameters.AddWithValue("@obsoletedBy", (object?)obsoletedBy ?? DBNull.Value);
            update.Parameters.AddWithValue("@obsoletedAt", (object?)obsoletedAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
            if (reTag)
            {
                update.Parameters.AddWithValue("@securityTags", (object?)patch.SecurityTags!.Value.ToSecurityDelimitedOrNull(SecurityTagPairSeparator, SecurityTagKeyValueSeparator) ?? DBNull.Value);
            }

            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            if (reTag)
            {
                SecurityTagSet securityTags = patch.SecurityTags!.Value;
                using (SqliteCommand deleteTags = this.connection.CreateCommand())
                {
                    deleteTags.CommandText = "DELETE FROM CatalogVersionSecurityTags WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
                    deleteTags.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
                    deleteTags.Parameters.AddWithValue("@versionNumber", versionNumber);
                    await deleteTags.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }

                if (!securityTags.IsEmpty)
                {
                    // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
                    foreach (SecurityTag tag in securityTags.ToList())
                    {
                        using SqliteCommand tagInsert = this.connection.CreateCommand();
                        tagInsert.CommandText = "INSERT INTO CatalogVersionSecurityTags (BaseWorkflowId, VersionNumber, TagKey, TagValue) VALUES (@baseWorkflowId, @versionNumber, @key, @value);";
                        tagInsert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
                        tagInsert.Parameters.AddWithValue("@versionNumber", versionNumber);
                        tagInsert.Parameters.AddWithValue("@key", tag.Key);
                        tagInsert.Parameters.AddWithValue("@value", tag.Value);
                        await tagInsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            return await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber; DELETE FROM CatalogVersionSecurityTags WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
            delete.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            delete.Parameters.AddWithValue("@versionNumber", versionNumber);
            return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT BaseWorkflowId, VersionNumber, WorkflowId FROM CatalogVersions WHERE Status = @status;";
            select.Parameters.AddWithValue("@status", nameof(CatalogStatus.Obsolete));

            var refs = new List<CatalogVersionRef>();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                refs.Add(new CatalogVersionRef(reader.GetString(0), (int)reader.GetInt64(1), reader.GetString(2)));
            }

            return refs;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return;
        }

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            foreach (CatalogVersionRef reference in versions)
            {
                using SqliteCommand delete = this.connection.CreateCommand();
                delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber; DELETE FROM CatalogVersionSecurityTags WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
                delete.Parameters.AddWithValue("@baseWorkflowId", reference.BaseWorkflowId);
                delete.Parameters.AddWithValue("@versionNumber", reference.VersionNumber);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>> AddCoreAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            int versionNumber;
            using (SqliteCommand max = this.connection.CreateCommand())
            {
                max.CommandText = "SELECT COALESCE(MAX(VersionNumber), 0) FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId;";
                max.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
                versionNumber = (int)(long)(await max.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! + 1;
            }

            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
            TagSet tags = metadata.Tags;
            SecurityTagSet securityTags = metadata.SecurityTags;

            // Bind the columns directly from the projected/governance source values (no round-trip through the
            // CatalogVersion document); the document is built once, for the return value.
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText =
                $"""
                INSERT INTO CatalogVersions ({ColumnList}, Package)
                VALUES (@baseWorkflowId, @versionNumber, @workflowId, @title, @description, @status, @tags, @ownerName, @ownerEmail, @ownerTeam, @ownerUrl, @sources, @hash, @createdBy, @createdAt, @lastUpdatedBy, @lastUpdatedAt, @obsoletedBy, @obsoletedAt, @runnable, @securityTags, @package);
                """;
            insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            insert.Parameters.AddWithValue("@versionNumber", versionNumber);
            insert.Parameters.AddWithValue("@workflowId", projection.WorkflowId);
            insert.Parameters.AddWithValue("@title", projection.Title);
            insert.Parameters.AddWithValue("@description", (object?)projection.Description ?? DBNull.Value);
            insert.Parameters.AddWithValue("@status", nameof(CatalogStatus.Active));
            insert.Parameters.AddWithValue("@tags", (object?)tags.ToDelimitedOrNull('\u001F') ?? DBNull.Value);
            insert.Parameters.AddWithValue("@ownerName", metadata.Owner.Name);
            insert.Parameters.AddWithValue("@ownerEmail", metadata.Owner.Email);
            insert.Parameters.AddWithValue("@ownerTeam", (object?)metadata.Owner.Team ?? DBNull.Value);
            insert.Parameters.AddWithValue("@ownerUrl", (object?)metadata.Owner.Url ?? DBNull.Value);
            insert.Parameters.AddWithValue("@sources", (object?)SourceSet.FromSources(projection.Sources).ToJsonStringOrNull() ?? DBNull.Value);
            insert.Parameters.AddWithValue("@hash", projection.Hash);
            insert.Parameters.AddWithValue("@createdBy", metadata.CreatedBy);
            insert.Parameters.AddWithValue("@createdAt", now.ToUnixTimeMilliseconds());
            insert.Parameters.AddWithValue("@lastUpdatedBy", DBNull.Value);
            insert.Parameters.AddWithValue("@lastUpdatedAt", DBNull.Value);
            insert.Parameters.AddWithValue("@obsoletedBy", DBNull.Value);
            insert.Parameters.AddWithValue("@obsoletedAt", DBNull.Value);
            insert.Parameters.AddWithValue("@runnable", projection.HasExecutor ? 1 : 0);
            insert.Parameters.AddWithValue("@securityTags", (object?)securityTags.ToSecurityDelimitedOrNull(SecurityTagPairSeparator, SecurityTagKeyValueSeparator) ?? DBNull.Value);

            // The projection is the sole owner of its freshly-built canonical-package array (PackPooled returns an
            // exact-sized array, so the ReadOnlyMemory wraps it whole), so bind it directly rather than copying.
            byte[] packageBytes = MemoryMarshal.TryGetArray(projection.CanonicalPackage, out ArraySegment<byte> segment)
                && segment.Offset == 0 && segment.Array is { } array && array.Length == segment.Count
                ? array
                : projection.CanonicalPackage.ToArray();
            insert.Parameters.AddWithValue("@package", packageBytes);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Persist the version's security tags into the child table for indexed reach-filtering (§14.4).
            // Catalog versions are immutable, so this is insert-once at add time.
            if (!securityTags.IsEmpty)
            {
                // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
                foreach (SecurityTag tag in securityTags.ToList())
                {
                    using SqliteCommand tagInsert = this.connection.CreateCommand();
                    tagInsert.CommandText = "INSERT INTO CatalogVersionSecurityTags (BaseWorkflowId, VersionNumber, TagKey, TagValue) VALUES (@baseWorkflowId, @versionNumber, @key, @value);";
                    tagInsert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
                    tagInsert.Parameters.AddWithValue("@versionNumber", versionNumber);
                    tagInsert.Parameters.AddWithValue("@key", tag.Key);
                    tagInsert.Parameters.AddWithValue("@value", tag.Value);
                    await tagInsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            return CatalogVersion.Create(
                baseWorkflowId: baseWorkflowId,
                versionNumber: versionNumber,
                workflowId: projection.WorkflowId,
                title: projection.Title,
                description: projection.Description,
                status: CatalogStatus.Active,
                tags: tags,
                owner: metadata.Owner,
                sources: SourceSet.FromSources(projection.Sources),
                hash: projection.Hash,
                createdBy: metadata.CreatedBy,
                createdAt: now,
                runnable: projection.HasExecutor,
                securityTags: securityTags);
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async ValueTask<ParsedJsonDocument<CatalogVersion>?> ReadOneAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = $"SELECT {ColumnList} FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        select.Parameters.AddWithValue("@versionNumber", versionNumber);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadVersion(reader) : null;
    }

    private async ValueTask<byte[]?> LoadPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Package FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
            select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            select.Parameters.AddWithValue("@versionNumber", versionNumber);
            object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is byte[] bytes ? bytes : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private static ParsedJsonDocument<CatalogVersion> ReadVersion(SqliteDataReader reader)
        => CatalogVersion.Create(
            baseWorkflowId: reader.GetString(0),
            versionNumber: (int)reader.GetInt64(1),
            workflowId: reader.GetString(2),
            title: reader.GetString(3),
            description: reader.IsDBNull(4) ? null : reader.GetString(4),
            status: Enum.Parse<CatalogStatus>(reader.GetString(5)),
            tags: TagSet.FromDelimited(reader.IsDBNull(6) ? null : reader.GetString(6), '\u001F'),
            owner: new CatalogOwner(
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10)),
            sources: SourceSet.FromJsonStringOrEmpty(reader.IsDBNull(11) ? null : reader.GetString(11)),
            hash: reader.GetString(12),
            createdBy: reader.GetString(13),
            createdAt: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14)),
            lastUpdatedBy: reader.IsDBNull(15) ? null : reader.GetString(15),
            lastUpdatedAt: reader.IsDBNull(16) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(16)),
            obsoletedBy: reader.IsDBNull(17) ? null : reader.GetString(17),
            obsoletedAt: reader.IsDBNull(18) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(18)),
            runnable: reader.GetInt64(19) != 0,
            securityTags: SecurityTagSet.FromSecurityDelimited(reader.IsDBNull(20) ? null : reader.GetString(20), SecurityTagPairSeparator, SecurityTagKeyValueSeparator));

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS CatalogVersions (
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            WorkflowId TEXT NOT NULL,
            Title TEXT NOT NULL,
            Description TEXT NULL,
            Status TEXT NOT NULL,
            Tags TEXT NULL,
            OwnerName TEXT NOT NULL,
            OwnerEmail TEXT NOT NULL,
            OwnerTeam TEXT NULL,
            OwnerUrl TEXT NULL,
            Sources TEXT NULL,
            Hash TEXT NOT NULL,
            CreatedBy TEXT NOT NULL,
            CreatedAt INTEGER NOT NULL,
            LastUpdatedBy TEXT NULL,
            LastUpdatedAt INTEGER NULL,
            ObsoletedBy TEXT NULL,
            ObsoletedAt INTEGER NULL,
            Runnable INTEGER NOT NULL DEFAULT 0,
            SecurityTags TEXT NULL,
            Package BLOB NOT NULL,
            PRIMARY KEY (BaseWorkflowId, VersionNumber)
        );
        CREATE INDEX IF NOT EXISTS IX_CatalogVersions_Status ON CatalogVersions (Status);
        CREATE INDEX IF NOT EXISTS IX_CatalogVersions_WorkflowId ON CatalogVersions (WorkflowId COLLATE NOCASE);
        CREATE TABLE IF NOT EXISTS CatalogVersionSecurityTags (
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_CatalogVersionSecurityTags_Version ON CatalogVersionSecurityTags (BaseWorkflowId, VersionNumber);
        CREATE INDEX IF NOT EXISTS IX_CatalogVersionSecurityTags_KeyValue ON CatalogVersionSecurityTags (TagKey, TagValue);
        """;
}