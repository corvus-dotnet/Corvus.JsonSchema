// <copyright file="SqliteWorkflowDeploymentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IWorkflowDeploymentStore"/> — serverless deployments (ADR 0055) persisted for a single-file /
/// embedded host. Each deployment is stored as its <see cref="WorkflowDeployment"/> schema document in a BLOB column, with the
/// filterable fields (status, target tuple) and the etag mirrored into columns for querying and the optimistic-concurrency
/// check. The id is derived from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent <c>INSERT ... ON CONFLICT</c>
/// upsert. Mirrors <see cref="SqliteAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through a single-writer gate, as the other SQLite
/// stores do; the claim is a plain select-oldest-queued then update under that gate (no <c>SKIP LOCKED</c> needed), so two
/// workers never claim the same deployment.</remarks>
public sealed class SqliteWorkflowDeploymentStore : IWorkflowDeploymentStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkflowDeploymentStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens a workflow-deployment store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=deployments.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkflowDeploymentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteWorkflowDeploymentStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(WorkflowDeployment draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Idempotent per target: the id is derived from the tuple, so a repeated enqueue overwrites the same row — an
            // upsert that resets the deployment to Queued (SerializeNew omits startedAt/completedAt/failureReason/functionUrl/
            // claimedBy). A (re-)enqueue resets the deployment to Queued, so its lease is cleared (NULL); a Queued deployment
            // holds no lease.
            string id = WorkflowDeployment.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
            WorkflowEtag etag = NewEtag();
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            byte[] json = WorkflowDeploymentSerialization.SerializeNew(id, draft, now, etag);
            using SqliteCommand upsert = this.connection.CreateCommand();
            upsert.CommandText =
                "INSERT INTO WorkflowDeployments (Id, BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier, Status, CreatedAt, Etag, LeaseExpiresAt, Document) " +
                "VALUES (@id, @base, @ver, @env, @rid, @status, @createdAt, @etag, NULL, @doc) " +
                "ON CONFLICT(Id) DO UPDATE SET BaseWorkflowId = excluded.BaseWorkflowId, VersionNumber = excluded.VersionNumber, " +
                "Environment = excluded.Environment, RuntimeIdentifier = excluded.RuntimeIdentifier, Status = excluded.Status, " +
                "CreatedAt = excluded.CreatedAt, Etag = excluded.Etag, LeaseExpiresAt = excluded.LeaseExpiresAt, Document = excluded.Document;";
            upsert.Parameters.AddWithValue("@id", id);
            upsert.Parameters.AddWithValue("@base", draft.BaseWorkflowIdValue);
            upsert.Parameters.AddWithValue("@ver", draft.VersionNumberValue);
            upsert.Parameters.AddWithValue("@env", draft.EnvironmentValue);
            upsert.Parameters.AddWithValue("@rid", draft.RuntimeIdentifierValue);
            upsert.Parameters.AddWithValue("@status", WorkflowDeploymentStatusNames.Queued);
            upsert.Parameters.AddWithValue("@createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            upsert.Parameters.AddWithValue("@etag", etag.Value!);
            upsert.Parameters.AddWithValue("@doc", json);
            await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync(id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // One `now` guards the lease-expiry filter and stamps the new lease/startedAt, so the claim is evaluated at a
            // single instant. Lease/created timestamps are the ISO-8601 round-trip ("o") of a UTC instant, so a lexicographic
            // column compare is chronological (the same property the CreatedAt ordering relies on).
            DateTimeOffset now = this.timeProvider.GetUtcNow();
            string nowText = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

            // The gate serialises every operation (single-writer), so a plain select-oldest-claimable then update is atomic:
            // no SKIP LOCKED is needed because no other operation can interleave. Two workers never claim the same deployment.
            // A row is claimable when Queued, or an orphaned Deploying whose lease has expired (ADR 0056).
            string? id = null;
            byte[]? document = null;
            using (SqliteCommand select = this.connection.CreateCommand())
            {
                select.CommandText =
                    "SELECT Id, Document FROM WorkflowDeployments " +
                    "WHERE Status = @queued OR (Status = @deploying AND (LeaseExpiresAt IS NULL OR LeaseExpiresAt <= @now)) " +
                    "ORDER BY CreatedAt, Id LIMIT 1;";
                select.Parameters.AddWithValue("@queued", WorkflowDeploymentStatusNames.Queued);
                select.Parameters.AddWithValue("@deploying", WorkflowDeploymentStatusNames.Deploying);
                select.Parameters.AddWithValue("@now", nowText);
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

            // Stamp the Deploying transition + the lease. The lease also rides inside the Document; the column is the
            // denormalised copy the claim filters on.
            DateTimeOffset leaseExpiresAt = now + leaseTtl;
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(document!.AsMemory());
            WorkflowEtag etag = NewEtag();
            byte[] claimed = WorkflowDeploymentSerialization.SerializeClaimed(current.RootElement, claimedBy, now, leaseExpiresAt, etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowDeployments SET Status = @deploying, Etag = @etag, LeaseExpiresAt = @lease, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@deploying", WorkflowDeploymentStatusNames.Deploying);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@lease", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("@doc", claimed);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkflowDeployment>(claimed);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> CompleteAsync(string id, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
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

            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
            if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
            {
                ThrowHelper.ThrowWorkflowDeploymentNotDeployingForCompletion(id);
            }

            WorkflowEtag etag = NewEtag();
            byte[] json = WorkflowDeploymentSerialization.SerializeCompletion(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowDeployments SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@status", WorkflowDeploymentStatusNames.ToWire(completion.Status));
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
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

            // Parse the existing document NON-COPYING; it must be Deploying, then the etag is checked (a reclaim would have
            // moved it, so a stale expected etag conflicts) and the lease-extended record serialized. The lease rides inside
            // the Document; the column is the denormalised copy the claim filters on.
            using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
            if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
            {
                ThrowHelper.ThrowWorkflowDeploymentNotDeployingForLeaseRenewal(id);
            }

            DateTimeOffset leaseExpiresAt = this.timeProvider.GetUtcNow() + leaseTtl;
            WorkflowEtag etag = NewEtag();
            byte[] json = WorkflowDeploymentSerialization.SerializeRenewal(current.RootElement, id, expectedEtag, leaseExpiresAt, etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowDeployments SET Etag = @etag, LeaseExpiresAt = @lease, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@lease", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkflowDeployment>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<WorkflowDeployment>> ListAsync(WorkflowDeploymentQuery query, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var list = new PooledDocumentList<WorkflowDeployment>();
            using SqliteCommand select = this.connection.CreateCommand();
            var sql = new StringBuilder("SELECT Document FROM WorkflowDeployments");
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
                list.Add(ParsedJsonDocument<WorkflowDeployment>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
            }

            return list;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsDeployedAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // A native indexed point read on the derived id (the target tuple maps to a unique id) — never a scan.
            string id = WorkflowDeployment.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Status FROM WorkflowDeployments WHERE Id = @k;";
            select.Parameters.AddWithValue("@k", id);
            object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return result is string status && string.Equals(status, WorkflowDeploymentStatusNames.Deployed, StringComparison.Ordinal);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowDeploymentPage> ListAsync(WorkflowDeploymentQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : WorkflowDeploymentPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the ADO predicate needs (a genuine DB-param leaf)
        // only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's UTC
        // ticks), id as its text. Undefined token = first page; a malformed token throws FormatException.
        string? cursorCreatedAt = null;
        string? cursorId = null;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            byte[] buffer = ArrayPool<byte>.Shared.Rent(WorkflowDeploymentContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
            try
            {
                if (WorkflowDeploymentContinuationToken.TryDecode(tokenUtf8.Span, buffer, out long cursorTicks, out ReadOnlySpan<byte> cursorIdUtf8))
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
            var sql = new StringBuilder("SELECT Document FROM WorkflowDeployments");
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

            var page = new PooledDocumentList<WorkflowDeployment>(pageSize);
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

                        page.Add(ParsedJsonDocument<WorkflowDeployment>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return WorkflowDeploymentPage.Create(page);
                }

                WorkflowDeployment last = page[page.Count - 1];
                using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
                return WorkflowDeploymentPage.Create(page, last.CreatedAtValue.UtcTicks, lastId.Span);
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
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowDeploymentQuery query, int cap, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            var conditions = new List<string>(5);
            AppendFilters(conditions, select, query);

            // Bounded count: COUNT over a subquery capped at cap + 1, so the scan stops one row past the cap; the (cap+1)th
            // row's existence trips Capped — never a full COUNT of the whole queue. Same predicate as the list.
            var inner = new StringBuilder("SELECT 1 FROM WorkflowDeployments");
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
    private static void AppendFilters(List<string> conditions, SqliteCommand command, WorkflowDeploymentQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("@status", WorkflowDeploymentStatusNames.ToWire(status));
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
        select.CommandText = "SELECT Document FROM WorkflowDeployments WHERE Id = @k;";
        select.Parameters.AddWithValue("@k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkflowDeployments (
            Id TEXT NOT NULL PRIMARY KEY,
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            Environment TEXT NOT NULL,
            RuntimeIdentifier TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            LeaseExpiresAt TEXT NULL,
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Status ON WorkflowDeployments (Status);
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Target ON WorkflowDeployments (BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier);
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Queue ON WorkflowDeployments (Status, CreatedAt, Id);
        """;
}