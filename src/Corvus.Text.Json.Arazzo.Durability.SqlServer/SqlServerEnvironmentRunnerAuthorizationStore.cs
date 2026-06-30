// <copyright file="SqlServerEnvironmentRunnerAuthorizationStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Data;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.RunnerAuthorization;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IEnvironmentRunnerAuthorizationStore"/> — a runner's authorization to serve a deployment
/// environment (design §5.5) persisted relationally. Each authorization is stored as its
/// <see cref="EnvironmentRunnerAuthorization"/> schema document in a <c>varbinary(max)</c> column, keyed by
/// <c>(Environment, RunnerId)</c>, with the filterable field (status) and the etag mirrored into columns for querying and the
/// optimistic-concurrency check. Mirrors <see cref="SqlServerAvailabilityRequestStore"/>, keyed by environment + runner
/// rather than a single id.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class SqlServerEnvironmentRunnerAuthorizationStore : IEnvironmentRunnerAuthorizationStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerEnvironmentRunnerAuthorizationStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerEnvironmentRunnerAuthorizationStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerEnvironmentRunnerAuthorizationStore>(new SqlServerEnvironmentRunnerAuthorizationStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>> EnsurePendingAsync(string environment, string runnerId, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        ArgumentNullException.ThrowIfNull(actor);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);

        // Idempotent: a runner re-registering for an environment keeps whatever status it already has (so an Authorized
        // runner is not reset to Pending).
        byte[]? existing = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(existing);
        }

        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializePending(environment, runnerId, actor, this.timeProvider.GetUtcNow(), etag);
        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText =
            "INSERT INTO EnvironmentRunnerAuthorizations (Environment, RunnerId, Status, Etag, Document) " +
            "VALUES (@env, @runner, @status, @etag, @doc);";
        insert.Parameters.AddWithValue("@env", environment);
        insert.Parameters.AddWithValue("@runner", runnerId);
        insert.Parameters.AddWithValue("@status", RunnerAuthorizationStatusNames.Pending);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(json.AsMemory());
        insert.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentRunnerAuthorization>?> GetAsync(string environment, string runnerId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(runnerId);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(doc);
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<EnvironmentRunnerAuthorization>> ListAsync(RunnerAuthorizationQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<EnvironmentRunnerAuthorization>();
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM EnvironmentRunnerAuthorizations");
            var conditions = new List<string>(3);
            AppendFilters(conditions, select, query);

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Environment, RunnerId;");
            select.CommandText = sql.ToString();
            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        catch
        {
            list.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentRunnerAuthorizationPage> ListAsync(RunnerAuthorizationQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : EnvironmentRunnerAuthorizationPage.DefaultPageSize;

        // Decode the keyset cursor; environment + runnerId reify to the strings the SqlClient predicate needs (a genuine
        // DB-param leaf) only here. Undefined token = first page; a malformed token throws FormatException.
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

        var page = new PooledDocumentList<EnvironmentRunnerAuthorization>(pageSize);
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT TOP (@limit) Document FROM EnvironmentRunnerAuthorizations");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            var conditions = new List<string>(4);
            AppendFilters(conditions, select, query);

            if (cursorEnvironment is not null)
            {
                // Keyset seek strictly past (Environment, RunnerId): both columns are declared COLLATE Latin1_General_BIN2 so
                // their compare is byte-ordinal == the in-memory pager's span compare.
                conditions.Add("(Environment > @ce OR (Environment = @ce AND RunnerId > @cr))");
                select.Parameters.AddWithValue("@ce", cursorEnvironment);
                select.Parameters.AddWithValue("@cr", cursorRunnerId!);
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            // ORDER BY the keyset and TOP one beyond the page (lookahead); ORDER BY drives the bounded read, never a full
            // read + re-parse.
            sql.Append(" ORDER BY Environment, RunnerId;");
            select.CommandText = sql.ToString();

            bool hasMore = false;
            await using (SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, environment, runnerId, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag (a stale etag throws
        // RunnerAuthorizationConflictException), and serialize the decided record into the pooled buffer the returned document
        // owns — streamed as the VARBINARY(MAX) parameter (no GC document array, no second copy).
        using ParsedJsonDocument<EnvironmentRunnerAuthorization> current = ParsedJsonDocument<EnvironmentRunnerAuthorization>.Parse(doc.AsMemory());
        byte[] json = EnvironmentRunnerAuthorizationSerialization.SerializeDecision(current.RootElement, decision, expectedEtag, actor, this.timeProvider.GetUtcNow(), etag);
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE EnvironmentRunnerAuthorizations SET Status = @status, Etag = @etag, Document = @doc WHERE Environment = @env AND RunnerId = @runner;";
        update.Parameters.AddWithValue("@status", RunnerAuthorizationStatusNames.ToWire(decision.Status));
        update.Parameters.AddWithValue("@etag", etag.Value!);
        using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(json.AsMemory());
        update.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
        update.Parameters.AddWithValue("@env", environment);
        update.Parameters.AddWithValue("@runner", runnerId);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentRunnerAuthorization>(json);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    // Appends the shared list filters (status / environment) and the approver-inbox filter (Environment IN the administered
    // set) — server-derived strings reified as @-parameters (the SQL leaf). The administered set is never empty here (the
    // handler short-circuits a caller who administers nothing to an empty page before the store), but a null set (the
    // non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, SqlCommand command, RunnerAuthorizationQuery query)
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

    private static async ValueTask<byte[]?> DocumentAsync(SqlConnection connection, string environment, string runnerId, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM EnvironmentRunnerAuthorizations WHERE Environment = @env AND RunnerId = @runner;";
        select.Parameters.AddWithValue("@env", environment);
        select.Parameters.AddWithValue("@runner", runnerId);
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
        IF OBJECT_ID(N'EnvironmentRunnerAuthorizations', N'U') IS NULL
        BEGIN
            CREATE TABLE EnvironmentRunnerAuthorizations (
                Environment NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                RunnerId NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                Status NVARCHAR(64) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_EnvironmentRunnerAuthorizations PRIMARY KEY (Environment, RunnerId)
            );
            CREATE INDEX IX_EnvironmentRunnerAuthorizations_Status ON EnvironmentRunnerAuthorizations (Status);
        END;
        """;
}