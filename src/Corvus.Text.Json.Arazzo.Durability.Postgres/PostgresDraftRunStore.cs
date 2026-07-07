// <copyright file="PostgresDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture holds the
/// audited <see cref="DraftRun"/> record as its JSON document in a <c>bytea</c> column (the runner registry's
/// registration-document idiom) and the packed document + sources as an opaque <c>bytea</c> blob (the catalog
/// store's package idiom), keyed by run id. It opens a pooled connection per operation, so it is naturally
/// concurrent.
/// </summary>
public sealed class PostgresDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS draft_run_captures (
            run_id TEXT PRIMARY KEY NOT NULL,
            record BYTEA NOT NULL,
            package BYTEA NOT NULL
        );
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresDraftRunStore(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the capture-store schema (table) from a connection string.</summary>
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

    /// <summary>Provisions the capture-store schema over a caller-supplied data source (the caller retains ownership).</summary>
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

    /// <summary>Opens the draft-run store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresDraftRunStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresDraftRunStore>(new PostgresDraftRunStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the draft-run store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<PostgresDraftRunStore> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresDraftRunStore>(new PostgresDraftRunStore(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            INSERT INTO draft_run_captures (run_id, record, package)
            VALUES (@runId, @record, @package)
            ON CONFLICT (run_id) DO UPDATE SET record = EXCLUDED.record, package = EXCLUDED.package;
            """;
        upsert.Parameters.AddWithValue("@runId", id.Value);

        // The record is bound as bytea directly from its serialized UTF-8 (the runner registry's doc idiom).
        upsert.Parameters.AddWithValue("@record", recordUtf8);

        // The (potentially large, ~KB) package is bound straight from its memory as a strongly-typed bytea
        // parameter — no GC copy of the whole package (the catalog store's package-blob idiom).
        upsert.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("@package", package));
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT record FROM draft_run_captures WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] record
            ? PersistedJson.ToPooledDocument<DraftRun>(record)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT package FROM draft_run_captures WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] package
            ? (ReadOnlyMemory<byte>?)package
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM draft_run_captures WHERE run_id = @runId;";
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