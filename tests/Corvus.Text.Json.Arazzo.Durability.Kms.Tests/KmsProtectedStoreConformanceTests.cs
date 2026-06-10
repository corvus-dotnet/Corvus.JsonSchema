// <copyright file="KmsProtectedStoreConformanceTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Conformance;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Testcontainers.LocalStack;

namespace Corvus.Text.Json.Arazzo.Durability.Kms.Tests;

/// <summary>
/// Runs the shared store-conformance suite against the in-memory store wrapped in a
/// <see cref="ProtectedWorkflowStateStore"/> using a <see cref="KmsCheckpointProtector"/> over a real AWS KMS
/// (LocalStack). This exercises the envelope's data-key wrap/unwrap end-to-end through the full store contract.
/// </summary>
[TestClass]
[TestCategory("integration")]
[TestCategory("docker")]
public sealed class KmsProtectedStoreConformanceTests : WorkflowStateStoreConformance
{
    private static LocalStackContainer container = null!;
    private static AmazonKeyManagementServiceClient kms = null!;
    private static string keyId = null!;

    [ClassInitialize]
    public static async Task ClassInitAsync(TestContext context)
    {
        container = new LocalStackBuilder().Build();
        await container.StartAsync();

        var config = new AmazonKeyManagementServiceConfig
        {
            ServiceURL = container.GetConnectionString(),
            AuthenticationRegion = "us-east-1",
        };
        kms = new AmazonKeyManagementServiceClient(new BasicAWSCredentials("test", "test"), config);

        CreateKeyResponse key = await kms.CreateKeyAsync(new CreateKeyRequest());
        keyId = key.KeyMetadata.KeyId;
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        kms?.Dispose();
        if (container is not null)
        {
            await container.DisposeAsync();
        }
    }

    protected override ValueTask<IWorkflowStateStore> CreateStoreAsync(TimeProvider timeProvider)
    {
        var protector = new KmsCheckpointProtector(kms, keyId);
        var inner = new InMemoryWorkflowStateStore(timeProvider);
        return ValueTask.FromResult<IWorkflowStateStore>(new ProtectedWorkflowStateStore(inner, protector));
    }
}
