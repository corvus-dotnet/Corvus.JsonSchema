// <copyright file="SqliteWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
public sealed class SqliteWorkflowCatalogStore : IWorkflowCatalogStore, IAsyncDisposable
{
    private const string ColumnList =
        "BaseWorkflowId, VersionNumber, WorkflowId, Title, Description, Status, Tags, OwnerName, OwnerEmail, OwnerTeam, OwnerUrl, Sources, Hash, CreatedBy, CreatedAt, LastUpdatedBy, LastUpdatedAt, ObsoletedBy, ObsoletedAt";

    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowCatalogStore(SqliteConnection connection, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
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
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
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
            return new SqliteWorkflowCatalogStore(connection, timeProvider ?? TimeProvider.System, metadataProvider);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        return this.AddCoreAsync(baseWorkflowId, packageUtf8.ToArray(), metadata, cancellationToken);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
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
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        int limit = query.Limit <= 0 ? 100 : query.Limit;
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                "SELECT " + ColumnList +
                """

                FROM CatalogVersions
                WHERE (@baseWorkflowId IS NULL OR BaseWorkflowId = @baseWorkflowId)
                  AND (@status IS NULL OR Status = @status)
                  AND (@text IS NULL OR Title LIKE @textLike ESCAPE '\' OR (Description IS NOT NULL AND Description LIKE @textLike ESCAPE '\'))
                  AND (@owner IS NULL OR OwnerName LIKE @ownerLike ESCAPE '\' OR OwnerEmail LIKE @ownerLike ESCAPE '\')
                  AND (@workflowIdPrefix IS NULL OR WorkflowId LIKE @workflowIdPrefixLike ESCAPE '\')
                  {{tagPredicates}}
                  AND (@after IS NULL OR (BaseWorkflowId || printf('%010d', VersionNumber)) > @after)
                ORDER BY BaseWorkflowId, VersionNumber
                LIMIT @limit;
                """;
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

            if (query.Tags is { Count: > 0 } tags)
            {
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

            var versions = new List<CatalogVersion>();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                versions.Add(ReadVersion(reader));
            }

            string? continuation = null;
            if (versions.Count > limit)
            {
                versions.RemoveAt(versions.Count - 1);
                CatalogVersion last = versions[^1];
                continuation = WorkflowContinuationToken.Encode(SortKey(last.BaseWorkflowId, last.VersionNumber));
            }

            return new CatalogPage(versions, continuation);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            CatalogVersion? current = await this.ReadOneAsync(baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
            if (current is null)
            {
                return null;
            }

            CatalogStatus status = patch.Status ?? current.Status;
            bool newlyObsolete = status == CatalogStatus.Obsolete && current.Status != CatalogStatus.Obsolete;
            bool reactivated = status == CatalogStatus.Active && current.Status == CatalogStatus.Obsolete;

            CatalogOwner owner = patch.Owner ?? current.Owner;
            IReadOnlyList<string> tags = patch.Tags is { } t ? [.. t] : current.Tags;
            string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : current.ObsoletedBy;
            DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : current.ObsoletedAt;

            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText =
                """
                UPDATE CatalogVersions
                SET Status = @status, Tags = @tags, OwnerName = @ownerName, OwnerEmail = @ownerEmail, OwnerTeam = @ownerTeam, OwnerUrl = @ownerUrl,
                    LastUpdatedBy = @lastUpdatedBy, LastUpdatedAt = @lastUpdatedAt, ObsoletedBy = @obsoletedBy, ObsoletedAt = @obsoletedAt
                WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;
                """;
            update.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            update.Parameters.AddWithValue("@versionNumber", versionNumber);
            update.Parameters.AddWithValue("@status", status.ToString());
            update.Parameters.AddWithValue("@tags", (object?)EncodeTags(tags) ?? DBNull.Value);
            update.Parameters.AddWithValue("@ownerName", owner.Name);
            update.Parameters.AddWithValue("@ownerEmail", owner.Email);
            update.Parameters.AddWithValue("@ownerTeam", (object?)owner.Team ?? DBNull.Value);
            update.Parameters.AddWithValue("@ownerUrl", (object?)owner.Url ?? DBNull.Value);
            update.Parameters.AddWithValue("@lastUpdatedBy", patch.UpdatedBy);
            update.Parameters.AddWithValue("@lastUpdatedAt", now.ToUnixTimeMilliseconds());
            update.Parameters.AddWithValue("@obsoletedBy", (object?)obsoletedBy ?? DBNull.Value);
            update.Parameters.AddWithValue("@obsoletedAt", (object?)obsoletedAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return current with
            {
                Owner = owner,
                Tags = tags,
                Status = status,
                LastUpdatedBy = patch.UpdatedBy,
                LastUpdatedAt = now,
                ObsoletedBy = obsoletedBy,
                ObsoletedAt = obsoletedAt,
            };
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
            delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
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
                delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
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

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
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

            CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider);
            IReadOnlyList<string> tags = metadata.Tags is { Count: > 0 } t ? [.. t] : [];
            var version = new CatalogVersion(
                BaseWorkflowId: baseWorkflowId,
                VersionNumber: versionNumber,
                WorkflowId: projection.WorkflowId,
                Title: projection.Title,
                Description: projection.Description,
                Status: CatalogStatus.Active,
                Tags: tags,
                Owner: metadata.Owner,
                Sources: projection.Sources,
                Hash: projection.Hash,
                CreatedBy: metadata.CreatedBy,
                CreatedAt: now);

            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText =
                $"""
                INSERT INTO CatalogVersions ({ColumnList}, Package)
                VALUES (@baseWorkflowId, @versionNumber, @workflowId, @title, @description, @status, @tags, @ownerName, @ownerEmail, @ownerTeam, @ownerUrl, @sources, @hash, @createdBy, @createdAt, @lastUpdatedBy, @lastUpdatedAt, @obsoletedBy, @obsoletedAt, @package);
                """;
            insert.Parameters.AddWithValue("@baseWorkflowId", version.BaseWorkflowId);
            insert.Parameters.AddWithValue("@versionNumber", version.VersionNumber);
            insert.Parameters.AddWithValue("@workflowId", version.WorkflowId);
            insert.Parameters.AddWithValue("@title", version.Title);
            insert.Parameters.AddWithValue("@description", (object?)version.Description ?? DBNull.Value);
            insert.Parameters.AddWithValue("@status", version.Status.ToString());
            insert.Parameters.AddWithValue("@tags", (object?)EncodeTags(version.Tags) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@ownerName", version.Owner.Name);
            insert.Parameters.AddWithValue("@ownerEmail", version.Owner.Email);
            insert.Parameters.AddWithValue("@ownerTeam", (object?)version.Owner.Team ?? DBNull.Value);
            insert.Parameters.AddWithValue("@ownerUrl", (object?)version.Owner.Url ?? DBNull.Value);
            insert.Parameters.AddWithValue("@sources", (object?)EncodeSources(version.Sources) ?? DBNull.Value);
            insert.Parameters.AddWithValue("@hash", version.Hash);
            insert.Parameters.AddWithValue("@createdBy", version.CreatedBy);
            insert.Parameters.AddWithValue("@createdAt", version.CreatedAt.ToUnixTimeMilliseconds());
            insert.Parameters.AddWithValue("@lastUpdatedBy", DBNull.Value);
            insert.Parameters.AddWithValue("@lastUpdatedAt", DBNull.Value);
            insert.Parameters.AddWithValue("@obsoletedBy", DBNull.Value);
            insert.Parameters.AddWithValue("@obsoletedAt", DBNull.Value);
            insert.Parameters.AddWithValue("@package", projection.CanonicalPackage.ToArray());
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            return version;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async ValueTask<CatalogVersion?> ReadOneAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
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

    private static CatalogVersion ReadVersion(SqliteDataReader reader)
        => new(
            BaseWorkflowId: reader.GetString(0),
            VersionNumber: (int)reader.GetInt64(1),
            WorkflowId: reader.GetString(2),
            Title: reader.GetString(3),
            Description: reader.IsDBNull(4) ? null : reader.GetString(4),
            Status: Enum.Parse<CatalogStatus>(reader.GetString(5)),
            Tags: DecodeTags(reader.IsDBNull(6) ? null : reader.GetString(6)) ?? [],
            Owner: new CatalogOwner(
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.IsDBNull(10) ? null : reader.GetString(10)),
            Sources: DecodeSources(reader.IsDBNull(11) ? null : reader.GetString(11)),
            Hash: reader.GetString(12),
            CreatedBy: reader.GetString(13),
            CreatedAt: DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(14)),
            LastUpdatedBy: reader.IsDBNull(15) ? null : reader.GetString(15),
            LastUpdatedAt: reader.IsDBNull(16) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(16)),
            ObsoletedBy: reader.IsDBNull(17) ? null : reader.GetString(17),
            ObsoletedAt: reader.IsDBNull(18) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(18)));

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

