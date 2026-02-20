// <copyright file="SourceGeneratorHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.SourceGenerator;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Json.SourceGeneratorTools;

/// <summary>
/// Useful methods for building source generators for JSON Schema.
/// </summary>
public static class SourceGeneratorHelpers
{
    private static readonly DiagnosticDescriptor Crv1001ErrorGeneratingCSharpCode =
        new(
            id: "CRV1001",
            title: "JSON Schema Type Generator Error",
            messageFormat: "Error generating C# code: {0}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Crv1000ErrorAddingTypeDeclarations =
        new(
            id: "CRV1000",
            title: "JSON Schema Type Generator Error",
            messageFormat: "Error adding type declarations for path '{0}': {1}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    /// <summary>
    /// Generate code into a source production context.
    /// </summary>
    /// <typeparam name="TGlobalOptions">The type of the global options.</typeparam>
    /// <param name="context">The <see cref="SourceProductionContext"/>.</param>
    /// <param name="typesToGenerate">The types to generate.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry.</param>
    /// <returns>The list of root type declarations that were generated.</returns>
    public static List<TypeDeclaration> GenerateCode<TGlobalOptions>(SourceProductionContext context, TypesToGenerate<TGlobalOptions> typesToGenerate, VocabularyRegistry vocabularyRegistry)
        where TGlobalOptions : IGlobalOptions
    {
        if (typesToGenerate.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return [];
        }

        List<TypeDeclaration> typeDeclarationsToGenerate = [];
        JsonSchemaTypeBuilder typeBuilder = new(typesToGenerate.DocumentResolver, vocabularyRegistry);

        string? defaultNamespace = null;

        foreach (GenerationSpecification spec in typesToGenerate.GenerationSpecifications)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return [];
            }

            string schemaFile = spec.Location;
            JsonReference reference = new(schemaFile);
            TypeDeclaration rootType;
            try
            {
                rootType = typeBuilder.AddTypeDeclarations(reference, typesToGenerate.GlobalOptions.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Crv1000ErrorAddingTypeDeclarations,
                        Location.None,
                        reference,
                        ex.Message));

                return [];
            }

            typeDeclarationsToGenerate.Add(rootType);

            defaultNamespace ??= spec.Namespace;

            // Only add the named type if the spec.TypeName is not null or empty.
            if (!string.IsNullOrEmpty(spec.TypeName))
            {
                typesToGenerate.GlobalOptions.AddNamedType(
                        rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                        spec.TypeName,
                        spec.Namespace,
                        spec.Accessibility);
            }
        }

        ILanguageProvider languageProvider = typesToGenerate.GlobalOptions.CreateLanguageProvider();

        IReadOnlyCollection<GeneratedCodeFile> generatedCode;

