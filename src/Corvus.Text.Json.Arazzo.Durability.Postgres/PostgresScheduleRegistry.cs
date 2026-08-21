// <copyright file="PostgresScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IScheduleRegistry"/>: one row per <c>scheduleId</c> whose primary-key
/// insert conflict enforces the schedules contract's deployment-global uniqueness. Under the composite
/// <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of that
/// uniqueness — a run-key collision can no longer observe a schedule created in another environment — so a
/// Postgres deployment needs this durable registry, not the in-memory one, or every control-plane restart
/// forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the registry is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class PostgresScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS schedule_registrations (
            schedule_id TEXT COLLATE "C" PRIMARY KEY NOT NULL,
            environment TEXT NOT NULL,
            run_id TEXT NOT NULL
        );
        """;

    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;

    private PostgresScheduleRegistry(NpgsqlDataSource dataSource, bool ownsDataSource)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
    }

    /// <summary>Provisions the registry schema (the table) from a connection string.</summary>
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
    public static ValueTask<PostgresScheduleRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresScheduleRegistry>(new PostgresScheduleRegistry(NpgsqlDataSource.Create(connectionString), ownsDataSource: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<PostgresScheduleRegistry> ConnectAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresScheduleRegistry>(new PostgresScheduleRegistry(dataSource, ownsDataSource: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        // The primary-key insert conflict IS the uniqueness gate. ON CONFLICT DO NOTHING waits out a concurrent
        // inserter, so the read below always sees the committed occupant.
        await using (NpgsqlCommand insert = connection.CreateCommand())
        {
            insert.CommandText =
                """
                INSERT INTO schedule_registrations (schedule_id, environment, run_id)
                VALUES (@scheduleId, @environment, @runId)
                ON CONFLICT (schedule_id) DO NOTHING;
                """;
            insert.Parameters.AddWithValue("@scheduleId", scheduleId);
            insert.Parameters.AddWithValue("@environment", registration.Environment);
            insert.Parameters.AddWithValue("@runId", registration.RunId.Value);
            if (await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1)
            {
                return;
            }
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT environment, run_id FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        select.Parameters.AddWithValue("@scheduleId", scheduleId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            && new ScheduleRegistration(reader.GetString(0), new WorkflowRunId(reader.GetString(1))) == registration)
        {
            return;
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT environment, run_id FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        select.Parameters.AddWithValue("@scheduleId", scheduleId);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new ScheduleRegistration(reader.GetString(0), new WorkflowRunId(reader.GetString(1)))
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await using NpgsqlConnection connection = await this.dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        delete.Parameters.AddWithValue("@scheduleId", scheduleId);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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