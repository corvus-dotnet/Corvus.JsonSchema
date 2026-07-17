// <copyright file="SqliteWorkflowStateStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IWorkflowStateStore"/> and <see cref="IWorkflowWaitIndex"/> — a single-file,
/// zero-setup durable store for local-development and embedded single-node runs. The checkpoint is held as
/// an opaque blob alongside the projected index columns (the store never parses it); optimistic concurrency
/// maps to a version column and the single-owner lease to a small leases table.
/// </summary>
/// <remarks>
/// One connection is held open for the store's lifetime (so an in-memory database survives between
/// operations) and all operations are serialised through it — adequate for the local/embedded use this
/// adapter targets. Create instances with <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IWorkflowDispatchIndex, IWorkflowLeaseAdministration, ISupportsRowSecurityFilter, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const string PendingStatus = nameof(WorkflowRunStatus.Pending);
    private const string RunningStatus = nameof(WorkflowRunStatus.Running);
    private const string FaultedStatus = nameof(WorkflowRunStatus.Faulted);

    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowStateStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the store's schema (tables and indexes) against a file database.</summary>
    /// <remarks>
    /// SQLite is the deliberate exception to the prepare/connect split: it is an embedded, single-process
    /// engine with no server-side privilege boundary (access is governed by file-system permissions), and an
    /// in-memory database exists only for the lifetime of its connection — so <see cref="ConnectAsync"/> also
    /// ensures the schema. This method is offered for symmetry, to pre-create a file database's schema.
    /// </remarks>
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

    /// <summary>Opens a store over the given connection string, ensuring its schema exists.</summary>
    /// <remarks>
    /// Unlike the out-of-process backends, this ensures the schema on open (see <see cref="PrepareAsync"/> for
    /// why): SQLite has no privilege boundary to separate, and an in-memory database must be provisioned on
    /// the held connection.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=workflows.db</c>).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowStateStore> ConnectAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
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
            return new SqliteWorkflowStateStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
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

    // The interface passes the index by `in`; an async method cannot take an `in` parameter, so SaveAsync
    // copies it (a small struct) and this private core does the work.
    private async ValueTask<WorkflowEtag> SaveCoreAsync(
        WorkflowRunId id,
        byte[] checkpoint,
        WorkflowRunIndexEntry indexCopy,
        WorkflowEtag expected,
        CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (expected.IsNone)
            {
                using SqliteCommand insert = this.connection.CreateCommand();
                insert.CommandText =
                    """
                    INSERT INTO WorkflowRuns (RunId, Checkpoint, Version, Status, WorkflowId, Environment, CreatedAt, UpdatedAt, DueAt, AwaitingChannel, AwaitingCorrelationId, ErrorType, CorrelationId, Tags, ResumeRequestedAt)
                    VALUES (@id, @checkpoint, 1, @status, @workflowId, @environment, @createdAt, @updatedAt, @dueAt, @awaitingChannel, @awaitingCorrelationId, @errorType, @correlationId, @tags, @resumeRequestedAt)
                    ON CONFLICT(RunId) DO NOTHING;
                    """;
                BindRun(insert, id, checkpoint, indexCopy);
                int inserted = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (inserted == 0)
                {
                    throw new WorkflowConflictException(id, expected);
                }

                await this.SyncSecurityTagsAsync(id, indexCopy.SecurityTags, cancellationToken).ConfigureAwait(false);
                return new WorkflowEtag("1");
            }

            long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText =
                """
                UPDATE WorkflowRuns
                SET Checkpoint = @checkpoint, Version = Version + 1, Status = @status, WorkflowId = @workflowId,
                    Environment = @environment, CreatedAt = @createdAt, UpdatedAt = @updatedAt, DueAt = @dueAt,
                    AwaitingChannel = @awaitingChannel, AwaitingCorrelationId = @awaitingCorrelationId, ErrorType = @errorType,
                    CorrelationId = @correlationId, Tags = @tags, ResumeRequestedAt = @resumeRequestedAt
                WHERE RunId = @id AND Version = @expectedVersion;
                """;
            BindRun(update, id, checkpoint, indexCopy);
            update.Parameters.AddWithValue("@expectedVersion", expectedVersion);
            int updated = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (updated == 0)
            {
                throw new WorkflowConflictException(id, expected);
            }

            await this.SyncSecurityTagsAsync(id, indexCopy.SecurityTags, cancellationToken).ConfigureAwait(false);
            return new WorkflowEtag((expectedVersion + 1).ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowCheckpoint?> LoadAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Checkpoint, Version FROM WorkflowRuns WHERE RunId = @id;";
            select.Parameters.AddWithValue("@id", id.Value);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                return null;
            }

            byte[] checkpoint = (byte[])reader.GetValue(0);
            var etag = new WorkflowEtag(reader.GetInt64(1).ToString(CultureInfo.InvariantCulture));
            return new WorkflowCheckpoint(checkpoint, etag);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowLease?> AcquireLeaseAsync(WorkflowRunId id, string owner, TimeSpan ttl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(owner);

        DateTimeOffset now = this.timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + ttl;
        string token = Guid.NewGuid().ToString("N");

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand upsert = this.connection.CreateCommand();
            upsert.CommandText =
                """
                INSERT INTO WorkflowLeases (RunId, Owner, Token, ExpiresAt)
                VALUES (@id, @owner, @token, @expiresAt)
                ON CONFLICT(RunId) DO UPDATE SET Owner = excluded.Owner, Token = excluded.Token, ExpiresAt = excluded.ExpiresAt
                WHERE WorkflowLeases.ExpiresAt <= @now OR WorkflowLeases.Owner = @owner;
                """;
            upsert.Parameters.AddWithValue("@id", id.Value);
            upsert.Parameters.AddWithValue("@owner", owner);
            upsert.Parameters.AddWithValue("@token", token);
            upsert.Parameters.AddWithValue("@expiresAt", expiresAt.ToUnixTimeMilliseconds());
            upsert.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
            int affected = await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return affected > 0 ? new WorkflowLease(id, owner, token, expiresAt) : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask ReleaseLeaseAsync(WorkflowLease lease, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM WorkflowLeases WHERE RunId = @id AND Token = @token;";
            delete.Parameters.AddWithValue("@id", lease.RunId.Value);
            delete.Parameters.AddWithValue("@token", lease.Token);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> ExpireLeasesForOwnerAsync(string owner, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(owner);

        // The control-plane revocation fence (§5.5): expire every live lease this owner holds in place, so an authorized peer
        // reclaims its in-flight runs at the next poll (WorkflowLeases.ExpiresAt <= now makes the row re-acquirable) rather
        // than after the TTL. The affected count is the number of live leases fenced.
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand expire = this.connection.CreateCommand();
            expire.CommandText = "UPDATE WorkflowLeases SET ExpiresAt = @now WHERE Owner = @owner AND ExpiresAt > @now;";
            expire.Parameters.AddWithValue("@owner", owner);
            expire.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
            return await expire.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM WorkflowRuns WHERE RunId = @id; DELETE FROM WorkflowLeases WHERE RunId = @id; DELETE FROM WorkflowRunSecurityTags WHERE RunId = @id;";
            delete.Parameters.AddWithValue("@id", id.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryDueAsync(DateTimeOffset before, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<WorkflowRunId> due = await this.QueryIdsAsync(
            "SELECT RunId FROM WorkflowRuns WHERE Status = @status AND DueAt IS NOT NULL AND DueAt <= @before;",
            cmd =>
            {
                cmd.Parameters.AddWithValue("@status", SuspendedStatus);
                cmd.Parameters.AddWithValue("@before", before.ToUnixTimeMilliseconds());
            },
            cancellationToken).ConfigureAwait(false);

        foreach (WorkflowRunId id in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return id;
        }
    }

    /// <inheritdoc/>
    public async IAsyncEnumerable<WorkflowRunId> QueryAwaitingAsync(string channel, string? correlationId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(channel);

        List<WorkflowRunId> awaiting = await this.QueryIdsAsync(
            """
            SELECT RunId FROM WorkflowRuns
            WHERE Status = @status AND AwaitingChannel = @channel
              AND (@correlationId IS NULL OR AwaitingCorrelationId IS NULL OR AwaitingCorrelationId = @correlationId);
            """,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@status", SuspendedStatus);
                cmd.Parameters.AddWithValue("@channel", channel);
                cmd.Parameters.AddWithValue("@correlationId", (object?)correlationId ?? DBNull.Value);
            },
            cancellationToken).ConfigureAwait(false);

        foreach (WorkflowRunId id in awaiting)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return id;
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            string filter = BuildVisibilityFilter(select, query);
            select.CommandText =
                $"""
                SELECT RunId, Status, WorkflowId, CreatedAt, UpdatedAt, DueAt, AwaitingChannel, AwaitingCorrelationId, ErrorType, CorrelationId, Tags, Environment
                FROM WorkflowRuns
                WHERE {filter}
                  AND (@after IS NULL OR RunId > @after)
                ORDER BY RunId
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", query.Limit + 1);

            var runs = new List<WorkflowRunListing>();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                var entry = new WorkflowRunIndexEntry(
                    reader.GetString(2),
                    Enum.Parse<WorkflowRunStatus>(reader.GetString(1)),
                    FromUnixMilliseconds(reader.GetInt64(3)),
                    FromUnixMilliseconds(reader.GetInt64(4)),
                    reader.IsDBNull(5) ? null : FromUnixMilliseconds(reader.GetInt64(5)),
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
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowQuery query, int cap, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            string filter = BuildVisibilityFilter(select, query);

            // Bounded native count: COUNT over a cap+1-limited sub-select — reuses the list's exact filter (so the
            // §14.4 reach can never drift), never materialises rows, and stops the moment the cap is exceeded.
            select.CommandText = $"SELECT COUNT(*) FROM (SELECT 1 FROM WorkflowRuns WHERE {filter} LIMIT @cap);";
            select.Parameters.AddWithValue("@cap", cap + 1);
            long total = (long)(await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return total > cap ? (cap, true) : ((int)total, false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    // Builds the shared visibility WHERE body (status / workflow / draft-exclusion / timestamps / correlation / tags /
    // §14.4 security reach) and binds its parameters onto <paramref name="command"/>, returning the predicate SQL
    // WITHOUT the "WHERE" keyword, the keyset cursor, ORDER BY, or LIMIT. QueryAsync appends the cursor + paging;
    // CountAsync wraps it in a bounded COUNT — both share this exact predicate so the reach filter cannot drift.
    private static string BuildVisibilityFilter(SqliteCommand command, in WorkflowQuery query)
    {
        var sql = new System.Text.StringBuilder();
        sql.Append("(@status IS NULL OR Status = @status)");
        sql.Append(" AND (@workflowId IS NULL OR WorkflowId = @workflowId)");
        sql.Append(" AND (@workflowId IS NOT NULL OR WorkflowId <> @draftId)");
        sql.Append(" AND (@createdAfter IS NULL OR CreatedAt >= @createdAfter)");
        sql.Append(" AND (@createdBefore IS NULL OR CreatedAt < @createdBefore)");
        sql.Append(" AND (@updatedAfter IS NULL OR UpdatedAt >= @updatedAfter)");
        sql.Append(" AND (@updatedBefore IS NULL OR UpdatedAt < @updatedBefore)");
        sql.Append(" AND (@correlationId IS NULL OR CorrelationId = @correlationId)");

        command.Parameters.AddWithValue("@status", (object?)query.Status?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("@workflowId", (object?)query.WorkflowId ?? DBNull.Value);

        // §18: draft runs never surface on an unfiltered visibility query — a caller must name the reserved
        // $draft workflow id explicitly (the debug-run surface does; the runs listing never does).
        command.Parameters.AddWithValue("@draftId", DraftRuns.RunWorkflowId);
        command.Parameters.AddWithValue("@createdAfter", (object?)query.CreatedAfter?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdBefore", (object?)query.CreatedBefore?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@updatedAfter", (object?)query.UpdatedAfter?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@updatedBefore", (object?)query.UpdatedBefore?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@correlationId", (object?)query.CorrelationId ?? DBNull.Value);

        if (!query.Tags.IsEmpty)
        {
            // The needle (per query, not per row) is materialized to strings only here, at the ADO LIKE-parameter
            // boundary the driver forces; the row tags themselves are never materialized — the LIKE matches the bytes.
            List<string> tags = query.Tags.ToList();
            for (int i = 0; i < tags.Count; i++)
            {
                string name = "@tag" + i.ToString(CultureInfo.InvariantCulture);
                sql.Append(" AND Tags LIKE ").Append(name).Append(" ESCAPE '\\'");
                command.Parameters.AddWithValue(name, "%" + EscapeLike(tags[i]) + "%");
            }
        }

        // Row-security reach (§14.4): translate the filter to a correlated EXISTS predicate over the run's security
        // tags. The client only reaches here for a store that declares ISupportsRowSecurityFilter.
        if (query.Security is { } security)
        {
            int securityParam = 0;
            var emitter = new SqlSecurityRuleEmitter(
                "WorkflowRunSecurityTags",
                ["RunId"],
                "TagKey",
                "TagValue",
                "WorkflowRuns",
                value =>
                {
                    string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                    command.Parameters.AddWithValue(name, value);
                    return name;
                });
            sql.Append(" AND (").Append(security.ToSqlPredicate(emitter)).Append(')');
        }

        return sql.ToString();
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }

    private static void BindRun(SqliteCommand command, WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        command.Parameters.AddWithValue("@id", id.Value);
        command.Parameters.AddWithValue("@checkpoint", checkpoint);
        command.Parameters.AddWithValue("@status", index.Status.ToString());
        command.Parameters.AddWithValue("@workflowId", index.WorkflowId);
        command.Parameters.AddWithValue("@environment", (object?)index.Environment ?? DBNull.Value);
        command.Parameters.AddWithValue("@createdAt", index.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@updatedAt", index.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@dueAt", (object?)index.DueAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaitingChannel", (object?)index.AwaitingChannel ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaitingCorrelationId", (object?)index.AwaitingCorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@errorType", (object?)index.ErrorType ?? DBNull.Value);
        command.Parameters.AddWithValue("@correlationId", (object?)index.CorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@resumeRequestedAt", (object?)index.ResumeRequestedAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@tags", (object?)index.Tags.ToDelimitedOrNull('\u001F') ?? DBNull.Value);
    }

    // Re-syncs a run's security tags (§14.4) into the child table for indexed reach-filtering. Called under the
    // gate after the run row is written; run security tags are immutable, but a full delete+insert is simplest
    // and the tag count is tiny.
    private async Task SyncSecurityTagsAsync(WorkflowRunId id, SecurityTagSet securityTags, CancellationToken cancellationToken)
    {
        using (SqliteCommand delete = this.connection.CreateCommand())
        {
            delete.CommandText = "DELETE FROM WorkflowRunSecurityTags WHERE RunId = @id;";
            delete.Parameters.AddWithValue("@id", id.Value);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (securityTags.IsEmpty)
        {
            return;
        }

        // Materialize at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
        foreach (SecurityTag tag in securityTags.ToList())
        {
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO WorkflowRunSecurityTags (RunId, TagKey, TagValue) VALUES (@id, @key, @value);";
            insert.Parameters.AddWithValue("@id", id.Value);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static DateTimeOffset FromUnixMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

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

        // §5.5 environment-scoped dispatch: a real runner (non-null @runnerEnvironment) claims a run only when pinned to
        // EXACTLY its environment — the equality excludes an unpinned run (Environment IS NULL, since NULL = value is
        // never true) and a differently-pinned run. A null @runnerEnvironment is the env-agnostic base overload (list all
        // claimable), never a runner — the WorkflowDispatcher rejects an unscoped runner, so dispatch is always strict.
        // §18: a paused (or faulted) run the control plane marked resume-claimable (ResumeRequestedAt IS NOT NULL) also
        // surfaces here, so a separate runner can claim and advance it; the marker is cleared on its first checkpoint.
        string sql =
            $"""
            SELECT r.RunId FROM WorkflowRuns r
            LEFT JOIN WorkflowLeases l ON l.RunId = r.RunId
            WHERE r.WorkflowId IN ({string.Join(", ", placeholders)})
              AND (@runnerEnvironment IS NULL OR r.Environment = @runnerEnvironment)
              AND (r.Status = @pending
                   OR (r.Status = @running AND (l.RunId IS NULL OR l.ExpiresAt <= @now))
                   OR (r.ResumeRequestedAt IS NOT NULL AND r.Status IN (@suspended, @faulted)));
            """;

        List<WorkflowRunId> claimable = await this.QueryIdsAsync(
            sql,
            cmd =>
            {
                cmd.Parameters.AddWithValue("@pending", PendingStatus);
                cmd.Parameters.AddWithValue("@running", RunningStatus);
                cmd.Parameters.AddWithValue("@suspended", SuspendedStatus);
                cmd.Parameters.AddWithValue("@faulted", FaultedStatus);
                cmd.Parameters.AddWithValue("@now", now.ToUnixTimeMilliseconds());
                cmd.Parameters.AddWithValue("@runnerEnvironment", (object?)runnerEnvironment ?? DBNull.Value);
                for (int i = 0; i < ids.Count; i++)
                {
                    cmd.Parameters.AddWithValue(placeholders[i], ids[i]);
                }
            },
            cancellationToken).ConfigureAwait(false);

        foreach (WorkflowRunId id in claimable)
        {
            cancellationToken.ThrowIfCancellationRequested();
            yield return id;
        }
    }

    private async ValueTask<List<WorkflowRunId>> QueryIdsAsync(string sql, Action<SqliteCommand> bind, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = sql;
            bind(select);

            var ids = new List<WorkflowRunId>();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                ids.Add(new WorkflowRunId(reader.GetString(0)));
            }

            return ids;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkflowRuns (
            RunId TEXT PRIMARY KEY NOT NULL,
            Checkpoint BLOB NOT NULL,
            Version INTEGER NOT NULL,
            Status TEXT NOT NULL,
            WorkflowId TEXT NOT NULL,
            Environment TEXT NULL,
            CreatedAt INTEGER NOT NULL,
            UpdatedAt INTEGER NOT NULL,
            DueAt INTEGER NULL,
            AwaitingChannel TEXT NULL,
            AwaitingCorrelationId TEXT NULL,
            ErrorType TEXT NULL,
            CorrelationId TEXT NULL,
            Tags TEXT NULL,
            ResumeRequestedAt INTEGER NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkflowRuns_Due ON WorkflowRuns (Status, DueAt);
        CREATE INDEX IF NOT EXISTS IX_WorkflowRuns_Awaiting ON WorkflowRuns (Status, AwaitingChannel, AwaitingCorrelationId);
        CREATE TABLE IF NOT EXISTS WorkflowRunSecurityTags (
            RunId TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkflowRunSecurityTags_Run ON WorkflowRunSecurityTags (RunId);
        CREATE INDEX IF NOT EXISTS IX_WorkflowRunSecurityTags_KeyValue ON WorkflowRunSecurityTags (TagKey, TagValue);
        CREATE TABLE IF NOT EXISTS WorkflowLeases (
            RunId TEXT PRIMARY KEY NOT NULL,
            Owner TEXT NOT NULL,
            Token TEXT NOT NULL,
            ExpiresAt INTEGER NOT NULL
        );
        """;
}