// <copyright file="SqliteSecurityPolicyStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2) persisted for a single-file / embedded host. Each record is stored as its
/// Corvus.Text.Json schema document (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>)
/// in a BLOB column, with its etag in a column for the optimistic-concurrency check; a single-row meta table holds
/// the monotonic generation a resolver caches against.
/// </summary>
/// <remarks>One connection is held open and all operations are serialised through it, as the catalog/state stores do.</remarks>
public sealed class SqliteSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteSecurityPolicyStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens a security-policy store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=policy.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteSecurityPolicyStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeNewRule(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SecurityRules (Name, Expression, Etag, Document) VALUES (@name, @expression, @etag, @doc);";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@expression", draft.ExpressionValue);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                ThrowHelper.ThrowSecurityRuleAlreadyExists(name);
            }

            // Mirror the management tags into the queryable side table so the reach can be pushed into list/count.
            await this.SyncTagsAsync(transaction, "SecurityRuleSecurityTags", "Name", name, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;

        // Decode the keyset cursor + build the q LIKE pattern; both reify to strings only at the ADO parameter boundary the
        // driver forces (a genuine DB-param leaf) — the cursor name from the token, the q pattern from its JSON value.
        string? after = DecodeRuleCursor(pageToken);
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Native keyset page: Name is the PRIMARY KEY (SQLite BINARY collation == ordinal UTF-8 byte order, the same
            // order the in-memory pager sorts by) so the index drives the seek + order; q matches the indexed Name column
            // or the extracted Expression column server-side (LIKE is case-insensitive); LIMIT bounds the read to one page.
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                """
                SELECT Document FROM SecurityRules
                WHERE (@after IS NULL OR Name > @after)
                  AND (@q IS NULL OR Name LIKE @q ESCAPE '\' OR Expression LIKE @q ESCAPE '\')
                ORDER BY Name
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
            select.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", pageSize + 1);

            var page = new PooledDocumentList<SecurityRuleDocument>(pageSize);
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

                        page.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return SecurityRulePage.Create(page);
                }

                using UnescapedUtf8JsonString lastName = page[page.Count - 1].Name.GetUtf8String();
                return SecurityRulePage.Create(page, lastName.Span);
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
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;
        string? after = DecodeRuleCursor(pageToken);
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();

            // Push the §14.2 read reach into the query as a correlated EXISTS over the rules' tags side table; appended only
            // when the caller's reach is not full (a null reach adds nothing, so an unscoped caller reads everything).
            var sql = new StringBuilder(
                """
                SELECT Document FROM SecurityRules
                WHERE (@after IS NULL OR Name > @after)
                  AND (@q IS NULL OR Name LIKE @q ESCAPE '\' OR Expression LIKE @q ESCAPE '\')
                """);
            string? reach = BuildReachPredicate(select, context, "SecurityRuleSecurityTags", "Name", "SecurityRules");
            if (reach is not null)
            {
                sql.Append(" AND ").Append(reach);
            }

            sql.Append(" ORDER BY Name LIMIT @limit;");
            select.CommandText = sql.ToString();
            select.Parameters.AddWithValue("@after", (object?)after ?? DBNull.Value);
            select.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", pageSize + 1);

            var page = new PooledDocumentList<SecurityRuleDocument>(pageSize);
            try
            {
                bool hasMore = false;
                using (SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (page.Count == pageSize)
                        {
                            hasMore = true;
                            break;
                        }

                        page.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return SecurityRulePage.Create(page);
                }

                using UnescapedUtf8JsonString lastName = page[page.Count - 1].Name.GetUtf8String();
                return SecurityRulePage.Create(page, lastName.Span);
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
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
            byte[] json = SecurityPolicySerialization.SerializeUpdatedRule(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SecurityRules SET Expression = @expression, Etag = @etag, Document = @doc WHERE Name = @k;";
            update.Parameters.AddWithValue("@expression", draft.ExpressionValue);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", name);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityRuleDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityRules", "Name", "rule", name, "SecurityRuleSecurityTags", expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
            WorkflowEtag etag = NewEtag();
            byte[] json = SecurityPolicySerialization.SerializeNewBinding(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SecurityBindings (Id, SortOrder, ClaimType, ClaimValue, Description, Etag, Document) VALUES (@id, @order, @claimType, @claimValue, @description, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@order", draft.OrderValue);
            insert.Parameters.AddWithValue("@claimType", draft.ClaimTypeValue);
            insert.Parameters.AddWithValue("@claimValue", draft.ClaimValue.IsNotUndefined() ? (string)draft.ClaimValue : DBNull.Value);
            insert.Parameters.AddWithValue("@description", draft.Description.IsNotUndefined() ? (string)draft.Description : DBNull.Value);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            // Mirror the management tags into the queryable side table so the reach can be pushed into list/count.
            await this.SyncTagsAsync(transaction, "SecurityBindingSecurityTags", "Id", id, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
            return doc is null ? null : ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;

        // Decode the keyset cursor (order, id) + build the q LIKE pattern; all reify to managed values only at the ADO
        // parameter boundary (the DB-param leaf). Undefined token = first page; undefined q = no filter.
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Native keyset page: the IX_SecurityBindings_Order index on (SortOrder, Id) drives the seek + order (Id is the
            // TEXT primary key, BINARY collation == ordinal == the in-memory pager's id compare); q matches the extracted
            // ClaimType/ClaimValue/Description columns server-side (LIKE is case-insensitive); LIMIT bounds the read.
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                """
                SELECT Document FROM SecurityBindings
                WHERE (@hasCursor = 0 OR SortOrder > @order OR (SortOrder = @order AND Id > @id))
                  AND (@q IS NULL OR ClaimType LIKE @q ESCAPE '\' OR ClaimValue LIKE @q ESCAPE '\' OR Description LIKE @q ESCAPE '\')
                ORDER BY SortOrder, Id
                LIMIT @limit;
                """;
            select.Parameters.AddWithValue("@hasCursor", hasCursor ? 1 : 0);
            select.Parameters.AddWithValue("@order", cursorOrder);
            select.Parameters.AddWithValue("@id", (object?)cursorId ?? DBNull.Value);
            select.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", pageSize + 1);

            var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
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

                        page.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return SecurityBindingPage.Create(page);
                }

                SecurityBindingDocument last = page[page.Count - 1];
                using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
                return SecurityBindingPage.Create(page, last.OrderValue, lastId.Span);
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
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();

            // Push the §14.2 read reach into the query as a correlated EXISTS over the bindings' tags side table.
            var sql = new StringBuilder(
                """
                SELECT Document FROM SecurityBindings
                WHERE (@hasCursor = 0 OR SortOrder > @order OR (SortOrder = @order AND Id > @id))
                  AND (@q IS NULL OR ClaimType LIKE @q ESCAPE '\' OR ClaimValue LIKE @q ESCAPE '\' OR Description LIKE @q ESCAPE '\')
                """);
            string? reach = BuildReachPredicate(select, context, "SecurityBindingSecurityTags", "Id", "SecurityBindings");
            if (reach is not null)
            {
                sql.Append(" AND ").Append(reach);
            }

            sql.Append(" ORDER BY SortOrder, Id LIMIT @limit;");
            select.CommandText = sql.ToString();
            select.Parameters.AddWithValue("@hasCursor", hasCursor ? 1 : 0);
            select.Parameters.AddWithValue("@order", cursorOrder);
            select.Parameters.AddWithValue("@id", (object?)cursorId ?? DBNull.Value);
            select.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            select.Parameters.AddWithValue("@limit", pageSize + 1);

            var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
            try
            {
                bool hasMore = false;
                using (SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        if (page.Count == pageSize)
                        {
                            hasMore = true;
                            break;
                        }

                        page.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                    }
                }

                if (!hasMore)
                {
                    return SecurityBindingPage.Create(page);
                }

                SecurityBindingDocument last = page[page.Count - 1];
                using UnescapedUtf8JsonString lastId = last.Id.GetUtf8String();
                return SecurityBindingPage.Create(page, last.OrderValue, lastId.Span);
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
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsForSubjectAsync(JsonString claimType, JsonString claimValue, CancellationToken cancellationToken)
    {
        // The subject reifies to a managed value only at the ADO parameter boundary (the DB-param leaf).
        string type = (string)claimType;
        string value = (string)claimValue;

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Native by-subject lookup: equality on the extracted ClaimType/ClaimValue columns (a value-less binding has
            // NULL ClaimValue, which never equals a value, so it is excluded — the by-subject contract), ordered by the
            // same (SortOrder, Id) as the full read. Only this subject's rows are read, never the whole table.
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                """
                SELECT Document FROM SecurityBindings
                WHERE ClaimType = @claimType AND ClaimValue = @claimValue
                ORDER BY SortOrder, Id;
                """;
            select.Parameters.AddWithValue("@claimType", type);
            select.Parameters.AddWithValue("@claimValue", value);

            var list = new PooledDocumentList<SecurityBindingDocument>();
            try
            {
                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
                }

                return list;
            }
            catch
            {
                list.Dispose();
                throw;
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountRulesAsync(int cap, JsonString q, CancellationToken cancellationToken)
    {
        int bound = cap > 0 ? cap : SecurityRulePage.DefaultPageSize;
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Bounded native COUNT over the same q-predicate as ListRulesAsync (minus the keyset cursor) — the inner LIMIT
            // caps the scan at cap+1. No Document blob is read.
            using SqliteCommand count = this.connection.CreateCommand();
            count.CommandText =
                """
                SELECT COUNT(*) FROM (
                    SELECT 1 FROM SecurityRules
                    WHERE (@q IS NULL OR Name LIKE @q ESCAPE '\' OR Expression LIKE @q ESCAPE '\')
                    LIMIT @cap);
                """;
            count.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            count.Parameters.AddWithValue("@cap", bound + 1);
            long total = (long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return total > bound ? (bound, true) : ((int)total, false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountRulesAsync(int cap, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        int bound = cap > 0 ? cap : SecurityRulePage.DefaultPageSize;
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand count = this.connection.CreateCommand();

            // Same reach predicate as the reach-filtered list so the footer count cannot drift.
            var inner = new StringBuilder(
                """
                SELECT 1 FROM SecurityRules
                WHERE (@q IS NULL OR Name LIKE @q ESCAPE '\' OR Expression LIKE @q ESCAPE '\')
                """);
            string? reach = BuildReachPredicate(count, context, "SecurityRuleSecurityTags", "Name", "SecurityRules");
            if (reach is not null)
            {
                inner.Append(" AND ").Append(reach);
            }

            inner.Append(" LIMIT @cap");
            count.CommandText = "SELECT COUNT(*) FROM (" + inner + ");";
            count.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            count.Parameters.AddWithValue("@cap", bound + 1);
            long total = (long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return total > bound ? (bound, true) : ((int)total, false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountBindingsAsync(int cap, JsonString q, CancellationToken cancellationToken)
    {
        int bound = cap > 0 ? cap : SecurityBindingPage.DefaultPageSize;
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand count = this.connection.CreateCommand();
            count.CommandText =
                """
                SELECT COUNT(*) FROM (
                    SELECT 1 FROM SecurityBindings
                    WHERE (@q IS NULL OR ClaimType LIKE @q ESCAPE '\' OR ClaimValue LIKE @q ESCAPE '\' OR Description LIKE @q ESCAPE '\')
                    LIMIT @cap);
                """;
            count.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            count.Parameters.AddWithValue("@cap", bound + 1);
            long total = (long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return total > bound ? (bound, true) : ((int)total, false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountBindingsAsync(int cap, JsonString q, AccessContext context, CancellationToken cancellationToken)
    {
        int bound = cap > 0 ? cap : SecurityBindingPage.DefaultPageSize;
        string? like = BuildLike(q);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand count = this.connection.CreateCommand();

            // Same reach predicate as the reach-filtered list so the footer count cannot drift.
            var inner = new StringBuilder(
                """
                SELECT 1 FROM SecurityBindings
                WHERE (@q IS NULL OR ClaimType LIKE @q ESCAPE '\' OR ClaimValue LIKE @q ESCAPE '\' OR Description LIKE @q ESCAPE '\')
                """);
            string? reach = BuildReachPredicate(count, context, "SecurityBindingSecurityTags", "Id", "SecurityBindings");
            if (reach is not null)
            {
                inner.Append(" AND ").Append(reach);
            }

            inner.Append(" LIMIT @cap");
            count.CommandText = "SELECT COUNT(*) FROM (" + inner + ");";
            count.Parameters.AddWithValue("@q", (object?)like ?? DBNull.Value);
            count.Parameters.AddWithValue("@cap", bound + 1);
            long total = (long)(await count.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false))!;
            return total > bound ? (bound, true) : ((int)total, false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            byte[]? doc = await this.DocumentAsync("SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
            if (doc is null)
            {
                return null;
            }

            WorkflowEtag etag = NewEtag();
            using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
            byte[] json = SecurityPolicySerialization.SerializeUpdatedBinding(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SecurityBindings SET SortOrder = @order, ClaimType = @claimType, ClaimValue = @claimValue, Description = @description, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@order", draft.OrderValue);
            update.Parameters.AddWithValue("@claimType", draft.ClaimTypeValue);
            update.Parameters.AddWithValue("@claimValue", draft.ClaimValue.IsNotUndefined() ? (string)draft.ClaimValue : DBNull.Value);
            update.Parameters.AddWithValue("@description", draft.Description.IsNotUndefined() ? (string)draft.Description : DBNull.Value);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await this.BumpGenerationAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SecurityBindingDocument>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityBindings", "Id", "binding", id, "SecurityBindingSecurityTags", expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            PooledDocumentList<SecurityRuleDocument> rules = await this.ReadRulesAsync(cancellationToken).ConfigureAwait(false);
            PooledDocumentList<SecurityBindingDocument> bindings = await this.ReadBindingsAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Generation FROM SecurityPolicyMeta WHERE Id = 0;";
            object? gen = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            return new SecurityPolicySnapshot(rules, bindings, gen is long g ? g : 0);
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

    // Decodes the keyset cursor (the last page's name) from the request's page token; base64url over the request UTF-8 into
    // a pooled buffer, the name reified to a string only for the @after parameter (the DB-param leaf). Undefined = first page.
    private static string? DecodeRuleCursor(JsonString pageToken)
    {
        if (!pageToken.IsNotUndefined())
        {
            return null;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SecurityRuleContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
        try
        {
            return SecurityRuleContinuationToken.TryDecode(tokenUtf8.Span, buffer, out ReadOnlySpan<byte> nameUtf8)
                ? Encoding.UTF8.GetString(nameUtf8)
                : null;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Decodes the keyset cursor (order, id) from the request's page token; base64url over the request UTF-8 into a pooled
    // buffer, the id reified to a string only for the @id parameter (the DB-param leaf). Returns false for the first page.
    private static bool DecodeBindingCursor(JsonString pageToken, out int order, out string? id)
    {
        order = 0;
        id = null;
        if (!pageToken.IsNotUndefined())
        {
            return false;
        }

        using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
        byte[] buffer = ArrayPool<byte>.Shared.Rent(SecurityBindingContinuationToken.GetMaxDecodedLength(tokenUtf8.Span.Length));
        try
        {
            if (SecurityBindingContinuationToken.TryDecode(tokenUtf8.Span, buffer, out order, out ReadOnlySpan<byte> idUtf8))
            {
                id = Encoding.UTF8.GetString(idUtf8);
                return true;
            }

            return false;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    // Builds the case-insensitive substring LIKE pattern for the optional q filter ("%<escaped>%"), reifying q to a string
    // only at this ADO LIKE-parameter leaf; null when q is undefined (no filter).
    private static string? BuildLike(JsonString q)
        => q.IsNotUndefined() ? "%" + EscapeLike((string)q) + "%" : null;

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private async ValueTask<byte[]?> DocumentAsync(string table, string column, string key, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = $"SELECT Document FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityRules ORDER BY Name;";
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    private async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityBindings ORDER BY SortOrder, Id;";
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    private async ValueTask<bool> DeleteAsync(string table, string column, string kind, string key, string tagTable, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = $"SELECT Etag FROM {table} WHERE {column} = @k;";
            select.Parameters.AddWithValue("@k", key);
            object? etag = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            if (etag is not string current)
            {
                return false;
            }

            SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, new WorkflowEtag(current));

            // Delete the row and its mirrored security tags atomically so the reach side table cannot outlive the row.
            using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText = $"DELETE FROM {table} WHERE {column} = @k;";
            delete.Parameters.AddWithValue("@k", key);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            using SqliteCommand deleteTags = this.connection.CreateCommand();
            deleteTags.Transaction = transaction;
            deleteTags.CommandText = $"DELETE FROM {tagTable} WHERE {column} = @k;";
            deleteTags.Parameters.AddWithValue("@k", key);
            await deleteTags.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await this.BumpGenerationAsync(cancellationToken, transaction).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async ValueTask BumpGenerationAsync(CancellationToken cancellationToken, SqliteTransaction? transaction = null)
    {
        using SqliteCommand bump = this.connection.CreateCommand();
        bump.Transaction = transaction;
        bump.CommandText = "UPDATE SecurityPolicyMeta SET Generation = Generation + 1 WHERE Id = 0;";
        await bump.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    // Mirrors a rule's/binding's MANAGEMENT tags (one row per key/value) into the queryable side table, keyed by the
    // owner key (rule Name / binding Id), so the §14.2 read reach can be pushed into the list/count query as a correlated
    // EXISTS. Tags are immutable, so this runs only on create; a row with no management tags mirrors nothing and is
    // admitted only under full read reach (fail-closed).
    private async ValueTask SyncTagsAsync(SqliteTransaction transaction, string tagTable, string keyColumn, string keyValue, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {tagTable} ({keyColumn}, TagKey, TagValue) VALUES (@k, @key, @value);";
            insert.Parameters.AddWithValue("@k", keyValue);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Builds the §14.2 read-reach predicate: a correlated EXISTS over the owner's security-tags side table mirroring the
    // caller's reach (the same rules context.Admits evaluates), and binds its @sec parameters on the command. Returns null
    // for a full reach (null), so the caller appends nothing; an untagged, system-owned row is admitted only by that full
    // reach. Shared by the list and count so they cannot drift.
    private static string? BuildReachPredicate(SqliteCommand command, AccessContext context, string tagTable, string keyColumn, string parentTable)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return null;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            tagTable,
            [keyColumn],
            "TagKey",
            "TagValue",
            parentTable,
            value =>
            {
                string p = "sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue("@" + p, value);
                return "@" + p;
            });
        return "(" + reach.ToSqlPredicate(emitter) + ")";
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SecurityRules (
            Name TEXT NOT NULL PRIMARY KEY,
            Expression TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS SecurityBindings (
            Id TEXT NOT NULL PRIMARY KEY,
            SortOrder INTEGER NOT NULL,
            ClaimType TEXT NOT NULL,
            ClaimValue TEXT NULL,
            Description TEXT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SecurityBindings_Order ON SecurityBindings (SortOrder, Id);
        CREATE TABLE IF NOT EXISTS SecurityRuleSecurityTags (
            Name TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SecurityRuleSecurityTags_Owner ON SecurityRuleSecurityTags (Name);
        CREATE INDEX IF NOT EXISTS IX_SecurityRuleSecurityTags_KV ON SecurityRuleSecurityTags (TagKey, TagValue);
        CREATE TABLE IF NOT EXISTS SecurityBindingSecurityTags (
            Id TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SecurityBindingSecurityTags_Owner ON SecurityBindingSecurityTags (Id);
        CREATE INDEX IF NOT EXISTS IX_SecurityBindingSecurityTags_KV ON SecurityBindingSecurityTags (TagKey, TagValue);
        CREATE TABLE IF NOT EXISTS SecurityPolicyMeta (
            Id INTEGER NOT NULL PRIMARY KEY,
            Generation INTEGER NOT NULL
        );
        INSERT OR IGNORE INTO SecurityPolicyMeta (Id, Generation) VALUES (0, 0);
        """;
}