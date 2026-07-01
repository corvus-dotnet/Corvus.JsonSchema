// <copyright file="PostgresWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Npgsql;
using NpgsqlTypes;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/> — the
/// relational default. The checkpoint is held as an opaque <c>bytea</c> blob alongside the projected index
/// columns (the store never parses it); optimistic concurrency maps to a version column and the single-owner
/// lease to a small leases table. It speaks the PostgreSQL wire protocol directly (Npgsql, no ORM, no
/// migrations runtime), so it also serves CockroachDB, YugabyteDB, AlloyDB, Aurora PostgreSQL, Neon and Citus.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class PostgresWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresWorkflowStateStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>
    /// Provisions the store's schema (tables and indexes). This performs DDL, so it requires a credential
    /// permitted to create tables; run it once at deploy/migration time, separately from the least-privileged
    /// credential used to <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/> the store for operation.
    /// </summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational credential granted only data
    /// access (select/insert/update/delete) on the tables. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand —
    /// with an elevated credential — to create the schema.
    /// </remarks>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresWorkflowStateStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowStateStore>(
            new PostgresWorkflowStateStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Provisions the store's schema over a caller-supplied data source.</summary>
    /// <remarks>
    /// Supply a data source the caller configured — for example one whose periodic password provider supplies
    /// Entra ID access tokens (managed identity) — so provisioning runs under a deliberate credential. The
    /// caller retains ownership of the data source.
    /// </remarks>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a data source the caller configured — for example with a least-privileged operational role, or a
    /// periodic password provider for managed-identity tokens — so the store runs under a least-privileged
    /// principal. This performs no DDL; call <see cref="PrepareAsync(NpgsqlDataSource, CancellationToken)"/>
    /// once beforehand.
    /// </remarks>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresWorkflowStateStore> ConnectAsync(
        NpgsqlDataSource dataSource,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowStateStore>(
            new PostgresWorkflowStateStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
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
        => this.SaveCoreAsync(id, checkpointUtf8, index, expected, cancellationToken);

    private async ValueTask<WorkflowEtag> SaveCoreAsync(WorkflowRunId id, ReadOnlyMemory<byte> checkpoint, WorkflowRunIndexEntry index, WorkflowEtag expected, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        if (expected.IsNone)
        {
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                """
                INSERT INTO workflow_runs (run_id, checkpoint, version, status, workflow_id, environment, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags)
                VALUES (@id, @checkpoint, 1, @status, @workflow_id, @environment, @created_at, @updated_at, @due_at, @awaiting_channel, @awaiting_correlation_id, @error_type, @correlation_id, @tags)
                ON CONFLICT (run_id) DO NOTHING;
                """;
            BindRun(insert, id, checkpoint, index);
            int inserted = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (inserted == 0)
            {
                throw new WorkflowConflictException(id, expected);
            }

            await SyncSecurityTagsAsync(connection, id, index.SecurityTags, cancellationToken).ConfigureAwait(false);
            return new WorkflowEtag("1");
        }

        long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE workflow_runs
            SET checkpoint = @checkpoint, version = version + 1, status = @status, workflow_id = @workflow_id,
                environment = @environment, created_at = @created_at, updated_at = @updated_at, due_at = @due_at,
                awaiting_channel = @awaiting_channel, awaiting_correlation_id = @awaiting_correlation_id, error_type = @error_type,
                correlation_id = @correlation_id, tags = @tags
            WHERE run_id = @id AND version = @expected_version;
            """;
        BindRun(update, id, checkpoint, index);
        update.Parameters.AddWithValue("expected_version", expectedVersion);
        int updated = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        if (updated == 0)
        {
            throw new WorkflowConflictException(id, expected);
        }

        await SyncSecurityTagsAsync(connection, id, index.SecurityTags, cancellationToken).ConfigureAwait(false);
        return new WorkflowEtag((expectedVersion + 1).ToString(CultureInfo.InvariantCulture));
    }

    private static async Task SyncSecurityTagsAsync(NpgsqlConnection connection, WorkflowRunId id, SecurityTagSet securityTags, CancellationToken cancellationToken)
    {
        await using (NpgsqlCommand delete = connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM workflow_run_security_tags WHERE run_id = @id;";
            delete.Parameters.AddWithValue("id", id.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (securityTags.IsEmpty)
        {
            return;
        }

        // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
        foreach (SecurityTag tag in securityTags.ToList())
        {
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO workflow_run_security_tags (run_id, tag_key, tag_value) VALUES (@id, @key, @value);";
            insert.Parameters.AddWithValue("id", id.Value);
            insert.Parameters.AddWithValue("key", tag.Key);
            insert.Parameters.AddWithValue("value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT checkpoint, version FROM workflow_runs WHERE run_id = @id;";
        select.Parameters.AddWithValue("id", id.Value);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO workflow_leases (run_id, owner, token, expires_at)
            VALUES (@id, @owner, @token, @expires_at)
            ON CONFLICT (run_id) DO UPDATE SET owner = excluded.owner, token = excluded.token, expires_at = excluded.expires_at
            WHERE workflow_leases.expires_at <= @now OR workflow_leases.owner = @owner;
            """;
        upsert.Parameters.AddWithValue("id", id.Value);
        upsert.Parameters.AddWithValue("owner", owner);
        upsert.Parameters.AddWithValue("token", token);
        upsert.Parameters.AddWithValue("expires_at", expiresAt.ToUnixTimeMilliseconds());
        upsert.Parameters.AddWithValue("now", now.ToUnixTimeMilliseconds());
        int affected = await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return affected > 0 ? new WorkflowLease(id, owner, token, expiresAt) : null;
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM workflow_leases WHERE run_id = @id AND token = @token;";
        delete.Parameters.AddWithValue("id", lease.RunId.Value);
        delete.Parameters.AddWithValue("token", lease.Token);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM workflow_runs WHERE run_id = @id; DELETE FROM workflow_leases WHERE run_id = @id; DELETE FROM workflow_run_security_tags WHERE run_id = @id;";
        delete.Parameters.AddWithValue("id", id.Value);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT run_id FROM workflow_runs WHERE status = @status AND due_at IS NOT NULL AND due_at <= @before;";
        select.Parameters.AddWithValue("status", SuspendedStatus);
        select.Parameters.AddWithValue("before", before.ToUnixTimeMilliseconds());
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT run_id FROM workflow_runs
            WHERE status = @status AND awaiting_channel = @channel
              AND (@correlation_id IS NULL OR awaiting_correlation_id IS NULL OR awaiting_correlation_id = @correlation_id);
            """;
        select.Parameters.AddWithValue("status", SuspendedStatus);
        select.Parameters.AddWithValue("channel", channel);
        select.Parameters.Add(NullableText("correlation_id", correlationId));
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, DateTimeOffset now, CancellationToken cancellationToken)
        => this.QueryClaimableAsync(hostedWorkflowIds, null, now, cancellationToken);

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryClaimableAsync(IReadOnlyCollection<string> hostedWorkflowIds, string? runnerEnvironment, DateTimeOffset now, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();

        // §5.5 environment-scoped dispatch: a run pinned to an environment is claimable only by a runner serving it; an
        // unpinned run (environment IS NULL) or an unscoped dispatcher (@runner_environment IS NULL) matches anything.
        select.CommandText =
            $"""
            SELECT r.run_id FROM workflow_runs r
            LEFT JOIN workflow_leases l ON l.run_id = r.run_id
            WHERE r.workflow_id IN ({string.Join(", ", placeholders)})
              AND (@runner_environment IS NULL OR r.environment IS NULL OR r.environment = @runner_environment)
              AND (r.status = @pending OR (r.status = @running AND (l.run_id IS NULL OR l.expires_at <= @now)));
            """;
        select.Parameters.AddWithValue("pending", PendingStatus);
        select.Parameters.AddWithValue("running", RunningStatus);
        select.Parameters.AddWithValue("now", now.ToUnixTimeMilliseconds());
        select.Parameters.Add(NullableText("runner_environment", runnerEnvironment));
        for (int i = 0; i < ids.Count; i++)
        {
            select.Parameters.AddWithValue("w" + i.ToString(CultureInfo.InvariantCulture), ids[i]);
        }

        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return new WorkflowRunId(reader.GetString(0));
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        // Decode the keyset cursor straight from the request UTF-8 (no managed token string); undefined = first page.
        string? after = null;
        if (query.ContinuationToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = query.ContinuationToken.GetUtf8String();
            after = WorkflowContinuationToken.Decode(tokenUtf8.Span);
        }

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT run_id, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type, correlation_id, tags, environment
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
            ORDER BY run_id
            LIMIT @limit;
            """;
        select.Parameters.Add(NullableText("status", query.Status?.ToString()));
        select.Parameters.Add(NullableText("workflow_id", query.WorkflowId));
        select.Parameters.Add(NullableBigint("created_after", query.CreatedAfter?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("created_before", query.CreatedBefore?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("updated_after", query.UpdatedAfter?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableBigint("updated_before", query.UpdatedBefore?.ToUnixTimeMilliseconds()));
        select.Parameters.Add(NullableText("correlation_id", query.CorrelationId));
        select.Parameters.Add(NullableText("after", after));
        select.Parameters.AddWithValue("limit", query.Limit + 1);

        if (!query.Tags.IsEmpty)
        {
            List<string> tags = query.Tags.ToList();
            var predicates = new System.Text.StringBuilder();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "tag" + i.ToString(CultureInfo.InvariantCulture);
                predicates.Append("AND tags LIKE @").Append(name).Append(" ESCAPE '\\'\n              ");
                select.Parameters.Add(NullableText(name, "%" + EscapeLike(tags[i]) + "%"));
            }

            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", predicates.ToString().TrimEnd());
        }
        else
        {
            select.CommandText = select.CommandText.Replace("{{tagPredicates}}", string.Empty);
        }

        // Row-security reach (§14.4): correlated EXISTS over the run's security tags (reached only for a store
        // declaring ISupportsRowSecurityFilter).
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
                    string name = "sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                    select.Parameters.AddWithValue(name, value);
                    return "@" + name;
                });
            select.CommandText = select.CommandText.Replace("{{securityPredicate}}", "AND (" + security.ToSqlPredicate(emitter) + ")");
        }
        else
        {
            select.CommandText = select.CommandText.Replace("{{securityPredicate}}", string.Empty);
        }

        var runs = new List<WorkflowRunListing>();
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
                Tags: TagSet.FromDelimited(reader.IsDBNull(10) ? null : reader.GetString(10), '\u001F'),
                Environment: reader.IsDBNull(11) ? null : reader.GetString(11));
            runs.Add(new WorkflowRunListing(new WorkflowRunId(reader.GetString(0)), entry));
        }

        return WorkflowContinuationToken.Paginate(runs, query.Limit);
    }

    private static void BindRun(NpgsqlCommand command, WorkflowRunId id, ReadOnlyMemory<byte> checkpoint, in WorkflowRunIndexEntry index)
    {
        command.Parameters.AddWithValue("id", id.Value);
        command.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("checkpoint", checkpoint));
        command.Parameters.AddWithValue("status", index.Status.ToString());
        command.Parameters.AddWithValue("workflow_id", index.WorkflowId);
        command.Parameters.Add(NullableText("environment", index.Environment));
        command.Parameters.AddWithValue("created_at", index.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("updated_at", index.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.Add(NullableBigint("due_at", index.DueAt?.ToUnixTimeMilliseconds()));
        command.Parameters.Add(NullableText("awaiting_channel", index.AwaitingChannel));
        command.Parameters.Add(NullableText("awaiting_correlation_id", index.AwaitingCorrelationId));
        command.Parameters.Add(NullableText("error_type", index.ErrorType));
        command.Parameters.Add(NullableText("correlation_id", index.CorrelationId));
        command.Parameters.Add(NullableText("tags", index.Tags.ToDelimitedOrNull('\u001F')));
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static NpgsqlParameter NullableText(string name, string? value)
        => new(name, NpgsqlDbType.Text) { Value = (object?)value ?? DBNull.Value };

    private static NpgsqlParameter NullableBigint(string name, long? value)
        => new(name, NpgsqlDbType.Bigint) { Value = (object?)value ?? DBNull.Value };

    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS workflow_runs (
            run_id TEXT PRIMARY KEY NOT NULL,
            checkpoint BYTEA NOT NULL,
            version BIGINT NOT NULL,
            status TEXT NOT NULL,
            workflow_id TEXT NOT NULL,
            environment TEXT NULL,
            created_at BIGINT NOT NULL,
            updated_at BIGINT NOT NULL,
            due_at BIGINT NULL,
            awaiting_channel TEXT NULL,
            awaiting_correlation_id TEXT NULL,
            error_type TEXT NULL,
            correlation_id TEXT NULL,
            tags TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_workflow_runs_due ON workflow_runs (status, due_at);
        CREATE INDEX IF NOT EXISTS ix_workflow_runs_awaiting ON workflow_runs (status, awaiting_channel, awaiting_correlation_id);
        CREATE TABLE IF NOT EXISTS workflow_run_security_tags (
            run_id TEXT NOT NULL,
            tag_key TEXT NOT NULL,
            tag_value TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_workflow_run_security_tags_run ON workflow_run_security_tags (run_id);
        CREATE INDEX IF NOT EXISTS ix_workflow_run_security_tags_kv ON workflow_run_security_tags (tag_key, tag_value);
        CREATE TABLE IF NOT EXISTS workflow_leases (
            run_id TEXT PRIMARY KEY NOT NULL,
            owner TEXT NOT NULL,
            token TEXT NOT NULL,
            expires_at BIGINT NOT NULL
        );
        """;
}