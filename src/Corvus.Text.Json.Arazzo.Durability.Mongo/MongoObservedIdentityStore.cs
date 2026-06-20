// <copyright file="MongoObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane has
/// observed, each persisted as its <see cref="ObservedIdentity"/> document keyed by the composite <c>_id</c>
/// (<c>{ k: subjectKind, v: subjectValue }</c>). A sighting upserts; the prefix typeahead is an indexed keyset page in
/// <c>(subjectValue, subjectKind)</c> order, made a single ascending range scan by a precomputed <c>sortKey</c>
/// (<c>subjectValue \0 subjectKind</c>).
/// </summary>
/// <remarks>
/// <para>The caller's read-reach (§17.1) is applied <strong>in memory</strong> over each candidate's persisted
/// <c>sys:</c> tags (the catalog/source-credential idiom), so the server-side <c>Limit</c> is dropped when a reach
/// filter is present: the <c>sortKey</c>-ordered cursor is streamed and the page filled from the admitted rows,
/// preserving keyset paging. An unrestricted (System) reach skips the per-row materialisation entirely and may push a
/// server <c>Limit</c>. The prefix is a case-sensitive anchored regex on <c>subjectValue</c> (ordinal, unlike the
/// catalog's lower-cased match) so the contract's ordinal order is honoured; the keyset bound is a <c>sortKey</c>
/// comparison (MongoDB's default binary collation is the ordinal order the contract pages by).</para>
/// <para>The driver pools connections internally, so the store is naturally concurrent. Create instances with
/// <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/> after provisioning with
/// <see cref="PrepareAsync(string, string, CancellationToken)"/>.</para>
/// </remarks>
public sealed class MongoObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    // The null control char cannot appear in a subjectValue/subjectKind token and is the lowest byte, so it joins the
    // two key parts into a sortKey whose ascending binary order is exactly the contract's (subjectValue, subjectKind).
    private const char Separator = (char)0;

    private readonly IMongoClient client;
    private readonly bool ownsClient;
    private readonly TimeProvider timeProvider;
    private readonly IMongoCollection<BsonDocument> identities;

    private MongoObservedIdentityStore(IMongoClient client, string databaseName, bool ownsClient, TimeProvider timeProvider)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.timeProvider = timeProvider;
        IMongoDatabase database = client.GetDatabase(databaseName);
        this.identities = database.GetCollection<BsonDocument>("observed_identities");
    }

    /// <summary>Provisions the store's indexes over a connection string.</summary>
    /// <remarks>
    /// Creating indexes requires the <c>createIndex</c> privilege, so run this once at deploy/migration time, separately
    /// from the least-privileged user used to <see cref="ConnectAsync(string, string, TimeProvider?, CancellationToken)"/>
    /// the store for operation. (The collection itself is created lazily on first write, so the operational user needs
    /// only <c>readWrite</c>.)
    /// </remarks>
    /// <param name="connectionString">A MongoDB connection string for a user permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        var client = new MongoClient(connectionString);
        await using var store = new MongoObservedIdentityStore(client, databaseName, ownsClient: true, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the store's indexes over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client permitted to create indexes.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the indexes exist (the operation is idempotent).</returns>
    public static async ValueTask PrepareAsync(IMongoClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        await using var store = new MongoObservedIdentityStore(client, databaseName, ownsClient: false, TimeProvider.System);
        await store.EnsureIndexesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned database.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client).</returns>
    public static ValueTask<MongoObservedIdentityStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        var client = new MongoClient(connectionString);
        return new ValueTask<MongoObservedIdentityStore>(new MongoObservedIdentityStore(client, databaseName, ownsClient: true, timeProvider ?? TimeProvider.System));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoObservedIdentityStore> ConnectAsync(IMongoClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoObservedIdentityStore>(new MongoObservedIdentityStore(client, databaseName, ownsClient: false, timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, JsonString value, JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();

        // The _id / subjectValue / sortKey are text storage keys, so the value reifies once here; the document body is
        // serialized bytes-to-bytes from the value/label JSON values at the synchronous serialize calls below (taken
        // after the read-await).
        string valueKey = (string)value;
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // Read-merge-write keyed by (kind, value): a first sighting inserts; an existing one preserves firstSeen, bumps
        // lastSeen, unions provenance, and refreshes label/identity/completeness (the shared merge). The driver pools
        // connections so concurrent sightings race on the unique _id; ReplaceOne with IsUpsert resolves either case.
        byte[]? existing = await this.ReadDocumentAsync(kindToken, valueKey, cancellationToken).ConfigureAwait(false);
        byte[] json = existing is null
            ? ObservedIdentitySerialization.SerializeNew(kind, value, label, identity, complete, now, provenance)
            : ObservedIdentitySerialization.SerializeUpserted(existing, kind, value, label, identity, complete, now, provenance);

        var document = new BsonDocument
        {
            ["_id"] = Key(kindToken, valueKey),
            ["sortKey"] = SortKey(valueKey, kindToken),
            ["subjectKind"] = kindToken,
            ["subjectValue"] = valueKey,
            ["doc"] = new BsonBinaryData(json),

            // The indexed collision-probe key (§16.5.4): the order-independent digest of the sys: identity, or BsonNull
            // for the empty (unscoped) identity, which never collides. The full ReplaceOne below overwrites this field,
            // so a re-sighting that changes (or clears) the identity rewrites the digest in step with the document.
            ["identityDigest"] = (BsonValue?)SecurityIdentityDigest.Compute(identity) ?? BsonNull.Value,
        };

        await this.identities.ReplaceOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", Key(kindToken, valueKey)),
            document,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, JsonString prefix, int limit, string? pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

        // The subjectValue field is text, so the prefix reifies once for the anchored regex + the .Length test.
        string prefixStr = prefix.IsNotUndefined() ? (string)prefix : string.Empty;
        bool hasCursor = ObservedIdentityContinuationToken.TryDecode(pageToken, out (string SubjectValue, string SubjectKind) cursor);
        SecurityFilter? readReach = context.Reach(AccessVerb.Read);

        // Keyset seek past the cursor in (subjectValue, subjectKind) order — an indexed range scan over sortKey (binary
        // collation = ordinal). The optional kind equality and a CASE-SENSITIVE anchored prefix regex on subjectValue
        // (ordinal, no "i") keep the scan in the contiguous prefix range; the ascending sortKey sort is the total order.
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.Empty;
        if (kindToken is not null)
        {
            filter = b.And(filter, b.Eq("subjectKind", kindToken));
        }

        if (prefixStr.Length > 0)
        {
            filter = b.And(filter, b.Regex("subjectValue", new BsonRegularExpression("^" + EscapeRegex(prefixStr))));
        }

        if (hasCursor)
        {
            filter = b.And(filter, b.Gt("sortKey", SortKey(cursor.SubjectValue, cursor.SubjectKind)));
        }

        // The reach (§17.1) is applied in memory over each candidate's persisted security tags (see the class remarks),
        // so the server-side Limit is dropped when a reach filter is present: stream the sortKey-ordered cursor and fill
        // the page from the admitted rows, preserving keyset paging. System reach materialises nothing and may push Limit.
        IFindFluent<BsonDocument, BsonDocument> find = this.identities.Find(filter).Sort(Builders<BsonDocument>.Sort.Ascending("sortKey"));
        if (readReach is null)
        {
            find = find.Limit(pageSize + 1);
        }

        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextToken = null;
        try
        {
            // A FURTHER admitted row beyond the page is the signal to emit a continuation token — the (value, kind) of
            // the last *included* identity, so the next request seeks strictly past it.
            using IAsyncCursor<BsonDocument> mongoCursor = await find.ToCursorAsync(cancellationToken).ConfigureAwait(false);
            string lastValue = string.Empty, lastKind = string.Empty;
            bool stop = false;
            while (!stop && await mongoCursor.MoveNextAsync(cancellationToken).ConfigureAwait(false))
            {
                foreach (BsonDocument document in mongoCursor.Current)
                {
                    byte[] json = document["doc"].AsBsonBinaryData.Bytes;
                    if (readReach is not null)
                    {
                        using ParsedJsonDocument<ObservedIdentity> candidate = PersistedJson.ToPooledDocument<ObservedIdentity>(json);
                        if (!readReach.IsSatisfiedBy(candidate.RootElement.IdentityTagsValue))
                        {
                            continue;
                        }
                    }

                    if (docs.Count == pageSize)
                    {
                        nextToken = ObservedIdentityContinuationToken.Encode(lastValue, lastKind);
                        stop = true;
                        break;
                    }

                    docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(json));
                    lastValue = document["subjectValue"].AsString;
                    lastKind = document["subjectKind"].AsString;
                }
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
    public async ValueTask<ParsedJsonDocument<ObservedIdentity>?> FindIdentityConflictAsync(ObservedIdentity.GranteeKind kind, JsonString value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise seek the indexed digest for a document whose identity
        // is set-equal (same digest) but whose (kind, value) differs — a non-unique identity the authoring path refuses.
        // Full reach (§16.5.4): the probe is deliberately NOT reach-filtered, so a cross-tenant collision is still found.
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        string kindToken = kind.ToToken();
        string valueKey = (string)value;
        FilterDefinitionBuilder<BsonDocument> b = Builders<BsonDocument>.Filter;
        FilterDefinition<BsonDocument> filter = b.And(
            b.Eq("identityDigest", digest),
            b.Not(b.And(b.Eq("subjectKind", kindToken), b.Eq("subjectValue", valueKey))));

        BsonDocument? match = await this.identities.Find(filter).Limit(1).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label live
        // in the record, so nothing is reified into a separate POCO here.
        return match is null
            ? null
            : PersistedJson.ToPooledDocument<ObservedIdentity>(match["doc"].AsBsonBinaryData.Bytes);
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }

    // The composite document id: (subjectKind, subjectValue). The value can carry arbitrary text (and the canonical
    // separator control char), so it is held as a structured subdocument rather than concatenated into a string id —
    // the unique _id then keys the upsert by the natural (kind, value).
    private static BsonDocument Key(string kindToken, string value)
        => new()
        {
            ["k"] = kindToken,
            ["v"] = value,
        };

    // The keyset sort key: subjectValue, the null separator, then subjectKind — so an ascending binary sort yields the
    // contract's (subjectValue, subjectKind) ordinal order, with the separator (the lowest byte) keeping a value that
    // is a prefix of another value ordered before it.
    private static string SortKey(string value, string kindToken) => string.Concat(value, Separator.ToString(), kindToken);

    private static string EscapeRegex(string value) => System.Text.RegularExpressions.Regex.Escape(value);

    private async ValueTask<byte[]?> ReadDocumentAsync(string kindToken, string value, CancellationToken cancellationToken)
    {
        BsonDocument? document = await this.identities
            .Find(Builders<BsonDocument>.Filter.Eq("_id", Key(kindToken, value)))
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document?["doc"].AsBsonBinaryData.Bytes;
    }

    private async ValueTask EnsureIndexesAsync(CancellationToken cancellationToken)
    {
        var bySortKey = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("sortKey"));
        var bySubjectKind = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("subjectKind"));
        var bySubjectValue = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("subjectValue"));
        var byIdentityDigest = new CreateIndexModel<BsonDocument>(Builders<BsonDocument>.IndexKeys.Ascending("identityDigest"));
        await this.identities.Indexes.CreateManyAsync([bySortKey, bySubjectKind, bySubjectValue, byIdentityDigest], cancellationToken).ConfigureAwait(false);
    }
}