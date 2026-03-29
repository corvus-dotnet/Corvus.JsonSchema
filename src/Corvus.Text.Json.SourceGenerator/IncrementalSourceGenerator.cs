// <copyright file="IncrementalSourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.SourceGeneratorTools;
using Corvus.Text.Json.CodeGeneration;
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
    private static readonly PrepopulatedDocumentResolver MetaSchemaResolver = SourceGeneratorHelpers.CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = SourceGeneratorHelpers.CreateVocabularyRegistry(MetaSchemaResolver);

    private static readonly IVocabulary Corvus202012Vocab = CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabularyWith([CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance]);

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext initializationContext)
    {
        EmitGeneratorAttribute(initializationContext);

        IncrementalValueProvider<GlobalOptions> globalOptions = initializationContext.AnalyzerConfigOptionsProvider.Select((provider, token) => GetGlobalOptions(VocabularyRegistry, provider, token));

        IncrementalValuesProvider<AdditionalText> jsonSourceFiles = initializationContext.AdditionalTextsProvider.Where(p => p.Path.EndsWith(".json"));

        IncrementalValueProvider<PrepopulatedDocumentResolver> documentResolver = jsonSourceFiles.Collect().Select(SourceGeneratorHelpers.BuildDocumentResolver);

        IncrementalValueProvider<SourceGeneratorHelpers.GenerationContext<GlobalOptions>> generationContext = documentResolver.Combine(globalOptions).Select((r, c) => new SourceGeneratorHelpers.GenerationContext<GlobalOptions>(r.Left, r.Right));

        IncrementalValuesProvider<SourceGeneratorHelpers.GenerationSpecification> generationSpecifications =
            initializationContext.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.JsonSchemaTypeGeneratorAttribute",
                IsValidAttributeTarget,
                BuildGenerationSpecifications);

        IncrementalValueProvider<SourceGeneratorHelpers.TypesToGenerate<GlobalOptions>> typesToGenerate = generationSpecifications.Collect().Combine(generationContext).Select((c, t) => new SourceGeneratorHelpers.TypesToGenerate<GlobalOptions>(c.Left, c.Right));

        initializationContext.RegisterSourceOutput(typesToGenerate, GenerateCode);
    }

    private static void GenerateCode(SourceProductionContext context, SourceGeneratorHelpers.TypesToGenerate<GlobalOptions> generationSource)
    {
        SourceGeneratorHelpers.GenerateCode(
            context,
            generationSource,
            VocabularyRegistry);
    }

    private static SourceGeneratorHelpers.GenerationSpecification BuildGenerationSpecifications(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        AttributeData attribute = context.Attributes[0];
        string location = attribute.ConstructorArguments[0].Value as string ?? throw new InvalidOperationException("Location is required");

        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(location, Path.GetDirectoryName(context.TargetNode.SyntaxTree.FilePath), out string? normalizedLocation))
        {
            location = normalizedLocation;
        }

        bool rebaseToRootPath = attribute.ConstructorArguments[0].Value as bool? ?? false;

        bool emitEvaluator = false;
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "EmitEvaluator" && namedArg.Value.Value is bool b)
            {
                emitEvaluator = b;
            }
        }

        return new(context.TargetSymbol.ContainingNamespace.ToDisplayString(), location, rebaseToRootPath, context.TargetSymbol.Name, GetAccessibility(context.TargetSymbol.DeclaredAccessibility), emitEvaluator);
    }

    private static SourceGeneratorTools.GeneratedTypeAccessibility GetAccessibility(Accessibility accessibility)
    {
        return accessibility switch
        {
            Accessibility.Public => SourceGeneratorTools.GeneratedTypeAccessibility.Public,
            Accessibility.Internal => SourceGeneratorTools.GeneratedTypeAccessibility.Internal,
            Accessibility.Private => SourceGeneratorTools.GeneratedTypeAccessibility.Private,
            _ => throw new InvalidOperationException($"Unsupported accessibility: {accessibility}; try public or internal."),
        };
    }

    private static GlobalOptions GetGlobalOptions(VocabularyRegistry vocabularyRegistry, AnalyzerConfigOptionsProvider source, CancellationToken token)
    {
        IVocabulary fallbackVocabulary = CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonFallbackVocabulary", out string? fallbackVocabularyName))
        {
            fallbackVocabulary = fallbackVocabularyName switch
            {
                "Draft202012" => CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
                "Draft201909" => CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
                "Draft7" => CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
                "Draft6" => CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
                "Draft4" => CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
                "OpenApi30" => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
                "Corvus202012" => Corvus202012Vocab,
                string value => vocabularyRegistry.TryGetSchemaDialect(value, out IVocabulary? vocab) ? vocab : CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            };
        }

        bool optionalAsNullable = false;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonOptionalAsNullable", out string? optionalAsNullableName))
        {
            optionalAsNullable = optionalAsNullableName == "NullOrUndefined";
        }

        bool addExplicitUsings = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonAddExplicitUsings", out string? addExplicitUsingsName))
        {
            addExplicitUsings = addExplicitUsingsName == "true" || addExplicitUsingsName == "True";
        }

        bool useImplicitOperatorString = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonUseImplicitOperatorString", out string? useImplicitOperatorStringName))
        {
            useImplicitOperatorString = useImplicitOperatorStringName == "true" || useImplicitOperatorStringName == "True";
        }

        bool useOptionalNameHeuristics = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonUseOptionalNameHeuristics", out string? useOptionalNameHeuristicsName))
        {
            useOptionalNameHeuristics = useOptionalNameHeuristicsName == "true" || useOptionalNameHeuristicsName == "True";
        }

        bool alwaysAssertFormat = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonAlwaysAssertFormat", out string? alwaysAssertFormatName))
        {
            if (alwaysAssertFormatName == "false" || alwaysAssertFormatName == "False")
            {
                alwaysAssertFormat = false;
            }
            else if (alwaysAssertFormatName == "true" || alwaysAssertFormatName == "True" || alwaysAssertFormatName?.Length == 0)
            {
                alwaysAssertFormat = true;
            }
            else
            {
                throw new InvalidOperationException($"Unrecognized value '{alwaysAssertFormatName}' for 'CorvusTextJsonAlwaysAssertFormat'. Try 'true' or 'false'.");
            }
        }

        Text.Json.CodeGeneration.GeneratedTypeAccessibility defaultAccessibility = Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonDefaultAccessibility", out string? defaultAccessibilityName))
        {
            defaultAccessibility = defaultAccessibilityName switch
            {
                "Public" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public,
                "public" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public,
                "Internal" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Internal,
                "internal" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Internal,
                "Private" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Internal,
                "private" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Internal,
                "" => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public,
                _ => throw new InvalidOperationException($"Invalid build property value for 'CorvusTextJsonDefaultAccessibility': '{defaultAccessibilityName}'. Try 'public' or 'internal'."),
            };
        }

        ImmutableArray<string>? disabledNamingHeuristics = null;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonDisabledNamingHeuristics", out string? disabledNamingHeuristicsSemicolonSeparated))
        {
            string[] disabledNames = disabledNamingHeuristicsSemicolonSeparated.Split([';'], StringSplitOptions.RemoveEmptyEntries);

            disabledNamingHeuristics = disabledNames.Select(d => d.Trim()).ToImmutableArray();
        }

        return new(
            fallbackVocabulary,
            optionalAsNullable,
            useOptionalNameHeuristics,
            alwaysAssertFormat,
            disabledNamingHeuristics ?? DefaultDisabledNamingHeuristics,
            defaultAccessibility,
            addExplicitUsings,
            useImplicitOperatorString);
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

                    namespace Corvus.Text.Json;

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

                        /// <summary>
                        /// Gets or sets a value indicating whether to emit a standalone
                        /// schema evaluator in addition to (or instead of) the generated types.
                        /// </summary>
                        public bool EmitEvaluator { get; set; }
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

    private class GlobalOptions(
        IVocabulary fallbackVocabulary,
        bool optionalAsNullable,
        bool useOptionalNameHeuristics,
        bool alwaysAssertFormat,
        ImmutableArray<string> disabledNamingHeuristics,
        Text.Json.CodeGeneration.GeneratedTypeAccessibility defaultAccessibility,
        bool addExplicitUsings,
        bool useImplicitOperatorString) : IGlobalOptions
    {
        private readonly List<CSharpLanguageProvider.NamedType> _namedTypes = [];

        public IVocabulary FallbackVocabulary { get; } = fallbackVocabulary;

        public bool OptionalAsNullable { get; } = optionalAsNullable;

        public bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

        public ImmutableArray<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics;

        public bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

        public Text.Json.CodeGeneration.GeneratedTypeAccessibility DefaultAccessibility { get; } = defaultAccessibility;

        public bool AddExplicitUsings { get; } = addExplicitUsings;

        public bool UseImplicitOperatorString { get; } = useImplicitOperatorString;

        public bool EmitEvaluator { get; set; }

        public void SetEmitEvaluator()
        {
            EmitEvaluator = true;
        }

        public void AddNamedType(JsonReference schemaLocation, string typeName, string? ns, SourceGeneratorTools.GeneratedTypeAccessibility? accessibility)
        {
            _namedTypes.Add(new CSharpLanguageProvider.NamedType(schemaLocation, typeName, ns, GetAccessibility(accessibility)));
        }

        public ILanguageProvider CreateLanguageProvider(string? defaultNamespace)
        {
            return CSharpLanguageProvider.DefaultWithOptions(MapOptions(defaultNamespace));
        }

        private static Text.Json.CodeGeneration.GeneratedTypeAccessibility GetAccessibility(SourceGeneratorTools.GeneratedTypeAccessibility? accessibility)
        {
            return accessibility switch
            {
                SourceGeneratorTools.GeneratedTypeAccessibility.Public => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public,
                SourceGeneratorTools.GeneratedTypeAccessibility.Internal => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Internal,
                SourceGeneratorTools.GeneratedTypeAccessibility.Private => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Private,
                _ => Text.Json.CodeGeneration.GeneratedTypeAccessibility.Public,
            };
        }

        private CSharpLanguageProvider.Options MapOptions(string? defaultNamespace)
        {
            CSharpLanguageProvider.Options options = new(
                defaultNamespace ?? "GeneratedTypes",
                [.. _namedTypes],
                useOptionalNameHeuristics: UseOptionalNameHeuristics,
                alwaysAssertFormat: AlwaysAssertFormat,
                optionalAsNullable: OptionalAsNullable,
                disabledNamingHeuristics: [.. DisabledNamingHeuristics],
                fileExtension: ".g.cs",
                defaultAccessibility: DefaultAccessibility,
                codeGenerationMode: EmitEvaluator ? CodeGenerationMode.Both : CodeGenerationMode.TypeGeneration);

            return options;
        }
    }
}