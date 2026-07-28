// <copyright file="AotHostAppAssemblerTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Aot.Tests;

/// <summary>
/// Proves the <see cref="AotHostAppAssembler"/> builds the thin host-app around a version's signed executor assembly:
/// the entry stub constructs the manifest's baked type, the project file targets the requested runtime, references the
/// executor by its true assembly name and the runtime graph as packages, the feed config is hermetic, and a manifest
/// with no engine version (which cannot be safely native-compiled) is refused.
/// </summary>
[TestClass]
public sealed class AotHostAppAssemblerTests
{
    private const string EntryType = "Corvus.Workflows.Generated.Workflows.PetWorkflowHost";

    private static readonly AotHostAppOptions Options = new()
    {
        RuntimePackageVersion = "5.0.0-local.2",
        FeedSources = [("local", "/work/local-packages"), ("nuget.org", "https://api.nuget.org/v3/index.json")],
    };

    private static readonly AotHostAppOptions AzureOptions = new()
    {
        Target = ServerlessTarget.AzureFunctions,
        RuntimePackageVersion = "5.0.0-local.2",
        FeedSources = [("local", "/work/local-packages"), ("nuget.org", "https://api.nuget.org/v3/index.json")],
    };

    // A real .NET assembly stands in for the executor: the assembler only reads its name from PE metadata and embeds
    // its bytes, so any valid assembly exercises those paths.
    private static readonly byte[] StandInExecutor = File.ReadAllBytes(typeof(AotHostAppAssembler).Assembly.Location);
    private static readonly string StandInExecutorName = typeof(AotHostAppAssembler).Assembly.GetName().Name!;

    [TestMethod]
    public void Assembles_the_entry_stub_that_constructs_the_baked_type()
    {
        AssembledHostApp app = Assemble("linux-x64");

        string program = FileText(app, "Program.cs");
        program.ShouldContain($"new BakedHostedWorkflowResolver(new {EntryType}())");
        program.ShouldContain("LambdaServerlessFunction.RunAsync");
        app.EntryType.ShouldBe(EntryType);
        app.EngineVersion.ShouldBe("5.0.0.0");
    }

    [TestMethod]
    public void Assembles_the_project_for_the_target_referencing_the_signed_executor_and_the_runtime_packages()
    {
        AssembledHostApp app = Assemble("linux-x64");

        string project = FileText(app, "fn.csproj");
        project.ShouldContain("<PublishAot>true</PublishAot>");
        project.ShouldContain("<AssemblyName>bootstrap</AssemblyName>");
        project.ShouldContain("<RuntimeIdentifier>linux-x64</RuntimeIdentifier>");
        project.ShouldContain("""<PackageReference Include="Corvus.Text.Json.Arazzo.Durability.Serverless.Lambda" Version="5.0.0-local.2" />""");
        project.ShouldContain($"""<Reference Include="{StandInExecutorName}">""");
        // The executor file is named after its assembly simple name so ILC resolves it by name (GetModuleForSimpleName),
        // not by HintPath — otherwise the AOT type loader cannot find the executor assembly and the entry type fails to load.
        project.ShouldContain($"<HintPath>{StandInExecutorName}.dll</HintPath>");
        project.ShouldContain($"""<TrimmerRootAssembly Include="{StandInExecutorName}" />""");
    }

    [TestMethod]
    public void The_runtime_identifier_flows_into_the_project()
    {
        FileText(Assemble("linux-arm64"), "fn.csproj").ShouldContain("<RuntimeIdentifier>linux-arm64</RuntimeIdentifier>");
        Assemble("win-x64").RuntimeIdentifier.ShouldBe("win-x64");
    }

    [TestMethod]
    public void Writes_a_hermetic_feed_config_from_the_options()
    {
        string config = FileText(Assemble("linux-x64"), "nuget.config");

        config.ShouldContain("<clear />");
        config.ShouldContain("""<add key="local" value="/work/local-packages" />""");
        config.ShouldContain("""<add key="nuget.org" value="https://api.nuget.org/v3/index.json" />""");
    }

    [TestMethod]
    public void Embeds_the_signed_executor_assembly_verbatim()
    {
        AssembledHostApp app = Assemble("linux-x64");

        AotProjectFile executor = app.Files.Single(f => f.RelativePath == $"{StandInExecutorName}.dll");
        executor.Content.ToArray().ShouldBe(StandInExecutor);
    }

