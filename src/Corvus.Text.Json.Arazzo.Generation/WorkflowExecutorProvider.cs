// <copyright file="WorkflowExecutorProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.Buffers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.Validator;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// The code-generation implementation of <see cref="IWorkflowExecutorProvider"/>: at catalog-add time it
/// generates the OpenAPI/AsyncAPI clients + the workflow executor for a package entirely in memory, compiles
/// them into a single release assembly, and emits a manifest binding that assembly to the package version so a
/// runner can dynamically load and run the workflow without re-generating or re-compiling it.
/// </summary>
public sealed class WorkflowExecutorProvider : IWorkflowExecutorProvider
{
    /// <summary>The manifest format version this provider emits.</summary>
    public const int ManifestFormatVersion = 1;

    private const string TargetFramework = "net10.0";
    private const string RootNamespace = "Corvus.Workflows.Generated";
    private const string WorkflowsNamespaceSuffix = "Workflows";

    private readonly bool durable;
    private readonly Action<string>? progress;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkflowExecutorProvider"/> class.
    /// </summary>
    /// <param name="durable">When <see langword="true"/> (the default), generate durable (checkpoint &amp; resume)
    /// executors so an execution host can suspend and resume runs; otherwise generate straight-line executors.</param>
    /// <param name="progress">An optional callback invoked with human-readable progress messages during a build.</param>
    public WorkflowExecutorProvider(bool durable = true, Action<string>? progress = null)
    {
        this.durable = durable;
        this.progress = progress;
    }

    /// <inheritdoc/>
    public WorkflowExecutorArtifact? BuildExecutor(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        string packageHash)
    {
        ArgumentNullException.ThrowIfNull(sources);
        ArgumentException.ThrowIfNullOrEmpty(packageHash);

        try
        {
            return this.BuildCore(workflowUtf8, sources, packageHash);
        }
        catch (Exception ex) when (ex is not OperationCanceledException and not OutOfMemoryException)
        {
            // A package that cannot be generated or compiled is still catalogued — just not runnable.
            this.progress?.Invoke($"Executor build skipped: {ex.Message}");
            return null;
        }
    }

