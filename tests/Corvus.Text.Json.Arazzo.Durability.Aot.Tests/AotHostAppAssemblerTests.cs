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
        project.ShouldContain("<HintPath>executor.dll</HintPath>");
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

        AotProjectFile executor = app.Files.Single(f => f.RelativePath == "executor.dll");
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

    private static AssembledHostApp Assemble(string rid) => new AotHostAppAssembler().Assemble(StandInExecutor, Manifest(), rid, Options);

    private static byte[] Manifest() => Encoding.UTF8.GetBytes(
        $$"""{"formatVersion":2,"targetFramework":"net10.0","packageHash":"deadbeef","assemblyDigest":"sha256:x","entryType":"{{EntryType}}","workflowId":"pet@1","durable":true,"engineVersion":"5.0.0.0"}""");

    private static string FileText(AssembledHostApp app, string path) => Encoding.UTF8.GetString(app.Files.Single(f => f.RelativePath == path).Content.Span);
}