// <copyright file="WorkflowExecutorLoader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;

namespace Corvus.Text.Json.Arazzo.Execution;

/// <summary>
/// Loads a compiled workflow executor assembly into a collectible <see cref="AssemblyLoadContext"/> per
/// <c>(baseWorkflowId, versionNumber)</c>, verifies its integrity against the executor manifest, resolves the
/// manifest's entry type to an <see cref="IHostedWorkflow"/>, and caches it for reuse. Unloading a version
/// disposes its load context so a deleted/obsoleted version is evicted promptly.
/// </summary>
/// <remarks>
/// Loading a prebuilt DLL needs no Roslyn or dependency context: the collectible load context resolves the
/// executor assembly from the supplied bytes and falls back to the default context for the shared Corvus
/// runtime assemblies the runner already references — that closure is the whole dependency set, because the
/// clients, executor, and host adapter were compiled into the single assembly.
/// </remarks>
public sealed class WorkflowExecutorLoader : IDisposable
{
    /// <summary>The target framework this runner can load executor assemblies for.</summary>
    public const string SupportedTargetFramework = "net10.0";

    private readonly Lock gate = new();
    private readonly Dictionary<(string BaseWorkflowId, int VersionNumber), LoadedWorkflow> loaded = new();
    private readonly string supportedTargetFramework;
    private readonly IExecutorPackageVerifier? verifier;
    private bool disposed;

    /// <summary>Initializes a new instance of the <see cref="WorkflowExecutorLoader"/> class.</summary>
    /// <param name="supportedTargetFramework">The target framework this runner accepts; defaults to <see cref="SupportedTargetFramework"/>.</param>
    /// <param name="verifier">The package-signature verifier (§3.3): when supplied, every loaded package must carry a signature that verifies against the runner's trust store; when <see langword="null"/> (single-node / trusted deployments) only the hash-based integrity binding is checked.</param>
    public WorkflowExecutorLoader(string? supportedTargetFramework = null, IExecutorPackageVerifier? verifier = null)
    {
        this.supportedTargetFramework = supportedTargetFramework ?? SupportedTargetFramework;
        this.verifier = verifier;
    }

