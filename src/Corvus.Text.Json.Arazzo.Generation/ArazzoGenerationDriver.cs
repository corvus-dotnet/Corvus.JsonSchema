// <copyright file="ArazzoGenerationDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.Text;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// Orchestrates end-to-end Arazzo generation: for each OpenAPI source description it generates the
/// client + models and collects the operations, builds the operation binder, then generates the inputs
/// models and executor for every workflow (via <see cref="ArazzoCodeGeneration"/>) and writes everything
/// to disk. This is the testable core behind the <c>arazzo-generate</c> CLI command.
/// </summary>
public static class ArazzoGenerationDriver
{
    /// <summary>
    /// Generates an Arazzo document's workflows (and the OpenAPI clients/models its sources reference).
    /// </summary>
    /// <param name="arazzoFilePath">The path to the Arazzo document (JSON or YAML).</param>
    /// <param name="rootNamespace">The root namespace for all generated code.</param>
    /// <param name="outputPath">The directory to write generated files to.</param>
    /// <param name="clientName">The OpenAPI client name prefix, or <see langword="null"/> for the default.</param>
    /// <param name="durable">When <see langword="true"/>, generate durable (checkpoint &amp; resume) executors.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <param name="registeredDocuments">
    /// Documents the caller has already loaded (the Arazzo document and/or any of its OpenAPI/AsyncAPI
    /// sources), keyed by the absolute URI they should resolve as. A <c>sourceDescriptions[].url</c> (or
    /// the Arazzo document itself) that resolves to a registered URI is taken from memory instead of the
    /// file system — so source documents may come from anywhere. Unregistered references fall back to the
    /// file system (and <c>http(s)</c>). <see langword="null"/> or empty means file-system loading only.
    /// </param>
    /// <param name="progress">An optional callback invoked with human-readable progress messages.</param>
    /// <returns>The absolute paths of all files written.</returns>
    public static async Task<IReadOnlyList<string>> GenerateAsync(
        string arazzoFilePath,
        string rootNamespace,
        string outputPath,
        string? clientName,
        bool durable,
        CancellationToken cancellationToken,
        IReadOnlyList<RegisteredDocument>? registeredDocuments = null,
        Action<string>? progress = null)
    {
        Func<Uri, byte[]?> documentLoader = BuildDocumentLoader(registeredDocuments);

        // The Arazzo document is itself resolved through the loader (keyed by its retrieval URI), so it too
        // can be supplied in memory; otherwise the loader reads (and YAML-converts) it from disk.
        Uri arazzoRetrievalUri = new(Path.GetFullPath(arazzoFilePath));
        byte[] arazzoBytes = documentLoader(arazzoRetrievalUri)
            ?? throw new FileNotFoundException($"The Arazzo document '{arazzoFilePath}' could not be loaded.", arazzoFilePath);

        Uri baseUri = ComputeBaseUri(arazzoBytes, arazzoRetrievalUri);
        var written = new List<string>();

        // Cross-document bookkeeping: `ancestors` is the current recursion stack (a document that
        // references one of its own ancestors is a true cycle), while `generated` maps each already-
        // generated Arazzo document's identity to the namespace its workflows landed in — so a document
        // reachable by several paths (a diamond) is generated exactly once and every referrer reuses it.
        var ancestors = new HashSet<string>(StringComparer.Ordinal);
        var generated = new Dictionary<string, string>(StringComparer.Ordinal);

        await GenerateDocumentAsync(
            arazzoBytes, baseUri, rootNamespace, outputPath, clientName, durable, documentLoader, ancestors, generated, written, cancellationToken, progress)
            .ConfigureAwait(false);

        return written;
    }

    /// <summary>
    /// Generates one Arazzo document (and the OpenAPI/AsyncAPI clients its sources reference), recursing
    /// into <c>arazzo</c>-type sources so cross-document sub-workflows
    /// (<c>$sourceDescriptions.&lt;name&gt;.&lt;workflowId&gt;</c>) have a generated executor to call. Written
    /// file paths accumulate into <paramref name="written"/>. <paramref name="ancestors"/> (the recursion
    /// stack) detects true cycles; <paramref name="generated"/> (identity → workflows namespace) ensures
    /// each referenced document is generated only once and shared by every referrer.
    /// </summary>
    private static async Task GenerateDocumentAsync(
        byte[] arazzoBytes,
        Uri baseUri,
        string rootNamespace,
        string outputPath,
        string? clientName,
        bool durable,
        Func<Uri, byte[]?> documentLoader,
        HashSet<string> ancestors,
        Dictionary<string, string> generated,
        List<string> written,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        ancestors.Add(baseUri.AbsoluteUri);
        try
        {
            await GenerateDocumentCoreAsync(
                arazzoBytes, baseUri, rootNamespace, outputPath, clientName, durable, documentLoader, ancestors, generated, written, cancellationToken, progress)
                .ConfigureAwait(false);
        }
        finally
        {
            ancestors.Remove(baseUri.AbsoluteUri);
        }
    }

