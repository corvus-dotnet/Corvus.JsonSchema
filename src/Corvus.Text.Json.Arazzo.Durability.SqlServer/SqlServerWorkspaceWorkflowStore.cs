// <copyright file="SqlServerWorkspaceWorkflowStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.WorkspaceWorkflows;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IWorkspaceWorkflowStore"/> (workflow-designer design §4.1): designer working copies
/// persisted relationally so a working copy survives a restart. Each working copy is stored as its
/// <see cref="WorkspaceWorkflow"/> document in a <c>varbinary(max)</c> column, keyed by its server-minted <c>id</c>
/// (globally unique, so the id alone is the primary key — no name/tags composite, no name column or insert-conflict
/// handling); its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in
/// memory over the single row for an id (a working copy outside reach is reported as absent, non-disclosing) and per row
/// in keyset order for the list. Each operation opens a pooled connection, so the store is naturally concurrent.</para>
/// <para>The document is carried bytes-to-bytes (#803): rows read/write via <see cref="ParsedJsonDocument{T}"/> and the
/// shared pooled serialization, never a per-op detached clone. The serialized <see cref="byte"/> array binds directly
/// into the <c>varbinary(max)</c> <c>Document</c> column and reads back through <c>GetFieldValue&lt;byte[]&gt;</c>, so no
/// intermediate copy is made on either leg.</para>
/// </remarks>
public sealed class SqlServerWorkspaceWorkflowStore : IWorkspaceWorkflowStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerWorkspaceWorkflowStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerWorkspaceWorkflowStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerWorkspaceWorkflowStore>(new SqlServerWorkspaceWorkflowStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>> AddAsync(WorkspaceWorkflow draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);

        // The durable backend mints its own opaque id (the reference in-memory store's ids are creation-sequential; the
        // id is opaque to clients either way). It is globally unique, so there is no insert conflict to guard against.
        string id = "wc-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = WorkspaceWorkflowSerialization.SerializeNew(draft, id, actor, this.timeProvider.GetUtcNow(), etag);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO WorkspaceWorkflows (Id, Etag, Document) VALUES (@id, @etag, @doc);";
        insert.Parameters.AddWithValue("@id", id);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        insert.Parameters.AddWithValue("@doc", json);
        await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

        // Mirror the management tags into the queryable side table so the §14.2 read reach can be pushed into the
        // list/count query (a correlated EXISTS). Atomic; tags are immutable so no re-sync on save.
        await SyncSecurityTagsAsync(connection, transaction, id, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> GetAsync(string id, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? json = await FindForManagementAsync(connection, id, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
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

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<WorkspaceWorkflow>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor id in id order — an indexed range scan over the primary key, not a table load
            // (the id is globally unique, so it is the whole total order and the tie-breaker is empty) — with the §14.2
            // read reach pushed into the query as a correlated EXISTS over WorkspaceWorkflowSecurityTags (the same
            // predicate context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it.
            await using SqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("Id > @id");
                select.Parameters.AddWithValue("@id", cursor.Id);
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT TOP (@limit) Id, Document FROM WorkspaceWorkflows");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Id;");
            select.CommandText = sql.ToString();

            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkspaceWorkflow>?> UpdateAsync(string id, WorkspaceWorkflow draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await FindForManagementAsync(connection, id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = WorkspaceWorkflowSerialization.SerializeUpdated(existing, id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE WorkspaceWorkflows SET Etag = @etag, Document = @doc WHERE Id = @id;"; // the id, provenance, and tags are immutable → key unchanged
        update.Parameters.AddWithValue("@etag", WorkspaceWorkflowSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@id", id);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string id, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await FindForManagementAsync(connection, id, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            WorkspaceWorkflowSerialization.EnsureEtag(id, expectedEtag, WorkspaceWorkflowSerialization.EtagOf(existing));
        }

        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM WorkspaceWorkflows WHERE Id = @id; " +
            "DELETE FROM WorkspaceWorkflowSecurityTags WHERE Id = @id;";
        delete.Parameters.AddWithValue("@id", id);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        var conditions = new List<string>(1);
        AppendReachPredicate(conditions, select, context);

        // Bounded count: COUNT over a TOP (@cap) subquery, so the scan stops one row past the cap; the (cap+1)th row
        // trips Capped. Same reach predicate as the list (AppendReachPredicate) — cannot drift. (SQL Server has no LIMIT.)
        var inner = new StringBuilder("SELECT TOP (@cap) 1 AS x FROM WorkspaceWorkflows");
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

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over WorkspaceWorkflowSecurityTags mirroring the
    // caller's reach (the same rules context.Admits evaluates), keyed on the Id row discriminator. A null reach
    // (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never drift from the list.
    private static void AppendReachPredicate(List<string> conditions, SqlCommand command, AccessContext context)
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
    private static async Task SyncSecurityTagsAsync(SqlConnection connection, SqlTransaction transaction, string id, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using SqlCommand insert = connection.CreateCommand();
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
    private static async ValueTask<byte[]?> FindForManagementAsync(SqlConnection connection, string id, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM WorkspaceWorkflows WHERE Id = @id;";
        select.Parameters.AddWithValue("@id", id);
        if (await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is not byte[] json)
        {
            return null;
        }

        using ParsedJsonDocument<WorkspaceWorkflow> candidate = PersistedJson.ToPooledDocument<WorkspaceWorkflow>(json);
        return context.Admits(verb, candidate.RootElement.ManagementTagsValue) ? json : null;
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
        IF OBJECT_ID(N'WorkspaceWorkflows', N'U') IS NULL
        BEGIN
            CREATE TABLE WorkspaceWorkflows (
                Id NVARCHAR(450) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_WorkspaceWorkflows PRIMARY KEY (Id)
            );
        END;
        IF OBJECT_ID(N'WorkspaceWorkflowSecurityTags', N'U') IS NULL
        BEGIN
            CREATE TABLE WorkspaceWorkflowSecurityTags (
                Id NVARCHAR(450) NOT NULL,
                TagKey NVARCHAR(200) NOT NULL,
                TagValue NVARCHAR(200) NOT NULL
            );
            CREATE INDEX IX_WorkspaceWorkflowSecurityTags_owner ON WorkspaceWorkflowSecurityTags (Id);
            CREATE INDEX IX_WorkspaceWorkflowSecurityTags_kv ON WorkspaceWorkflowSecurityTags (TagKey, TagValue);
        END;
        """;
}