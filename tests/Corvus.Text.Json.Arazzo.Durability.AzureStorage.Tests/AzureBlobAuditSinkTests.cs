// <copyright file="AzureBlobAuditSinkTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Testcontainers.Azurite;

namespace Corvus.Text.Json.Arazzo.Durability.AzureStorage.Tests;

/// <summary>
/// The Azure Storage audit sink (ADR 0069) against the Azurite emulator: what the chain writer appends is what the
/// container holds, one block to a record, in a blob that is never reopened. Azurite has no immutability policies, so
/// the immutable half is exercised as far as the emulator allows: a mutable container is refused unless the caller
/// opts out.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class AzureBlobAuditSinkTests
{
    private static readonly AuditEntry Approve = new("access-request.approve", "alice", "acme", "access-request", "req-1", "granted", "production");

    private static AzuriteContainer azurite = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        azurite = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
        await azurite.StartAsync();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (azurite is not null)
        {
            await azurite.DisposeAsync();
        }
    }

    [TestMethod]
    public async Task A_chain_written_through_the_sink_is_one_append_blob_with_one_block_to_a_record_and_it_verifies()
    {
        BlobContainerClient container = await NewContainerAsync();
        AzureBlobAuditSink sink = await AzureBlobAuditSink.ConnectAsync(container, allowMutableContainer: true);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        await using (var writer = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), headOptions: new AuditHeadOptions(2, TimeSpan.FromHours(1))))
        {
            for (int i = 0; i < 5; i++)
            {
                await writer.AppendAsync(Approve, default);
            }
        }

        BlobItem blob = await SingleBlobAsync(container);
        blob.Name.ShouldEndWith(AzureBlobAuditSink.ChainBlobExtension);
        blob.Properties.BlobType.ShouldBe(BlobType.Append);

        // The open record, five mutations and three heads (one to every two records): nine blocks.
        BlobProperties properties = await container.GetBlobClient(blob.Name).GetPropertiesAsync();
        properties.BlobCommittedBlockCount.ShouldBe(9);

        await using Stream stored = await container.GetBlobClient(blob.Name).OpenReadAsync();
        AuditChainVerification verification = await AuditChainVerifier.VerifyAsync(
            stored,
            new AuditChainVerificationOptions { TrustStore = new TrustStoreExecutorPackageVerifier(new Dictionary<string, AsymmetricAlgorithm> { ["audit-1"] = key }) });
        verification.IsIntact.ShouldBeTrue();
        verification.ChainId.ShouldBe(Path.GetFileNameWithoutExtension(blob.Name));
        verification.RecordCount.ShouldBe(9);
        verification.HeadCount.ShouldBe(3);
        verification.UnsignedTailCount.ShouldBe(0);
    }

    [TestMethod]
    public async Task A_chain_is_never_reopened_or_replaced()
    {
        BlobContainerClient container = await NewContainerAsync();
        AzureBlobAuditSink sink = await AzureBlobAuditSink.ConnectAsync(container, allowMutableContainer: true);
        byte[] chainId = "0123456789abcdef0123456789abcdef"u8.ToArray();

        await using (IAuditChainStream first = await sink.CreateChainAsync("cp-0"u8.ToArray(), chainId, default))
        {
            await first.AppendAsync("{\"kept\":true}\n"u8.ToArray(), default);
        }

        RequestFailedException refused = await Should.ThrowAsync<RequestFailedException>(async () => await sink.CreateChainAsync("cp-0"u8.ToArray(), chainId, default));
        refused.Status.ShouldBe(409);

        // The refused create replaced nothing: the chain still holds what was appended to it.
        BlobDownloadResult kept = await container.GetBlobClient("cp-0/0123456789abcdef0123456789abcdef" + AzureBlobAuditSink.ChainBlobExtension).DownloadContentAsync();
        kept.Content.ToString().ShouldBe("{\"kept\":true}\n");
    }

    [TestMethod]
    public async Task A_container_that_disappears_fails_the_append_and_the_next_chain_continues_the_abandoned_one()
    {
        BlobContainerClient container = await NewContainerAsync();
        AzureBlobAuditSink sink = await AzureBlobAuditSink.ConnectAsync(container, allowMutableContainer: true);
        await using var writer = new AuditChainWriter(sink);

        await writer.AppendAsync(Approve, default);
        string abandoned = Path.GetFileNameWithoutExtension((await SingleBlobAsync(container)).Name);

        await container.DeleteAsync();
        AuditAppendException failure = await Should.ThrowAsync<AuditAppendException>(async () => await writer.AppendAsync(Approve, default));
        failure.InnerException.ShouldBeOfType<RequestFailedException>();

        await container.CreateAsync();
        await writer.AppendAsync(Approve, default);

        BlobItem successor = await SingleBlobAsync(container);
        await using Stream stored = await container.GetBlobClient(successor.Name).OpenReadAsync();
        AuditChainVerification next = await AuditChainVerifier.VerifyAsync(stored);
        next.IsIntact.ShouldBeTrue();
        next.ContinuesChain.ShouldBe(abandoned);
    }

    [TestMethod]
    public async Task A_writer_that_starts_again_finds_its_last_chain_under_its_prefix_and_continues_it()
    {
        BlobContainerClient container = await NewContainerAsync();
        AzureBlobAuditSink sink = await AzureBlobAuditSink.ConnectAsync(container, allowMutableContainer: true);
        using ECDsa key = ECDsa.Create(ECCurve.NamedCurves.nistP256);

        // Two processes of cp-0, and one of cp-1 between them, whose chain cp-0 must not take for its own.
        var first = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), writerId: "cp-0");
        await first.AppendAsync(Approve, default);
        await using (var other = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), writerId: "cp-1"))
        {
            await other.AppendAsync(Approve, default);
        }

        await using (var second = new AuditChainWriter(sink, headSigner: new EcdsaExecutorPackageSigner(key, "audit-1"), writerId: "cp-0"))
        {
            await second.ResumeAsync(default);
        }

        var mine = new List<string>();
        await foreach (BlobItem blob in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, "cp-0/", default))
        {
            mine.Add(blob.Name);
        }

        mine.Sort(StringComparer.Ordinal);
        mine.Count.ShouldBe(2);
        await using Stream old = await container.GetBlobClient(mine[0]).OpenReadAsync();
        await using Stream next = await container.GetBlobClient(mine[1]).OpenReadAsync();
        AuditChainVerification oldChain = await AuditChainVerifier.VerifyAsync(old);
        AuditChainVerification nextChain = await AuditChainVerifier.VerifyAsync(next);
        nextChain.Writer.ShouldBe("cp-0");
        nextChain.ContinuesChain.ShouldBe(oldChain.ChainId);
        nextChain.ContinuesHash.ShouldBe(oldChain.LastHash);
        nextChain.HeadCount.ShouldBe(1);
    }

    [TestMethod]
    public async Task A_mutable_container_is_refused_unless_the_caller_opts_out()
    {
        BlobContainerClient container = await NewContainerAsync();

        InvalidOperationException refused = await Should.ThrowAsync<InvalidOperationException>(async () => await AzureBlobAuditSink.ConnectAsync(container));
        refused.Message.ShouldContain(container.Name);
        refused.Message.ShouldContain("allowMutableContainer");

        (await AzureBlobAuditSink.ConnectAsync(container, allowMutableContainer: true)).ShouldNotBeNull();
    }

    private static async Task<BlobContainerClient> NewContainerAsync()
    {
        var blobService = new BlobServiceClient(azurite.GetConnectionString(), new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_11_04));
        BlobContainerClient container = blobService.GetBlobContainerClient("audit-" + Guid.NewGuid().ToString("N"));
        await container.CreateAsync();
        return container;
    }

    private static async Task<BlobItem> SingleBlobAsync(BlobContainerClient container)
    {
        var blobs = new List<BlobItem>();
        await foreach (BlobItem blob in container.GetBlobsAsync())
        {
            blobs.Add(blob);
        }

        return blobs.ShouldHaveSingleItem();
    }
}