// <copyright file="MySqlAccessRequestStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IAccessRequestStore"/> — access requests (design §16.5) persisted relationally. Each
/// request is stored as its <see cref="AccessRequest"/> schema document in a <c>LONGBLOB</c> column, with the
/// filterable fields (status, target workflow, subject) and the etag mirrored into columns for querying and the
/// optimistic-concurrency check.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class MySqlAccessRequestStore : IAccessRequestStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlAccessRequestStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A MySqlConnector connection string for a role permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        await using var connection = new MySqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the schema over a caller-supplied data source.</summary>
    /// <param name="dataSource">A MySqlConnector data source whose credential is permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the schema exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(MySqlDataSource dataSource, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        await using MySqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await ProvisionAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned schema.</summary>
    /// <param name="connectionString">A MySqlConnector connection string.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the data source it creates).</returns>
    public static ValueTask<MySqlAccessRequestStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlAccessRequestStore>(
            new MySqlAccessRequestStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlAccessRequestStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlAccessRequestStore>(
            new MySqlAccessRequestStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>> CreateAsync(AccessRequest draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "req-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the LONGBLOB parameter
        // (MySqlConnector carries the exact length for a ReadOnlyMemory blob — no GC document array, no second copy). The
        // document is returned on success, disposed on failure.
        ParsedJsonDocument<AccessRequest> doc = AccessRequestSerialization.SerializeNewDoc(id, draft, actor, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using MySqlCommand insert = connection.CreateCommand();
            insert.CommandText =
                "INSERT INTO AccessRequests (Id, BaseWorkflowId, SubjectClaimType, SubjectClaimValue, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @bw, @st, @sv, @status, @createdAt, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@bw", draft.BaseWorkflowIdValue);
            insert.Parameters.AddWithValue("@st", draft.SubjectClaimTypeValue);
            insert.Parameters.AddWithValue("@sv", draft.SubjectClaimValueValue);
            insert.Parameters.AddWithValue("@status", AccessRequestStatusNames.Pending);
            insert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", utf8);
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
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<AccessRequest>> ListAsync(AccessRequestQuery query, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<AccessRequest>();
        await using MySqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM AccessRequests");
        var conditions = new List<string>(4);
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            select.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId.IsNotUndefined())
        {
            conditions.Add("BaseWorkflowId = @bw");
            select.Parameters.AddWithValue("@bw", (string)query.BaseWorkflowId);
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            conditions.Add("SubjectClaimType = @st");
            select.Parameters.AddWithValue("@st", subjectType);
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            conditions.Add("SubjectClaimValue = @sv");
            select.Parameters.AddWithValue("@sv", subjectValue);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY CreatedAt, Id;");
        select.CommandText = sql.ToString();
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<AccessRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<AccessRequestPage> ListAsync(AccessRequestQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : AccessRequestPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the MySqlConnector predicate needs (a genuine
        // DB-param leaf) only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the
        // token's UTC ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(AccessRequestContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (AccessRequestContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM AccessRequests");
        var conditions = new List<string>(5);
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            select.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId.IsNotUndefined())
        {
            conditions.Add("BaseWorkflowId = @bw");
            select.Parameters.AddWithValue("@bw", (string)query.BaseWorkflowId);
        }

        if (query.SubjectClaimType is { } subjectType)
        {
            conditions.Add("SubjectClaimType = @st");
            select.Parameters.AddWithValue("@st", subjectType);
        }

        if (query.SubjectClaimValue is { } subjectValue)
        {
            conditions.Add("SubjectClaimValue = @sv");
            select.Parameters.AddWithValue("@sv", subjectValue);
        }

        if (cursorCreatedAt is not null)
        {
            // Keyset seek strictly past (createdAt, id): CreatedAt is the fixed-width ISO-8601 "o" UTC form (ordinal ==
            // chronological), and Id is declared COLLATE utf8mb4_bin so its compare is byte-ordinal == the in-memory pager's.
            conditions.Add("(CreatedAt > @ca OR (CreatedAt = @ca AND Id > @id))");
            select.Parameters.AddWithValue("@ca", cursorCreatedAt);
            select.Parameters.AddWithValue("@id", cursorId!);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        // The IX_AccessRequests_Created index on (CreatedAt, Id) drives both the order and the seek; LIMIT bounds the read
        // to one page + 1 (lookahead) — never a full read + parse of the whole queue.
        sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
        select.Parameters.AddWithValue("@limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<AccessRequest>(pageSize);
        try
        {
            bool hasMore = false;
            await using (MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (page.Count == pageSize)
                    {
                        hasMore = true; // the (pageSize+1)th row exists → a next page; don't parse it
                        break;
                    }

                    page.Add(ParsedJsonDocument<AccessRequest>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                }
            }

            if (!hasMore)
            {
                return AccessRequestPage.Create(page);
            }

            AccessRequest last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return AccessRequestPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<AccessRequest>?> DecideAsync(string id, AccessRequestDecision decision, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (doc is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // merged result into the pooled buffer the returned document owns — bound as the LONGBLOB parameter (no GC array, no copy).
        using ParsedJsonDocument<AccessRequest> current = ParsedJsonDocument<AccessRequest>.Parse(doc.AsMemory());
        ParsedJsonDocument<AccessRequest> updated = AccessRequestSerialization.SerializeDecisionDoc(current.RootElement, id, expectedEtag, decision, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using MySqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE AccessRequests SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", AccessRequestStatusNames.ToWire(decision.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", utf8);
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
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static async ValueTask ProvisionAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> DocumentAsync(MySqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM AccessRequests WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS AccessRequests (
            Id VARCHAR(255) COLLATE utf8mb4_bin NOT NULL PRIMARY KEY,
            BaseWorkflowId VARCHAR(255) NOT NULL,
            SubjectClaimType VARCHAR(255) NOT NULL,
            SubjectClaimValue VARCHAR(255) NOT NULL,
            Status VARCHAR(64) NOT NULL,
            CreatedAt VARCHAR(33) NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            INDEX IX_AccessRequests_Status (Status),
            INDEX IX_AccessRequests_Workflow (BaseWorkflowId),
            INDEX IX_AccessRequests_Subject (SubjectClaimType, SubjectClaimValue),
            INDEX IX_AccessRequests_Created (CreatedAt, Id)
        );
        """;
}