    private static async Task GenerateDocumentCoreAsync(
        byte[] arazzoBytes,
        Uri baseUri,
        string rootNamespace,
        string outputPath,
        string? clientName,
        bool durable,
        Func<Uri, byte[]?> documentLoader,
        HashSet<string> ancestors,
        Dictionary<string, string> generated,
        List<string> written,
        CancellationToken cancellationToken,
        Action<string>? progress)
    {
        // Generate the client/models for each source description and collect its operations (OpenAPI),
        // channel operations (AsyncAPI), or recursively the workflows of an arazzo-type source.
        var clients = new List<SourceDescriptionClient>();
        var channelSources = new List<SourceDescriptionChannels>();
        var subWorkflowNamespaces = new Dictionary<string, string>(StringComparer.Ordinal);

        using (ParsedJsonDocument<ArazzoDocument> document = ParsedJsonDocument<ArazzoDocument>.Parse(arazzoBytes))
        {
            ArazzoDocument arazzo = document.RootElement;

            if (arazzo.SourceDescriptions.IsNotUndefined())
            {
                foreach (ArazzoDocument.SourceDescriptionObject source in arazzo.SourceDescriptions.EnumerateArray())
                {
                    if (!source.Name.IsNotUndefined() || !source.Url.IsNotUndefined())
                    {
                        continue;
                    }

                    // An unspecified type defaults to OpenAPI. OpenAPI sources produce operations; AsyncAPI
                    // sources produce channel operations; arazzo sources are generated recursively so their
                    // workflows can be invoked cross-document.
                    string sourceType = source.Type.IsNotUndefined() ? source.Type.GetString()! : "openapi";

                    string name = source.Name.GetString()!;
                    string url = source.Url.GetString()!;

                    // Per Arazzo §5.6, the source url resolves as a URI reference (RFC 3986) against this
                    // description's base URI.
                    Uri specUri = new(baseUri, url);
                    string sourceSegment = ToIdentifier(name);
                    string sourceNamespace = $"{rootNamespace}.{sourceSegment}";
                    string sourceOutput = Path.Combine(outputPath, sourceSegment);

                    if (sourceType == "openapi")
                    {
                        IReadOnlyList<OpenApi.CodeGeneration.OperationDescriptor> operations = await OpenApiSourceGenerator
                            .GenerateAsync(specUri, documentLoader, sourceNamespace, sourceOutput, clientName, cancellationToken, progress)
                            .ConfigureAwait(false);

                        clients.Add(new SourceDescriptionClient(name, OperationResolver.Create(name, operations)));
                    }
                    else if (sourceType == "asyncapi")
                    {
                        IReadOnlyList<AsyncApiChannelDescriptor> channels = await AsyncApiSourceGenerator
                            .GenerateAsync(specUri, documentLoader, sourceNamespace, sourceOutput, cancellationToken, progress)
                            .ConfigureAwait(false);

                        channelSources.Add(new SourceDescriptionChannels(name, channels));
                    }
                    else if (sourceType == "arazzo")
                    {
                        byte[] childBytes = documentLoader(specUri)
                            ?? throw new FileNotFoundException($"The Arazzo source document '{specUri}' could not be loaded.");

                        Uri childBaseUri = ComputeBaseUri(childBytes, specUri);
                        string childIdentity = childBaseUri.AbsoluteUri;

                        if (generated.TryGetValue(childIdentity, out string? existingNamespace))
                        {
                            // Already generated via another reference (a diamond): reuse it, don't regenerate.
                            subWorkflowNamespaces[name] = existingNamespace;
                        }
                        else if (ancestors.Contains(childIdentity))
                        {
                            throw new InvalidOperationException(
                                $"A cyclic Arazzo source-description reference was detected at '{childBaseUri}'.");
                        }
                        else
                        {
                            await GenerateDocumentAsync(
                                childBytes, childBaseUri, sourceNamespace, sourceOutput, clientName, durable, documentLoader, ancestors, generated, written, cancellationToken, progress)
                                .ConfigureAwait(false);

                            string childNamespace = $"{sourceNamespace}.{ArazzoCodeGeneration.DefaultWorkflowsNamespaceSuffix}";
                            generated[childIdentity] = childNamespace;
                            subWorkflowNamespaces[name] = childNamespace;
                        }
                    }
                }
            }
        }

        var binder = new WorkflowOperationBinder(clients, channelSources);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration
            .GenerateAsync(
                arazzoBytes,
                binder,
                new ArazzoGenerationOptions(
                    rootNamespace,
                    Durable: durable,
                    SubWorkflowSourceNamespaces: subWorkflowNamespaces.Count > 0 ? subWorkflowNamespaces : null),
                cancellationToken)
            .ConfigureAwait(false);

        foreach (GeneratedModelFile file in files)
        {
            string path = Path.Combine(outputPath, file.FileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.Content, cancellationToken).ConfigureAwait(false);
            written.Add(path);
        }
    }

