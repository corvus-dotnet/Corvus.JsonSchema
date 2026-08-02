// <copyright file="AzureStorageEnvironmentStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Runtime.InteropServices;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Environment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IEnvironmentStore"/> (design §7.7): deployment environments persisted as
/// Table entities. Each environment is one entity holding its <see cref="Environment"/> document in a binary <c>Doc</c>
/// property, with PartitionKey = the (encoded) name and RowKey = the (encoded) tag discriminator, so every environment
/// for a name is a single efficient partition query. Its etag travels inside the document (independent of the Table
/// entity ETag), so optimistic concurrency is a read-compare-write. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// Management reads/writes are reach-filtered by the caller's <see cref="AccessContext"/> (§14.2) — applied in memory
/// over the small candidate set for a name, since a deployment keeps those reach-disjoint. Table queries are unordered,
/// so <see cref="ListAsync"/> sorts its snapshot client-side by (name, discriminator) to match every other backend's
/// ordering. Tag round-tripping is Corvus.Text.Json end to end (no System.Text.Json): the environment bytes are stored
/// and read verbatim and the discriminator that keys the entity is the canonical tag string from
/// <see cref="SourceCredentialKey"/>.
/// </remarks>
public sealed class AzureStorageEnvironmentStore : IEnvironmentStore
{
    private const string EnvironmentsTable = "arazzoEnvironments";

    // The tenancy ledger's own table (ADR 0065). Not a reserved partition in the environments table: the list path
    // queries that table for keys without a partition filter, so a ledger entity there would be a candidate row.
    private const string TenancyTable = "arazzoEnvironmentTenancy";
    private const string TenancyKey = "tenancy";
    private const string DocColumn = "Doc";
    private const string NameColumn = "Name";
    private const string DiscriminatorColumn = "Tags";

    private readonly TableClient environments;
    private readonly TableClient tenancy;
    private readonly TimeProvider timeProvider;

