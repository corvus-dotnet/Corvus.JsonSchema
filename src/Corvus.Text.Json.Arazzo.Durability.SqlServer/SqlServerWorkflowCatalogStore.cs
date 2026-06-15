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
public sealed class SqlServerWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter
{
    private const string ColumnList =
        "BaseWorkflowId, VersionNumber, WorkflowId, Title, Description, Status, Tags, OwnerName, OwnerEmail, OwnerTeam, OwnerUrl, Sources, Hash, CreatedBy, CreatedAt, LastUpdatedBy, LastUpdatedAt, ObsoletedBy, ObsoletedAt, Runnable, SecurityTags";

    // Field separators for the denormalized SecurityTags column (control chars, never present in tag text).
    private const char SecurityTagPairSeparator = (char)0x1F;
    private const char SecurityTagKeyValueSeparator = (char)0x1E;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;

    private SqlServerWorkflowCatalogStore(string connectionString, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
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
    /// <param name="metadataProvider">An optional provider that supplies projected metadata for each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="executorProvider">An optional provider that compiles the workflow executor assembly baked into each added version; <see langword="null"/> to store packages without it.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerWorkflowCatalogStore>(new SqlServerWorkflowCatalogStore(connectionString, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider));
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
              {{securityPredicate}}
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

        if (!query.Tags.IsEmpty)
        {
            List<string> tags = query.Tags.ToList();
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

        // Row-security reach (§14.4): correlated EXISTS over the version's security tags.
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
            continuation = WorkflowContinuationToken.Encode(SortKey(last.Ref.BaseWorkflowId, last.Ref.VersionNumber));
        }

        return new CatalogPage(versions, continuation);
    }

