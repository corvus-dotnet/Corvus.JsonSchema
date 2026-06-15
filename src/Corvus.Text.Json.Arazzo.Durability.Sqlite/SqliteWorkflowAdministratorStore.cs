// <copyright file="SqliteWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IWorkflowAdministratorStore"/> (design §13/§14.2): the explicit administration record for a base
/// workflow id — the mutable set of administrator identities — persisted for a single-file / embedded host. Each record is
/// stored as its <see cref="WorkflowAdministrators"/> document in a BLOB column, keyed by BaseWorkflowId; its etag is held in
/// a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// One connection is held open and all operations are serialised through a gate, as the other Sqlite stores do; the
/// <see cref="PutAsync"/> create-or-replace runs under that gate so the etag check and write are atomic.
/// </remarks>
public sealed class SqliteWorkflowAdministratorStore : IWorkflowAdministratorStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowAdministratorStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema against a database.</summary>
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

    /// <summary>Opens a workflow administration store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=administration.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteWorkflowAdministratorStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
        }
        finally
        {
            this.gate.Release();
        }
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            byte[] json;
            if (existing is not null)
            {
                // A record exists: the caller must hold its current etag (None means "I expected no record").
                if (expectedEtag.IsNone || expectedEtag != WorkflowAdministratorsSerialization.EtagOf(existing))
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                json = WorkflowAdministratorsSerialization.SerializeUpdated(existing, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
                using SqliteCommand update = this.connection.CreateCommand();
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
                using SqliteCommand insert = this.connection.CreateCommand();
                insert.CommandText = "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
                insert.Parameters.AddWithValue("@id", baseWorkflowId);
                insert.Parameters.AddWithValue("@etag", WorkflowAdministratorsSerialization.EtagOf(json).Value!);
                insert.Parameters.AddWithValue("@doc", json);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkflowAdministrators WHERE BaseWorkflowId = @id;";
        select.Parameters.AddWithValue("@id", baseWorkflowId);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkflowAdministrators (
            BaseWorkflowId TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        """;
}