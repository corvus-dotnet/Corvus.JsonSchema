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
using System.Globalization;
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
    private static readonly PrepopulatedDocumentResolver MetaSchemaResolver = SourceGeneratorHelpers.MetaSchemaResolver;
    private static readonly VocabularyRegistry VocabularyRegistry = SourceGeneratorHelpers.CreateVocabularyRegistry(MetaSchemaResolver);

    private static readonly IVocabulary Corvus202012Vocab = CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabularyWith([CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance]);

    private readonly GenerationMemo generationMemo = new();

    /// <inheritdoc/>
    public void Initialize(IncrementalGeneratorInitializationContext initializationContext)
    {
        EmitGeneratorAttribute(initializationContext);

        IncrementalValueProvider<GlobalOptions> globalOptions = initializationContext.AnalyzerConfigOptionsProvider.Select((provider, token) => GetGlobalOptions(VocabularyRegistry, provider, token));

        IncrementalValuesProvider<AdditionalText> jsonSourceFiles = initializationContext.AdditionalTextsProvider.Where(static p => p.Path.EndsWith(".json", StringComparison.Ordinal) || p.Path.EndsWith(".yaml", StringComparison.Ordinal) || p.Path.EndsWith(".yml", StringComparison.Ordinal));

        // Each file is compared by path and content checksum, so re-reading unchanged content leaves the pipeline
        // cached; the documents are parsed in the output step, through a cache keyed by the text.
        IncrementalValueProvider<ImmutableArray<SchemaFile>> schemaFiles = jsonSourceFiles.Select(static (text, token) => SchemaFile.Create(text, token)).Collect();

        IncrementalValuesProvider<SourceGeneratorHelpers.GenerationSpecification> generationSpecifications =
            initializationContext.SyntaxProvider.ForAttributeWithMetadataName(
                "Corvus.Text.Json.JsonSchemaTypeGeneratorAttribute",
                IsValidAttributeTarget,
                BuildGenerationSpecifications);

        IncrementalValueProvider<(ImmutableArray<SourceGeneratorHelpers.GenerationSpecification> Specifications, (ImmutableArray<SchemaFile> Files, GlobalOptions Options) Context)> typesToGenerate =
            generationSpecifications.Collect().Combine(schemaFiles.Combine(globalOptions));

        initializationContext.RegisterSourceOutput(
            typesToGenerate,
            (context, source) => SourceGeneratorHelpers.GenerateCode(
                context,
                source.Specifications,
                source.Context.Files,
                source.Context.Options,
                VocabularyRegistry,
                this.generationMemo));
    }

    private static SourceGeneratorHelpers.GenerationSpecification BuildGenerationSpecifications(GeneratorAttributeSyntaxContext context, CancellationToken token)
    {
        AttributeData attribute = context.Attributes[0];
        string location = attribute.ConstructorArguments[0].Value as string ?? throw new InvalidOperationException("Location is required");

        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(location, Path.GetDirectoryName(context.TargetNode.SyntaxTree.FilePath), out string? normalizedLocation))
        {
            location = normalizedLocation;
        }

        bool rebaseToRootPath = attribute.ConstructorArguments[1].Value as bool? ?? false;

        bool emitEvaluator = false;
        foreach (KeyValuePair<string, TypedConstant> namedArg in attribute.NamedArguments)
        {
            if (namedArg.Key == "EmitEvaluator" && namedArg.Value.Value is bool b)
            {
                emitEvaluator = b;
            }
        }

        string ns = context.TargetSymbol.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : context.TargetSymbol.ContainingNamespace.ToDisplayString();

        return new(ns, location, rebaseToRootPath, context.TargetSymbol.Name, GetAccessibility(context.TargetSymbol.DeclaredAccessibility), emitEvaluator);
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
                "OpenApi20" => CodeGeneration.OpenApi20.VocabularyAnalyser.DefaultVocabulary,
                "OpenApi30" => CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
                "Corvus202012" => Corvus202012Vocab,
                string value => vocabularyRegistry.TryGetSchemaDialect(value, out IVocabulary? vocab) ? vocab : CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            };
        }

        bool optionalAsNullable = false;
        bool excludeNonNullDefaulted = false;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonOptionalAsNullable", out string? optionalAsNullableName))
        {
            optionalAsNullable = optionalAsNullableName is "NullOrUndefined" or "NullOrUndefinedExceptNonNullDefaulted";
            excludeNonNullDefaulted = optionalAsNullableName == "NullOrUndefinedExceptNonNullDefaulted";
        }

        bool emitNativeStringEnums = true;
        bool emitNativeFlagsEnums = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonNativeEnums", out string? nativeEnumsName))
        {
            (emitNativeStringEnums, emitNativeFlagsEnums) = nativeEnumsName switch
            {
                "None" => (false, false),
                "StringEnums" => (true, false),
                "FlagsObjects" => (false, true),
                "All" => (true, true),
                "" => (true, true),
                _ => throw new InvalidOperationException($"Invalid build property value for 'CorvusTextJsonNativeEnums': '{nativeEnumsName}'. Try 'None', 'StringEnums', 'FlagsObjects' or 'All'."),
            };
        }

        bool emitUnions = true;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonUnions", out string? unionsName))
        {
            emitUnions = unionsName switch
            {
                "false" or "False" => false,
                "true" or "True" or "" => true,
                _ => throw new InvalidOperationException($"Invalid build property value for 'CorvusTextJsonUnions': '{unionsName}'. Try 'true' or 'false'."),
            };
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

        IReadOnlyDictionary<string, FormatAssertionMode>? formatModeOverrides = null;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonFormatMode", out string? formatModeSpec) &&
            !string.IsNullOrWhiteSpace(formatModeSpec))
        {
            formatModeOverrides = FormatAssertionModeParser.ParseSpecification(formatModeSpec, ';', ',');
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

        int buildParametersThreshold = CSharpLanguageProvider.Options.DefaultBuildParametersThreshold;

        if (source.GlobalOptions.TryGetValue("build_property.CorvusTextJsonBuildParametersThreshold", out string? buildParametersThresholdName) &&
            !string.IsNullOrEmpty(buildParametersThresholdName))
        {
            if (!int.TryParse(buildParametersThresholdName, NumberStyles.Integer, CultureInfo.InvariantCulture, out buildParametersThreshold))
            {
                throw new InvalidOperationException($"Invalid build property value for 'CorvusTextJsonBuildParametersThreshold': '{buildParametersThresholdName}'. Expected an integer.");
            }
        }

        return new(
            fallbackVocabulary,
            optionalAsNullable,
            excludeNonNullDefaulted,
            useOptionalNameHeuristics,
            alwaysAssertFormat,
            disabledNamingHeuristics ?? DefaultDisabledNamingHeuristics,
            defaultAccessibility,
            addExplicitUsings,
            useImplicitOperatorString,
            buildParametersThreshold,
            formatModeOverrides,
            emitNativeStringEnums,
            emitNativeFlagsEnums,
            emitUnions);
    }

    private static void EmitGeneratorAttribute(IncrementalGeneratorInitializationContext initializationContext)
    {
        initializationContext.RegisterPostInitializationOutput(static postInitializationContext =>
        {
            postInitializationContext.AddSource(
                "JsonSchemaTypeGeneratorAttribute.g.cs",
                SourceText.From(
                    """
                    // <auto-generated/>
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
                        /// schema evaluator in addition to the generated types.
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
               structDeclarationSyntax.Parent is (FileScopedNamespaceDeclarationSyntax or NamespaceDeclarationSyntax or CompilationUnitSyntax);
    }

    private class GlobalOptions(
        IVocabulary fallbackVocabulary,
        bool optionalAsNullable,
        bool excludeNonNullDefaulted,
        bool useOptionalNameHeuristics,
        bool alwaysAssertFormat,
        ImmutableArray<string> disabledNamingHeuristics,
        Text.Json.CodeGeneration.GeneratedTypeAccessibility defaultAccessibility,
        bool addExplicitUsings,
        bool useImplicitOperatorString,
        int buildParametersThreshold,
        IReadOnlyDictionary<string, FormatAssertionMode>? formatModeOverrides,
        bool emitNativeStringEnums,
        bool emitNativeFlagsEnums,
        bool emitUnions) : IGlobalOptions, IEquatable<GlobalOptions>
    {
        public IVocabulary FallbackVocabulary { get; } = fallbackVocabulary;

        public bool OptionalAsNullable { get; } = optionalAsNullable;

        public bool ExcludeNonNullDefaulted { get; } = excludeNonNullDefaulted;

        public bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

        public ImmutableArray<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics;

        public bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

        public Text.Json.CodeGeneration.GeneratedTypeAccessibility DefaultAccessibility { get; } = defaultAccessibility;

        public bool AddExplicitUsings { get; } = addExplicitUsings;

        public bool UseImplicitOperatorString { get; } = useImplicitOperatorString;

        public int BuildParametersThreshold { get; } = buildParametersThreshold;

        public IReadOnlyDictionary<string, FormatAssertionMode>? FormatModeOverrides { get; } = formatModeOverrides;

        public bool EmitNativeStringEnums { get; } = emitNativeStringEnums;

        public bool EmitNativeFlagsEnums { get; } = emitNativeFlagsEnums;

        public bool EmitUnions { get; } = emitUnions;

        public ILanguageProvider CreateLanguageProvider(string? defaultNamespace, IReadOnlyList<NamedTypeSpecification> namedTypes, bool emitEvaluator)
        {
            var mappedNamedTypes = new CSharpLanguageProvider.NamedType[namedTypes.Count];
            for (int i = 0; i < mappedNamedTypes.Length; i++)
            {
                NamedTypeSpecification namedType = namedTypes[i];
                mappedNamedTypes[i] = new CSharpLanguageProvider.NamedType(namedType.Reference, namedType.DotnetTypeName, namedType.DotnetNamespace, GetAccessibility(namedType.Accessibility));
            }

            return CSharpLanguageProvider.DefaultWithOptions(MapOptions(defaultNamespace, mappedNamedTypes, emitEvaluator));
        }

        // Value equality: the incremental pipeline compares this object to decide whether the
        // options input changed, so two option sets read from identical build properties must
        // compare equal or every options-provider update would regenerate the whole project.
        public bool Equals(GlobalOptions? other)
        {
            return
                other is not null &&
                ReferenceEquals(FallbackVocabulary, other.FallbackVocabulary) &&
                OptionalAsNullable == other.OptionalAsNullable &&
                ExcludeNonNullDefaulted == other.ExcludeNonNullDefaulted &&
                UseOptionalNameHeuristics == other.UseOptionalNameHeuristics &&
                AlwaysAssertFormat == other.AlwaysAssertFormat &&
                DisabledNamingHeuristics.SequenceEqual(other.DisabledNamingHeuristics, StringComparer.Ordinal) &&
                DefaultAccessibility == other.DefaultAccessibility &&
                AddExplicitUsings == other.AddExplicitUsings &&
                UseImplicitOperatorString == other.UseImplicitOperatorString &&
                BuildParametersThreshold == other.BuildParametersThreshold &&
                FormatModeOverridesEqual(FormatModeOverrides, other.FormatModeOverrides) &&
                EmitNativeStringEnums == other.EmitNativeStringEnums &&
                EmitNativeFlagsEnums == other.EmitNativeFlagsEnums &&
                EmitUnions == other.EmitUnions;
        }

        public override bool Equals(object? obj) => obj is GlobalOptions other && Equals(other);

        public override int GetHashCode()
        {
            HashCode hash = default;
            hash.Add(FallbackVocabulary.Uri, StringComparer.Ordinal);
            hash.Add(OptionalAsNullable);
            hash.Add(ExcludeNonNullDefaulted);
            hash.Add(UseOptionalNameHeuristics);
            hash.Add(AlwaysAssertFormat);
            hash.Add(DisabledNamingHeuristics.Length);
            hash.Add(DefaultAccessibility);
            hash.Add(AddExplicitUsings);
            hash.Add(UseImplicitOperatorString);
            hash.Add(BuildParametersThreshold);
            hash.Add(FormatModeOverrides?.Count ?? -1);
            hash.Add(EmitNativeStringEnums);
            hash.Add(EmitNativeFlagsEnums);
            hash.Add(EmitUnions);
            return hash.ToHashCode();
        }

        private static bool FormatModeOverridesEqual(IReadOnlyDictionary<string, FormatAssertionMode>? left, IReadOnlyDictionary<string, FormatAssertionMode>? right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left is null || right is null || left.Count != right.Count)
            {
                return false;
            }

            foreach (KeyValuePair<string, FormatAssertionMode> kvp in left)
            {
                if (!right.TryGetValue(kvp.Key, out FormatAssertionMode mode) || mode != kvp.Value)
                {
                    return false;
                }
            }

            return true;
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

        private CSharpLanguageProvider.Options MapOptions(string? defaultNamespace, CSharpLanguageProvider.NamedType[] namedTypes, bool emitEvaluator)
        {
            CSharpLanguageProvider.Options options = new(
                defaultNamespace ?? "GeneratedTypes",
                namedTypes,
                useOptionalNameHeuristics: UseOptionalNameHeuristics,
                alwaysAssertFormat: AlwaysAssertFormat,
                optionalAsNullable: OptionalAsNullable,
                disabledNamingHeuristics: [.. DisabledNamingHeuristics],
                fileExtension: ".g.cs",
                defaultAccessibility: DefaultAccessibility,
                codeGenerationMode: emitEvaluator ? CodeGenerationMode.Both : CodeGenerationMode.TypeGeneration,
                excludeNonNullDefaulted: ExcludeNonNullDefaulted,
                buildParametersThreshold: BuildParametersThreshold,
                formatModeOverrides: FormatModeOverrides,
                emitNativeStringEnums: EmitNativeStringEnums,
                emitNativeFlagsEnums: EmitNativeFlagsEnums,
                programCompiler: global::Corvus.Json.CodeGenerator.RuntimeProgramCompiler.CompileWithoutRegexTable,
                emitUnions: EmitUnions,
                storeFilesAsStrings: true);

            return options;
        }
    }
}