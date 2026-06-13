// <copyright file="SqlServerWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. The checkpoint
/// is held as an opaque <c>VARBINARY(MAX)</c> blob alongside the projected index columns (the store never
/// parses it); optimistic concurrency maps to a version column and the single-owner lease to a small leases
/// table (a race-safe <c>MERGE</c>). It uses Microsoft.Data.SqlClient directly (no ORM, no migrations
/// runtime), so the same code covers SQL Server, Azure SQL Database and Azure SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateKeyViolation = 2601;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerWorkflowStateStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Provisions the store's schema (tables and indexes). This performs DDL, so it requires a login
    /// permitted to create tables; run it once at deploy/migration time, separately from the least-privileged
    /// login used to <see cref="ConnectAsync"/> the store for operation.
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

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data
    /// access on the tables. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to
    /// create the schema. The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerWorkflowStateStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerWorkflowStateStore>(new SqlServerWorkflowStateStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (expected.IsNone)
        {
            await using SqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO workflow_runs (run_id, [checkpoint], version, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags)
                VALUES (@id, @checkpoint, 1, @status, @workflow_id, @created_at, @updated_at, @due_at, @awaiting_channel, @awaiting_correlation_id, @error_type, @correlation_id, @tags);
                """;
            BindRun(insert, id, checkpoint, index);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqlException ex) when (ex.Number is UniqueConstraintViolation or DuplicateKeyViolation)
            {
                throw new WorkflowConflictException(id, expected);
            }

            await SyncSecurityTagsAsync(connection, id, index.SecurityTags, cancellationToken).ConfigureAwait(false);
            return new WorkflowEtag("1");
        }

        long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE workflow_runs
            SET [checkpoint] = @checkpoint, version = version + 1, status = @status, workflow_id = @workflow_id,
                created_at = @created_at, updated_at = @updated_at, due_at = @due_at,
                awaiting_channel = @awaiting_channel, awaiting_correlation_id = @awaiting_correlation_id, error_type = @error_type,
                correlation_id = @correlation_id, tags = @tags
            WHERE run_id = @id AND version = @expected_version;
            """;
        BindRun(update, id, checkpoint, index);
        update.Parameters.AddWithValue("@expected_version", expectedVersion);
        int updated = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (updated == 0)
        {
            throw new WorkflowConflictException(id, expected);
        }

        await SyncSecurityTagsAsync(connection, id, index.SecurityTags, cancellationToken).ConfigureAwait(false);
        return new WorkflowEtag((expectedVersion + 1).ToString(CultureInfo.InvariantCulture));
    }

    private static async Task SyncSecurityTagsAsync(SqlConnection connection, WorkflowRunId id, IReadOnlyList<SecurityTag>? securityTags, CancellationToken cancellationToken)
    {
        await using (SqlCommand delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM workflow_run_security_tags WHERE run_id = @id;";
            delete.Parameters.AddWithValue("@id", id.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (securityTags is not { Count: > 0 })
        {
            return;
        }

        foreach (SecurityTag tag in securityTags)
        {
            await using SqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO workflow_run_security_tags (run_id, tag_key, tag_value) VALUES (@id, @key, @value);";
            insert.Parameters.AddWithValue("@id", id.Value);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT [checkpoint], version FROM workflow_runs WHERE run_id = @id;";
        select.Parameters.AddWithValue("@id", id.Value);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        byte[] checkpoint = await reader.GetFieldValueAsync<byte[]>(0, cancellationToken).ConfigureAwait(false);
        var etag = new WorkflowEtag(reader.GetInt64(1).ToString(CultureInfo.InvariantCulture));
        return new WorkflowCheckpoint(checkpoint, etag);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand merge = connection.CreateCommand();
        merge.CommandText =
            """
            MERGE workflow_leases WITH (HOLDLOCK) AS target
            USING (SELECT @id AS run_id) AS source ON target.run_id = source.run_id
            WHEN MATCHED AND (target.expires_at <= @now OR target.owner = @owner) THEN
                UPDATE SET owner = @owner, token = @token, expires_at = @expires_at
            WHEN NOT MATCHED THEN
                INSERT (run_id, owner, token, expires_at) VALUES (@id, @owner, @token, @expires_at);
            """;
        merge.Parameters.AddWithValue("@id", id.Value);
        merge.Parameters.AddWithValue("@owner", owner);
        merge.Parameters.AddWithValue("@token", token);
        merge.Parameters.AddWithValue("@expires_at", expiresAt.ToUnixTimeMilliseconds());
        merge.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
        int affected = await merge.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0 ? new WorkflowLease(id, owner, token, expiresAt) : null;
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM workflow_leases WHERE run_id = @id AND token = @token;";
        delete.Parameters.AddWithValue("@id", lease.RunId.Value);
        delete.Parameters.AddWithValue("@token", lease.Token);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM workflow_runs WHERE run_id = @id; DELETE FROM workflow_leases WHERE run_id = @id; DELETE FROM workflow_run_security_tags WHERE run_id = @id;";
        delete.Parameters.AddWithValue("@id", id.Value);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT run_id FROM workflow_runs WHERE status = @status AND due_at IS NOT NULL AND due_at <= @before;";
        select.Parameters.AddWithValue("@status", SuspendedStatus);
        select.Parameters.AddWithValue("@before", before.ToUnixTimeMilliseconds());
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT run_id FROM workflow_runs
            WHERE status = @status AND awaiting_channel = @channel
              AND (@correlation_id IS NULL OR awaiting_correlation_id IS NULL OR awaiting_correlation_id = @correlation_id);
            """;
        select.Parameters.AddWithValue("@status", SuspendedStatus);
        select.Parameters.AddWithValue("@channel", channel);
        select.Parameters.Add(NullableText("@correlation_id", correlationId));
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(hostedWorkflowIds);
        if (hostedWorkflowIds.Count == 0)
        {
            yield break;
        }

        var ids = new List<string>(hostedWorkflowIds);
        var placeholders = new string[ids.Count];
        for (int i = 0; i < ids.Count; i++)
        {
            placeholders[i] = "@w" + i.ToString(CultureInfo.InvariantCulture);
        }

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            $"""
            SELECT r.run_id FROM workflow_runs r
            LEFT JOIN workflow_leases l ON l.run_id = r.run_id
            WHERE r.workflow_id IN ({string.Join(", ", placeholders)})
              AND (r.status = @pending OR (r.status = @running AND (l.run_id IS NULL OR l.expires_at <= @now)));
            """;
        select.Parameters.AddWithValue("@pending", PendingStatus);
        select.Parameters.AddWithValue("@running", RunningStatus);
        select.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
        for (int i = 0; i < ids.Count; i++)
        {
            select.Parameters.AddWithValue(placeholders[i], ids[i]);
        }

        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT TOP (@limit) run_id, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags
            FROM workflow_runs
            WHERE (@status IS NULL OR status = @status) AND (@workflow_id IS NULL OR workflow_id = @workflow_id)
              AND (@created_after IS NULL OR created_at >= @created_after)
              AND (@created_before IS NULL OR created_at < @created_before)
              AND (@updated_after IS NULL OR updated_at >= @updated_after)
              AND (@updated_before IS NULL OR updated_at < @updated_before)
              AND (@correlation_id IS NULL OR correlation_id = @correlation_id)
              {{tagPredicates}}
              {{securityPredicate}}
              AND (@after IS NULL OR run_id > @after)
            ORDER BY run_id;
            """;
        select.Parameters.Add(NullableText("@status", query.Status?.ToString()));
        select.Parameters.Add(NullableText("@workflow_id", query.WorkflowId));
        select.Parameters.Add(NullableBigint("@created_after", query.CreatedAfter?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("@created_before", query.CreatedBefore?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("@updated_after", query.UpdatedAfter?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("@updated_before", query.UpdatedBefore?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableText("@correlation_id", query.CorrelationId));
        select.Parameters.Add(NullableText("@after", after));
        select.Parameters.AddWithValue("@limit", query.Limit + 1);

        if (query.Tags is { Count: > 0 } tags)
        {
            var predicates = new System.Text.StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                predicates.Append("AND tags LIKE ").Append(name).Append(" ESCAPE '\\'\n              ");
                select.Parameters.Add(NullableText(name, "%" + EscapeLike(tags[i]) + "%"));
            }

            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", predicates.ToString().TrimEnd());
        }
        else
        {
            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", string.Empty);
        }

        // Row-security reach (§14.4): correlated EXISTS over the run's security tags.
        if (query.Security is { } security)
        {
            int securityParam = 0;
            var emitter = new SqlSecurityRuleEmitter(
                "workflow_run_security_tags",
                ["run_id"],
                "tag_key",
                "tag_value",
                "workflow_runs",
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

        var runs = new List<WorkflowRunListing>();
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var entry = new WorkflowRunIndexEntry(
                reader.GetString(2),
                Enum.Parse<WorkflowRunStatus>(reader.GetString(1)),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(3)),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4)),
                reader.IsDBNull(5) ? null : DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5)),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                CorrelationId: reader.IsDBNull(9) ? null : reader.GetString(9),
                Tags: DecodeTags(reader.IsDBNull(10) ? null : reader.GetString(10)));
            runs.Add(new WorkflowRunListing(new WorkflowRunId(reader.GetString(0)), entry));
        }

        return WorkflowContinuationToken.Paginate(runs, query.Limit);
    }

    private static void BindRun(SqlCommand command, WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        command.Parameters.AddWithValue("@id", id.Value);
        command.Parameters.AddWithValue("@checkpoint", checkpoint);
        command.Parameters.AddWithValue("@status", index.Status.ToString());
        command.Parameters.AddWithValue("@workflow_id", index.WorkflowId);
        command.Parameters.AddWithValue("@created_at", index.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@updated_at", index.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.Add(NullableBigint("@due_at", index.DueAt?.ToUnixTimeMilliseconds()));
        command.Parameters.Add(NullableText("@awaiting_channel", index.AwaitingChannel));
        command.Parameters.Add(NullableText("@awaiting_correlation_id", index.AwaitingCorrelationId));
        command.Parameters.Add(NullableText("@error_type", index.ErrorType));
        command.Parameters.Add(NullableText("@correlation_id", index.CorrelationId));
        command.Parameters.Add(NullableText("@tags", EncodeTags(index.Tags)));
    }

    private static string? EncodeTags(IReadOnlyList<string>? tags)
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

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static SqlParameter NullableText(string name, string? value)
        => new(name, SqlDbType.NVarChar) { Value = (object?)value ?? DBNull.Value };

    private static SqlParameter NullableBigint(string name, long? value)
        => new(name, SqlDbType.BigInt) { Value = (object?)value ?? DBNull.Value };

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
        IF OBJECT_ID(N'workflow_runs', N'U') IS NULL
        BEGIN
            CREATE TABLE workflow_runs (
                run_id NVARCHAR(450) NOT NULL PRIMARY KEY,
                [checkpoint] VARBINARY(MAX) NOT NULL,
                version BIGINT NOT NULL,
                status NVARCHAR(255) NOT NULL,
                workflow_id NVARCHAR(255) NOT NULL,
                created_at BIGINT NOT NULL,
                updated_at BIGINT NOT NULL,
                due_at BIGINT NULL,
                awaiting_channel NVARCHAR(255) NULL,
                awaiting_correlation_id NVARCHAR(255) NULL,
                error_type NVARCHAR(1024) NULL,
                correlation_id NVARCHAR(MAX) NULL,
                tags NVARCHAR(MAX) NULL
            );
            CREATE INDEX ix_workflow_runs_due ON workflow_runs (status, due_at);
            CREATE INDEX ix_workflow_runs_awaiting ON workflow_runs (status, awaiting_channel, awaiting_correlation_id);
        END;
        IF OBJECT_ID(N'workflow_run_security_tags', N'U') IS NULL
        BEGIN
            CREATE TABLE workflow_run_security_tags (
                run_id NVARCHAR(450) NOT NULL,
                tag_key NVARCHAR(255) NOT NULL,
                tag_value NVARCHAR(255) NOT NULL
            );
            CREATE INDEX ix_workflow_run_security_tags_run ON workflow_run_security_tags (run_id);
            CREATE INDEX ix_workflow_run_security_tags_kv ON workflow_run_security_tags (tag_key, tag_value);
        END;
        IF OBJECT_ID(N'workflow_leases', N'U') IS NULL
        BEGIN
            CREATE TABLE workflow_leases (
                run_id NVARCHAR(450) NOT NULL PRIMARY KEY,
                owner NVARCHAR(255) NOT NULL,
                token NVARCHAR(255) NOT NULL,
                expires_at BIGINT NOT NULL
            );
        END;
        """;
}