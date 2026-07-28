// <copyright file="IWorkflowAotBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// Native-AOT compiles an assembled serverless host-app to a single native binary for one runtime target (ADR 0055).
/// The implementation runs a build container (Amazon Linux 2023 for the Linux targets); a Windows target needs a
/// Windows build host, since Native AOT does not cross-compile across operating systems, so this is a per-target seam
/// a builder for each target implements. A test fake stands in without a container.
/// </summary>
public interface IWorkflowAotBuilder
{
    /// <summary>
    /// Compiles the assembled host-app to a native binary for its runtime target.
    /// </summary>
    /// <param name="hostApp">The assembled host-app project (the entry stub, the project file, the signed executor assembly, and the feed config).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The build result: the native binary on success, and the build log either way.</returns>
    ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken);
}

/// <summary>The outcome of a serverless host-app build.</summary>
/// <param name="Succeeded">Whether the build produced a deploy artifact.</param>
/// <param name="NativeBinary">The deploy artifact bytes when <paramref name="Succeeded"/> is <see langword="true"/>; otherwise empty. This is the single native binary for a Native-AOT (AWS Lambda) target, or the zipped published isolated-worker app for a ReadyToRun (Azure Functions) target.</param>
/// <param name="Log">The build log (compiler and linker output), for diagnostics whether the build succeeded or failed.</param>
public readonly record struct AotBuildResult(bool Succeeded, ReadOnlyMemory<byte> NativeBinary, string Log)
{
    /// <summary>Creates a successful result.</summary>
    /// <param name="nativeBinary">The deploy artifact bytes (a native binary, or a zipped published app).</param>
    /// <param name="log">The build log.</param>
    /// <returns>The result.</returns>
    public static AotBuildResult Success(ReadOnlyMemory<byte> nativeBinary, string log) => new(true, nativeBinary, log);

    /// <summary>Creates a failed result.</summary>
    /// <param name="log">The build log explaining the failure.</param>
    /// <returns>The result.</returns>
    public static AotBuildResult Failure(string log) => new(false, default, log);
}

/// <summary>
/// A serverless host-app assembled around a version's signed executor, ready to native-AOT compile for one runtime
/// target. The <see cref="Files"/> are written to a working directory verbatim (the entry stub, the project file, the
/// hermetic feed config, and the signed <c>executor.dll</c>) and the target's builder compiles them.
/// </summary>
/// <param name="RuntimeIdentifier">The .NET runtime identifier the binary targets (e.g. <c>linux-x64</c>, <c>win-x64</c>).</param>
/// <param name="EntryType">The baked <c>IHostedWorkflow</c> type the host stub constructs (from the executor manifest).</param>
/// <param name="EngineVersion">The Corvus runtime version the executor was compiled against (from the manifest), which the referenced runtime packages must provide.</param>
/// <param name="Files">The project files, each a relative path and its content.</param>
/// <param name="Target">The serverless platform this host-app is compiled for, which decides the compile form-factor and the published-artifact shape a builder reads back.</param>
public readonly record struct AssembledHostApp(
    string RuntimeIdentifier,
    string EntryType,
    string EngineVersion,
    IReadOnlyList<AotProjectFile> Files,
    ServerlessTarget Target = ServerlessTarget.AwsLambda);

/// <summary>One file in an assembled host-app project.</summary>
/// <param name="RelativePath">The path relative to the project root (forward-slashed).</param>
/// <param name="Content">The file content.</param>
public readonly record struct AotProjectFile(string RelativePath, ReadOnlyMemory<byte> Content);