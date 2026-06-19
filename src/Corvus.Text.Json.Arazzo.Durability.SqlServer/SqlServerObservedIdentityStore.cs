// <copyright file="SqlServerObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Data.SqlClient;

namespace Corvus.Text.Json.Arazzo.Durability.SqlServer;

/// <summary>
/// A SQL Server-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane
/// has observed, each persisted as its <see cref="ObservedIdentity"/> document keyed by (SubjectKind, SubjectValue). A
/// sighting upserts; the prefix typeahead is an indexed keyset page in <c>(SubjectValue, SubjectKind)</c> order with the
/// caller's read-reach (§17.1) <strong>pushed down to the index</strong>.
/// </summary>
/// <remarks>
/// <para>Reach-filtering follows the catalog store's optimised pattern (§14.4): each identity's <c>sys:</c> tags are
/// denormalised into a child <c>ObservedIdentitySecurityTags</c> table and the caller's <see cref="SecurityFilter"/> is
/// translated by the shared <see cref="SqlSecurityRuleEmitter"/> into a correlated <c>EXISTS</c>, so a scoped search is
/// index-filtered server-side (and the <c>TOP</c> is exact). An unrestricted (System) reach emits no predicate. The key
/// columns are declared <c>COLLATE Latin1_General_BIN2</c> so ordering, the prefix lower bound, and the keyset are
/// byte-ordinal (SQL Server's default collation is case-insensitive); the prefix upper bound is an in-memory
/// <c>StartsWith</c>. The key is a non-clustered primary key (the two text columns exceed the 900-byte clustered key
/// limit). Each operation opens a pooled connection; a sighting runs in one transaction.</para>
/// </remarks>
public sealed class SqlServerObservedIdentityStore : IObservedIdentityStore
{
    private readonly string connectionString;
    private readonly TimeProvider timeProvider;

