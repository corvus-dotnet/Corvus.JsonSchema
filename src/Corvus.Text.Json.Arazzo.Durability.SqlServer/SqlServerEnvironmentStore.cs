// <copyright file="SqlServerEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted relationally.
/// Each environment is stored as its <see cref="Environment"/> document in a <c>varbinary(max)</c> column, keyed by
/// (Name, and a discriminator over its immutable management tags) so reach-isolated environments that share a name
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
public sealed class SqlServerEnvironmentStore : IEnvironmentStore, IAsyncDisposable
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerEnvironmentStore(string connectionString, TimeProvider timeProvider)
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
    public static ValueTask<SqlServerEnvironmentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerEnvironmentStore>(new SqlServerEnvironmentStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string tags = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand insert = connection.CreateCommand();
        insert.CommandText = "INSERT INTO Environments (Name, TagsHash, Tags, Etag, Document) VALUES (@n, HASHBYTES('SHA2_256', @t), @t, @etag, @doc);";
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
            throw new InvalidOperationException($"An environment named '{draft.NameValue}' with those security tags already exists.");
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
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

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<Environment>(pageSize);
        bool hasMore = false;
        try
        {
            // Keyset seek past the cursor in (Name, TagsHash) order — an indexed PK range scan. Tags is nvarchar(max) and
            // cannot be ordered, so the orderable PK component TagsHash (binary(32)) is the tie-breaker; the token carries
            // it as a hex string. Reach is a per-row predicate applied in memory until the page fills.
            await using SqlCommand select = connection.CreateCommand();
            if (hasCursor)
            {
                select.CommandText =
                    "SELECT Name, TagsHash, Document FROM Environments " +
                    "WHERE Name > @n OR (Name = @n AND TagsHash > @t) " +
                    "ORDER BY Name, TagsHash;";
                select.Parameters.AddWithValue("@n", cursor.Name);
                select.Parameters.AddWithValue("@t", Convert.FromHexString(cursor.TieBreaker));
            }
            else
            {
                select.CommandText = "SELECT Name, TagsHash, Document FROM Environments ORDER BY Name, TagsHash;";
            }

            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastName = string.Empty, lastTie = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string name = reader.GetString(0);
                string tie = Convert.ToHexString(reader.GetFieldValue<byte[]>(1));
                byte[] json = reader.GetFieldValue<byte[]>(2);
                ParsedJsonDocument<Environment> cand = PersistedJson.ToPooledDocument<Environment>(json);
                bool kept = false;
                try
                {
                    SecurityTagSet tags = cand.RootElement.ManagementTags.IsNotUndefined()
                        ? SecurityTagSet.FromOwnedJsonArray(JsonMarshal.GetRawUtf8Value(cand.RootElement.ManagementTags).Memory)
                        : SecurityTagSet.Empty;
                    if (!context.Admits(AccessVerb.Read, tags))
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
                    lastTie = tie;
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
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        (byte[]? existing, string? tags) = await FindForManagementAsync(connection, name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        await using SqlCommand update = connection.CreateCommand();
        update.CommandText = "UPDATE Environments SET Etag = @etag, Document = @doc WHERE Name = @n AND TagsHash = HASHBYTES('SHA2_256', @t);";
        update.Parameters.AddWithValue("@etag", EnvironmentSerialization.EtagOf(json).Value!);
        update.Parameters.AddWithValue("@doc", json);
        update.Parameters.AddWithValue("@n", name);
        update.Parameters.AddWithValue("@t", tags!);
        await update.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<Environment>(json);
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
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        await using SqlCommand delete = connection.CreateCommand();
        delete.CommandText = "DELETE FROM Environments WHERE Name = @n AND TagsHash = HASHBYTES('SHA2_256', @t);";
        delete.Parameters.AddWithValue("@n", name);
        delete.Parameters.AddWithValue("@t", tags!);
        await delete.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync() => default;

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the raw Tags value, re-hashed in-SQL for row addressing). An environment outside reach is invisible.
    private static async ValueTask<(byte[]? Json, string? Tags)> FindForManagementAsync(SqlConnection connection, string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText = "SELECT Tags, Document FROM Environments WHERE Name = @n;";
        select.Parameters.AddWithValue("@n", name);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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
        IF OBJECT_ID(N'Environments', N'U') IS NULL
        BEGIN
            CREATE TABLE Environments (
                Name NVARCHAR(450) NOT NULL,
                TagsHash BINARY(32) NOT NULL,
                Tags NVARCHAR(MAX) NOT NULL,
                Etag NVARCHAR(255) NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                CONSTRAINT PK_Environments PRIMARY KEY (Name, TagsHash)
            );
            CREATE INDEX IX_Environments_Name ON Environments (Name);
        END;
        """;
}