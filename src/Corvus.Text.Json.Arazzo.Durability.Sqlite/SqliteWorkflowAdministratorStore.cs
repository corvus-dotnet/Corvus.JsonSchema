// <copyright file="SqliteWorkflowAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
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
            return json is null ? null : ParsedJsonDocument<WorkflowAdministrators>.Parse(json.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
            WorkflowEtag etag = NewEtag();
            byte[] json;
            bool isUpdate;
            if (existing is not null)
            {
                // A record exists: parse it ONCE, NON-COPYING over the driver's array, for both the etag check and the
                // carried-forward merge. The caller must hold its current etag (None means "I expected no record").
                using ParsedJsonDocument<WorkflowAdministrators> current = ParsedJsonDocument<WorkflowAdministrators>.Parse(existing.AsMemory());
                if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                json = WorkflowAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
                isUpdate = true;
            }
            else
            {
                // No record yet: materialization is only valid against the None etag (the v1-derived default).
                if (!expectedEtag.IsNone)
                {
                    throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
                }

                json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), etag);
                isUpdate = false;
            }

            // The document write and the reverse-index rewrite are atomic (design §15.4): the inbox must never observe a
            // base id indexed under a digest its current administrator set no longer holds, or vice versa.
            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand write = this.connection.CreateCommand())
            {
                write.Transaction = transaction;
                write.CommandText = isUpdate
                    ? "UPDATE WorkflowAdministrators SET Etag = @etag, Document = @doc WHERE BaseWorkflowId = @id;"
                    : "INSERT INTO WorkflowAdministrators (BaseWorkflowId, Etag, Document) VALUES (@id, @etag, @doc);";
                write.Parameters.AddWithValue("@id", baseWorkflowId);
                write.Parameters.AddWithValue("@etag", etag.Value!);
                write.Parameters.AddWithValue("@doc", json);
                await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Rewrite this base id's reverse-index rows: retract the stale digests, then index the current ones. The
            // administrator set is small, so a delete-all-then-insert is simplest and correct.
            using (SqliteCommand clear = this.connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM WorkflowAdministratorIndex WHERE BaseWorkflowId = @id;";
                clear.Parameters.AddWithValue("@id", baseWorkflowId);
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (string digest in WorkflowAdministeredPaging.DistinctDigests(administrators))
            {
                using SqliteCommand index = this.connection.CreateCommand();
                index.Transaction = transaction;
                index.CommandText = "INSERT INTO WorkflowAdministratorIndex (AdminDigest, BaseWorkflowId) VALUES (@digest, @id);";
                index.Parameters.AddWithValue("@digest", digest);
                index.Parameters.AddWithValue("@id", baseWorkflowId);
                await index.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : WorkflowAdministeredPage.DefaultPageSize;

        // The keyset cursor (the base id to page strictly after) reifies once here for the @after parameter — the SQL
        // leaf — never per row. SQLite TEXT compares with BINARY (ordinal) collation, matching the contract's order.
        string? after = WorkflowAdministeredContinuationToken.DecodeCursorToString(pageToken);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = after is null
                ? "SELECT BaseWorkflowId FROM WorkflowAdministratorIndex WHERE AdminDigest = @digest ORDER BY BaseWorkflowId LIMIT @n;"
                : "SELECT BaseWorkflowId FROM WorkflowAdministratorIndex WHERE AdminDigest = @digest AND BaseWorkflowId > @after ORDER BY BaseWorkflowId LIMIT @n;";
            select.Parameters.AddWithValue("@digest", adminDigest);
            select.Parameters.AddWithValue("@n", pageSize + 1);
            if (after is not null)
            {
                select.Parameters.AddWithValue("@after", after);
            }

            var rows = new List<string>(pageSize + 1);
            using (SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    rows.Add(reader.GetString(0));
                }
            }

            return WorkflowAdministeredPaging.ToPage(rows, pageSize);
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
        CREATE TABLE IF NOT EXISTS WorkflowAdministratorIndex (
            AdminDigest TEXT NOT NULL,
            BaseWorkflowId TEXT NOT NULL,
            PRIMARY KEY (AdminDigest, BaseWorkflowId)
        );
        """;
}