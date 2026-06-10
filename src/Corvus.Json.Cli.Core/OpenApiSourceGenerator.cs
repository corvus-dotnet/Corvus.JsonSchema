// <copyright file="OpenApiSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if NET10_0_OR_GREATER

using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi30;
using Corvus.Text.Json.OpenApi31;
using Corvus.Text.Json.OpenApi32;

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Generates the OpenAPI client + schema models for a single specification file and returns the
/// operation descriptors — the pieces an Arazzo workflow generator needs to bind each step to a
/// generated operation. Wraps the same schema-type generation the <c>openapi generate</c> command uses.
/// </summary>
internal static class OpenApiSourceGenerator
{
    /// <summary>
    /// Generates the client and models for one OpenAPI source description and returns its operations.
    /// </summary>
    /// <param name="specFilePath">The absolute path to the OpenAPI spec file.</param>
    /// <param name="rootNamespace">The root namespace for the generated client/models.</param>
    /// <param name="outputPath">The directory the client + models are written to (models under <c>Models/</c>).</param>
    /// <param name="clientName">The client name prefix, or <see langword="null"/> for the default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operations the spec declares, with generated request/response/client type names.</returns>
    public static async Task<IReadOnlyList<OperationDescriptor>> GenerateAsync(
        string specFilePath,
        string rootNamespace,
        string outputPath,
        string? clientName,
        CancellationToken cancellationToken)
    {
        bool useYaml = IsYamlFile(specFilePath);
        byte[] specBytes = await File.ReadAllBytesAsync(specFilePath, cancellationToken).ConfigureAwait(false);
        if (useYaml)
        {
            specBytes = YamlToJson(specBytes);
        }

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;
        using ExternalReferenceResolver resolver = new(specRoot, specFilePath);

        return await GenerateCoreAsync(
            specRoot, resolver, specFilePath, entryUri: null, documentLoader: null, useYaml,
            rootNamespace, outputPath, clientName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Generates the client and models for one OpenAPI source description whose document — and any it
    /// references — are supplied through a virtualized loader rather than read from disk, returning its
    /// operations. The document the loader returns for <paramref name="specUri"/> is the entry spec.
    /// </summary>
    /// <param name="specUri">The absolute URI the OpenAPI spec was retrieved from (its reference-resolution base).</param>
    /// <param name="documentLoader">Loads a document's raw UTF-8 JSON bytes by absolute URI, or returns <see langword="null"/>.</param>
    /// <param name="rootNamespace">The root namespace for the generated client/models.</param>
    /// <param name="outputPath">The directory the client + models are written to (models under <c>Models/</c>).</param>
    /// <param name="clientName">The client name prefix, or <see langword="null"/> for the default.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The operations the spec declares, with generated request/response/client type names.</returns>
    public static async Task<IReadOnlyList<OperationDescriptor>> GenerateAsync(
        Uri specUri,
        Func<Uri, byte[]?> documentLoader,
        string rootNamespace,
        string outputPath,
        string? clientName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(specUri);
        ArgumentNullException.ThrowIfNull(documentLoader);

        byte[] specBytes = documentLoader(specUri)
            ?? throw new FileNotFoundException($"The OpenAPI source document '{specUri}' could not be loaded.");

        using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(specBytes);
        JsonElement specRoot = doc.RootElement;
        using ExternalReferenceResolver resolver = new(specRoot, specUri, documentLoader);

        return await GenerateCoreAsync(
            specRoot, resolver, specUri.AbsoluteUri, specUri, documentLoader, useYaml: false,
            rootNamespace, outputPath, clientName, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<IReadOnlyList<OperationDescriptor>> GenerateCoreAsync(
        JsonElement specRoot,
        ExternalReferenceResolver resolver,
        string schemaEntryKey,
        Uri? entryUri,
        Func<Uri, byte[]?>? documentLoader,
        bool useYaml,
        string rootNamespace,
        string outputPath,
        string? clientName,
        CancellationToken cancellationToken)
    {
        string version = OpenApiCommandHelpers.DetectSpecVersion(specRoot, null);
        string modelsPath = Path.Combine(outputPath, "Models");

        IReadOnlyList<GeneratedFile> clientFiles;
        IReadOnlyList<OperationDescriptor> operations;

        if (version is "3.2")
        {
            SchemaReference[] schemaRefs = OpenApi32CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, null, resolver);
            Dictionary<string, string> schemaTypeMap = await ResolveSchemaTypesAsync(schemaEntryKey, version, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, entryUri, documentLoader, cancellationToken).ConfigureAwait(false);
            OpenApi32CodeGenerator generator = new(rootNamespace, schemaTypeMap, clientName, false);
            clientFiles = generator.Generate(specRoot, null, resolver);
            operations = generator.DescribeOperations(specRoot, null, resolver);
        }
        else if (version is "3.0")
        {
            SchemaReference[] schemaRefs = OpenApi30CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, null, resolver);
            Dictionary<string, string> schemaTypeMap = await ResolveSchemaTypesAsync(schemaEntryKey, version, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, entryUri, documentLoader, cancellationToken).ConfigureAwait(false);
            OpenApi30CodeGenerator generator = new(rootNamespace, schemaTypeMap, clientName, false);
            clientFiles = generator.Generate(specRoot, null, resolver);
            operations = generator.DescribeOperations(specRoot, null, resolver);
        }
        else
        {
            SchemaReference[] schemaRefs = OpenApi31CodeGenerator.CollectSchemaPointers(specRoot, out var parameterNames, null, resolver);
            Dictionary<string, string> schemaTypeMap = await ResolveSchemaTypesAsync(schemaEntryKey, version, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, entryUri, documentLoader, cancellationToken).ConfigureAwait(false);
            OpenApi31CodeGenerator generator = new(rootNamespace, schemaTypeMap, clientName, false);
            clientFiles = generator.Generate(specRoot, null, resolver);
            operations = generator.DescribeOperations(specRoot, null, resolver);
        }

        Directory.CreateDirectory(outputPath);
        foreach (GeneratedFile file in clientFiles)
        {
            await File.WriteAllTextAsync(Path.Combine(outputPath, file.FileName), file.Content, cancellationToken).ConfigureAwait(false);
        }

        return operations;
    }

    private static async Task<Dictionary<string, string>> ResolveSchemaTypesAsync(
        string specFilePath,
        string version,
        string rootNamespace,
        string modelsPath,
        SchemaReference[] schemaRefs,
        Dictionary<string, string> parameterNames,
        bool useYaml,
        Uri? entryUri,
        Func<Uri, byte[]?>? documentLoader,
        CancellationToken cancellationToken)
    {
        if (schemaRefs.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        (Dictionary<string, string> schemaTypeMap, _) = await OpenApiSchemaTypeGeneration.GenerateSchemaTypesAsync(
            specFilePath, version, rootNamespace, modelsPath, schemaRefs, parameterNames, useYaml, cancellationToken, entryUri, documentLoader)
            .ConfigureAwait(false);
        return schemaTypeMap;
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