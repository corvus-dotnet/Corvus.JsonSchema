// <copyright file="SqliteAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IAvailabilityRequestStore"/> — availability ("promotion") requests (design §7.8) persisted
/// for a single-file / embedded host. Each request is stored as its <see cref="AvailabilityRequest"/> schema document in a
/// BLOB column, with the filterable fields (status, target environment, requester) and the etag mirrored into columns for
/// querying and the optimistic-concurrency check. Mirrors <see cref="SqliteAccessRequestStore"/>, parameterised by
/// environment.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through it, as the other SQLite stores do.</remarks>
public sealed class SqliteAvailabilityRequestStore : IAvailabilityRequestStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteAvailabilityRequestStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an availability-request store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=requests.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteAvailabilityRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteAvailabilityRequestStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = "areq-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            WorkflowEtag etag = NewEtag();
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            byte[] json = AvailabilityRequestSerialization.SerializeNew(id, draft, actor, now, etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO AvailabilityRequests (Id, Environment, CreatedBy, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @env, @by, @status, @createdAt, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
            insert.Parameters.AddWithValue("@by", actor);
            insert.Parameters.AddWithValue("@status", AvailabilityRequestStatusNames.Pending);
            insert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<AvailabilityRequest>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new PooledDocumentList<AvailabilityRequest>();
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM AvailabilityRequests");
            var conditions = new List<string>(4);
            AppendFilters(conditions, select, query);

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY CreatedAt, Id;");
            select.CommandText = sql.ToString();
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AvailabilityRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the ADO predicate needs (a genuine DB-param leaf)
        // only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's UTC
        // ticks), id as its text. Undefined token = first page; a malformed token throws FormatException.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AvailabilityRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AvailabilityRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
                {
                    cursorCreatedAt = new DateTime(cursorTicks, DateTimeKind.Utc).ToString("o", CultureInfo.InvariantCulture);
                    cursorId = Encoding.UTF8.GetString(cursorIdUtf8);
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
            var sql = new StringBuilder("SELECT Document FROM AvailabilityRequests");
            var conditions = new List<string>(5);
            AppendFilters(conditions, select, query);

            if (cursorCreatedAt is not null)
            {
                // Keyset seek strictly past (createdAt, id): CreatedAt is the fixed-width ISO-8601 "o" UTC form so its
                // ordinal/lexicographic order is chronological, and Id is the TEXT primary key (SQLite BINARY collation ==
                // ordinal byte order == the in-memory pager's id span compare).
                conditions.Add("(CreatedAt > @ca OR (CreatedAt = @ca AND Id > @id))");
                select.Parameters.AddWithValue("@ca", cursorCreatedAt);
                select.Parameters.AddWithValue("@id", cursorId!);
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            // ORDER BY the keyset and LIMIT one beyond the page (lookahead); ORDER BY drives the bounded read, never a full
            // read + re-parse of the whole inbox.
            sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            select.CommandText = sql.ToString();

            var page = new PooledDocumentList<AvailabilityRequest>(pageSize);
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

                        page.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return AvailabilityRequestPage.Create(page);
                }

                AvailabilityRequest last = page[page.Count - 1];
                using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
                return AvailabilityRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
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
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
            byte[] json = AvailabilityRequestSerialization.SerializeDecision(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE AvailabilityRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", AvailabilityRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<AvailabilityRequest>(json);
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

    // Appends the shared list filters (status / environment / requester) and the approver-inbox filter (Environment IN the
    // administered set) — server-derived strings reified as @-parameters (the SQL leaf). The administered set is never empty
    // here (the handler short-circuits a caller who administers nothing to an empty page before the store), but a null set
    // (the non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, SqliteCommand command, AvailabilityRequestQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("@status", AvailabilityRequestStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("@env", environment);
        }

        if (query.CreatedBy is { } createdBy)
        {
            conditions.Add("CreatedBy = @by");
            command.Parameters.AddWithValue("@by", createdBy);
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

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AvailabilityRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS AvailabilityRequests (
            Id TEXT NOT NULL PRIMARY KEY,
            Environment TEXT NOT NULL,
            CreatedBy TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_Status ON AvailabilityRequests (Status);
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_Environment ON AvailabilityRequests (Environment);
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_CreatedBy ON AvailabilityRequests (CreatedBy);
        """;
}