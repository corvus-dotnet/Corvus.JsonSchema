// <copyright file="SqliteNativeBuildJobStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="INativeBuildJobStore"/> — Native-AOT build jobs (ADR 0055) persisted for a single-file /
/// embedded host. Each job is stored as its <see cref="NativeBuildJob"/> schema document in a BLOB column, with the filterable
/// fields (status, target tuple) and the etag mirrored into columns for querying and the optimistic-concurrency check. The id
/// is derived from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent <c>INSERT ... ON CONFLICT</c> upsert.
/// Mirrors <see cref="SqliteAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through a single-writer gate, as the other SQLite
/// stores do; the claim is a plain select-oldest-queued then update under that gate (no <c>SKIP LOCKED</c> needed), so two
/// workers never claim the same job.</remarks>
public sealed class SqliteNativeBuildJobStore : INativeBuildJobStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteNativeBuildJobStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens a native-build-job store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=jobs.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteNativeBuildJobStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteNativeBuildJobStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>> EnqueueAsync(NativeBuildJob draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Idempotent per target: the id is derived from the tuple, so a repeated enqueue overwrites the same row — an
            // upsert that resets the job to Queued (SerializeNew omits startedAt/completedAt/failureReason/claimedBy).
            string id = NativeBuildJob.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
            WorkflowEtag etag = NewEtag();
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            byte[] json = NativeBuildJobSerialization.SerializeNew(id, draft, now, etag);
            using SqliteCommand upsert = this.connection.CreateCommand();
            upsert.CommandText =
                "INSERT INTO NativeBuildJobs (Id, BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier, Status, CreatedAt, Etag, Document) " +
                "VALUES (@id, @base, @ver, @env, @rid, @status, @createdAt, @etag, @doc) " +
                "ON CONFLICT(Id) DO UPDATE SET BaseWorkflowId = excluded.BaseWorkflowId, VersionNumber = excluded.VersionNumber, " +
                "Environment = excluded.Environment, RuntimeIdentifier = excluded.RuntimeIdentifier, Status = excluded.Status, " +
                "CreatedAt = excluded.CreatedAt, Etag = excluded.Etag, Document = excluded.Document;";
            upsert.Parameters.AddWithValue("@id", id);
            upsert.Parameters.AddWithValue("@base", draft.BaseWorkflowIdValue);
            upsert.Parameters.AddWithValue("@ver", draft.VersionNumberValue);
            upsert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
            upsert.Parameters.AddWithValue("@rid", draft.RuntimeIdentifierValue);
            upsert.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.Queued);
            upsert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            upsert.Parameters.AddWithValue("@etag", etag.Value!);
            upsert.Parameters.AddWithValue("@doc", json);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> ClaimNextQueuedAsync(string claimedBy, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The gate serialises every operation (single-writer), so a plain select-oldest-queued then update is atomic: no
            // SKIP LOCKED is needed because no other operation can interleave. Two workers never claim the same job.
            string? id = null;
            byte[]? document = null;
            using (SqliteCommand select = this.connection.CreateCommand())
            {
                select.CommandText = "SELECT Id, Document FROM NativeBuildJobs WHERE Status = @queued ORDER BY CreatedAt, Id LIMIT 1;";
                select.Parameters.AddWithValue("@queued", NativeBuildJobStatusNames.Queued);
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    id = reader.GetString(0);
                    document = reader.GetFieldValue<byte[]>(1);
                }
            }

            if (id is null)
            {
                return null;
            }

            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(document!.AsMemory());
            WorkflowEtag etag = NewEtag();
            byte[] claimed = NativeBuildJobSerialization.SerializeClaimed(current.RootElement, claimedBy, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE NativeBuildJobs SET Status = @building, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@building", NativeBuildJobStatusNames.Building);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", claimed);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<NativeBuildJob>(claimed);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<NativeBuildJob>?> CompleteAsync(string id, NativeBuildJobCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            using ParsedJsonDocument<NativeBuildJob> current = ParsedJsonDocument<NativeBuildJob>.Parse(doc.AsMemory());
            if (!current.RootElement.HasStatus(NativeBuildJobStatus.Building))
            {
                throw new NativeBuildJobStateException(id, $"The native build job '{id}' cannot be completed because it is not building.");
            }

            WorkflowEtag etag = NewEtag();
            byte[] json = NativeBuildJobSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE NativeBuildJobs SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", NativeBuildJobStatusNames.ToWire(completion.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<NativeBuildJob>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<NativeBuildJob>> ListAsync(NativeBuildJobQuery query, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new PooledDocumentList<NativeBuildJob>();
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM NativeBuildJobs");
            var conditions = new List<string>(5);
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
                list.Add(ParsedJsonDocument<NativeBuildJob>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsTargetReadyAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A native indexed point read on the derived id (the target tuple maps to a unique id) — never a scan.
            string id = NativeBuildJob.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Status FROM NativeBuildJobs WHERE Id = @k;";
            select.Parameters.AddWithValue("@k", id);
            object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is string status && string.Equals(status, NativeBuildJobStatusNames.Ready, StringComparison.Ordinal);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<NativeBuildJobPage> ListAsync(NativeBuildJobQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : NativeBuildJobPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the ADO predicate needs (a genuine DB-param leaf)
        // only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's UTC
        // ticks), id as its text. Undefined token = first page; a malformed token throws FormatException.
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM NativeBuildJobs");
            var conditions = new List<string>(6);
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
            // read + re-parse of the whole queue.
            sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            select.CommandText = sql.ToString();

            var page = new PooledDocumentList<NativeBuildJob>(pageSize);
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
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(NativeBuildJobQuery query, int cap, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
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
            select.CommandText = "SELECT COUNT(*) FROM (" + inner + ");";
            object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            long total = result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
            return total > cap ? (cap, true) : ((int)total, false);
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

    // Appends the shared list filters (status / target tuple) as @-parameters; a null criterion adds nothing.
    private static void AppendFilters(List<string> conditions, SqliteCommand command, NativeBuildJobQuery query)
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

    private async ValueTask<byte[]?> DocumentAsync(string id, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM NativeBuildJobs WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS NativeBuildJobs (
            Id TEXT NOT NULL PRIMARY KEY,
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            Environment TEXT NOT NULL,
            RuntimeIdentifier TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_NativeBuildJobs_Status ON NativeBuildJobs (Status);
        CREATE INDEX IF NOT EXISTS IX_NativeBuildJobs_Target ON NativeBuildJobs (BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier);
        CREATE INDEX IF NOT EXISTS IX_NativeBuildJobs_Queue ON NativeBuildJobs (Status, CreatedAt, Id);
        """;
}