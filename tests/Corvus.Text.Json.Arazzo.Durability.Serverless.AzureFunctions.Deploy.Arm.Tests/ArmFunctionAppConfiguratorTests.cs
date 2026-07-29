// <copyright file="ArmFunctionAppConfiguratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Azure.Core;
using Azure.ResourceManager;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm.Tests;

/// <summary>
/// Unit proofs of the <see cref="ArmFunctionAppConfigurator"/>'s pure logic: the globally-unique Function App name it
/// derives, and the constructor guards. The real ARM behaviour (setting run-from-package on a live app) has no emulator,
/// so it is the opt-in live-Azure proof.
/// </summary>
[TestClass]
public sealed class ArmFunctionAppConfiguratorTests
{
    private static readonly ArmFunctionAppConfiguratorOptions Options = new()
    {
        SubscriptionId = "00000000-0000-0000-0000-000000000000",
        ResourceGroupName = "rg",
        AppNamePrefix = "arz",
    };

    [TestMethod]
    public void AppName_is_deterministic_prefixed_lowercased_and_sanitized()
    {
        var request = new ServerlessDeployRequest("Adopt.Pet", 3, "Prod", "linux-x64", ReadOnlyMemory<byte>.Empty);

        string first = ArmFunctionAppConfigurator.AppName(request, "arz");
        string second = ArmFunctionAppConfigurator.AppName(request, "arz");

        // Prefixed for global uniqueness; each of base/env/rid lowercased with non [a-z0-9-] mapped to '-'; version a v{n} segment.
        first.ShouldBe("arz-adopt-pet-v3-prod-linux-x64");
        second.ShouldBe(first);
    }

    [TestMethod]
    public void AppName_stays_within_the_azure_60_char_site_name_limit()
    {
        var request = new ServerlessDeployRequest(new string('a', 80), 12, "production-environment", "linux-x64", ReadOnlyMemory<byte>.Empty);

        string name = ArmFunctionAppConfigurator.AppName(request, "arz-long-prefix");

        name.Length.ShouldBeLessThanOrEqualTo(60);
        name.ShouldNotStartWith("-");
        name.ShouldNotEndWith("-");
    }

    [TestMethod]
    public void AppName_distinguishes_targets_that_differ_only_by_version()
    {
        var v1 = new ServerlessDeployRequest("check", 1, "isolated", "linux-x64", ReadOnlyMemory<byte>.Empty);
        var v2 = new ServerlessDeployRequest("check", 2, "isolated", "linux-x64", ReadOnlyMemory<byte>.Empty);

        ArmFunctionAppConfigurator.AppName(v1, "arz").ShouldBe("arz-check-v1-isolated-linux-x64");
        ArmFunctionAppConfigurator.AppName(v2, "arz").ShouldNotBe(ArmFunctionAppConfigurator.AppName(v1, "arz"));
    }

    [TestMethod]
    public void Ctor_rejects_a_null_arm_client()
    {
        Should.Throw<ArgumentNullException>(() => new ArmFunctionAppConfigurator(null!, Options));
    }

    [TestMethod]
    public void Ctor_rejects_null_options()
    {
        var client = new ArmClient(new ThrowingCredential());
        Should.Throw<ArgumentNullException>(() => new ArmFunctionAppConfigurator(client, null!));
    }

    // A credential that never yields a token: the constructor tests only need a non-null ArmClient and never call Azure.
    private sealed class ThrowingCredential : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new NotSupportedException("The unit tests never authenticate.");

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
            => throw new NotSupportedException("The unit tests never authenticate.");
    }
}