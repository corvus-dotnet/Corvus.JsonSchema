// <copyright file="ServerlessDeployerSelectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy.Arm;
using Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda.Deploy;
using Corvus.Text.Json.Arazzo.ServerlessRunner.Demo;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Runner.Demo.Tests;

/// <summary>
/// The serverless runner's platform selection (ADR 0061: deployer selection is a host-wiring concern). One host image
/// serves either platform from configuration alone — lambda by default (the demo AppHost's LocalStack path), azure-flex
/// on request — and a missing or unknown configuration fails fast at startup with the offending key named, never as a
/// silent deploy failure later.
/// </summary>
[TestClass]
public sealed class ServerlessDeployerSelectionTests
{
    [TestMethod]
    public void The_default_platform_is_lambda()
    {
        IServerlessDeployer deployer = ServerlessDeployerSelection.Create(
            Config(("Runner:Lambda:ServiceUrl", "http://localhost:4566")),
            new Dictionary<string, string> { ["ARAZZO_SOURCE__echo"] = "http://host:8080/demo" });

        deployer.ShouldBeOfType<LambdaServerlessDeployer>();
    }

    [TestMethod]
    public void The_lambda_platform_requires_a_service_url()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            ServerlessDeployerSelection.Create(Config(), new Dictionary<string, string>()));
        ex.Message.ShouldContain("Runner:Lambda:ServiceUrl");
    }

    [TestMethod]
    public void The_azure_flex_platform_builds_the_flex_deployer()
    {
        IServerlessDeployer deployer = ServerlessDeployerSelection.Create(
            Config(
                ("Runner:Serverless:Platform", "azure-flex"),
                ("Runner:AzureFlex:SubscriptionId", "00000000-0000-0000-0000-000000000000"),
                ("Runner:AzureFlex:ResourceGroup", "rg-arazzo"),
                ("Runner:AzureFlex:AppNamePrefix", "acme-arazzo")),
            new Dictionary<string, string>());

        deployer.ShouldBeOfType<AzureFunctionsFlexDeployer>();
    }

    [TestMethod]
    public void The_azure_flex_platform_names_each_missing_required_key()
    {
        // Each key is validated in declaration order; drop one at a time and the error names IT, so a misconfigured
        // host points the operator straight at the gap.
        (string Key, string Value)[] all =
        [
            ("Runner:AzureFlex:SubscriptionId", "00000000-0000-0000-0000-000000000000"),
            ("Runner:AzureFlex:ResourceGroup", "rg-arazzo"),
            ("Runner:AzureFlex:AppNamePrefix", "acme-arazzo"),
        ];
        foreach ((string missing, _) in all)
        {
            (string, string)[] supplied = [("Runner:Serverless:Platform", "azure-flex"), .. all.Where(k => k.Key != missing)];
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
                ServerlessDeployerSelection.Create(Config(supplied), new Dictionary<string, string>()));
            ex.Message.ShouldContain(missing);
        }
    }

    [TestMethod]
    public void The_micro_guest_platform_builds_the_micro_guest_deployer()
    {
        IServerlessDeployer deployer = ServerlessDeployerSelection.Create(
            Config(
                ("Runner:Serverless:Platform", "micro-guest"),
                ("Runner:MicroGuest:SidecarUrl", "http://127.0.0.1:9411"),
                ("Runner:MicroGuest:CheckpointSurfaceUrl", "http://172.20.0.10:8199/checkpoints")),
            new Dictionary<string, string>());

        deployer.ShouldBeOfType<MicroGuestDeployer>();
    }

    [TestMethod]
    public void The_micro_guest_platform_names_each_missing_required_key()
    {
        (string Key, string Value)[] all =
        [
            ("Runner:MicroGuest:SidecarUrl", "http://127.0.0.1:9411"),
            ("Runner:MicroGuest:CheckpointSurfaceUrl", "http://172.20.0.10:8199/checkpoints"),
        ];
        foreach ((string missing, _) in all)
        {
            (string, string)[] supplied = [("Runner:Serverless:Platform", "micro-guest"), .. all.Where(k => k.Key != missing)];
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
                ServerlessDeployerSelection.Create(Config(supplied), new Dictionary<string, string>()));
            ex.Message.ShouldContain(missing);
            ex.Message.ShouldContain("micro-guest");
        }
    }

    [TestMethod]
    public void An_unknown_platform_is_refused_with_the_known_choices()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            ServerlessDeployerSelection.Create(Config(("Runner:Serverless:Platform", "gcp")), new Dictionary<string, string>()));
        ex.Message.ShouldContain("gcp");
        ex.Message.ShouldContain("azure-flex");
        ex.Message.ShouldContain("micro-guest");
    }

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}