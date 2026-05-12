// <copyright file="CSharpLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Frozen;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A delegate for functions that emit code for a type.
/// </summary>
/// <param name="generator">The generator into which to emit the code.</param>
/// <param name="typeName">The (not fully qualified) .NET type name to emit.</param>
public delegate void NamedTypeEmitter(CodeGenerator generator, string typeName);

/// <summary>
/// The C# language provider.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CSharpLanguageProvider"/> class.
/// </remarks>
public class CSharpLanguageProvider : IHierarchicalLanguageProvider
{
    private readonly KeywordValidationHandlerRegistry validationHandlerRegistry = new();
    private readonly CodeFileBuilderRegistry codeFileBuilderRegistry = new();
    private readonly NameHeuristicRegistry nameHeuristicRegistry = new();
    private readonly NameCollisionResolverRegistry nameCollisionResolverRegistry = new();
    private readonly Options options;
    private readonly Dictionary<string, NamedTypes> namedTypesInRootNamespace = new(StringComparer.Ordinal);
    private SimpleCoreTypeNameHeuristic? simpleCoreTypeHeuristic;
    private CodeGenerator? rootNamespaceGenerator = null;
    private IReadOnlyList<IBuiltInTypeNameHeuristic>? cachedBuiltInTypeNameHeuristics;
    private IReadOnlyList<INameHeuristic>? cachedNameBeforeSubschemaHeuristics;
    private IReadOnlyList<INameHeuristic>? cachedNameAfterSubschemaHeuristics;
    private TypeDeclaration[]? evaluatorRootTypes;

    private CSharpLanguageProvider(Options? options = null)
    {
        this.options = options ?? Options.Default;
    }

    /// <summary>
    /// Gets the default <see cref="CSharpLanguageProvider"/> instance.
    /// </summary>
    public static CSharpLanguageProvider Default { get; } = CreateDefaultCSharpLanguageProvider(null);

    /// <summary>
    /// Gets a <see cref="CSharpLanguageProvider"/> instance with the default configuration and specified options.
    /// </summary>
    /// <param name="options">The options to set.</param>
    /// <returns>An instance of a <see cref="CSharpLanguageProvider"/> with the default configuration and specified options.</returns>
    public static CSharpLanguageProvider DefaultWithOptions(CSharpLanguageProvider.Options options)
    {
        return CreateDefaultCSharpLanguageProvider(options);
    }

    /// <summary>
    /// Sets the original (unreduced) root type declarations for standalone evaluator generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This must be called before <see cref="GenerateCodeFor"/> when the code generation mode
    /// includes evaluator generation. The pipeline's <c>GetCandidateTypesToGenerate</c> replaces
    /// reducible types (e.g., annotation-only schemas) with their reduced targets, losing the
    /// original type information needed by the evaluator. By storing the original roots here,
    /// the evaluator generator can access the full unreduced type tree.
    /// </para>
    /// </remarks>
    /// <param name="rootTypes">The original root type declarations from <c>AddTypeDeclarations</c>.</param>
    public void SetEvaluatorRootTypes(params TypeDeclaration[] rootTypes)
    {
        this.evaluatorRootTypes = rootTypes;
    }

    /// <summary>
    /// Gets the fully qualified .NET type name for the <see cref="GeneratedCodeFile"/>.
    /// </summary>
    /// <param name="generatedCodeFile">The generated code file.</param>
    /// <returns>The fully qualified .NET type name.</returns>
    public static string? GetFullyQualifiedDotnetTypeName(GeneratedCodeFile generatedCodeFile)
    {
        return generatedCodeFile.TypeDeclaration is TypeDeclaration t ? GetFullyQualifiedDotnetTypeName(t) : null;
    }

    /// <summary>
    /// Gets the .NET type name for the <see cref="GeneratedCodeFile"/>.
    /// </summary>
    /// <param name="generatedCodeFile">The generated code file.</param>
    /// <returns>The .NET type name.</returns>
    public static string? GetDotnetTypeName(GeneratedCodeFile generatedCodeFile)
    {
        return generatedCodeFile.TypeDeclaration is TypeDeclaration ? GetDotnetTypeName(generatedCodeFile.TypeDeclaration) : null;
    }