    [TestMethod]
    public void Rejects_a_manifest_without_an_engine_version()
    {
        // A manifest written before format 2 has no engineVersion, so no runtime can be pinned for a safe native build.
        byte[] legacyManifest = Encoding.UTF8.GetBytes(
            """{"formatVersion":1,"targetFramework":"net10.0","packageHash":"deadbeef","assemblyDigest":"sha256:x","entryType":"X","workflowId":"pet@1","durable":true}""");

        Should.Throw<ArgumentException>(() => new AotHostAppAssembler().Assemble(StandInExecutor, legacyManifest, "linux-x64", Options));
    }

    [TestMethod]
    public void Rejects_an_empty_runtime_identifier()
    {
        Should.Throw<ArgumentException>(() => new AotHostAppAssembler().Assemble(StandInExecutor, Manifest(), string.Empty, Options));
    }

    [TestMethod]
    public void Assembles_an_azure_isolated_worker_entry_that_registers_the_baked_handler()
    {
        AssembledHostApp app = AssembleAzure("linux-x64");
        app.Target.ShouldBe(ServerlessTarget.AzureFunctions);

        string program = FileText(app, "Program.cs");
        program.ShouldContain("FunctionsApplication.CreateBuilder(args)");
        program.ShouldContain("ConfigureFunctionsWebApplication()");
        program.ShouldContain($"new BakedHostedWorkflowResolver(new {EntryType}())");
        program.ShouldContain("ServerlessInvocationHandler");
        // The Lambda entry must not leak into the Azure host.
        program.ShouldNotContain("LambdaServerlessFunction");
    }

    [TestMethod]
    public void Assembles_the_azure_http_trigger_and_host_json()
    {
        AssembledHostApp app = AssembleAzure("linux-x64");

        string invoke = FileText(app, "ArazzoInvokeFunction.cs");
        invoke.ShouldContain("""[Function("invoke")]""");
        invoke.ShouldContain("[HttpTrigger(");
        invoke.ShouldContain("HandleAsync");

        FileText(app, "host.json").ShouldContain("\"version\": \"2.0\"");
    }

    [TestMethod]
    public void Assembles_the_azure_project_as_a_framework_dependent_readytorun_isolated_worker()
    {
        string project = FileText(AssembleAzure("linux-x64"), "fn.csproj");

        project.ShouldContain("<PublishReadyToRun>true</PublishReadyToRun>");
        project.ShouldContain("<SelfContained>false</SelfContained>");
        project.ShouldContain("<AzureFunctionsVersion>v4</AzureFunctionsVersion>");
        project.ShouldContain("<RuntimeIdentifier>linux-x64</RuntimeIdentifier>");
        project.ShouldContain("""<FrameworkReference Include="Microsoft.AspNetCore.App" />""");
        project.ShouldContain("""<PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />""");
        project.ShouldContain("""<PackageReference Include="Corvus.Text.Json.Arazzo.Durability.Serverless" Version="5.0.0-local.2" />""");
        project.ShouldContain($"""<Reference Include="{StandInExecutorName}">""");

        // Lambda-only bits must not leak into a runtime-present ReadyToRun target.
        project.ShouldNotContain("<PublishAot>");
        project.ShouldNotContain("bootstrap");
        project.ShouldNotContain("Amazon.Lambda");
        project.ShouldNotContain("TrimmerRootAssembly");
    }

    private static AssembledHostApp AssembleAzure(string rid) => new AotHostAppAssembler().Assemble(StandInExecutor, Manifest(), rid, AzureOptions);

    private static AssembledHostApp Assemble(string rid) => new AotHostAppAssembler().Assemble(StandInExecutor, Manifest(), rid, Options);

    private static byte[] Manifest() => Encoding.UTF8.GetBytes(
        $$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"deadbeef","assemblyDigest":"sha256:x","entryType":"{{EntryType}}","workflowId":"pet@1","durable":true,"engineVersion":"5.0.0.0"}""");

    private static string FileText(AssembledHostApp app, string path) => Encoding.UTF8.GetString(app.Files.Single(f => f.RelativePath == path).Content.Span);
}