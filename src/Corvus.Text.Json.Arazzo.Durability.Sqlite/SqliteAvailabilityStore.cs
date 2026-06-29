// <copyright file="SqliteAvailabilityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IAvailabilityStore"/> (design §7.8): the availability matrix (which workflow versions are
/// available in which environments) persisted for a single-file / embedded host. Each entry is stored as its
/// <see cref="AvailabilityEntry"/> document in a BLOB column, keyed by (BaseWorkflowId, VersionNumber, Environment).
/// Availability has no mutable state and carries no security tags — an entry is created (idempotently) to make a version
/// available and deleted to withdraw it; authorization and readiness are the control-plane surface's concern.
/// </summary>
/// <remarks>
/// One connection is held open and all operations are serialised through a gate, as the other Sqlite stores do. The two
/// list axes are indexed keyset range scans: by-version orders by Environment (the primary key prefix already covers
/// it); by-environment orders by (BaseWorkflowId, VersionNumber) over a secondary index.
/// </remarks>
public sealed class SqliteAvailabilityStore : IAvailabilityStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteAvailabilityStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an availability store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=availability.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteAvailabilityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteAvailabilityStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(ParsedJsonDocument<AvailabilityEntry> Entry, bool Created)> MakeAvailableAsync(string baseWorkflowId, int versionNumber, string environment, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Idempotent: if the version is already available in the environment, return the existing entry unchanged.
            byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return (PersistedJson.ToPooledDocument<AvailabilityEntry>(existing), false);
            }

            using ParsedJsonDocument<AvailabilityEntry> draft = AvailabilityEntry.Draft(baseWorkflowId, versionNumber, environment);
            byte[] json = AvailabilitySerialization.SerializeNew(draft.RootElement, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO Availability (BaseWorkflowId, VersionNumber, Environment, Document) VALUES (@b, @v, @e, @doc);";
            insert.Parameters.AddWithValue("@b", baseWorkflowId);
            insert.Parameters.AddWithValue("@v", versionNumber);
            insert.Parameters.AddWithValue("@e", environment);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return (PersistedJson.ToPooledDocument<AvailabilityEntry>(json), true);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityEntry>?> GetAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, versionNumber, environment, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<AvailabilityEntry>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> WithdrawAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment = @e;";
            delete.Parameters.AddWithValue("@b", baseWorkflowId);
            delete.Parameters.AddWithValue("@v", versionNumber);
            delete.Parameters.AddWithValue("@e", environment);
            return await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) > 0;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByVersionAsync(string baseWorkflowId, int versionNumber, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = hasCursor
                ? "SELECT Environment, Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment > @ce ORDER BY Environment;"
                : "SELECT Environment, Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v ORDER BY Environment;";
            select.Parameters.AddWithValue("@b", baseWorkflowId);
            select.Parameters.AddWithValue("@v", versionNumber);
            if (hasCursor)
            {
                select.Parameters.AddWithValue("@ce", cursor.Environment);
            }

            var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
            try
            {
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                bool hasMore = false;
                string lastEnvironment = string.Empty;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (docs.Count == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    lastEnvironment = reader.GetString(0);
                    docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(reader.GetFieldValue<byte[]>(1)));
                }

                return hasMore
                    ? AvailabilityPage.Create(docs, baseWorkflowId, versionNumber, lastEnvironment)
                    : AvailabilityPage.Create(docs);
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityPage> ListByEnvironmentAsync(string environment, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environment);
        int pageSize = limit > 0 ? limit : AvailabilityPage.DefaultPageSize;
        bool hasCursor = TryDecodeCursor(pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = hasCursor
                ? "SELECT BaseWorkflowId, VersionNumber, Document FROM Availability WHERE Environment = @e AND (BaseWorkflowId > @cb OR (BaseWorkflowId = @cb AND VersionNumber > @cv)) ORDER BY BaseWorkflowId, VersionNumber;"
                : "SELECT BaseWorkflowId, VersionNumber, Document FROM Availability WHERE Environment = @e ORDER BY BaseWorkflowId, VersionNumber;";
            select.Parameters.AddWithValue("@e", environment);
            if (hasCursor)
            {
                select.Parameters.AddWithValue("@cb", cursor.BaseWorkflowId);
                select.Parameters.AddWithValue("@cv", cursor.VersionNumber);
            }

            var docs = new PooledDocumentList<AvailabilityEntry>(pageSize);
            try
            {
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                bool hasMore = false;
                string lastBaseWorkflowId = string.Empty;
                int lastVersionNumber = 0;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (docs.Count == pageSize)
                    {
                        hasMore = true;
                        break;
                    }

                    lastBaseWorkflowId = reader.GetString(0);
                    lastVersionNumber = reader.GetInt32(1);
                    docs.Add(PersistedJson.ToPooledDocument<AvailabilityEntry>(reader.GetFieldValue<byte[]>(2)));
                }

                return hasMore
                    ? AvailabilityPage.Create(docs, lastBaseWorkflowId, lastVersionNumber, environment)
                    : AvailabilityPage.Create(docs);
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    private static bool TryDecodeCursor(JsonString pageToken, out (string BaseWorkflowId, int VersionNumber, string Environment) cursor)
    {
        cursor = default;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        return AvailabilityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
    }

    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, int versionNumber, string environment, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM Availability WHERE BaseWorkflowId = @b AND VersionNumber = @v AND Environment = @e;";
        select.Parameters.AddWithValue("@b", baseWorkflowId);
        select.Parameters.AddWithValue("@v", versionNumber);
        select.Parameters.AddWithValue("@e", environment);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Availability (
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            Environment TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (BaseWorkflowId, VersionNumber, Environment)
        );
        CREATE INDEX IF NOT EXISTS IX_Availability_Environment ON Availability (Environment, BaseWorkflowId, VersionNumber);
        """;
}