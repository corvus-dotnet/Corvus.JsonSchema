// <copyright file="SpecDocumentReader.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Internal;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Reads the specification file of an OpenAPI or AsyncAPI command as JSON, whether the file holds JSON or YAML, and
/// creates the resolver for the documents the specification refers to.
/// </summary>
internal static class SpecDocumentReader
{
    /// <summary>
    /// Determines whether a specification file is YAML.
    /// </summary>
    /// <param name="explicitYaml">The value of the command's YAML option, or <see langword="null"/> when it was not given.</param>
    /// <param name="specFile">The specification file.</param>
    /// <returns>The option's value when it was given; otherwise whether the file's extension is <c>.yaml</c> or
    /// <c>.yml</c>.</returns>
    internal static bool UseYaml(bool? explicitYaml, string specFile)
    {
        if (explicitYaml is bool value)
        {
            return value;
        }

        string extension = Path.GetExtension(specFile);
        return extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".yml", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reads a specification file as UTF-8 JSON.
    /// </summary>
    /// <param name="specFile">The specification file.</param>
    /// <param name="useYaml">Whether the file is YAML, to be converted to JSON.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The JSON text of the specification.</returns>
    internal static async Task<byte[]> ReadAsJsonAsync(string specFile, bool useYaml, CancellationToken cancellationToken)
    {
        byte[] specBytes = await File.ReadAllBytesAsync(specFile, cancellationToken).ConfigureAwait(false);
        return useYaml ? ConvertYamlToJson(specBytes) : specBytes;
    }

    /// <summary>
    /// Reads a specification file as UTF-8 JSON.
    /// </summary>
    /// <param name="specFile">The specification file.</param>
    /// <param name="useYaml">Whether the file is YAML, to be converted to JSON.</param>
    /// <returns>The JSON text of the specification.</returns>
    internal static byte[] ReadAsJson(string specFile, bool useYaml)
    {
        byte[] specBytes = File.ReadAllBytes(specFile);
        return useYaml ? ConvertYamlToJson(specBytes) : specBytes;
    }

    /// <summary>
    /// Creates the resolver for the documents a specification refers to (the specification file itself included).
    /// </summary>
    /// <param name="useYaml">Whether those documents are YAML, to be converted to JSON as they are loaded.</param>
    /// <param name="first">A resolver to consult before the file system and HTTP (for example one holding synthesized
    /// documents), or <see langword="null"/>.</param>
    /// <returns>A resolver over <paramref name="first"/>, the file system and HTTP.</returns>
    internal static CompoundDocumentResolver CreateDocumentResolver(bool useYaml, IDocumentResolver? first = null)
    {
        YamlPreProcessor? preProcessor = useYaml ? new() : null;
        FileSystemDocumentResolver fileSystem = preProcessor is null ? new() : new(preProcessor);
        HttpClientDocumentResolver http = preProcessor is null ? new(new HttpClient()) : new(new HttpClient(), preProcessor);
        return first is null ? new(fileSystem, http) : new(first, fileSystem, http);
    }

    private static byte[] ConvertYamlToJson(byte[] specBytes)
    {
        YamlPreProcessor yamlPreProcessor = new();
        using MemoryStream inputStream = new(specBytes);
        using Stream processedStream = yamlPreProcessor.Process(inputStream);
        using MemoryStream outputStream = new();
        processedStream.CopyTo(outputStream);
        return outputStream.ToArray();
    }
}

#endif