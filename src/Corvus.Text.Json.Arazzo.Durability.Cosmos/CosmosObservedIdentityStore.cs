// <copyright file="CosmosObservedIdentityStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Microsoft.Azure.Cosmos;

namespace Corvus.Text.Json.Arazzo.Durability.Cosmos;

/// <summary>
/// A Cosmos DB-backed <see cref="IObservedIdentityStore"/> (design §16.5.4): the distinct grantees the control plane has
/// observed, each persisted as its <see cref="ObservedIdentity"/> document keyed by (SubjectKind, SubjectValue). A
/// sighting upserts; the prefix typeahead is a native keyset query with the caller's read-reach (§17.1)
/// <strong>pushed down to the query</strong>.
/// </summary>
/// <remarks>
/// <para>Reach-filtering follows the catalog store's optimised pattern (§14.2): each identity's <c>sys:</c> tags are
/// embedded as a <c>securityTags</c> array (<c>{ "k": …, "v": … }</c>) on the document, and the caller's
/// <see cref="SecurityFilter"/> is translated by the <see cref="CosmosSecurityRuleEmitter"/> into a native
/// <c>EXISTS</c> over that array, so a scoped search is filtered server-side. An unrestricted (System) reach emits no
/// predicate. The prefix is a native, case-sensitive <c>STARTSWITH(c.subjectValue, …)</c> and the page orders by a
/// stored <c>sortKey</c> (<c>subjectValue</c> + a unit separator + <c>subjectKind</c>) whose string order is the
/// ordinal <c>(subjectValue, subjectKind)</c> order the contract pages by. Documents are written and read through the
/// Cosmos <em>stream</em> APIs (the binding doc embedded verbatim, no SDK serializer).</para>
/// </remarks>
public sealed class CosmosObservedIdentityStore : IObservedIdentityStore, IAsyncDisposable
{
    private const string ContainerId = "workflow_observed_identities";
    private const char Separator = '\u001f';

    private readonly CosmosClient client;
    private readonly Container container;
    private readonly TimeProvider timeProvider;
    private readonly bool ownsClient;

    private CosmosObservedIdentityStore(CosmosClient client, Container container, TimeProvider timeProvider, bool ownsClient)
    {
        this.client = client;
        this.container = container;
        this.timeProvider = timeProvider;
        this.ownsClient = ownsClient;
    }

