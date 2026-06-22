// <copyright file="AzureStorageObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the
/// control plane has observed, each persisted as one Table entity holding its <see cref="ObservedIdentity"/> document in
/// a binary <c>Doc</c> property, keyed by (SubjectKind, SubjectValue). A sighting upserts; the prefix typeahead is a
/// keyset page in <c>(SubjectValue, SubjectKind)</c> order with the caller's read-reach (§17.1) applied in memory over
/// the candidate set. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// <para>The entity key folds the two key parts: PartitionKey = the (encoded) subject value and RowKey = the (encoded)
/// subject kind, so every sighting of a (kind, value) maps to one entity that a re-sighting overwrites. Both are
/// derived strings that may contain Table-forbidden characters (control chars, <c>/\#?</c>), so each is URL-safe-base64
/// encoded; the encoding is not ordinal-order-preserving, so — as the <see cref="AzureStorageSourceCredentialStore"/>
/// does — the keyset cannot be pushed as a server-side range filter. Instead the plain key columns are pulled, sorted in
/// memory into the contractual <c>(subjectValue, subjectKind)</c> total order, paged, and only the page's documents are
/// fetched.</para>
/// <para>Reach-filtering (§17.1) is applied in process: Table OData cannot match inside the serialized identity tags, so
/// each admitted candidate is materialised and its tags checked against the caller's <see cref="SecurityFilter"/> — the
/// catalog store's idiom (§14.2). An unrestricted (System) reach touches no candidate's tags. Provision the table once
/// with <see cref="PrepareAsync(string, CancellationToken)"/>, then open the store with
/// <see cref="ConnectAsync(string, TimeProvider?, CancellationToken)"/>.</para>
/// </remarks>
public sealed class AzureStorageObservedIdentityStore : IObservedIdentityStore
{
    private const string IdentitiesTable = "arazzoObservedIdentities";
    private const string DocColumn = "Doc";
    private const string SubjectKindColumn = "SubjectKind";
    private const string SubjectValueColumn = "SubjectValue";
    private const string DigestColumn = "IdentityDigest";

    // The collision-probe digest index (design §16.5.4) is folded into the same table as a secondary entity per identity:
    // its PartitionKey is "digest:" + digest, so every grantee that resolves to a set-equal identity lands in one
    // partition that FindIdentityConflictAsync reads with a single PartitionKey-scoped query (an indexed seek, never a
    // scan). Table storage only indexes PartitionKey/RowKey, so SearchAsync's keyset-by-(value, kind) scan cannot also
    // push a digest property filter — hence a dedicated index entity rather than a non-indexed IdentityDigest column
    // scan. The index partition is disjoint from the primary entities' value-derived partitions because of this prefix.
    private const string DigestPartitionPrefix = "digest:";

    // Every primary entity's PartitionKey is Enc(value), which always begins with this character; the digest-index
    // partitions ("digest:"…) sort strictly below it. SearchAsync filters PartitionKey >= this floor so it enumerates
    // only primary entities and never the secondary index ones (shared with Enc so the two cannot drift apart).
    private const string PrimaryPartitionFloor = "~";

    private readonly TableClient identities;
    private readonly TimeProvider timeProvider;

