// <copyright file="SqliteEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration record for a
/// deployment environment — the mutable set of administrator identities — persisted for a single-file / embedded host.
/// Each record is stored as its <see cref="EnvironmentAdministrators"/> document in a BLOB column, keyed by
/// EnvironmentName; its etag is held in a column for the optimistic-concurrency check. Mirrors
/// <see cref="SqliteWorkflowAdministratorStore"/>, including the reverse administration index that powers
/// <see cref="ListAdministeredAsync"/>.
/// </summary>
/// <remarks>
/// One connection is held open and all operations are serialised through a gate, as the other Sqlite stores do; the
/// <see cref="PutAsync"/> create-or-replace runs under that gate so the etag check and write are atomic.
/// </remarks>
public sealed class SqliteEnvironmentAdministratorStore : IEnvironmentAdministratorStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteEnvironmentAdministratorStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an environment administration store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=administration.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteEnvironmentAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteEnvironmentAdministratorStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? json = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
            return json is null ? null : ParsedJsonDocument<EnvironmentAdministrators>.Parse(json.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
            WorkflowEtag etag = NewEtag();
            byte[] json;
            bool isUpdate;
            if (existing is not null)
            {
                // A record exists: parse it ONCE, NON-COPYING over the driver's array, for both the etag check and the
                // carried-forward merge. The caller must hold its current etag (None means "I expected no record").
                using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory());
                if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
                {
                    throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
                }

                json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
                isUpdate = true;
            }
            else
            {
                // No record yet: materialization is only valid against the None etag.
                if (!expectedEtag.IsNone)
                {
                    throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
                }

                json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
                isUpdate = false;
            }

            // The document write and the reverse-index rewrite are atomic (design §15.4): the inbox must never observe an
            // environment indexed under a digest its current administrator set no longer holds, or vice versa.
            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand write = this.connection.CreateCommand())
            {
                write.Transaction = transaction;
                write.CommandText = isUpdate
                    ? "UPDATE EnvironmentAdministrators SET Etag = @etag, Document = @doc WHERE EnvironmentName = @id;"
                    : "INSERT INTO EnvironmentAdministrators (EnvironmentName, Etag, Document) VALUES (@id, @etag, @doc);";
                write.Parameters.AddWithValue("@id", environmentName);
                write.Parameters.AddWithValue("@etag", etag.Value!);
                write.Parameters.AddWithValue("@doc", json);
                await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Rewrite this environment's reverse-index rows: retract the stale digests, then index the current ones. The
            // administrator set is small, so a delete-all-then-insert is simplest and correct.
            using (SqliteCommand clear = this.connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM EnvironmentAdministratorIndex WHERE EnvironmentName = @id;";
                clear.Parameters.AddWithValue("@id", environmentName);
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (string digest in EnvironmentAdministeredPaging.DistinctDigests(administrators))
            {
                using SqliteCommand index = this.connection.CreateCommand();
                index.Transaction = transaction;
                index.CommandText = "INSERT INTO EnvironmentAdministratorIndex (AdminDigest, EnvironmentName) VALUES (@digest, @id);";
                index.Parameters.AddWithValue("@digest", digest);
                index.Parameters.AddWithValue("@id", environmentName);
                await index.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The document delete and the reverse-index retraction are atomic, mirroring the PutAsync rewrite: the inbox
            // must never observe an environment indexed under a digest after its record has been removed. A missing record
            // is a no-op (both deletes simply affect no rows). The administrator set is small, so retracting every
            // index row for this environment — removing it from each administrator-digest bucket it was in — is simplest.
            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand delete = this.connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
                delete.Parameters.AddWithValue("@id", environmentName);
                await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            using (SqliteCommand clear = this.connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM EnvironmentAdministratorIndex WHERE EnvironmentName = @id;";
                clear.Parameters.AddWithValue("@id", environmentName);
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;

        // The keyset cursor (the environment name to page strictly after) reifies once here for the @after parameter — the
        // SQL leaf — never per row. SQLite TEXT compares with BINARY (ordinal) collation, matching the contract's order.
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = after is null
                ? "SELECT EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest ORDER BY EnvironmentName LIMIT @n;"
                : "SELECT EnvironmentName FROM EnvironmentAdministratorIndex WHERE AdminDigest = @digest AND EnvironmentName > @after ORDER BY EnvironmentName LIMIT @n;";
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

            return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    private async ValueTask<byte[]?> ReadDocumentAsync(string environmentName, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
        select.Parameters.AddWithValue("@id", environmentName);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS EnvironmentAdministrators (
            EnvironmentName TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS EnvironmentAdministratorIndex (
            AdminDigest TEXT NOT NULL,
            EnvironmentName TEXT NOT NULL,
            PRIMARY KEY (AdminDigest, EnvironmentName)
        );
        """;
}