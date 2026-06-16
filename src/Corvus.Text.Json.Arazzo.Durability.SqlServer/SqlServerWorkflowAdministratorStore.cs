// <copyright file="SqlServerWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
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
        return json is null ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
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
        byte[] json;
        if (existing is not null)
        {
            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != WorkflowAdministratorsSerialization.EtagOf(existing))
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeUpdated(existing, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
            await using SqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowAdministrators SET Etag = @etag, Document = @doc WHERE BaseWorkflowId = @id;";
            update.Parameters.AddWithValue("@etag", WorkflowAdministratorsSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@id", baseWorkflowId);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
            await using SqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", baseWorkflowId);
            insert.Parameters.AddWithValue("@etag", WorkflowAdministratorsSerialization.EtagOf(json).Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
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