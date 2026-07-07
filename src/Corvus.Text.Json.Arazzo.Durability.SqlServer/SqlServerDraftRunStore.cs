// <copyright file="SqlServerDraftRunStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Text;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IDraftRunStore"/> — the §18 draft-run capture store. Each capture holds the
/// audited <see cref="DraftRun"/> record as its JSON document (a UTF-8 <c>VARCHAR(MAX)</c> column, exactly the
/// runner registry's registration-document idiom) and the packed document + sources as an opaque
/// <c>VARBINARY(MAX)</c> blob (exactly the catalog store's package idiom), keyed by run id. It uses
/// Microsoft.Data.SqlClient directly (no ORM, no migrations runtime), so the same code covers SQL Server, Azure
/// SQL Database and Azure SQL Managed Instance.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/> after provisioning with <see cref="PrepareAsync(string, CancellationToken)"/>.
/// </remarks>
public sealed class SqlServerDraftRunStore : IDraftRunStore, IAsyncDisposable
{
    private const string SchemaSql =
        """
        IF OBJECT_ID(N'draft_run_captures', N'U') IS NULL
        BEGIN
            CREATE TABLE draft_run_captures (
                run_id NVARCHAR(450) NOT NULL,
                record VARCHAR(MAX) COLLATE Latin1_General_100_CI_AS_SC_UTF8 NOT NULL,
                package VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_draft_run_captures PRIMARY KEY (run_id)
            );
        END;
        """;

    private readonly string connectionString;

    private SqlServerDraftRunStore(string connectionString)
    {
        this.connectionString = connectionString;
    }

    /// <summary>
    /// Provisions the capture-store schema (table). This performs DDL, so it requires a login permitted to
    /// create tables; run it once at deploy/migration time, separately from the least-privileged login used to
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

    /// <summary>Opens the draft-run store for operation against an already-provisioned schema.</summary>
    /// <remarks>
    /// This performs no DDL, so it is safe to use a least-privileged operational login granted only data access
    /// on the table. Call <see cref="PrepareAsync"/> once beforehand — with an elevated login — to create the
    /// schema. The connection string can carry an Entra/managed-identity credential
    /// (<c>Authentication=Active Directory Managed Identity</c>) for password-free operation.
    /// </remarks>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerDraftRunStore> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerDraftRunStore>(new SqlServerDraftRunStore(connectionString));
    }

    /// <inheritdoc/>
    public async ValueTask PutAsync(WorkflowRunId id, DraftRun record, ReadOnlyMemory<byte> package, CancellationToken cancellationToken)
    {
        // The record column is a UTF-8 VARCHAR; SqlClient transmits .NET strings as NVARCHAR, which the server
        // converts losslessly into the UTF-8 column (the runner registry's registration-document idiom). Serialize
        // once, then decode to a string for the parameter — a cold-path put cost; reads stay bytes-native.
        byte[] recordUtf8 = PersistedJson.ToArray(record, static (Utf8JsonWriter writer, in DraftRun r) => r.WriteTo(writer));
        string recordText = Encoding.UTF8.GetString(recordUtf8);

        // The (potentially large, ~KB) package is streamed straight from its memory as the VARBINARY(MAX)
        // parameter — no GC copy of the whole package (the catalog store's package-blob idiom).
        using ReadOnlyMemoryStream packageStream = ReadOnlyMemoryStream.Rent(package);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand upsert = connection.CreateCommand();
        upsert.CommandText =
            """
            MERGE draft_run_captures AS target
            USING (SELECT @runId AS run_id) AS source
            ON target.run_id = source.run_id
            WHEN MATCHED THEN
                UPDATE SET record = @record, package = @package
            WHEN NOT MATCHED THEN
                INSERT (run_id, record, package) VALUES (@runId, @record, @package);
            """;
        upsert.Parameters.AddWithValue("@runId", id.Value);
        upsert.Parameters.Add(new SqlParameter("@record", SqlDbType.NVarChar, -1) { Value = recordText });
        upsert.Parameters.Add(new SqlParameter("@package", SqlDbType.VarBinary, -1) { Value = packageStream });
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<DraftRun>?> GetAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();

        // Read the UTF-8 record column back as raw bytes (CAST to VARBINARY) so the parse stays bytes-native — no
        // nvarchar string materialization on the read path.
        select.CommandText = "SELECT CAST(record AS VARBINARY(MAX)) FROM draft_run_captures WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] record
            ? PersistedJson.ToPooledDocument<DraftRun>(record)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT package FROM draft_run_captures WHERE run_id = @runId;";
        select.Parameters.AddWithValue("@runId", id.Value);
        return await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is byte[] package
            ? (ReadOnlyMemory<byte>?)package
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(WorkflowRunId id, CancellationToken cancellationToken)
    {
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM draft_run_captures WHERE run_id = @runId;";
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