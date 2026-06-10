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
/// adapter targets. Create instances with <see cref="CreateAsync"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex, IAsyncDisposable
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);

    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowStateStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Opens a store over the given connection string and ensures its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=workflows.db</c>).</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowStateStore> CreateAsync(
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
                    INSERT INTO WorkflowRuns (RunId, Checkpoint, Version, Status, WorkflowId, CreatedAt, UpdatedAt, DueAt, AwaitingChannel, AwaitingCorrelationId, ErrorType)
                    VALUES (@id, @checkpoint, 1, @status, @workflowId, @createdAt, @updatedAt, @dueAt, @awaitingChannel, @awaitingCorrelationId, @errorType)
                    ON CONFLICT(RunId) DO NOTHING;
                    """;
                BindRun(insert, id, checkpoint, indexCopy);
                int inserted = await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                if (inserted == 0)
                {
                    throw new WorkflowConflictException(id, expected);
                }

                return new WorkflowEtag("1");
            }

            long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText =
                """
                UPDATE WorkflowRuns
                SET Checkpoint = @checkpoint, Version = Version + 1, Status = @status, WorkflowId = @workflowId,
                    CreatedAt = @createdAt, UpdatedAt = @updatedAt, DueAt = @dueAt,
                    AwaitingChannel = @awaitingChannel, AwaitingCorrelationId = @awaitingCorrelationId, ErrorType = @errorType
                WHERE RunId = @id AND Version = @expectedVersion;
                """;
            BindRun(update, id, checkpoint, indexCopy);
            update.Parameters.AddWithValue("@expectedVersion", expectedVersion);
            int updated = await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            if (updated == 0)
            {
                throw new WorkflowConflictException(id, expected);
            }

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
    public async ValueTask DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM WorkflowRuns WHERE RunId = @id; DELETE FROM WorkflowLeases WHERE RunId = @id;";
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
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                """
                SELECT RunId, Status, WorkflowId, CreatedAt, UpdatedAt, DueAt, AwaitingChannel, AwaitingCorrelationId, ErrorType
                FROM WorkflowRuns
                WHERE (@status IS NULL OR Status = @status) AND (@workflowId IS NULL OR WorkflowId = @workflowId)
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@status", (object?)query.Status?.ToString() ?? DBNull.Value);
            select.Parameters.AddWithValue("@workflowId", (object?)query.WorkflowId ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", query.Limit);

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
                    reader.IsDBNull(8) ? null : reader.GetString(8));
                runs.Add(new WorkflowRunListing(new WorkflowRunId(reader.GetString(0)), entry));
            }

            return new WorkflowRunPage(runs);
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

    private static void BindRun(SqliteCommand command, WorkflowRunId id, byte[] checkpoint, in WorkflowRunIndexEntry index)
    {
        command.Parameters.AddWithValue("@id", id.Value);
        command.Parameters.AddWithValue("@checkpoint", checkpoint);
        command.Parameters.AddWithValue("@status", index.Status.ToString());
        command.Parameters.AddWithValue("@workflowId", index.WorkflowId);
        command.Parameters.AddWithValue("@createdAt", index.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@updatedAt", index.UpdatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue("@dueAt", (object?)index.DueAt?.ToUnixTimeMilliseconds() ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaitingChannel", (object?)index.AwaitingChannel ?? DBNull.Value);
        command.Parameters.AddWithValue("@awaitingCorrelationId", (object?)index.AwaitingCorrelationId ?? DBNull.Value);
        command.Parameters.AddWithValue("@errorType", (object?)index.ErrorType ?? DBNull.Value);
    }

    private static DateTimeOffset FromUnixMilliseconds(long milliseconds) => DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);

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
            CreatedAt INTEGER NOT NULL,
            UpdatedAt INTEGER NOT NULL,
            DueAt INTEGER NULL,
            AwaitingChannel TEXT NULL,
            AwaitingCorrelationId TEXT NULL,
            ErrorType TEXT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkflowRuns_Due ON WorkflowRuns (Status, DueAt);
        CREATE INDEX IF NOT EXISTS IX_WorkflowRuns_Awaiting ON WorkflowRuns (Status, AwaitingChannel, AwaitingCorrelationId);
        CREATE TABLE IF NOT EXISTS WorkflowLeases (
            RunId TEXT PRIMARY KEY NOT NULL,
            Owner TEXT NOT NULL,
            Token TEXT NOT NULL,
            ExpiresAt INTEGER NOT NULL
        );
        """;
}