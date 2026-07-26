// <copyright file="WorkflowPackageNativeArtifactTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.Arazzo.Durability;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>
/// The per-runtime-target native executor entries (<c>metadata/native/&lt;rid&gt;</c>, ADR 0055): the build service
/// attaches one once a target's Native-AOT build completes, several coexist for a multi-target version, re-attaching a
/// target replaces it, and — because a native binary is metadata derived from the signed executor, not content — none
/// of it perturbs the content hash or the other entries.
/// </summary>
[TestClass]
public sealed class WorkflowPackageNativeArtifactTests
{
    private static readonly byte[] Workflow = Encoding.UTF8.GetBytes(
        """{"arazzo":"1.1.0","info":{"title":"t","version":"1"},"workflows":[{"workflowId":"wf","steps":[]}]}""");

    private static readonly IReadOnlyList<KeyValuePair<string, byte[]>> Sources =
        [new("pets", Encoding.UTF8.GetBytes("""{"openapi":"3.1.0","info":{"title":"p","version":"1"},"paths":{}}"""))];

    private static readonly byte[] Executor = [0x4D, 0x5A, 1, 2, 3, 4];        // a stand-in executor assembly.
    private static readonly byte[] Manifest = Encoding.UTF8.GetBytes("""{"entryType":"X"}""");
    private static readonly byte[] Signature = Encoding.UTF8.GetBytes("""{"sig":"abc"}""");
    private static readonly byte[] LinuxBinary = [0x7F, (byte)'E', (byte)'L', (byte)'F', 10, 20, 30];
    private static readonly byte[] WindowsBinary = [(byte)'M', (byte)'Z', 40, 50, 60];

    private static byte[] BasePackage() => WorkflowPackage.Pack(
        Workflow, Sources, executor: Executor, executorManifest: Manifest, executorSignature: Signature);

    [TestMethod]
    public void Attaches_a_native_binary_that_reads_back_for_its_target()
    {
        byte[] withNative = WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux-x64", LinuxBinary);

        WorkflowPackage.TryReadNativeArtifact(withNative, "linux-x64", out ReadOnlyMemory<byte> data).ShouldBeTrue();
        data.ToArray().ShouldBe(LinuxBinary);
        WorkflowPackage.TryReadNativeArtifact(withNative, "win-x64", out _).ShouldBeFalse();
    }

    [TestMethod]
    public void Attaching_a_native_leaves_the_content_hash_unchanged()
    {
        byte[] bare = BasePackage();
        byte[] withNative = WorkflowPackage.AttachNativeArtifact(bare, "linux-x64", LinuxBinary);

        // A native binary is metadata (ADR 0055), not content — the hash canonicalises only {workflow, sources}.
        CatalogPackage.HashCanonical(withNative).ShouldBe(CatalogPackage.HashCanonical(bare));
    }

    [TestMethod]
    public void Attaching_a_native_preserves_every_other_entry()
    {
        byte[] withNative = WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux-x64", LinuxBinary);

        WorkflowPackageContents contents = WorkflowPackage.Open(withNative);
        contents.Workflow.ShouldBe(Workflow);
        contents.Sources.Count.ShouldBe(1);
        contents.Sources[0].Key.ShouldBe("pets");
        contents.Sources[0].Value.ShouldBe(Sources[0].Value);
        contents.Executor.ShouldBe(Executor);
        contents.ExecutorManifest.ShouldBe(Manifest);
        contents.ExecutorSignature.ShouldBe(Signature);
    }

    [TestMethod]
    public void Several_targets_coexist_and_enumerate_ordinally()
    {
        byte[] withLinux = WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux-x64", LinuxBinary);
        byte[] both = WorkflowPackage.AttachNativeArtifact(withLinux, "win-x64", WindowsBinary);

        WorkflowPackage.TryReadNativeArtifact(both, "linux-x64", out ReadOnlyMemory<byte> linux).ShouldBeTrue();
        linux.ToArray().ShouldBe(LinuxBinary);
        WorkflowPackage.TryReadNativeArtifact(both, "win-x64", out ReadOnlyMemory<byte> win).ShouldBeTrue();
        win.ToArray().ShouldBe(WindowsBinary);

        WorkflowPackage.EnumerateNativeArtifactRids(both).ShouldBe(["linux-x64", "win-x64"]);
    }

    [TestMethod]
    public void Re_attaching_a_target_replaces_rather_than_duplicates()
    {
        byte[] first = WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux-x64", LinuxBinary);
        byte[] rebuilt = [0x7F, (byte)'E', (byte)'L', (byte)'F', 99];
        byte[] second = WorkflowPackage.AttachNativeArtifact(first, "linux-x64", rebuilt);

        WorkflowPackage.TryReadNativeArtifact(second, "linux-x64", out ReadOnlyMemory<byte> data).ShouldBeTrue();
        data.ToArray().ShouldBe(rebuilt);
        WorkflowPackage.EnumerateNativeArtifactRids(second).ShouldBe(["linux-x64"]);   // one, not two.
    }

    [TestMethod]
    public void Enumerate_is_empty_for_a_package_without_natives()
    {
        WorkflowPackage.EnumerateNativeArtifactRids(BasePackage()).ShouldBeEmpty();
    }

    [TestMethod]
    public void Rejects_an_empty_native_binary()
    {
        Should.Throw<ArgumentException>(() => WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux-x64", default));
    }

    [TestMethod]
    public void Rejects_a_runtime_identifier_bearing_a_path_separator()
    {
        Should.Throw<ArgumentException>(() => WorkflowPackage.AttachNativeArtifact(BasePackage(), "linux/x64", LinuxBinary));
    }

    [TestMethod]
    public void Rejects_an_empty_runtime_identifier()
    {
        Should.Throw<ArgumentException>(() => WorkflowPackage.AttachNativeArtifact(BasePackage(), string.Empty, LinuxBinary));
    }
}