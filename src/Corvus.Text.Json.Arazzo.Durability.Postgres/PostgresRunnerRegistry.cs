// <copyright file="PostgresRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>bytea</c> column keyed by runner id, alongside a queryable <c>last_seen_at</c> column
/// used for pruning. It opens a pooled connection per operation, so it is naturally concurrent.
/// </summary>
public sealed class PostgresRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS runner_registrations (
            runner_id TEXT PRIMARY KEY NOT NULL,
            last_seen_at BIGINT NOT NULL,
            doc BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        CREATE TABLE IF NOT EXISTS runner_hosted_versions (
            runner_id TEXT NOT NULL REFERENCES runner_registrations (runner_id) ON DELETE CASCADE,
            base_workflow_id TEXT NOT NULL,
            version_number INTEGER NOT NULL,
            PRIMARY KEY (runner_id, base_workflow_id, version_number)
        );
        CREATE INDEX IF NOT EXISTS ix_runner_hosted_versions_version ON runner_hosted_versions (base_workflow_id, version_number);
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresRunnerRegistry(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the registry schema (table and index) from a connection string.</summary>
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

    /// <summary>Provisions the registry schema over a caller-supplied data source (the caller retains ownership).</summary>
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

    /// <summary>Opens the registry for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<PostgresRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresRunnerRegistry>(new PostgresRunnerRegistry(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<PostgresRunnerRegistry> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresRunnerRegistry>(new PostgresRunnerRegistry(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (NpgsqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO runner_registrations (runner_id, last_seen_at, doc)
                VALUES (@runnerId, @lastSeenAt, @doc)
                ON CONFLICT (runner_id) DO UPDATE SET last_seen_at = EXCLUDED.last_seen_at, doc = EXCLUDED.doc;
                """;
            upsert.Parameters.AddWithValue("@runnerId", runnerId);
            upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            upsert.Parameters.AddWithValue("@doc", registration.ToJsonBytes());
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
        await using (NpgsqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
            clear.Parameters.AddWithValue("@runnerId", runnerId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO runner_hosted_versions (runner_id, base_workflow_id, version_number) VALUES (@runnerId, @baseWorkflowId, @versionNumber);";
            insert.Parameters.AddWithValue("@runnerId", runnerId);
            insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            insert.Parameters.AddWithValue("@versionNumber", versionNumber);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber);";
        command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        command.Parameters.AddWithValue("@versionNumber", versionNumber);
        return await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is true;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        byte[]? existing;
        await using (NpgsqlCommand select = connection.CreateCommand())
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
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE runner_registrations SET last_seen_at = @lastSeenAt, doc = @doc WHERE runner_id = @runnerId;";
        update.Parameters.AddWithValue("@runnerId", runnerId);
        update.Parameters.AddWithValue("@lastSeenAt", at.ToUnixTimeMilliseconds());
        update.Parameters.AddWithValue("@doc", updated.ToJsonBytes());
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT doc FROM runner_registrations;";
        var result = new List<RunnerRegistration>();
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
        command.Parameters.AddWithValue("@cutoff", deadBefore.ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the data source if this registry created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }
}