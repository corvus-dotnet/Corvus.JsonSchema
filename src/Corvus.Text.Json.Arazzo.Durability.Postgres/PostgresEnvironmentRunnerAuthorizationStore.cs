// <copyright file="PostgresEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a deployment
/// environment (design §5.5) persisted relationally. Each authorization is stored as its
/// <see cref="EnvironmentRunnerAuthorization"/> schema document in a <c>bytea</c> column, keyed by <c>(Environment, RunnerId)</c>,
/// with the filterable field (status) and the etag mirrored into columns for querying and the optimistic-concurrency check.
/// Mirrors <see cref="PostgresAvailabilityRequestStore"/>, keyed by environment + runner rather than a single id.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class PostgresEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresEnvironmentRunnerAuthorizationStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">An Npgsql connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the schema over a caller-supplied data source.</summary>
    /// <param name="dataSource">An Npgsql data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(NpgsqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">An Npgsql connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the data source it creates).</returns>
    public static ValueTask<PostgresEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentRunnerAuthorizationStore>(
            new PostgresEnvironmentRunnerAuthorizationStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresEnvironmentRunnerAuthorizationStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentRunnerAuthorizationStore>(
            new PostgresEnvironmentRunnerAuthorizationStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending).
        byte[]? existing = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO EnvironmentRunnerAuthorizations (Environment, RunnerId, Status, Etag, Document) " +
            "VALUES (@env, @runner, @status, @etag, @doc);";
        insert.Parameters.AddWithValue("env", environment);
        insert.Parameters.AddWithValue("runner", runnerId);
        insert.Parameters.AddWithValue("status", RunnerAuthorizationStatusNames.Pending);
        insert.Parameters.AddWithValue("etag", etag.Value!);
        insert.Parameters.AddWithValue("doc", json);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM EnvironmentRunnerAuthorizations");
        var conditions = new List<string>(3);
        AppendFilters(conditions, select, query);

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY Environment, RunnerId;");
        select.CommandText = sql.ToString();
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the Npgsql predicate needs (a genuine DB-param
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM EnvironmentRunnerAuthorizations");
        var conditions = new List<string>(4);
        AppendFilters(conditions, select, query);

        if (cursorEnvironment is not null)
        {
            // Keyset seek strictly past (Environment, RunnerId): both are declared COLLATE "C" so their compare is byte-ordinal
            // == the in-memory pager's span compare.
            conditions.Add("(Environment > @ce OR (Environment = @ce AND RunnerId > @cr))");
            select.Parameters.AddWithValue("ce", cursorEnvironment);
            select.Parameters.AddWithValue("cr", cursorRunnerId!);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        // ORDER BY the keyset and LIMIT one beyond the page (lookahead); the IX index on (Environment, RunnerId) drives both
        // the order and the seek — never a full read + re-parse of the whole table.
        sql.Append(" ORDER BY Environment, RunnerId LIMIT @limit;");
        select.Parameters.AddWithValue("limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            bool hasMore = false;
            await using (NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> DecideAsync(string environment, string runnerId, RunnerAuthorizationDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // decided result; SerializeDecision throws RunnerAuthorizationConflictException on a stale etag.
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc.AsMemory());
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE EnvironmentRunnerAuthorizations SET Status = @status, Etag = @etag, Document = @doc WHERE Environment = @env AND RunnerId = @runner;";
        update.Parameters.AddWithValue("status", RunnerAuthorizationStatusNames.ToWire(decision.Status));
        update.Parameters.AddWithValue("etag", etag.Value!);
        update.Parameters.AddWithValue("doc", json);
        update.Parameters.AddWithValue("env", environment);
        update.Parameters.AddWithValue("runner", runnerId);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Appends the shared list filters (status / environment) and the approver-inbox filter (Environment IN the administered
    // set) — server-derived strings reified as @-parameters (the SQL leaf). The administered set is never empty here (the
    // handler short-circuits a caller who administers nothing to an empty page before the store), but a null set (the
    // non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, NpgsqlCommand command, RunnerAuthorizationQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("status", RunnerAuthorizationStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("env", environment);
        }

        if (query.AdministeredEnvironments is { Count: > 0 } set)
        {
            var names = new string[set.Count];
            for (int i = 0; i < set.Count; i++)
            {
                string name = "adm" + i.ToString(CultureInfo.InvariantCulture);
                names[i] = "@" + name;
                command.Parameters.AddWithValue(name, set[i]);
            }

            conditions.Add("Environment IN (" + string.Join(", ", names) + ")");
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> DocumentAsync(NpgsqlConnection connection, string environment, string runnerId, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentRunnerAuthorizations WHERE Environment = @env AND RunnerId = @runner;";
        select.Parameters.AddWithValue("env", environment);
        select.Parameters.AddWithValue("runner", runnerId);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS EnvironmentRunnerAuthorizations (
            Environment TEXT COLLATE "C" NOT NULL,
            RunnerId TEXT COLLATE "C" NOT NULL,
            Status TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL,
            PRIMARY KEY (Environment, RunnerId)
        );
        CREATE INDEX IF NOT EXISTS IX_EnvironmentRunnerAuthorizations_Status ON EnvironmentRunnerAuthorizations (Status);
        """;
}