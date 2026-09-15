// <copyright file="SourceGeneratorHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.SourceGenerator;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Yaml;
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

    private static readonly DiagnosticDescriptor Crv1002CircularSchemaReference =
        new(
            id: "CRV1002",
            title: "Circular JSON Schema reference",
            messageFormat: "Circular schema reference: the schema at '{0}' references '{1}' for the same instance, so validation would never terminate and terminating code cannot be generated. Break the cycle before generating types.",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    /// <summary>
    /// Gets the process-wide <see cref="PrepopulatedDocumentResolver"/> containing the
    /// well-known JSON Schema meta-schema, parsed once per process.
    /// </summary>
    public static PrepopulatedDocumentResolver MetaSchemaResolver { get; } = CreateMetaSchemaResolver();

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
        return GenerateCodeCore(context, typesToGenerate.GenerationSpecifications, typesToGenerate.DocumentResolver, typesToGenerate.GlobalOptions, vocabularyRegistry, producedSources: null, out _);
    }

    /// <summary>
    /// Build a document resolver populated with the given array of additional text sources.
    /// </summary>
    /// <param name="source">The additional text source.</param>
    /// <param name="token">The cancellation token.</param>
    /// <returns>A compound document resolver containing the JSON documents registered as additional text sources.</returns>
    public static IDocumentResolver BuildDocumentResolver(ImmutableArray<AdditionalText> source, CancellationToken token)
    {
        PrepopulatedDocumentResolver newResolver = new();
        foreach (AdditionalText additionalText in source)
        {
            if (token.IsCancellationRequested)
            {
                return newResolver;
            }

            JsonDocument? doc;

            try
            {
                if (additionalText.Path.EndsWith(".yaml", StringComparison.Ordinal) || additionalText.Path.EndsWith(".yml", StringComparison.Ordinal))
                {
                    string? yaml = additionalText.GetText(token)?.ToString();
                    doc = yaml is not null ? YamlDocument.Parse(yaml) : null;
                }
                else
                {
                    string? json = additionalText.GetText(token)?.ToString();
                    doc = json is not null ? JsonDocument.Parse(json) : null;
                }
            }
            catch (YamlException)
            {
                continue;
            }
            catch (JsonException)
            {
                continue;
            }

            if (doc is not null)
            {
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
        }

        // Chain the process-wide metaschema resolver so that schemas which $ref into a
        // metaschema (e.g. the Swagger 2.0 metaschema's references into draft-04's
        // definitions) resolve during generation, without re-parsing the metaschemas for
        // every build. The additional texts are consulted first, so a user-supplied copy of
        // a metaschema URI still takes precedence; documents the type builder registers
        // during generation land in the compound resolver's own table.
        return new CompoundDocumentResolver(newResolver, new SharedDocumentResolver(MetaSchemaResolver));
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
    /// A read-only view over a resolver shared by every generation in the process: resolves
    /// through the shared resolver, but never disposes, resets or adds to it.
    /// </summary>
    private sealed class SharedDocumentResolver(IDocumentResolver shared) : IDocumentResolver
    {
        public bool AddDocument(string uri, JsonDocument document) => false;

        public ValueTask<JsonElement?> TryResolve(JsonReference reference) => shared.TryResolve(reference);

        public void Reset()
        {
        }

        public void Dispose()
        {
        }
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
        CodeGeneration.OpenApi20.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);

        // And register the custom vocabulary for Corvus extensions.
        vocabularyRegistry.RegisterVocabularies(
            CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

        return vocabularyRegistry;
    }

    /// <summary>
    /// Chains the process-wide metaschema resolver behind a generation's resolver.
    /// </summary>
    /// <param name="resolver">The resolver with the generation's documents.</param>
    /// <returns>The resolver for the generation.</returns>
    internal static IDocumentResolver ChainMetaschemas(PrepopulatedDocumentResolver resolver)
    {
        return new CompoundDocumentResolver(resolver, new SharedDocumentResolver(MetaSchemaResolver));
    }

    /// <summary>
    /// Generate code into a source production context from value-equatable pipeline inputs, reusing an earlier
    /// generation's sources when the specifications, the options and every document it read are unchanged.
    /// </summary>
    /// <typeparam name="TGlobalOptions">The type of the global options.</typeparam>
    /// <param name="context">The <see cref="SourceProductionContext"/>.</param>
    /// <param name="generationSpecifications">The generation specifications.</param>
    /// <param name="schemaFiles">The JSON and YAML additional texts.</param>
    /// <param name="globalOptions">The global options.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry.</param>
    /// <param name="memo">The outputs of recent generations.</param>
    internal static void GenerateCode<TGlobalOptions>(SourceProductionContext context, ImmutableArray<GenerationSpecification> generationSpecifications, ImmutableArray<SchemaFile> schemaFiles, TGlobalOptions globalOptions, VocabularyRegistry vocabularyRegistry, GenerationMemo memo)
        where TGlobalOptions : IGlobalOptions
    {
        if (generationSpecifications.Length == 0)
        {
            // Nothing to generate
            return;
        }

        SchemaFileSet files = SchemaFileSet.Create(schemaFiles, context.CancellationToken);
        if (context.CancellationToken.IsCancellationRequested)
        {
            return;
        }

        if (memo.TryGet(generationSpecifications, globalOptions, files, out IReadOnlyList<(string HintName, SourceText Text)>? previousSources))
        {
            foreach ((string hintName, SourceText text) in previousSources)
            {
                if (context.CancellationToken.IsCancellationRequested)
                {
                    return;
                }

                context.AddSource(hintName, text);
            }

            return;
        }

        RecordingDocumentResolver documentResolver = new(files.CreateResolver());
        List<(string HintName, SourceText Text)> producedSources = [];
        GenerateCodeCore(context, generationSpecifications, documentResolver, globalOptions, vocabularyRegistry, producedSources, out bool succeeded);

        // Only a complete generation that reported no diagnostics is reused.
        if (succeeded)
        {
            memo.Store(generationSpecifications, globalOptions, files.GetFingerprints(documentResolver.ResolvedUris), producedSources);
        }
    }

    /// <summary>
    /// Generate code for the specifications with a given document resolver.
    /// </summary>
    /// <typeparam name="TGlobalOptions">The type of the global options.</typeparam>
    /// <param name="context">The <see cref="SourceProductionContext"/>.</param>
    /// <param name="generationSpecifications">The generation specifications.</param>
    /// <param name="documentResolver">The document resolver.</param>
    /// <param name="globalOptions">The global options.</param>
    /// <param name="vocabularyRegistry">The vocabulary registry.</param>
    /// <param name="producedSources">If not <see langword="null"/>, receives the sources added to the context.</param>
    /// <param name="succeeded">Set to <see langword="true"/> if every source was added and no diagnostic was reported.</param>
    /// <returns>The list of root type declarations that were generated.</returns>
    private static List<TypeDeclaration> GenerateCodeCore<TGlobalOptions>(SourceProductionContext context, ImmutableArray<GenerationSpecification> generationSpecifications, IDocumentResolver documentResolver, TGlobalOptions globalOptions, VocabularyRegistry vocabularyRegistry, List<(string HintName, SourceText Text)>? producedSources, out bool succeeded)
        where TGlobalOptions : IGlobalOptions
    {
        succeeded = false;

        if (generationSpecifications.Length == 0)
        {
            // Nothing to generate
            return [];
        }

        List<TypeDeclaration> typeDeclarationsToGenerate = [];
        List<TypeDeclaration>? evaluatorRootTypes = null;

        // Per-generation inputs. The global options are cached across generations by the
        // incremental pipeline, so they must not accumulate state from one run to the next.
        List<NamedTypeSpecification> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

        string? defaultNamespace = null;

        foreach (GenerationSpecification spec in generationSpecifications)
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
                rootType = typeBuilder.AddTypeDeclarations(reference, globalOptions.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
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

            if (spec.EmitEvaluator)
            {
                evaluatorRootTypes ??= [];
                evaluatorRootTypes.Add(rootType);
            }

            // Prefer the first non-empty namespace: an empty namespace (a global-namespace
            // target) must not capture the default used for shared generated types.
            if (string.IsNullOrEmpty(defaultNamespace))
            {
                defaultNamespace = spec.Namespace;
            }

            // Only add the named type if the spec.TypeName is not null or empty.
            if (!string.IsNullOrEmpty(spec.TypeName))
            {
                namedTypes.Add(
                    new NamedTypeSpecification(
                        rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                        spec.TypeName,
                        spec.Namespace,
                        spec.Accessibility));
            }
        }

        ILanguageProvider languageProvider = globalOptions.CreateLanguageProvider(defaultNamespace, namedTypes, emitEvaluator: evaluatorRootTypes is not null);

        // Set the evaluator root types on the language provider so the evaluator generator
        // can access the unreduced type declarations.
        if (evaluatorRootTypes is not null && languageProvider is CSharpLanguageProvider csharpProvider)
        {
            csharpProvider.SetEvaluatorRootTypes([.. evaluatorRootTypes]);
        }

        IReadOnlyCollection<GeneratedCodeFile> generatedCode;

        try
        {
            generatedCode =
                typeBuilder.GenerateCodeUsing(
                    languageProvider,
                    typeDeclarationsToGenerate,
                    context.CancellationToken);
        }
        catch (CircularSchemaReferenceException ex)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    Crv1002CircularSchemaReference,
                    Location.None,
                    ex.ReferencingLocation,
                    ex.ReferencedLocation));

            return [];
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
                SourceText text = SourceText.From(codeFile.FileContent, Encoding.UTF8);
                context.AddSource(codeFile.FileName, text);
                producedSources?.Add((codeFile.FileName, text));
            }
        }

        succeeded = !context.CancellationToken.IsCancellationRequested;
        return typeDeclarationsToGenerate;
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
    /// <param name="emitEvaluator">Indicates whether to emit a standalone evaluator.</param>
    public readonly struct GenerationSpecification(string ns, string location, bool rebaseToRootPath, string? typeName = null, GeneratedTypeAccessibility accessibility = GeneratedTypeAccessibility.Public, bool emitEvaluator = false) : IEquatable<GenerationSpecification>
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

        /// <summary>
        /// Gets a value indicating whether to emit a standalone evaluator.
        /// </summary>
        public bool EmitEvaluator { get; } = emitEvaluator;

        /// <summary>
        /// Determines whether two specifications are equal.
        /// </summary>
        /// <param name="left">The first specification.</param>
        /// <param name="right">The second specification.</param>
        /// <returns><see langword="true"/> if they are equal.</returns>
        public static bool operator ==(GenerationSpecification left, GenerationSpecification right) => left.Equals(right);

        /// <summary>
        /// Determines whether two specifications differ.
        /// </summary>
        /// <param name="left">The first specification.</param>
        /// <param name="right">The second specification.</param>
        /// <returns><see langword="true"/> if they differ.</returns>
        public static bool operator !=(GenerationSpecification left, GenerationSpecification right) => !left.Equals(right);

        /// <inheritdoc/>
        public bool Equals(GenerationSpecification other)
        {
            return
                string.Equals(this.TypeName, other.TypeName, StringComparison.Ordinal) &&
                string.Equals(this.Namespace, other.Namespace, StringComparison.Ordinal) &&
                string.Equals(this.Location, other.Location, StringComparison.Ordinal) &&
                this.RebaseToRootPath == other.RebaseToRootPath &&
                this.Accessibility == other.Accessibility &&
                this.EmitEvaluator == other.EmitEvaluator;
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj) => obj is GenerationSpecification other && this.Equals(other);

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.TypeName, this.Namespace, this.Location, this.RebaseToRootPath, this.Accessibility, this.EmitEvaluator);
        }
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