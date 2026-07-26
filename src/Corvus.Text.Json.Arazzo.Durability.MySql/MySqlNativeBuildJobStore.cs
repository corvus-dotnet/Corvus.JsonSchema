// <copyright file="MySqlNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055) persisted relationally. Each job is
/// stored as its <see cref="NativeBuildJob"/> schema document in a <c>LONGBLOB</c> column, with the filterable fields (status,
/// target tuple) and the etag mirrored into columns for querying and the optimistic-concurrency check. The id is derived from
/// the target tuple, so <see cref="EnqueueAsync"/> is an idempotent <c>INSERT ... ON DUPLICATE KEY UPDATE</c> upsert. Mirrors
/// <see cref="MySqlAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent; the claim is a single
/// transaction that locks the oldest queued row with <c>FOR UPDATE SKIP LOCKED</c>, so two workers never claim the same job.</remarks>
public sealed class MySqlNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlNativeBuildJobStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlNativeBuildJobStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlNativeBuildJobStore>(
            new MySqlNativeBuildJobStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlNativeBuildJobStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlNativeBuildJobStore>(
            new MySqlNativeBuildJobStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the LONGBLOB parameter
        // (no GC document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<NativeBuildJob> doc = NativeBuildJobSerialization.SerializeNewDoc(id, draft, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using MySqlCommand upsert = connection.CreateCommand();

            // Idempotent per target: the id is derived from the tuple, so a repeated enqueue overwrites the same row — an
            // upsert that resets the job to Queued (SerializeNewDoc omits startedAt/completedAt/failureReason/claimedBy).
            upsert.CommandText =
                "INSERT INTO NativeBuildJobs (Id, BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @base, @ver, @env, @rid, @status, @createdAt, @etag, @doc) " +
                "ON DUPLICATE KEY UPDATE BaseWorkflowId = VALUES(BaseWorkflowId), VersionNumber = VALUES(VersionNumber), " +
                "Environment = VALUES(Environment), RuntimeIdentifier = VALUES(RuntimeIdentifier), Status = VALUES(Status), " +
                "CreatedAt = VALUES(CreatedAt), Etag = VALUES(Etag), Document = VALUES(Document);";
            upsert.Parameters.AddWithValue("@id", id);
            upsert.Parameters.AddWithValue("@base", draft.BaseWorkflowIdValue);
            upsert.Parameters.AddWithValue("@ver", draft.VersionNumberValue);
            upsert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
            upsert.Parameters.AddWithValue("@rid", draft.RuntimeIdentifierValue);
            upsert.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.Queued);
            upsert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            upsert.Parameters.AddWithValue("@etag", etag.Value!);
            upsert.Parameters.AddWithValue("@doc", utf8);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Lock the single oldest Queued row (oldest-first by (CreatedAt, Id)) with FOR UPDATE SKIP LOCKED: a concurrent claim
        // skips this locked row and takes the next, so two workers never claim the same job.
        string? id = null;
        byte[]? document = null;
        await using (MySqlCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT Id, Document FROM NativeBuildJobs WHERE Status = @queued ORDER BY CreatedAt, Id LIMIT 1 FOR UPDATE SKIP LOCKED;";
            select.Parameters.AddWithValue("@queued", NativeBuildJobStatusNames.Queued);
            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                id = reader.GetString(0);
                document = reader.GetFieldValue<byte[]>(1);
            }
        }

        if (id is null)
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            return null;
        }

        // Parse the locked row NON-COPYING, stamp the Building transition, and write it back within the same transaction.
        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(document!.AsMemory());
        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<NativeBuildJob> claimed = NativeBuildJobSerialization.SerializeClaimedDoc(current.RootElement, claimedBy, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(claimed.RootElement).Memory;
            await using (MySqlCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE NativeBuildJobs SET Status = @building, Etag = @etag, Document = @doc WHERE Id = @k;";
                update.Parameters.AddWithValue("@building", NativeBuildJobStatusNames.Building);
                update.Parameters.AddWithValue("@etag", etag.Value!);
                update.Parameters.AddWithValue("@doc", utf8);
                update.Parameters.AddWithValue("@k", id);
                await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return claimed;
        }
        catch
        {
            claimed.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // Parse the existing document NON-COPYING over the driver's array (the read leaf); it must be Building, then the etag
        // is checked and the completed record serialized into the pooled buffer the returned document owns.
        using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(existing.AsMemory());
        if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
        {
            throw new NativeBuildJobStateException(id, $"The native build job '{id}' cannot be completed because it is not building.");
        }

        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<NativeBuildJob> updated = NativeBuildJobSerialization.SerializeCompletionDoc(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using MySqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE NativeBuildJobs SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.ToWire(completion.Status));
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
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<NativeBuildJob>();
        await using MySqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM NativeBuildJobs");
        var conditions = new List<string>(5);
        AppendFilters(conditions, select, query);

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY CreatedAt, Id;");
        select.CommandText = sql.ToString();
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<NativeBuildJob>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native indexed point read on the derived id (the target tuple maps to a unique id) — never a scan.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Status FROM NativeBuildJobs WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string status && string.Equals(status, NativeBuildJobStatusNames.Ready, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the MySqlConnector predicate needs (a genuine
        // DB-param leaf) only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the
        // token's UTC ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(NativeBuildJobContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (NativeBuildJobContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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
        var sql = new StringBuilder("SELECT Document FROM NativeBuildJobs");
        var conditions = new List<string>(6);
        AppendFilters(conditions, select, query);

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

        // The IX_NativeBuildJobs_Created index on (CreatedAt, Id) drives both the order and the seek; LIMIT bounds the read
        // to one page + 1 (lookahead) — never a full read + parse of the whole queue.
        sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
        select.Parameters.AddWithValue("@limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<NativeBuildJob>(pageSize);
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

                    page.Add(ParsedJsonDocument<NativeBuildJob>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                }
            }

            if (!hasMore)
            {
                return NativeBuildJobPage.Create(page);
            }

            NativeBuildJob last = page[page.Count - 1];
            using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
            return NativeBuildJobPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
        }
        catch
        {
            page.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        var conditions = new List<string>(5);
        AppendFilters(conditions, select, query);

        // Bounded count: COUNT over a subquery capped at cap + 1, so the scan stops one row past the cap; the (cap+1)th
        // row's existence trips Capped — never a full COUNT of the whole queue. Same predicate as the list.
        var inner = new StringBuilder("SELECT 1 FROM NativeBuildJobs");
        if (conditions.Count > 0)
        {
            inner.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        inner.Append(" LIMIT @cap");
        select.Parameters.AddWithValue("@cap", cap + 1);
        select.CommandText = "SELECT COUNT(*) FROM (" + inner + ") AS bounded;";
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        long total = result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        return total > cap ? (cap, true) : ((int)total, false);
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

    // Appends the shared list filters (status / target tuple) as @-parameters; a null criterion adds nothing.
    private static void AppendFilters(List<string> conditions, MySqlCommand command, NativeBuildJobQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            conditions.Add("BaseWorkflowId = @base");
            command.Parameters.AddWithValue("@base", baseWorkflowId);
        }

        if (query.VersionNumber is { } versionNumber)
        {
            conditions.Add("VersionNumber = @ver");
            command.Parameters.AddWithValue("@ver", versionNumber);
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("@env", environment);
        }

        if (query.RuntimeIdentifier is { } runtimeIdentifier)
        {
            conditions.Add("RuntimeIdentifier = @rid");
            command.Parameters.AddWithValue("@rid", runtimeIdentifier);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> DocumentAsync(MySqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM NativeBuildJobs WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS NativeBuildJobs (
            Id VARCHAR(255) COLLATE utf8mb4_bin NOT NULL PRIMARY KEY,
            BaseWorkflowId VARCHAR(255) NOT NULL,
            VersionNumber INT NOT NULL,
            Environment VARCHAR(255) NOT NULL,
            RuntimeIdentifier VARCHAR(255) NOT NULL,
            Status VARCHAR(64) NOT NULL,
            CreatedAt VARCHAR(33) NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            INDEX IX_NativeBuildJobs_Status (Status),
            INDEX IX_NativeBuildJobs_Target (BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier),
            INDEX IX_NativeBuildJobs_Queue (Status, CreatedAt, Id),
            INDEX IX_NativeBuildJobs_Created (CreatedAt, Id)
        );
        """;
}