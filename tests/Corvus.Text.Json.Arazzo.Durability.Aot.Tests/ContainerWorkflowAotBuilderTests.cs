// <copyright file="ContainerWorkflowAotBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Aot.Tests;

/// <summary>
/// Proves the <see cref="ContainerWorkflowAotBuilder"/> forms the container run command correctly: it mounts the
/// assembled project read-write and the feeds read-only, and publishes the one project to the requested runtime target.
/// Running an actual build is the container integration proof, not a unit test.
/// </summary>
[TestClass]
public sealed class ContainerWorkflowAotBuilderTests
{
    [TestMethod]
    public void Builds_the_container_run_command_for_the_target()
    {
        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions
        {
            ContainerImage = "arazzo-aot-builder:net10",
            ReadOnlyMounts = [("/host/local-packages", "/work/local-packages")],
        });

        string command = string.Join(' ', builder.BuildContainerArguments("/tmp/work", "linux-x64"));

        command.ShouldStartWith("run --rm");
        command.ShouldContain("-e DOTNET_CLI_HOME=/tmp");
        command.ShouldContain("-v /tmp/work:/work/fn:rw");
        command.ShouldContain("-v /host/local-packages:/work/local-packages:ro");
        command.ShouldContain("arazzo-aot-builder:net10");
        command.ShouldContain("dotnet publish /work/fn/fn.csproj -r linux-x64 -c Release -o /work/fn/out");
        // A Native-AOT (Lambda) target is self-contained; the framework-dependent flag is Azure-only.
        command.ShouldNotContain("--self-contained");
    }

    [TestMethod]
    public void Builds_a_framework_dependent_publish_command_for_the_azure_target()
    {
        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions { ReadOnlyMounts = [] });

        string command = string.Join(' ', builder.BuildContainerArguments("/tmp/work", "linux-x64", ServerlessTarget.AzureFunctions));

        // The dotnet-isolated host provides the runtime, so the isolated-worker app is published framework-dependent.
        command.ShouldContain("dotnet publish /work/fn/fn.csproj -r linux-x64 -c Release --self-contained false -o /work/fn/out");
    }

    [TestMethod]
    public void The_runtime_target_flows_into_the_publish_command()
    {
        var builder = new ContainerWorkflowAotBuilder(new ContainerAotBuilderOptions { ReadOnlyMounts = [] });

        string command = string.Join(' ', builder.BuildContainerArguments("/tmp/work", "linux-arm64"));

        command.ShouldContain("-r linux-arm64");
    }

    [TestMethod]
    public void Rejects_null_options()
    {
        Should.Throw<ArgumentNullException>(() => new ContainerWorkflowAotBuilder(null!));
    }
}