        try
        {
            generatedCode =
                typeBuilder.GenerateCodeUsing(
                    languageProvider,
                    context.CancellationToken,
                    typeDeclarationsToGenerate);
        }
        catch (Exception ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Crv1001ErrorGeneratingCSharpCode,
                    Location.None,
                    ex.Message));

            return [];
        }

        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                context.AddSource(codeFile.FileName, SourceText.From(codeFile.FileContent, Encoding.UTF8));
            }
        }

        return typeDeclarationsToGenerate;
    }

    /// <summary>
    /// Build a document resolver populated with the given array of additional text sources.
    /// </summary>
    /// <param name="source">The additional text source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A compound document resolver containing the JSON documents registered as additional text sources.</returns>
    public static PrepopulatedDocumentResolver BuildDocumentResolver(ImmutableArray<AdditionalText> source, CancellationToken token)
    {
        PrepopulatedDocumentResolver newResolver = new();
        foreach (AdditionalText additionalText in source)
        {
            if (token.IsCancellationRequested)
            {
                return newResolver;
            }

            string? json = additionalText.GetText(token)?.ToString();
            if (json is string j)
            {
                try
                {
                    var doc = JsonDocument.Parse(j);
                    if (SchemaReferenceNormalization.TryNormalizeSchemaReference(additionalText.Path, string.Empty, out string? normalizedReference))
                    {
                        newResolver.AddDocument(normalizedReference, doc);
                    }

                    // Add the document by its $id if it has one.
                    if (doc.RootElement.TryGetProperty("$id", out JsonElement idElement) &&
                        idElement.ValueKind == JsonValueKind.String)
                    {
                        string id = idElement.GetString()!;
                        newResolver.AddDocument(id, doc);
                    }
                }
                catch (JsonException)
                {
                    // We just ignore bad JSON files.
                }
            }
        }

        return newResolver;
    }

    /// <summary>
    /// Create a <see cref="PrepopulatedDocumentResolver"/> containing the
    /// well-known JSON Schema meta-schema.
    /// </summary>
    /// <returns>A document resolver containing the meta-schema.</returns>
    public static PrepopulatedDocumentResolver CreateMetaSchemaResolver()
    {
        PrepopulatedDocumentResolver metaSchemaResolver = new();
        metaSchemaResolver.AddMetaschema();

        return metaSchemaResolver;
    }

    /// <summary>
    /// Creates a vocabulary registry pre-populated with the JSON schema draft vocabularies.
    /// </summary>
    /// <param name="documentResolver">The document resolver from which the meta-schema can
    /// be resolved. (Typically created using <see cref="CreateMetaSchemaResolver"/>.</param>
    /// <returns>An instance of the vocabulary registry.</returns>
    public static VocabularyRegistry CreateVocabularyRegistry(IDocumentResolver documentResolver)
    {
        VocabularyRegistry vocabularyRegistry = new();

        // Add support for the vocabularies we are interested in.
        CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
        CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
        CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // And register the custom vocabulary for Corvus extensions.
        vocabularyRegistry.RegisterVocabularies(
            CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

        return vocabularyRegistry;
    }

    /// <summary>
    /// Defines the types to generate and the context in which they should be generated.
    /// </summary>
    /// <typeparam name="TGlobalOptions">The type of the global options.</typeparam>
    /// <param name="generationSpecifications">The generation specifications.</param>
    /// <param name="generationContext">The generation context.</param>
    public readonly struct TypesToGenerate<TGlobalOptions>(ImmutableArray<GenerationSpecification> generationSpecifications, GenerationContext<TGlobalOptions> generationContext)
        where TGlobalOptions : IGlobalOptions
    {
        /// <summary>
        /// Gets the generation specifications for the types to generate.
        /// </summary>
        public ImmutableArray<GenerationSpecification> GenerationSpecifications => generationSpecifications;

        /// <summary>
        /// Gets the document resolver for the types to generate.
        /// </summary>
        public IDocumentResolver DocumentResolver => generationContext.DocumentResolver;

        /// <summary>
        /// Gets the global options.
        /// </summary>
        public TGlobalOptions GlobalOptions { get; } = generationContext.GlobalOptions;
    }

    /// <summary>
    /// Defines the specification for generating a single type and its dependencies.
    /// </summary>
    /// <param name="ns">The .NET namespace for the type.</param>
    /// <param name="location">The schema location of the type.</param>
    /// <param name="rebaseToRootPath">Indicates whether to rebase the schema as a document root.</param>
    /// <param name="typeName">The .NET name of the type. If null, the type name will be inferred.</param>
    /// <param name="accessibility">The accessibility of the type. The default is <see cref="GeneratedTypeAccessibility.Public"/>.</param>
    public readonly struct GenerationSpecification(string ns, string location, bool rebaseToRootPath, string? typeName = null, GeneratedTypeAccessibility accessibility = GeneratedTypeAccessibility.Public)
    {
        /// <summary>
        /// Gets the .NET name of the type.
        /// </summary>
        public string? TypeName { get; } = typeName;

        /// <summary>
        /// Gets the .NET namespace for the type.
        /// </summary>
        public string Namespace { get; } = ns;

        /// <summary>
        /// Gets the schema location of the type.
        /// </summary>
        public string Location { get; } = location;

        /// <summary>
        /// Gets a value indicating whether to rebase the schema as document root.
        /// </summary>
        public bool RebaseToRootPath { get; } = rebaseToRootPath;

        /// <summary>
        /// Gets the accessibility for the generated type.
        /// </summary>
        public GeneratedTypeAccessibility Accessibility { get; } = accessibility;
    }

    /// <summary>
    /// Gets teh generation context for the types to generate.
    /// </summary>
    /// <typeparam name="TGlobalOptions">The type of the global options.</typeparam>
    /// <param name="resolver">The document resolver.</param>
    /// <param name="globalOptions">The global options.</param>
    public readonly struct GenerationContext<TGlobalOptions>(IDocumentResolver resolver, TGlobalOptions globalOptions)
        where TGlobalOptions : IGlobalOptions
    {
        /// <summary>
        /// Gets the document resolver.
        /// </summary>
        public IDocumentResolver DocumentResolver { get; } = resolver;

        /// <summary>
        /// Gets the global options.
        /// </summary>
        public TGlobalOptions GlobalOptions { get; } = globalOptions;
    }
}