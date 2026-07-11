// <copyright file="MySqlEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MySqlConnector;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted relationally. Each
/// environment is stored as its <see cref="Environment"/> document in a <c>LONGBLOB</c> column, keyed by (Name, and a
/// discriminator over its immutable management tags) so reach-isolated environments that share a name coexist; its etag
/// is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// <para>Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in
/// memory over the small candidate set for a name, since a deployment keeps those reach-disjoint. Each operation opens a
/// pooled connection, so the store is naturally concurrent.</para>
/// <para>The tag discriminator can be arbitrarily long, but MySQL cannot index a full <c>TEXT</c> column, so the full
/// discriminator is stored verbatim in a <c>LONGTEXT</c> <c>Tags</c> column while uniqueness and row addressing run off a
/// deterministic <c>CHAR(64)</c> SHA-256 hex hash of (Name, discriminator) in <c>TagsHash</c>, which carries the unique
/// index and is the orderable keyset tie-breaker.</para>
/// </remarks>
public sealed class MySqlEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlEnvironmentStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<MySqlEnvironmentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlEnvironmentStore>(
            new MySqlEnvironmentStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlEnvironmentStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlEnvironmentStore>(
            new MySqlEnvironmentStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        string tagsHash = TagsHash(draft.NameValue, tags);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO Environments (Name, TagsHash, Tags, Etag, Document) VALUES (@n, @h, @t, @etag, @doc);";
        insert.Parameters.AddWithValue("@n", draft.NameValue);
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
            throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
        }

        // Mirror the management tags into the queryable side table (keyed by TagsHash, the orderable row discriminator)
        // so the §14.2 read reach can be pushed into the list/count query. Atomic; tags are immutable so no update re-sync.
        await SyncSecurityTagsAsync(connection, transaction, draft.NameValue, tagsHash, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? json, _) = await FindForManagementAsync(connection, name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentPage> ListAsync(AccessContext context, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        (string Name, string TieBreaker) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = EnvironmentContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (Name, TagsHash) order — an indexed PK range scan (Tags is LONGTEXT and
            // cannot be ordered, so TagsHash CHAR(64) hex is the orderable tie-breaker) — with the §14.2 read reach
            // pushed into the query as a correlated EXISTS over EnvironmentSecurityTags (the same predicate
            // context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it. LIMIT bounds it.
            await using MySqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("(Name > @n OR (Name = @n AND TagsHash > @t))");
                select.Parameters.AddWithValue("@n", cursor.Name);
                select.Parameters.AddWithValue("@t", cursor.TieBreaker);
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT Name, TagsHash, Document FROM Environments");
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Name, TagsHash LIMIT @limit;");
            select.Parameters.AddWithValue("@limit", pageSize + 1);
            select.CommandText = sql.ToString();

            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastName = string.Empty, lastTie = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                    break;
                }

                lastName = reader.GetString(0);
                lastTie = reader.GetString(1);
                docs.Add(PersistedJson.ToPooledDocument<Environment>(reader.GetFieldValue<byte[]>(2)));
            }

            return hasMore
                ? EnvironmentPage.Create(docs, lastName, lastTie)
                : EnvironmentPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using MySqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Environments SET Etag = @etag, Document = @doc WHERE Name = @n AND TagsHash = @h;";
        update.Parameters.AddWithValue("@etag", EnvironmentSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@n", name);
        update.Parameters.AddWithValue("@h", TagsHash(name, tags!));
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        string tagsHash = TagsHash(name, tags!);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM Environments WHERE Name = @n AND TagsHash = @h; " +
            "DELETE FROM EnvironmentSecurityTags WHERE Name = @n AND TagsHash = @h;";
        delete.Parameters.AddWithValue("@n", name);
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
        var inner = new StringBuilder("SELECT 1 FROM Environments");
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
    public async ValueTask DisposeAsync()
    {
        if (this.ownsDataSource)
        {
            await this.dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over EnvironmentSecurityTags mirroring the caller's
    // reach (the same rules context.Admits evaluates), keyed on the (Name, TagsHash) row discriminator. A null reach
    // (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never drift from the list.
    private static void AppendReachPredicate(List<string> conditions, MySqlCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "EnvironmentSecurityTags",
            ["Name", "TagsHash"],
            "TagKey",
            "TagValue",
            "Environments",
            value =>
            {
                string p = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors an environment's management tags into the queryable side table (keyed by TagsHash) so the reach can be
    // pushed into the list/count query. Management tags are immutable after creation, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(MySqlConnection connection, MySqlTransaction transaction, string name, string tagsHash, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using MySqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO EnvironmentSecurityTags (Name, TagsHash, TagKey, TagValue) VALUES (@n, @h, @key, @value);";
            insert.Parameters.AddWithValue("@n", name);
            insert.Parameters.AddWithValue("@h", tagsHash);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask ProvisionAsync(MySqlConnection connection, CancellationToken cancellationToken)
    {
        await using MySqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The fixed-length, deterministic row-addressing key for an environment: a SHA-256 hex digest over the identity
    // tuple (Name, tag discriminator), so an arbitrarily long discriminator carries a CHAR(64) unique index MySQL can
    // address in full. The fields are length-prefixed so no concatenation collides.
    private static string TagsHash(string name, string tags)
    {
        var builder = new StringBuilder(name.Length + tags.Length + 16);
        builder.Append(name.Length).Append((char)1).Append(name)
               .Append(tags.Length).Append((char)1).Append(tags);
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()), hash);
        return Convert.ToHexStringLower(hash);
    }

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the raw Tags value, re-hashed for row addressing). An environment outside reach is invisible.
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(MySqlConnection connection, string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Environments WHERE Name = @n;";
        select.Parameters.AddWithValue("@n", name);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            string tags = reader.GetString(0);
            byte[] json = reader.GetFieldValue<byte[]>(1);
            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(json);
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
        CREATE TABLE IF NOT EXISTS Environments (
            Name VARCHAR(255) NOT NULL,
            TagsHash CHAR(64) NOT NULL,
            Tags LONGTEXT NOT NULL,
            Etag VARCHAR(255) NOT NULL,
            Document LONGBLOB NOT NULL,
            PRIMARY KEY (Name, TagsHash),
            INDEX IX_Environments_Name (Name)
        );
        CREATE TABLE IF NOT EXISTS EnvironmentSecurityTags (
            Name VARCHAR(255) NOT NULL,
            TagsHash CHAR(64) NOT NULL,
            TagKey VARCHAR(191) NOT NULL,
            TagValue VARCHAR(191) NOT NULL,
            INDEX IX_EnvironmentSecurityTags_owner (Name, TagsHash),
            INDEX IX_EnvironmentSecurityTags_kv (TagKey, TagValue)
        );
        """;
}