    /// <summary>
    /// Verifies and loads a version's executor assembly (or returns the already-loaded instance). The assembly
    /// is rejected unless its digest matches the manifest, the manifest's package hash matches
    /// <paramref name="expectedPackageHash"/>, and its target framework is supported.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="assembly">The compiled executor assembly bytes (the package's <c>metadata/executor.dll</c>).</param>
    /// <param name="manifestUtf8">The executor manifest as UTF-8 JSON (the package's <c>metadata/executor-manifest.json</c>).</param>
    /// <param name="expectedPackageHash">The catalog version's content hash, which the manifest must bind to.</param>
    /// <param name="signatureUtf8">The detached package signature as UTF-8 JSON (the package's <c>metadata/executor-manifest.sig</c>), or empty when the package is unsigned; required and verified when this loader was given a verifier.</param>
    /// <returns>The loaded, verified, cached workflow.</returns>
    /// <exception cref="WorkflowExecutorLoadException">Integrity or signature verification failed, the target framework is unsupported, or the entry type could not be activated.</exception>
    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Loads a workflow executor from IL at runtime through a collectible AssemblyLoadContext (the in-process dynamic-load path). AOT execution backends do not use this; they bake the executor at build time (ADR 0028).")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Loads a workflow executor from IL at runtime through a collectible AssemblyLoadContext (the in-process dynamic-load path). AOT execution backends do not use this; they bake the executor at build time (ADR 0028).")]
    public LoadedWorkflow Load(
        string baseWorkflowId,
        int versionNumber,
        ReadOnlyMemory<byte> assembly,
        ReadOnlyMemory<byte> manifestUtf8,
        string expectedPackageHash,
        ReadOnlyMemory<byte> signatureUtf8 = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(baseWorkflowId);
        ArgumentException.ThrowIfNullOrEmpty(expectedPackageHash);

        var key = (baseWorkflowId, versionNumber);
        lock (this.gate)
        {
            ObjectDisposedException.ThrowIf(this.disposed, this);
            if (this.loaded.TryGetValue(key, out LoadedWorkflow? existing))
            {
                return existing;
            }

            WorkflowExecutorManifest manifest = ParseAndVerify(assembly, manifestUtf8, signatureUtf8, expectedPackageHash, this.supportedTargetFramework, this.verifier);
            LoadedWorkflow result = Activate(baseWorkflowId, versionNumber, assembly, manifest);
            this.loaded[key] = result;
            return result;
        }
    }

    /// <summary>Gets the already-loaded workflow for a version, if present.</summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <param name="workflow">The loaded workflow when present.</param>
    /// <returns><see langword="true"/> if the version is loaded.</returns>
    public bool TryGet(string baseWorkflowId, int versionNumber, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out LoadedWorkflow? workflow)
    {
        lock (this.gate)
        {
            return this.loaded.TryGetValue((baseWorkflowId, versionNumber), out workflow);
        }
    }

    /// <summary>
    /// Unloads a version: removes it from the cache and disposes its collectible load context. In-flight runs
    /// holding a reference keep the context alive until they release it (the GC collects the context once the
    /// last reference is gone); new resolutions miss the cache.
    /// </summary>
    /// <param name="baseWorkflowId">The base workflow id.</param>
    /// <param name="versionNumber">The version number.</param>
    /// <returns><see langword="true"/> if a loaded version was unloaded.</returns>
    public bool Unload(string baseWorkflowId, int versionNumber)
    {
        lock (this.gate)
        {
            if (this.loaded.Remove((baseWorkflowId, versionNumber), out LoadedWorkflow? existing))
            {
                existing.LoadContext.Unload();
                return true;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        lock (this.gate)
        {
            if (this.disposed)
            {
                return;
            }

            foreach (LoadedWorkflow workflow in this.loaded.Values)
            {
                workflow.LoadContext.Unload();
            }

            this.loaded.Clear();
            this.disposed = true;
        }
    }

    private static WorkflowExecutorManifest ParseAndVerify(
        ReadOnlyMemory<byte> assembly,
        ReadOnlyMemory<byte> manifestUtf8,
        ReadOnlyMemory<byte> signatureUtf8,
        string expectedPackageHash,
        string supportedTargetFramework,
        IExecutorPackageVerifier? verifier)
    {
        WorkflowExecutorManifest manifest;
        try
        {
            manifest = WorkflowExecutorManifest.Parse(manifestUtf8);
        }
        catch (FormatException ex)
        {
            throw new WorkflowExecutorLoadException($"The executor manifest is malformed: {ex.Message}", ex);
        }

        if (!string.Equals(manifest.PackageHash, expectedPackageHash, StringComparison.Ordinal))
        {
            throw new WorkflowExecutorLoadException(
                $"The executor manifest's package hash '{manifest.PackageHash}' does not match the version's content hash '{expectedPackageHash}'.");
        }

        string actualDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(assembly.Span));
        if (!string.Equals(manifest.AssemblyDigest, actualDigest, StringComparison.Ordinal))
        {
            throw new WorkflowExecutorLoadException(
                $"The executor assembly digest '{actualDigest}' does not match the manifest's '{manifest.AssemblyDigest}'.");
        }

        if (!string.Equals(manifest.TargetFramework, supportedTargetFramework, StringComparison.Ordinal))
        {
            throw new WorkflowExecutorLoadException(
                $"The executor targets '{manifest.TargetFramework}', which this runner ('{supportedTargetFramework}') cannot load.");
        }

        // Signature (§3.3): the integrity binding above proves the DLL matches the manifest and the manifest matches
        // the version; the signature proves the manifest itself was produced by the control plane and not tampered
        // with. Verifying it also transitively vouches for the assemblyDigest and packageHash it contains. A runner
        // configured with a verifier rejects any unsigned or badly-signed package; an unconfigured runner (single-node
        // / trusted) skips this and relies on integrity alone.
        if (verifier is not null)
        {
            if (signatureUtf8.IsEmpty)
            {
                throw new WorkflowExecutorLoadException("The executor package is unsigned, but this runner requires a valid signature.");
            }

            ExecutorPackageSignature signature;
            try
            {
                signature = ExecutorPackageSignature.Parse(signatureUtf8);
            }
            catch (FormatException ex)
            {
                throw new WorkflowExecutorLoadException($"The executor signature is malformed: {ex.Message}", ex);
            }

            if (!verifier.Verify(manifestUtf8, signature))
            {
                throw new WorkflowExecutorLoadException(
                    $"The executor manifest's signature (key '{signature.KeyId}', {signature.Algorithm}) did not verify against a trusted key.");
            }
        }

        return manifest;
    }

    [System.Diagnostics.CodeAnalysis.RequiresUnreferencedCode("Loads and activates an executor from IL through a collectible AssemblyLoadContext. AOT execution backends bake the executor at build time and do not call this.")]
    [System.Diagnostics.CodeAnalysis.RequiresDynamicCode("Loads and activates an executor from IL through a collectible AssemblyLoadContext. AOT execution backends bake the executor at build time and do not call this.")]
    private static LoadedWorkflow Activate(string baseWorkflowId, int versionNumber, ReadOnlyMemory<byte> assembly, in WorkflowExecutorManifest manifest)
    {
        var context = new WorkflowAssemblyLoadContext(baseWorkflowId, versionNumber);
        try
        {
            using var stream = new MemoryStream(assembly.ToArray(), writable: false);
            Assembly loadedAssembly = context.LoadFromStream(stream);

            Type entryType = loadedAssembly.GetType(manifest.EntryType)
                ?? throw new WorkflowExecutorLoadException($"The executor manifest's entry type '{manifest.EntryType}' was not found in the assembly.");

            if (Activator.CreateInstance(entryType) is not IHostedWorkflow workflow)
            {
                throw new WorkflowExecutorLoadException($"The executor entry type '{manifest.EntryType}' does not implement IHostedWorkflow.");
            }

            VerifyDeclaredSources(manifest, workflow.Descriptor);
            return new LoadedWorkflow(workflow, manifest, context);
        }
        catch
        {
            context.Unload();
            throw;
        }
    }

    // Fail-fast at load (design §3.3, §546): every API source the compiled workflow actually calls (the descriptor's
    // required bindings) must be declared in the manifest's sources[], so a host that reads the manifest to bind
    // transports sees the complete required set. A manifest that under-declares its assembly's sources is inconsistent —
    // reject it here rather than let a run fail deep in the step that reaches the undeclared binding.
    private static void VerifyDeclaredSources(in WorkflowExecutorManifest manifest, in WorkflowDescriptor descriptor)
    {
        foreach (string required in descriptor.Sources)
        {
            bool declared = false;
            foreach (WorkflowManifestSource source in manifest.Sources)
            {
                if (string.Equals(source.Name, required, StringComparison.Ordinal))
                {
                    declared = true;
                    break;
                }
            }

            if (!declared)
            {
                throw new WorkflowExecutorLoadException(
                    $"The executor manifest does not declare the source '{required}' the workflow requires; its sources[] is inconsistent with the compiled assembly.");
            }
        }
    }

    private sealed class WorkflowAssemblyLoadContext(string baseWorkflowId, int versionNumber)
        : AssemblyLoadContext($"arazzo-executor:{baseWorkflowId}-v{versionNumber}", isCollectible: true)
    {
        // Returning null defers to the default context, which carries the shared Corvus runtime assemblies the
        // runner references — the executor assembly's whole dependency closure.
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}