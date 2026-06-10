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
/// <see cref="CreateAsync"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqlServerWorkflowStateStore : IWorkflowStateStore, IWorkflowWaitIndex
{
    private const string SuspendedStatus = nameof(WorkflowRunStatus.Suspended);
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateKeyViolation = 2601;

    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerWorkflowStateStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>Opens a store over the given connection string and ensures its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for lease expiry; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqlServerWorkflowStateStore> CreateAsync(
        string connectionString,
        TimeProvider? timeProvider = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var store = new SqlServerWorkflowStateStore(connectionString, timeProvider ?? TimeProvider.System);
        await using SqlConnection connection = await store.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return store;
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
                INSERT INTO workflow_runs (run_id, [checkpoint], version, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type)
                VALUES (@id, @checkpoint, 1, @status, @workflow_id, @created_at, @updated_at, @due_at, @awaiting_channel, @awaiting_correlation_id, @error_type);
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

            return new WorkflowEtag("1");
        }

        long expectedVersion = long.Parse(expected.Value!, CultureInfo.InvariantCulture);
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText =
            """
            UPDATE workflow_runs
            SET [checkpoint] = @checkpoint, version = version + 1, status = @status, workflow_id = @workflow_id,
                created_at = @created_at, updated_at = @updated_at, due_at = @due_at,
                awaiting_channel = @awaiting_channel, awaiting_correlation_id = @awaiting_correlation_id, error_type = @error_type
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
        delete.CommandText = "DELETE FROM workflow_runs WHERE run_id = @id; DELETE FROM workflow_leases WHERE run_id = @id;";
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
    public async ValueTask<WorkflowRunPage> QueryAsync(WorkflowQuery query, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            """
            SELECT TOP (@limit) run_id, status, workflow_id, created_at, updated_at, due_at, awaiting_channel, awaiting_correlation_id, error_type
            FROM workflow_runs
            WHERE (@status IS NULL OR status = @status) AND (@workflow_id IS NULL OR workflow_id = @workflow_id);
            """;
        select.Parameters.Add(NullableText("@status", query.Status?.ToString()));
        select.Parameters.Add(NullableText("@workflow_id", query.WorkflowId));
        select.Parameters.AddWithValue("@limit", query.Limit);

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
                reader.IsDBNull(8) ? null : reader.GetString(8));
            runs.Add(new WorkflowRunListing(new WorkflowRunId(reader.GetString(0)), entry));
        }

        return new WorkflowRunPage(runs);
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
    }

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
                error_type NVARCHAR(1024) NULL
            );
            CREATE INDEX ix_workflow_runs_due ON workflow_runs (status, due_at);
            CREATE INDEX ix_workflow_runs_awaiting ON workflow_runs (status, awaiting_channel, awaiting_correlation_id);
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