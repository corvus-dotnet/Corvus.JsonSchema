// <copyright file="SqlServerSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted relationally.
/// Each source is stored as its <see cref="RegisteredSource"/> document in a <c>varbinary(max)</c> column, keyed by
/// (Name, and a discriminator over its immutable management tags) so reach-isolated sources that share a name
/// coexist; its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in
/// memory over the small candidate set for a name, since a deployment keeps those reach-disjoint. Each operation opens a
/// pooled connection, so the store is naturally concurrent.</para>
/// <para>The tag discriminator can be arbitrarily long, so it cannot be a key column (an index key is bounded at 900
/// bytes). The full discriminator is kept verbatim in an <c>nvarchar(max)</c> column for equality matching, and
/// uniqueness over (Name, Tags) is enforced via a deterministic SHA-256 hash of the discriminator held in a fixed-length
/// <c>binary(32)</c> <c>TagsHash</c> column carrying the primary key and the orderable keyset tie-breaker.</para>
/// </remarks>
public sealed class SqlServerSourceStore : ISourceStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerSourceStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerSourceStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerSourceStore>(new SqlServerSourceStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO Sources (Name, TagsHash, Tags, Etag, Document) VALUES (@n, HASHBYTES('SHA2_256', @t), @t, @etag, @doc);";
        insert.Parameters.AddWithValue("@n", draft.NameValue);
        insert.Parameters.AddWithValue("@t", tags);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        insert.Parameters.AddWithValue("@doc", json);
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
        }

        // Mirror the management tags into the queryable side table (keyed by the same in-SQL HASHBYTES discriminator) so
        // the §14.2 read reach can be pushed into the list/count query. Atomic; tags are immutable so no update re-sync.
        await SyncSecurityTagsAsync(connection, transaction, draft.NameValue, tags, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? json, _) = await FindForManagementAsync(connection, name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourcePage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (Name, TagsHash) order — an indexed PK range scan (Tags is nvarchar(max) and
            // cannot be ordered, so TagsHash binary(32) is the orderable tie-breaker, carried in the token as hex) — with
            // the §14.2 read reach pushed into the query as a correlated EXISTS over SourceSecurityTags (the same
            // predicate context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it.
            await using SqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("(Name > @n OR (Name = @n AND TagsHash > @t))");
                select.Parameters.AddWithValue("@n", cursor.Name);
                select.Parameters.AddWithValue("@t", Convert.FromHexString(cursor.TieBreaker));
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT TOP (@limit) Name, TagsHash, Document FROM Sources");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Name, TagsHash;");
            select.CommandText = sql.ToString();

            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastName = string.Empty, lastTie = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                    break;
                }

                lastName = reader.GetString(0);
                lastTie = Convert.ToHexString(reader.GetFieldValue<byte[]>(1));
                docs.Add(PersistedJson.ToPooledDocument<RegisteredSource>(reader.GetFieldValue<byte[]>(2)));
            }

            return hasMore
                ? SourcePage.Create(docs, lastName, lastTie)
                : SourcePage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Sources SET Etag = @etag, Document = @doc WHERE Name = @n AND TagsHash = HASHBYTES('SHA2_256', @t);";
        update.Parameters.AddWithValue("@etag", SourceSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@n", name);
        update.Parameters.AddWithValue("@t", tags!);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
        }

        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM Sources WHERE Name = @n AND TagsHash = HASHBYTES('SHA2_256', @t); " +
            "DELETE FROM SourceSecurityTags WHERE Name = @n AND TagsHash = HASHBYTES('SHA2_256', @t);";
        delete.Parameters.AddWithValue("@n", name);
        delete.Parameters.AddWithValue("@t", tags!);
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
        var inner = new StringBuilder("SELECT TOP (@cap) 1 AS x FROM Sources");
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

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over SourceSecurityTags mirroring the caller's reach
    // (the same rules context.Admits evaluates), keyed on the (Name, TagsHash) row discriminator. A null reach
    // (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never drift from the list.
    private static void AppendReachPredicate(List<string> conditions, SqlCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "SourceSecurityTags",
            ["Name", "TagsHash"],
            "TagKey",
            "TagValue",
            "Sources",
            value =>
            {
                string p = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors a source's management tags into the queryable side table (keyed by the same in-SQL HASHBYTES discriminator)
    // so the reach can be pushed into the list/count query. Tags are immutable, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(SqlConnection connection, SqlTransaction transaction, string name, string canonicalTags, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using SqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SourceSecurityTags (Name, TagsHash, TagKey, TagValue) VALUES (@n, HASHBYTES('SHA2_256', @t), @key, @value);";
            insert.Parameters.AddWithValue("@n", name);
            insert.Parameters.AddWithValue("@t", canonicalTags);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the raw Tags value, re-hashed in-SQL for row addressing). A source outside reach is invisible.
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(SqlConnection connection, string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Sources WHERE Name = @n;";
        select.Parameters.AddWithValue("@n", name);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string tags = reader.GetString(0);
            byte[] json = reader.GetFieldValue<byte[]>(1);
            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, tags);
            }
        }

        return (null, null);
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
        IF OBJECT_ID(N'Sources', N'U') IS NULL
        BEGIN
            CREATE TABLE Sources (
                Name NVARCHAR(450) NOT NULL,
                TagsHash BINARY(32) NOT NULL,
                Tags NVARCHAR(MAX) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_Sources PRIMARY KEY (Name, TagsHash)
            );
            CREATE INDEX IX_Sources_Name ON Sources (Name);
        END;
        IF OBJECT_ID(N'SourceSecurityTags', N'U') IS NULL
        BEGIN
            CREATE TABLE SourceSecurityTags (
                Name NVARCHAR(450) NOT NULL,
                TagsHash BINARY(32) NOT NULL,
                TagKey NVARCHAR(200) NOT NULL,
                TagValue NVARCHAR(200) NOT NULL
            );
            CREATE INDEX IX_SourceSecurityTags_owner ON SourceSecurityTags (Name, TagsHash);
            CREATE INDEX IX_SourceSecurityTags_kv ON SourceSecurityTags (TagKey, TagValue);
        END;
        """;
}