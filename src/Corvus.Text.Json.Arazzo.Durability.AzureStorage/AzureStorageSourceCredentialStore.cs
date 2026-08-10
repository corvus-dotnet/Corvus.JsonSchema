// <copyright file="AzureStorageSourceCredentialStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Text;
using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="ISourceCredentialStore"/> (design §13): source credential bindings —
/// references and non-sensitive metadata only, never secret material — persisted as Table entities. Each binding is one
/// entity holding its <see cref="SourceCredentialBinding"/> document in a binary <c>Doc</c> property, with PartitionKey
/// = the (encoded) source name and RowKey = the (encoded) environment and tag discriminator, so every binding for a
/// (sourceName, environment) — and every binding for a sourceName — is a single efficient partition query. Its etag
/// travels inside the document (independent of the Table entity ETag), so optimistic concurrency is a read-compare-write.
/// Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// Point management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) and the usage
/// path by label-superset — applied in memory over the small candidate set for a (sourceName, environment), since a
/// deployment keeps those reach-disjoint. List/count narrow through the store's §14.4 label table first — the reach
/// resolves to candidate entity keys by indexed partition lookups — so only candidate rows are ever addressed, and the
/// exact reach then decides each one; a re-tagging update re-points the label entries around the entity write in the
/// §14.4 ordering. Table queries are unordered, so <see cref="ListAsync"/> sorts its snapshot client-side by
/// (sourceName, environment) to match every other backend's ordering. Tag round-tripping is Corvus.Text.Json end to
/// end (no System.Text.Json): the binding bytes are stored and read verbatim and the discriminator that keys the entity
/// is the canonical tag string from <see cref="SourceCredentialKey"/>.
/// </remarks>
public sealed class AzureStorageSourceCredentialStore : ISourceCredentialStore
{
    private const string CredentialsTable = "arazzoSourceCredentials";
    private const string LabelsTable = "arazzoSourceCredentialLabels";
    private const string DocColumn = "Doc";
    private const string SourceNameColumn = "SourceName";
    private const string EnvironmentColumn = "Environment";
    private const string DiscriminatorColumn = "Tags";

    private readonly TableClient credentials;
    private readonly TableClient labels;
    private readonly AzureStorageSecurityLabelIndex labelIndex;
    private readonly TimeProvider timeProvider;

