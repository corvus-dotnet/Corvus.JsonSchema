// <copyright file="AzureBlobAuditSink.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage;

/// <summary>
/// An audit sink over an Azure Storage blob container (ADR 0069): each chain is one append blob,
/// <c>{writerId}/{chainId}.jsonl</c>, and each record is one appended block. The container is the deployment's evidence store,
/// outside the operational database, and it is meant to be immutable: under a time-based retention policy that allows
/// protected append writes, or under a legal hold, an appended block cannot be altered or removed, by this process or
/// by whoever holds the storage account.
/// </summary>
/// <remarks>
/// <para>
/// The sink checks that when it connects. A container with neither an immutability policy nor a legal hold is refused,
/// because an audit store that can be rewritten is the control ADR 0069 exists to replace, and a control that is
/// declared and not enforced is what stops anyone checking it. Development and the storage emulator, which has no
/// immutability, opt out explicitly with <c>allowMutableContainer</c>.
/// </para>
/// <para>
/// The caller owns the container client, and with it the account, the credential and the retention policy. The retention
/// period is the audit's retention: the platform adds none of its own.
/// </para>
/// <para>
/// An append blob holds 50,000 blocks. The chain writer opens a new chain at 40,000 records by default, heads included
/// in what follows, so a chain never reaches the limit.
/// </para>
/// </remarks>
public sealed class AzureBlobAuditSink : IAuditSink
{
    /// <summary>The file extension of a chain blob, with its dot.</summary>
    public const string ChainBlobExtension = ".jsonl";

    private readonly BlobContainerClient container;

    private AzureBlobAuditSink(BlobContainerClient container) => this.container = container;

    /// <summary>Connects the sink to its container, checking that the container is immutable.</summary>
    /// <param name="container">The container the chains are appended to. It must exist: a deployment creates it, with its retention policy, before anything is recorded.</param>
    /// <param name="allowMutableContainer"><see langword="true"/> to accept a container with neither an immutability policy nor a legal hold. For development and the storage emulator only: what such a container keeps can be rewritten, so it is not evidence.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The sink.</returns>
    /// <exception cref="InvalidOperationException">The container is mutable and <paramref name="allowMutableContainer"/> is <see langword="false"/>.</exception>
    public static async ValueTask<AzureBlobAuditSink> ConnectAsync(BlobContainerClient container, bool allowMutableContainer = false, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(container);

        if (!allowMutableContainer)
        {
            BlobContainerProperties properties = await container.GetPropertiesAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            if (properties.HasImmutabilityPolicy != true && properties.HasLegalHold != true)
            {
                ThrowHelper.ThrowAuditContainerIsMutable(container.Name);
            }
        }

        return new AzureBlobAuditSink(container);
    }

    /// <inheritdoc/>
    public async ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
    {
        // The blob name is a string-typed sink (the storage SDK), once per chain.
        AppendBlobClient blob = this.container.GetAppendBlobClient(Encoding.UTF8.GetString(writerId.Span) + "/" + Encoding.UTF8.GetString(chainId.Span) + ChainBlobExtension);

        // If-None-Match: * makes the create fail where the blob exists, so a chain is never reopened and never replaced.
        await blob.CreateAsync(
            new AppendBlobCreateOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/jsonl" },
                Conditions = new AppendBlobRequestConditions { IfNoneMatch = ETag.All },
            },
            cancellationToken).ConfigureAwait(false);
        return new ChainBlob(blob);
    }

    /// <inheritdoc/>
    public async ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken)
    {
        // A chain's id is a version 7 UUID, so the last chain a writer opened is the last by name under its prefix.
        string? last = null;
        await foreach (BlobItem blob in this.container.GetBlobsAsync(BlobTraits.None, BlobStates.None, Encoding.UTF8.GetString(writerId.Span) + "/", cancellationToken).ConfigureAwait(false))
        {
            if (blob.Name.EndsWith(ChainBlobExtension, StringComparison.Ordinal) && (last is null || string.CompareOrdinal(blob.Name, last) > 0))
            {
                last = blob.Name;
            }
        }

        return last is null ? null : await this.container.GetBlobClient(last).OpenReadAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private sealed class ChainBlob(AppendBlobClient blob) : IAuditChainStream
    {
        public async ValueTask AppendAsync(ReadOnlyMemory<byte> line, CancellationToken cancellationToken)
        {
            using ReadOnlyMemoryStream content = ReadOnlyMemoryStream.Rent(line);
            await blob.AppendBlockAsync(content, cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}