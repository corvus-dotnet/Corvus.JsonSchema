// <copyright file="SqliteScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IScheduleRegistry"/>: one row per <c>scheduleId</c> whose primary-key insert
/// conflict enforces the schedules contract's deployment-global uniqueness. Under the composite
/// <c>(Environment, RunId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of that
/// uniqueness — a run-key collision can no longer observe a schedule created in another environment — so a
/// SQLite deployment whose run store outlives the process needs this durable registry, not the in-memory one,
/// or every control-plane restart forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// One connection is held open for the registry's lifetime (so an in-memory database survives between
/// operations) and all operations are serialised through it, matching the SQLite state store's posture.
/// Create instances with <see cref="ConnectAsync(string, CancellationToken)"/>, which runs the idempotent
/// schema (SQLite is the deliberate exception to the prepare/connect split; <see cref="PrepareAsync(string, CancellationToken)"/>
/// is offered for symmetry, to pre-create a file database's schema).
/// </remarks>
public sealed class SqliteScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS ScheduleRegistrations (
            ScheduleId TEXT NOT NULL PRIMARY KEY,
            Environment TEXT NOT NULL,
            RunId TEXT NOT NULL
        );
        """;

    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteScheduleRegistry(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Provisions the registry schema (the table) against a file database.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a registry over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=schedules.db</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised registry.</returns>
    public static async ValueTask<SqliteScheduleRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteScheduleRegistry(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The primary-key insert conflict IS the uniqueness gate; ON CONFLICT DO NOTHING makes the refused
            // insert report zero rows, and the read below sees the occupant.
            using (SqliteCommand insert = this.connection.CreateCommand())
            {
                insert.CommandText =
                    """
                    INSERT INTO ScheduleRegistrations (ScheduleId, Environment, RunId)
                    VALUES (@scheduleId, @environment, @runId)
                    ON CONFLICT(ScheduleId) DO NOTHING;
                    """;
                insert.Parameters.AddWithValue("@scheduleId", scheduleId);
                insert.Parameters.AddWithValue("@environment", registration.Environment);
                insert.Parameters.AddWithValue("@runId", registration.RunId.Value);
                if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
                {
                    return;
                }
            }

            // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds;
            // anything else refuses without disclosing the occupant (the caller may have no reach over its
            // environment).
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Environment, RunId FROM ScheduleRegistrations WHERE ScheduleId = @scheduleId;";
            select.Parameters.AddWithValue("@scheduleId", scheduleId);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                && new ScheduleRegistration(reader.GetString(0), new WorkflowRunId(reader.GetString(1))) == registration)
            {
                return;
            }
        }
        finally
        {
            this.gate.Release();
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Environment, RunId FROM ScheduleRegistrations WHERE ScheduleId = @scheduleId;";
            select.Parameters.AddWithValue("@scheduleId", scheduleId);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? new ScheduleRegistration(reader.GetString(0), new WorkflowRunId(reader.GetString(1)))
                : null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM ScheduleRegistrations WHERE ScheduleId = @scheduleId;";
            delete.Parameters.AddWithValue("@scheduleId", scheduleId);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }
}