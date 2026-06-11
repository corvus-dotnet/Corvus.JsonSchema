// <copyright file="MySqlWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/>. The checkpoint is
/// held as an opaque <c>LONGBLOB</c> alongside the projected index columns (the store never parses it);
/// optimistic concurrency maps to a version column and the single-owner lease to a small leases table. It
/// speaks the MySQL wire protocol directly (MySqlConnector, no ORM, no migrations runtime), so it also serves
/// MariaDB and Aurora MySQL.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class MySqlWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);

    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlWorkflowStateStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Provisions the store's schema (tables and indexes). This performs DDL, so it requires a user
    /// permitted to create tables; run it once at deploy/migration time, separately from the least-privileged
    /// user used to <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the store for operation.
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
    /// This performs no DDL, so it is safe to use a least-privileged operational user granted only data
    /// access on the tables. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with an elevated user — to
    /// create the schema.
    /// </remarks>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<MySqlWorkflowStateStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();

        // Optimistic-concurrency and lease grants are detected from rows-affected, so the store needs the
        // server's "changed rows" semantics (not the default "found rows"): an ON DUPLICATE KEY UPDATE that
        // changes nothing must report 0 affected.
        var builder = new MySqlConnectionStringBuilder(connectionString) { UseAffectedRows = true };
        return new ValueTask<MySqlWorkflowStateStore>(
            new MySqlWorkflowStateStore(new MySqlDataSource(builder.ConnectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Provisions the store's schema over a caller-supplied data source.</summary>
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
    /// the store runs under a least-privileged principal. The data source <strong>must</strong> have
    /// <c>UseAffectedRows=true</c> (required for correct optimistic-concurrency and lease semantics); this is
    /// validated. Performs no DDL — call <see cref="PrepareAsync(MySqlDataSource, CancellationToken)"/> first.
    /// </remarks>
    /// <param name="dataSource">A MySqlConnector data source configured with <c>UseAffectedRows=true</c>.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlWorkflowStateStore> ConnectAsync(
        MySqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();

        if (!new MySqlConnectionStringBuilder(dataSource.ConnectionString).UseAffectedRows)
        {
            throw new ArgumentException(
                "The MySqlDataSource must be configured with UseAffectedRows=true for correct optimistic-concurrency and lease semantics.",
                nameof(dataSource));
        }

        return new ValueTask<MySqlWorkflowStateStore>(
            new MySqlWorkflowStateStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
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
    public ValueTask<WorkflowEtag> SaveAsync(
        WorkflowRunId id,
        ReadOnlyMemory<byte> checkpointUtf8,
        in WorkflowRunIndexEntry index,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
        => this.SaveCoreAsync(id, checkpointUtf8.ToArray(), index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, byte[] checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (expected.IsNone)
        {
            await using MySqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO workflow_runs (run_id, checkpoint, version, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags)
                VALUES (@id, @checkpoint, 1, @status, @workflow_id, @created_at, @updated_at, @due_at, @awaiting_channel, @awaiting_correlation_id, @error_type, @correlation_id, @tags)
                ON DUPLICATE KEY UPDATE run_id = run_id;
                """;
            BindRun(insert, id, checkpoint, index);
            int inserted = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (inserted == 0)
            {
                throw new WorkflowConflictException(id, expected);
            }

            return new WorkflowEtag("1");
        }

        long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
        await using MySqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE workflow_runs
            SET checkpoint = @checkpoint, version = version + 1, status = @status, workflow_id = @workflow_id,
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

        return new WorkflowEtag((expectedVersion + 1).ToString(CultureInfo.InvariantCulture));
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT checkpoint, version FROM workflow_runs WHERE run_id = @id;";
        select.Parameters.AddWithValue("@id", id.Value);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO workflow_leases (run_id, owner, token, expires_at)
            VALUES (@id, @owner, @token, @expires_at) AS new
            ON DUPLICATE KEY UPDATE
                owner = IF(workflow_leases.expires_at <= @now OR workflow_leases.owner = @owner, new.owner, workflow_leases.owner),
                token = IF(workflow_leases.expires_at <= @now OR workflow_leases.owner = @owner, new.token, workflow_leases.token),
                expires_at = IF(workflow_leases.expires_at <= @now OR workflow_leases.owner = @owner, new.expires_at, workflow_leases.expires_at);
            """;
        upsert.Parameters.AddWithValue("@id", id.Value);
        upsert.Parameters.AddWithValue("@owner", owner);
        upsert.Parameters.AddWithValue("@token", token);
        upsert.Parameters.AddWithValue("@expires_at", expiresAt.ToUnixTimeMilliseconds());
        upsert.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
        int affected = await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0 ? new WorkflowLease(id, owner, token, expiresAt) : null;
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM workflow_leases WHERE run_id = @id AND token = @token;";
        delete.Parameters.AddWithValue("@id", lease.RunId.Value);
        delete.Parameters.AddWithValue("@token", lease.Token);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand deleteRun = connection.CreateCommand();
        deleteRun.CommandText = "DELETE FROM workflow_runs WHERE run_id = @id;";
        deleteRun.Parameters.AddWithValue("@id", id.Value);
        await deleteRun.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        await using MySqlCommand deleteLease = connection.CreateCommand();
        deleteLease.CommandText = "DELETE FROM workflow_leases WHERE run_id = @id;";
        deleteLease.Parameters.AddWithValue("@id", id.Value);
        await deleteLease.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT run_id FROM workflow_runs WHERE status = @status AND due_at IS NOT NULL AND due_at <= @before;";
        select.Parameters.AddWithValue("@status", SuspendedStatus);
        select.Parameters.AddWithValue("@before", before.ToUnixTimeMilliseconds());
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT run_id FROM workflow_runs
            WHERE status = @status AND awaiting_channel = @channel
              AND (@correlation_id IS NULL OR awaiting_correlation_id IS NULL OR awaiting_correlation_id = @correlation_id);
            """;
        select.Parameters.AddWithValue("@status", SuspendedStatus);
        select.Parameters.AddWithValue("@channel", channel);
        select.Parameters.AddWithValue("@correlation_id", (object?)correlationId ?? DBNull.Value);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        string? after = WorkflowContinuationToken.Decode(query.ContinuationToken);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT run_id, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags
            FROM workflow_runs
            WHERE (@status IS NULL OR status = @status) AND (@workflow_id IS NULL OR workflow_id = @workflow_id)
              AND (@created_after IS NULL OR created_at >= @created_after)
              AND (@created_before IS NULL OR created_at < @created_before)
              AND (@updated_after IS NULL OR updated_at >= @updated_after)
              AND (@updated_before IS NULL OR updated_at < @updated_before)
              AND (@correlation_id IS NULL OR correlation_id = @correlation_id)
              {{tagPredicates}}
              AND (@after IS NULL OR run_id > @after)
            ORDER BY run_id
            LIMIT @limit;
            """;
        select.Parameters.AddWithValue("@status", (object?)query.Status?.ToString() ?? DBNull.Value);
        select.Parameters.AddWithValue("@workflow_id", (object?)query.WorkflowId ?? DBNull.Value);
        select.Parameters.AddWithValue("@created_after", (object?)query.CreatedAfter?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        select.Parameters.AddWithValue("@created_before", (object?)query.CreatedBefore?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        select.Parameters.AddWithValue("@updated_after", (object?)query.UpdatedAfter?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        select.Parameters.AddWithValue("@updated_before", (object?)query.UpdatedBefore?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        select.Parameters.AddWithValue("@correlation_id", (object?)query.CorrelationId ?? DBNull.Value);
        select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
        select.Parameters.AddWithValue("@limit", query.Limit + 1);

        if (query.Tags is { Count: > 0 } tags)
        {
            var predicates = new System.Text.StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                predicates.Append("AND tags LIKE ").Append(name).Append(" ESCAPE '\\\\'\n              ");
                select.Parameters.AddWithValue(name, "%" + EscapeLike(tags[i]) + "%");
            }

            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", predicates.ToString().TrimEnd());
        }
        else
        {
            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", string.Empty);
        }

        var runs = new List<WorkflowRunListing>();
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    private static void BindRun(MySqlCommand command, WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        command.Parameters.AddWithValue("@id", id.Value);
        command.Parameters.AddWithValue("@checkpoint", checkpoint);
        command.Parameters.AddWithValue("@status", index.Status.ToString());
        command.Parameters.AddWithValue("@workflow_id", index.WorkflowId);
        command.Parameters.AddWithValue("@created_at", index.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@updated_at", index.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@due_at", (object?)index.DueAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaiting_channel", (object?)index.AwaitingChannel ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaiting_correlation_id", (object?)index.AwaitingCorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@error_type", (object?)index.ErrorType ?? DBNull.Value);
        command.Parameters.AddWithValue("@correlation_id", (object?)index.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@tags", (object?)EncodeTags(index.Tags) ?? DBNull.Value);
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

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private static readonly string[] SchemaStatements =
    [
        """
        CREATE TABLE IF NOT EXISTS workflow_runs (
            run_id VARCHAR(255) PRIMARY KEY NOT NULL,
            checkpoint LONGBLOB NOT NULL,
            version BIGINT NOT NULL,
            status VARCHAR(64) NOT NULL,
            workflow_id VARCHAR(255) NOT NULL,
            created_at BIGINT NOT NULL,
            updated_at BIGINT NOT NULL,
            due_at BIGINT NULL,
            awaiting_channel VARCHAR(255) NULL,
            awaiting_correlation_id VARCHAR(255) NULL,
            error_type VARCHAR(1024) NULL,
            correlation_id VARCHAR(512) NULL,
            tags TEXT NULL,
            INDEX ix_workflow_runs_due (status, due_at),
            INDEX ix_workflow_runs_awaiting (status, awaiting_channel, awaiting_correlation_id)
        );
        """,
        """
        CREATE TABLE IF NOT EXISTS workflow_leases (
            run_id VARCHAR(255) PRIMARY KEY NOT NULL,
            owner VARCHAR(255) NOT NULL,
            token VARCHAR(255) NOT NULL,
            expires_at BIGINT NOT NULL
        );
        """,
    ];
}