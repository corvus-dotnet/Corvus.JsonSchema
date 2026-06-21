// <copyright file="MySqlSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO SourceCredentials (SourceName, Environment, TagsHash, Tags, Etag, Document) VALUES (@s, @e, @h, @t, @etag, @doc);";
        insert.Parameters.AddWithValue("@s", draft.SourceNameValue);
        insert.Parameters.AddWithValue("@e", draft.EnvironmentValue);
        insert.Parameters.AddWithValue("@h", TagsHash(draft.SourceNameValue, draft.EnvironmentValue, tags));
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
            // primary key, not a table load. Tags is LONGTEXT and cannot be a key/ordered column, so the orderable PK
            // component TagsHash (CHAR(64), a SHA-256 hex digest) is the tie-breaker; it is already a hex string, so the
            // token carries it verbatim. Reach is a per-row predicate applied in memory as we stream; the reader is
            // consumed only until the page fills, so we read ≈ pageSize / selectivity rows, never the whole table.
            await using MySqlCommand select = connection.CreateCommand();
            if (hasCursor)
            {
                select.CommandText =
                    "SELECT SourceName, Environment, TagsHash, Document FROM SourceCredentials " +
                    "WHERE SourceName > @s OR (SourceName = @s AND Environment > @e) OR (SourceName = @s AND Environment = @e AND TagsHash > @t) " +
                    "ORDER BY SourceName, Environment, TagsHash;";
                select.Parameters.AddWithValue("@s", cursor.SourceName);
                select.Parameters.AddWithValue("@e", cursor.Environment);
                select.Parameters.AddWithValue("@t", cursor.TieBreaker);
            }
            else
            {
                select.CommandText = "SELECT SourceName, Environment, TagsHash, Document FROM SourceCredentials ORDER BY SourceName, Environment, TagsHash;";
            }

            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastSource = string.Empty, lastEnv = string.Empty, lastTie = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string source = reader.GetString(0);
                string environment = reader.GetString(1);
                string tie = reader.GetString(2);
                byte[] json = reader.GetFieldValue<byte[]>(3);
                using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
                if (!context.Admits(AccessVerb.Read, candidate.RootElement.ManagementTagsValue))
                {
                    continue;
                }

                if (docs.Count == pageSize)
                {
                    hasMore = true;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<SourceCredentialBinding>(json));
                lastSource = source;
                lastEnv = environment;
                lastTie = tie;
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

        await using MySqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM SourceCredentials WHERE SourceName = @s AND Environment = @e AND TagsHash = @h;";
        delete.Parameters.AddWithValue("@s", sourceName);
        delete.Parameters.AddWithValue("@e", environment);
        delete.Parameters.AddWithValue("@h", TagsHash(sourceName, environment, tags!));
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
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
        """;
}