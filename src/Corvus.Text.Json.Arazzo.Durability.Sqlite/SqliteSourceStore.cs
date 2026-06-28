// <copyright file="SqliteSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted for a single-file /
/// embedded host. Each source is stored as its <see cref="RegisteredSource"/> document in a BLOB column, keyed by (Name,
/// and a discriminator over its immutable management tags) so reach-isolated sources that share a name coexist; its
/// etag is held in a column for the optimistic-concurrency check.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. One connection is held open
/// and all operations are serialised through it, as the other Sqlite stores do.
/// </remarks>
public sealed class SqliteSourceStore : ISourceStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteSourceStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens a source store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=sources.db</c>).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteSourceStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteSourceStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            WorkflowEtag etag = NewEtag();
            byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
            using SqliteCommand insert = this.connection.CreateCommand();
            insert.CommandText = "INSERT INTO Sources (Name, Tags, Etag, Document) VALUES (@n, @t, @etag, @doc);";
            insert.Parameters.AddWithValue("@n", draft.NameValue);
            insert.Parameters.AddWithValue("@t", SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue));
            insert.Parameters.AddWithValue("@etag", etag.Value!);
            insert.Parameters.AddWithValue("@doc", json);
            try
            {
                await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                throw new InvalidOperationException($"A source named '{draft.NameValue}' with those security tags already exists.");
            }

            return PersistedJson.ToPooledDocument<RegisteredSource>(json);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
            return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
        }
        finally
        {
            this.gate.Release();
        }
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

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<RegisteredSource>(pageSize);
            bool hasMore = false;
            try
            {
                // Keyset seek past the cursor in (Name, Tags) order — an indexed range scan, not a table load. Reach is a
                // per-row predicate applied in memory as we stream; the reader is consumed only until the page fills.
                using SqliteCommand select = this.connection.CreateCommand();
                if (hasCursor)
                {
                    select.CommandText =
                        "SELECT Name, Tags, Document FROM Sources " +
                        "WHERE Name > @n OR (Name = @n AND Tags > @t) " +
                        "ORDER BY Name, Tags;";
                    select.Parameters.AddWithValue("@n", cursor.Name);
                    select.Parameters.AddWithValue("@t", cursor.TieBreaker);
                }
                else
                {
                    select.CommandText = "SELECT Name, Tags, Document FROM Sources ORDER BY Name, Tags;";
                }

                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                string lastName = string.Empty, lastTags = string.Empty;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string name = reader.GetString(0);
                    string tags = reader.GetString(1);
                    byte[] json = reader.GetFieldValue<byte[]>(2);
                    ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(json);
                    bool kept = false;
                    try
                    {
                        SecurityTagSet tagSet = cand.RootElement.ManagementTags.IsNotUndefined()
                            ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                            : SecurityTagSet.Empty;
                        if (!context.Admits(AccessVerb.Read, tagSet))
                        {
                            continue;
                        }

                        if (docs.Count == pageSize)
                        {
                            hasMore = true;
                            break;
                        }

                        docs.Add(cand);
                        kept = true;
                        lastName = name;
                        lastTags = tags;
                    }
                    finally
                    {
                        if (!kept)
                        {
                            cand.Dispose();
                        }
                    }
                }

                return hasMore
                    ? SourcePage.Create(docs, lastName, lastTags)
                    : SourcePage.Create(docs);
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
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
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

            byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
            using SqliteCommand update = this.connection.CreateCommand();
            update.CommandText = "UPDATE Sources SET Etag = @etag, Document = @doc WHERE Name = @n AND Tags = @t;";
            update.Parameters.AddWithValue("@etag", SourceSerialization.EtagOf(json).Value!);
            update.Parameters.AddWithValue("@doc", json);
            update.Parameters.AddWithValue("@n", name);
            update.Parameters.AddWithValue("@t", tags!);
            await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return PersistedJson.ToPooledDocument<RegisteredSource>(json);
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
                SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
            }

            using SqliteCommand delete = this.connection.CreateCommand();
            delete.CommandText = "DELETE FROM Sources WHERE Name = @n AND Tags = @t;";
            delete.Parameters.AddWithValue("@n", name);
            delete.Parameters.AddWithValue("@t", tags!);
            await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n"));

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row key). A source outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Sources WHERE Name = @n;";
        select.Parameters.AddWithValue("@n", name);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS Sources (
            Name TEXT NOT NULL,
            Tags TEXT NOT NULL,
            Etag TEXT NOT NULL,
            Document BLOB NOT NULL,
            PRIMARY KEY (Name, Tags)
        );
        CREATE INDEX IF NOT EXISTS IX_Sources_Name ON Sources (Name);
        """;
}