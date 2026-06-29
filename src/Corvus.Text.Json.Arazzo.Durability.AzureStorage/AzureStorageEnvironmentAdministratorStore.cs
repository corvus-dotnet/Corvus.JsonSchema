// <copyright file="AzureStorageEnvironmentAdministratorStore.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Azure;
using Azure.Data.Tables;
using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability.Security;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An Azure Table Storage-backed <see cref="IEnvironmentAdministratorStore"/> (design §7.7): the explicit administration
/// record for a deployment environment — the mutable set of administrator identities entitled to govern the environment.
/// Each record is one Table entity holding its <see cref="EnvironmentAdministrators"/> document in a binary
/// <c>Document</c> property, keyed solely by the (encoded) environment name, with a constant PartitionKey so every record
/// lives in a single partition. Its etag travels inside the document (independent of the Table entity ETag), so
/// optimistic concurrency is a read-compare-write. The record holds deployment-stamped identities only — never secret
/// material. Mirrors <see cref="AzureStorageWorkflowAdministratorStore"/>, including the reverse administration index.
/// Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// The store takes no <see cref="AccessContext"/>: it is a CAS key/value persistence seam (like the security-policy and
/// source-credential stores), with authorization the governing service's concern. The <see cref="PutAsync"/>
/// create-or-replace reads the current document and compares its (in-document) etag before writing, mirroring every other
/// backend; a mismatch — or a present-vs-expected-absent record (and vice versa) — surfaces as
/// <see cref="EnvironmentAdministrationConflictException"/>. Tag round-tripping is Corvus.Text.Json end to end
/// (no System.Text.Json): the record bytes are stored and read back verbatim.
/// </remarks>
public sealed class AzureStorageEnvironmentAdministratorStore : IEnvironmentAdministratorStore
{
    private const string AdministratorsTable = "arazzoEnvironmentAdministrators";

    // The reverse administration index (design §7.7): a separate table whose PartitionKey is an administrator digest and
    // whose RowKey is the encoded environment name, with the plain environment name in a column so a digest's administered
    // environments are a single-partition query (no RowKey decode). Azure Table has no server-side ORDER BY, so the
    // (bounded) partition is ordered client-side — mirroring the access-request store's keyset approach.
    private const string IndexTable = "arazzoEnvironmentAdministratorIndex";
    private const string DocumentColumn = "Document";
    private const string EtagColumn = "Etag";
    private const string EnvironmentNameColumn = "EnvironmentName";

    // A single logical entity per environment name: the partition is constant (one partition for the table) and the
    // row is the encoded environment name, so a record is a point read by (PartitionKey, RowKey).
    private const string AdministratorsPartition = "admin";

    private readonly TableClient administrators;
    private readonly TableClient index;
    private readonly TimeProvider timeProvider;