    /// <summary>
    /// Gets the fully qualified .NET type name for the <see cref="TypeDeclaration"/>.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The fully qualified .NET type name.</returns>
    public static string GetFullyQualifiedDotnetTypeName(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.FullyQualifiedDotnetTypeName();
    }

    /// <summary>
    /// Gets the .NET type name for the <see cref="TypeDeclaration"/>.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The .NET type name.</returns>
    public static string GetDotnetTypeName(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.DotnetTypeName();
    }

    /// <summary>
    /// Gets the list of available name heuristics.
    /// </summary>
    /// <returns>The ordered list of name heuristics.</returns>
    public IEnumerable<(string Name, bool IsOptional)> GetNameHeuristicNames()
    {
        return nameHeuristicRegistry.RegisteredHeuristics
            .Select(h => (h.GetType().Name, h.IsOptional))
            .Distinct()
            .OrderBy(n => n.IsOptional)
            .ThenBy(n => n.Name);
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics)
    {
        nameHeuristicRegistry.RegisterNameHeuristics(heuristics);
        cachedBuiltInTypeNameHeuristics = null;
        cachedNameBeforeSubschemaHeuristics = null;
        cachedNameAfterSubschemaHeuristics = null;
        return this;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders)
    {
        codeFileBuilderRegistry.RegisterCodeFileBuilders(builders);
        return this;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        validationHandlerRegistry.RegisterValidationHandlers(handlers);
        return this;
    }

    /// <inheritdoc/>
    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
    {
        return validationHandlerRegistry.TryGetHandlersFor(keyword, out validationHandlers);
    }

