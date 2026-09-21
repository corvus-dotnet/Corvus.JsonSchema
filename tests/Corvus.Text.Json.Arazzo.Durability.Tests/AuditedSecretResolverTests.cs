// <copyright file="AuditedSecretResolverTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Security.Cryptography;
using System.Text;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Execution;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;
using Stj = System.Text.Json;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// A runner's record of the secrets it resolves (ADR 0070): on the runner's own chain, naming the secret and never its
/// material, and never the reason a secret is not resolved, since run execution is not gated on the audit sink.
/// </summary>
[TestClass]
public sealed class AuditedSecretResolverTests
{
    private const string Reference = "vault://secret/arazzo/petstore#api-key";

    [TestMethod]
    public async Task A_resolution_is_a_read_record_on_the_runners_chain_naming_the_secret_and_not_its_material()
    {
        var sink = new InMemoryAuditSink();
        await using GovernanceAuditor auditor = Auditor(sink);
        var resolver = new AuditedSecretResolver(new FixedResolver("s3cr3t-material"), auditor, "runner-7");

        using (SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse(Reference), default))
        {
            Encoding.UTF8.GetString(material.Utf8).ShouldBe("s3cr3t-material");
        }

        string chain = Encoding.UTF8.GetString(sink.Snapshot(sink.ChainIds.ShouldHaveSingleItem()));
        Stj.JsonElement read = Reads(chain).ShouldHaveSingleItem();
        read.GetProperty("action").GetString().ShouldBe("secret.resolve");
        read.GetProperty("actor").GetString().ShouldBe("runner-7");
        read.GetProperty("targetKind").GetString().ShouldBe("secret");
        read.GetProperty("targetId").GetString().ShouldBe(Reference);
        read.GetProperty("disclosure").GetString().ShouldBe("resolved");
        chain.ShouldNotContain("s3cr3t-material");
        resolver.CanResolve(SecretScheme.HashiCorpVault).ShouldBeTrue();
    }

    [TestMethod]
    public async Task A_secret_that_will_not_resolve_is_recorded_as_failed_and_the_failure_still_reaches_the_caller()
    {
        var sink = new InMemoryAuditSink();
        await using GovernanceAuditor auditor = Auditor(sink);
        var resolver = new AuditedSecretResolver(new FixedResolver(null), auditor, "runner-7");

        await Should.ThrowAsync<CryptographicException>(async () => await resolver.ResolveAsync(SecretRef.Parse(Reference), default));

        Reads(Encoding.UTF8.GetString(sink.Snapshot(sink.ChainIds[0]))).ShouldHaveSingleItem().GetProperty("disclosure").GetString().ShouldBe("failed");
    }

    [TestMethod]
    public async Task An_audit_sink_that_is_down_never_stops_a_secret_being_resolved()
    {
        await using var auditor = new GovernanceAuditor(sink: new DownSink(), headSigner: new EcdsaExecutorPackageSigner(ECDsa.Create(ECCurve.NamedCurves.nistP256), "runner-audit"), writerId: "runner-7");
        var resolver = new AuditedSecretResolver(new FixedResolver("s3cr3t-material"), auditor, "runner-7");

        // Run execution is never gated on the sink (ADR 0069): the secret resolves, and the runner's audit health says
        // that its record did not land.
        using SecretMaterial material = await resolver.ResolveAsync(SecretRef.Parse(Reference), default);

        Encoding.UTF8.GetString(material.Utf8).ShouldBe("s3cr3t-material");
        auditor.Health.IsHealthy.ShouldBeFalse();
        auditor.Health.FailuresSinceSuccess.ShouldBe(1);
    }

    private static GovernanceAuditor Auditor(IAuditSink sink)
        => new(sink: sink, headSigner: new EcdsaExecutorPackageSigner(ECDsa.Create(ECCurve.NamedCurves.nistP256), "runner-audit"), headOptions: new AuditHeadOptions(1000, TimeSpan.FromHours(1)), writerId: "runner-7");

    private static List<Stj.JsonElement> Reads(string chain)
        => [.. chain.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(l => Stj.JsonDocument.Parse(l).RootElement).Where(r => r.GetProperty("kind").GetString() == "read")];

    private sealed class FixedResolver(string? material) : ISecretResolver
    {
        public bool CanResolve(SecretScheme scheme) => true;

        public ValueTask<SecretMaterial> ResolveAsync(SecretRef reference, CancellationToken cancellationToken)
            => material is null
                ? throw new CryptographicException("the secret would not decrypt")
                : new ValueTask<SecretMaterial>(new SecretMaterial(Encoding.UTF8.GetBytes(material)));
    }

    private sealed class DownSink : IAuditSink
    {
        public ValueTask<IAuditChainStream> CreateChainAsync(ReadOnlyMemory<byte> writerId, ReadOnlyMemory<byte> chainId, CancellationToken cancellationToken)
            => throw new IOException("the audit store is unreachable");

        public ValueTask<Stream?> OpenLastChainAsync(ReadOnlyMemory<byte> writerId, CancellationToken cancellationToken) => new((Stream?)null);
    }
}