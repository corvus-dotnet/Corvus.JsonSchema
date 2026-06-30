// <copyright file="SqliteEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve an environment
/// (design §5.5) persisted for a single-file / embedded host. Each authorization is stored as its
/// <see cref="EnvironmentRunnerAuthorization"/> schema document in a BLOB column, keyed by <c>(Environment, RunnerId)</c>,
/// with the filterable fields (status) and the etag mirrored into columns for querying and the optimistic-concurrency check.
/// Mirrors <see cref="SqliteAvailabilityRequestStore"/>, keyed by environment + runner rather than a single id.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through it, as the other SQLite stores do.</remarks>
public sealed class SqliteEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteEnvironmentRunnerAuthorizationStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema against a file database.</summary>
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

    /// <summary>Opens a runner-authorization store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=runners.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteEnvironmentRunnerAuthorizationStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
            // runner is not reset to Pending).
            byte[]? existing = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
            if (existing is not null)
            {
                return ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(existing.AsMemory());
            }

            WorkflowEtag etag = NewEtag();
            byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO EnvironmentRunnerAuthorizations (Environment, RunnerId, Status, Etag, Document) " +
                "VALUES (@env, @runner, @status, @etag, @doc);";
            insert.Parameters.AddWithValue("@env", environment);
            insert.Parameters.AddWithValue("@runner", runnerId);
            insert.Parameters.AddWithValue("@status", RunnerAuthorizationStatusNames.Pending);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM EnvironmentRunnerAuthorizations");
            var conditions = new List<string>(3);
            AppendFilters(conditions, select, query);

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Environment, RunnerId;");
            select.CommandText = sql.ToString();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the ADO predicate needs (a genuine DB-param
        // leaf) only here. Undefined token = first page; a malformed token throws FormatException.
        string? cursorEnvironment = null;
        string? cursorRunnerId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(EnvironmentRunnerAuthorizationContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (EnvironmentRunnerAuthorizationContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> cursorEnvUtf8, out ReadOnlySpan<byte> cursorRunnerUtf8))
                {
                    cursorEnvironment = Encoding.UTF8.GetString(cursorEnvUtf8);
                    cursorRunnerId = Encoding.UTF8.GetString(cursorRunnerUtf8);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM EnvironmentRunnerAuthorizations");
            var conditions = new List<string>(4);
            AppendFilters(conditions, select, query);

            if (cursorEnvironment is not null)
            {
                // Keyset seek strictly past (Environment, RunnerId): both are TEXT (SQLite BINARY collation == ordinal byte
                // order == the in-memory pager's span compare).
                conditions.Add("(Environment > @ce OR (Environment = @ce AND RunnerId > @cr))");
                select.Parameters.AddWithValue("@ce", cursorEnvironment);
                select.Parameters.AddWithValue("@cr", cursorRunnerId!);
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            // ORDER BY the keyset and LIMIT one beyond the page (lookahead); ORDER BY drives the bounded read, never a full
            // read + re-parse.
            sql.Append(" ORDER BY Environment, RunnerId LIMIT @limit;");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            select.CommandText = sql.ToString();

            var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
            try
            {
                bool hasMore = false;
                using (SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (page.Count == pageSize)
                        {
                            hasMore = true; // the (pageSize+1)th row exists → a next page; don't parse it
                            break;
                        }

                        page.Add(ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return EnvironmentRunnerAuthorizationPage.Create(page);
                }

                EnvironmentRunnerAuthorization last = page[page.Count - 1];
                using UnescapedUtf8JsonString lastEnv = last.Environment.GetUtf8String();
                using UnescapedUtf8JsonString lastRunner = last.RunnerId.GetUtf8String();
                return EnvironmentRunnerAuthorizationPage.Create(page, lastEnv.Span, lastRunner.Span);
            }
            catch
            {
                page.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(environment, runnerId, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc.AsMemory());
            byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE EnvironmentRunnerAuthorizations SET Status = @status, Etag = @etag, Document = @doc WHERE Environment = @env AND RunnerId = @runner;";
            update.Parameters.AddWithValue("@status", RunnerAuthorizationStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@env", environment);
            update.Parameters.AddWithValue("@runner", runnerId);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        this.gate.Dispose();
        return this.connection.DisposeAsync();
    }

    // Appends the shared list filters (status / environment) and the approver-inbox filter (Environment IN the administered
    // set) — server-derived strings reified as @-parameters (the SQL leaf). The administered set is never empty here (the
    // handler short-circuits a caller who administers nothing to an empty page before the store), but a null set (the
    // non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, SqliteCommand command, RunnerAuthorizationQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("@status", RunnerAuthorizationStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("@env", environment);
        }

        if (query.AdministeredEnvironments is { Count: > 0 } set)
        {
            var names = new string[set.Count];
            for (int i = 0; i < set.Count; i++)
            {
                names[i] = "@adm" + i.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(names[i], set[i]);
            }

            conditions.Add("Environment IN (" + string.Join(", ", names) + ")");
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private async ValueTask<byte[]?> DocumentAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentRunnerAuthorizations WHERE Environment = @env AND RunnerId = @runner;";
        select.Parameters.AddWithValue("@env", environment);
        select.Parameters.AddWithValue("@runner", runnerId);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS EnvironmentRunnerAuthorizations (
            Environment TEXT NOT NULL,
            RunnerId TEXT NOT NULL,
            Status TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (Environment, RunnerId)
        );
        CREATE INDEX IF NOT EXISTS IX_EnvironmentRunnerAuthorizations_Status ON EnvironmentRunnerAuthorizations (Status);
        """;
}