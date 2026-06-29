// <copyright file="SqlServerAvailabilityRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Data;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IAvailabilityRequestStore"/> — availability ("promotion") requests (design §7.8)
/// persisted relationally. Each request is stored as its <see cref="AvailabilityRequest"/> schema document in a
/// <c>varbinary(max)</c> column, with the filterable fields (status, target environment, requester) and the etag mirrored
/// into columns for querying and the optimistic-concurrency check. The creation instant is held sortably (ISO-8601
/// round-trip text) so the contract's oldest-first ordering is a plain <c>ORDER BY</c>. Mirrors
/// <see cref="SqlServerAccessRequestStore"/>, parameterised by environment.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class SqlServerAvailabilityRequestStore : IAvailabilityRequestStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerAvailabilityRequestStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerAvailabilityRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerAvailabilityRequestStore>(new SqlServerAvailabilityRequestStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AvailabilityRequest>> CreateAsync(AvailabilityRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "areq-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; stream its exact bytes as the VARBINARY(MAX)
        // parameter (no GC document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<AvailabilityRequest> doc = AvailabilityRequestSerialization.SerializeNewDoc(id, draft, actor, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO AvailabilityRequests (Id, Environment, CreatedBy, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @env, @by, @status, @createdAt, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
            insert.Parameters.AddWithValue("@by", actor);
            insert.Parameters.AddWithValue("@status", AvailabilityRequestStatusNames.Pending);
            insert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
            insert.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AvailabilityRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AvailabilityRequest>> ListAsync(AvailabilityRequestQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<AvailabilityRequest>();
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM AvailabilityRequests");
            var conditions = new List<string>(4);
            AppendFilters(conditions, select, query);

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY CreatedAt, Id;");
            select.CommandText = sql.ToString();
            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                list.Add(ParsedJsonDocument<AvailabilityRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
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
    public async ValueTask<AvailabilityRequestPage> ListAsync(AvailabilityRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AvailabilityRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the SqlClient predicate needs (a genuine DB-param
        // leaf) only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's
        // UTC ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
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

        var page = new PooledDocumentList<AvailabilityRequest>(pageSize);
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT TOP (@limit) Document FROM AvailabilityRequests");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            var conditions = new List<string>(5);
            AppendFilters(conditions, select, query);

            if (cursorCreatedAt is not null)
            {
                // Keyset seek strictly past (createdAt, id): CreatedAt is the fixed-width ISO-8601 "o" UTC form (ordinal ==
                // chronological), and Id is declared COLLATE Latin1_General_BIN2 so its compare is byte-ordinal == the
                // in-memory pager's.
                conditions.Add("(CreatedAt > @ca OR (CreatedAt = @ca AND Id > @id))");
                select.Parameters.AddWithValue("@ca", cursorCreatedAt);
                select.Parameters.AddWithValue("@id", cursorId!);
            }

            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            // The IX_AvailabilityRequests_Created index on (CreatedAt, Id) drives both the order and the seek; TOP bounds the
            // read to one page + 1 (lookahead) — never a full read + parse of the whole queue.
            sql.Append(" ORDER BY CreatedAt, Id;");
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // decided record into the pooled buffer the returned document owns — streamed as the parameter (no GC array, no copy).
        using ParsedJsonDocument<AvailabilityRequest> current = ParsedJsonDocument<AvailabilityRequest>.Parse(existing.AsMemory());
        ParsedJsonDocument<AvailabilityRequest> updated = AvailabilityRequestSerialization.SerializeDecisionDoc(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using SqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE AvailabilityRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", AvailabilityRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
            update.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
            update.Parameters.AddWithValue("@k", id);
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
    public ValueTask DisposeAsync() => default;

    // Appends the shared list filters (status / environment / requester) and the approver-inbox filter (Environment IN the
    // administered set) — server-derived strings reified as @-parameters (the SQL leaf). The administered set is never empty
    // here (the handler short-circuits a caller who administers nothing to an empty page before the store), but a null set
    // (the non-inbox modes) adds nothing.
    private static void AppendFilters(List<string> conditions, SqlCommand command, AvailabilityRequestQuery query)
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

    private static async ValueTask<byte[]?> DocumentAsync(SqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AvailabilityRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
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
        IF OBJECT_ID(N'AvailabilityRequests', N'U') IS NULL
        BEGIN
            CREATE TABLE AvailabilityRequests (
                Id NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY,
                Environment NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                CreatedBy NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                Status NVARCHAR(64) NOT NULL,
                CreatedAt NVARCHAR(33) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
            CREATE INDEX IX_AvailabilityRequests_Status ON AvailabilityRequests (Status);
            CREATE INDEX IX_AvailabilityRequests_Environment ON AvailabilityRequests (Environment);
            CREATE INDEX IX_AvailabilityRequests_CreatedBy ON AvailabilityRequests (CreatedBy);
            CREATE INDEX IX_AvailabilityRequests_Created ON AvailabilityRequests (CreatedAt, Id);
        END;
        """;
}