// <copyright file="SqliteDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IDraftRunStore"/> — a single-file, zero-setup capture store for §18 draft runs in
/// local-development and embedded single-node use. Each capture holds the audited <see cref="DraftRun"/> record
/// as its JSON document and the packed document + sources as an opaque blob, keyed by run id — exactly the
/// record-verbatim + package-blob split the catalog store uses.
/// </summary>
/// <remarks>
/// One connection is held open for the store's lifetime and all operations are serialised through it — adequate
/// for the local/embedded use this adapter targets. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS draft_run_captures (
            run_id TEXT PRIMARY KEY NOT NULL,
            record BLOB NOT NULL,
            package BLOB NOT NULL
        );
        """;

    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteDraftRunStore(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Provisions the capture-store schema against a file database.</summary>
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

    /// <summary>Opens a draft-run store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=drafts.db</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteDraftRunStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteDraftRunStore(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand upsert = this.connection.CreateCommand();
            upsert.CommandText =
                """
                INSERT INTO draft_run_captures (run_id, record, package)
                VALUES (@runId, @record, @package)
                ON CONFLICT(run_id) DO UPDATE SET record = excluded.record, package = excluded.package;
                """;
            upsert.Parameters.AddWithValue("@runId", id.Value);
            upsert.Parameters.AddWithValue("@record", recordUtf8);
            upsert.Parameters.AddWithValue("@package", package.ToArray());
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT record FROM draft_run_captures WHERE run_id = @runId;";
            select.Parameters.AddWithValue("@runId", id.Value);
            return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] record
                ? PersistedJson.ToPooledDocument<DraftRun>(record)
                : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT package FROM draft_run_captures WHERE run_id = @runId;";
            select.Parameters.AddWithValue("@runId", id.Value);
            return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] package
                ? (ReadOnlyMemory<byte>?)package
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
            delete.CommandText = "DELETE FROM draft_run_captures WHERE run_id = @runId;";
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