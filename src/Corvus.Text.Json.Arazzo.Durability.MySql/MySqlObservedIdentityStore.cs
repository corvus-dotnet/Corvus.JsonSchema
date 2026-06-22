// <copyright file="MySqlObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MySqlConnector;

namespace Corvus.Text.Json.Arazzo.Durability.MySql;

/// <summary>
/// A MySQL-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane has
/// observed, each persisted as its <see cref="ObservedIdentity"/> document keyed by (SubjectKind, SubjectValue). A
/// sighting upserts; the prefix typeahead is an indexed keyset page in <c>(SubjectValue, SubjectKind)</c> order with the
/// caller's read-reach (§17.1) <strong>pushed down to the index</strong>.
/// </summary>
/// <remarks>
/// <para>Reach-filtering follows the catalog store's optimised pattern (§14.4): each identity's <c>sys:</c> tags are
/// denormalised into a child <c>ObservedIdentitySecurityTags</c> table and the caller's <see cref="SecurityFilter"/> is
/// translated by the shared <see cref="SqlSecurityRuleEmitter"/> into a correlated <c>EXISTS</c>, so a scoped search is
/// index-filtered server-side (and the <c>LIMIT</c> is exact). An unrestricted (System) reach emits no predicate. The
/// key columns are declared <c>COLLATE utf8mb4_bin</c> so ordering, the prefix lower bound, and the keyset are
/// byte-ordinal (MySQL's default collation is case-insensitive); the prefix upper bound is an in-memory
/// <c>StartsWith</c>. Each operation opens a pooled connection; a sighting runs in one transaction.</para>
/// </remarks>
public sealed class MySqlObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    private readonly MySqlDataSource dataSource;
    private readonly bool ownsDataSource;
    private readonly TimeProvider timeProvider;

    private MySqlObservedIdentityStore(MySqlDataSource dataSource, bool ownsDataSource, TimeProvider timeProvider)
    {
        this.dataSource = dataSource;
        this.ownsDataSource = ownsDataSource;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A MySqlConnector connection string for a user permitted to create tables.</param>
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
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the data source it creates).</returns>
    public static ValueTask<MySqlObservedIdentityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlObservedIdentityStore>(
            new MySqlObservedIdentityStore(new MySqlDataSource(connectionString), ownsDataSource: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied data source (the caller retains ownership).</summary>
    /// <param name="dataSource">A MySqlConnector data source.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied data source).</returns>
    public static ValueTask<MySqlObservedIdentityStore> ConnectAsync(MySqlDataSource dataSource, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MySqlObservedIdentityStore>(
            new MySqlObservedIdentityStore(dataSource, ownsDataSource: false, timeProvider ?? TimeProvider.System));
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

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        byte[]? existing = await ReadDocumentAsync(connection, transaction, kindToken, valueKey, cancellationToken).ConfigureAwait(false);

        await using (MySqlCommand write = connection.CreateCommand())
        {
            // Serialize the document into a pooled buffer and bind it via ReadOnlyMemory (MySqlConnector carries the exact
            // length for a blob) — no GC document array. The buffer is returned once the command completes.
            using PooledUtf8 doc = existing is null
                ? ObservedIdentitySerialization.SerializeNewPooled(kind, value, label, identity, complete, now, provenance)
                : ObservedIdentitySerialization.SerializeUpsertedPooled(existing, kind, value, label, identity, complete, now, provenance);

            write.Transaction = transaction;
            write.CommandText = existing is null
                ? "INSERT INTO ObservedIdentities (SubjectKind, SubjectValue, Document, IdentityDigest) VALUES (@k, @v, @doc, @digest);"
                : "UPDATE ObservedIdentities SET Document = @doc, IdentityDigest = @digest WHERE SubjectKind = @k AND SubjectValue = @v;";
            write.Parameters.AddWithValue("@k", kindToken);
            write.Parameters.AddWithValue("@v", valueKey);
            write.Parameters.AddWithValue("@doc", doc.Memory);
            write.Parameters.AddWithValue("@digest", (object?)SecurityIdentityDigest.Compute(identity) ?? DBNull.Value);
            await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Rewrite the denormalised reach-index tags for this (kind, value): the identity can change on a re-sighting.
        await using (MySqlCommand clear = connection.CreateCommand())
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
                await using MySqlCommand tagInsert = connection.CreateCommand();
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

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The SubjectValue column is TEXT, so the prefix reifies once for the @p lower bound + the StartsWith break
        // (undefined prefix matches all).
        string prefixStr = prefix.IsNotUndefined() ? (string)prefix : string.Empty;

        // The opaque page token arrives as its JSON value; decode the (subjectValue, subjectKind) cursor straight from the
        // request UTF-8 (no managed token string). Init to empties so the unused-when-!hasCursor tuple is never null.
        (string SubjectValue, string SubjectKind) cursor = (string.Empty, string.Empty);
        bool hasCursor = false;
        if (pageToken.IsNotUndefined())
        {
            using UnescapedUtf8JsonString tokenUtf8 = pageToken.GetUtf8String();
            hasCursor = ObservedIdentityContinuationToken.TryDecode(tokenUtf8.Span, out cursor);
        }

        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextValue = null, nextKind = null;
        try
        {
            await using MySqlCommand select = connection.CreateCommand();

            // Reach (§17.1) pushed down as a correlated EXISTS over the child tag table; System reach emits none.
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

            await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            string lastValue = string.Empty, lastKind = string.Empty;
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                string rowKind = reader.GetString(0);
                string rowValue = reader.GetString(1);
                if (prefixStr.Length > 0 && !rowValue.StartsWith(prefixStr, StringComparison.Ordinal))
                {
                    break;
                }

                if (docs.Count == pageSize)
                {
                    nextValue = lastValue;
                    nextKind = lastKind;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(reader.GetFieldValue<byte[]>(2)));
                lastValue = rowValue;
                lastKind = rowKind;
            }

            return nextValue is not null ? ObservedIdentityPage.Create(docs, nextValue, nextKind!) : ObservedIdentityPage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
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
        await using MySqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT SubjectKind, SubjectValue, Document FROM ObservedIdentities " +
            "WHERE IdentityDigest = @d AND NOT (SubjectKind = @k AND SubjectValue = @v) LIMIT 1;";
        select.Parameters.AddWithValue("@d", digest);
        select.Parameters.AddWithValue("@k", kindToken);
        select.Parameters.AddWithValue("@v", valueKey);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);

        // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label live
        // in the record, so nothing is reified into a separate POCO here.
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? PersistedJson.ToPooledDocument<ObservedIdentity>(reader.GetFieldValue<byte[]>(2))
            : (ParsedJsonDocument<ObservedIdentity>?)null;
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

    private static async ValueTask<byte[]?> ReadDocumentAsync(MySqlConnection connection, MySqlTransaction transaction, string kindToken, string value, CancellationToken cancellationToken)
    {
        await using MySqlCommand select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT Document FROM ObservedIdentities WHERE SubjectKind = @k AND SubjectValue = @v;";
        select.Parameters.AddWithValue("@k", kindToken);
        select.Parameters.AddWithValue("@v", value);
        await using MySqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
    }

    private ValueTask<MySqlConnection> OpenAsync(CancellationToken cancellationToken)
        => this.dataSource.OpenConnectionAsync(cancellationToken);

    private const string SchemaSql =
        """
        CREATE TABLE IF NOT EXISTS ObservedIdentities (
            SubjectKind VARCHAR(64) COLLATE utf8mb4_bin NOT NULL,
            SubjectValue VARCHAR(512) COLLATE utf8mb4_bin NOT NULL,
            Document LONGBLOB NOT NULL,
            IdentityDigest VARCHAR(64) NULL,
            PRIMARY KEY (SubjectKind, SubjectValue),
            INDEX IX_ObservedIdentities_Value (SubjectValue, SubjectKind),
            INDEX IX_ObservedIdentities_Digest (IdentityDigest)
        );
        CREATE TABLE IF NOT EXISTS ObservedIdentitySecurityTags (
            SubjectKind VARCHAR(64) COLLATE utf8mb4_bin NOT NULL,
            SubjectValue VARCHAR(512) COLLATE utf8mb4_bin NOT NULL,
            TagKey VARCHAR(255) NOT NULL,
            TagValue VARCHAR(255) NOT NULL,
            INDEX IX_ObservedIdentitySecurityTags_Owner (SubjectKind, SubjectValue),
            INDEX IX_ObservedIdentitySecurityTags_KeyValue (TagKey, TagValue)
        );
        """;
}