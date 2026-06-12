// <copyright file="MySqlRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>LONGBLOB</c> column keyed by runner id, alongside a queryable <c>last_seen_at</c>
/// column used for pruning. It speaks the MySQL wire protocol directly (MySqlConnector, no ORM), so it also
/// serves MariaDB and Aurora MySQL. It opens a pooled connection per operation, so it is naturally concurrent.
/// </summary>
public sealed class MySqlRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS runner_registrations (
            runner_id VARCHAR(255) NOT NULL,
            last_seen_at BIGINT NOT NULL,
            doc LONGBLOB NOT NULL,
            PRIMARY KEY (runner_id),
            INDEX ix_runner_registrations_last_seen (last_seen_at)
        );
        CREATE TABLE IF NOT EXISTS runner_hosted_versions (
            runner_id VARCHAR(255) NOT NULL,
            base_workflow_id VARCHAR(255) NOT NULL,
            version_number INT NOT NULL,
            PRIMARY KEY (runner_id, base_workflow_id, version_number),
            INDEX ix_runner_hosted_versions_version (base_workflow_id, version_number),
            CONSTRAINT fk_runner_hosted FOREIGN KEY (runner_id) REFERENCES runner_registrations (runner_id) ON DELETE CASCADE
        );
        """;

    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private MySqlRunnerRegistry(MySqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>
    /// Provisions the registry schema (table and index). This performs DDL, so it requires a user permitted to
    /// create tables; run it once at deploy/migration time, separately from the least-privileged user used to
    /// <see cref="ConnectAsync(string, CancellationToken)"/> the registry for operation.
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

    /// <summary>Provisions the registry schema over a caller-supplied data source (the caller retains ownership).</summary>
    /// <remarks>
    /// Supply a data source the caller configured — for example one whose credential provider supplies Entra
    /// ID/IAM tokens — so provisioning runs under a deliberate credential.
    /// </remarks>
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

    /// <summary>Opens the registry for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational user granted only data access
    /// on the table. Call <see cref="PrepareAsync(string, CancellationToken)"/> once beforehand — with an
    /// elevated user — to create the schema.
    /// </remarks>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<MySqlRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlRunnerRegistry>(new MySqlRunnerRegistry(new MySqlDataSource(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlRunnerRegistry> ConnectAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlRunnerRegistry>(new MySqlRunnerRegistry(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (MySqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                INSERT INTO runner_registrations (runner_id, last_seen_at, doc)
                VALUES (@runnerId, @lastSeenAt, @doc)
                ON DUPLICATE KEY UPDATE last_seen_at = VALUES(last_seen_at), doc = VALUES(doc);
                """;
            upsert.Parameters.AddWithValue("@runnerId", runnerId);
            upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            upsert.Parameters.AddWithValue("@doc", registration.ToJsonBytes());
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
        await using (MySqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
            clear.Parameters.AddWithValue("@runnerId", runnerId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await using MySqlCommand insert = connection.CreateCommand();
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
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT EXISTS(SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber);";
        command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        command.Parameters.AddWithValue("@versionNumber", versionNumber);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false)) != 0;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        byte[]? existing;
        await using (MySqlCommand select = connection.CreateCommand())
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
        await using MySqlCommand update = connection.CreateCommand();
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
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT doc FROM runner_registrations;";
        var result = new List<RunnerRegistration>();
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
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