    private AzureStorageEnvironmentAdministratorStore(TableClient administrators, TableClient index, TimeProvider timeProvider)
    {
        this.administrators = administrators;
        this.index = index;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the environment-administrators table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the environment-administrators table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(AdministratorsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
        await tableService.GetTableClient(IndexTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageEnvironmentAdministratorStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageEnvironmentAdministratorStore>(
            new AzureStorageEnvironmentAdministratorStore(
                tableService.GetTableClient(AdministratorsTable),
                tableService.GetTableClient(IndexTable),
                timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>?> GetAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        byte[]? json = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
        return json is null ? null : ParsedJsonDocument<EnvironmentAdministrators>.Parse(json.AsMemory());
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<EnvironmentAdministrators>> PutAsync(string environmentName, IReadOnlyList<EnvironmentAdministrators.AdministratorIdentity> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("An environment administration record requires at least one administrator identity.", nameof(administrators));
        }

        byte[]? existing = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
        WorkflowEtag etag = NewEtag();
        byte[] json;

        // The environment's previous administrator digests (so stale reverse-index rows can be retracted): read from the
        // record on disk, since Table storage has no atomic server-side script holding them. Empty for a fresh record.
        IReadOnlyList<string> oldDigests;
        if (existing is not null)
        {
            // Parse the existing record ONCE, NON-COPYING over the driver's array (the read leaf) — used for both the etag
            // check and the carried-forward merge. Azure Table stores the document in a binary column, so the write stays a
            // byte[] leaf; the column etag is the one we just generated.
            using ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory());

            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != current.RootElement.EtagValue)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            oldDigests = EnvironmentAdministeredPaging.DistinctDigests(current.RootElement);
            json = EnvironmentAdministratorsSerialization.SerializeUpdated(current.RootElement, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new EnvironmentAdministrationConflictException(environmentName, expectedEtag);
            }

            oldDigests = [];
            json = EnvironmentAdministratorsSerialization.SerializeNew(environmentName, administrators, actor, this.timeProvider.GetUtcNow(), etag);
        }

        var entity = new TableEntity(AdministratorsPartition, RowKey(environmentName))
        {
            [EtagColumn] = etag.Value!,
            [DocumentColumn] = json,
        };
        await this.administrators.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        await this.ReindexAsync(environmentName, oldDigests, EnvironmentAdministeredPaging.DistinctDigests(administrators), cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<EnvironmentAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<EnvironmentAdministeredPage> ListAdministeredAsync(string adminDigest, int limit, JsonString pageToken, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(adminDigest);
        int pageSize = limit > 0 ? limit : EnvironmentAdministeredPage.DefaultPageSize;
        string? after = EnvironmentAdministeredContinuationToken.DecodeCursorToString(pageToken);

        // The digest's partition (the environments it administers) is bounded; fetch it projecting just the plain
        // environment name, order client-side (ordinal — the contract's order), then apply the keyset cursor + page (Azure
        // has no ORDER BY).
        var names = new List<string>();
        await foreach (TableEntity entity in this.index.QueryAsync<TableEntity>(
            e => e.PartitionKey == adminDigest, select: [EnvironmentNameColumn], cancellationToken: cancellationToken).ConfigureAwait(false))
        {
            if (entity.GetString(EnvironmentNameColumn) is { } environmentName)
            {
                names.Add(environmentName);
            }
        }

        names.Sort(StringComparer.Ordinal);

        var rows = new List<string>(Math.Min(pageSize + 1, names.Count));
        foreach (string name in names)
        {
            if (after is not null && string.CompareOrdinal(name, after) <= 0)
            {
                continue; // at or before the cursor — already returned on an earlier page
            }

            rows.Add(name);
            if (rows.Count > pageSize)
            {
                break;
            }
        }

        return EnvironmentAdministeredPaging.ToPage(rows, pageSize);
    }

    /// <inheritdoc/>
    public async ValueTask DeleteAsync(string environmentName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(environmentName);

        // Read the record first to learn which administrator digests reference it (Azurite does not 404 on deleting a
        // missing entity, so we cannot rely on the forward delete to tell us the record was absent). A missing record is a
        // no-op for both the forward row and the reverse index.
        byte[]? existing = await this.ReadDocumentAsync(environmentName, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return;
        }

        // Retract this environment's reverse-index rows (no surviving digests), mirroring PutAsync's retract-before-reindex
        // logic with an empty new-digest set, then drop the forward record.
        IReadOnlyList<string> oldDigests;
        using (ParsedJsonDocument<EnvironmentAdministrators> current = ParsedJsonDocument<EnvironmentAdministrators>.Parse(existing.AsMemory()))
        {
            oldDigests = EnvironmentAdministeredPaging.DistinctDigests(current.RootElement);
        }

        await this.ReindexAsync(environmentName, oldDigests, [], cancellationToken).ConfigureAwait(false);

        try
        {
            await this.administrators.DeleteEntityAsync(AdministratorsPartition, RowKey(environmentName), ETag.All, cancellationToken).ConfigureAwait(false);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Already gone — a missing record is a no-op.
        }
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // Reconciles an environment's reverse-index rows (§7.7): delete the rows for digests it no longer administers, then
    // (idempotently) upsert a row for each current digest. Not atomic with the document write (Table storage spans
    // partitions here), but a stale row only ever over-reports until the next write, and the work is bounded by the
    // small administrator set.
    private async ValueTask ReindexAsync(string environmentName, IReadOnlyList<string> oldDigests, IReadOnlyList<string> newDigests, CancellationToken cancellationToken)
    {
        string rowKey = RowKey(environmentName);
        foreach (string digest in oldDigests)
        {
            if (!newDigests.Contains(digest))
            {
                try
                {
                    await this.index.DeleteEntityAsync(digest, rowKey, ETag.All, cancellationToken).ConfigureAwait(false);
                }
                catch (RequestFailedException ex) when (ex.Status == 404)
                {
                    // Already gone — the reverse index is idempotent.
                }
            }
        }

        foreach (string digest in newDigests)
        {
            var entity = new TableEntity(digest, rowKey)
            {
                [EnvironmentNameColumn] = environmentName,
            };
            await this.index.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        }
    }

    // The RowKey is the environment name, which may contain Table-forbidden characters (/\#? and control chars), so it
    // is URL-safe-base64 encoded; the encoded form is always a permitted Table key (see Enc).
    private static string RowKey(string environmentName) => Enc(environmentName);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and those
    // two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the empty
    // string, which Table storage forbids as a key.
    private static string Enc(string value)
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // The record is a point read by (PartitionKey, RowKey); a missing entity is reported by the 404 status code rather
    // than by a thrown exception leaking out (NoThrow), and surfaces as a null document to the caller.
    private async ValueTask<byte[]?> ReadDocumentAsync(string environmentName, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.administrators
            .GetEntityIfExistsAsync<TableEntity>(AdministratorsPartition, RowKey(environmentName), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}