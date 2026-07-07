// <copyright file="MySqlDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug (<c>$draft</c>) run's
/// latest assembled metadata trace. Each row holds one run's trace as an opaque <c>LONGBLOB</c> blob keyed by run
/// id — exactly the package-blob idiom <see cref="MySqlDraftRunStore"/> persists beside its record. It speaks the
/// MySQL wire protocol directly (MySqlConnector, no ORM), so it also serves MariaDB and Aurora MySQL. It opens a
/// pooled connection per operation, so it is naturally concurrent.
/// </summary>
/// <remarks>
/// The put binds the trace straight from its memory as a <c>LONGBLOB</c> parameter (MySqlConnector binds
/// <see cref="ReadOnlyMemory{Byte}"/> directly) — no GC copy of the trace, matching
/// <see cref="MySqlDraftRunStore"/>'s package bind.
/// </remarks>
public sealed class MySqlDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS draft_run_traces (
            run_id VARCHAR(255) NOT NULL,
            trace LONGBLOB NOT NULL,
            PRIMARY KEY (run_id)
        );
        """;

    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private MySqlDraftRunTraceStore(MySqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>
    /// Provisions the trace-store schema (table). This performs DDL, so it requires a user permitted to create
    /// tables; run it once at deploy/migration time, separately from the least-privileged user used to
    /// <see cref="ConnectAsync(string, CancellationToken)"/> the store for operation.
    /// </summary>
    /// <param name="connectionString">A MySqlConnector connection string for a user permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the trace-store schema over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source whose user is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using MySqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the draft-run-trace store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<MySqlDraftRunTraceStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlDraftRunTraceStore>(new MySqlDraftRunTraceStore(new MySqlDataSource(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the draft-run-trace store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlDraftRunTraceStore> ConnectAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlDraftRunTraceStore>(new MySqlDraftRunTraceStore(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO draft_run_traces (run_id, trace)
            VALUES (@runId, @trace)
            ON DUPLICATE KEY UPDATE trace = VALUES(trace);
            """;
        upsert.Parameters.AddWithValue("@runId", id.Value);

        // The trace is bound straight from its memory as a LONGBLOB parameter — no GC copy of the trace
        // (the package-blob idiom MySqlDraftRunStore uses; MySqlConnector binds ReadOnlyMemory).
        upsert.Parameters.AddWithValue("@trace", traceUtf8);
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT trace FROM draft_run_traces WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] trace
            ? (ReadOnlyMemory<byte>?)trace
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM draft_run_traces WHERE run_id = @runId;";
        delete.Parameters.AddWithValue("@runId", id.Value);
        return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
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
}