// <copyright file="SqlServerSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted relationally. Each binding is stored as its
/// <see cref="SourceCredentialBinding"/> document in a <c>varbinary(max)</c> column, keyed by (SourceName, Environment,
/// and a discriminator over its immutable management/usage tags) so tenant-/workflow-scoped bindings for the same
/// source/environment coexist; its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage
/// path by label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a
/// deployment keeps those reach-disjoint. Each operation opens a pooled connection, so the store is naturally
/// concurrent.</para>
/// <para>The tag discriminator can be arbitrarily long, so it cannot itself be a key column in SQL Server (an index key
/// is bounded at 900 bytes). The full discriminator is kept verbatim in an <c>nvarchar(max)</c> column for equality
/// matching, and uniqueness over (SourceName, Environment, Tags) is enforced via a deterministic SHA-256 hash of the
/// discriminator held in a fixed-length <c>binary(32)</c> column carrying the primary key.</para>
/// </remarks>
public sealed class SqlServerSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerSourceCredentialStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerSourceCredentialStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerSourceCredentialStore>(new SqlServerSourceCredentialStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO SourceCredentials (SourceName, Environment, TagsHash, Tags, Etag, Document) VALUES (@s, @e, HASHBYTES('SHA2_256', @t), @t, @etag, @doc);";
        insert.Parameters.AddWithValue("@s", draft.SourceNameValue);
        insert.Parameters.AddWithValue("@e", draft.EnvironmentValue);
        insert.Parameters.AddWithValue("@t", tags);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        insert.Parameters.AddWithValue("@doc", json);
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            throw new InvalidOperationException($"A source credential binding for '{draft.SourceNameValue}@{draft.EnvironmentValue}' with those security tags already exists.");
        }

        // Mirror the MANAGEMENT tags (the reach discriminator, distinct from the row's combined mgmt+usage Tags key)
        // into the queryable side table (keyed by the same in-SQL HASHBYTES discriminator) so the §14.2 read reach can
        // be pushed into the list/count query. Atomic; tags are immutable so no re-sync on update.
        await SyncSecurityTagsAsync(connection, transaction, draft.SourceNameValue, draft.EnvironmentValue, tags, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? json, _) = await FindForManagementAsync(connection, sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<SourceCredentialPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string SourceName, string Environment, string TieBreaker) cursor = (string.Empty, string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = SourceCredentialContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (SourceName, Environment, TagsHash) order — an indexed range scan over the
            // primary key, not a table load (Tags is nvarchar(max) and cannot be a key/ordered column, so the orderable
            // PK component TagsHash binary(32) is the tie-breaker, carried in the token as hex) — with the §14.2 read
            // reach pushed into the query as a correlated EXISTS over SourceCredentialSecurityTags (the same predicate
            // context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it.
            await using SqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("(SourceName > @s OR (SourceName = @s AND Environment > @e) OR (SourceName = @s AND Environment = @e AND TagsHash > @t))");
                select.Parameters.AddWithValue("@s", cursor.SourceName);
                select.Parameters.AddWithValue("@e", cursor.Environment);
                select.Parameters.AddWithValue("@t", Convert.FromHexString(cursor.TieBreaker));
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT TOP (@limit) SourceName, Environment, TagsHash, Document FROM SourceCredentials");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY SourceName, Environment, TagsHash;");
            select.CommandText = sql.ToString();

            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastSource = string.Empty, lastEnv = string.Empty, lastTie = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                    break;
                }

                lastSource = reader.GetString(0);
                lastEnv = reader.GetString(1);
                lastTie = Convert.ToHexString(reader.GetFieldValue<byte[]>(2));
                docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(3)));
            }

            return hasMore
                ? SourceCredentialPage.Create(docs, lastSource, lastEnv, lastTie)
                : SourceCredentialPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SourceCredentials SET Etag = @etag, Document = @doc WHERE SourceName = @s AND Environment = @e AND TagsHash = HASHBYTES('SHA2_256', @t);";
        update.Parameters.AddWithValue("@etag", SourceCredentialSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@s", sourceName);
        update.Parameters.AddWithValue("@e", environment);
        update.Parameters.AddWithValue("@t", tags!);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM SourceCredentials WHERE SourceName = @s AND Environment = @e AND TagsHash = HASHBYTES('SHA2_256', @t); " +
            "DELETE FROM SourceCredentialSecurityTags WHERE SourceName = @s AND Environment = @e AND TagsHash = HASHBYTES('SHA2_256', @t);";
        delete.Parameters.AddWithValue("@s", sourceName);
        delete.Parameters.AddWithValue("@e", environment);
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
        var inner = new StringBuilder("SELECT TOP (@cap) 1 AS x FROM SourceCredentials");
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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(0));
            if (candidate.RootElement.IsUsableBy(runTags))
            {
                return candidate;
            }

            candidate.Dispose();
        }

        return null;
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s;";
        select.Parameters.AddWithValue("@s", sourceName);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        bool any = false;
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            any = true;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(0));
            if (candidate.RootElement.IsUsableBy(tags))
            {
                return CredentialSourceAccess.Granted;
            }
        }

        return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its
    // bytes and its tag discriminator (the row key). A binding outside reach is invisible (non-disclosing).
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(SqlConnection connection, string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string tags = reader.GetString(0);
            byte[] json = reader.GetFieldValue<byte[]>(1);
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, tags);
            }
        }

        return (null, null);
    }

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over SourceCredentialSecurityTags mirroring the
    // caller's reach (the same rules context.Admits evaluates), keyed on the (SourceName, Environment, TagsHash) row
    // discriminator. A null reach (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never
    // drift from the list it annotates.
    private static void AppendReachPredicate(List<string> conditions, SqlCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "SourceCredentialSecurityTags",
            ["SourceName", "Environment", "TagsHash"],
            "TagKey",
            "TagValue",
            "SourceCredentials",
            value =>
            {
                string p = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors a binding's MANAGEMENT tags into the queryable side table (keyed by the same in-SQL HASHBYTES
    // discriminator) so the reach can be pushed into the list/count query. Reach filters on the management tags
    // (distinct from the row's combined mgmt+usage Tags discriminator). Tags are immutable, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(SqlConnection connection, SqlTransaction transaction, string sourceName, string environment, string canonicalTags, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using SqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SourceCredentialSecurityTags (SourceName, Environment, TagsHash, TagKey, TagValue) VALUES (@s, @e, HASHBYTES('SHA2_256', @t), @key, @value);";
            insert.Parameters.AddWithValue("@s", sourceName);
            insert.Parameters.AddWithValue("@e", environment);
            insert.Parameters.AddWithValue("@t", canonicalTags);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
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
        IF OBJECT_ID(N'SourceCredentials', N'U') IS NULL
        BEGIN
            CREATE TABLE SourceCredentials (
                SourceName NVARCHAR(450) NOT NULL,
                Environment NVARCHAR(450) NOT NULL,
                TagsHash BINARY(32) NOT NULL,
                Tags NVARCHAR(MAX) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_SourceCredentials PRIMARY KEY (SourceName, Environment, TagsHash)
            );
            CREATE INDEX IX_SourceCredentials_Source ON SourceCredentials (SourceName);
        END;
        IF OBJECT_ID(N'SourceCredentialSecurityTags', N'U') IS NULL
        BEGIN
            CREATE TABLE SourceCredentialSecurityTags (
                SourceName NVARCHAR(450) NOT NULL,
                Environment NVARCHAR(450) NOT NULL,
                TagsHash BINARY(32) NOT NULL,
                TagKey NVARCHAR(200) NOT NULL,
                TagValue NVARCHAR(200) NOT NULL
            );
            CREATE INDEX IX_SourceCredentialSecurityTags_owner ON SourceCredentialSecurityTags (SourceName, Environment, TagsHash);
            CREATE INDEX IX_SourceCredentialSecurityTags_kv ON SourceCredentialSecurityTags (TagKey, TagValue);
        END;
        """;
}