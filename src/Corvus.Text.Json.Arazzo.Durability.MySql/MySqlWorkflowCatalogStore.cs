// <copyright file="MySqlWorkflowCatalogStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IWorkflowCatalogStore"/> — an immutable, content-hashed, versioned store of
/// workflow document packages. Each version's canonical package is held as an opaque <c>LONGBLOB</c> alongside
/// its projected metadata columns (the store never re-parses the blob except to slice an addressable document);
/// versions are keyed by (base workflow id, version number). It speaks the MySQL wire protocol directly
/// (MySqlConnector, no ORM, no migrations runtime), so it also serves MariaDB and Aurora MySQL.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class MySqlWorkflowCatalogStore : IWorkflowCatalogStore, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string ColumnList =
        "BaseWorkflowId, VersionNumber, WorkflowId, Title, Description, Status, Tags, OwnerName, OwnerEmail, OwnerTeam, OwnerUrl, Sources, Hash, CreatedBy, CreatedAt, LastUpdatedBy, LastUpdatedAt, ObsoletedBy, ObsoletedAt, Runnable, SecurityTags";

    // Field separators for the denormalized SecurityTags column (control chars, never present in tag text).
    private const char SecurityTagPairSeparator = (char)0x1F;
    private const char SecurityTagKeyValueSeparator = (char)0x1E;

    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;
    private readonly IWorkflowMetadataProvider? metadataProvider;
    private readonly IWorkflowExecutorProvider? executorProvider;

    private MySqlWorkflowCatalogStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider, IWorkflowMetadataProvider? metadataProvider, IWorkflowExecutorProvider? executorProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
        this.metadataProvider = metadataProvider;
        this.executorProvider = executorProvider;
    }

    /// <summary>
    /// Provisions the catalog schema (table and indexes). This performs DDL, so it requires a user permitted to
    /// create tables; run it once at deploy/migration time, separately from the least-privileged user used to
    /// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the store for operation.
    /// </summary>
    /// <param name="connectionString">A MySqlConnector connection string for a user permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (string statement in SchemaStatements)
        {
            await using MySqlCommand schema = connection.CreateCommand();
            schema.CommandText = statement;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational user granted only data access
    /// on the table. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with an elevated user — to
    /// create the schema.
    /// </remarks>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<MySqlWorkflowCatalogStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();

        return new ValueTask<MySqlWorkflowCatalogStore>(
            new MySqlWorkflowCatalogStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider));
    }

    /// <summary>Provisions the catalog schema over a caller-supplied data source.</summary>
    /// <remarks>
    /// Supply a data source the caller configured — for example one whose credential provider supplies Entra
    /// ID/IAM tokens — so provisioning runs under a deliberate credential. The caller retains ownership.
    /// </remarks>
    /// <param name="dataSource">A MySqlConnector data source whose user is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using MySqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        foreach (string statement in SchemaStatements)
        {
            await using MySqlCommand schema = connection.CreateCommand();
            schema.CommandText = statement;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a data source the caller configured — for example with a least-privileged operational user — so
    /// the store runs under a least-privileged principal. Performs no DDL — call
    /// <see cref="PrepareAsync(MySqlDataSource, CancellationToken)"/> first.
    /// </remarks>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlWorkflowCatalogStore> ConnectAsync(
        MySqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        IWorkflowMetadataProvider? metadataProvider = null,
        IWorkflowExecutorProvider? executorProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();

        return new ValueTask<MySqlWorkflowCatalogStore>(
            new MySqlWorkflowCatalogStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System, metadataProvider, executorProvider));
    }

    /// <summary>Disposes the data source if this store created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT " + ColumnList +
            """

            FROM CatalogVersions
            WHERE (@baseWorkflowId IS NULL OR BaseWorkflowId = @baseWorkflowId)
              AND (@status IS NULL OR Status = @status)
              AND (@text IS NULL OR Title LIKE @textLike ESCAPE '\\' OR (Description IS NOT NULL AND Description LIKE @textLike ESCAPE '\\'))
              AND (@owner IS NULL OR OwnerName LIKE @ownerLike ESCAPE '\\' OR OwnerEmail LIKE @ownerLike ESCAPE '\\')
              AND (@workflowIdPrefix IS NULL OR WorkflowId LIKE @workflowIdPrefixLike ESCAPE '\\')
              {{tagPredicates}}
              {{securityPredicate}}
              AND (@after IS NULL OR CONCAT(BaseWorkflowId, LPAD(VersionNumber, 10, '0')) > @after)
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

        if (!query.Tags.IsEmpty)
        {
            List<string> tags = query.Tags.ToList();
            var predicates = new StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                predicates.Append("AND Tags LIKE ").Append(name).Append(" ESCAPE '\\\\'\n              ");
                select.Parameters.AddWithValue(name, "%\u001F" + EscapeLike(tags[i]) + "\u001F%");
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
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

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

        await using MySqlCommand update = connection.CreateCommand();
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
        update.Parameters.AddWithValue("@tags", (object?)tags.ToDelimitedOrNull('\u001F') ?? DBNull.Value);
        update.Parameters.AddWithValue("@ownerName", owner.Name);
        update.Parameters.AddWithValue("@ownerEmail", owner.Email);
        update.Parameters.AddWithValue("@ownerTeam", (object?)owner.Team ?? DBNull.Value);
        update.Parameters.AddWithValue("@ownerUrl", (object?)owner.Url ?? DBNull.Value);
        update.Parameters.AddWithValue("@lastUpdatedBy", patch.UpdatedBy);
        update.Parameters.AddWithValue("@lastUpdatedAt", now.ToUnixTimeMilliseconds());
        update.Parameters.AddWithValue("@obsoletedBy", (object?)obsoletedBy ?? DBNull.Value);
        update.Parameters.AddWithValue("@obsoletedAt", (object?)obsoletedAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        return await ReadOneAsync(connection, baseWorkflowId, versionNumber, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        delete.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        delete.Parameters.AddWithValue("@versionNumber", versionNumber);
        return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT BaseWorkflowId, VersionNumber, WorkflowId FROM CatalogVersions WHERE Status = @status;";
        select.Parameters.AddWithValue("@status", nameof(CatalogStatus.Obsolete));

        var refs = new List<CatalogVersionRef>();
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        foreach (CatalogVersionRef reference in versions)
        {
            await using MySqlCommand delete = connection.CreateCommand();
            delete.CommandText = "DELETE FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber; DELETE FROM CatalogVersionSecurityTags WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
            delete.Parameters.AddWithValue("@baseWorkflowId", reference.BaseWorkflowId);
            delete.Parameters.AddWithValue("@versionNumber", reference.VersionNumber);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask<CatalogVersion> AddCoreAsync(string baseWorkflowId, byte[] packageUtf8, CatalogMetadata metadata, CancellationToken cancellationToken)
    {
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        int versionNumber;
        await using (MySqlCommand max = connection.CreateCommand())
        {
            max.CommandText = "SELECT COALESCE(MAX(VersionNumber), 0) FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId;";
            max.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            versionNumber = Convert.ToInt32(await max.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false), CultureInfo.InvariantCulture) + 1;
        }

        CatalogPackageProjection projection = CatalogPackage.Project(packageUtf8, baseWorkflowId, versionNumber, this.metadataProvider, this.executorProvider);
        TagSet tags = metadata.Tags;
        SecurityTagSet securityTags = metadata.SecurityTags;

        // Bind the columns directly from the projected/governance source values (no round-trip through the
        // CatalogVersion document); the document is built once, for the return value.
        await using MySqlCommand insert = connection.CreateCommand();
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
        insert.Parameters.AddWithValue("@package", projection.CanonicalPackage.ToArray());
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Persist the version's security tags for indexed reach-filtering (§14.4); versions are immutable.
        if (!securityTags.IsEmpty)
        {
            // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
            foreach (SecurityTag tag in securityTags.ToList())
            {
                await using MySqlCommand tagInsert = connection.CreateCommand();
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Package FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        select.Parameters.AddWithValue("@versionNumber", versionNumber);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask<CatalogVersion?> ReadOneAsync(MySqlConnection connection, string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT {ColumnList} FROM CatalogVersions WHERE BaseWorkflowId = @baseWorkflowId AND VersionNumber = @versionNumber;";
        select.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        select.Parameters.AddWithValue("@versionNumber", versionNumber);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? ReadVersion(reader) : null;
    }

    private static CatalogVersion ReadVersion(MySqlDataReader reader)
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

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS CatalogVersions (
            BaseWorkflowId VARCHAR(255) NOT NULL,
            VersionNumber INT NOT NULL,
            WorkflowId VARCHAR(255) NOT NULL,
            Title VARCHAR(1024) NOT NULL,
            Description TEXT NULL,
            Status VARCHAR(64) NOT NULL,
            Tags TEXT NULL,
            OwnerName VARCHAR(512) NOT NULL,
            OwnerEmail VARCHAR(512) NOT NULL,
            OwnerTeam VARCHAR(512) NULL,
            OwnerUrl VARCHAR(1024) NULL,
            Sources TEXT NULL,
            Hash VARCHAR(64) NOT NULL,
            CreatedBy VARCHAR(255) NOT NULL,
            CreatedAt BIGINT NOT NULL,
            LastUpdatedBy VARCHAR(255) NULL,
            LastUpdatedAt BIGINT NULL,
            ObsoletedBy VARCHAR(255) NULL,
            ObsoletedAt BIGINT NULL,
            Runnable TINYINT(1) NOT NULL DEFAULT 0,
            SecurityTags TEXT NULL,
            Package LONGBLOB NOT NULL,
            PRIMARY KEY (BaseWorkflowId, VersionNumber),
            INDEX ix_catalog_versions_status (Status),
            INDEX ix_catalog_versions_workflow_id (WorkflowId)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS CatalogVersionSecurityTags (
            BaseWorkflowId VARCHAR(255) NOT NULL,
            VersionNumber INT NOT NULL,
            TagKey VARCHAR(255) NOT NULL,
            TagValue VARCHAR(255) NOT NULL,
            INDEX ix_catalog_version_security_tags_version (BaseWorkflowId, VersionNumber),
            INDEX ix_catalog_version_security_tags_kv (TagKey, TagValue)
        );
        """,
    ];
}