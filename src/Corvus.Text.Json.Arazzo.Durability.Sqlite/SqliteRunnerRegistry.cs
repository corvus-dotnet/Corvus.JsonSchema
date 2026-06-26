// <copyright file="SqliteRunnerRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IRunnerRegistry"/> — a single-file, zero-setup directory of live runners for
/// local-development and embedded single-node use. Each <see cref="RunnerRegistration"/> is stored as its
/// JSON document in a <c>doc</c> blob keyed by runner id, alongside a queryable <c>last_seen_at</c> column
/// used for pruning.
/// </summary>
/// <remarks>
/// One connection is held open for the registry's lifetime and all operations are serialised through it — adequate
/// for the local/embedded use this adapter targets. Create instances with
/// <see cref="ConnectAsync(string, CancellationToken)"/>, which runs the idempotent schema.
/// </remarks>
public sealed class SqliteRunnerRegistry : IRunnerRegistry, IAsyncDisposable
{
    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS runner_registrations (
            runner_id TEXT PRIMARY KEY NOT NULL,
            last_seen_at INTEGER NOT NULL,
            doc BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_runner_registrations_last_seen ON runner_registrations (last_seen_at);
        CREATE TABLE IF NOT EXISTS runner_hosted_versions (
            runner_id TEXT NOT NULL,
            base_workflow_id TEXT NOT NULL,
            version_number INTEGER NOT NULL,
            PRIMARY KEY (runner_id, base_workflow_id, version_number)
        );
        CREATE INDEX IF NOT EXISTS IX_runner_hosted_versions_version ON runner_hosted_versions (base_workflow_id, version_number);
        """;

    private readonly SqliteConnection connection;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteRunnerRegistry(SqliteConnection connection)
    {
        this.connection = connection;
    }

    /// <summary>Provisions the registry schema (table and index) against a file database.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        using SqliteCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens a runner registry over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=runners.db</c>).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised registry.</returns>
    public static async ValueTask<SqliteRunnerRegistry> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);

        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteRunnerRegistry(connection);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(RunnerRegistration registration, CancellationToken cancellationToken)
    {
        string runnerId = registration.RunnerIdValue;
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand upsert = this.connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText =
                    """
                    INSERT INTO runner_registrations (runner_id, last_seen_at, doc)
                    VALUES (@runnerId, @lastSeenAt, @doc)
                    ON CONFLICT(runner_id) DO UPDATE SET last_seen_at = excluded.last_seen_at, doc = excluded.doc;
                    """;
                upsert.Parameters.AddWithValue("@runnerId", runnerId);
                upsert.Parameters.AddWithValue("@lastSeenAt", registration.LastSeenAtValue.ToUnixTimeMilliseconds());
                upsert.Parameters.AddWithValue("@doc", PersistedJson.ToArray(registration, static (Utf8JsonWriter writer, in RunnerRegistration r) => r.WriteTo(writer)));
                await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Re-project this runner's hosting index: drop its old rows, then insert one per loaded hosted version.
            using (SqliteCommand clear = this.connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id = @runnerId;";
                clear.Parameters.AddWithValue("@runnerId", runnerId);
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach ((string baseWorkflowId, int versionNumber) in registration.LoadedHostedVersions())
            {
                using SqliteCommand insert = this.connection.CreateCommand();
                insert.Transaction = transaction;
                insert.CommandText = "INSERT INTO runner_hosted_versions (runner_id, base_workflow_id, version_number) VALUES (@runnerId, @baseWorkflowId, @versionNumber);";
                insert.Parameters.AddWithValue("@runnerId", runnerId);
                insert.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
                insert.Parameters.AddWithValue("@versionNumber", versionNumber);
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsVersionHostedAsync(string baseWorkflowId, int versionNumber, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(baseWorkflowId);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand command = this.connection.CreateCommand();
            command.CommandText = "SELECT EXISTS(SELECT 1 FROM runner_hosted_versions WHERE base_workflow_id = @baseWorkflowId AND version_number = @versionNumber);";
            command.Parameters.AddWithValue("@baseWorkflowId", baseWorkflowId);
            command.Parameters.AddWithValue("@versionNumber", versionNumber);
            return (long)(await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))! != 0L;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> HeartbeatAsync(string runnerId, DateTimeOffset at, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(runnerId);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing;
            using (SqliteCommand select = this.connection.CreateCommand())
            {
                select.CommandText = "SELECT doc FROM runner_registrations WHERE runner_id = @runnerId;";
                select.Parameters.AddWithValue("@runnerId", runnerId);
                existing = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as byte[];
            }

            if (existing is null)
            {
                return false;
            }

            byte[] doc = PersistedJson.ToArray((existing, at), static (Utf8JsonWriter writer, in (byte[] Existing, DateTimeOffset At) ctx) =>
            {
                using ParsedJsonDocument<RunnerRegistration> parsed = ParsedJsonDocument<RunnerRegistration>.Parse(ctx.Existing);
                parsed.RootElement.WriteWithLastSeenAt(writer, ctx.At);
            });
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE runner_registrations SET last_seen_at = @lastSeenAt, doc = @doc WHERE runner_id = @runnerId;";
            update.Parameters.AddWithValue("@runnerId", runnerId);
            update.Parameters.AddWithValue("@lastSeenAt", at.ToUnixTimeMilliseconds());
            update.Parameters.AddWithValue("@doc", doc);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<IReadOnlyList<RunnerRegistration>> ListAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand command = this.connection.CreateCommand();
            command.CommandText = "SELECT doc FROM runner_registrations;";
            var result = new List<RunnerRegistration>();
            using SqliteDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                result.Add(RunnerRegistration.FromJson(reader.GetFieldValue<byte[]>(0)));
            }

            return result;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<RunnerRegistryPage> ListAsync(int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : RunnerRegistryPage.DefaultPageSize;

        // Decode the keyset cursor from the request's page token; the runner id reifies to a string only at the ADO
        // TEXT-parameter boundary the driver forces (a genuine DB-param leaf) — undefined token = first page.
        string? after = RunnerRegistryContinuationToken.DecodeCursorToString(pageToken);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Native keyset page: the runner_id PRIMARY KEY B-tree (BINARY collation == ordinal UTF-8 byte order, the same
            // order the in-memory pager sorts by) drives both the range seek and the ordering, and LIMIT bounds the read to
            // one page — never a full SELECT + parse of every registration. @limit is pageSize + 1 to look one row ahead.
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                """
                SELECT doc FROM runner_registrations
                WHERE (@after IS NULL OR runner_id > @after)
                ORDER BY runner_id
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", pageSize + 1);

            var page = new List<RunnerRegistration>(pageSize + 1);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                page.Add(RunnerRegistration.FromJson(reader.GetFieldValue<byte[]>(0)));
            }

