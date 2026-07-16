// <copyright file="PostgresDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug (<c>$draft</c>)
/// run's latest assembled metadata trace. Each row holds one run's trace as an opaque <c>bytea</c> blob keyed by
/// run id — exactly the package-blob idiom <see cref="PostgresDraftRunStore"/> persists beside its record. It opens
/// a pooled connection per operation, so it is naturally concurrent.
/// </summary>
/// <remarks>
/// The put binds the trace straight from its memory as a strongly-typed <c>bytea</c> parameter
/// (<see cref="NpgsqlParameter{T}"/> of <see cref="ReadOnlyMemory{Byte}"/>) — no GC copy of the trace, matching
/// <see cref="PostgresDraftRunStore"/>'s package bind.
/// </remarks>
public sealed class PostgresDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS draft_run_traces (
            run_id TEXT PRIMARY KEY NOT NULL,
            trace BYTEA NOT NULL
        );
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresDraftRunTraceStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the trace-store schema (table) from a connection string.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the trace-store schema over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the draft-run-trace store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresDraftRunTraceStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresDraftRunTraceStore>(new PostgresDraftRunTraceStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the draft-run-trace store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresDraftRunTraceStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresDraftRunTraceStore>(new PostgresDraftRunTraceStore(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO draft_run_traces (run_id, trace)
            VALUES (@runId, @trace)
            ON CONFLICT (run_id) DO UPDATE SET trace = EXCLUDED.trace;
            """;
        upsert.Parameters.AddWithValue("@runId", id.Value);

        // The trace is bound straight from its memory as a strongly-typed bytea parameter — no GC copy of the trace
        // (the package-blob idiom PostgresDraftRunStore uses).
        upsert.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("@trace", traceUtf8));
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT trace FROM draft_run_traces WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] trace
            ? (ReadOnlyMemory<byte>?)trace
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
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