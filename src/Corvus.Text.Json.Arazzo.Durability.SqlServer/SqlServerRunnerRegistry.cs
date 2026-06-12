// <copyright file="SqlServerRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IRunnerRegistry"/>. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>VARBINARY(MAX)</c> column keyed by runner id, alongside a queryable <c>last_seen_at</c>
/// column used for pruning. It uses Microsoft.Data.SqlClient directly (no ORM, no migrations runtime), so the
/// same code covers SQL Server, Azure SQL Database and Azure SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the registry is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        IF OBJECT_ID(N'runner_registrations', N'U') IS NULL
        BEGIN
            CREATE TABLE runner_registrations (
                runner_id NVARCHAR(450) NOT NULL,
                last_seen_at BIGINT NOT NULL,
                doc VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_runner_registrations PRIMARY KEY (runner_id)
            );
            CREATE INDEX IX_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        END;
        IF OBJECT_ID(N'runner_hosted_versions', N'U') IS NULL
        BEGIN
            CREATE TABLE runner_hosted_versions (
                runner_id NVARCHAR(450) NOT NULL,
                base_workflow_id NVARCHAR(450) NOT NULL,
                version_number INT NOT NULL,
                CONSTRAINT PK_runner_hosted PRIMARY KEY (runner_id, base_workflow_id, version_number),
                CONSTRAINT FK_runner_hosted FOREIGN KEY (runner_id) REFERENCES runner_registrations (runner_id) ON DELETE CASCADE
            );
            CREATE INDEX IX_runner_hosted_versions_version ON runner_hosted_versions (base_workflow_id, version_number);
        END;
        """;

    private readonly string connectionString;

    private SqlServerRunnerRegistry(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Provisions the registry schema (table and index). This performs DDL, so it requires a login permitted
    /// to create tables; run it once at deploy/migration time, separately from the least-privileged login used
    /// to <see cref="ConnectAsync"/> the registry for operation.
    /// </summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string for a login permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the registry for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data
    /// access on the table. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to
    /// create the schema. The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<SqlServerRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerRunnerRegistry>(new SqlServerRunnerRegistry(connectionString));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        await using (SqlCommand upsert = connection.CreateCommand())
        {
            upsert.Transaction = transaction;
            upsert.CommandText =
                """
                MERGE runner_registrations AS target
                USING (SELECT @runnerId AS runner_id) AS source
                ON target.runner_id = source.runner_id
                WHEN MATCHED THEN
                    UPDATE SET last_seen_at = @lastSeenAt, doc = @doc
                WHEN NOT MATCHED THEN
                    INSERT (runner_id, last_seen_at, doc) VALUES (@runnerId, @lastSeenAt, @doc);
                """;
            upsert.Parameters.AddWithValue("@runnerId", runnerId);
            upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
            upsert.Parameters.AddWithValue("@doc", registration.ToJsonBytes());
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
        await using (SqlCommand clear = connection.CreateCommand())
        {
            clear.Transaction = transaction;
            clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
            clear.Parameters.AddWithValue("@runnerId", runnerId);
            await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
        {
            await using SqlCommand insert = connection.CreateCommand();
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT CASE WHEN EXISTS (SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber) THEN 1 ELSE 0 END;";
        command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
        command.Parameters.AddWithValue("@versionNumber", versionNumber);
        return (int)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! == 1;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        byte[]? existing;
        await using (SqlCommand select = connection.CreateCommand())
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
        await using SqlCommand update = connection.CreateCommand();
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "SELECT doc FROM runner_registrations;";
        var result = new List<RunnerRegistration>();
        await using SqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            result.Add(RunnerRegistration.FromJson((byte[])reader[0]));
        }

        return result;
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand command = connection.CreateCommand();
        command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
        command.Parameters.AddWithValue("@cutoff", deadBefore.ToUnixTimeMilliseconds());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the registry. This implementation holds no per-instance resources, so it is a no-op.</summary>
    /// <returns>A completed task.</returns>
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

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
}