    /// <summary>Provisions the database and container (requires an account/control-plane credential); run once at deploy time.</summary>
    /// <param name="connectionString">A Cosmos DB connection string.</param>
    /// <param name="databaseName">The database name; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static async ValueTask PrepareAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        using var client = new CosmosClient(connectionString);
        await ProvisionAsync(client, databaseName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Provisions the database and container over a caller-supplied client.</summary>
    /// <param name="client">A Cosmos client.</param>
    /// <param name="databaseName">The database name; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the database and container exist (idempotent).</returns>
    public static ValueTask PrepareAsync(CosmosClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        return ProvisionAsync(client, databaseName, cancellationToken);
    }

    /// <summary>Opens the store for operation against an already-provisioned database/container.</summary>
    /// <param name="connectionString">A Cosmos DB connection string.</param>
    /// <param name="databaseName">The database name; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it owns and disposes the client it creates).</returns>
    public static ValueTask<CosmosObservedIdentityStore> ConnectAsync(string connectionString, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosObservedIdentityStore>(Connect(new CosmosClient(connectionString), databaseName, timeProvider, ownsClient: true));
    }

    /// <summary>Opens the store for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A Cosmos client.</param>
    /// <param name="databaseName">The database name; defaults to <c>arazzo</c>.</param>
    /// <param name="timeProvider">The time source for sighting timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store (it does not dispose the supplied client).</returns>
    public static ValueTask<CosmosObservedIdentityStore> ConnectAsync(CosmosClient client, string databaseName = "arazzo", TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<CosmosObservedIdentityStore>(Connect(client, databaseName, timeProvider, ownsClient: false));
    }

    // The seam's JsonString is fully qualified here (and in SearchAsync/FindIdentityConflictAsync): V5 has no shared
    // JsonString — each generation root emits its own — and this project references the base Corvus.Text.Json directly,
    // so an unqualified JsonString binds to a different per-root type than the interface's …Durability.JsonString
    // (CS0535). The FQN pins the exact seam type.

    /// <inheritdoc/>
    public async ValueTask SeenAsync(ObservedIdentity.GranteeKind kind, Corvus.Text.Json.Arazzo.Durability.JsonString value, Corvus.Text.Json.Arazzo.Durability.JsonString label, SecurityTagSet identity, bool complete, string provenance, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provenance);
        string kindToken = kind.ToToken();
        DateTimeOffset now = this.timeProvider.GetUtcNow();

        // subjectValue is the storage-key leaf (item id / partition key / query param / sortKey / stored column), so the
        // value reifies once here; the document body is serialized bytes-to-bytes from the value/label JSON values at the
        // synchronous serialize calls below (taken after the read-await).
        string valueKey = (string)value;
        string partition = PartitionKey(kindToken, valueKey);

        // Read the existing document (single partition) into a pooled document — copied off the live query response, not a
        // GC array — to merge; then upsert the whole envelope. The parsed model is read synchronously by the merge below.
        using ParsedJsonDocument<ObservedIdentity>? existing = await this.ReadDocumentAsync(kindToken, valueKey, partition, cancellationToken).ConfigureAwait(false);

        // Serialize the document into a pooled buffer; its span is written straight into the envelope (no GC document
        // array). The buffer is alive through the synchronous envelope build, then returned to the pool.
        using PooledUtf8 doc = existing is null
            ? ObservedIdentitySerialization.SerializeNewPooled(kind, value, label, identity, complete, now, provenance)
            : ObservedIdentitySerialization.SerializeUpsertedPooled(existing.RootElement, kind, value, label, identity, complete, now, provenance);

        using Stream stream = EnvelopeStream(ItemId(kindToken, valueKey), partition, kindToken, valueKey, SecurityIdentityDigest.Compute(identity), identity.ToList(), doc.Memory);
        using ResponseMessage response = await this.container.UpsertItemStreamAsync(stream, new PartitionKey(partition), cancellationToken: cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    /// <inheritdoc/>
    public async ValueTask<ObservedIdentityPage> SearchAsync(AccessContext context, ObservedIdentity.GranteeKind kind, Corvus.Text.Json.Arazzo.Durability.JsonString prefix, int limit, Corvus.Text.Json.Arazzo.Durability.JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        int pageSize = limit > 0 ? limit : 1;
        string? kindToken = kind.IsNotUndefined() ? kind.ToToken() : null;

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

        // subjectValue is stored as a string column, so the prefix reifies once for the STARTSWITH @prefix param.
        string prefixStr = prefix.IsNotUndefined() ? (string)prefix : string.Empty;
        var conditions = new List<string>();
        var parameters = new List<(string Name, string Value)>();
        if (kindToken is not null)
        {
            conditions.Add("c.subjectKind = @kind");
            parameters.Add(("@kind", kindToken));
        }

        if (prefixStr.Length > 0)
        {
            // Native, case-sensitive prefix (the contract is ordinal): STARTSWITH defaults to case-sensitive.
            conditions.Add("STARTSWITH(c.subjectValue, @prefix)");
            parameters.Add(("@prefix", prefixStr));
        }

        // Reach (§17.1) as a native EXISTS over the embedded securityTags array; System reach emits none.
        if (readReach is not null)
        {
            int securityParam = 0;
            var emitter = new CosmosSecurityRuleEmitter("c.securityTags", "k", "v", v =>
            {
                string name = "@sec" + securityParam++.ToString(CultureInfo.InvariantCulture);
                parameters.Add((name, v));
                return name;
            });
            conditions.Add(readReach.ToSqlPredicate(emitter));
        }

        if (hasCursor)
        {
            conditions.Add("c.sortKey > @after");
            parameters.Add(("@after", SortKey(cursor.SubjectValue, cursor.SubjectKind)));
        }

        string where = conditions.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", conditions);
        var definition = new QueryDefinition("SELECT c.subjectValue, c.subjectKind, c.doc FROM c" + where + " ORDER BY c.sortKey");
        foreach ((string name, string value) in parameters)
        {
            definition = definition.WithParameter(name, value);
        }

        var docs = new PooledDocumentList<ObservedIdentity>(pageSize);
        string? nextValue = null, nextKind = null;
        try
        {
            string lastValue = string.Empty, lastKind = string.Empty;
            await foreach ((string rowValue, string rowKind, ReadOnlyMemory<byte> doc) in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
            {
                if (docs.Count == pageSize)
                {
                    // Fetched one beyond the page (every filter, including reach, was applied server-side) — a next page
                    // exists; the cursor resumes after the LAST INCLUDED row, not this overflow row.
                    nextValue = lastValue;
                    nextKind = lastKind;
                    break;
                }

                docs.Add(PersistedJson.ToPooledDocument<ObservedIdentity>(doc.Span));
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
    public async ValueTask<ParsedJsonDocument<ObservedIdentity>?> FindIdentityConflictAsync(ObservedIdentity.GranteeKind kind, Corvus.Text.Json.Arazzo.Durability.JsonString value, SecurityTagSet identity, CancellationToken cancellationToken)
    {
        // The empty (unscoped) identity never collides; otherwise seek the indexed identityDigest property for an item
        // whose identity is set-equal (same digest) but whose (kind, value) differs — a non-unique identity the authoring
        // path refuses. This probe runs at FULL reach (a cross-tenant collision must be visible), so unlike SearchAsync it
        // pushes no reach predicate down and the query spans every partition (no PartitionKey on the request).
        if (SecurityIdentityDigest.Compute(identity) is not { } digest)
        {
            return null;
        }

        // subjectValue is stored as a string column, so the value reifies once for the @v query param.
        string kindToken = kind.ToToken();
        string valueKey = (string)value;
        var definition = new QueryDefinition(
            "SELECT c.subjectValue, c.subjectKind, c.doc FROM c WHERE c.identityDigest = @d AND NOT (c.subjectKind = @k AND c.subjectValue = @v) OFFSET 0 LIMIT 1")
            .WithParameter("@d", digest)
            .WithParameter("@k", kindToken)
            .WithParameter("@v", valueKey);

        await foreach ((string rowValue, string rowKind, ReadOnlyMemory<byte> doc) in this.QueryDocumentsAsync(definition, cancellationToken).ConfigureAwait(false))
        {
            // Hand back the conflicting grantee as its own JSON document (the caller disposes it); its kind/value/label
            // live in the record, so nothing is reified into a separate POCO here. The doc span is a view into the query
            // page, so the copy into the pooled document must complete before iteration moves on — it does (we return).
            return PersistedJson.ToPooledDocument<ObservedIdentity>(doc.Span);
        }

        return null;
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient)
        {
            this.client.Dispose();
        }

        return ValueTask.CompletedTask;
    }

    // The partition for an identity: subjectKind + unit separator + subjectValue (the separator cannot appear in either,
    // so the join is unambiguous), giving a single-partition point read/upsert per identity.
    private static string PartitionKey(string subjectKind, string subjectValue) => subjectKind + Separator + subjectValue;

    // The stable total order the contract pages by, as a single sortable string: subjectValue, then the subjectKind
    // tie-breaker, separated by a unit separator (which sorts before any printable character).
    private static string SortKey(string subjectValue, string subjectKind) => subjectValue + Separator + subjectKind;

    // A deterministic, opaque, Cosmos-id-safe document id from (subjectKind, subjectValue): a re-sighting of the same
    // grantee upserts the same item.
    private static string ItemId(string subjectKind, string subjectValue)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(subjectKind + Separator + subjectValue), hash);
        return "obid-" + Convert.ToHexStringLower(hash);
    }

    // Serializes the { id, pk, subjectKind, subjectValue, identityDigest, sortKey, securityTags:[{k,v}], doc } envelope
    // into a pooled stream. The identity document is itself JSON we produced, so embed it verbatim (no base64, no
    // validation). identityDigest (the §16.5.4 collision-probe index key) is the set-equal digest of the identity, or
    // null for the empty (unscoped) identity — which never collides; the re-sighting upsert replaces the whole envelope,
    // so the digest tracks any identity change.
    private static Stream EnvelopeStream(string id, string partition, string subjectKind, string subjectValue, string? identityDigest, IReadOnlyList<SecurityTag> securityTags, ReadOnlyMemory<byte> doc)
        => CosmosJson.WriteToStream(
            (Id: id, Partition: partition, Kind: subjectKind, Value: subjectValue, Digest: identityDigest, SortKey: SortKey(subjectValue, subjectKind), Tags: securityTags, Doc: doc),
            static (Utf8JsonWriter writer, in (string Id, string Partition, string Kind, string Value, string? Digest, string SortKey, IReadOnlyList<SecurityTag> Tags, ReadOnlyMemory<byte> Doc) c) =>
            {
                writer.WriteStartObject();
                writer.WriteString("id"u8, c.Id);
                writer.WriteString("pk"u8, c.Partition);
                writer.WriteString("subjectKind"u8, c.Kind);
                writer.WriteString("subjectValue"u8, c.Value);
                if (c.Digest is null)
                {
                    writer.WriteNull("identityDigest"u8);
                }
                else
                {
                    writer.WriteString("identityDigest"u8, c.Digest);
                }

                writer.WriteString("sortKey"u8, c.SortKey);
                writer.WritePropertyName("securityTags"u8);
                writer.WriteStartArray();
                foreach (SecurityTag tag in c.Tags)
                {
                    writer.WriteStartObject();
                    writer.WriteString("k"u8, tag.Key);
                    writer.WriteString("v"u8, tag.Value);
                    writer.WriteEndObject();
                }

                writer.WriteEndArray();
                writer.WritePropertyName("doc"u8);
                writer.WriteRawValue(c.Doc.Span, skipInputValidation: true);
                writer.WriteEndObject();
            });

    private static async ValueTask ProvisionAsync(CosmosClient client, string databaseName, CancellationToken cancellationToken)
    {
        Database database = await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false);
        await database.CreateContainerIfNotExistsAsync(new ContainerProperties(ContainerId, "/pk"), cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private static CosmosObservedIdentityStore Connect(CosmosClient client, string databaseName, TimeProvider? timeProvider, bool ownsClient)
    {
        Database database = client.GetDatabase(databaseName);
        Container container = database.GetContainer(ContainerId);
        return new CosmosObservedIdentityStore(client, container, timeProvider ?? TimeProvider.System, ownsClient);
    }

    // Reads the existing identity document for (subjectKind, subjectValue) from its single partition into a pooled document
    // (copied off the live response buffer, no GC array), or null. The caller disposes the returned document.
    private async ValueTask<ParsedJsonDocument<ObservedIdentity>?> ReadDocumentAsync(string subjectKind, string subjectValue, string partition, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT c.doc FROM c WHERE c.subjectKind = @kind AND c.subjectValue = @value")
            .WithParameter("@kind", subjectKind)
            .WithParameter("@value", subjectValue);
        var options = new QueryRequestOptions { PartitionKey = new PartitionKey(partition) };
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                // Copy the doc slice off the live response buffer into an owned pooled document (no GC array); the copy
                // completes before the response buffer is returned at the end of this using.
                return PersistedJson.ToPooledDocument<ObservedIdentity>(CosmosJson.GetRawValue(element, "doc"u8).Span);
            }
        }

        return null;
    }

    // Yields (subjectValue, subjectKind, doc-bytes) for each result, ordered by sortKey. The doc slice is valid only for
    // the duration of the consumer's iteration step; the consumer copies it (into a pooled document) before moving on.
    private async IAsyncEnumerable<(string SubjectValue, string SubjectKind, ReadOnlyMemory<byte> Doc)> QueryDocumentsAsync(QueryDefinition query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using FeedIterator iterator = this.container.GetItemQueryStreamIterator(query);
        while (iterator.HasMoreResults)
        {
            using ResponseMessage response = await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            using CosmosJson.RentedResponse page = await CosmosJson.ReadAllAsync(response.Content, cancellationToken).ConfigureAwait(false);
            foreach (ReadOnlyMemory<byte> element in CosmosJson.ReadDocuments(page.Memory))
            {
                string subjectValue = CosmosJson.GetString(element, "subjectValue"u8) ?? string.Empty;
                string subjectKind = CosmosJson.GetString(element, "subjectKind"u8) ?? string.Empty;
                yield return (subjectValue, subjectKind, CosmosJson.GetRawValue(element, "doc"u8));
            }
        }
    }
}