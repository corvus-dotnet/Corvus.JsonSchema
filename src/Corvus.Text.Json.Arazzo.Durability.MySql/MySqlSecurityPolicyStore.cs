// <copyright file="MySqlSecurityPolicyStore.cs" company="Endjin Limited">
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
/// A MySQL-backed <see cref="ISecurityPolicyStore"/> — the row-authorization policy (named rules + claim→rule
/// bindings, design §14.2) persisted relationally. Each record is stored as its Corvus.Text.Json schema document
/// (<see cref="SecurityRuleDocument"/> / <see cref="SecurityBindingDocument"/>) in a <c>LONGBLOB</c> column, with
/// its etag in a column for the optimistic-concurrency check; a single-row meta table holds the monotonic
/// generation a resolver caches against.
/// </summary>
/// <remarks>Each operation opens a pooled connection, so the store is naturally concurrent.</remarks>
public sealed class MySqlSecurityPolicyStore : ISecurityPolicyStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlSecurityPolicyStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlSecurityPolicyStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlSecurityPolicyStore>(
            new MySqlSecurityPolicyStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlSecurityPolicyStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlSecurityPolicyStore>(
            new MySqlSecurityPolicyStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>> AddRuleAsync(string name, SecurityRuleDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes as the LONGBLOB parameter
        // (MySqlConnector carries the exact length for a ReadOnlyMemory blob — no GC document array, no second copy). The
        // document is returned on success, disposed on failure.
        ParsedJsonDocument<SecurityRuleDocument> doc = SecurityPolicySerialization.SerializeNewRuleDoc(name, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using MySqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO SecurityRules (Name, Expression, Etag, Document) VALUES (@name, @expression, @etag, @doc);";
            insert.Parameters.AddWithValue("@name", name);
            insert.Parameters.AddWithValue("@expression", draft.ExpressionValue);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", utf8);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                throw new InvalidOperationException($"A security rule named '{name}' already exists.");
            }

            await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> GetRuleAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityRuleDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityRuleDocument>> ListRulesAsync(CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityRulePage> ListRulesAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityRulePage.DefaultPageSize;
        string? after = DecodeRuleCursor(pageToken);
        string? like = BuildLike(q);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM SecurityRules");
        var conditions = new List<string>(2);
        if (after is not null)
        {
            // Name is the PRIMARY KEY declared COLLATE utf8mb4_bin → a byte-ordinal seek + order (the in-memory pager's).
            conditions.Add("Name > @after");
            select.Parameters.AddWithValue("@after", after);
        }

        if (like is not null)
        {
            // q is case-insensitive (in-memory OrdinalIgnoreCase); Name is binary-collated, so apply a case-insensitive
            // collation for its LIKE only (Expression already uses the case-insensitive default collation).
            conditions.Add("(Name COLLATE utf8mb4_0900_ai_ci LIKE @q ESCAPE '\\\\' OR Expression LIKE @q ESCAPE '\\\\')");
            select.Parameters.AddWithValue("@q", like);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY Name LIMIT @limit;");
        select.Parameters.AddWithValue("@limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<SecurityRuleDocument>(pageSize);
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

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityRuleDocument>?> UpdateRuleAsync(string name, SecurityRuleDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, "SecurityRules", "Name", name, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // merged result into the pooled buffer the returned document owns — bound as the LONGBLOB parameter (no GC array, no copy).
        using ParsedJsonDocument<SecurityRuleDocument> current = ParsedJsonDocument<SecurityRuleDocument>.Parse(existing.AsMemory());
        ParsedJsonDocument<SecurityRuleDocument> updated = SecurityPolicySerialization.SerializeUpdatedRuleDoc(current.RootElement, "rule", name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using MySqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE SecurityRules SET Expression = @expression, Etag = @etag, Document = @doc WHERE Name = @k;";
            update.Parameters.AddWithValue("@expression", draft.ExpressionValue);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", utf8);
            update.Parameters.AddWithValue("@k", name);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteRuleAsync(string name, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityRules", "Name", "rule", name, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>> AddBindingAsync(SecurityBindingDocument draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        string id = "bnd-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();

        // Serialize once into the pooled buffer the returned document owns; bind its exact bytes (no GC array, no copy).
        ParsedJsonDocument<SecurityBindingDocument> doc = SecurityPolicySerialization.SerializeNewBindingDoc(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(doc.RootElement).Memory;
            await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
            await using MySqlCommand insert = connection.CreateCommand();
            insert.CommandText = "INSERT INTO SecurityBindings (Id, SortOrder, ClaimType, ClaimValue, Description, Etag, Document) VALUES (@id, @order, @claimType, @claimValue, @description, @etag, @doc);";
            insert.Parameters.AddWithValue("@id", id);
            insert.Parameters.AddWithValue("@order", draft.OrderValue);
            insert.Parameters.AddWithValue("@claimType", draft.ClaimTypeValue);
            insert.Parameters.AddWithValue("@claimValue", draft.ClaimValue.IsNotUndefined() ? (string)draft.ClaimValue : DBNull.Value);
            insert.Parameters.AddWithValue("@description", draft.Description.IsNotUndefined() ? (string)draft.Description : DBNull.Value);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", utf8);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
            return doc;
        }
        catch
        {
            doc.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> GetBindingAsync(string id, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? doc = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        return doc is null ? null : ParsedJsonDocument<SecurityBindingDocument>.Parse(doc.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<PooledDocumentList<SecurityBindingDocument>> ListBindingsAsync(CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        return await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<SecurityBindingPage> ListBindingsAsync(int limit, JsonString pageToken, JsonString q, CancellationToken cancellationToken)
    {
        int pageSize = limit > 0 ? limit : SecurityBindingPage.DefaultPageSize;
        bool hasCursor = DecodeBindingCursor(pageToken, out int cursorOrder, out string? cursorId);
        string? like = BuildLike(q);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        var sql = new StringBuilder("SELECT Document FROM SecurityBindings");
        var conditions = new List<string>(2);
        if (hasCursor)
        {
            // Id is the PRIMARY KEY declared COLLATE utf8mb4_bin; IX_SecurityBindings_Order (SortOrder, Id) serves the keyset.
            conditions.Add("(SortOrder > @order OR (SortOrder = @order AND Id > @id))");
            select.Parameters.AddWithValue("@order", cursorOrder);
            select.Parameters.AddWithValue("@id", cursorId!);
        }

        if (like is not null)
        {
            // The q columns use the case-insensitive default collation, so plain LIKE is case-insensitive (== in-memory).
            conditions.Add("(ClaimType LIKE @q ESCAPE '\\\\' OR ClaimValue LIKE @q ESCAPE '\\\\' OR Description LIKE @q ESCAPE '\\\\')");
            select.Parameters.AddWithValue("@q", like);
        }

        if (conditions.Count > 0)
        {
            sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        sql.Append(" ORDER BY SortOrder, Id LIMIT @limit;");
        select.Parameters.AddWithValue("@limit", pageSize + 1);
        select.CommandText = sql.ToString();

        var page = new PooledDocumentList<SecurityBindingDocument>(pageSize);
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

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SecurityBindingDocument>?> UpdateBindingAsync(string id, SecurityBindingDocument draft, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(actor);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        byte[]? existing = await DocumentAsync(connection, "SecurityBindings", "Id", id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        WorkflowEtag etag = NewEtag();

        // Parse the existing document NON-COPYING over the driver's array (the read leaf), check the etag, and serialize the
        // merged result into the pooled buffer the returned document owns — bound as the LONGBLOB parameter (no GC array, no copy).
        using ParsedJsonDocument<SecurityBindingDocument> current = ParsedJsonDocument<SecurityBindingDocument>.Parse(existing.AsMemory());
        ParsedJsonDocument<SecurityBindingDocument> updated = SecurityPolicySerialization.SerializeUpdatedBindingDoc(current.RootElement, "binding", id, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), etag);
        try
        {
            ReadOnlyMemory<byte> utf8 = JsonMarshal.GetRawUtf8Value(updated.RootElement).Memory;
            await using MySqlCommand update = connection.CreateCommand();
            update.CommandText = "UPDATE SecurityBindings SET SortOrder = @order, ClaimType = @claimType, ClaimValue = @claimValue, Description = @description, Etag = @etag, Document = @doc WHERE Id = @k;";
            update.Parameters.AddWithValue("@order", draft.OrderValue);
            update.Parameters.AddWithValue("@claimType", draft.ClaimTypeValue);
            update.Parameters.AddWithValue("@claimValue", draft.ClaimValue.IsNotUndefined() ? (string)draft.ClaimValue : DBNull.Value);
            update.Parameters.AddWithValue("@description", draft.Description.IsNotUndefined() ? (string)draft.Description : DBNull.Value);
            update.Parameters.AddWithValue("@etag", etag.Value!);
            update.Parameters.AddWithValue("@doc", utf8);
            update.Parameters.AddWithValue("@k", id);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
            return updated;
        }
        catch
        {
            updated.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public ValueTask<bool> DeleteBindingAsync(string id, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
        => this.DeleteAsync("SecurityBindings", "Id", "binding", id, expectedEtag, cancellationToken);

    /// <inheritdoc/>
    public async ValueTask<SecurityPolicySnapshot> LoadSnapshotAsync(CancellationToken cancellationToken)
    {
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityRuleDocument> rules = await ReadRulesAsync(connection, cancellationToken).ConfigureAwait(false);
        PooledDocumentList<SecurityBindingDocument> bindings = await ReadBindingsAsync(connection, cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Generation FROM SecurityPolicyMeta WHERE Id = 0;";
        object? gen = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return new SecurityPolicySnapshot(rules, bindings, gen is long g ? g : 0);
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

    // Decodes the keyset cursor (rule name) from the request page token; the name reified to a string only for @after (leaf).
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

    // Decodes the keyset cursor (order, id) from the request page token; the id reified to a string only for @id (leaf).
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
    // only at this LIKE-parameter leaf; null when q is undefined (no filter).
    private static string? BuildLike(JsonString q)
        => q.IsNotUndefined() ? "%" + EscapeLike((string)q) + "%" : null;

    private static string EscapeLike(string value)
        => value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

    private static async ValueTask<byte[]?> DocumentAsync(MySqlConnection connection, string table, string column, string key, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Document FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return result is byte[] bytes ? bytes : null;
    }

    private static async ValueTask<PooledDocumentList<SecurityRuleDocument>> ReadRulesAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityRuleDocument>();
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityRules ORDER BY Name;";
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<SecurityRuleDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    private static async ValueTask<PooledDocumentList<SecurityBindingDocument>> ReadBindingsAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        var list = new PooledDocumentList<SecurityBindingDocument>();
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SecurityBindings ORDER BY SortOrder, Id;";
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            list.Add(ParsedJsonDocument<SecurityBindingDocument>.Parse(reader.GetFieldValue<byte[]>(0).AsMemory()));
        }

        return list;
    }

    private static async ValueTask BumpGenerationAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using MySqlCommand bump = connection.CreateCommand();
        bump.CommandText = "UPDATE SecurityPolicyMeta SET Generation = Generation + 1 WHERE Id = 0;";
        await bump.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private async ValueTask<bool> DeleteAsync(string table, string column, string kind, string key, WorkflowEtag expectedEtag, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = $"SELECT Etag FROM {table} WHERE {column} = @k;";
        select.Parameters.AddWithValue("@k", key);
        object? etag = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        if (etag is not string current)
        {
            return false;
        }

        SecurityPolicySerialization.EnsureEtag(kind, key, expectedEtag, new WorkflowEtag(current));
        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = $"DELETE FROM {table} WHERE {column} = @k;";
        delete.Parameters.AddWithValue("@k", key);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await BumpGenerationAsync(connection, cancellationToken).ConfigureAwait(false);
        return true;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SecurityRules (
            Name VARCHAR(255) COLLATE utf8mb4_bin NOT NULL PRIMARY KEY,
            Expression TEXT NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL
        );
        CREATE TABLE IF NOT EXISTS SecurityBindings (
            Id VARCHAR(255) COLLATE utf8mb4_bin NOT NULL PRIMARY KEY,
            SortOrder INT NOT NULL,
            ClaimType TEXT NOT NULL,
            ClaimValue TEXT NULL,
            Description TEXT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            INDEX IX_SecurityBindings_Order (SortOrder, Id)
        );
        CREATE TABLE IF NOT EXISTS SecurityPolicyMeta (
            Id INT NOT NULL PRIMARY KEY,
            Generation BIGINT NOT NULL
        );
        INSERT IGNORE INTO SecurityPolicyMeta (Id, Generation) VALUES (0, 0);
        """;
}