    private AzureStorageEnvironmentStore(TableClient environments, TableClient tenancy, TimeProvider timeProvider)
    {
        this.environments = environments;
        this.tenancy = tenancy;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the environments table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the environments table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(EnvironmentsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(TenancyTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageEnvironmentStore>(
            new AzureStorageEnvironmentStore(
                tableService.GetTableClient(EnvironmentsTable),
                tableService.GetTableClient(TenancyTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>> AddAsync(Environment draft, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        WorkflowEtag etag = NewEtag();
        byte[] json = EnvironmentSerialization.SerializeNew(draft, actor, this.timeProvider.GetUtcNow(), etag);
        string discriminator = SourceCredentialKey.CanonicalTags(draft.ManagementTagsValue);
        var entity = new TableEntity(PartitionKey(draft.NameValue), RowKey(discriminator))
        {
            [NameColumn] = draft.NameValue,
            [DiscriminatorColumn] = discriminator,
            [DocColumn] = json,
        };
        try
        {
            await this.environments.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 409)
        {
            ThrowHelper.ThrowEnvironmentAlreadyExists(draft.NameValue);
        }

        return PersistedJson.ToPooledDocument<Environment>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<Environment>?> GetAsync(string name, AccessContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(context);
        (byte[]? json, _) = await this.FindForManagementAsync(name, AccessVerb.Read, context, cancellationToken).ConfigureAwait(false);
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

        // The contractual order is name with the tag discriminator as the tie-breaker for a stable TOTAL order. Table
        // storage orders by (PartitionKey, RowKey) = (Enc(name), Enc(disc)), but Enc is URL-safe base64 and therefore
        // NOT ordinal-order-preserving, so the keyset cannot be pushed as a server-side range filter. Instead the entity
        // keys (decoded into the plain Name/Tags columns) are pulled, sorted in memory into the total order, paged, and
        // only the page's Documents are fetched.
        var keys = new List<EntityKey>();
        await foreach (TableEntity entity in this.environments.QueryAsync<TableEntity>(
            select: [NameColumn, DiscriminatorColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(NameColumn) is not { } name ||
                entity.GetString(DiscriminatorColumn) is not { } discriminator)
            {
                continue;
            }

            keys.Add(new EntityKey(name, discriminator));
        }

        keys.Sort(static (a, b) =>
        {
            int byName = string.CompareOrdinal(a.Name, b.Name);
            return byName != 0 ? byName : string.CompareOrdinal(a.Discriminator, b.Discriminator);
        });

        var docs = new PooledDocumentList<Environment>(pageSize);
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

                // Fetch the Document only now, for entities past the cursor, and only until the page fills plus one.
                TableEntity entity = (await this.environments.GetEntityAsync<TableEntity>(
                    PartitionKey(key.Name), RowKey(key.Discriminator), [DocColumn], cancellationToken).ConfigureAwait(false)).Value;
                if (entity.GetBinary(DocColumn) is not { } json)
                {
                    continue;
                }

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
                ? EnvironmentPage.Create(docs, last.Name, last.Discriminator)
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
        (byte[]? existing, string? discriminator) = await this.FindForManagementAsync(name, AccessVerb.Write, context, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return null;
        }

        byte[] json = EnvironmentSerialization.SerializeUpdated(existing, name, expectedEtag, draft, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(PartitionKey(name), RowKey(discriminator!))
        {
            [NameColumn] = name,
            [DiscriminatorColumn] = discriminator!,
            [DocColumn] = json,
        };
        await this.environments.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<Environment>(json);
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
            EnvironmentSerialization.EnsureEtag(name, expectedEtag, EnvironmentSerialization.EtagOf(existing));
        }

        await this.environments.DeleteEntityAsync(PartitionKey(name), RowKey(discriminator!), ETag.All, cancellationToken).ConfigureAwait(false);
        return true;
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<TenancyLedger>?> GetTenancyLedgerAsync(CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> row = await this.tenancy
            .GetEntityIfExistsAsync<TableEntity>(TenancyKey, TenancyKey, [DocColumn], cancellationToken).ConfigureAwait(false);
        return row.HasValue && row.Value!.GetBinary(DocColumn) is { } json
            ? PersistedJson.ToPooledDocument<TenancyLedger>(json)
            : null;
    }

    /// <inheritdoc/>
    public async ValueTask<bool> TryCommitTenancyLedgerAsync(TenancyLedger current, ReadOnlyMemory<byte> admitting, string actor, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(actor);
        byte[] json = TenancyLedgerSerialization.SerializeCommitted(current, admitting, actor, this.timeProvider.GetUtcNow(), NewEtag());
        var entity = new TableEntity(TenancyKey, TenancyKey) { [DocColumn] = json };
        if (current.IsUndefined())
        {
            // The single entity key makes "no row must exist" the table's own uniqueness: a lost race is a 409.
            try
            {
                await this.tenancy.AddEntityAsync(entity, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (RequestFailedException ex) when (ex.Status == 409)
            {
                return false;
            }
        }

        // Read for the native entity ETag, then update conditional on it. Both windows are covered: a writer landing
        // before the read is caught by the stored ledger no longer carrying the etag the caller decided against, and one
        // landing between the read and the update is caught by the If-Match (412).
        NullableResponse<TableEntity> stored = await this.tenancy
            .GetEntityIfExistsAsync<TableEntity>(TenancyKey, TenancyKey, cancellationToken: cancellationToken).ConfigureAwait(false);
        if (!stored.HasValue || stored.Value!.GetBinary(DocColumn) is not { } storedJson
            || !TenancyLedgerSerialization.CarriesEtagOf(storedJson, current))
        {
            return false;
        }

        try
        {
            await this.tenancy.UpdateEntityAsync(entity, stored.Value.ETag, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412)
        {
            return false;
        }
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
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // Finds the single environment named `name` the caller's reach for the verb admits, returning its bytes and its tag
    // discriminator (the row-key seed). An environment outside reach is invisible (non-disclosing).
    private async ValueTask<(byte[]? Json, string? Discriminator)> FindForManagementAsync(string name, AccessVerb verb, AccessContext context, CancellationToken cancellationToken)
    {
        string filter = TableClient.CreateQueryFilter($"PartitionKey eq {PartitionKey(name)}");
        await foreach (TableEntity entity in this.environments.QueryAsync<TableEntity>(filter, cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetBinary(DocColumn) is not { } json)
            {
                continue;
            }

            using ParsedJsonDocument<Environment> candidate = PersistedJson.ToPooledDocument<Environment>(json);
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