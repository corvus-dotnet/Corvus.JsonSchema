// <copyright file="SqliteObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.Sqlite;

namespace Corvus.Text.Json.Arazzo.Durability.Sqlite;

/// <summary>
/// A SQLite-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane has
/// observed, each persisted as its <see cref="ObservedIdentity"/> document keyed by (SubjectKind, SubjectValue), for a
/// single-file / embedded host. A sighting upserts; the prefix typeahead is an indexed keyset page in
/// <c>(SubjectValue, SubjectKind)</c> order with the caller's read-reach (§17.1) <strong>pushed down to the index</strong>.
/// </summary>
/// <remarks>
/// <para>Reach-filtering follows the catalog store's optimised pattern (§14.4): each identity's <c>sys:</c> tags are
/// denormalised into a child <c>ObservedIdentitySecurityTags</c> table, and the caller's <see cref="SecurityFilter"/> is
/// translated by the shared <see cref="SqlSecurityRuleEmitter"/> into a correlated <c>EXISTS</c> — so a scoped search is
/// index-filtered server-side (and the <c>LIMIT</c> is pushed too), never a full scan with per-row materialisation. An
/// unrestricted (System) reach emits no predicate. The prefix is a binary-collation lower bound (SQLite's default BINARY
/// collation is the ordinal order the contract pages by) plus an in-memory <c>StartsWith</c> upper bound, so the scan
/// stays within the contiguous prefix range.</para>
/// <para>One connection is held open and all operations are serialised through it, as the other Sqlite stores do; a
/// sighting's document + tag-row rewrite runs in one transaction so the index never diverges from the document.</para>
/// </remarks>
public sealed class SqliteObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly TimeProvider timeProvider;
    private readonly SemaphoreSlim gate = new(1, 1);

    private SqliteObservedIdentityStore(SqliteConnection connection, TimeProvider timeProvider)
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

    /// <summary>Opens an observed-identity store over the given connection string, ensuring its schema exists.</summary>
    /// <param name="connectionString">A Microsoft.Data.Sqlite connection string (e.g. <c>Data Source=identities.db</c>).</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened, schema-initialised store.</returns>
    public static async ValueTask<SqliteObservedIdentityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
            using SqliteCommand schema = connection.CreateCommand();
            schema.CommandText = SchemaSql;
            await schema.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            return new SqliteObservedIdentityStore(connection, timeProvider ?? TimeProvider.System);
        }
        catch
        {
            await connection.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();

        // The SubjectValue column is TEXT (the storage-key leaf), so the value reifies once here for the SQL params; the
        // document body is serialized bytes-to-bytes from the value/label JSON values at the synchronous serialize calls
        // below (the value is valid for the whole call, so reifying the key synchronously before any await is fine).
        string valueKey = (string)value;
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            // Read-merge-write under the gate: a first sighting inserts; an existing one preserves firstSeen, bumps
            // lastSeen, unions provenance, and refreshes label/identity/completeness (the shared merge).
            byte[]? existing = await this.ReadDocumentAsync(kindToken, valueKey, cancellationToken).ConfigureAwait(false);
            byte[] json = existing is null
                ? ObservedIdentitySerialization.SerializeNew(kind, value, label, identity, complete, now, provenance)
                : ObservedIdentitySerialization.SerializeUpserted(existing, kind, value, label, identity, complete, now, provenance);

            await using SqliteTransaction transaction = (SqliteTransaction)await this.connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

            using (SqliteCommand upsert = this.connection.CreateCommand())
            {
                upsert.Transaction = transaction;
                upsert.CommandText = "INSERT OR REPLACE INTO ObservedIdentities (SubjectKind, SubjectValue, Document, IdentityDigest) VALUES (@k, @v, @doc, @digest);";
                upsert.Parameters.AddWithValue("@k", kindToken);
                upsert.Parameters.AddWithValue("@v", valueKey);
                upsert.Parameters.AddWithValue("@doc", json);
                upsert.Parameters.AddWithValue("@digest", (object?)SecurityIdentityDigest.Compute(identity) ?? DBNull.Value);
                await upsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            // Rewrite the denormalised reach-index tags for this (kind, value): the identity can change on a re-sighting.
            using (SqliteCommand clear = this.connection.CreateCommand())
            {
                clear.Transaction = transaction;
                clear.CommandText = "DELETE FROM ObservedIdentitySecurityTags WHERE SubjectKind = @k AND SubjectValue = @v;";
                clear.Parameters.AddWithValue("@k", kindToken);
                clear.Parameters.AddWithValue("@v", valueKey);
                await clear.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            if (!identity.IsEmpty)
            {
                // Materialise at this write leaf: the ref-struct enumerator cannot cross the per-row await below.
                foreach (SecurityTag tag in identity.ToList())
                {
                    using SqliteCommand tagInsert = this.connection.CreateCommand();
                    tagInsert.Transaction = transaction;
                    tagInsert.CommandText = "INSERT INTO ObservedIdentitySecurityTags (SubjectKind, SubjectValue, TagKey, TagValue) VALUES (@k, @v, @key, @value);";
                    tagInsert.Parameters.AddWithValue("@k", kindToken);
                    tagInsert.Parameters.AddWithValue("@v", valueKey);
                    tagInsert.Parameters.AddWithValue("@key", tag.Key);
                    tagInsert.Parameters.AddWithValue("@value", tag.Value);
                    await tagInsert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The SubjectValue column is TEXT, so the prefix reifies once for the @p lower bound + the StartsWith break
        // (undefined prefix matches all).
        string prefixStr = prefix.IsNotUndefined() ? (string)prefix : string.Empty;
        bool hasCursor = ObservedIdentityContinuationToken.TryDecode(pageToken, out (string SubjectValue, string SubjectKind) cursor);
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
            string? nextToken = null;
            try
            {
                using SqliteCommand select = this.connection.CreateCommand();

                // The reach (§17.1) is pushed down as a correlated EXISTS over the child tag table (the catalog idiom),
                // so a scoped search is index-filtered server-side and the LIMIT below is exact. System reach emits none.
                string securityPredicate = string.Empty;
                if (readReach is not null)
                {
                    int securityParam = 0;
                    var emitter = new SqlSecurityRuleEmitter(
                        "ObservedIdentitySecurityTags",
                        ["SubjectKind", "SubjectValue"],
                        "TagKey",
                        "TagValue",
                        "ObservedIdentities",
                        v =>
                        {
                            string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                            select.Parameters.AddWithValue(name, v);
                            return name;
                        });
                    securityPredicate = " AND (" + readReach.ToSqlPredicate(emitter) + ")";
                }

                // Keyset seek (SubjectValue, SubjectKind) with the optional kind equality and prefix lower bound — an
                // indexed range scan (BINARY collation = ordinal). The prefix upper bound is the in-memory StartsWith
                // break below (the prefix range is contiguous in the order). LIMIT pageSize+1 detects a next page.
                select.CommandText =
                    "SELECT SubjectKind, SubjectValue, Document FROM ObservedIdentities WHERE 1 = 1" +
                    (kindToken is not null ? " AND SubjectKind = @k" : string.Empty) +
                    (prefixStr.Length > 0 ? " AND SubjectValue >= @p" : string.Empty) +
                    (hasCursor ? " AND (SubjectValue > @cv OR (SubjectValue = @cv AND SubjectKind > @ck))" : string.Empty) +
                    securityPredicate +
                    " ORDER BY SubjectValue, SubjectKind LIMIT @limit;";
                if (kindToken is not null)
                {
                    select.Parameters.AddWithValue("@k", kindToken);
                }

                if (prefixStr.Length > 0)
                {
                    select.Parameters.AddWithValue("@p", prefixStr);
                }

                if (hasCursor)
                {
                    select.Parameters.AddWithValue("@cv", cursor.SubjectValue);
                    select.Parameters.AddWithValue("@ck", cursor.SubjectKind);
                }

                select.Parameters.AddWithValue("@limit", pageSize + 1);

                using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                string lastValue = string.Empty, lastKind = string.Empty;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string rowKind = reader.GetString(0);
                    string rowValue = reader.GetString(1);

                    // Past the contiguous prefix range (rows are ordered by SubjectValue) → no further matches.
                    if (prefixStr.Length > 0 && !rowValue.StartsWith(prefixStr, StringComparison.Ordinal))
                    {
                        break;
                    }

                    if (docs.Count == pageSize)
                    {
                        nextToken = ObservedIdentityContinuationToken.Encode(lastValue, lastKind);
                        break;
                    }

                    docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(reader.GetFieldValue<byte[]>(2)));
                    lastValue = rowValue;
                    lastKind = rowKind;
                }

                return new ObservedIdentityPage(docs, nextToken);
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
    public async ValueTask<ParsedJsonDocument<ObservedIdentity>?> FindIdentityConflictAsync(ObservedIdentity.GranteeKind kind, JsonString value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise seek the indexed digest column for a row whose identity
        // is set-equal (same digest) but whose (kind, value) differs — a non-unique identity the authoring path refuses.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        string kindToken = kind.ToToken();
        string valueKey = (string)value;
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using SqliteCommand select = this.connection.CreateCommand();
            select.CommandText =
                "SELECT SubjectKind, SubjectValue, Document FROM ObservedIdentities " +
                "WHERE IdentityDigest = @d AND NOT (SubjectKind = @k AND SubjectValue = @v) LIMIT 1;";
            select.Parameters.AddWithValue("@d", digest);
            select.Parameters.AddWithValue("@k", kindToken);
            select.Parameters.AddWithValue("@v", valueKey);
            using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

            // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label
            // live in the record, so nothing is reified into a separate POCO here.
            return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
                ? PersistedJson.ToPooledDocument<ObservedIdentity>(reader.GetFieldValue<byte[]>(2))
                : (ParsedJsonDocument<ObservedIdentity>?)null;
        }
        finally
        {
            this.gate.Release();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync() => await this.connection.DisposeAsync().ConfigureAwait(false);

    private async ValueTask<byte[]?> ReadDocumentAsync(string kindToken, string value, CancellationToken cancellationToken)
    {
        using SqliteCommand select = this.connection.CreateCommand();
        select.CommandText = "SELECT Document FROM ObservedIdentities WHERE SubjectKind = @k AND SubjectValue = @v;";
        select.Parameters.AddWithValue("@k", kindToken);
        select.Parameters.AddWithValue("@v", value);
        using SqliteDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS ObservedIdentities (
            SubjectKind TEXT NOT NULL,
            SubjectValue TEXT NOT NULL,
            Document BLOB NOT NULL,
            IdentityDigest TEXT NULL,
            PRIMARY KEY (SubjectKind, SubjectValue)
        );
        CREATE INDEX IF NOT EXISTS IX_ObservedIdentities_Value ON ObservedIdentities (SubjectValue, SubjectKind);
        CREATE INDEX IF NOT EXISTS IX_ObservedIdentities_Digest ON ObservedIdentities (IdentityDigest);
        CREATE TABLE IF NOT EXISTS ObservedIdentitySecurityTags (
            SubjectKind TEXT NOT NULL,
            SubjectValue TEXT NOT NULL,
            TagKey TEXT NOT NULL,
            TagValue TEXT NOT NULL
        );
        CREATE INDEX IF NOT EXISTS IX_ObservedIdentitySecurityTags_Owner ON ObservedIdentitySecurityTags (SubjectKind, SubjectValue);
        CREATE INDEX IF NOT EXISTS IX_ObservedIdentitySecurityTags_KeyValue ON ObservedIdentitySecurityTags (TagKey, TagValue);
        """;
}