    private static (string? WorkflowId, string? Self, IReadOnlyList<(string Name, string Url, string Type)> Sources) ReadWorkflowFacts(ReadOnlyMemory<byte> workflowUtf8)
    {
        using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(workflowUtf8);
        JsonElement root = document.RootElement;

        string? self = root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("$self"u8, out JsonElement selfEl)
            && selfEl.ValueKind == JsonValueKind.String
                ? selfEl.GetString()
                : null;

        string? workflowId = null;
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("workflows"u8, out JsonElement workflows)
            && workflows.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement wf in workflows.EnumerateArray())
            {
                if (wf.TryGetProperty("workflowId"u8, out JsonElement id) && id.GetString() is { Length: > 0 } value)
                {
                    workflowId = value;
                }

                break;
            }
        }

        var sourceList = new List<(string, string, string)>();
        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("sourceDescriptions"u8, out JsonElement descriptions)
            && descriptions.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement source in descriptions.EnumerateArray())
            {
                if (source.TryGetProperty("name"u8, out JsonElement name) && name.GetString() is { Length: > 0 } nameValue
                    && source.TryGetProperty("url"u8, out JsonElement url) && url.GetString() is { Length: > 0 } urlValue)
                {
                    string type = source.TryGetProperty("type"u8, out JsonElement t) && t.GetString() is { Length: > 0 } typeValue
                        ? typeValue
                        : "openapi";
                    sourceList.Add((nameValue, urlValue, type));
                }
            }
        }

        return (workflowId, self, sourceList);
    }

    private WorkflowExecutorArtifact? BuildCore(
        ReadOnlyMemory<byte> workflowUtf8,
        IReadOnlyList<KeyValuePair<string, byte[]>> sources,
        string packageHash)
    {
        (string? workflowId, string? self, IReadOnlyList<(string Name, string Url, string Type)> sourceRefs) =
            ReadWorkflowFacts(workflowUtf8);

        if (workflowId is null)
        {
            this.progress?.Invoke("Executor build skipped: the workflow has no workflowId.");
            return null;
        }

        // An arazzo-type source is a cross-document sub-workflow whose document is a separate catalog
        // version, not part of this package — so this package cannot be compiled into a self-contained
        // assembly. Catalogue it as not-runnable.
        if (sourceRefs.Any(s => string.Equals(s.Type, "arazzo", StringComparison.OrdinalIgnoreCase)))
        {
            this.progress?.Invoke("Executor build skipped: cross-document (arazzo) sources are not self-contained.");
            return null;
        }

        // Resolve every document by URI in memory. The Arazzo document is registered under a synthetic
        // non-file retrieval URI so its relative source urls resolve to registered (in-memory) documents
        // rather than the local file system; if the document declares a $self that is its resolution base.
        var retrievalUri = new Uri($"https://catalog.corvus.invalid/{packageHash}/workflow.json");
        Uri baseUri = self is not null && Uri.TryCreate(self, UriKind.Absolute, out Uri? selfUri) ? selfUri : retrievalUri;

        var sourcesByName = sources.ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        var registered = new List<RegisteredDocument> { new(retrievalUri, workflowUtf8.ToArray()) };
        foreach ((string name, string url, _) in sourceRefs)
        {
            if (!sourcesByName.TryGetValue(name, out byte[]? bytes))
            {
                this.progress?.Invoke($"Executor build skipped: source '{name}' is declared but absent from the package.");
                return null;
            }

            registered.Add(new RegisteredDocument(new Uri(baseUri, url), bytes));
        }

        string outputPath = Path.Combine(Path.GetTempPath(), "corvus-executor-build", Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(outputPath);

            this.progress?.Invoke($"Generating executor for '{workflowId}'...");
            ArazzoGenerationDriver.GenerateAsync(
                retrievalUri, RootNamespace, outputPath, clientName: null, this.durable,
                CancellationToken.None, registered, this.progress)
                .GetAwaiter().GetResult();

            var generatedCode = new List<GeneratedCodeFile>();
            foreach (string file in Directory.EnumerateFiles(outputPath, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(outputPath, file).Replace('\\', '/');
                generatedCode.Add(new GeneratedCodeFile(relative, File.ReadAllText(file)));
            }

            if (generatedCode.Count == 0)
            {
                this.progress?.Invoke("Executor build skipped: generation produced no code.");
                return null;
            }

            this.progress?.Invoke($"Compiling {generatedCode.Count} generated file(s)...");
            byte[] assembly = DynamicCompiler.CompileToAssemblyBytes(generatedCode, typeof(WorkflowExecutorProvider).Assembly);

            // A durable build emits a host adapter ({ClassName}Host : IHostedWorkflow) the runner activates;
            // a non-durable build has only the static executor class.
            string entryType = $"{RootNamespace}.{WorkflowsNamespaceSuffix}.{ToPascalCase(workflowId)}Workflow{(this.durable ? "Host" : string.Empty)}";
            byte[] manifest = BuildManifest(assembly, packageHash, workflowId, entryType, this.durable, sourceRefs);

            this.progress?.Invoke($"Executor built for '{workflowId}' ({assembly.Length} bytes).");
            return new WorkflowExecutorArtifact(assembly, manifest);
        }
        finally
        {
            TryDeleteDirectory(outputPath);
        }
    }

    private static byte[] BuildManifest(
        byte[] assembly,
        string packageHash,
        string workflowId,
        string entryType,
        bool durable,
        IReadOnlyList<(string Name, string Url, string Type)> sourceRefs)
    {
        string assemblyDigest = "sha256:" + Convert.ToHexStringLower(SHA256.HashData(assembly));

        var buffer = new ArrayBufferWriter<byte>();
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            // Keys are written in a stable (alphabetical) order so the manifest is reproducible.
            writer.WriteStartObject();
            writer.WriteString("assemblyDigest"u8, assemblyDigest);
            writer.WriteBoolean("durable"u8, durable);
            writer.WriteString("entryType"u8, entryType);
            writer.WriteNumber("formatVersion"u8, ManifestFormatVersion);
            writer.WriteString("packageHash"u8, packageHash);
            writer.WriteString("rootNamespace"u8, RootNamespace);

            writer.WritePropertyName("sources"u8);
            writer.WriteStartArray();
            foreach ((string name, _, string type) in sourceRefs)
            {
                writer.WriteStartObject();
                writer.WriteString("name"u8, name);
                writer.WriteString("type"u8, type);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();

            writer.WriteString("targetFramework"u8, TargetFramework);
            writer.WriteString("workflowId"u8, workflowId);
            writer.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    /// <summary>
    /// Produces the PascalCase executor class stem from a workflow id, treating any run of non-alphanumeric
    /// characters as a word boundary. Mirrors the Arazzo code generator's class-naming
    /// (<c>EmitText.ToPascalCase</c>); the integration test asserts the derived entry type resolves in the
    /// compiled assembly, so divergence cannot go unnoticed.
    /// </summary>
    private static string ToPascalCase(string value)
    {
        var builder = new StringBuilder(value.Length);
        bool upperNext = true;
        foreach (char c in value)
        {
            if (!char.IsLetterOrDigit(c))
            {
                upperNext = true;
                continue;
            }

            builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
            upperNext = false;
        }

        if (builder.Length == 0)
        {
            return "_";
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of the temporary build directory.
        }
        catch (UnauthorizedAccessException)
        {
            // Best-effort cleanup of the temporary build directory.
        }
    }
}

#endif