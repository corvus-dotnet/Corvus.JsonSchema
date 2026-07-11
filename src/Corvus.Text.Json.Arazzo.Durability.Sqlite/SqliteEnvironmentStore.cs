// <copyright file="SqliteEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted for a single-file /
/// embedded host. Each environment is stored as its <see cref="Environment"/> document in a BLOB column, keyed by (Name,
/// and a discriminator over its immutable management tags) so reach-isolated environments that share a name coexist; its
/// etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. One connection is held open
/// and all operations are serialised through it, as the other Sqlite stores do.
/// </remarks>
public sealed class SqliteEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteEnvironmentStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an environment store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=environments.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteEnvironmentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteEnvironmentStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowEtag etag = NewEtag();
            byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
            string canonicalTags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO Environments (Name, Tags, Etag, Document) VALUES (@n, @t, @etag, @doc);";
            insert.Parameters.AddWithValue("@n", draft.NameValue);
            insert.Parameters.AddWithValue("@t", canonicalTags);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
            }

            // Mirror the management tags into the queryable side table so the §14.2 read reach can be pushed down into the
            // list/count query (a correlated EXISTS) rather than evaluated per row in memory. Atomic with the row insert;
            // the tags are immutable, so no re-sync is needed on update.
            await SyncSecurityTagsAsync(this.connection, transaction, draft.NameValue, canonicalTags, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
            transaction.Commit();

            return PersistedJson.ToPooledDocument<Environment>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<Environment>(json);
        }
        finally
        {
            this.gate.Release();
        }
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<Environment>(pageSize);
            bool hasMore = false;
            try
            {
                // Keyset seek past the cursor in (Name, Tags) order — an indexed range scan — with the §14.2 read reach
                // pushed into the query as a correlated EXISTS over EnvironmentSecurityTags (the same predicate
                // context.Admits evaluates, but applied in the database), so out-of-reach rows never leave it. The
                // ORDER BY drives the page and LIMIT bounds the read to one page + 1 (lookahead).
                using SqliteCommand select = this.connection.CreateCommand();
                var conditions = new List<string>(2);
                if (hasCursor)
                {
                    conditions.Add("(Name > @n OR (Name = @n AND Tags > @t))");
                    select.Parameters.AddWithValue("@n", cursor.Name);
                    select.Parameters.AddWithValue("@t", cursor.TieBreaker);
                }

                AppendReachPredicate(conditions, select, context);

                var sql = new StringBuilder("SELECT Name, Tags, Document FROM Environments");
                if (conditions.Count > 0)
                {
                    sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
                }

                sql.Append(" ORDER BY Name, Tags LIMIT @limit;");
                select.Parameters.AddWithValue("@limit", pageSize + 1);
                select.CommandText = sql.ToString();

                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> UpdateAsync(string name, Environment draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return null;
            }

            byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE Environments SET Etag = @etag, Document = @doc WHERE Name = @n AND Tags = @t;";
            update.Parameters.AddWithValue("@etag", EnvironmentSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@n", name);
            update.Parameters.AddWithValue("@t", tags!);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<Environment>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return false;
            }

            if (!expectedEtag.IsNone)
            {
                EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
            }

            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText =
                "DELETE FROM Environments WHERE Name = @n AND Tags = @t; " +
                "DELETE FROM EnvironmentSecurityTags WHERE Name = @n AND Tags = @t;";
            delete.Parameters.AddWithValue("@n", name);
            delete.Parameters.AddWithValue("@t", tags!);
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
            var inner = new StringBuilder("SELECT 1 FROM Environments");
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
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    // Appends the §14.2 read-reach predicate: a correlated EXISTS over EnvironmentSecurityTags mirroring the caller's
    // reach (the same rules context.Admits evaluates). A null reach (unrestricted) adds nothing. Shared by ListAsync and
    // CountAsync so the count can never drift from the list it annotates.
    private static void AppendReachPredicate(List<string> conditions, SqliteCommand command, AccessContext context)
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
                string p = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue(p, value);
                return p;
            });
        conditions.Add("(" + reach.ToSqlPredicate(emitter) + ")");
    }

    // Mirrors an environment's management tags into the queryable side table (one row per key/value) so the reach can be
    // pushed into the list/count query. Management tags are immutable after creation, so this runs only on add.
    private static async Task SyncSecurityTagsAsync(SqliteConnection connection, SqliteTransaction transaction, string name, string canonicalTags, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO EnvironmentSecurityTags (Name, Tags, TagKey, TagValue) VALUES (@n, @t, @key, @value);";
            insert.Parameters.AddWithValue("@n", name);
            insert.Parameters.AddWithValue("@t", canonicalTags);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row key). An environment outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Environments WHERE Name = @n;";
        select.Parameters.AddWithValue("@n", name);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Environments (
            Name TEXT NOT NULL,
            Tags TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (Name, Tags)
        );
        CREATE INDEX IF NOT EXISTS IX_Environments_Name ON Environments (Name);
        CREATE TABLE IF NOT EXISTS EnvironmentSecurityTags (
            Name TEXT NOT NULL,
            Tags TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_EnvironmentSecurityTags_owner ON EnvironmentSecurityTags (Name, Tags);
        CREATE INDEX IF NOT EXISTS IX_EnvironmentSecurityTags_kv ON EnvironmentSecurityTags (TagKey, TagValue);
        """;
}