            if (page.Count <= pageSize)
            {
                return RunnerRegistryPage.Create(page);
            }

            // A (pageSize+1)th row exists → there is a next page; drop it and emit the token from the last kept row's id
            // (bytes-native: base64url straight over the runner id's persisted UTF-8, no managed id string).
            page.RemoveAt(page.Count - 1);
            using UnescapedUtf8JsonString lastId = page[page.Count - 1].RunnerId.GetUtf8String();
            return RunnerRegistryPage.Create(page, lastId.Span);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<int> PruneAsync(DateTimeOffset deadBefore, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            long cutoff = deadBefore.ToUnixTimeMilliseconds();
            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            // SQLite does not enforce FK cascade unless PRAGMA foreign_keys=ON per connection, so delete the
            // child hosting rows for the pruned runners explicitly before removing the parent registrations.
            using (SqliteCommand clearChildren = this.connection.CreateCommand())
            {
                clearChildren.Transaction = transaction;
                clearChildren.CommandText = "DELETE FROM runner_hosted_versions WHERE runner_id IN (SELECT runner_id FROM runner_registrations WHERE last_seen_at < @cutoff);";
                clearChildren.Parameters.AddWithValue("@cutoff", cutoff);
                await clearChildren.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            int pruned;
            using (SqliteCommand command = this.connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "DELETE FROM runner_registrations WHERE last_seen_at < @cutoff;";
                command.Parameters.AddWithValue("@cutoff", cutoff);
                pruned = await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return pruned;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <summary>Disposes the connection held open for the registry's lifetime.</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }
}