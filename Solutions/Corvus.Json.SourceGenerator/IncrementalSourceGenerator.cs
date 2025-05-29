// <copyright file="IncrementalSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Text;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Json.SourceGenerator;

/// <summary>
/// Base for a source generator.
/// </summary>
[Generator(LanguageNames.CSharp)]
public class IncrementalSourceGenerator : IIncrementalGenerator
{
    private static readonly ImmutableArray<string> DefaultDisabledNamingHeuristics = ["DocumentationNameHeuristic"];
    private static readonly PrepopulatedDocumentResolver MetaSchemaResolver = CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = RegisterVocabularies(MetaSchemaResolver);
    private static readonly DiagnosticDescriptor Crv1001ErrorGeneratingCSharpCode =
        new(
            id: "CRV1001",
            title: "JSON Schema Type Generator Error",
            messageFormat: $"Error generating C# code: {{0}}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    private static readonly DiagnosticDescriptor Crv1000ErrorAddingTypeDeclarations =
        new(
            id: "CRV1000",
            title: "JSON Schema Type Generator Error",
            messageFormat: $"Error adding type declarations for path '{{0}}': {{1}}",
            category: "JsonSchemaCodeGenerator",
            DiagnosticSeverity.Error,
            isEnabledByDefault: true);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext initializationContext)
    {
        EmitGeneratorAttribute(initializationContext);

        IncrementalValueProvider<GlobalOptions> globalOptions = initializationContext.AnalyzerConfigOptionsProvider.Select(GetGlobalOptions);

        IncrementalValuesProvider<AdditionalText> jsonSourceFiles = initializationContext.AdditionalTextsProvider.Where(p => p.Path.EndsWith(".json"));

        IncrementalValueProvider<CompoundDocumentResolver> documentResolver = jsonSourceFiles.Collect().Select(BuildDocumentResolver);

        IncrementalValueProvider<GenerationContext> generationContext = documentResolver.Combine(globalOptions).Select((r, c) => new GenerationContext(r.Left, r.Right));

        IncrementalValuesProvider<GenerationSpecification> generationSpecifications =
            initializationContext.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Json.JsonSchemaTypeGeneratorAttribute",
                IsValidAttributeTarget,
                BuildGenerationSpecifications);

        IncrementalValueProvider<TypesToGenerate> typesToGenerate = generationSpecifications.Collect().Combine(generationContext).Select((c, t) => new TypesToGenerate(c.Left, c.Right));