    private static string? EncodeTags(IReadOnlyList<string> tags)
        => tags is { Count: > 0 } ? "\u001F" + string.Join('\u001F', tags) + "\u001F" : null;

    private static IReadOnlyList<string>? DecodeTags(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return null;
        }

        string[] parts = encoded.Trim('\u001F').Split('\u001F', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : parts;
    }

    private static string? EncodeSources(IReadOnlyList<CatalogSourceRef> sources)
    {
        if (sources.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < sources.Count; i++)
        {
            if (i > 0)
            {
                builder.Append('\u001E');
            }

            builder.Append(sources[i].Name);
            if (sources[i].Type is { } type)
            {
                builder.Append('\u001F').Append(type);
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<CatalogSourceRef> DecodeSources(string? encoded)
    {
        if (string.IsNullOrEmpty(encoded))
        {
            return [];
        }

        string[] records = encoded.Split('\u001E', StringSplitOptions.RemoveEmptyEntries);
        var sources = new List<CatalogSourceRef>(records.Length);
        foreach (string record in records)
        {
            int sep = record.IndexOf('\u001F', StringComparison.Ordinal);
            sources.Add(sep < 0
                ? new CatalogSourceRef(record, null)
                : new CatalogSourceRef(record[..sep], record[(sep + 1)..]));
        }

        return sources;
    }

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
            Package BLOB NOT NULL,
            PRIMARY KEY (BaseWorkflowId, VersionNumber)
        );
        CREATE INDEX IF NOT EXISTS IX_CatalogVersions_Status ON CatalogVersions (Status);
        CREATE INDEX IF NOT EXISTS IX_CatalogVersions_WorkflowId ON CatalogVersions (WorkflowId COLLATE NOCASE);
        """;
}