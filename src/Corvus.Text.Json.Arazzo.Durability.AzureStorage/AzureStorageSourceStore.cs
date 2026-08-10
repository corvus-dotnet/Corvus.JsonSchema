// <copyright file="AzureStorageSourceStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="ISourceStore"/> (design §7.6): registered sources persisted as
/// Table entities. Each source is one entity holding its <see cref="RegisteredSource"/> document in a binary <c>Doc</c>
/// property, with PartitionKey = the (encoded) name and RowKey = the (encoded) tag discriminator, so every source
/// for a name is a single efficient partition query. Its etag travels inside the document (independent of the Table
/// entity ETag), so optimistic concurrency is a read-compare-write. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// Point management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) in memory over
/// the small candidate set for a name, since a deployment keeps those reach-disjoint. List/count narrow through the
/// store's §14.4 label table first — the reach resolves to candidate entity keys by indexed partition lookups — so only
/// candidate rows are ever addressed, and the exact reach then decides each one; a re-tagging update re-points the
/// label entries around the entity write in the §14.4 ordering. Table queries are unordered,
/// so <see cref="ListAsync"/> sorts its snapshot client-side by (name, discriminator) to match every other backend's
/// ordering. Tag round-tripping is Corvus.Text.Json end to end (no System.Text.Json): the source bytes are stored
/// and read verbatim and the discriminator that keys the entity is the canonical tag string from
/// <see cref="SourceCredentialKey"/>.
/// </remarks>
public sealed class AzureStorageSourceStore : ISourceStore
{
    private const string SourcesTable = "arazzoSources";
    private const string LabelsTable = "arazzoSourceLabels";
    private const string DocColumn = "Doc";
    private const string NameColumn = "Name";
    private const string DiscriminatorColumn = "Tags";

