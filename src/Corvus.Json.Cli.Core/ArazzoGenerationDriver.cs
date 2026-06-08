// <copyright file="ArazzoGenerationDriver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using System.Text;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.Arazzo.CodeGeneration;
using Corvus.Text.Json.Arazzo11;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Orchestrates end-to-end Arazzo generation: for each OpenAPI source description it generates the
/// client + models and collects the operations, builds the operation binder, then generates the inputs
/// models and executor for every workflow (via <see cref="ArazzoCodeGeneration"/>) and writes everything
/// to disk. This is the testable core behind the <c>arazzo-generate</c> CLI command.
/// </summary>
internal static class ArazzoGenerationDriver
{
    /// <summary>
    /// Generates an Arazzo document's workflows (and the OpenAPI clients/models its sources reference).
    /// </summary>
    /// <param name="arazzoFilePath">The path to the Arazzo document (JSON or YAML).</param>
    /// <param name="rootNamespace">The root namespace for all generated code.</param>
    /// <param name="outputPath">The directory to write generated files to.</param>
    /// <param name="clientName">The OpenAPI client name prefix, or <see langword="null"/> for the default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The absolute paths of all files written.</returns>
    public static async Task<IReadOnlyList<string>> GenerateAsync(
        string arazzoFilePath,
        string rootNamespace,
        string outputPath,
        string? clientName,
        CancellationToken cancellationToken)
    {
        byte[] arazzoBytes = await File.ReadAllBytesAsync(arazzoFilePath, cancellationToken).ConfigureAwait(false);
        if (IsYamlFile(arazzoFilePath))
        {
            arazzoBytes = YamlToJson(arazzoBytes);
        }

        string arazzoDirectory = Path.GetDirectoryName(Path.GetFullPath(arazzoFilePath))!;

        // Generate the client/models for each source description and collect its operations (OpenAPI)
        // or channel operations (AsyncAPI).
        var clients = new List<SourceDescriptionClient>();
        var channelSources = new List<SourceDescriptionChannels>();
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

                    // An unspecified type defaults to OpenAPI. OpenAPI sources produce operations;
                    // AsyncAPI sources produce channel operations. Arazzo (sub-workflow) sources are
                    // generated from the same document and need no per-source client.
                    string sourceType = source.Type.IsNotUndefined() ? source.Type.GetString()! : "openapi";

                    string name = source.Name.GetString()!;
                    string url = source.Url.GetString()!;
                    string specPath = Path.GetFullPath(Path.Combine(arazzoDirectory, url));
                    string sourceSegment = ToIdentifier(name);
                    string sourceNamespace = $"{rootNamespace}.{sourceSegment}";
                    string sourceOutput = Path.Combine(outputPath, sourceSegment);

                    if (sourceType == "openapi")
                    {
                        IReadOnlyList<OpenApi.CodeGeneration.OperationDescriptor> operations = await OpenApiSourceGenerator
                            .GenerateAsync(specPath, sourceNamespace, sourceOutput, clientName, cancellationToken)
                            .ConfigureAwait(false);

                        clients.Add(new SourceDescriptionClient(name, OperationResolver.Create(name, operations)));
                    }
                    else if (sourceType == "asyncapi")
                    {
                        IReadOnlyList<AsyncApiChannelDescriptor> channels = await AsyncApiSourceGenerator
                            .GenerateAsync(specPath, sourceNamespace, sourceOutput, cancellationToken)
                            .ConfigureAwait(false);

                        channelSources.Add(new SourceDescriptionChannels(name, channels));
                    }
                }
            }
        }

        var binder = new WorkflowOperationBinder(clients, channelSources);
        IReadOnlyList<GeneratedModelFile> files = await ArazzoCodeGeneration
            .GenerateAsync(arazzoBytes, binder, new ArazzoGenerationOptions(rootNamespace), cancellationToken)
            .ConfigureAwait(false);

        var written = new List<string>(files.Count);
        foreach (GeneratedModelFile file in files)
        {
            string path = Path.Combine(outputPath, file.FileName.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, file.Content, cancellationToken).ConfigureAwait(false);
            written.Add(path);
        }

        return written;
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

#endif