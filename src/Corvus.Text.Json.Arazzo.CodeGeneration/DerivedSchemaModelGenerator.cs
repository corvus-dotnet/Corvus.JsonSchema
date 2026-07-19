// <copyright file="DerivedSchemaModelGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.Draft202012;
using Corvus.Text.Json.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Drives the Corvus JSON Schema code generator over a self-contained derived schema document (one produced by
/// <see cref="WorkflowSchemaMetadataGenerator.TryBuildValidationSchema"/>), producing a strongly-typed model, the
/// accessor map (JSON property name → generated dotnet property), and the generated source files. Shared by the
/// step outputs and workflow outputs model generators (#872): both differ only in the target they resolve, then
/// generate a model from the resulting schema in exactly this way.
/// </summary>
internal static class DerivedSchemaModelGenerator
{
    // An absolute URI so JsonReference treats the derived schema as an opaque registered document rather than a path.
    private const string DocumentUri = "https://corvus.local/arazzo/derived-schema.json";

    /// <summary>
    /// Generates a typed model from a self-contained derived schema document.
    /// </summary>
    /// <param name="schemaDocument">The derived schema document as UTF-8 JSON (its <c>$ref</c>s resolve internally).</param>
    /// <param name="modelNamespace">The namespace for the generated model types.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generated model, or <see langword="null"/> when the schema does not reduce to a named type.</returns>
    internal static async ValueTask<DerivedSchemaModel?> GenerateAsync(
        byte[] schemaDocument,
        string modelNamespace,
        CancellationToken cancellationToken)
    {
        JsonDocument document = JsonDocument.Parse(schemaDocument);
        var resolver = new PrepopulatedDocumentResolver();
        try
        {
            // The derived schema is self-contained (its wrapper carries the source + workflow $defs/components/
            // definitions), so only it needs registering — its $refs resolve internally.
            resolver.AddDocument(DocumentUri, document);

            var vocabularyRegistry = new VocabularyRegistry();
            VocabularyAnalyser.RegisterAnalyser(resolver, vocabularyRegistry);

            var typeBuilder = new JsonSchemaTypeBuilder(resolver, vocabularyRegistry);
            var reference = new JsonReference(DocumentUri, "#");

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

            // An outputs object with no declared members reduces to an empty object type with no accessors — there
            // is nothing to read through it, so no model is generated (the caller keeps the untyped element).
            if (accessors.Count == 0)
            {
                return null;
            }

            var files = new List<GeneratedModelFile>(generated.Count);
            foreach (GeneratedCodeFile file in generated)
            {
                files.Add(new GeneratedModelFile(file.FileName, file.FileContent));
            }

            return new DerivedSchemaModel(typeName, accessors, files);
        }
        finally
        {
            resolver.Dispose();
            document.Dispose();
        }
    }
}

/// <summary>
/// A typed model generated from a derived schema — the fully-qualified type name, the accessor map (JSON property
/// name → generated dotnet property), and the generated source files.
/// </summary>
/// <param name="TypeName">The fully-qualified generated type name.</param>
/// <param name="Accessors">Map of JSON property name → generated dotnet accessor property.</param>
/// <param name="Files">The generated model source files.</param>
internal sealed record DerivedSchemaModel(
    string TypeName,
    IReadOnlyDictionary<string, string> Accessors,
    IReadOnlyList<GeneratedModelFile> Files);