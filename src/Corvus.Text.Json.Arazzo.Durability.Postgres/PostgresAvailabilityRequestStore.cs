// <copyright file="PostgresAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IAvailabilityRequestStore"/> — availability ("promotion") requests (design §7.8)
/// persisted relationally. Each request is stored as its <see cref="AvailabilityRequest"/> schema document in a
/// <c>bytea</c> column, with the filterable fields (status, target environment, requester) and the etag mirrored into
/// columns for querying and the optimistic-concurrency check. Mirrors <see cref="PostgresAccessRequestStore"/>,
/// parameterised by environment.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class PostgresAvailabilityRequestStore : IAvailabilityRequestStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresAvailabilityRequestStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresAvailabilityRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresAvailabilityRequestStore>(
            new PostgresAvailabilityRequestStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresAvailabilityRequestStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresAvailabilityRequestStore>(
            new PostgresAvailabilityRequestStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "areq-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes via ReadOnlyMemory as the
        // BYTEA parameter (no GC document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<AvailabilityRequest> doc = AvailabilityRequestSerialization.SerializeNewDoc(id, draft, actor, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO AvailabilityRequests (Id, Environment, CreatedBy, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @env, @by, @status, @createdAt, @etag, @doc);";
            insert.Parameters.AddWithValue("id", id);
            insert.Parameters.AddWithValue("env", draft.EnvironmentValue);
            insert.Parameters.AddWithValue("by", actor);
            insert.Parameters.AddWithValue("status", AvailabilityRequestStatusNames.Pending);
            insert.Parameters.AddWithValue("createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("etag", etag.Value!);
            insert.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<AvailabilityRequest>();
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM AvailabilityRequests");
        var conditions = new List<string>(4);
        AppendFilters(conditions, select, query);

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY CreatedAt, Id;");
        select.CommandText = sql.ToString();
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AvailabilityRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Npgsql predicate needs (a genuine DB-param leaf)
        // only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's UTC
        // ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM AvailabilityRequests");
        var conditions = new List<string>(5);
        AppendFilters(conditions, select, query);

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): CreatedAt is the fixed-width ISO-8601 "o" UTC form (ordinal ==
            // chronological), and Id is declared COLLATE "C" so its compare is byte-ordinal == the in-memory pager's.
            conditions.Add("(CreatedAt > @ca OR (CreatedAt = @ca AND Id > @id))");
            select.Parameters.AddWithValue("ca", cursorCreatedAt);
            select.Parameters.AddWithValue("id", cursorId!);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        // The IX_AvailabilityRequests_Created index on (CreatedAt, Id) drives both the order and the seek; LIMIT bounds the
        // read to one page + 1 (lookahead) — never a full read + parse of the whole queue.
        sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
        select.Parameters.AddWithValue("limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<AvailabilityRequest>(pageSize);
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

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>?> DecideAsync(string id, AvailabilityRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // decided result into the pooled buffer the returned document owns — bound via ReadOnlyMemory (no GC array, no copy).
        using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(existing.AsMemory());
        ParsedJsonDocument<AvailabilityRequest> updated = AvailabilityRequestSerialization.SerializeDecisionDoc(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using NpgsqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE AvailabilityRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("status", AvailabilityRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("etag", etag.Value!);
            update.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
            update.Parameters.AddWithValue("k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Appends the shared list filters (status / environment / requester) and the approver-inbox filter (design §7.8):
    // Environment IN (the administered set) — server-derived strings reified as @-parameters (the SQL leaf). The administered
    // set is never empty here (the handler short-circuits a caller who administers nothing to an empty page before the store);
    // a null set (the non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, NpgsqlCommand command, AvailabilityRequestQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("status", AvailabilityRequestStatusNames.ToWire(status));
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("env", environment);
        }

        if (query.CreatedBy is { } createdBy)
        {
            conditions.Add("CreatedBy = @by");
            command.Parameters.AddWithValue("by", createdBy);
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

    private static async ValueTask<byte[]?> DocumentAsync(NpgsqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AvailabilityRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("k", id);
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
        CREATE TABLE IF NOT EXISTS AvailabilityRequests (
            Id TEXT COLLATE "C" NOT NULL PRIMARY KEY,
            Environment TEXT NOT NULL,
            CreatedBy TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_Status ON AvailabilityRequests (Status);
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_Environment ON AvailabilityRequests (Environment);
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_CreatedBy ON AvailabilityRequests (CreatedBy);
        CREATE INDEX IF NOT EXISTS IX_AvailabilityRequests_Created ON AvailabilityRequests (CreatedAt, Id);
        """;
}