    /// <inheritdoc/>
    public async ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        CatalogVersion? current = await ReadOneAsync(connection, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
        if (current is not { } cur)
        {
            return null;
        }

        CatalogStatus currentStatus = cur.StatusValue;
        CatalogStatus status = patch.Status ?? currentStatus;
        bool newlyObsolete = status == CatalogStatus.Obsolete && currentStatus != CatalogStatus.Obsolete;
        bool reactivated = status == CatalogStatus.Active && currentStatus == CatalogStatus.Obsolete;

        CatalogOwner owner = patch.Owner ?? cur.OwnerValue;
        TagSet tags = patch.Tags ?? cur.TagsValue;
        string? obsoletedBy = newlyObsolete ? patch.UpdatedBy : reactivated ? null : cur.ObsoletedByOrNull;
        DateTimeOffset? obsoletedAt = newlyObsolete ? now : reactivated ? null : cur.ObsoletedAtValue;

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
        update.Parameters.Add(NullableText("@tags", tags.ToDelimitedOrNull('\u001F')));
        update.Parameters.AddWithValue("@ownerName", owner.Name);
        update.Parameters.AddWithValue("@ownerEmail", owner.Email);
        update.Parameters.Add(NullableText("@ownerTeam", owner.Team));
        update.Parameters.Add(NullableText("@ownerUrl", owner.Url));
        update.Parameters.AddWithValue("@lastUpdatedBy", patch.UpdatedBy);
        update.Parameters.AddWithValue("@lastUpdatedAt", now.ToUnixTimeMilliseconds());
        update.Parameters.Add(NullableText("@obsoletedBy", obsoletedBy));
        update.Parameters.Add(NullableBigint("@obsoletedAt", obsoletedAt?.ToUnixTimeMilliseconds()));
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return await ReadOneAsync(connection, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
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
            delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber; DELETE FROM CatalogVersionSecurityTags WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
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
        => CatalogVersion.Create(
            baseWorkflowId: reader.GetString(0),
            versionNumber: reader.GetInt32(1),
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
            runnable: reader.GetBoolean(19),
            securityTags: SecurityTagSet.FromSecurityDelimited(reader.IsDBNull(20) ? null : reader.GetString(20), SecurityTagPairSeparator, SecurityTagKeyValueSeparator));

    private static string SortKey(string baseWorkflowId, int versionNumber)
        => string.Create(CultureInfo.InvariantCulture, $"{baseWorkflowId}{versionNumber:D10}");

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

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        // Bind the columns directly from the projected/governance source values (no round-trip through the
        // CatalogVersion document); the document is built once, for the return value.
        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            $"""
            INSERT INTO CatalogVersions ({ColumnList}, Package)
            VALUES (@baseWorkflowId, @versionNumber, @workflowId, @title, @description, @status, @tags, @ownerName, @ownerEmail, @ownerTeam, @ownerUrl, @sources, @hash, @createdBy, @createdAt, @lastUpdatedBy, @lastUpdatedAt, @obsoletedBy, @obsoletedAt, @runnable, @securityTags, @package);
            """;
        insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        insert.Parameters.AddWithValue("@versionNumber", versionNumber);
        insert.Parameters.AddWithValue("@workflowId", projection.WorkflowId);
        insert.Parameters.AddWithValue("@title", projection.Title);
        insert.Parameters.Add(NullableText("@description", projection.Description));
        insert.Parameters.AddWithValue("@status", nameof(CatalogStatus.Active));
        insert.Parameters.Add(NullableText("@tags", tags.ToDelimitedOrNull('\u001F')));
        insert.Parameters.AddWithValue("@ownerName", metadata.Owner.Name);
        insert.Parameters.AddWithValue("@ownerEmail", metadata.Owner.Email);
        insert.Parameters.Add(NullableText("@ownerTeam", metadata.Owner.Team));
        insert.Parameters.Add(NullableText("@ownerUrl", metadata.Owner.Url));
        insert.Parameters.Add(NullableText("@sources", SourceSet.FromSources(projection.Sources).ToJsonStringOrNull()));
        insert.Parameters.AddWithValue("@hash", projection.Hash);
        insert.Parameters.AddWithValue("@createdBy", metadata.CreatedBy);
        insert.Parameters.AddWithValue("@createdAt", now.ToUnixTimeMilliseconds());
        insert.Parameters.Add(NullableText("@lastUpdatedBy", null));
        insert.Parameters.Add(NullableBigint("@lastUpdatedAt", null));
        insert.Parameters.Add(NullableText("@obsoletedBy", null));
        insert.Parameters.Add(NullableBigint("@obsoletedAt", null));
        insert.Parameters.AddWithValue("@runnable", projection.HasExecutor);
        insert.Parameters.Add(NullableText("@securityTags", securityTags.ToSecurityDelimitedOrNull(SecurityTagPairSeparator, SecurityTagKeyValueSeparator)));
        insert.Parameters.AddWithValue("@package", projection.CanonicalPackage.ToArray());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Persist the version's security tags for indexed reach-filtering (§14.4); versions are immutable.
        if (!securityTags.IsEmpty)
        {
            // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
            foreach (SecurityTag tag in securityTags.ToList())
            {
                await using SqlCommand tagInsert = connection.CreateCommand();
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
                Runnable BIT NOT NULL DEFAULT 0,
                SecurityTags NVARCHAR(MAX) NULL,
                Package VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_CatalogVersions PRIMARY KEY (BaseWorkflowId, VersionNumber)
            );
            CREATE INDEX IX_CatalogVersions_Status ON CatalogVersions (Status);
            CREATE INDEX IX_CatalogVersions_WorkflowId ON CatalogVersions (WorkflowId);
        END;
        IF OBJECT_ID(N'CatalogVersionSecurityTags', N'U') IS NULL
        BEGIN
            CREATE TABLE CatalogVersionSecurityTags (
                BaseWorkflowId NVARCHAR(450) NOT NULL,
                VersionNumber INT NOT NULL,
                TagKey NVARCHAR(255) NOT NULL,
                TagValue NVARCHAR(255) NOT NULL
            );
            CREATE INDEX IX_CatalogVersionSecurityTags_Version ON CatalogVersionSecurityTags (BaseWorkflowId, VersionNumber);
            CREATE INDEX IX_CatalogVersionSecurityTags_KeyValue ON CatalogVersionSecurityTags (TagKey, TagValue);
        END;
        """;
}