    private AzureStorageSourceCredentialStore(TableClient credentials, TableClient labels, TimeProvider timeProvider)
    {
        this.credentials = credentials;
        this.labels = labels;
        this.labelIndex = new AzureStorageSecurityLabelIndex(labels);
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the source-credentials table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the source-credentials table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(CredentialsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(LabelsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSourceCredentialStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageSourceCredentialStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageSourceCredentialStore>(
            new AzureStorageSourceCredentialStore(
                tableService.GetTableClient(CredentialsTable),
                tableService.GetTableClient(LabelsTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>> AddAsync(SourceCredentialBinding draft, string actor, CancellationToken cancellationToken)
    {
        SourceCredentialBinding.ValidateDraft(draft);
        ArgumentNullException.ThrowIfNull(actor);
        string id = "scred-" + Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture);
        WorkflowEtag etag = NewEtag();
        byte[] json = SourceCredentialSerialization.SerializeNew(id, draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.Discriminator(draft.ManagementTagsValue, draft.UsageTagsValue);
        var entity = new TableEntity(PartitionKey(draft.SourceNameValue), RowKey(draft.EnvironmentValue, discriminator))
        {
            [SourceNameColumn] = draft.SourceNameValue,
            [EnvironmentColumn] = draft.EnvironmentValue,
            [DiscriminatorColumn] = discriminator,
            [DocColumn] = json,
        };

        // §14.4 label entries: ADDED before the binding becomes visible, so an interrupted add can only leave an
        // entry pointing at a row that never landed — which the exact reach evaluation discards — never a visible
        // row with no entry, which would hide it from a narrowed list.
        string labelRowId = LabelRowId(draft.SourceNameValue, draft.EnvironmentValue, discriminator);
        foreach (string token in AzureStorageSecurityLabelIndex.TokensFor(draft.ManagementTagsValue))
        {
            await this.labels.UpsertEntityAsync(new TableEntity(token, labelRowId), TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }

        try
        {
            await this.credentials.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            ThrowHelper.ThrowSourceCredentialAlreadyExists(draft.SourceNameValue, draft.EnvironmentValue);
        }

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> GetAsync(string sourceName, string environment, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
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

        // The contractual order is (sourceName, environment) with the tag discriminator as the tie-breaker for a stable
        // TOTAL order. Table storage orders by (PartitionKey, RowKey) = (Enc(source), Enc(env)_Enc(disc)), but Enc is
        // URL-safe base64 and therefore NOT ordinal-order-preserving, so the keyset cannot be pushed as a server-side
        // range filter. Instead the entity keys (decoded into the plain SourceName/Environment/Tags columns) are pulled,
        // sorted in memory into the total order, paged, and only the page's Documents are fetched.
        // §14.4: narrow to the candidate entity keys the label table admits before reading anything. A null answer
        // means the index could not narrow and the keys-only sweep runs as it always did; an empty one means no
        // binding qualifies, so the page is empty without a single row addressed. The exact reach below still
        // decides every candidate (the plan is a sound over-approximation), so an imprecise plan costs throughput
        // and can never widen reach.
        IReadOnlySet<string>? candidates = await this.ResolveReachCandidatesAsync(context.Reach(AccessVerb.Read), cancellationToken).ConfigureAwait(false);
        if (candidates is { Count: 0 })
        {
            return SourceCredentialPage.Create(PooledDocumentList<SourceCredentialBinding>.Empty);
        }

        var keys = new List<EntityKey>();
        if (candidates is null)
        {
            await foreach (TableEntity entity in this.credentials.QueryAsync<TableEntity>(
                select: [SourceNameColumn, EnvironmentColumn, DiscriminatorColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
            {
                if (entity.GetString(SourceNameColumn) is not { } source ||
                    entity.GetString(EnvironmentColumn) is not { } environment ||
                    entity.GetString(DiscriminatorColumn) is not { } discriminator)
                {
                    continue;
                }

                keys.Add(new EntityKey(source, environment, discriminator));
            }
        }
        else
        {
            foreach (string rowId in candidates)
            {
                if (TryParseLabelRowId(rowId, out (string SourceName, string Environment, string Discriminator) parts))
                {
                    keys.Add(new EntityKey(parts.SourceName, parts.Environment, parts.Discriminator));
                }
            }
        }

        keys.Sort(static (a, b) =>
        {
            int bySource = string.CompareOrdinal(a.SourceName, b.SourceName);
            if (bySource != 0)
            {
                return bySource;
            }

            int byEnv = string.CompareOrdinal(a.Environment, b.Environment);
            return byEnv != 0 ? byEnv : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

        var docs = new PooledDocumentList<SourceCredentialBinding>(pageSize);
        bool hasMore = false;
        try
        {
            EntityKey last = default;
            foreach (EntityKey key in keys)
            {
                // Skip entities at or before the cursor in (source, env, disc) total order.
                if (hasCursor && Compare(key, cursor) <= 0)
                {
                    continue;
                }

                // Fetch the Document only now, for entities past the cursor, and only until the page fills plus one;
                // a stale label entry resolving to a deleted entity falls into the same absent skip.
                if (await this.TryGetDocAsync(key.SourceName, key.Environment, key.Discriminator, cancellationToken).ConfigureAwait(false) is not { } json)
                {
                    continue;
                }

                ParsedJsonDocument<SourceCredentialBinding> cand = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
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
                ? SourceCredentialPage.Create(docs, last.SourceName, last.Environment, last.Discriminator)
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
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = SourceCredentialSerialization.SerializeUpdated(existing, $"{sourceName}@{environment}", expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(PartitionKey(sourceName), RowKey(environment, discriminator!))
        {
            [SourceNameColumn] = sourceName,
            [EnvironmentColumn] = environment,
            [DiscriminatorColumn] = discriminator!,
            [DocColumn] = json,
        };

        // The row is addressed by its frozen create-time keys (ADR 0067; the usage tags stay immutable too). A draft
        // that supplies management tags re-tags the row's reach scope (a store-level replace; an omitted set is
        // carried forward), so the §14.4 label entries are re-pointed by the diff around the entity write, in the
        // §14.4 ordering — entries for the NEW tags are added before the entity carries them and the removed ones
        // are dropped only after it stops, so an interruption can only leave a stale entry (discarded by the exact
        // evaluation), never a hidden row.
        HashSet<string>? previousTokens = null;
        HashSet<string>? desiredTokens = null;
        string labelRowId = LabelRowId(sourceName, environment, discriminator!);
        if (!draft.ManagementTagsValue.IsEmpty)
        {
            SecurityTagSet previousTags;
            using (ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(existing))
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

        await this.credentials.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);

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

        return PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<bool> DeleteAsync(string sourceName, string environment, WorkflowEtag expectedEtag, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(sourceName, environment, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return false;
        }

        if (!expectedEtag.IsNone)
        {
            SourceCredentialSerialization.EnsureEtag($"{sourceName}@{environment}", expectedEtag, SourceCredentialSerialization.EtagOf(existing));
        }

        // The label entries derive from the CURRENT doc tags (a re-tag re-pointed them in step), read before the row
        // goes, and dropped only after it — the §14.4 ordering: an interrupted delete leaves a stale entry, harmless
        // when nothing loads behind it, whereas dropping entries first would strand a still-visible row.
        HashSet<string> tokens;
        using (ParsedJsonDocument<SourceCredentialBinding> current = PersistedJson.ToPooledDocument<SourceCredentialBinding>(existing))
        {
            tokens = AzureStorageSecurityLabelIndex.TokensFor(SecurityTagSet.CopyFrom(current.RootElement.ManagementTags));
        }

        string labelRowId = LabelRowId(sourceName, environment, discriminator!);
        await this.credentials.DeleteEntityAsync(PartitionKey(sourceName), RowKey(environment, discriminator!), ETag.All, cancellationToken).ConfigureAwait(false);
        foreach (string token in tokens)
        {
            await this.labels.DeleteEntityAsync(token, labelRowId, ETag.All, cancellationToken).ConfigureAwait(false);
        }

        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<SourceCredentialBinding>?> ResolveForUsageAsync(string sourceName, string environment, SecurityTagSet runTags, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sourceName);
        ArgumentNullException.ThrowIfNull(environment);
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(sourceName)} and Environment eq {environment}");
        await foreach (TableEntity entity in this.credentials.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
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
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(sourceName)}");
        bool any = false;
        await foreach (TableEntity entity in this.credentials.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            any = true;
            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (candidate.RootElement.IsUsableBy(tags))
            {
                return CredentialSourceAccess.Granted;
            }
        }

        return any ? CredentialSourceAccess.Denied : CredentialSourceAccess.Unconfigured;
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Orders a key against a decoded keyset cursor in the contractual total order: (source, env, discriminator) ordinal.
    private static int Compare(in EntityKey key, in (string SourceName, string Environment, string TieBreaker) cursor)
    {
        int bySource = string.CompareOrdinal(key.SourceName, cursor.SourceName);
        if (bySource != 0)
        {
            return bySource;
        }

        int byEnv = string.CompareOrdinal(key.Environment, cursor.Environment);
        return byEnv != 0 ? byEnv : string.CompareOrdinal(key.Discriminator, cursor.TieBreaker);
    }

    // The PartitionKey is the source name; the RowKey folds environment and the tag discriminator. Both are
    // user-supplied/derived strings that may contain Table-forbidden characters (/\#? and control chars — the
    // discriminator carries a U+0001 tag-set separator), so each segment is URL-safe-base64 encoded; the encoded
    // forms are joined with '_' (outside the base64 alphabet) so the RowKey decomposition is unambiguous.
    private static string PartitionKey(string sourceName) => Enc(sourceName);

    private static string RowKey(string environment, string discriminator) => $"{Enc(environment)}_{Enc(discriminator)}";

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and
    // those two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the
    // empty string (an empty tag discriminator), which Table storage forbids as a key.
    private static string Enc(string value)
        => "~" + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(value));

    // The label entry's row id: the identity's three encoded tokens concatenated — Enc prefixes every token with
    // '~' and Base64Url never emits one, so the concatenation is self-delimiting and RowKey-safe.
    private static string LabelRowId(string sourceName, string environment, string discriminator)
        => Enc(sourceName) + Enc(environment) + Enc(discriminator);

    private static bool TryParseLabelRowId(string rowId, out (string SourceName, string Environment, string Discriminator) parts)
    {
        parts = default;
        int second = rowId.IndexOf('~', 1);
        int third = second < 0 ? -1 : rowId.IndexOf('~', second + 1);
        if (rowId.Length == 0 || rowId[0] != '~' || second < 0 || third < 0)
        {
            return false;
        }

        try
        {
            parts = (
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowId.AsSpan(1, second - 1))),
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowId.AsSpan(second + 1, third - second - 1))),
                Encoding.UTF8.GetString(Base64Url.DecodeFromChars(rowId.AsSpan(third + 1))));
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
    private async ValueTask<byte[]?> TryGetDocAsync(string sourceName, string environment, string discriminator, CancellationToken cancellationToken)
    {
        try
        {
            TableEntity entity = (await this.credentials.GetEntityAsync<TableEntity>(
                PartitionKey(sourceName), RowKey(environment, discriminator), [DocColumn], cancellationToken).ConfigureAwait(false)).Value;
            return entity.GetBinary(DocColumn);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    // Finds the single binding for (sourceName, environment) the caller's reach for the verb admits, returning its
    // bytes and its tag discriminator (the row-key segment). A binding outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Discriminator)> FindForManagementAsync(string sourceName, string environment, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(sourceName)} and Environment eq {environment}");
        await foreach (TableEntity entity in this.credentials.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            using ParsedJsonDocument<SourceCredentialBinding> candidate = PersistedJson.ToPooledDocument<SourceCredentialBinding>(json);
            if (context.Admits(verb, candidate.RootElement.ManagementTagsValue))
            {
                return (json, entity.GetString(DiscriminatorColumn));
            }
        }

        return (null, null);
    }

    // The decoded entity key columns (the plain source/environment/discriminator, not the base64 PartitionKey/RowKey),
    // carried so the listing snapshot can be put into the contractual total order without re-decoding the keys.
    private readonly record struct EntityKey(string SourceName, string Environment, string Discriminator);
}