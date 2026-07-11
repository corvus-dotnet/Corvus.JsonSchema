// <copyright file="SqliteSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings — references and
/// non-sensitive metadata only, never secret material — persisted for a single-file / embedded host. Each binding is
/// stored as its <see cref="SourceCredentialBinding"/> document in a BLOB column, keyed by (SourceName, Environment, and
/// a discriminator over its immutable management/usage tags) so tenant-/workflow-scoped bindings for the same
/// source/environment coexist; its etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage path by
/// label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a deployment
/// keeps those reach-disjoint. One connection is held open and all operations are serialised through it, as the other
/// Sqlite stores do.
/// </remarks>
public sealed class SqliteSourceCredentialStore : ISourceCredentialStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);
    private long idSequence;

    private SqliteSourceCredentialStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens a source credential store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=credentials.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteSourceCredentialStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteSourceCredentialStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string id = $"scred-{++this.idSequence}";
            WorkflowEtag etag = NewEtag();
            byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
            string discriminator = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);
            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SourceCredentials (SourceName, Environment, Tags, Etag, Document) VALUES (@s, @e, @t, @etag, @doc);";
            insert.Parameters.AddWithValue("@s", draft.SourceNameValue);
            insert.Parameters.AddWithValue("@e", draft.EnvironmentValue);
            insert.Parameters.AddWithValue("@t", discriminator);
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"A source credential binding for '{draft.SourceNameValue}@{draft.EnvironmentValue}' with those security tags already exists.");
            }

            // Mirror the MANAGEMENT tags (the reach discriminator, distinct from the row's combined mgmt+usage Tags key)
            // into the queryable side table so the §14.2 read reach can be pushed into the list/count query as a
            // correlated EXISTS. Atomic; tags are immutable so no re-sync on update.
            await SyncSecurityTagsAsync(this.connection, transaction, draft.SourceNameValue, draft.EnvironmentValue, discriminator, draft.ManagementTagsValue, cancellationToken).ConfigureAwait(false);
            transaction.Commit();

            return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
            bool hasMore = false;
            try
            {
                // Keyset seek past the cursor in (SourceName, Environment, Tags) order — an indexed range scan, not a
                // table load — with the §14.2 read reach pushed into the query as a correlated EXISTS over
                // SourceCredentialSecurityTags (the same predicate context.Admits evaluates, but applied in the database),
                // so out-of-reach rows never leave it. LIMIT bounds the read.
                using SqliteCommand select = this.connection.CreateCommand();
                var conditions = new List<string>(2);
                if (hasCursor)
                {
                    conditions.Add("(SourceName > @s OR (SourceName = @s AND Environment > @e) OR (SourceName = @s AND Environment = @e AND Tags > @t))");
                    select.Parameters.AddWithValue("@s", cursor.SourceName);
                    select.Parameters.AddWithValue("@e", cursor.Environment);
                    select.Parameters.AddWithValue("@t", cursor.TieBreaker);
                }

                AppendReachPredicate(conditions, select, context);

                var sql = new StringBuilder("SELECT SourceName, Environment, Tags, Document FROM SourceCredentials");
                if (conditions.Count > 0)
                {
                    sql.Append(" WHERE ").Append(string.Join(" AND ", conditions));
                }

                sql.Append(" ORDER BY SourceName, Environment, Tags LIMIT @limit;");
                select.Parameters.AddWithValue("@limit", pageSize + 1);
                select.CommandText = sql.ToString();

                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                string lastSource = string.Empty, lastEnv = string.Empty, lastTags = string.Empty;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    if (docs.Count == pageSize)
                    {
                        hasMore = true; // the (pageSize+1)th admitted row exists → there is a next page
                        break;
                    }

                    lastSource = reader.GetString(0);
                    lastEnv = reader.GetString(1);
                    lastTags = reader.GetString(2);
                    docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(reader.GetFieldValue<byte[]>(3)));
                }

                return hasMore
                    ? SourceCredentialPage.Create(docs, lastSource, lastEnv, lastTags)
                    : SourceCredentialPage.Create(docs);
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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> UpdateAsync(string sourceName, string environment, SourceCredentialBinding draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return null;
            }

            byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE SourceCredentials SET Etag = @etag, Document = @doc WHERE SourceName = @s AND Environment = @e AND Tags = @t;";
            update.Parameters.AddWithValue("@etag", SourceCredentialSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@s", sourceName);
            update.Parameters.AddWithValue("@e", environment);
            update.Parameters.AddWithValue("@t", tags!);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? existing, string? tags) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
            if (existing is null)
            {
                return false;
            }

            if (!expectedEtag.IsNone)
            {
                SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
            }

            using SqliteTransaction transaction = this.connection.BeginTransaction();
            using SqliteCommand delete = this.connection.CreateCommand();
            delete.Transaction = transaction;
            delete.CommandText =
                "DELETE FROM SourceCredentials WHERE SourceName = @s AND Environment = @e AND Tags = @t; " +
                "DELETE FROM SourceCredentialSecurityTags WHERE SourceName = @s AND Environment = @e AND Tags = @t;";
            delete.Parameters.AddWithValue("@s", sourceName);
            delete.Parameters.AddWithValue("@e", environment);
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
            var inner = new StringBuilder("SELECT 1 FROM SourceCredentials");
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
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
            select.Parameters.AddWithValue("@s", sourceName);
            select.Parameters.AddWithValue("@e", environment);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<CredentialSourceAccess> EvaluateSourceAccessAsync(string sourceName, SecurityTagSet tags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText = "SELECT Document FROM SourceCredentials WHERE SourceName = @s;";
            select.Parameters.AddWithValue("@s", sourceName);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its bytes
    // and its tag discriminator (the row key). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM SourceCredentials WHERE SourceName = @s AND Environment = @e;";
        select.Parameters.AddWithValue("@s", sourceName);
        select.Parameters.AddWithValue("@e", environment);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
    // caller's reach (the same rules context.Admits evaluates), keyed on the (SourceName, Environment, Tags) row
    // discriminator. A null reach (unrestricted) adds nothing. Shared by ListAsync and CountAsync so the count can never
    // drift from the list it annotates.
    private static void AppendReachPredicate(List<string> conditions, SqliteCommand command, AccessContext context)
    {
        if (context.Reach(AccessVerb.Read) is not { } reach)
        {
            return;
        }

        int securityParam = 0;
        var emitter = new SqlSecurityRuleEmitter(
            "SourceCredentialSecurityTags",
            ["SourceName", "Environment", "Tags"],
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

    // Mirrors a binding's MANAGEMENT tags (one row per key/value) into the queryable side table, keyed by the binding's
    // (SourceName, Environment, Tags) row key, so the reach can be pushed into the list/count query. Reach filters on the
    // management tags (distinct from the row's combined mgmt+usage Tags discriminator). Tags are immutable → add-only.
    private static async Task SyncSecurityTagsAsync(SqliteConnection connection, SqliteTransaction transaction, string sourceName, string environment, string discriminator, SecurityTagSet managementTags, CancellationToken cancellationToken)
    {
        if (managementTags.IsEmpty)
        {
            return;
        }

        foreach (SecurityTag tag in managementTags.ToList())
        {
            using SqliteCommand insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = "INSERT INTO SourceCredentialSecurityTags (SourceName, Environment, Tags, TagKey, TagValue) VALUES (@s, @e, @t, @key, @value);";
            insert.Parameters.AddWithValue("@s", sourceName);
            insert.Parameters.AddWithValue("@e", environment);
            insert.Parameters.AddWithValue("@t", discriminator);
            insert.Parameters.AddWithValue("@key", tag.Key);
            insert.Parameters.AddWithValue("@value", tag.Value);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS SourceCredentials (
            SourceName TEXT NOT NULL,
            Environment TEXT NOT NULL,
            Tags TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (SourceName, Environment, Tags)
        );
        CREATE INDEX IF NOT EXISTS IX_SourceCredentials_Source ON SourceCredentials (SourceName);
        CREATE TABLE IF NOT EXISTS SourceCredentialSecurityTags (
            SourceName TEXT NOT NULL,
            Environment TEXT NOT NULL,
            Tags TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_SourceCredentialSecurityTags_owner ON SourceCredentialSecurityTags (SourceName, Environment, Tags);
        CREATE INDEX IF NOT EXISTS IX_SourceCredentialSecurityTags_kv ON SourceCredentialSecurityTags (TagKey, TagValue);
        """;
}