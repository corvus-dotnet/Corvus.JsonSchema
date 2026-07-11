// <copyright file="PostgresEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Npgsql;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Postgres;

/// <summary>
/// A PostgreSQL-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted relationally.
/// Each environment is stored as its <see cref="Environment"/> document in a <c>bytea</c> column, keyed by (Name, and a
/// discriminator over its immutable management tags) so reach-isolated environments that share a name coexist; its etag
/// is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. Each operation opens a pooled
/// connection, so the store is naturally concurrent.
/// </remarks>
public sealed class PostgresEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private readonly NpgsqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private PostgresEnvironmentStore(NpgsqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
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
    public static ValueTask<PostgresEnvironmentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentStore>(
            new PostgresEnvironmentStore(NpgsqlDataSource.Create(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">An Npgsql data source.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<PostgresEnvironmentStore> ConnectAsync(NpgsqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<PostgresEnvironmentStore>(
            new PostgresEnvironmentStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string canonicalTags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand insert = connection.CreateCommand();
        insert.Transaction = transaction;
        insert.CommandText = "INSERT INTO Environments (Name, Tags, Etag, Document) VALUES (@n, @t, @etag, @doc);";
        insert.Parameters.AddWithValue("n", draft.NameValue);
        insert.Parameters.AddWithValue("t", canonicalTags);
        insert.Parameters.AddWithValue("etag", etag.Value!);
        insert.Parameters.AddWithValue("doc", json);
        try
        {
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
        }

        // Mirror the management tags into the queryable side table so the §14.2 read reach can be pushed into the
        // list/count query (a correlated EXISTS) rather than evaluated per row in memory. Atomic with the row insert;
        // the tags are immutable, so no re-sync is needed on update.
        await SyncSecurityTagsAsync(connection, transaction, draft.NameValue, canonicalTags, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (Name, Tags) order — an indexed range scan — with the §14.2 read reach
            // pushed into the query as a correlated EXISTS over EnvironmentSecurityTags (the same predicate
            // context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it. The
            // ORDER BY drives the page and LIMIT bounds the read to one page + 1 (lookahead).
            await using NpgsqlCommand select = connection.CreateCommand();
            var conditions = new List<string>(2);
            if (hasCursor)
            {
                conditions.Add("(Name > @n OR (Name = @n AND Tags > @t))");
                select.Parameters.AddWithValue("n", cursor.Name);
                select.Parameters.AddWithValue("t", cursor.TieBreaker);
            }

            AppendReachPredicate(conditions, select, context);

            var sql = new StringBuilder("SELECT Name, Tags, Document FROM Environments");
            if (conditions.Count > 0)
            {
                sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
            }

            sql.Append(" ORDER BY Name, Tags LIMIT @limit;");
            select.Parameters.AddWithValue("limit", pageSize + 1);
            select.CommandText = sql.ToString();

            await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastName = string.Empty, lastTags = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                    break;
                }

                lastName = reader.GetString(0);
                lastTags = reader.GetString(1);
                docs.Add(PersistedJson.ToPooledDocument<Environment>(reader.GetFieldValue<byte[]>(2)));
            }

            return hasMore
                ? EnvironmentPage.Create(docs, lastName, lastTags)
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
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using NpgsqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Environments SET Etag = @etag, Document = @doc WHERE Name = @n AND Tags = @t;";
        update.Parameters.AddWithValue("etag", EnvironmentSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("doc", json);
        update.Parameters.AddWithValue("n", name);
        update.Parameters.AddWithValue("t", tags!);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand delete = connection.CreateCommand();
        delete.Transaction = transaction;
        delete.CommandText =
            "DELETE FROM Environments WHERE Name = @n AND Tags = @t; " +
            "DELETE FROM EnvironmentSecurityTags WHERE Name = @n AND Tags = @t;";
        delete.Parameters.AddWithValue("n", name);
        delete.Parameters.AddWithValue("t", tags!);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<(int Count, bool Capped)> CountAsync(AccessContext context, int cap, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        await using NpgsqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand select = connection.CreateCommand();
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

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over EnvironmentSecurityTags mirroring the caller's
    // reach (the same rules context.Admits evaluates). A null reach (unrestricted) adds nothing. Shared by ListAsync and
    // CountAsync so the count can never drift from the list it annotates.
    private static void AppendReachPredicate(List<string> conditions, NpgsqlCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "EnvironmentSecurityTags",
            ["Name", "Tags"],
            "TagKey",
            "TagValue",
            "Environments",
            value =>
            {
                string p = "sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return "@" + p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors an environment's management tags into the queryable side table (one row per key/value) so the reach can be
    // pushed into the list/count query. Management tags are immutable after creation, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string name, string canonicalTags, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            await using NpgsqlCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO EnvironmentSecurityTags (Name, Tags, TagKey, TagValue) VALUES (@n, @t, @key, @value);";
            insert.Parameters.AddWithValue("n", name);
            insert.Parameters.AddWithValue("t", canonicalTags);
            insert.Parameters.AddWithValue("key", tag.Key);
            insert.Parameters.AddWithValue("value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async ValueTask ProvisionAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand schema = connection.CreateCommand();
        schema.CommandText = SchemaSql;
        await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row key). An environment outside reach is invisible (non-disclosing).
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(NpgsqlConnection connection, string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Environments WHERE Name = @n;";
        select.Parameters.AddWithValue("n", name);
        await using NpgsqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    private ValueTask<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Environments (
            Name TEXT NOT NULL,
            Tags TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BYTEA NOT NULL,
            PRIMARY KEY (Name, Tags)
        );
        CREATE INDEX IF NOT EXISTS ix_environments_name ON Environments (Name);
        CREATE TABLE IF NOT EXISTS EnvironmentSecurityTags (
            Name TEXT NOT NULL,
            Tags TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS ix_environmentsecuritytags_owner ON EnvironmentSecurityTags (Name, Tags);
        CREATE INDEX IF NOT EXISTS ix_environmentsecuritytags_kv ON EnvironmentSecurityTags (TagKey, TagValue);
        """;
}