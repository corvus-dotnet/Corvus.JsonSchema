// <copyright file="SqlServerScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IScheduleRegistry"/>: one row per <c>scheduleId</c> whose primary-key
/// insert conflict enforces the schedules contract's deployment-global uniqueness. Under the composite
/// <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the sole guardian of that
/// uniqueness — a run-key collision can no longer observe a schedule created in another environment — so a
/// SQL Server deployment needs this durable registry, not the in-memory one, or every control-plane restart
/// forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the registry is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerScheduleRegistry : IScheduleRegistry
{
    private const int UniqueConstraintViolation = 2627;
    private const int DuplicateKeyViolation = 2601;

    private const string SchemaSql =
        """
        IF OBJECT_ID(N'schedule_registrations', N'U') IS NULL
        BEGIN
            CREATE TABLE schedule_registrations (
                schedule_id NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY,
                environment NVARCHAR(63) NOT NULL,
                run_id NVARCHAR(387) NOT NULL
            );
        END;
        """;

    private readonly string connectionString;

    private SqlServerScheduleRegistry(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>Provisions the registry schema (the table) from a connection string.</summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string for a login permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
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
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry.</returns>
    public static ValueTask<SqlServerScheduleRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerScheduleRegistry>(new SqlServerScheduleRegistry(connectionString));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        // The primary-key insert conflict IS the uniqueness gate; the read below sees the committed occupant.
        await using (SqlCommand insert = connection.CreateCommand())
        {
            insert.CommandText = "INSERT INTO schedule_registrations (schedule_id, environment, run_id) VALUES (@scheduleId, @environment, @runId);";
            insert.Parameters.AddWithValue("@scheduleId", scheduleId);
            insert.Parameters.AddWithValue("@environment", registration.Environment);
            insert.Parameters.AddWithValue("@runId", registration.RunId.Value);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SqlException ex) when (ex.Number is UniqueConstraintViolation or DuplicateKeyViolation)
            {
                // Occupied: fall through to compare with the occupant.
            }
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT environment, run_id FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        select.Parameters.AddWithValue("@scheduleId", scheduleId);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT environment, run_id FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        select.Parameters.AddWithValue("@scheduleId", scheduleId);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? new ScheduleRegistration(reader.GetString(0), new WorkflowRunId(reader.GetString(1)))
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await using var connection = new SqlConnection(this.connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM schedule_registrations WHERE schedule_id = @scheduleId;";
        delete.Parameters.AddWithValue("@scheduleId", scheduleId);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}