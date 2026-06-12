// <copyright file="SqliteRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IRunnerRegistry"/> — a single-file, zero-setup directory of live runners for
/// local-development and embedded single-node use. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>doc</c> blob keyed by runner id, alongside a queryable <c>last_seen_at</c> column
/// used for pruning.
/// </summary>
/// <remarks>
/// One connection is held open for the registry's lifetime and all operations are serialised through it — adequate
/// for the local/embedded use this adapter targets. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS runner_registrations (
            runner_id TEXT PRIMARY KEY NOT NULL,
            last_seen_at INTEGER NOT NULL,
            doc BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        """;

    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteRunnerRegistry(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Provisions the registry schema (table and index) against a file database.</summary>
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

    /// <summary>Opens a runner registry over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=runners.db</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised registry.</returns>
    public static async ValueTask<SqliteRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteRunnerRegistry(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand command = this.connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO runner_registrations (runner_id, last_seen_at, doc)
                VALUES (@runnerId, @lastSeenAt, @doc)
                ON CONFLICT(runner_id) DO UPDATE SET last_seen_at = excluded.last_seen_at, doc = excluded.doc;
                """;
            command.Parameters.AddWithValue("@runnerId", registration.RunnerIdValue);
            command.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            command.Parameters.AddWithValue("@doc", registration.ToJsonBytes());
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing;
            using (SqliteCommand select = this.connection.CreateCommand())
            {
                select.CommandText = "SELECT doc FROM runner_registrations WHERE runner_id = @runnerId;";
                select.Parameters.AddWithValue("@runnerId", runnerId);
                existing = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
            }

            if (existing is null)
            {
                return false;
            }

            RunnerRegistration updated = RunnerRegistration.FromJson(existing).WithLastSeenAt(at);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE runner_registrations SET last_seen_at = @lastSeenAt, doc = @doc WHERE runner_id = @runnerId;";
            update.Parameters.AddWithValue("@runnerId", runnerId);
            update.Parameters.AddWithValue("@lastSeenAt", at.ToUnixTimeMilliseconds());
            update.Parameters.AddWithValue("@doc", updated.ToJsonBytes());
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand command = this.connection.CreateCommand();
            command.CommandText = "SELECT doc FROM runner_registrations;";
            var result = new List<RunnerRegistration>();
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                result.Add(RunnerRegistration.FromJson(reader.GetFieldValue<byte[]>(0)));
            }

            return result;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand command = this.connection.CreateCommand();
            command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
            command.Parameters.AddWithValue("@cutoff", deadBefore.ToUnixTimeMilliseconds());
            return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Disposes the connection held open for the registry's lifetime.</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }
}