    private readonly TableClient sources;
    private readonly TableClient labels;
    private readonly AzureStorageSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private AzureStorageSourceStore(TableClient sources, TableClient labels, TimeProvider timeProvider)
    {
        this.sources = sources;
        this.labels = labels;
        this.labelIndex = new AzureStorageSecurityLabelIndex(labels);
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the sources table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the sources table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(SourcesTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(LabelsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSourceStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSourceStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageSourceStore>(
            new AzureStorageSourceStore(
                tableService.GetTableClient(SourcesTable),
                tableService.GetTableClient(LabelsTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>> AddAsync(RegisteredSource draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        var entity = new TableEntity(PartitionKey(draft.NameValue), RowKey(discriminator))
        {
            [NameColumn] = draft.NameValue,
            [DiscriminatorColumn] = discriminator,
            [DocColumn] = json,
        };

        // §14.4 label entries: ADDED before the source becomes visible, so an interrupted add can only leave an
        // entry pointing at a row that never landed — which the exact reach evaluation discards — never a visible
        // row with no entry, which would hide it from a narrowed list.
        string labelRowId = LabelRowId(draft.NameValue, discriminator);
        foreach (string token in AzureStorageSecurityLabelIndex.TokensFor(draft.ManagementTagsValue))
        {
            await this.labels.UpsertEntityAsync(new TableEntity(token, labelRowId), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await this.sources.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            ThrowHelper.ThrowSourceAlreadyExists(draft.NameValue);
        }

        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<RegisteredSource>(json);
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

        // The contractual order is name with the tag discriminator as the tie-breaker for a stable TOTAL order. Table
        // storage orders by (PartitionKey, RowKey) = (Enc(name), Enc(disc)), but Enc is URL-safe base64 and therefore
        // NOT ordinal-order-preserving, so the keyset cannot be pushed as a server-side range filter. Instead the entity
        // keys (decoded into the plain Name/Tags columns) are pulled, sorted in memory into the total order, paged, and
        // only the page's Documents are fetched.
        // §14.4: narrow to the candidate entity keys the label table admits before reading anything. A null answer
        // means the index could not narrow and the keys-only sweep runs as it always did; an empty one means no
        // source qualifies, so the page is empty without a single row addressed. The exact reach below still
        // decides every candidate (the plan is a sound over-approximation), so an imprecise plan costs throughput
        // and can never widen reach.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return SourcePage.Create(PooledDocumentList<RegisteredSource>.Empty);
        }

        var keys = new List<EntityKey>();
        if (candidates is null)
        {
            await foreach (TableEntity entity in this.sources.QueryAsync<TableEntity>(
                select: [NameColumn, DiscriminatorColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (entity.GetString(NameColumn) is not { } name ||
                    entity.GetString(DiscriminatorColumn) is not { } discriminator)
                {
                    continue;
                }

                keys.Add(new EntityKey(name, discriminator));
            }
        }
        else
        {
            foreach (string rowId in candidates)
            {
                if (TryParseLabelRowId(rowId, out (string Name, string Discriminator) parts))
                {
                    keys.Add(new EntityKey(parts.Name, parts.Discriminator));
                }
            }
        }

        keys.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

        var docs = new PooledDocumentList<RegisteredSource>(pageSize);
        bool hasMore = false;
        try
        {
            EntityKey last = default;
            foreach (EntityKey key in keys)
            {
                // Skip entities at or before the cursor in (name, disc) total order.
                if (hasCursor && Compare(key, cursor) <= 0)
                {
                    continue;
                }

                // Fetch the Document only now, for entities past the cursor, and only until the page fills plus one;
                // a stale label entry resolving to a deleted entity falls into the same absent skip.
                if (await this.TryGetDocAsync(key.Name, key.Discriminator, cancellationToken).ConfigureAwait(false) is not { } json)
                {
                    continue;
                }

                ParsedJsonDocument<RegisteredSource> cand = PersistedJson.ToPooledDocument<RegisteredSource>(json);
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
                    last = key;
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
                ? SourcePage.Create(docs, last.Name, last.Discriminator)
                : SourcePage.Create(docs);
        }
        catch
        {
            docs.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<RegisteredSource>?> UpdateAsync(string name, RegisteredSource draft, WorkflowEtag expectedEtag, string actor, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(PartitionKey(name), RowKey(discriminator!))
        {
            [NameColumn] = name,
            [DiscriminatorColumn] = discriminator!,
            [DocColumn] = json,
        };

        // The row is addressed by its frozen create-time keys (ADR 0067). A draft that supplies management tags
        // re-tags the row's reach scope (a store-level replace; an omitted set is carried forward), so the §14.4
        // label entries are re-pointed by the diff around the entity write, in the §14.4 ordering — entries for the
        // NEW tags are added before the entity carries them and the removed ones are dropped only after it stops,
        // so an interruption can only leave a stale entry (discarded by the exact evaluation), never a hidden row.
        HashSet<string>? previousTokens = null;
        HashSet<string>? desiredTokens = null;
        string labelRowId = LabelRowId(name, discriminator!);
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            SecurityTagSet previousTags;
            using (ParsedJsonDocument<RegisteredSource> current = PersistedJson.ToPooledDocument<RegisteredSource>(existing))
            {
                previousTags = SecurityTagSet.CopyFrom(current.RootElement.ManagementTags);
            }

            previousTokens = AzureStorageSecurityLabelIndex.TokensFor(previousTags);
            desiredTokens = AzureStorageSecurityLabelIndex.TokensFor(draft.ManagementTagsValue);
            foreach (string token in desiredTokens)
            {
                if (!previousTokens.Contains(token))
                {
                    await this.labels.UpsertEntityAsync(new TableEntity(token, labelRowId), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        await this.sources.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

        if (previousTokens is not null && desiredTokens is not null)
        {
            foreach (string token in previousTokens)
            {
                if (!desiredTokens.Contains(token))
                {
                    await this.labels.DeleteEntityAsync(token, labelRowId, ETag.All, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        return PersistedJson.ToPooledDocument<RegisteredSource>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string name, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceSerialization.EnsureEtag(name, expectedEtag, SourceSerialization.EtagOf(existing));
        }

        // The label entries derive from the CURRENT doc tags (a re-tag re-pointed them in step), read before the row
        // goes, and dropped only after it — the §14.4 ordering: an interrupted delete leaves a stale entry, harmless
        // when nothing loads behind it, whereas dropping entries first would strand a still-visible row.
        HashSet<string> tokens;
        using (ParsedJsonDocument<RegisteredSource> current = PersistedJson.ToPooledDocument<RegisteredSource>(existing))
        {
            tokens = AzureStorageSecurityLabelIndex.TokensFor(SecurityTagSet.CopyFrom(current.RootElement.ManagementTags));
        }

        string labelRowId = LabelRowId(name, discriminator!);
        await this.sources.DeleteEntityAsync(PartitionKey(name), RowKey(discriminator!), ETag.All, cancellationToken).ConfigureAwait(false);
        foreach (string token in tokens)
        {
            await this.labels.DeleteEntityAsync(token, labelRowId, ETag.All, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", System.Globalization.CultureInfo.InvariantCulture));

    // Orders a key against a decoded keyset cursor in the contractual total order: (name, discriminator) ordinal.
    private static int Compare(in EntityKey key, in (string Name, string TieBreaker) cursor)
    {
        int byName = string.CompareOrdinal(key.Name, cursor.Name);
        return byName != 0 ? byName : string.CompareOrdinal(key.Discriminator, cursor.TieBreaker);
    }

    // The PartitionKey is the name; the RowKey is the tag discriminator. Both are user-supplied/derived strings that may
    // contain Table-forbidden characters (/\#? and control chars — the discriminator carries a U+0001 tag-set
    // separator), so each is URL-safe-base64 encoded.
    private static string PartitionKey(string name) => Enc(name);

    private static string RowKey(string discriminator) => Enc(discriminator);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and
    // those two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the
    // empty string (an empty tag discriminator), which Table storage forbids as a key.
    private static string Enc(string value)
        => "~" + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The label entry's row id: the entity's encoded (PartitionKey, RowKey) pair concatenated — Enc prefixes every
    // token with '~' and Base64Url never emits one, so the concatenation is self-delimiting and RowKey-safe.
    private static string LabelRowId(string name, string discriminator)
        => PartitionKey(name) + RowKey(discriminator);

    private static bool TryParseLabelRowId(string rowId, out (string Name, string Discriminator) parts)
    {
        parts = default;
        int second = rowId.IndexOf('~', 1);
        if (rowId.Length == 0 || rowId[0] != '~' || second < 0)
        {
            return false;
        }

        try
        {
            parts = (
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowId.AsSpan(1, second - 1))),
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowId.AsSpan(second + 1))));
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Resolves the reach to the candidate label row ids the label table admits (§14.4). Null means "the index could
    // not narrow this rule", which is a legitimate answer — the exact evaluation downstream still decides what the
    // principal sees, so an un-narrowable rule costs throughput and never widens reach.
    private ValueTask<IReadOnlySet<string>?> ResolveReachCandidatesAsync(SecurityFilter? security, CancellationToken cancellationToken)
        => security is null
            ? new ValueTask<IReadOnlySet<string>?>((IReadOnlySet<string>?)null)
            : SecurityLabelQueryResolver.ResolveAsync(
                security.ToPredicate(SecurityLabelQueryEmitter.Instance), this.labelIndex, cancellationToken);

    // The entity's document bytes, or null when the entity is gone (a stale label entry, or a concurrent delete).
    private async ValueTask<byte[]?> TryGetDocAsync(string name, string discriminator, CancellationToken cancellationToken)
    {
        try
        {
            TableEntity entity = (await this.sources.GetEntityAsync<TableEntity>(
                PartitionKey(name), RowKey(discriminator), [DocColumn], cancellationToken).ConfigureAwait(false)).Value;
            return entity.GetBinary(DocColumn);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Finds the single source named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row-key seed). A source outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Discriminator)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(name)}");
        await foreach (TableEntity entity in this.sources.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            using ParsedJsonDocument<RegisteredSource> candidate = PersistedJson.ToPooledDocument<RegisteredSource>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, entity.GetString(DiscriminatorColumn));
            }
        }

        return (null, null);
    }

    // The decoded entity key columns (the plain name/discriminator, not the base64 PartitionKey/RowKey), carried so the
    // listing snapshot can be put into the contractual total order without re-decoding the keys.
    private readonly record struct EntityKey(string Name, string Discriminator);
}