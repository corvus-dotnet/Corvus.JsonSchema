// <copyright file="SqlServerDraftRunTraceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IDraftRunTraceStore"/> — the sibling store carrying a §18 debug (<c>$draft</c>)
/// run's latest assembled metadata trace. Each row holds one run's trace as an opaque <c>VARBINARY(MAX)</c> blob
/// keyed by run id — exactly the package-blob idiom <see cref="SqlServerDraftRunStore"/> persists beside its
/// record. It uses Microsoft.Data.SqlClient directly (no ORM, no migrations runtime), so the same code covers SQL
/// Server, Azure SQL Database and Azure SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. The put streams the trace
/// straight from its memory as the <c>VARBINARY(MAX)</c> parameter via <see cref="ReadOnlyMemoryStream.Rent"/> — no
/// GC copy of the trace, matching <see cref="SqlServerDraftRunStore"/>'s package bind. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerDraftRunTraceStore : IDraftRunTraceStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        IF OBJECT_ID(N'draft_run_traces', N'U') IS NULL
        BEGIN
            CREATE TABLE draft_run_traces (
                run_id NVARCHAR(450) NOT NULL,
                trace VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_draft_run_traces PRIMARY KEY (run_id)
            );
        END;
        """;

    private readonly string connectionString;

    private SqlServerDraftRunTraceStore(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Provisions the trace-store schema (table). This performs DDL, so it requires a login permitted to create
    /// tables; run it once at deploy/migration time, separately from the least-privileged login used to
    /// <see cref="ConnectAsync"/> the store for operation.
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

    /// <summary>Opens the draft-run-trace store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data access on
    /// the table. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to create the schema.
    /// The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerDraftRunTraceStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerDraftRunTraceStore>(new SqlServerDraftRunTraceStore(connectionString));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, ReadOnlyMemory<byte> traceUtf8, CancellationToken cancellationToken)
    {
        // The trace is streamed straight from its memory as the VARBINARY(MAX) parameter — no GC copy of the trace
        // (the package-blob idiom SqlServerDraftRunStore uses).
        using ReadOnlyMemoryStream traceStream = ReadOnlyMemoryStream.Rent(traceUtf8);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            MERGE draft_run_traces AS target
            USING (SELECT @runId AS run_id) AS source
            ON target.run_id = source.run_id
            WHEN MATCHED THEN
                UPDATE SET trace = @trace
            WHEN NOT MATCHED THEN
                INSERT (run_id, trace) VALUES (@runId, @trace);
            """;
        upsert.Parameters.AddWithValue("@runId", id.Value);
        upsert.Parameters.Add(new SqlParameter("@trace", SqlDbType.VarBinary, -1) { Value = traceStream });
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT trace FROM draft_run_traces WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] trace
            ? (ReadOnlyMemory<byte>?)trace
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM draft_run_traces WHERE run_id = @runId;";
        delete.Parameters.AddWithValue("@runId", id.Value);
        return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
    }

    /// <summary>Disposes the store. This implementation holds no per-instance resources, so it is a no-op.</summary>
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