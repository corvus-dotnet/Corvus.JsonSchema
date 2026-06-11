// <copyright file="SqlServerWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IWorkflowCatalogStore"/> — an immutable, content-hashed, versioned store of
/// workflow document packages. Each version's canonical package is held as an opaque <c>VARBINARY(MAX)</c> blob
/// alongside its projected metadata columns (the store never re-parses the blob except to slice an addressable
/// document); versions are keyed by (base workflow id, version number). It uses Microsoft.Data.SqlClient
/// directly (no ORM, no migrations runtime), so the same code covers SQL Server, Azure SQL Database and Azure
/// SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerWorkflowCatalogStore : IWorkflowCatalogStore
{
    private const string ColumnList =
        "BaseWorkflowId, VersionNumber, WorkflowId, Title, Description, Status, Tags, OwnerName, OwnerEmail, OwnerTeam, OwnerUrl, Sources, Hash, CreatedBy, CreatedAt, LastUpdatedBy, LastUpdatedAt, ObsoletedBy, ObsoletedAt";

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerWorkflowCatalogStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Provisions the catalog schema (table and indexes). This performs DDL, so it requires a login permitted
    /// to create tables; run it once at deploy/migration time, separately from the least-privileged login used
    /// to <see cref="ConnectAsync"/> the store for operation.
    /// </summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string for a login permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the catalog store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data
    /// access on the table. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to
    /// create the schema. The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerWorkflowCatalogStore>(new SqlServerWorkflowCatalogStore(connectionString, timeProvider ?? TimeProvider.System));
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadOneAsync(connection, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT TOP (@limit) " + ColumnList +
            """

            FROM CatalogVersions
            WHERE (@baseWorkflowId IS NULL OR BaseWorkflowId = @baseWorkflowId)
              AND (@status IS NULL OR Status = @status)
              AND (@text IS NULL OR Title LIKE @textLike ESCAPE '\' OR (Description IS NOT NULL AND Description LIKE @textLike ESCAPE '\'))
              AND (@owner IS NULL OR OwnerName LIKE @ownerLike ESCAPE '\' OR OwnerEmail LIKE @ownerLike ESCAPE '\')
              AND (@workflowIdPrefix IS NULL OR WorkflowId LIKE @workflowIdPrefixLike ESCAPE '\')
              {{tagPredicates}}
              AND (@after IS NULL OR (BaseWorkflowId + RIGHT('0000000000' + CAST(VersionNumber AS NVARCHAR(10)), 10)) > @after)
            ORDER BY BaseWorkflowId, VersionNumber;
            """;
        select.Parameters.Add(NullableText("@baseWorkflowId", query.BaseWorkflowId));
        select.Parameters.Add(NullableText("@status", query.Status?.ToString()));
        select.Parameters.Add(NullableText("@text", query.Text));
        select.Parameters.Add(NullableText("@textLike", query.Text is { Length: > 0 } t ? "%" + EscapeLike(t) + "%" : null));
        select.Parameters.Add(NullableText("@owner", query.Owner));
        select.Parameters.Add(NullableText("@ownerLike", query.Owner is { Length: > 0 } o ? "%" + EscapeLike(o) + "%" : null));
        select.Parameters.Add(NullableText("@workflowIdPrefix", query.WorkflowIdPrefix));
        select.Parameters.Add(NullableText("@workflowIdPrefixLike", query.WorkflowIdPrefix is { Length: > 0 } p ? EscapeLike(p) + "%" : null));
        select.Parameters.Add(NullableText("@after", after));
        select.Parameters.AddWithValue("@limit", limit + 1);

        if (query.Tags is { Count: > 0 } tags)
        {
            var predicates = new StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                predicates.Append("AND Tags LIKE ").Append(name).Append(" ESCAPE '\\'\n              ");
                select.Parameters.Add(NullableText(name, "%" + EscapeLike(tags[i]) + "%"));
            }

            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", predicates.ToString().TrimEnd());
        }
        else
        {
            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", string.Empty);
        }

        var versions = new List<CatalogVersion>();
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        CatalogVersion? current = await ReadOneAsync(connection, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
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

        await using SqlCommand update = connection.CreateCommand();
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
        update.Parameters.Add(NullableText("@tags", EncodeTags(tags)));
        update.Parameters.AddWithValue("@ownerName", owner.Name);
        update.Parameters.AddWithValue("@ownerEmail", owner.Email);
        update.Parameters.Add(NullableText("@ownerTeam", owner.Team));
        update.Parameters.Add(NullableText("@ownerUrl", owner.Url));
        update.Parameters.AddWithValue("@lastUpdatedBy", patch.UpdatedBy);
        update.Parameters.AddWithValue("@lastUpdatedAt", now.ToUnixTimeMilliseconds());
        update.Parameters.Add(NullableText("@obsoletedBy", obsoletedBy));
        update.Parameters.Add(NullableBigint("@obsoletedAt", obsoletedAt?.ToUnixTimeMilliseconds()));
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

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        delete.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        delete.Parameters.AddWithValue("@versionNumber", versionNumber);
        return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT BaseWorkflowId, VersionNumber, WorkflowId FROM CatalogVersions WHERE Status = @status;";
        select.Parameters.AddWithValue("@status", nameof(CatalogStatus.Obsolete));

        var refs = new List<CatalogVersionRef>();
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            refs.Add(new CatalogVersionRef(reader.GetString(0), reader.GetInt32(1), reader.GetString(2)));
        }

        return refs;
    }

    /// <inheritdoc/>
    public async ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(versions);
        if (versions.Count == 0)
        {
            return;
        }

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (CatalogVersionRef reference in versions)
        {
            await using SqlCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
            delete.Parameters.AddWithValue("@baseWorkflowId", reference.BaseWorkflowId);
            delete.Parameters.AddWithValue("@versionNumber", reference.VersionNumber);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask<CatalogVersion?> ReadOneAsync(SqlConnection connection, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT {ColumnList} FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        select.Parameters.AddWithValue("@versionNumber", versionNumber);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadVersion(reader) : null;
    }

    private static CatalogVersion ReadVersion(SqlDataReader reader)
        => new(
            BaseWorkflowId: reader.GetString(0),
            VersionNumber: reader.GetInt32(1),
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

    private static SqlParameter NullableText(string name, string? value)
        => new(name, SqlDbType.NVarChar) { Value = (object?)value ?? DBNull.Value };

    private static SqlParameter NullableBigint(string name, long? value)
        => new(name, SqlDbType.BigInt) { Value = (object?)value ?? DBNull.Value };

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        int versionNumber;
        await using (SqlCommand max = connection.CreateCommand())
        {
            max.CommandText = "SELECT COALESCE(MAX(VersionNumber), 0) FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId;";
            max.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            versionNumber = (int)(await max.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! + 1;
        }

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber);
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

        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            $"""
            INSERT INTO CatalogVersions ({ColumnList}, Package)
            VALUES (@baseWorkflowId, @versionNumber, @workflowId, @title, @description, @status, @tags, @ownerName, @ownerEmail, @ownerTeam, @ownerUrl, @sources, @hash, @createdBy, @createdAt, @lastUpdatedBy, @lastUpdatedAt, @obsoletedBy, @obsoletedAt, @package);
            """;
        insert.Parameters.AddWithValue("@baseWorkflowId", version.BaseWorkflowId);
        insert.Parameters.AddWithValue("@versionNumber", version.VersionNumber);
        insert.Parameters.AddWithValue("@workflowId", version.WorkflowId);
        insert.Parameters.AddWithValue("@title", version.Title);
        insert.Parameters.Add(NullableText("@description", version.Description));
        insert.Parameters.AddWithValue("@status", version.Status.ToString());
        insert.Parameters.Add(NullableText("@tags", EncodeTags(version.Tags)));
        insert.Parameters.AddWithValue("@ownerName", version.Owner.Name);
        insert.Parameters.AddWithValue("@ownerEmail", version.Owner.Email);
        insert.Parameters.Add(NullableText("@ownerTeam", version.Owner.Team));
        insert.Parameters.Add(NullableText("@ownerUrl", version.Owner.Url));
        insert.Parameters.Add(NullableText("@sources", EncodeSources(version.Sources)));
        insert.Parameters.AddWithValue("@hash", version.Hash);
        insert.Parameters.AddWithValue("@createdBy", version.CreatedBy);
        insert.Parameters.AddWithValue("@createdAt", version.CreatedAt.ToUnixTimeMilliseconds());
        insert.Parameters.Add(NullableText("@lastUpdatedBy", null));
        insert.Parameters.Add(NullableBigint("@lastUpdatedAt", null));
        insert.Parameters.Add(NullableText("@obsoletedBy", null));
        insert.Parameters.Add(NullableBigint("@obsoletedAt", null));
        insert.Parameters.AddWithValue("@package", projection.CanonicalPackage.ToArray());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return version;
    }

    private async ValueTask<byte[]?> LoadPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Package FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        select.Parameters.AddWithValue("@versionNumber", versionNumber);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private async ValueTask<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(this.connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private const string SchemaSql =
        """
        IF OBJECT_ID(N'CatalogVersions', N'U') IS NULL
        BEGIN
            CREATE TABLE CatalogVersions (
                BaseWorkflowId NVARCHAR(450) NOT NULL,
                VersionNumber INT NOT NULL,
                WorkflowId NVARCHAR(900) NOT NULL,
                Title NVARCHAR(MAX) NOT NULL,
                Description NVARCHAR(MAX) NULL,
                Status NVARCHAR(255) NOT NULL,
                Tags NVARCHAR(MAX) NULL,
                OwnerName NVARCHAR(MAX) NOT NULL,
                OwnerEmail NVARCHAR(MAX) NOT NULL,
                OwnerTeam NVARCHAR(MAX) NULL,
                OwnerUrl NVARCHAR(MAX) NULL,
                Sources NVARCHAR(MAX) NULL,
                Hash NVARCHAR(255) NOT NULL,
                CreatedBy NVARCHAR(MAX) NOT NULL,
                CreatedAt BIGINT NOT NULL,
                LastUpdatedBy NVARCHAR(MAX) NULL,
                LastUpdatedAt BIGINT NULL,
                ObsoletedBy NVARCHAR(MAX) NULL,
                ObsoletedAt BIGINT NULL,
                Package VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_CatalogVersions PRIMARY KEY (BaseWorkflowId, VersionNumber)
            );
            CREATE INDEX IX_CatalogVersions_Status ON CatalogVersions (Status);
            CREATE INDEX IX_CatalogVersions_WorkflowId ON CatalogVersions (WorkflowId);
        END;
        """;
}