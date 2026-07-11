// <copyright file="MySqlSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted relationally. Each binding is stored as its
/// <see cref="SourceCredentialBinding"/> document in a <c>LONGBLOB</c> column, keyed by (SourceName, Environment, and a
/// discriminator over its immutable management/usage tags) so tenant-/workflow-scoped bindings for the same
/// source/environment coexist; its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage
/// path by label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a
/// deployment keeps those reach-disjoint. Each operation opens a pooled connection, so the store is naturally
/// concurrent.</para>
/// <para>The tag discriminator can be arbitrarily long, but MySQL caps index key prefixes and cannot index a full
/// <c>TEXT</c> column, so the binding's full discriminator is stored verbatim in a <c>LONGTEXT</c> <c>Tags</c> column
/// while uniqueness and row addressing run off a deterministic fixed-length <c>CHAR(64)</c> SHA-256 hex hash of
/// (SourceName, Environment, discriminator) in <c>TagsHash</c>, which carries the unique index.</para>
/// </remarks>
public sealed class MySqlSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlSourceCredentialStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlSourceCredentialStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlSourceCredentialStore>(
            new MySqlSourceCredentialStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlSourceCredentialStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlSourceCredentialStore>(
            new MySqlSourceCredentialStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
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
        string tagsHash = TagsHash(draft.SourceNameValue, draft.EnvironmentValue, tags);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO SourceCredentials (SourceName, Environment, TagsHash, Tags, Etag, Document) VALUES (@s, @e, @h, @t, @etag, @doc);";
        insert.Parameters.AddWithValue("@s", draft.SourceNameValue);
        insert.Parameters.AddWithValue("@e", draft.EnvironmentValue);
        insert.Parameters.AddWithValue("@h", tagsHash);
        insert.Parameters.AddWithValue("@t", tags);
        insert.Parameters.AddWithValue("@etag", etag.Value!);
        insert.Parameters.AddWithValue("@doc", json);
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            throw new InvalidOperationException($"A source credential binding for '{draft.SourceNameValue}@{draft.EnvironmentValue}' with those security tags already exists.");
        }

        // Mirror the MANAGEMENT tags (the reach discriminator, distinct from the row's combined mgmt+usage Tags key)
        // into the queryable side table (keyed by TagsHash, the orderable row discriminator) so the §14.2 read reach can
        // be pushed into the list/count query. Atomic; tags are immutable so no re-sync on update.
        await SyncSecurityTagsAsync(connection, transaction, draft.SourceNameValue, draft.EnvironmentValue, tagsHash, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (SourceName, Environment, TagsHash) order — an indexed range scan over the
            // primary key, not a table load (Tags is LONGTEXT and cannot be a key/ordered column, so the orderable PK
            // component TagsHash CHAR(64) hex is the tie-breaker, carried verbatim in the token) — with the §14.2 read
            // reach pushed into the query as a correlated EXISTS over SourceCredentialSecurityTags (the same predicate
            // context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it. LIMIT bounds it.
            await using MySqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("(SourceName > @s OR (SourceName = @s AND Environment > @e) OR (SourceName = @s AND Environment = @e AND TagsHash > @t))");
                select.Parameters.AddWithValue("@s", cursor.SourceName);
                select.Parameters.AddWithValue("@e", cursor.Environment);
                select.Parameters.AddWithValue("@t", cursor.TieBreaker);
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT SourceName, Environment, TagsHash, Document FROM SourceCredentials");
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY SourceName, Environment, TagsHash LIMIT @limit;");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            select.CommandText = sql.ToString();

            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
                lastTie = reader.GetString(2);
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using MySqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE SourceCredentials SET Etag = @etag, Document = @doc WHERE SourceName = @s AND Environment = @e AND TagsHash = @h;";
        update.Parameters.AddWithValue("@etag", SourceCredentialSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@s", sourceName);
        update.Parameters.AddWithValue("@e", environment);
        update.Parameters.AddWithValue("@h", TagsHash(sourceName, environment, tags!));
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        string tagsHash = TagsHash(sourceName, environment, tags!);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM SourceCredentials WHERE SourceName = @s AND Environment = @e AND TagsHash = @h; " +
            "DELETE FROM SourceCredentialSecurityTags WHERE SourceName = @s AND Environment = @e AND TagsHash = @h;";
        delete.Parameters.AddWithValue("@s", sourceName);
        delete.Parameters.AddWithValue("@e", environment);
        delete.Parameters.AddWithValue("@h", tagsHash);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        var conditions = new List<string>(1);
        AppendReachPredicate(conditions, select, context);

        // Bounded count: COUNT over a subquery capped at cap + 1, so the scan stops one row past the cap; the (cap+1)th
        // row trips Capped. Same reach predicate as the list (AppendReachPredicate) — cannot drift.
        var inner = new StringBuilder("SELECT 1 FROM SourceCredentials");
        if (conditions.Count > 0)
        {
            inner.Append(" WHERE ").Append(string.Join(" AND ", conditions));
        }

        inner.Append(" LIMIT @cap");
        select.Parameters.AddWithValue("@cap", cap + 1);
        select.CommandText = "SELECT COUNT(*) FROM (" + inner + ") AS bounded;";
        object? result = await select.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        long total = result is long l ? l : Convert.ToInt64(result, CultureInfo.InvariantCulture);
        return total > cap ? (cap, true) : ((int)total, false);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s;";
        select.Parameters.AddWithValue("@s", sourceName);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    // The fixed-length, deterministic row-addressing key for a binding: a SHA-256 hex digest over the identity tuple
    // (SourceName, Environment, tag discriminator), so an arbitrarily long discriminator carries a CHAR(64) unique
    // index that MySQL can address in full. The fields are length-prefixed so no concatenation collides.
    private static string TagsHash(string sourceName, string environment, string tags)
    {
        var builder = new StringBuilder(sourceName.Length + environment.Length + tags.Length + 24);
        builder.Append(sourceName.Length).Append('\u0001').Append(sourceName)
               .Append(environment.Length).Append('\u0001').Append(environment)
               .Append(tags.Length).Append('\u0001').Append(tags);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()), hash);
        return Convert.ToHexStringLower(hash);
    }

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over SourceCredentialSecurityTags mirroring the
    // caller's reach (the same rules context.Admits evaluates), keyed on the (SourceName, Environment, TagsHash) row
    // discriminator. A null reach (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never
    // drift from the list it annotates.
    private static void AppendReachPredicate(List<string> conditions, MySqlCommand command, AccessContext context)
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

    // Mirrors a binding's MANAGEMENT tags into the queryable side table (keyed by TagsHash) so the reach can be pushed
    // into the list/count query. Reach filters on the management tags (distinct from the row's combined mgmt+usage Tags
    // discriminator). Tags are immutable after creation, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(MySqlConnection connection, MySqlTransaction transaction, string sourceName, string environment, string tagsHash, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using MySqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SourceCredentialSecurityTags (SourceName, Environment, TagsHash, TagKey, TagValue) VALUES (@s, @e, @h, @key, @value);";
            insert.Parameters.AddWithValue("@s", sourceName);
            insert.Parameters.AddWithValue("@e", environment);
            insert.Parameters.AddWithValue("@h", tagsHash);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its
    // bytes and its tag discriminator (the row key value). A binding outside reach is invisible (non-disclosing).
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(MySqlConnection connection, string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SourceCredentials (
            SourceName VARCHAR(255) NOT NULL,
            Environment VARCHAR(255) NOT NULL,
            TagsHash CHAR(64) NOT NULL,
            Tags LONGTEXT NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            PRIMARY KEY (SourceName, Environment, TagsHash),
            INDEX IX_SourceCredentials_Source (SourceName)
        );
        CREATE TABLE IF NOT EXISTS SourceCredentialSecurityTags (
            SourceName VARCHAR(255) NOT NULL,
            Environment VARCHAR(255) NOT NULL,
            TagsHash CHAR(64) NOT NULL,
            TagKey VARCHAR(191) NOT NULL,
            TagValue VARCHAR(191) NOT NULL,
            INDEX IX_SourceCredentialSecurityTags_owner (SourceName, Environment, TagsHash),
            INDEX IX_SourceCredentialSecurityTags_kv (TagKey, TagValue)
        );
        """;
}