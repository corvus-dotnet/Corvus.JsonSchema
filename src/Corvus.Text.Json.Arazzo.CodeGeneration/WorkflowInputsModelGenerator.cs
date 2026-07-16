// <copyright file="WorkflowInputsModelGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.Draft202012;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Generates a strongly-typed C# model for a workflow's <c>inputs</c> JSON Schema, and the accessor map
/// (input JSON name → generated dotnet property) the executor emitter needs to compile
/// <c>$inputs.&lt;name&gt;</c> to a strongly-typed accessor.
/// </summary>
/// <remarks>
/// This drives the Corvus JSON Schema code generator programmatically — the same engine the OpenAPI CLI
/// uses (<c>JsonSchemaTypeBuilder.GenerateCodeUsing</c>) — over the inline inputs schema at
/// <c>#/workflows/&lt;index&gt;/inputs</c>. The schema is an inline JSON Schema 2020-12 object, so the
/// default 2020-12 vocabulary is supplied directly and no metaschema document is required.
/// </remarks>
public static class WorkflowInputsModelGenerator
{
    // An absolute URI so JsonReference treats the in-memory document as an opaque registered document
    // rather than resolving it as a filesystem path.
    private const string DocumentUri = "https://corvus.local/arazzo/document.json";

    /// <summary>
    /// Generates the inputs model for a workflow.
    /// </summary>
    /// <param name="arazzoDocumentUtf8">The full Arazzo document, as UTF-8 JSON.</param>
    /// <param name="workflowIndex">The zero-based index of the workflow within the document's <c>workflows</c> array.</param>
    /// <param name="modelNamespace">The namespace for the generated model types.</param>
    /// <param name="schemaDocuments">External JSON Schema documents (name → UTF-8) the inputs schema may reference
    /// by <c>schemas/&lt;name&gt;#&lt;pointer&gt;</c> (design §6, #94); each registers as a sibling of the Arazzo
    /// document so the relative reference resolves with no rewriting. <see langword="null"/> when there are none.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated model (type name, accessor map, and source files), or <see langword="null"/> if the workflow declares no inputs object with properties.</returns>
    public static async ValueTask<WorkflowInputsModel?> GenerateAsync(
        ReadOnlyMemory<byte> arazzoDocumentUtf8,
        int workflowIndex,
        string modelNamespace,
        IReadOnlyList<KeyValuePair<string, byte[]>>? schemaDocuments = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(modelNamespace);

        JsonDocument document = JsonDocument.Parse(arazzoDocumentUtf8);
        var externalDocuments = new List<JsonDocument>();
        var resolver = new PrepopulatedDocumentResolver();
        try
        {
            resolver.AddDocument(DocumentUri, document);

            // External schema documents (design §6, #94). A document that declares an absolute root $id is
            // canonically identified by it (JSON Schema 2020-12) — the PREFERRED authored reference form —
            // so it registers under that $id. Every document ALSO registers under the virtual sibling URI
            // (schemas/<name>), the fallback form for a document with no $id; either authored form resolves.
            if (schemaDocuments is not null)
            {
                foreach ((string name, byte[] bytes) in schemaDocuments)
                {
                    JsonDocument external = JsonDocument.Parse(bytes);
                    externalDocuments.Add(external);
                    resolver.AddDocument("https://corvus.local/arazzo/schemas/" + name, external);
                    if (TryGetRootId(external, out string? id))
                    {
                        resolver.AddDocument(id, external);
                    }
                }
            }

            var vocabularyRegistry = new VocabularyRegistry();
            VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);

            var typeBuilder = new JsonSchemaTypeBuilder(resolver, vocabularyRegistry);
            var reference = new JsonReference(
                DocumentUri,
                $"#/workflows/{workflowIndex.ToString(CultureInfo.InvariantCulture)}/inputs");

            TypeDeclaration root = await typeBuilder
                .AddTypeDeclarationsAsync(reference, VocabularyAnalyser.DefaultVocabulary, rebaseAsRoot: false)
                .ConfigureAwait(false);

            var options = new CSharpLanguageProvider.Options(modelNamespace);
            CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
            IReadOnlyCollection<GeneratedCodeFile> generated =
                typeBuilder.GenerateCodeUsing(languageProvider, [root], cancellationToken);

            TypeDeclaration reduced = root.ReducedTypeDeclaration().ReducedType;
            if (!reduced.HasDotnetTypeName())
            {
                return null;
            }

            string typeName = reduced.FullyQualifiedDotnetTypeName();

            var accessors = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (PropertyDeclaration property in reduced.PropertyDeclarations)
            {
                accessors[property.JsonPropertyName] = property.DotnetPropertyName();
            }

            var files = new List<GeneratedModelFile>(generated.Count);
            foreach (GeneratedCodeFile file in generated)
            {
                files.Add(new GeneratedModelFile(file.FileName, file.FileContent));
            }

            return new WorkflowInputsModel(typeName, accessors, files);
        }
        finally
        {
            resolver.Dispose();
            document.Dispose();
            foreach (JsonDocument external in externalDocuments)
            {
                external.Dispose();
            }
        }
    }

    // Reads a schema document's canonical identity: its root `$id` when it declares one that parses as an
    // absolute URI (a relative root `$id` would resolve against the retrieval URI, which is virtual here —
    // the fallback registration already covers that document).
    private static bool TryGetRootId(JsonDocument document, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out string? id)
    {
        id = null;
        if (document.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
            && document.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement)
            && idElement.ValueKind == System.Text.Json.JsonValueKind.String
            && idElement.GetString() is { Length: > 0 } value
            && Uri.TryCreate(value, UriKind.Absolute, out _))
        {
            id = value;
            return true;
        }

        return false;
    }
}

/// <summary>
/// A generated model source file.
/// </summary>
/// <param name="FileName">The suggested file name.</param>
/// <param name="Content">The C# source.</param>
public readonly record struct GeneratedModelFile(string FileName, string Content);

/// <summary>
/// The generated inputs model for a workflow.
/// </summary>
/// <param name="TypeName">The fully-qualified generated inputs type name (the executor's <c>inputs</c> parameter type).</param>
/// <param name="Accessors">Map of input JSON name → generated dotnet accessor property (e.g. <c>petId</c> → <c>PetId</c>).</param>
/// <param name="Files">The generated model source files.</param>
public sealed record WorkflowInputsModel(
    string TypeName,
    IReadOnlyDictionary<string, string> Accessors,
    IReadOnlyList<GeneratedModelFile> Files);