    /// <summary>
    /// Computes an Arazzo document's base URI for relative reference resolution (§5.6): an absolute
    /// <c>$self</c> when the document declares one, otherwise its retrieval URI.
    /// </summary>
    private static Uri ComputeBaseUri(byte[] arazzoBytes, Uri retrievalUri)
        => TryReadSelfIdentity(arazzoBytes) is { } self && Uri.TryCreate(self, UriKind.Absolute, out Uri? selfUri)
            ? selfUri
            : retrievalUri;

    /// <summary>
    /// Builds the document loader the generation pipeline resolves every document through: registered
    /// (in-memory) documents — by their registration URI, then by their declared <c>$self</c> identity
    /// (Arazzo §5.5.2) — then the local file system (YAML auto-converted), then <c>http(s)</c>.
    /// </summary>
    private static Func<Uri, byte[]?> BuildDocumentLoader(IReadOnlyList<RegisteredDocument>? registeredDocuments)
    {
        Dictionary<string, byte[]>? registry = null;

        // Identity index (Arazzo §5.5.2): a registered document that declares a top-level absolute $self
        // also resolves under that $self URI, so an absolute sourceDescriptions[].url is matched by
        // identity rather than by where the document happens to have been registered.
        Dictionary<string, byte[]>? bySelf = null;
        if (registeredDocuments is { Count: > 0 })
        {
            registry = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (RegisteredDocument document in registeredDocuments)
            {
                registry[document.Uri.AbsoluteUri] = document.Content;

                if (TryReadSelfIdentity(document.Content) is { } self
                    && Uri.TryCreate(self, UriKind.Absolute, out Uri? selfUri))
                {
                    (bySelf ??= new Dictionary<string, byte[]>(StringComparer.Ordinal))[selfUri.AbsoluteUri] = document.Content;
                }
            }
        }

        return uri =>
        {
            if (registry is not null && registry.TryGetValue(uri.AbsoluteUri, out byte[]? registered))
            {
                return registered;
            }

            if (bySelf is not null && bySelf.TryGetValue(uri.AbsoluteUri, out byte[]? byIdentity))
            {
                return byIdentity;
            }

            if (uri.IsFile)
            {
                string path = uri.LocalPath;
                if (!File.Exists(path))
                {
                    return null;
                }

                byte[] fileBytes = File.ReadAllBytes(path);
                return IsYamlFile(path) ? YamlToJson(fileBytes) : fileBytes;
            }

            if (uri.Scheme is "http" or "https")
            {
                try
                {
                    using HttpClient http = new();
                    return http.GetByteArrayAsync(uri).GetAwaiter().GetResult();
                }
                catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or InvalidOperationException)
                {
                    return null;
                }
            }

            return null;
        };
    }

    /// <summary>
    /// Reads a document's top-level <c>$self</c> identity (Arazzo §5.5.2; also present on OpenAPI 3.1+),
    /// or <see langword="null"/> when it declares none or is not parseable JSON.
    /// </summary>
    private static string? TryReadSelfIdentity(byte[] content)
    {
        try
        {
            using ParsedJsonDocument<JsonElement> document = ParsedJsonDocument<JsonElement>.Parse(content);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty("$self"u8, out JsonElement self)
                && self.ValueKind == JsonValueKind.String
                ? self.GetString()
                : null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }
    }

    private static string ToIdentifier(string value)
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
            return "Source";
        }

        if (char.IsDigit(builder[0]))
        {
            builder.Insert(0, '_');
        }

        return builder.ToString();
    }

    private static byte[] YamlToJson(byte[] yamlBytes)
    {
        YamlPreProcessor preProcessor = new();
        using MemoryStream input = new(yamlBytes);
        using Stream processed = preProcessor.Process(input);
        using MemoryStream output = new();
        processed.CopyTo(output);
        return output.ToArray();
    }

    private static bool IsYamlFile(string path)
    {
        string ext = Path.GetExtension(path);
        return ext.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// A document the caller supplies to Arazzo generation in memory, keyed by the absolute URI it should
/// resolve as — so OpenAPI/AsyncAPI source documents (and the Arazzo document itself) can come from
/// anywhere (a build artifact store, an embedded resource, a network fetch the caller already made)
/// rather than the local file system.
/// </summary>
/// <param name="Uri">
/// The absolute URI this document resolves as. It must match the URI a <c>sourceDescriptions[].url</c>
/// resolves to against the Arazzo description's base URI (or the Arazzo document's own retrieval URI).
/// </param>
/// <param name="Content">The document's raw UTF-8 JSON bytes.</param>
public readonly record struct RegisteredDocument(Uri Uri, byte[] Content);

#endif