// <copyright file="SqlServerWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Data;
using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration record for a
/// base workflow id — the mutable set of administrator identities entitled to publish further versions and to manage
/// administration. Each record is stored as its <see cref="WorkflowAdministrators"/> document in a <c>varbinary(max)</c>
/// column, keyed by BaseWorkflowId; its etag is held in a column for the optimistic-concurrency check. The record holds
/// deployment-stamped identities only — never secret material.
/// </summary>
/// <remarks>
/// Each operation opens a pooled connection, so the store is naturally concurrent; the <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its etag before writing, mirroring the other backends.
/// </remarks>
public sealed class SqlServerWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerWorkflowAdministratorStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A SqlClient connection string for a role permitted to create tables.</param>
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

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">A SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<SqlServerWorkflowAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerWorkflowAdministratorStore>(new SqlServerWorkflowAdministratorStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await ReadDocumentAsync(connection, baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<WorkflowAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<WorkflowAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await ReadDocumentAsync(connection, baseWorkflowId, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();
        if (existing is not null)
        {
            // Parse the existing record ONCE, NON-COPYING over the driver's array (the read leaf) — used for both the etag
            // check and the carried-forward merge. Serialize the result into the pooled buffer the returned document owns and
            // stream its exact bytes (no GC document array, no second copy); the column etag is the one we just generated.
            using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            ParsedJsonDocument<WorkflowAdministrators> updated = WorkflowAdministratorsSerialization.SerializeUpdatedDoc(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
            try
            {
                ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
                await using SqlCommand update = connection.CreateCommand();
                update.CommandText = "UPDATE WorkflowAdministrators SET Etag = @etag, Document = @doc WHERE BaseWorkflowId = @id;";
                update.Parameters.AddWithValue("@etag", etag.Value!);
                using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
                update.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
                update.Parameters.AddWithValue("@id", baseWorkflowId);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                return updated;
            }
            catch
            {
                updated.Dispose();
                throw;
            }
        }

        // No record yet: materialization is only valid against the None etag (the v1-derived default).
        if (!expectedEtag.IsNone)
        {
            throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
        }

        ParsedJsonDocument<WorkflowAdministrators> doc = WorkflowAdministratorsSerialization.SerializeNewDoc(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using SqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", baseWorkflowId);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
            insert.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> ReadDocumentAsync(SqlConnection connection, string baseWorkflowId, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkflowAdministrators WHERE BaseWorkflowId = @id;";
        select.Parameters.AddWithValue("@id", baseWorkflowId);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

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

    private const string SchemaSql =
        """
        IF OBJECT_ID(N'WorkflowAdministrators', N'U') IS NULL
        BEGIN
            CREATE TABLE WorkflowAdministrators (
                BaseWorkflowId NVARCHAR(450) NOT NULL PRIMARY KEY,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
        END;
        """;
}