// <copyright file="ContainerWorkflowAotBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.IO.Compression;

namespace Corvus.Text.Json.Arazzo.Durability.Aot;

/// <summary>
/// An <see cref="IWorkflowAotBuilder"/> that compiles the assembled host-app by running the build image (ADR 0055). It
/// writes the host-app to a working directory, mounts it into the container alongside the package feed, runs a single
/// <c>dotnet publish</c>, and reads back the deploy artifact per target: the single native <c>bootstrap</c> for an AWS
/// Lambda (Native-AOT) build, or the whole published isolated-worker app zipped for an Azure Functions (ReadyToRun)
/// build. It builds the Linux targets; a Windows Native-AOT target needs a Windows build host, since Native AOT does
/// not cross-compile across operating systems.
/// </summary>
public sealed class ContainerWorkflowAotBuilder : IWorkflowAotBuilder
{
    private const string ProjectMountPath = "/work/fn";
    private const string OutputDirectoryName = "out";

    private readonly ContainerAotBuilderOptions options;

    /// <summary>Initializes a new instance of the <see cref="ContainerWorkflowAotBuilder"/> class.</summary>
    /// <param name="options">The container image, CLI, and feed mounts to run the build with.</param>
    public ContainerWorkflowAotBuilder(ContainerAotBuilderOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        this.options = options;
    }

    /// <inheritdoc/>
    public async ValueTask<AotBuildResult> BuildAsync(AssembledHostApp hostApp, CancellationToken cancellationToken)
    {
        string workDirectory = Directory.CreateTempSubdirectory("arazzo-aot-").FullName;
        try
        {
            foreach (AotProjectFile file in hostApp.Files)
            {
                string destination = Path.Combine(workDirectory, file.RelativePath);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                await File.WriteAllBytesAsync(destination, file.Content.ToArray(), cancellationToken).ConfigureAwait(false);
            }

            Directory.CreateDirectory(Path.Combine(workDirectory, OutputDirectoryName));

            IReadOnlyList<string> arguments = this.BuildContainerArguments(workDirectory, hostApp.RuntimeIdentifier, hostApp.Target);
            (int exitCode, string log) = await RunProcessAsync(this.options.ContainerCommand, arguments, cancellationToken).ConfigureAwait(false);

            string outputDirectory = Path.Combine(workDirectory, OutputDirectoryName);
            if (exitCode != 0)
            {
                return AotBuildResult.Failure(log);
            }

            if (hostApp.Target == ServerlessTarget.AzureFunctions)
            {
                // A runtime-present target publishes a framework-dependent isolated-worker app: the deploy artifact is the
                // whole published directory (handler.dll + its dependency assemblies + host.json + functions.metadata +
                // worker.config.json + .azurefunctions/), zipped. The worker assembly's presence is the success signal.
                if (!File.Exists(Path.Combine(outputDirectory, "handler.dll")))
                {
                    return AotBuildResult.Failure(log);
                }

                return AotBuildResult.Success(ZipDirectory(outputDirectory), log);
            }

            // A runtime-less target publishes a single self-contained native binary named 'bootstrap'.
            string bootstrapPath = Path.Combine(outputDirectory, "bootstrap");
            if (!File.Exists(bootstrapPath))
            {
                return AotBuildResult.Failure(log);
            }

            byte[] binary = await File.ReadAllBytesAsync(bootstrapPath, cancellationToken).ConfigureAwait(false);
            return AotBuildResult.Success(binary, log);
        }
        finally
        {
            TryDeleteDirectory(workDirectory);
        }
    }

    // The container CLI arguments to run the build: mount the assembled project read-write (so the publish writes its
    // intermediate and output there), mount the read-only feeds, and publish the one project to the runtime target.
    // Internal so the argument shape is unit-testable without a container.
    internal IReadOnlyList<string> BuildContainerArguments(string workDirectory, string runtimeIdentifier, ServerlessTarget target = ServerlessTarget.AwsLambda)
    {
        var arguments = new List<string> { "run", "--rm" };

        // A writable CLI home for the restore cache; the base image is otherwise read-only-friendly.
        arguments.Add("-e");
        arguments.Add("DOTNET_CLI_HOME=/tmp");

        arguments.Add("-v");
        arguments.Add($"{workDirectory}:{ProjectMountPath}:rw");

        foreach ((string hostPath, string containerPath) in this.options.ReadOnlyMounts)
        {
            arguments.Add("-v");
            arguments.Add($"{hostPath}:{containerPath}:ro");
        }

        arguments.Add(this.options.ContainerImage);
        arguments.Add("dotnet");
        arguments.Add("publish");
        arguments.Add($"{ProjectMountPath}/fn.csproj");
        arguments.Add("-r");
        arguments.Add(runtimeIdentifier);
        arguments.Add("-c");
        arguments.Add("Release");

        if (target == ServerlessTarget.AzureFunctions)
        {
            // Framework-dependent: the dotnet-isolated host provides the runtime (ADR 0061). The csproj sets
            // <SelfContained>false</SelfContained> too, but pass it explicitly so a -r publish never implies
            // self-contained on any SDK.
            arguments.Add("--self-contained");
            arguments.Add("false");
        }

        arguments.Add("-o");
        arguments.Add($"{ProjectMountPath}/{OutputDirectoryName}");
        return arguments;
    }

    private static async Task<(int ExitCode, string Log)> RunProcessAsync(string command, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo(command)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();

        // Read both streams to completion (they close on process exit) alongside the wait, so no output is lost and
        // there is no event-handler race.
        Task<string> standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> standardError = process.StandardError.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string log = string.Concat(await standardOutput.ConfigureAwait(false), await standardError.ConfigureAwait(false));
        return (process.ExitCode, log);
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            // The process already exited, or could not be signalled; nothing more to do.
        }
    }

    // Zips a published isolated-worker app directory into the deploy payload the Azure deployer zip-deploys. Debug
    // symbols are dropped: they are not needed at runtime and only enlarge the deploy (and its cold start).
    private static byte[] ZipDirectory(string directory)
    {
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string entryName = Path.GetRelativePath(directory, file).Replace('\\', '/');
                archive.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
            }
        }

        return memory.ToArray();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // A best-effort cleanup of the working directory; a leftover temp directory is not worth failing a build.
        }
    }
}

/// <summary>The container image, CLI, and feed mounts a <see cref="ContainerWorkflowAotBuilder"/> runs a build with.</summary>
public sealed record ContainerAotBuilderOptions
{
    /// <summary>Gets the read-only host-to-container mounts (host path, container path), e.g. the package feed the build restores from.</summary>
    public required IReadOnlyList<(string HostPath, string ContainerPath)> ReadOnlyMounts { get; init; }

    /// <summary>Gets the build image tag. Defaults to the tag <c>build-aot-builder-image.ps1</c> builds.</summary>
    public string ContainerImage { get; init; } = "arazzo-aot-builder:net10";

    /// <summary>Gets the container CLI command. Defaults to <c>podman</c>.</summary>
    public string ContainerCommand { get; init; } = "podman";
}