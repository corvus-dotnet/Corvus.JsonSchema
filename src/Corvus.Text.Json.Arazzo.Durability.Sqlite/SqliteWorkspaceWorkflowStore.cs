// <copyright file="SqliteWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted for a single-file / embedded host so a working copy survives a restart. Each working copy is stored as its
/// <see cref="WorkspaceWorkflow"/> document in a BLOB column, keyed by its server-minted <c>id</c> (globally unique, so
/// the id alone is the key); its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory over the
/// single row for an id (a working copy outside reach is reported as absent, non-disclosing), and per row in keyset
/// order for the list. One connection is held open and all operations are serialised through it, as the other SQLite
/// stores do. The document is carried bytes-to-bytes (#803): rows read/write via <see cref="ParsedJsonDocument{T}"/> and
/// the shared pooled serialization, never a per-op detached clone.
/// </remarks>
public sealed class SqliteWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteWorkspaceWorkflowStore(SqliteConnection connection, TimeProvider timeProvider)
    {
        this.connection = connection;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema against a database.</summary>
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

    /// <summary>Opens a working-copy store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=workspace-workflows.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteWorkspaceWorkflowStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteWorkspaceWorkflowStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential;
            // the id is opaque to clients either way). SQLite's default BINARY collation orders these ordinally, matching
            // the keyset pager's id compare.
            string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            WorkflowEtag etag = NewEtag();
            byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO WorkspaceWorkflows (Id, Etag, Document) VALUES (@id, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Mirror the management tags into the queryable side table so the §14.2 read reach can be pushed into the
            // list/count query (a correlated EXISTS). Atomic; tags are immutable so no re-sync on save.
            await SyncSecurityTagsAsync(this.connection, transaction, id, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
            transaction.Commit();

            return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? json = await this.FindForManagementAsync(id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<WorkspaceWorkflowPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Id, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = WorkspaceWorkflowContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
            bool hasMore = false;
            try
            {
                // Keyset seek past the cursor id in id order — an indexed range scan over the primary key, not a table
                // load (the id is globally unique, so it is the whole total order and the tie-breaker is empty) — with the
                // §14.2 read reach pushed into the query as a correlated EXISTS over WorkspaceWorkflowSecurityTags (the
                // same predicate context.Admits evaluates, but applied in the database), so out-of-reach rows never leave
                // it. LIMIT bounds the read.
                using SqliteCommand select = this.connection.CreateCommand();
                var conditions = new List<string>(2);
                if (hasCursor)
                {
                    conditions.Add("Id > @id");
                    select.Parameters.AddWithValue("@id", cursor.Id);
                }

                AppendReachPredicate(conditions, select, context);

                var sql = new StringBuilder("SELECT Id, Document FROM WorkspaceWorkflows");
                if (conditions.Count > 0)
                {
                    sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
                }

                sql.Append(" ORDER BY Id LIMIT @limit;");
                select.Parameters.AddWithValue("@limit", pageSize + 1);
                select.CommandText = sql.ToString();

                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                string lastId = string.Empty;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (docs.Count == pageSize)
                    {
                        hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                        break;
                    }

                    lastId = reader.GetString(0);
                    docs.Add(PersistedJson.ToPooledDocument<WorkspaceWorkflow>(reader.GetFieldValue<byte[]>(1)));
                }

                return hasMore
                    ? WorkspaceWorkflowPage.Create(docs, lastId, string.Empty)
                    : WorkspaceWorkflowPage.Create(docs);
            }
            catch
            {
                docs.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return null;
            }

            byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE WorkspaceWorkflows SET Etag = @etag, Document = @doc WHERE Id = @id;"; // the id, provenance, and tags are immutable → key unchanged
            update.Parameters.AddWithValue("@etag", WorkspaceWorkflowSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@id", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? existing = await this.FindForManagementAsync(id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return false;
            }

            if (!expectedEtag.IsNone)
            {
                WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
            }

            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText =
                "DELETE FROM WorkspaceWorkflows WHERE Id = @id; " +
                "DELETE FROM WorkspaceWorkflowSecurityTags WHERE Id = @id;";
            delete.Parameters.AddWithValue("@id", id);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            transaction.Commit();
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            var conditions = new List<string>(1);
            AppendReachPredicate(conditions, select, context);

            // Bounded count: COUNT over a subquery capped at cap + 1, so the scan stops one row past the cap; the
            // (cap+1)th row trips Capped. Same reach predicate as the list (AppendReachPredicate) — cannot drift.
            var inner = new StringBuilder("SELECT 1 FROM WorkspaceWorkflows");
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

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over WorkspaceWorkflowSecurityTags mirroring the
    // caller's reach (the same rules context.Admits evaluates), keyed on the Id row discriminator. A null reach
    // (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never drift from the list.
    private static void AppendReachPredicate(List<string> conditions, SqliteCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "WorkspaceWorkflowSecurityTags",
            ["Id"],
            "TagKey",
            "TagValue",
            "WorkspaceWorkflows",
            value =>
            {
                string p = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors a working copy's management tags (one row per key/value) into the queryable side table, keyed by its id, so
    // the reach can be pushed into the list/count query. Management tags are immutable after creation, so this runs only
    // on add.
    private static async Task SyncSecurityTagsAsync(SqliteConnection connection, SqliteTransaction transaction, string id, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO WorkspaceWorkflowSecurityTags (Id, TagKey, TagValue) VALUES (@id, @key, @value);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Finds the single working copy with the given id the caller's reach for the verb admits, returning its bytes (the id
    // is the sole key, so a scalar lookup suffices). A working copy outside reach is invisible (non-disclosing).
    private async ValueTask<byte[]?> FindForManagementAsync(string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkspaceWorkflows WHERE Id = @id;";
        select.Parameters.AddWithValue("@id", id);
        if (await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not byte[] json)
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS WorkspaceWorkflows (
            Id TEXT NOT NULL PRIMARY KEY,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS WorkspaceWorkflowSecurityTags (
            Id TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_WorkspaceWorkflowSecurityTags_owner ON WorkspaceWorkflowSecurityTags (Id);
        CREATE INDEX IF NOT EXISTS IX_WorkspaceWorkflowSecurityTags_kv ON WorkspaceWorkflowSecurityTags (TagKey, TagValue);
        """;
}