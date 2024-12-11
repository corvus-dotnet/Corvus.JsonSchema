// <copyright file="SourceGeneratorHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
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
    /// <param name="context">The <see cref="SourceProductionContext"/>.</param>
    /// <param name="typesToGenerate">The types to generate.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry.</param>
    public static void GenerateCode(SourceProductionContext context, TypesToGenerate typesToGenerate, VocabularyRegistry vocabularyRegistry)
    {
        if (typesToGenerate.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return;
        }

        List<TypeDeclaration> typeDeclarationsToGenerate = [];
        List<CSharpLanguageProvider.NamedType> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(typesToGenerate.DocumentResolver, vocabularyRegistry);

        string? defaultNamespace = null;

        foreach (GenerationSpecification spec in typesToGenerate.GenerationSpecifications)
        {
            if (context.CancellationToken.IsCancellationRequested)
            {
                return;
            }

            string schemaFile = spec.Location;
            JsonReference reference = new(schemaFile);
            TypeDeclaration rootType;
            try
            {
                rootType = typeBuilder.AddTypeDeclarations(reference, typesToGenerate.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
            }
            catch (Exception ex)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        Crv1000ErrorAddingTypeDeclarations,
                        Location.None,
                        reference,
                        ex.Message));

                return;
            }

            typeDeclarationsToGenerate.Add(rootType);

            defaultNamespace ??= spec.Namespace;

            namedTypes.Add(
                new CSharpLanguageProvider.NamedType(
                    rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                    spec.TypeName,
                    spec.Namespace));
        }

        CSharpLanguageProvider.Options options = new(
            defaultNamespace ?? "GeneratedTypes",
            [.. namedTypes],
            useOptionalNameHeuristics: typesToGenerate.UseOptionalNameHeuristics,
            alwaysAssertFormat: typesToGenerate.AlwaysAssertFormat,
            optionalAsNullable: typesToGenerate.OptionalAsNullable,
            disabledNamingHeuristics: [.. typesToGenerate.DisabledNamingHeuristics],
            fileExtension: ".g.cs");

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

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

            return;
        }

        foreach (GeneratedCodeFile codeFile in generatedCode)
        {
            if (!context.CancellationToken.IsCancellationRequested)
            {
                context.AddSource(codeFile.FileName, SourceText.From(codeFile.FileContent, Encoding.UTF8));
            }
        }
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
    /// <param name="generationSpecifications">The generation specifications.</param>
    /// <param name="generationContext">The generation context.</param>
    public readonly struct TypesToGenerate(ImmutableArray<GenerationSpecification> generationSpecifications, GenerationContext generationContext)
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
        /// Gets the fallback vocabulary for the types to generate.
        /// </summary>
        public IVocabulary FallbackVocabulary => generationContext.FallbackVocabulary;

        /// <summary>
        /// Gets a value indicating whether optional values should be treated as nullable.
        /// </summary>
        public bool OptionalAsNullable => generationContext.OptionalAsNullable;

        /// <summary>
        /// Gets a value indicating whether optional name heuristics should be used.
        /// </summary>
        public bool UseOptionalNameHeuristics => generationContext.UseOptionalNameHeuristics;

        /// <summary>
        /// Gets the names of the disabled naming heuristics.
        /// </summary>
        public ImmutableArray<string> DisabledNamingHeuristics => generationContext.DisabledNamingHeuristics;

        /// <summary>
        /// Gets a value indicating whether to assert format regardless of the vocabulary.
        /// </summary>
        public bool AlwaysAssertFormat => generationContext.AlwaysAssertFormat;
    }

    /// <summary>
    /// Defines the specification for generating a single type and its dependencies.
    /// </summary>
    /// <param name="typeName">The .NET name of the type.</param>
    /// <param name="ns">The .NET namespace for the type.</param>
    /// <param name="location">The schema location of the type.</param>
    /// <param name="rebaseToRootPath">Indicates whether to rebase the schema as a document root.</param>
    public readonly struct GenerationSpecification(string typeName, string ns, string location, bool rebaseToRootPath)
    {
        /// <summary>
        /// Gets the .NET name of the type.
        /// </summary>
        public string TypeName { get; } = typeName;

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
    }

    /// <summary>
    /// Gets teh generation context for the types to generate.
    /// </summary>
    /// <param name="resolver">The document resolver.</param>
    /// <param name="globalOptions">The global options.</param>
    public readonly struct GenerationContext(IDocumentResolver resolver, GlobalOptions globalOptions)
    {
        /// <summary>
        /// Gets the document resolver.
        /// </summary>
        public IDocumentResolver DocumentResolver { get; } = resolver;

        /// <summary>
        /// Gets the global fallback vocabulary. This can be overridden for particular types to generate.
        /// </summary>
        public IVocabulary FallbackVocabulary { get; } = globalOptions.FallbackVocabulary;

        /// <summary>
        /// Gets a value indicating whether optional values should be treated as nullable.
        /// </summary>
        public bool OptionalAsNullable { get; } = globalOptions.OptionalAsNullable;

        /// <summary>
        /// Gets a value indicating whether optional name heuristics should be used.
        /// </summary>
        public bool UseOptionalNameHeuristics { get; } = globalOptions.UseOptionalNameHeuristics;

        /// <summary>
        /// Gets the names of the disabled naming heuristics.
        /// </summary>
        public ImmutableArray<string> DisabledNamingHeuristics { get; } = globalOptions.DisabledNamingHeuristics;

        /// <summary>
        /// Gets a value indicating whether to assert format regardless of the vocabulary.
        /// </summary>
        public bool AlwaysAssertFormat { get; } = globalOptions.AlwaysAssertFormat;
    }

    /// <summary>
    /// Gets the global options for the source generator.
    /// </summary>
    /// <param name="fallbackVocabulary">The global fallback vocabulary. This can be overridden for particular types to generate.</param>
    /// <param name="optionalAsNullable">Indicates whether optional values should be treated as nullable.</param>
    /// <param name="useOptionalNameHeuristics">Indicates whether optional name heuristics should be used.</param>
    /// <param name="alwaysAssertFormat">Indicates whether to assert format regardless of the vocabulary.</param>
    /// <param name="disabledNamingHeuristics">The names of the disabled naming heuristics.</param>
    public readonly struct GlobalOptions(IVocabulary fallbackVocabulary, bool optionalAsNullable, bool useOptionalNameHeuristics, bool alwaysAssertFormat, ImmutableArray<string> disabledNamingHeuristics)
    {
        /// <summary>
        /// Gets the global fallback vocabulary. This can be overridden for particular types to generate.
        /// </summary>
        public IVocabulary FallbackVocabulary { get; } = fallbackVocabulary;

        /// <summary>
        /// Gets a value indicating whether optional values should be treated as nullable.
        /// </summary>
        public bool OptionalAsNullable { get; } = optionalAsNullable;

        /// <summary>
        /// Gets a value indicating whether optional name heuristics should be used.
        /// </summary>
        public bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

        /// <summary>
        /// Gets the names of the disabled naming heuristics.
        /// </summary>
        public ImmutableArray<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics;

        /// <summary>
        /// Gets a value indicating whether to assert format regardless of the vocabulary.
        /// </summary>
        public bool AlwaysAssertFormat { get; } = alwaysAssertFormat;
    }
}