    /// <summary>
    /// Register name collision resolvers.
    /// </summary>
    /// <param name="resolvers">The name collision resolvers.</param>
    /// <returns>A reference to the <see cref="ILanguageProvider"/> having completed the operation.</returns>
    public ILanguageProvider RegisterNameCollisionResolvers(params INameCollisionResolver[] resolvers)
    {
        nameCollisionResolverRegistry.RegisterNameCollisionResolvers(resolvers);
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
        bool generateTypes = options.CodeGenerationMode is CodeGenerationMode.TypeGeneration or CodeGenerationMode.Both;
        bool generateEvaluator = options.CodeGenerationMode is CodeGenerationMode.SchemaEvaluationOnly or CodeGenerationMode.Both;

#if DEBUG
        Dictionary<string, TypeDeclaration> namesSeen = new(StringComparer.Ordinal);
#endif
        CodeGenerator generator = new(this, cancellationToken, lineEndSequence: options.LineEndSequence);

        // Generate global simple types first. These have DoNotGenerate=true (so
        // ShouldGenerate returns false and the framework sets their parent to null),
        // but the first-seen instance for each canonical name needs to be generated.
        if (generateTypes && simpleCoreTypeHeuristic is { } heuristic)
        {
            foreach (TypeDeclaration globalType in heuristic.GetFirstSeenTypes())
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return [];
                }

#if DEBUG
                string fqn = globalType.FullyQualifiedDotnetTypeName();
                if (namesSeen.ContainsKey(fqn))
                {
                    System.Diagnostics.Debug.Fail($"Duplicate global simple type: {fqn}");
                    continue;
                }
                else
                {
                    namesSeen[fqn] = globalType;
                }
#endif

                if (generator.TryBeginTypeDeclaration(globalType))
                {
                    foreach (ICodeFileBuilder codeFileBuilder in codeFileBuilderRegistry.RegisteredBuilders)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return [];
                        }

                        codeFileBuilder.EmitFile(generator, globalType);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    generator.EndTypeDeclaration(globalType);
                }
            }
        }

        if (generateTypes)
        {
            foreach (TypeDeclaration typeDeclaration in typeDeclarations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return [];
                }

#if DEBUG
                if (namesSeen.ContainsKey(typeDeclaration.FullyQualifiedDotnetTypeName()))
                {
                    System.Diagnostics.Debug.Fail($"Skipped: {typeDeclaration.LocatedSchema.Location}");
                    continue;
                }
                else
                {
                    namesSeen[typeDeclaration.FullyQualifiedDotnetTypeName()] = typeDeclaration;
                }
#endif
                if (generator.TryBeginTypeDeclaration(typeDeclaration))
                {
                    foreach (ICodeFileBuilder codeFileBuilder in codeFileBuilderRegistry.RegisteredBuilders)
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            return [];
                        }

                        codeFileBuilder.EmitFile(generator, typeDeclaration);
                    }

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return [];
                    }

                    generator.EndTypeDeclaration(typeDeclaration);
                }
            }
        }

        List<GeneratedCodeFile> result = [];

        if (generateTypes)
        {
            result.AddRange(generator.GetGeneratedCodeFiles(t => new(t.DotnetTypeNameWithoutNamespace(), options.FileExtension)));

            if (rootNamespaceGenerator is not null)
            {
                result.Add(new GeneratedCodeFile($"{Formatting.GlobalDeclarationsFileName}{options.FileExtension}", rootNamespaceGenerator.ToString()));
            }
        }

        // Use the original (unreduced) root types stored via SetEvaluatorRootTypes,
        // not the types from the filtered pipeline which may have been reduced
        // (e.g., annotation-only schemas become JsonAny/boolean true).
        if (generateEvaluator && this.evaluatorRootTypes is not null)
        {
            foreach (TypeDeclaration rootType in this.evaluatorRootTypes)
            {
                GeneratedCodeFile? evaluatorFile = StandaloneEvaluatorGenerator.Generate(
                    rootType, options, options.LineEndSequence);
                if (evaluatorFile is not null)
                {
                    result.Add(evaluatorFile);
                }
            }
        }

        return result;
    }

    /// <inheritdoc/>
    public bool ShouldGenerate(TypeDeclaration typeDeclaration)
    {
        return !typeDeclaration.DoNotGenerate();
    }

    /// <inheritdoc/>
    public void SetParent(TypeDeclaration child, TypeDeclaration? parent)
    {
        child.SetParent(parent);
    }

    /// <inheritdoc/>
    public TypeDeclaration? GetParent(TypeDeclaration child)
    {
        return child.Parent();
    }

    /// <inheritdoc/>
    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        if (typeDeclaration.HasDotnetTypeName())
        {
            return;
        }

        typeDeclaration.SetCSharpOptions(options);

        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        SetTypeNameWithKeywordHeuristics(
            this,
            typeDeclaration,
            reference,
            typeNameBuffer,
            GetBuiltInTypeNameHeuristics(),
            cancellationToken);
    }

    /// <inheritdoc/>
    public void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName, CancellationToken cancellationToken)
    {
        // We've already set the .NET type name.
        if (typeDeclaration.HasDotnetTypeName())
        {
            return;
        }

        TypeDeclaration? dynamic = null;
        if (typeDeclaration.TryGetDynamicSource(out dynamic) && TrySetNameFromOptions(options, dynamic, typeDeclaration))
        {
            return;
        }

        if (TrySetNameFromOptions(options, typeDeclaration, typeDeclaration))
        {
            return;
        }

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        string ns = options.GetNamespace(typeDeclaration, dynamic);
        JsonReferenceBuilder reference = GetReferenceWithoutQuery(dynamic ?? typeDeclaration);

        SetTypeNameWithKeywordHeuristics(
             typeDeclaration,
             reference,
             typeNameBuffer,
             ns,
             fallbackName,
             GetOrderedNameBeforeSubschemaHeuristics(),
             cancellationToken);

        static bool TrySetNameFromOptions(Options options, TypeDeclaration sourceType, TypeDeclaration targetType)
        {
            string ns = options.GetNamespace(sourceType);
            JsonReferenceBuilder reference = GetReferenceWithoutQuery(sourceType);

            if (options.TryGetTypeName(reference.ToString(), out NamedType typeName))
            {
                targetType.SetDotnetTypeName(typeName.DotnetTypeName);

                if (typeName.DotnetNamespace is string nsOverride)
                {
                    targetType.SetDotnetNamespace(nsOverride);
                    targetType.SetParent(null);
                }
                else
                {
                    targetType.SetDotnetNamespace(ns);
                }

                // Set the accessibility, if it has been explicitly overridden
                if (typeName.Accessibility is GeneratedTypeAccessibility accessibility)
                {
                    targetType.SetDotnetAccessibility(accessibility);
                }

                return true;
            }

            return false;
        }
    }

    /// <inheritdoc/>
    public void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, IEnumerable<TypeDeclaration> existingTypeDeclarations, CancellationToken cancellationToken)
    {
        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        UpdateTypeNameWithKeywordHeuristics(
            typeDeclaration,
            existingTypeDeclarations,
            reference,
            typeNameBuffer,
            GetOrderedNameAfterSubschemaHeuristics(),
            cancellationToken);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.Children();
    }

    /// <summary>
    /// Emits a named type into the root namespace, returning its fully qualified name.
    /// </summary>
    /// <param name="name">The (unqualified) name of the type to emit.</param>
    /// <param name="key">The key which uniquely identifies the type.</param>
    /// <param name="emitter">A delegate which emits the type declaration.</param>
    /// <returns>The fully qualified name of the emitted type.</returns>
    /// <remarks>
    /// <para>
    /// This ensures that only a single instance of a named type with a given key is emitted into the root namespace,
    /// and that any references to that type use the same fully qualified name.
    /// </para>
    /// </remarks>
    public string GetOrEmitNamedTypeInRootNamespace(string name, string key, NamedTypeEmitter emitter)
    {
        if (!namedTypesInRootNamespace.TryGetValue(name, out NamedTypes? namedTypes))
        {
            namedTypes = new NamedTypes();
            namedTypesInRootNamespace.Add(name, namedTypes);
        }

        if (namedTypes.NamedTypeMap.TryGetValue(key, out string? fullyQualifiedName))
        {
            return fullyQualifiedName;
        }

        if (namedTypes.NamedTypeMap.Count != 0)
        {
            int i = namedTypes.NamedTypeMap.Count;
            string currentName;
            do
            {
                // Build a unique name for the type
                currentName = $"{name}{i++}";
            }
            while (namedTypes.NamedTypeMap.ContainsKey(currentName));

            name = currentName;
        }

        fullyQualifiedName = $"{options.DefaultNamespace}.{name}";
        namedTypes.NamedTypeMap.Add(key, fullyQualifiedName);

        if (rootNamespaceGenerator is null)
        {
            rootNamespaceGenerator = new(this, default, lineEndSequence: options.LineEndSequence);

            FrameworkType addExplicitUsings = options.AddExplicitUsings ? FrameworkType.All : FrameworkType.NotEmitted;

            rootNamespaceGenerator
                .AppendAutoGeneratedHeader()
                .AppendSeparatorLine()
                .AppendUsings(
                    new("global::System", addExplicitUsings),
                    new("global::System.Diagnostics", addExplicitUsings),
                    new("global::System.Diagnostics.CodeAnalysis", addExplicitUsings),
                    "global::System.Buffers",
                    "global::System.Buffers.Text",
                    "global::System.Runtime.CompilerServices",
                    "global::Corvus.Text.Json",
                    "global::Corvus.Text.Json.Internal")

                .AppendLineIndent("namespace " + options.DefaultNamespace, ";");
        }

        emitter(rootNamespaceGenerator, name);
        return fullyQualifiedName;
    }

    private static JsonReferenceBuilder GetReferenceWithoutQuery(TypeDeclaration typeDeclaration)
    {
        var reference = JsonReferenceBuilder.From(typeDeclaration.LocatedSchema.Location);

        if (reference.HasQuery)
        {
            // Remove the query.
            reference = new JsonReferenceBuilder(reference.Scheme, reference.Authority, reference.Path, [], reference.Fragment);
        }

        return reference;
    }

    private static CSharpLanguageProvider CreateDefaultCSharpLanguageProvider(Options? options)
    {
        Options resolvedOptions = options ?? Options.Default;
        CSharpLanguageProvider languageProvider = new(resolvedOptions);

        languageProvider.RegisterCodeFileBuilders(
            CorePartial.Instance,
            MutableCorePartial.Instance,
            JsonSchemaPartial.Instance);

        languageProvider.RegisterValidationHandlers(
            TypeValidationHandler.Instance,
            FormatValidationHandler.Instance,
            NumberValidationHandler.Instance,
            StringValidationHandler.Instance,
            ConstValidationHandler.Instance,
            CompositionAllOfValidationHandler.Instance,
            CompositionAnyOfValidationHandler.Instance,
            CompositionOneOfValidationHandler.Instance,
            CompositionNotValidationHandler.Instance,
            TernaryIfValidationHandler.Instance,
            ObjectValidationHandler.Instance,
            ArrayValidationHandler.Instance);

        SimpleCoreTypeNameHeuristic simpleCoreTypeHeuristic = new(resolvedOptions);
        languageProvider.simpleCoreTypeHeuristic = simpleCoreTypeHeuristic;

        languageProvider.RegisterNameHeuristics(
            WellKnownTypeNameHeuristic.Instance,
            simpleCoreTypeHeuristic,
            RequiredPropertyNameHeuristic.Instance,
            DefaultValueNameHeuristic.Instance,
            ConstPropertyNameHeuristic.Instance,
            DocumentationNameHeuristic.Instance,
            BaseSchemaNameHeuristic.Instance,
            CustomKeywordNameHeuristic.Instance,
            PathNameHeuristic.Instance,
            SubschemaNameHeuristic.Instance,
            SingleTypeArrayNameHeuristic.Instance);

        languageProvider.RegisterNameCollisionResolvers(
            DefaultNameCollisionResolver.Instance);

        return languageProvider;
    }

    private static void SetTypeNameWithKeywordHeuristics(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<IBuiltInTypeNameHeuristic> nameHeuristics,
        CancellationToken cancellationToken)
    {
        foreach (IBuiltInTypeNameHeuristic heuristic in nameHeuristics)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (heuristic.TryGetName(languageProvider, typeDeclaration, reference, typeNameBuffer, out int _))
            {
                typeDeclaration.SetDoNotGenerate();
                return;
            }
        }
    }

    private void UpdateTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        IEnumerable<TypeDeclaration> existingDeclarations,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<INameHeuristic> nameHeuristics,
        CancellationToken cancellationToken)
    {
        if (!options.TryGetTypeName(reference.ToString(), out _))
        {
            // We only apply the heuristics if we do not have an explicit type name
            foreach (INameHeuristic heuristic in nameHeuristics)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                if (heuristic.TryGetName(this, typeDeclaration, reference, typeNameBuffer, out int written))
                {
                    typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                    break;
                }
            }
        }

        // But we always apply the collision resolution
        if (typeDeclaration.Parent() is TypeDeclaration parent &&
            parent.FindChildNameCollision(typeDeclaration, typeDeclaration.DotnetTypeName().AsSpan()) is TypeDeclaration child
            && child.Parent() == parent)
        {
            // We have found this same type through multiple dynamic paths. If we are not the one with the dynamic reference, we will set ourselves
            // to DONOTGENERATE, and the other one will be the one that is actually generated.
            JsonReferenceBuilder builder = typeDeclaration.LocatedSchema.Location.AsBuilder();
            if (builder.HasQuery && !child.DoNotGenerate())
            {
                typeDeclaration.SetDoNotGenerate(resetParent: false);
            }
            else if (!typeDeclaration.DoNotGenerate())
            {
                child.SetDoNotGenerate(resetParent: false);
            }
        }

        string fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();
        string baseName = typeDeclaration.DotnetTypeName();

        // And now resolve any matching names from definitions containers
        if (!typeDeclaration.DoNotGenerate() &&
            typeDeclaration.IsInDefinitionsContainer())
        {
            int index = 1;
            while (existingDeclarations.Any(t => t != typeDeclaration && !t.DoNotGenerate() && t.HasDotnetTypeName() && t.FullyQualifiedDotnetTypeName() == fqdtn))
            {
                typeDeclaration.SetDotnetTypeName($"{baseName}{index++}");
                fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();
            }
        }
    }

    private void SetTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        string ns,
        string fallbackName,
        IEnumerable<INameHeuristic> nameHeuristics,
        CancellationToken cancellationToken)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (heuristic.TryGetName(this, typeDeclaration, reference, typeNameBuffer, out int written))
            {
                if (heuristic is IBuiltInTypeNameHeuristic)
                {
                    typeDeclaration.SetDoNotGenerate();
                }
                else
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    written = FixTypeNameForCollisionWithParent(typeDeclaration, typeNameBuffer, written, cancellationToken);

                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                    typeDeclaration.SetDotnetNamespace(ns);
                }

                return;
            }
        }

        typeDeclaration.SetDotnetTypeName(Formatting.FormatTypeNameComponent(typeDeclaration, fallbackName.AsSpan(), typeNameBuffer).ToString());
        typeDeclaration.SetDotnetNamespace(ns);
    }

    private int FixTypeNameForCollisionWithParent(TypeDeclaration typeDeclaration, Span<char> typeNameBuffer, int written, CancellationToken cancellationToken)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent &&
            !typeDeclaration.IsInDefinitionsContainer() &&
            parent.TryGetDotnetTypeName(out string? name))
        {
            foreach (INameCollisionResolver resolver in nameCollisionResolverRegistry.RegisteredCollisionResolvers)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return 0;
                }

                if (resolver.TryResolveNameCollision(this, typeDeclaration, parent, name.AsSpan(), typeNameBuffer, written, out int newLength))
                {
                    return newLength;
                }
            }
        }

        return written;
    }

    private IReadOnlyList<IBuiltInTypeNameHeuristic> GetBuiltInTypeNameHeuristics()
    {
        return cachedBuiltInTypeNameHeuristics ??= (
            options.UseOptionalNameHeuristics
                ? nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<IBuiltInTypeNameHeuristic>()
                    .Where(h => !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<IBuiltInTypeNameHeuristic>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)).ToArray();
    }

    private IReadOnlyList<INameHeuristic> GetOrderedNameBeforeSubschemaHeuristics()
    {
        return cachedNameBeforeSubschemaHeuristics ??= (
            options.UseOptionalNameHeuristics
                ? nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicBeforeSubschema>()
                    .Where(h => !options.DisabledNamingHeuristics
                    .Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicBeforeSubschema>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics
                    .Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)).ToArray();
    }

    private IReadOnlyList<INameHeuristic> GetOrderedNameAfterSubschemaHeuristics()
    {
        return cachedNameAfterSubschemaHeuristics ??= (
            options.UseOptionalNameHeuristics
                ? nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)).ToArray();
    }

    /// <summary>
    /// An explicit name for a type at a given reference.
    /// </summary>
    /// <param name="reference">The reference to the schema.</param>
    /// <param name="dotnetTypeName">The .NET type name to use.</param>
    /// <param name="dotnetNamespace">The (optional) .NET namespace to use.</param>
    /// <param name="accessibility">The (optional) accessibility for the type.</param>
    public readonly struct NamedType(JsonReference reference, string dotnetTypeName, string? dotnetNamespace = null, GeneratedTypeAccessibility? accessibility = null)
    {
        /// <summary>
        /// Gets the reference to schema with an explicit name.
        /// </summary>
        internal string Reference { get; } = reference;

        /// <summary>
        /// Gets the .NET type name.
        /// </summary>
        internal string DotnetTypeName { get; } = dotnetTypeName;

        /// <summary>
        /// Gets the (optional) .NET namespace to use for the type.
        /// </summary>
        /// <remarks>
        /// Providing a value for this property will ensure that the type
        /// is generated in the root of this global namespace, rather than
        /// as a child of its parent.
        /// </remarks>
        internal string? DotnetNamespace { get; } = dotnetNamespace;

        /// <summary>
        /// Gets the (optional) accessibility to use for the type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Providing a value for this property will ensure that the type is generated
        /// with the given accessibility.
        /// </para>
        /// <para>
        /// Any types for which this is the parent will inherit the accessibility of this
        /// type. However, care must be taken not to reference/expose types which have
        /// a more restricted accessibility than the type itself.
        /// </para>
        /// <para>You should consider using <see cref="Options.DefaultAccessibility"/>
        /// to set the accessibility for all generated types rather overriding a specific type.
        /// </para>
        /// </remarks>
        internal GeneratedTypeAccessibility? Accessibility { get; } = accessibility;
    }

    /// <summary>
    /// An explicit mapping to a namespace for schema with a particular
    /// base uri.
    /// </summary>
    /// <param name="baseUri">The base uri.</param>
    /// <param name="dotnetNamespace">The dotnet namespace to use.</param>
    public readonly struct Namespace(JsonReference baseUri, string dotnetNamespace)
    {
        /// <summary>
        /// Gets the base uri.
        /// </summary>
        internal string BaseUri { get; } = baseUri;

        /// <summary>
        /// Gets the dotnet namespace for schema in that base URI.
        /// </summary>
        internal string DotnetNamespace { get; } = dotnetNamespace;
    }

    /// <summary>
    /// Options for the <see cref="CSharpLanguageProvider"/>.
    /// </summary>
    /// <param name="defaultNamespace">The default namespace into which to generate types if not otherwise specified.</param>
    /// <param name="namedTypes">Specifically named types.</param>
    /// <param name="namespaces">Specific namespaces for a given base URI.</param>
    /// <param name="useOptionalNameHeuristics">Indicates whether to use optional name heuristics.</param>
    /// <param name="alwaysAssertFormat">If true, then Format will always be treated as a validation assertion keyword, regardless of the vocabulary.</param>
    /// <param name="optionalAsNullable">If true, then generate nullable types for optional parameters.</param>
    /// <param name="disabledNamingHeuristics">The list of well-known names of naming heuristics to disable.</param>
    /// <param name="fileExtension">Gets the file extension to use. Defaults to <c>.cs</c>.</param>
    /// <param name="useImplicitOperatorString">If true, then the string conversion will be implicit.</param>
    /// <param name="lineEndSequence">The line-end sequence. Defaults to <c>\r\n</c>.</param>
    /// <param name="addExplicitUsings">If true, then the generated files will include using statements for the standard implicit usings. You should use this when your project does not use implicit usings.</param>
    /// <param name="defaultAccessibility">Defines the accessibility of the generated types. Defaults to <see cref="GeneratedTypeAccessibility.Public"/>.</param>
    public class Options(
        string defaultNamespace,
        NamedType[]? namedTypes = null,
        Namespace[]? namespaces = null,
        bool useOptionalNameHeuristics = true,
        bool alwaysAssertFormat = true,
        bool optionalAsNullable = false,
        string[]? disabledNamingHeuristics = null,
        string fileExtension = ".cs",
        bool useImplicitOperatorString = false,
        string lineEndSequence = "\r\n",
        bool addExplicitUsings = false,
        GeneratedTypeAccessibility defaultAccessibility = GeneratedTypeAccessibility.Public,
        CodeGenerationMode codeGenerationMode = CodeGenerationMode.TypeGeneration)
    {
        private readonly FrozenDictionary<string, NamedType> namedTypeMap = namedTypes?.ToFrozenDictionary(kvp => kvp.Reference, kvp => kvp) ?? FrozenDictionary<string, NamedType>.Empty;
        private readonly FrozenDictionary<string, string> namespaceMap = namespaces?.ToFrozenDictionary(kvp => kvp.BaseUri, kvp => kvp.DotnetNamespace) ?? FrozenDictionary<string, string>.Empty;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new("GeneratedCode", [], [], useOptionalNameHeuristics: true, alwaysAssertFormat: true);

        /// <summary>
        /// Gets the root namespace for code generation.
        /// </summary>
        internal string DefaultNamespace { get; } = defaultNamespace;

        /// <summary>
        /// Gets a value indicating whether to use newer type naming heuristics.
        /// </summary>
        internal bool UseOptionalNameHeuristics { get; } = useOptionalNameHeuristics;

        /// <summary>
        /// Gets a value indicating whether to always assert the format validation, regardless of the vocabulary.
        /// </summary>
        internal bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

        /// <summary>
        /// Gets a value indicating whether to generate nullable types for optional parameters.
        /// </summary>
        internal bool OptionalAsNullable { get; } = optionalAsNullable;

        /// <summary>
        /// Gets the file extension (including the leading '.').
        /// </summary>
        internal string FileExtension { get; } = fileExtension;

        /// <summary>
        /// Gets a value indicating whether to generate an implicit operator for conversion to <see langword="string"/>.
        /// </summary>
        internal bool UseImplicitOperatorString { get; } = useImplicitOperatorString;

        /// <summary>
        /// Gets the line end sequence to use.
        /// </summary>
        internal string LineEndSequence { get; } = lineEndSequence;

        /// <summary>
        /// Gets the array of disabled naming heuristics.
        /// </summary>
        internal HashSet<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics is string[] n ? new(n, StringComparer.Ordinal) : new(StringComparer.Ordinal);

        /// <summary>
        /// Gets a value indicating whether to include using statements for the standard implicit usings.
        /// </summary>
        /// <remarks>
        ///  You should use this when your project does not use implicit usings.
        /// </remarks>
        internal bool AddExplicitUsings { get; } = addExplicitUsings;

        /// <summary>
        /// Gets the default accessibility of the generated types.
        /// </summary>
        internal GeneratedTypeAccessibility DefaultAccessibility { get; } = defaultAccessibility;

        /// <summary>
        /// Gets the code generation mode, determining whether to generate types,
        /// a standalone schema evaluator, or both.
        /// </summary>
        internal CodeGenerationMode CodeGenerationMode { get; } = codeGenerationMode;

        /// <summary>
        /// Gets the namespace for the base URI.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration for which to get the namespace.</param>
        /// <param name="dynamic">The dynamic source of the type declaration if available.</param>
        /// <returns>The namespace.</returns>
        internal string GetNamespace(TypeDeclaration typeDeclaration, TypeDeclaration? dynamic = null)
        {
            if (dynamic is TypeDeclaration d && TryGetNamespace(d.LocatedSchema.Location, out string? ns))
            {
                return ns;
            }

            if (!TryGetNamespace(typeDeclaration.LocatedSchema.Location, out ns))
            {
                ns = DefaultNamespace;
            }

            return ns;
        }

        /// <summary>
        /// Try to get the specific type name for the reference.
        /// </summary>
        /// <param name="reference">The reference .</param>
        /// <param name="typeName">The resulting type name.</param>
        /// <returns><see langword="true"/> if the name was provided.</returns>
        internal bool TryGetTypeName(string reference, [NotNullWhen(true)] out NamedType typeName)
        {
            return namedTypeMap.TryGetValue(reference, out typeName);
        }

        /// <summary>
        /// Try to get the namespace for the base URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="ns">The resulting namespace.</param>
        /// <returns><see langword="true"/> if the namespace was provided.</returns>
        private bool TryGetNamespace(JsonReference baseUri, [NotNullWhen(true)] out string? ns)
        {
            if (!baseUri.HasAbsoluteUri)
            {
                ns = null;
                return false;
            }

            return namespaceMap.TryGetValue(baseUri.Uri.ToString(), out ns);
        }
    }

    private class NamedTypes
    {
        // Named types by the unique key identifying the specific type.
        // Used in the dictionary of names to named types in the global namespace.
        public Dictionary<string, string> NamedTypeMap { get; } = [];
    }
}