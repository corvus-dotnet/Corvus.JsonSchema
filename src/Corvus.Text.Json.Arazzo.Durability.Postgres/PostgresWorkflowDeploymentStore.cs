// <copyright file="PostgresWorkflowDeploymentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Publishing;
using Npgsql;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IWorkflowDeploymentStore"/> — workflow deployments (ADR 0055) persisted relationally. Each
/// deployment is stored as its <see cref="WorkflowDeployment"/> schema document in a <c>bytea</c> column, with the filterable
/// fields (status, target tuple) and the etag mirrored into columns for querying and the optimistic-concurrency check. The id
/// is derived from the target tuple, so <see cref="EnqueueAsync"/> is an idempotent <c>INSERT ... ON CONFLICT</c> upsert.
/// Mirrors <see cref="PostgresAvailabilityRequestStore"/>, keyed by the target-derived id rather than a random id.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent; the claim is a single
/// transaction that locks the oldest queued row with <c>FOR UPDATE SKIP LOCKED</c>, so two workers never claim the same deployment.</remarks>
public sealed class PostgresWorkflowDeploymentStore : IWorkflowDeploymentStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresWorkflowDeploymentStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresWorkflowDeploymentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowDeploymentStore>(
            new PostgresWorkflowDeploymentStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresWorkflowDeploymentStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresWorkflowDeploymentStore>(
            new PostgresWorkflowDeploymentStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>> EnqueueAsync(WorkflowDeployment draft, string actor, CancellationToken cancellationToken)
    {
        // The entity carries no created-by field, so `actor` is validated for parity with the other stores but not persisted.
        ArgumentNullException.ThrowIfNull(actor);
        string id = WorkflowDeployment.DeriveId(draft.BaseWorkflowIdValue, draft.VersionNumberValue, draft.EnvironmentValue, draft.RuntimeIdentifierValue);
        WorkflowEtag etag = NewEtag();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes via ReadOnlyMemory as the
        // BYTEA parameter (no GC document array, no second copy). The document is returned on success, disposed on failure.
        ParsedJsonDocument<WorkflowDeployment> doc = WorkflowDeploymentSerialization.SerializeNewDoc(id, draft, now, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using NpgsqlCommand upsert = connection.CreateCommand();

            // Idempotent per target: the id is derived from the tuple, so a repeated enqueue overwrites the same row — an
            // upsert that resets the deployment to Queued (SerializeNewDoc omits startedAt/completedAt/failureReason/functionUrl/claimedBy).
            // A (re-)enqueue resets the deployment to Queued, so its lease is cleared (NULL); a Queued deployment holds no lease.
            upsert.CommandText =
                "INSERT INTO WorkflowDeployments (Id, BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier, Status, CreatedAt, Etag, LeaseExpiresAt, Document) " +
                "VALUES (@id, @base, @ver, @env, @rid, @status, @createdAt, @etag, NULL, @doc) " +
                "ON CONFLICT (Id) DO UPDATE SET " +
                "BaseWorkflowId = EXCLUDED.BaseWorkflowId, VersionNumber = EXCLUDED.VersionNumber, Environment = EXCLUDED.Environment, " +
                "RuntimeIdentifier = EXCLUDED.RuntimeIdentifier, Status = EXCLUDED.Status, CreatedAt = EXCLUDED.CreatedAt, " +
                "Etag = EXCLUDED.Etag, LeaseExpiresAt = EXCLUDED.LeaseExpiresAt, Document = EXCLUDED.Document;";
            upsert.Parameters.AddWithValue("id", id);
            upsert.Parameters.AddWithValue("base", draft.BaseWorkflowIdValue);
            upsert.Parameters.AddWithValue("ver", draft.VersionNumberValue);
            upsert.Parameters.AddWithValue("env", draft.EnvironmentValue);
            upsert.Parameters.AddWithValue("rid", draft.RuntimeIdentifierValue);
            upsert.Parameters.AddWithValue("status", WorkflowDeploymentStatusNames.Queued);
            upsert.Parameters.AddWithValue("createdAt", now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
            upsert.Parameters.AddWithValue("etag", etag.Value!);
            upsert.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
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
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> GetAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<WorkflowDeployment>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> ClaimNextQueuedAsync(string claimedBy, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claimedBy);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        // One `now` guards the lease-expiry filter and stamps the new lease/startedAt, so the claim is evaluated at a single
        // instant. Lease/created timestamps are the ISO-8601 round-trip ("o") of a UTC instant, so a lexicographic column
        // compare is chronological (the same property the CreatedAt ordering relies on).
        DateTimeOffset now = this.timeProvider.GetUtcNow();
        string nowText = now.UtcDateTime.ToString("o", CultureInfo.InvariantCulture);

        // Lock the single oldest CLAIMABLE row (oldest-first by (CreatedAt, Id)) with FOR UPDATE SKIP LOCKED: Queued, or an
        // orphaned Deploying whose lease has expired (ADR 0056). A concurrent claim skips this locked row and takes the next,
        // so two workers never claim the same deployment; a crashed worker holds no lock, so its orphan is lockable and reclaimed.
        string? id = null;
        byte[]? document = null;
        await using (NpgsqlCommand select = connection.CreateCommand())
        {
            select.Transaction = transaction;
            select.CommandText =
                "SELECT Id, Document FROM WorkflowDeployments " +
                "WHERE Status = @queued OR (Status = @deploying AND (LeaseExpiresAt IS NULL OR LeaseExpiresAt <= @now)) " +
                "ORDER BY CreatedAt, Id LIMIT 1 FOR UPDATE SKIP LOCKED;";
            select.Parameters.AddWithValue("queued", WorkflowDeploymentStatusNames.Queued);
            select.Parameters.AddWithValue("deploying", WorkflowDeploymentStatusNames.Deploying);
            select.Parameters.AddWithValue("now", nowText);
            await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

        // Parse the locked row NON-COPYING, stamp the Deploying transition + the lease, and write it back within the same
        // transaction. The lease also rides inside the Document; the column is the denormalised copy the claim filters on.
        DateTimeOffset leaseExpiresAt = now + leaseTtl;
        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(document!.AsMemory());
        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<WorkflowDeployment> claimed = WorkflowDeploymentSerialization.SerializeClaimedDoc(current.RootElement, claimedBy, now, leaseExpiresAt, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(claimed.RootElement).Memory;
            await using (NpgsqlCommand update = connection.CreateCommand())
            {
                update.Transaction = transaction;
                update.CommandText = "UPDATE WorkflowDeployments SET Status = @deploying, Etag = @etag, LeaseExpiresAt = @lease, Document = @doc WHERE Id = @k;";
                update.Parameters.AddWithValue("deploying", WorkflowDeploymentStatusNames.Deploying);
                update.Parameters.AddWithValue("etag", etag.Value!);
                update.Parameters.AddWithValue("lease", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
                update.Parameters.Add(new NpgsqlParameter<ReadOnlyMemory<byte>>("doc", utf8));
                update.Parameters.AddWithValue("k", id);
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
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> CompleteAsync(string id, WorkflowDeploymentCompletion completion, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // Parse the existing document NON-COPYING over the driver's array (the read leaf); it must be Deploying, then the etag
        // is checked and the completed record serialized into the pooled buffer the returned document owns.
        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(existing.AsMemory());
        if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
        {
            ThrowHelper.ThrowWorkflowDeploymentNotDeployingForCompletion(id);
        }

        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<WorkflowDeployment> updated = WorkflowDeploymentSerialization.SerializeCompletionDoc(current.RootElement, id, expectedEtag, completion, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using NpgsqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowDeployments SET Status = @status, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("status", WorkflowDeploymentStatusNames.ToWire(completion.Status));
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
    public async ValueTask<ParsedJsonDocument<WorkflowDeployment>?> RenewLeaseAsync(string id, WorkflowEtag expectedEtag, TimeSpan leaseTtl, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        // Parse the existing document NON-COPYING; it must be Deploying, then the etag is checked (a reclaim would have moved
        // it, so a stale expected etag conflicts) and the lease-extended record serialized into the pooled buffer the
        // returned document owns. The lease rides inside the Document; the column is the denormalised copy the claim filters on.
        using ParsedJsonDocument<WorkflowDeployment> current = ParsedJsonDocument<WorkflowDeployment>.Parse(existing.AsMemory());
        if (!current.RootElement.HasStatus(WorkflowDeploymentStatus.Deploying))
        {
            ThrowHelper.ThrowWorkflowDeploymentNotDeployingForLeaseRenewal(id);
        }

        DateTimeOffset leaseExpiresAt = this.timeProvider.GetUtcNow() + leaseTtl;
        WorkflowEtag etag = NewEtag();
        ParsedJsonDocument<WorkflowDeployment> updated = WorkflowDeploymentSerialization.SerializeRenewalDoc(current.RootElement, id, expectedEtag, leaseExpiresAt, etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using NpgsqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE WorkflowDeployments SET Etag = @etag, LeaseExpiresAt = @lease, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("etag", etag.Value!);
            update.Parameters.AddWithValue("lease", leaseExpiresAt.UtcDateTime.ToString("o", CultureInfo.InvariantCulture));
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
    public async ValueTask<PooledDocumentList<WorkflowDeployment>> ListAsync(WorkflowDeploymentQuery query, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var list = new PooledDocumentList<WorkflowDeployment>();
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM WorkflowDeployments");
        var conditions = new List<string>(5);
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
            list.Add(ParsedJsonDocument<WorkflowDeployment>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> IsDeployedAsync(string baseWorkflowId, int versionNumber, string environment, string runtimeIdentifier, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(environment);
        ArgumentException.ThrowIfNullOrEmpty(runtimeIdentifier);

        // A native indexed point read on the derived id (the target tuple maps to a unique id) — never a scan.
        string id = WorkflowDeployment.DeriveId(baseWorkflowId, versionNumber, environment, runtimeIdentifier);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Status FROM WorkflowDeployments WHERE Id = @k;";
        select.Parameters.AddWithValue("k", id);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is string status && string.Equals(status, WorkflowDeploymentStatusNames.Deployed, StringComparison.Ordinal);
    }

    /// <inheritdoc/>
    public async ValueTask<WorkflowDeploymentPage> ListAsync(WorkflowDeploymentQuery query, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : WorkflowDeploymentPage.DefaultPageSize;

        // Decode the keyset cursor; createdAt + id reify to the strings the Npgsql predicate needs (a genuine DB-param leaf)
        // only here — createdAt as the ISO-8601 "o" form the CreatedAt column stores (reconstructed from the token's UTC
        // ticks so it byte-matches the boundary row), id as its text. Undefined token = first page.
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM WorkflowDeployments");
        var conditions = new List<string>(6);
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

        // The IX_WorkflowDeployments_Created index on (CreatedAt, Id) drives both the order and the seek; LIMIT bounds the read
        // to one page + 1 (lookahead) — never a full read + parse of the whole queue.
        sql.Append(" ORDER BY CreatedAt, Id LIMIT @limit;");
        select.Parameters.AddWithValue("limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<WorkflowDeployment>(pageSize);
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

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(WorkflowDeploymentQuery query, int cap, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
        var conditions = new List<string>(5);
        AppendFilters(conditions, select, query);

        // Bounded count: COUNT over a subquery capped at cap + 1, so the scan stops one row past the cap; the (cap+1)th
        // row's existence trips Capped — never a full COUNT of the whole queue. Same predicate as the list (AppendFilters).
        var inner = new StringBuilder("SELECT 1 FROM WorkflowDeployments");
        if (conditions.Count > 0)
        {
            inner.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        inner.Append(" LIMIT @cap");
        select.Parameters.AddWithValue("cap", cap + 1);
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

    // Appends the shared list filters (status / target tuple) as @-parameters; a null criterion adds nothing.
    private static void AppendFilters(List<string> conditions, NpgsqlCommand command, WorkflowDeploymentQuery query)
    {
        if (query.Status is { } status)
        {
            conditions.Add("Status = @status");
            command.Parameters.AddWithValue("status", WorkflowDeploymentStatusNames.ToWire(status));
        }

        if (query.BaseWorkflowId is { } baseWorkflowId)
        {
            conditions.Add("BaseWorkflowId = @base");
            command.Parameters.AddWithValue("base", baseWorkflowId);
        }

        if (query.VersionNumber is { } versionNumber)
        {
            conditions.Add("VersionNumber = @ver");
            command.Parameters.AddWithValue("ver", versionNumber);
        }

        if (query.Environment is { } environment)
        {
            conditions.Add("Environment = @env");
            command.Parameters.AddWithValue("env", environment);
        }

        if (query.RuntimeIdentifier is { } runtimeIdentifier)
        {
            conditions.Add("RuntimeIdentifier = @rid");
            command.Parameters.AddWithValue("rid", runtimeIdentifier);
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    private static async ValueTask<byte[]?> DocumentAsync(NpgsqlConnection connection, string id, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkflowDeployments WHERE Id = @k;";
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
        CREATE TABLE IF NOT EXISTS WorkflowDeployments (
            Id TEXT COLLATE "C" NOT NULL PRIMARY KEY,
            BaseWorkflowId TEXT NOT NULL,
            VersionNumber INTEGER NOT NULL,
            Environment TEXT NOT NULL,
            RuntimeIdentifier TEXT NOT NULL,
            Status TEXT NOT NULL,
            CreatedAt TEXT NOT NULL,
            Etag TEXT NOT NULL,
            LeaseExpiresAt TEXT NULL,
            Document BYTEA NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Status ON WorkflowDeployments (Status);
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Target ON WorkflowDeployments (BaseWorkflowId, VersionNumber, Environment, RuntimeIdentifier);
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Queue ON WorkflowDeployments (Status, CreatedAt, Id);
        CREATE INDEX IF NOT EXISTS IX_WorkflowDeployments_Created ON WorkflowDeployments (CreatedAt, Id);
        """;
}