// <copyright file="SqlServerNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Data;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055) persisted relationally. Each job
/// is stored as its <see cref="NativeBuildJob"/> schema document in a <c>varbinary(max)</c> column, with the filterable fields
/// (status, target tuple) and the etag mirrored into columns for querying and the optimistic-concurrency check. The creation
/// instant is held sortably (ISO-8601 round-trip text) so the oldest-first ordering is a plain <c>ORDER BY</c>. The id is
/// derived from the target tuple, so <see cref="EnqueueAsync"/> is a race-safe <c>MERGE</c> upsert. Mirrors
/// <see cref="SqlServerAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent; the claim is a single transaction
/// that reads the oldest queued row <c>WITH (UPDLOCK, READPAST, ROWLOCK)</c>, so two workers never claim the same job.</remarks>
public sealed class SqlServerNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerNativeBuildJobStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerNativeBuildJobStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerNativeBuildJobStore>(new SqlServerNativeBuildJobStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once to owned bytes; MERGE references the document in both branches, so bind it as a re-usable byte[]
        // VARBINARY value (not a forward-only stream). The pooled return document is built over the same bytes.
        byte[] json = NativeBuildJobSerialization.SerializeNew(id, draft, now, etag);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand upsert = connection.CreateCommand();

        // Idempotent per target: the id is derived from the tuple, so a repeated enqueue overwrites the same row via a
        // race-safe MERGE that resets the job to Queued (SerializeNew omits startedAt/completedAt/failureReason/claimedBy).
        upsert.CommandText =
            """
            MERGE NativeBuildJobs WITH (HOLDLOCK) AS target
            USING (SELECT @id AS Id) AS source
            ON target.Id = source.Id
            WHEN MATCHED THEN
                UPDATE SET BaseWorkflowId = @base, VersionNumber = @ver, Environment = @env, RuntimeIdentifier = @rid,
                    Status = @status, CreatedAt = @createdAt, Etag = @etag, Document = @doc
            WHEN NOT MATCHED THEN
                INSERT (Id, BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier, Status, CreatedAt, Etag, Document)
                VALUES (@id, @base, @ver, @env, @rid, @status, @createdAt, @etag, @doc);
            """;
        upsert.Parameters.AddWithValue("@id", id);
        upsert.Parameters.AddWithValue("@base", draft.BaseWorkflowIdValue);
        upsert.Parameters.AddWithValue("@ver", draft.VersionNumberValue);
        upsert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
        upsert.Parameters.AddWithValue("@rid", draft.RuntimeIdentifierValue);
        upsert.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.Queued);
        upsert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
        upsert.Parameters.AddWithValue("@etag", etag.Value!);
        upsert.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = json });
        await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // Read the single oldest Queued row (oldest-first by (CreatedAt, Id)) WITH (UPDLOCK, READPAST, ROWLOCK): UPDLOCK holds
        // an update lock on the chosen row for the transaction and READPAST makes a concurrent claim skip it and take the
        // next, so two workers never claim the same job.
        string? id = null;
        byte[]? document = null;
        await using (SqlCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT TOP 1 Id, Document FROM NativeBuildJobs WITH (UPDLOCK, READPAST, ROWLOCK) WHERE Status = @queued ORDER BY CreatedAt, Id;";
            select.Parameters.AddWithValue("@queued", NativeBuildJobStatusNames.Queued);
            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
            await using (SqlCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE NativeBuildJobs SET Status = @building, Etag = @etag, Document = @doc WHERE Id = @k;";
                update.Parameters.AddWithValue("@building", NativeBuildJobStatusNames.Building);
                update.Parameters.AddWithValue("@etag", etag.Value!);
                using ReadOnlyMemoryStream docStream = ReadOnlyMemoryStream.Rent(utf8);
                update.Parameters.Add(new SqlParameter("@doc", SqlDbType.VarBinary, -1) { Value = docStream });
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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
            await using SqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE NativeBuildJobs SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.ToWire(completion.Status));
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
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<NativeBuildJob>();
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM NativeBuildJobs");
            var conditions = new List<string>(5);
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
                list.Add(ParsedJsonDocument<NativeBuildJob>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
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
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native indexed point read on the derived id (the target tuple maps to a unique id) — never a scan.
        string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Status FROM NativeBuildJobs WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string status && string.Equals(status, NativeBuildJobStatusNames.Ready, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the SqlClient predicate needs (a genuine DB-param
        // leaf) only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's
        // UTC ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
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

        var page = new PooledDocumentList<NativeBuildJob>(pageSize);
        try
        {
            await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using SqlCommand select = connection.CreateCommand();
            var sql = new StringBuilder("SELECT TOP (@limit) Document FROM NativeBuildJobs");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            var conditions = new List<string>(6);
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

            // The IX_NativeBuildJobs_Created index on (CreatedAt, Id) drives both the order and the seek; TOP bounds the read
            // to one page + 1 (lookahead) — never a full read + parse of the whole queue.
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        var conditions = new List<string>(5);
        AppendFilters(conditions, select, query);

        // Bounded count: COUNT over a TOP (@cap) subquery, so the scan stops one row past the cap; the (cap+1)th row's
        // existence trips Capped — never a full COUNT of the whole queue. Same predicate as the list. (SQL Server has no
        // LIMIT; TOP (@cap) with @cap = cap + 1 is the bound, and COUNT(*) returns int for counts this small.)
        var inner = new StringBuilder("SELECT TOP (@cap) 1 AS x FROM NativeBuildJobs");
        select.Parameters.AddWithValue("@cap", cap + 1);
        if (conditions.Count > 0)
        {
            inner.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        select.CommandText = "SELECT COUNT(*) FROM (" + inner + ") AS bounded;";
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        int total = result is int i ? i : Convert.ToInt32(result, CultureInfo.InvariantCulture);
        return total > cap ? (cap, true) : (total, false);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    // Appends the shared list filters (status / target tuple) as @-parameters; a null criterion adds nothing.
    private static void AppendFilters(List<string> conditions, SqlCommand command, NativeBuildJobQuery query)
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

    private static async ValueTask<byte[]?> DocumentAsync(SqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM NativeBuildJobs WHERE Id = @k;";
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
        IF OBJECT_ID(N'NativeBuildJobs', N'U') IS NULL
        BEGIN
            CREATE TABLE NativeBuildJobs (
                Id NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL PRIMARY KEY,
                BaseWorkflowId NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                VersionNumber INT NOT NULL,
                Environment NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                RuntimeIdentifier NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                Status NVARCHAR(64) NOT NULL,
                CreatedAt NVARCHAR(33) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL
            );
            CREATE INDEX IX_NativeBuildJobs_Status ON NativeBuildJobs (Status);
            CREATE INDEX IX_NativeBuildJobs_Target ON NativeBuildJobs (BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier);
            CREATE INDEX IX_NativeBuildJobs_Queue ON NativeBuildJobs (Status, CreatedAt, Id);
            CREATE INDEX IX_NativeBuildJobs_Created ON NativeBuildJobs (CreatedAt, Id);
        END;
        """;
}