    private AzureStorageObservedIdentityStore(TableClient identities, TimeProvider timeProvider)
    {
        this.identities = identities;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the observed-identities table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the observed-identities table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(IdentitiesTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageObservedIdentityStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageObservedIdentityStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageObservedIdentityStore>(
            new AzureStorageObservedIdentityStore(tableService.GetTableClient(IdentitiesTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();

        // The PartitionKey / SubjectValue column is the storage-key leaf (a Table key string), so the value reifies once
        // here as the key; the document body is serialized bytes-to-bytes from the value/label JSON values at the
        // synchronous serialize calls below.
        string valueKey = (string)value;
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Read-merge-write: a first sighting inserts; an existing one preserves firstSeen, bumps lastSeen, unions
        // provenance, and refreshes label/identity/completeness (the shared merge). The re-sighting overwrites via
        // UpsertEntity (Replace), so the (kind, value) stays one entity. The existing read also yields the prior identity
        // digest, so a re-sighting that changes the identity can retract the stale digest-index entity below.
        // The driver mints the existing document array (the table entity's binary column); parse it NON-COPYING (it stays
        // alive through the synchronous merge) — no pooled read buffer, no copy.
        (byte[]? existing, string? oldDigest) = await this.ReadDocumentAsync(kindToken, valueKey, cancellationToken).ConfigureAwait(false);
        byte[] json;
        if (existing is null)
        {
            json = ObservedIdentitySerialization.SerializeNew(kind, value, label, identity, complete, now, provenance);
        }
        else
        {
            using ParsedJsonDocument<ObservedIdentity> current = ParsedJsonDocument<ObservedIdentity>.Parse(existing.AsMemory());
            json = ObservedIdentitySerialization.SerializeUpserted(current.RootElement, kind, value, label, identity, complete, now, provenance);
        }

        // The new digest is carried on the primary entity (so the next re-sighting knows which index entity to retract)
        // and is the partition of the secondary index entity. The empty identity has no digest: no index entry, and the
        // column is simply absent.
        string? newDigest = SecurityIdentityDigest.Compute(identity);
        var entity = new TableEntity(PartitionKey(valueKey), RowKey(kindToken))
        {
            [SubjectKindColumn] = kindToken,
            [SubjectValueColumn] = valueKey,
            [DocColumn] = json,
        };
        if (newDigest is not null)
        {
            entity[DigestColumn] = newDigest;
        }

        await this.identities.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        // Maintain the disjoint digest-index partition. The primary entity is authoritative; the index trails it (the
        // sibling state store's primary-then-index idiom), so a crash between the two writes leaves a stale-or-missing
        // index entry that the next sighting reconciles — never a wrong conflict, because the probe re-reads the primary
        // entity for its current label and the index only narrows the candidate set.
        if (oldDigest is not null && !string.Equals(oldDigest, newDigest, StringComparison.Ordinal))
        {
            // The identity changed (or was cleared) on this re-sighting: retract the prior digest's index entity so the
            // retired identity no longer collides (the §16.5.4 re-sighting retraction). DeleteEntityAsync returns 404 (not
            // an exception) when it was already absent.
            await this.identities
                .DeleteEntityAsync(DigestPartition(oldDigest), DigestRowKey(kindToken, valueKey), ETag.All, cancellationToken)
                .ConfigureAwait(false);
        }

        if (newDigest is not null)
        {
            var indexEntity = new TableEntity(DigestPartition(newDigest), DigestRowKey(kindToken, valueKey))
            {
                [SubjectKindColumn] = kindToken,
                [SubjectValueColumn] = valueKey,
            };
            await this.identities.UpsertEntityAsync(indexEntity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The prefix is matched ordinally against the SubjectValue key string, so it reifies once for the per-row
        // StartsWith break (and its length guard) below (undefined prefix matches all).
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

        // The read reach is constant for the call; an unrestricted (System) reach admits everything without materialising
        // a single candidate's tags — only a scoped reach pays the per-candidate materialise-and-check cost (§17.1).
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        // The contractual order is (subjectValue, subjectKind). Table storage orders by (PartitionKey, RowKey) =
        // (Enc(value), Enc(kind)), but Enc is URL-safe base64 and therefore NOT ordinal-order-preserving, so the keyset
        // cannot be pushed as a server-side range filter. Instead the entity keys (decoded into the plain
        // SubjectValue/SubjectKind columns) are pulled, sorted in memory into the total order, paged, and only the page's
        // documents are fetched.
        // Enumerate only the primary entities — the secondary digest-index entities (PartitionKey "digest:"…) share this
        // table and carry the same SubjectKind/SubjectValue columns, so without this floor they would leak into the page.
        string primaryOnly = TableClient.CreateQueryFilter($"PartitionKey ge {PrimaryPartitionFloor}");
        var keys = new List<EntityKey>();
        await foreach (TableEntity entity in this.identities.QueryAsync<TableEntity>(
            primaryOnly, select: [SubjectKindColumn, SubjectValueColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(SubjectValueColumn) is not { } subjectValue ||
                entity.GetString(SubjectKindColumn) is not { } subjectKind)
            {
                continue;
            }

            // Cheap server-unexpressible filters applied before the document fetch: the optional kind equality and the
            // prefix (ordinal, the order the contract pages by).
            if (kindToken is not null && !string.Equals(subjectKind, kindToken, StringComparison.Ordinal))
            {
                continue;
            }

            if (prefixStr.Length > 0 && !subjectValue.StartsWith(prefixStr, StringComparison.Ordinal))
            {
                continue;
            }

            keys.Add(new EntityKey(subjectValue, subjectKind));
        }

        keys.Sort(static (a, b) =>
        {
            int byValue = string.CompareOrdinal(a.SubjectValue, b.SubjectValue);
            return byValue != 0 ? byValue : string.CompareOrdinal(a.SubjectKind, b.SubjectKind);
        });

        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextValue = null, nextKind = null;
        try
        {
            EntityKey last = default;
            foreach (EntityKey key in keys)
            {
                // Skip entities at or before the cursor in (value, kind) total order — already returned in an earlier page.
                if (hasCursor && Compare(key, cursor) <= 0)
                {
                    continue;
                }

                // Fetch the Document only now, for entities past the cursor, and only until the page fills plus one.
                TableEntity entity = (await this.identities.GetEntityAsync<TableEntity>(
                    PartitionKey(key.SubjectValue), RowKey(key.SubjectKind), [DocColumn], cancellationToken).ConfigureAwait(false)).Value;
                if (entity.GetBinary(DocColumn) is not { } json)
                {
                    continue;
                }

                // Reach filter (§17.1): a scoped caller discovers an identity only when their read-reach admits its tags.
                // Table OData cannot match inside the serialized tags, so materialise the candidate and check in process —
                // the catalog store's idiom. An unrestricted reach skips this entirely (no materialisation).
                if (readReach is not null)
                {
                    bool admitted;
                    using (ParsedJsonDocument<ObservedIdentity> candidate = PersistedJson.ToPooledDocument<ObservedIdentity>(json))
                    {
                        admitted = readReach.IsSatisfiedBy(candidate.RootElement.IdentityTagsValue);
                    }

                    if (!admitted)
                    {
                        continue; // not reach-visible to this caller (non-disclosing)
                    }
                }

                if (docs.Count == pageSize)
                {
                    // At least one further admitted row remains → there is a next page; resume after the last included row.
                    nextValue = last.SubjectValue;
                    nextKind = last.SubjectKind;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(json));
                last = key;
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
        // The empty (unscoped) identity has no digest and never collides; otherwise read the digest's index partition —
        // a single PartitionKey-scoped query (an indexed seek, never a table scan) — for an entry that is a DIFFERENT
        // grantee. Full reach (§16.5.4): the probe is deliberately not reach-filtered, so a cross-tenant collision is
        // visible to the authoring path that must refuse it.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        string kindToken = kind.ToToken();

        // The SubjectValue column is the storage-key string, so the value reifies once for the "self" comparison.
        string valueKey = (string)value;
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {DigestPartition(digest)}");
        await foreach (TableEntity indexEntity in this.identities.QueryAsync<TableEntity>(
            filter, select: [SubjectKindColumn, SubjectValueColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (indexEntity.GetString(SubjectKindColumn) is not { } conflictKindToken ||
                indexEntity.GetString(SubjectValueColumn) is not { } conflictValue)
            {
                continue;
            }

            // The same grantee is not in conflict with itself — identify "self" by its (kind, value), the grantee's
            // identity, rather than by the encoded RowKey (robust to which entity-row surfaces it).
            if (string.Equals(conflictKindToken, kindToken, StringComparison.Ordinal) && string.Equals(conflictValue, valueKey, StringComparison.Ordinal))
            {
                continue;
            }

            // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label
            // live in the record, so nothing is reified into a separate POCO here. The label is authoritative on the
            // conflicting party's primary entity (it can change on a re-sighting), read bytes-to-bytes from there rather
            // than denormalised onto the index entity. A concurrently deleted primary yields no document — skip it.
            (byte[]? document, _) = await this.ReadDocumentAsync(conflictKindToken, conflictValue, cancellationToken).ConfigureAwait(false);
            if (document is not null)
            {
                return PersistedJson.ToPooledDocument<ObservedIdentity>(document);
            }
        }

        return null;
    }

    // Orders a key against a decoded keyset cursor in the contractual total order: (subjectValue, subjectKind) ordinal.
    private static int Compare(in EntityKey key, in (string SubjectValue, string SubjectKind) cursor)
    {
        int byValue = string.CompareOrdinal(key.SubjectValue, cursor.SubjectValue);
        return byValue != 0 ? byValue : string.CompareOrdinal(key.SubjectKind, cursor.SubjectKind);
    }

    // The PartitionKey is the subject value and the RowKey is the subject-kind token. Both are user-supplied/derived
    // strings that may contain Table-forbidden characters (/\#? and control chars), so each is URL-safe-base64 encoded.
    private static string PartitionKey(string value) => Enc(value);

    private static string RowKey(string kindToken) => Enc(kindToken);

    // The digest-index entity's partition is "digest:" + the 64-char hex digest (hex is a Table-legal PartitionKey, so it
    // needs no encoding) — disjoint from the '~'-prefixed primary partitions. Its RowKey deterministically folds the
    // (kindToken, value) so a re-sighting addresses the exact entity to retract; the parts are URL-safe-base64 encoded
    // (they may contain Table-forbidden characters) and joined by '~'. The kind/value are read back from the index
    // entity's own columns (not by un-splitting this key), so the key need only be stable, never reversible.
    private static string DigestPartition(string digest) => DigestPartitionPrefix + digest;

    private static string DigestRowKey(string kindToken, string value) => Enc(kindToken) + "~" + Enc(value);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and those
    // two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the empty
    // string, which Table storage forbids as a key.
    private static string Enc(string value)
        => PrimaryPartitionFloor + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // Reads the primary entity's persisted document and its stored identity digest (the latter so a re-sighting can
    // retract a now-stale digest-index entity). A missing entity yields (null, null).
    private async ValueTask<(byte[]? Document, string? Digest)> ReadDocumentAsync(string kindToken, string value, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> existing = await this.identities
            .GetEntityIfExistsAsync<TableEntity>(PartitionKey(value), RowKey(kindToken), [DocColumn, DigestColumn], cancellationToken)
            .ConfigureAwait(false);
        return existing.HasValue
            ? (existing.Value!.GetBinary(DocColumn), existing.Value!.GetString(DigestColumn))
            : (null, null);
    }

    // The decoded entity key columns (the plain subject value/kind, not the base64 PartitionKey/RowKey), carried so the
    // listing snapshot can be put into the contractual total order without re-decoding the keys.
    private readonly record struct EntityKey(string SubjectValue, string SubjectKind);
}