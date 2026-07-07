// <copyright file="SqliteDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IDraftRunTraceStore"/> — a single-file, zero-setup trace store for §18 debug
/// (<c>$draft</c>) runs in local-development and embedded single-node use. Each row holds one run's latest
/// assembled metadata trace as an opaque BLOB, keyed by run id — exactly the package-blob shape
/// <see cref="SqliteDraftRunStore"/> persists beside its record.
/// </summary>
/// <remarks>
/// One connection is held open for the store's lifetime and all operations are serialised through it — adequate
/// for the local/embedded use this adapter targets. SQLite binds a <see cref="byte"/> array parameter natively, so
/// the put binds the trace as <c>byte[]</c> — its sibling bar, matching <see cref="SqliteDraftRunStore"/>; the
/// network backends stream it copy-free instead. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS draft_run_traces (
            run_id TEXT PRIMARY KEY NOT NULL,
            trace BLOB NOT NULL
        );
        """;

    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteDraftRunTraceStore(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Provisions the trace-store schema against a file database.</summary>
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

    /// <summary>Opens a draft-run-trace store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=drafts.db</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteDraftRunTraceStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteDraftRunTraceStore(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand upsert = this.connection.CreateCommand();
            upsert.CommandText =
                """
                INSERT INTO draft_run_traces (run_id, trace)
                VALUES (@runId, @trace)
                ON CONFLICT(run_id) DO UPDATE SET trace = excluded.trace;
                """;
            upsert.Parameters.AddWithValue("@runId", id.Value);
            upsert.Parameters.AddWithValue("@trace", traceUtf8.ToArray());
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT trace FROM draft_run_traces WHERE run_id = @runId;";
            select.Parameters.AddWithValue("@runId", id.Value);
            return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] trace
                ? (ReadOnlyMemory<byte>?)trace
                : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM draft_run_traces WHERE run_id = @runId;";
            delete.Parameters.AddWithValue("@runId", id.Value);
            return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Disposes the connection held open for the store's lifetime.</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }
}