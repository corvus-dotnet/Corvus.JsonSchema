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
/// <see cref="SqliteWorkflowAdministratorStore"/> without the reverse administration index.
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

            using SqliteCommand write = this.connection.CreateCommand();
            write.CommandText = isUpdate
                ? "UPDATE EnvironmentAdministrators SET Etag = @etag, Document = @doc WHERE EnvironmentName = @id;"
                : "INSERT INTO EnvironmentAdministrators (EnvironmentName, Etag, Document) VALUES (@id, @etag, @doc);";
            write.Parameters.AddWithValue("@id", environmentName);
            write.Parameters.AddWithValue("@etag", etag.Value!);
            write.Parameters.AddWithValue("@doc", json);
            await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM EnvironmentAdministrators WHERE EnvironmentName = @id;";
            delete.Parameters.AddWithValue("@id", environmentName);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
        """;
}