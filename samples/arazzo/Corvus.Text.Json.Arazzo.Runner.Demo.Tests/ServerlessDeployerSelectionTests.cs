// <copyright file="ServerlessDeployerSelectionTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Aot;
using Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy;
using Corvus.Text.Json.Arazzo.Durability.Serverless.AzureFunctions.Deploy;
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
            new Dictionary<string, string> { ["ARAZZO_SOURCE__echo"] = "http://host:8080/demo", [ServerlessCheckpointOrigins.SettingName] = "http://host:8080/" });

        deployer.ShouldBeOfType<LambdaServerlessDeployer>();
    }

    [TestMethod]
    public void The_lambda_platform_requires_a_service_url()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            ServerlessDeployerSelection.Create(Config(), Settings()));
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
                ("Runner:AzureFlex:AppNamePrefix", "acme-arazzo"),
                ("Runner:AzureFlex:InvokeKeyRef", "env://ARAZZO_INVOKE_KEY")),
            Settings());

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

            // The invoke key is required (ADR 0059 decision 4): there is no deploying a function nobody holds a key to.
            ("Runner:AzureFlex:InvokeKeyRef", "env://ARAZZO_INVOKE_KEY"),
        ];
        foreach ((string missing, _) in all)
        {
            (string, string)[] supplied = [("Runner:Serverless:Platform", "azure-flex"), .. all.Where(k => k.Key != missing)];
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
                ServerlessDeployerSelection.Create(Config(supplied), Settings()));
            ex.Message.ShouldContain(missing);
        }
    }

    [TestMethod]
    public void Every_platform_has_an_invoke_authenticator_and_none_is_anonymous()
    {
        // ADR 0059 decision 4: the invocation carries the platform's credential. Lambda signs, Azure presents its key
        // (with Entra on top when an audience is named), and the micro-guest sidecar is reachable on loopback alone.
        ServerlessDeployerSelection.CreateInvokeAuthenticator(Config())
            .ShouldBeOfType<SigV4ServerlessInvokeAuthenticator>();

        ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(
                ("Runner:Serverless:Platform", "azure-flex"),
                ("Runner:AzureFlex:InvokeKeyRef", "env://ARAZZO_INVOKE_KEY")))
            .ShouldBeOfType<FunctionKeyServerlessInvokeAuthenticator>();

        ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(
                ("Runner:Serverless:Platform", "azure-flex"),
                ("Runner:AzureFlex:InvokeKeyRef", "env://ARAZZO_INVOKE_KEY"),
                ("Runner:AzureFlex:EntraAudience", "api://arazzo-functions")))
            .ShouldBeOfType<EntraServerlessInvokeAuthenticator>();

        ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(
                ("Runner:Serverless:Platform", "micro-guest"),
                ("Runner:MicroGuest:AdminTokenRef", "env://ARAZZO_SIDECAR_ADMIN_TOKEN")))
            .ShouldBeOfType<MicroGuestSidecarInvokeAuthenticator>();

        // The sidecar's admin surface never runs unauthenticated, so the token reference is required (P1-10).
        Should.Throw<InvalidOperationException>(() => ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(("Runner:Serverless:Platform", "micro-guest"))))
            .Message.ShouldContain("Runner:MicroGuest:AdminTokenRef");

        // Azure without a key reference is a configuration error, not a keyless invoke.
        Should.Throw<InvalidOperationException>(() => ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(("Runner:Serverless:Platform", "azure-flex"))))
            .Message.ShouldContain("Runner:AzureFlex:InvokeKeyRef");
        Should.Throw<InvalidOperationException>(() => ServerlessDeployerSelection.CreateInvokeAuthenticator(Config(("Runner:Serverless:Platform", "nonsense"))));
    }

    [TestMethod]
    public void The_micro_guest_platform_builds_the_micro_guest_deployer()
    {
        IServerlessDeployer deployer = ServerlessDeployerSelection.Create(
            Config(
                ("Runner:Serverless:Platform", "micro-guest"),
                ("Runner:MicroGuest:SidecarUrl", "http://127.0.0.1:9411"),
                ("Runner:MicroGuest:AdminTokenRef", "env://ARAZZO_SIDECAR_ADMIN_TOKEN"),
                ("Runner:MicroGuest:CheckpointSurfaceUrl", "http://172.20.0.10:8199/checkpoints")),
            Settings());

        deployer.ShouldBeOfType<MicroGuestDeployer>();
    }

    [TestMethod]
    public void The_micro_guest_platform_names_each_missing_required_key()
    {
        (string Key, string Value)[] all =
        [
            ("Runner:MicroGuest:SidecarUrl", "http://127.0.0.1:9411"),
            ("Runner:MicroGuest:AdminTokenRef", "env://ARAZZO_SIDECAR_ADMIN_TOKEN"),
            ("Runner:MicroGuest:CheckpointSurfaceUrl", "http://172.20.0.10:8199/checkpoints"),
        ];
        foreach ((string missing, _) in all)
        {
            (string, string)[] supplied = [("Runner:Serverless:Platform", "micro-guest"), .. all.Where(k => k.Key != missing)];
            InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
                ServerlessDeployerSelection.Create(Config(supplied), Settings()));
            ex.Message.ShouldContain(missing);
            ex.Message.ShouldContain("micro-guest");
        }
    }

    [TestMethod]
    public void An_unknown_platform_is_refused_with_the_known_choices()
    {
        InvalidOperationException ex = Should.Throw<InvalidOperationException>(() =>
            ServerlessDeployerSelection.Create(Config(("Runner:Serverless:Platform", "gcp")), Settings()));
        ex.Message.ShouldContain("gcp");
        ex.Message.ShouldContain("azure-flex");
        ex.Message.ShouldContain("micro-guest");
    }

    // Function settings that carry the required checkpoint origins (ADR 0059 decision 4) and nothing else.
    private static Dictionary<string, string> Settings() => new() { [ServerlessCheckpointOrigins.SettingName] = "http://host:8080/" };

    private static IConfiguration Config(params (string Key, string Value)[] values)
        => new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build();
}