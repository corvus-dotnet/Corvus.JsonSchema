// <copyright file="AzureStorageWorkflowAdministratorStore.cs" company="Endjin Limited">
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
/// An Azure Table Storage-backed <see cref="IWorkflowAdministratorStore"/> (design §15): the explicit administration
/// record for a base workflow id — the mutable set of administrator identities entitled to publish further versions and
/// to manage administration. Each record is one Table entity holding its <see cref="WorkflowAdministrators"/> document
/// in a binary <c>Document</c> property, keyed solely by the (encoded) base workflow id, with a constant PartitionKey so
/// every record lives in a single partition. Its etag travels inside the document (independent of the Table entity
/// ETag), so optimistic concurrency is a read-compare-write. The record holds deployment-stamped identities only — never
/// secret material. Works against Azure Storage and the Azurite emulator.
/// </summary>
/// <remarks>
/// The store takes no <see cref="AccessContext"/>: it is a CAS key/value persistence seam (like the security-policy and
/// source-credential stores), with authorization the <see cref="WorkflowCatalogClient"/>'s concern. The
/// <see cref="PutAsync"/> create-or-replace reads the current document and compares its (in-document) etag before
/// writing, mirroring every other backend; a mismatch — or a present-vs-expected-absent record (and vice versa) —
/// surfaces as <see cref="WorkflowAdministrationConflictException"/>. Tag round-tripping is Corvus.Text.Json end to end
/// (no System.Text.Json): the record bytes are stored and read back verbatim.
/// </remarks>
public sealed class AzureStorageWorkflowAdministratorStore : IWorkflowAdministratorStore
{
    private const string AdministratorsTable = "arazzoWorkflowAdministrators";
    private const string DocumentColumn = "Document";
    private const string EtagColumn = "Etag";

    // A single logical entity per base workflow id: the partition is constant (one partition for the table) and the
    // row is the encoded base id, so a record is a point read by (PartitionKey, RowKey).
    private const string AdministratorsPartition = "admin";

    private readonly TableClient administrators;
    private readonly TimeProvider timeProvider;

    private AzureStorageWorkflowAdministratorStore(TableClient administrators, TimeProvider timeProvider)
    {
        this.administrators = administrators;
        this.timeProvider = timeProvider;
    }

    /// <summary>Provisions the workflow-administrators table over the given connection string.</summary>
    /// <param name="connectionString">An Azure Storage connection string for a credential permitted to create tables.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static ValueTask PrepareAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return PrepareAsync(new TableServiceClient(connectionString), cancellationToken);
    }

    /// <summary>Provisions the workflow-administrators table over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client (for example one built with a managed identity).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that completes once the table exists (idempotent).</returns>
    public static async ValueTask PrepareAsync(TableServiceClient tableService, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        await tableService.GetTableClient(AdministratorsTable).CreateIfNotExistsAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Opens the store for operation against an already-provisioned table.</summary>
    /// <param name="connectionString">An Azure Storage connection string (or the Azurite emulator's).</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowAdministratorStore> ConnectAsync(string connectionString, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        return ConnectAsync(new TableServiceClient(connectionString), timeProvider, cancellationToken);
    }

    /// <summary>Opens the store for operation over a caller-supplied service client.</summary>
    /// <param name="tableService">A table service client.</param>
    /// <param name="timeProvider">The time source for audit timestamps; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened store.</returns>
    public static ValueTask<AzureStorageWorkflowAdministratorStore> ConnectAsync(TableServiceClient tableService, TimeProvider? timeProvider = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableService);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<AzureStorageWorkflowAdministratorStore>(
            new AzureStorageWorkflowAdministratorStore(tableService.GetTableClient(AdministratorsTable), timeProvider ?? TimeProvider.System));
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>?> GetAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        byte[]? json = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        return json is null ? null : PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    /// <inheritdoc/>
    public async ValueTask<ParsedJsonDocument<WorkflowAdministrators>> PutAsync(string baseWorkflowId, IReadOnlyList<SecurityTagSet> administrators, WorkflowEtag expectedEtag, string actor, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentNullException.ThrowIfNull(administrators);
        ArgumentNullException.ThrowIfNull(actor);
        if (administrators.Count == 0)
        {
            throw new ArgumentException("A workflow administration record requires at least one administrator identity.", nameof(administrators));
        }

        byte[]? existing = await this.ReadDocumentAsync(baseWorkflowId, cancellationToken).ConfigureAwait(false);
        byte[] json;
        if (existing is not null)
        {
            // A record exists: the caller must hold its current etag (None means "I expected no record").
            if (expectedEtag.IsNone || expectedEtag != WorkflowAdministratorsSerialization.EtagOf(existing))
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeUpdated(existing, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
        }
        else
        {
            // No record yet: materialization is only valid against the None etag (the v1-derived default).
            if (!expectedEtag.IsNone)
            {
                throw new WorkflowAdministrationConflictException(baseWorkflowId, expectedEtag);
            }

            json = WorkflowAdministratorsSerialization.SerializeNew(baseWorkflowId, administrators, actor, this.timeProvider.GetUtcNow(), NewEtag());
        }

        var entity = new TableEntity(AdministratorsPartition, RowKey(baseWorkflowId))
        {
            [EtagColumn] = WorkflowAdministratorsSerialization.EtagOf(json).Value!,
            [DocumentColumn] = json,
        };
        await this.administrators.UpsertEntityAsync(entity, TableUpdateMode.Replace, cancellationToken).ConfigureAwait(false);
        return PersistedJson.ToPooledDocument<WorkflowAdministrators>(json);
    }

    private static WorkflowEtag NewEtag() => new(Guid.NewGuid().ToString("n", CultureInfo.InvariantCulture));

    // The RowKey is the base workflow id, which may contain Table-forbidden characters (/\#? and control chars), so it
    // is URL-safe-base64 encoded; the encoded form is always a permitted Table key (see Enc).
    private static string RowKey(string baseWorkflowId) => Enc(baseWorkflowId);

    // URL-safe base64 of the UTF-8 bytes (forbidden / and + remapped to _ and -). The base64 alphabet plus '=' and those
    // two replacements are all permitted in a Table key. A leading '~' guarantees a non-empty key even for the empty
    // string, which Table storage forbids as a key.
    private static string Enc(string value)
        => "~" + Convert.ToBase64String(Encoding.UTF8.GetBytes(value)).Replace('/', '_').Replace('+', '-');

    // The record is a point read by (PartitionKey, RowKey); a missing entity is reported by the 404 status code rather
    // than by a thrown exception leaking out (NoThrow), and surfaces as a null document to the caller.
    private async ValueTask<byte[]?> ReadDocumentAsync(string baseWorkflowId, CancellationToken cancellationToken)
    {
        NullableResponse<TableEntity> response = await this.administrators
            .GetEntityIfExistsAsync<TableEntity>(AdministratorsPartition, RowKey(baseWorkflowId), cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        return response.HasValue ? response.Value!.GetBinary(DocumentColumn) : null;
    }
}