    private SqlServerObservedIdentityStore(string connectionString, TimeProvider timeProvider)
    {
        this.connectionString = connectionString;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the schema (requires a DDL-capable credential); run once at deploy time.</summary>
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string for a login permitted to create tables.</param>
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
    /// <param name="connectionString">A Microsoft.Data.SqlClient connection string.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (each operation uses a pooled connection).</returns>
    public static ValueTask<SqlServerObservedIdentityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<SqlServerObservedIdentityStore>(
            new SqlServerObservedIdentityStore(connectionString, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(GranteeKind kind, ReadOnlyMemory<byte> value, ReadOnlyMemory<byte> label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();

        // The SubjectValue column is NVARCHAR (the storage-key leaf), so the value materializes once here for the SQL
        // params; the document body is serialized bytes-to-bytes from the value/label spans at the synchronous serialize
        // calls below (taken after the read-await, so no span crosses an await).
        string valueKey = Encoding.UTF8.GetString(value.Span);
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);

        byte[]? existing = await ReadDocumentAsync(connection, transaction, kindToken, valueKey, cancellationToken).ConfigureAwait(false);
        byte[] json = existing is null
            ? ObservedIdentitySerialization.SerializeNew(kindToken, value.Span, label.Span, identity, complete, now, provenance)
            : ObservedIdentitySerialization.SerializeUpserted(existing, kindToken, value.Span, label.Span, identity, complete, now, provenance);

        await using (SqlCommand write = connection.CreateCommand())
        {
            write.Transaction = transaction;
            write.CommandText = existing is null
                ? "INSERT INTO ObservedIdentities (SubjectKind, SubjectValue, Document, IdentityDigest) VALUES (@k, @v, @doc, @digest);"
                : "UPDATE ObservedIdentities SET Document = @doc, IdentityDigest = @digest WHERE SubjectKind = @k AND SubjectValue = @v;";
            write.Parameters.AddWithValue("@k", kindToken);
            write.Parameters.AddWithValue("@v", valueKey);
            write.Parameters.AddWithValue("@doc", json);
            write.Parameters.AddWithValue("@digest", (object?)SecurityIdentityDigest.Compute(identity) ?? DBNull.Value);
            await write.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        // Rewrite the denormalised reach-index tags for this (kind, value): the identity can change on a re-sighting.
        await using (SqlCommand clear = connection.CreateCommand())
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
                await using SqlCommand tagInsert = connection.CreateCommand();
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
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, GranteeKind? kind, ReadOnlyMemory<byte> prefix, int limit, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind?.ToToken();

        // The SubjectValue column is NVARCHAR, so the prefix materializes once for the @p lower bound + the StartsWith break.
        string prefixStr = Encoding.UTF8.GetString(prefix.Span);
        bool hasCursor = ObservedIdentityContinuationToken.TryDecode(pageToken, out (string SubjectValue, string SubjectKind) cursor);
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextToken = null;
        try
        {
            await using SqlCommand select = connection.CreateCommand();

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
                "SELECT TOP (@limit) SubjectKind, SubjectValue, Document FROM ObservedIdentities WHERE 1 = 1" +
                (kindToken is not null ? " AND SubjectKind = @k" : string.Empty) +
                (prefixStr.Length > 0 ? " AND SubjectValue >= @p" : string.Empty) +
                (hasCursor ? " AND (SubjectValue > @cv OR (SubjectValue = @cv AND SubjectKind > @ck))" : string.Empty) +
                securityPredicate +
                " ORDER BY SubjectValue, SubjectKind;";
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

            await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
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

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityConflict?> FindIdentityConflictAsync(GranteeKind kind, ReadOnlyMemory<byte> value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise seek the indexed digest column for a row whose identity
        // is set-equal (same digest) but whose (kind, value) differs — a non-unique identity the authoring path refuses.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        string kindToken = kind.ToToken();
        string valueKey = Encoding.UTF8.GetString(value.Span);
        await using SqlConnection connection = await this.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using SqlCommand select = connection.CreateCommand();
        select.CommandText =
            "SELECT TOP 1 SubjectKind, SubjectValue, Document FROM ObservedIdentities " +
            "WHERE IdentityDigest = @d AND NOT (SubjectKind = @k AND SubjectValue = @v);";
        select.Parameters.AddWithValue("@d", digest);
        select.Parameters.AddWithValue("@k", kindToken);
        select.Parameters.AddWithValue("@v", valueKey);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false)
            ? ToConflict(reader.GetString(0), reader.GetString(1), reader.GetFieldValue<byte[]>(2), kind)
            : null;
    }

    // Builds the conflict from a matched row: the kind from its token, the value verbatim, and the label parsed from the
    // document (a transient pooled parse — the label string it yields is an owned managed copy that outlives the dispose).
    private static ObservedIdentityConflict ToConflict(string conflictKindToken, string conflictValue, byte[] document, GranteeKind fallbackKind)
    {
        string? label;
        using (ParsedJsonDocument<ObservedIdentity> doc = PersistedJson.ToPooledDocument<ObservedIdentity>(document))
        {
            label = doc.RootElement.LabelOrNull;
        }

        GranteeKind conflictKind = GranteeKinds.TryParse(conflictKindToken, out GranteeKind parsed) ? parsed : fallbackKind;
        return new ObservedIdentityConflict(conflictKind, conflictValue, label);
    }

    private static async ValueTask<byte[]?> ReadDocumentAsync(SqlConnection connection, SqlTransaction transaction, string kindToken, string value, CancellationToken cancellationToken)
    {
        await using SqlCommand select = connection.CreateCommand();
        select.Transaction = transaction;
        select.CommandText = "SELECT Document FROM ObservedIdentities WHERE SubjectKind = @k AND SubjectValue = @v;";
        select.Parameters.AddWithValue("@k", kindToken);
        select.Parameters.AddWithValue("@v", value);
        await using SqlDataReader reader = await select.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        return await reader.ReadAsync(cancellationToken).ConfigureAwait(false) ? reader.GetFieldValue<byte[]>(0) : null;
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
        IF OBJECT_ID(N'ObservedIdentities', N'U') IS NULL
        BEGIN
            CREATE TABLE ObservedIdentities (
                SubjectKind NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
                SubjectValue NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                Document VARBINARY(MAX) NOT NULL,
                IdentityDigest NVARCHAR(64) NULL,
                CONSTRAINT PK_ObservedIdentities PRIMARY KEY NONCLUSTERED (SubjectKind, SubjectValue)
            );
            CREATE INDEX IX_ObservedIdentities_Value ON ObservedIdentities (SubjectValue, SubjectKind);
            CREATE INDEX IX_ObservedIdentities_Digest ON ObservedIdentities (IdentityDigest);
        END;
        IF OBJECT_ID(N'ObservedIdentitySecurityTags', N'U') IS NULL
        BEGIN
            CREATE TABLE ObservedIdentitySecurityTags (
                SubjectKind NVARCHAR(64) COLLATE Latin1_General_BIN2 NOT NULL,
                SubjectValue NVARCHAR(450) COLLATE Latin1_General_BIN2 NOT NULL,
                TagKey NVARCHAR(255) NOT NULL,
                TagValue NVARCHAR(255) NOT NULL
            );
            CREATE INDEX IX_ObservedIdentitySecurityTags_Owner ON ObservedIdentitySecurityTags (SubjectKind, SubjectValue);
            CREATE INDEX IX_ObservedIdentitySecurityTags_KeyValue ON ObservedIdentitySecurityTags (TagKey, TagValue);
        END;
        """;
}