        initializationContext.RegisterSourceOutput(typesToGenerate, GenerateCode);
    }

    private static void GenerateCode(SourceProductionContext context, TypesToGenerate generationSource)
    {
        if (generationSource.GenerationSpecifications.Length == 0)
        {
            // Nothing to generate
            return;
        }

        List<TypeDeclaration> typesToGenerate = [];
        List<CSharpLanguageProvider.NamedType> namedTypes = [];
        JsonSchemaTypeBuilder typeBuilder = new(generationSource.DocumentResolver, VocabularyRegistry);

        string? defaultNamespace = null;

        foreach (GenerationSpecification spec in generationSource.GenerationSpecifications)
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
                rootType = typeBuilder.AddTypeDeclarations(reference, generationSource.FallbackVocabulary, spec.RebaseToRootPath, context.CancellationToken);
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

            typesToGenerate.Add(rootType);

            defaultNamespace ??= spec.Namespace;

            namedTypes.Add(
                new CSharpLanguageProvider.NamedType(
                    rootType.ReducedTypeDeclaration().ReducedType.LocatedSchema.Location,
                    spec.TypeName,
                    spec.Namespace,
                    GetAccessibility(spec.Accessibility)));
        }

        CSharpLanguageProvider.Options options = new(
            defaultNamespace ?? "GeneratedTypes",
            namedTypes.ToArray(),
            disabledNamingHeuristics: generationSource.DisabledNamingHeuristics.ToArray(),
            optionalAsNullable: generationSource.OptionalAsNullable,
            useOptionalNameHeuristics: generationSource.UseOptionalNameHeuristics,
            alwaysAssertFormat: generationSource.AlwaysAssertFormat,
            fileExtension: "_g.cs",
            defaultAccessibility: generationSource.DefaultAccessibility,
            addExplicitUsings: generationSource.AddExplicitUsings,
            useImplicitOperatorString: generationSource.UseImplicitOperatorString);

        var languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        IReadOnlyCollection<GeneratedCodeFile> generatedCode;

        try
        {
            generatedCode =
                typeBuilder.GenerateCodeUsing(
                    languageProvider,
                    context.CancellationToken,
                    typesToGenerate);
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

    private static GeneratedTypeAccessibility GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => GeneratedTypeAccessibility.Public,
            Accessibility.Internal => GeneratedTypeAccessibility.Internal,
            _ => throw new InvalidOperationException($"Unsupported accessibility: {accessibility}; try public or internal."),
        };
    }

    private static GenerationSpecification BuildGenerationSpecifications(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        AttributeData attribute = context.Attributes[0];
        string location = attribute.ConstructorArguments[0].Value as string ?? throw new InvalidOperationException("Location is required");

        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(location, Path.GetDirectoryName(context.TargetNode.SyntaxTree.FilePath), out string? normalizedLocation))
        {
            location = normalizedLocation;
        }

        bool rebaseToRootPath = attribute.ConstructorArguments[0].Value as bool? ?? false;

        return new(context.TargetSymbol.Name, context.TargetSymbol.ContainingNamespace.ToDisplayString(), location, rebaseToRootPath, context.TargetSymbol.DeclaredAccessibility);
    }

    private static GlobalOptions GetGlobalOptions(AnalyzerConfigOptionsProvider source, CancellationToken token)
    {
        IVocabulary fallbackVocabulary = CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaFallbackVocabulary", out string? fallbackVocabularyName))
        {
            fallbackVocabulary = fallbackVocabularyName switch
            {
                "Draft202012" => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
                "Draft201909" => CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
                "Draft7" => CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
                "Draft6" => CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
                "Draft4" => CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
                "OpenApi30" => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
                _ => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            };
        }

        bool optionalAsNullable = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaOptionalAsNullable", out string? optionalAsNullableName))
        {
            optionalAsNullable = optionalAsNullableName == "NullOrUndefined";
        }

        bool addExplicitUsings = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaAddExplicitUsings", out string? addExplicitUsingsName))
        {
            addExplicitUsings = addExplicitUsingsName == "true" || addExplicitUsingsName == "True";
        }

        bool useImplicitOperatorString = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaUseImplicitOperatorString", out string? useImplicitOperatorStringName))
        {
            useImplicitOperatorString = useImplicitOperatorStringName == "true" || useImplicitOperatorStringName == "True";
        }

        bool useOptionalNameHeuristics = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaUseOptionalNameHeuristics", out string? useOptionalNameHeuristicsName))
        {
            useOptionalNameHeuristics = useOptionalNameHeuristicsName == "true" || useOptionalNameHeuristicsName == "True";
        }

        bool alwaysAssertFormat = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaAlwaysAssertFormat", out string? alwaysAssertFormatName))
        {
            alwaysAssertFormat = alwaysAssertFormatName == "true" || alwaysAssertFormatName == "True";
        }

        GeneratedTypeAccessibility defaultAccessibility = GeneratedTypeAccessibility.Public;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaDefaultAccessibility", out string? defaultAccessibilityName))
        {
            defaultAccessibility = defaultAccessibilityName switch
            {
                "Public" => GeneratedTypeAccessibility.Public,
                "public" => GeneratedTypeAccessibility.Public,
                "Internal" => GeneratedTypeAccessibility.Internal,
                "internal" => GeneratedTypeAccessibility.Internal,
                "" => GeneratedTypeAccessibility.Public,
                _ => throw new InvalidOperationException($"Invalid build property value for 'CorvusJsonSchemaDefaultAccessibility': '{defaultAccessibilityName}'. Try 'public' or 'internal'."),
            };
        }

        ImmutableArray<string>? disabledNamingHeuristics = null;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaDisabledNamingHeuristics", out string? disabledNamingHeuristicsSemicolonSeparated))
        {
            string[] disabledNames = disabledNamingHeuristicsSemicolonSeparated.Split([';'], StringSplitOptions.RemoveEmptyEntries);

            disabledNamingHeuristics = disabledNames.Select(d => d.Trim()).ToImmutableArray();
        }

        return new(fallbackVocabulary, optionalAsNullable, useOptionalNameHeuristics, alwaysAssertFormat, disabledNamingHeuristics ?? DefaultDisabledNamingHeuristics, defaultAccessibility, addExplicitUsings, useImplicitOperatorString);
    }

    private static CompoundDocumentResolver BuildDocumentResolver(ImmutableArray<AdditionalText> source, CancellationToken token)
    {
        PrepopulatedDocumentResolver newResolver = new();
        foreach (AdditionalText additionalText in source)
        {
            if (token.IsCancellationRequested)
            {
                return new CompoundDocumentResolver();
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
                    // We ignore bad JSON files.
                }
            }
        }

        return new CompoundDocumentResolver(newResolver, MetaSchemaResolver);
    }

    private static PrepopulatedDocumentResolver CreateMetaSchemaResolver()
    {
        PrepopulatedDocumentResolver metaSchemaResolver = new();
        metaSchemaResolver.AddMetaschema();

        return metaSchemaResolver;
    }

    private static VocabularyRegistry RegisterVocabularies(IDocumentResolver documentResolver)
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

    private static void EmitGeneratorAttribute(IncrementalGeneratorInitializationContext initializationContext)
    {
        initializationContext.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "JsonSchemaTypeGeneratorAttribute.g.cs",
                SourceText.From(
                    """
                    using System;

                    namespace Corvus.Json;

                    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
                    internal sealed class JsonSchemaTypeGeneratorAttribute : Attribute
                    {
                        public JsonSchemaTypeGeneratorAttribute(string location, bool rebaseToRootPath = false)
                        {
                            this.Location = location;
                            this.RebaseToRootPath = rebaseToRootPath;
                        }

                        /// <summary>
                        /// Gets the location for the JSON schema.
                        /// </summary>
                        public string Location { get; }

                        /// <summary>
                        /// Gets a value indicating whether to rebase to the root path.
                        /// </summary>
                        public bool RebaseToRootPath { get; }
                    }
                    """,
                    Encoding.UTF8));
        });
    }

    private static bool IsValidAttributeTarget(SyntaxNode node, CancellationToken token)
    {
        return
            node is StructDeclarationSyntax structDeclarationSyntax &&
            structDeclarationSyntax
                .Modifiers
                .Any(m => m.IsKind(SyntaxKind.PartialKeyword)) &&
            structDeclarationSyntax.Parent is (FileScopedNamespaceDeclarationSyntax or NamespaceDeclarationSyntax);
    }

    private readonly struct GenerationSpecification(string typeName, string ns, string location, bool rebaseToRootPath, Accessibility accessibility)
    {
        public string TypeName { get; } = typeName;

        public string Namespace { get; } = ns;

        public string Location { get; } = location;

        public bool RebaseToRootPath { get; } = rebaseToRootPath;

        public Accessibility Accessibility { get; } = accessibility;
    }

    private readonly struct GenerationContext(CompoundDocumentResolver left, GlobalOptions right)
    {
        public CompoundDocumentResolver DocumentResolver { get; } = left;

        public IVocabulary FallbackVocabulary { get; } = right.FallbackVocabulary;

        public bool OptionalAsNullable { get; } = right.OptionalAsNullable;

        public bool UseOptionalNameHeuristics { get; } = right.UseOptionalNameHeuristics;

        public ImmutableArray<string> DisabledNamingHeuristics { get; } = right.DisabledNamingHeuristics;

        public bool AlwaysAssertFormat { get; } = right.AlwaysAssertFormat;

        public GeneratedTypeAccessibility DefaultAccessibility { get; } = right.DefaultAccessibility;

        public bool AddExplicitUsings { get; } = right.AddExplicitUsings;

        public bool UseImplicitOperatorString { get; } = right.UseImplicitOperatorString;
    }

    private readonly struct TypesToGenerate(ImmutableArray<GenerationSpecification> left, GenerationContext right)
    {
        public ImmutableArray<GenerationSpecification> GenerationSpecifications { get; } = left;

        public CompoundDocumentResolver DocumentResolver { get; } = right.DocumentResolver;

        public IVocabulary FallbackVocabulary { get; } = right.FallbackVocabulary;

        public bool OptionalAsNullable { get; } = right.OptionalAsNullable;

        public bool UseOptionalNameHeuristics { get; } = right.UseOptionalNameHeuristics;

        public ImmutableArray<string> DisabledNamingHeuristics { get; } = right.DisabledNamingHeuristics;

        public bool AlwaysAssertFormat { get; } = right.AlwaysAssertFormat;

        public GeneratedTypeAccessibility DefaultAccessibility { get; } = right.DefaultAccessibility;

        public bool AddExplicitUsings { get; } = right.AddExplicitUsings;

        public bool UseImplicitOperatorString { get; } = right.UseImplicitOperatorString;
    }

    private readonly struct GlobalOptions(IVocabulary fallbackVocabulary, bool optionalAsNullable, bool useOptionalNameHeuristics, bool alwaysAssertFormat, ImmutableArray<string> disabledNamingHeuristics, GeneratedTypeAccessibility defaultAccessibility, bool addExplicitUsings, bool useImplicitOperatorString)
    {
        public IVocabulary FallbackVocabulary { get; } = fallbackVocabulary;

        public bool OptionalAsNullable { get; } = optionalAsNullable;

        public bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

        public ImmutableArray<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics;

        public bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

        public GeneratedTypeAccessibility DefaultAccessibility { get; } = defaultAccessibility;

        public bool AddExplicitUsings { get; } = addExplicitUsings;

        public bool UseImplicitOperatorString { get; } = useImplicitOperatorString;
    }
}