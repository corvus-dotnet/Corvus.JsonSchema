// <copyright file="CSharpLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// The C# language provider.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="CSharpLanguageProvider"/> class.
/// </remarks>
public class CSharpLanguageProvider(CSharpLanguageProvider.Options? options = null) : IHierarchicalLanguageProvider
{
    private readonly KeywordValidationHandlerRegistry validationHandlerRegistry = new();
    private readonly CodeFileBuilderRegistry codeFileBuilderRegistry = new();
    private readonly NameHeuristicRegistry nameHeuristicRegistry = new();
    private readonly NameCollisionResolverRegistry nameCollisionResolverRegistry = new();
    private readonly Options options = options ?? Options.Default;

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
        return generatedCodeFile.TypeDeclaration is TypeDeclaration t ? GetDotnetTypeName(t) : null;
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
        return this.nameHeuristicRegistry.RegisteredHeuristics
            .Select(h => (h.GetType().Name, h.IsOptional))
            .Distinct()
            .OrderBy(n => n.IsOptional)
            .ThenBy(n => n.Name);
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics)
    {
        this.nameHeuristicRegistry.RegisterNameHeuristics(heuristics);
        return this;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders)
    {
        this.codeFileBuilderRegistry.RegisterCodeFileBuilders(builders);
        return this;
    }

    /// <inheritdoc/>
    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        this.validationHandlerRegistry.RegisterValidationHandlers(handlers);
        return this;
    }

    /// <inheritdoc/>
    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
    {
        return this.validationHandlerRegistry.TryGetHandlersFor(keyword, out validationHandlers);
    }

    /// <summary>
    /// Register name collision resolvers.
    /// </summary>
    /// <param name="resolvers">The name collision resolvers.</param>
    /// <returns>A reference to the <see cref="ILanguageProvider"/> having completed the operation.</returns>
    public ILanguageProvider RegisterNameCollisionResolvers(params INameCollisionResolver[] resolvers)
    {
        this.nameCollisionResolverRegistry.RegisterNameCollisionResolvers(resolvers);
        return this;
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
#if DEBUG
        Dictionary<string, TypeDeclaration> namesSeen = [];
#endif
        CodeGenerator generator = new(this, cancellationToken, lineEndSequence: this.options.LineEndSequence);

        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return [];
            }
#if DEBUG
            if (namesSeen.ContainsKey(typeDeclaration.FullyQualifiedDotnetTypeName()))
            {
                Debug.Fail($"Skipped: {typeDeclaration.LocatedSchema.Location}");
                continue;
            }
            else
            {
                namesSeen[typeDeclaration.FullyQualifiedDotnetTypeName()] = typeDeclaration;
            }
#endif
            if (generator.TryBeginTypeDeclaration(typeDeclaration))
            {
                foreach (ICodeFileBuilder codeFileBuilder in this.codeFileBuilderRegistry.RegisteredBuilders)
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

        return generator.GetGeneratedCodeFiles(t => new(t.DotnetTypeNameWithoutNamespace(), this.options.FileExtension));
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

        typeDeclaration.SetCSharpOptions(this.options);

        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        SetTypeNameWithKeywordHeuristics(
            this,
            typeDeclaration,
            reference,
            typeNameBuffer,
            this.GetBuiltInTypeNameHeuristics(),
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
        if (typeDeclaration.TryGetDynamicSource(out dynamic) && TrySetNameFromOptions(this.options, dynamic, typeDeclaration))
        {
            return;
        }

        if (TrySetNameFromOptions(this.options, typeDeclaration, typeDeclaration))
        {
            return;
        }

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        string ns = this.options.GetNamespace(typeDeclaration, dynamic);
        JsonReferenceBuilder reference = GetReferenceWithoutQuery(dynamic ?? typeDeclaration);

        this.SetTypeNameWithKeywordHeuristics(
             typeDeclaration,
             reference,
             typeNameBuffer,
             ns,
             fallbackName,
             this.GetOrderedNameBeforeSubschemaHeuristics(),
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
        this.UpdateTypeNameWithKeywordHeuristics(
            typeDeclaration,
            existingTypeDeclarations,
            reference,
            typeNameBuffer,
            this.GetOrderedNameAfterSubschemaHeuristics(),
            cancellationToken);
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.Children();
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
        CSharpLanguageProvider languageProvider = new(options);

        languageProvider.RegisterCodeFileBuilders(
            CorePartial.Instance,
            ArrayPartial.Instance,
            ObjectPartial.Instance,
            BooleanPartial.Instance,
            StringPartial.Instance,
            NumberPartial.Instance,
            ValidatePartial.Instance);

        languageProvider.RegisterValidationHandlers(
            ArrayValidationHandler.Instance,
            CompositionAllOfValidationHandler.Instance,
            CompositionAnyOfValidationHandler.Instance,
            CompositionNotValidationHandler.Instance,
            CompositionOneOfValidationHandler.Instance,
            ConstValidationHandler.Instance,
            FormatValidationHandler.Instance,
            NumberValidationHandler.Instance,
            ObjectValidationHandler.Instance,
            StringValidationHandler.Instance,
            TernaryIfValidationHandler.Instance,
            TypeValidationHandler.Instance);

        languageProvider.RegisterNameHeuristics(
            BuiltInNullTypeNameHeuristic.Instance,
            BuiltInBooleanTypeNameHeuristic.Instance,
            BuiltInObjectTypeNameHeuristic.Instance,
            BuiltInArrayTypeNameHeuristic.Instance,
            BuiltInStringTypeNameHeuristic.Instance,
            BuiltInNumberTypeNameHeuristic.Instance,
            BuiltInIntegerTypeNameHeuristic.Instance,
            WellKnownTypeNameHeuristic.Instance,
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
        if (!this.options.TryGetTypeName(reference.ToString(), out _))
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

                    written = this.FixTypeNameForCollisionWithParent(typeDeclaration, typeNameBuffer, written, cancellationToken);

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
            foreach (INameCollisionResolver resolver in this.nameCollisionResolverRegistry.RegisteredCollisionResolvers)
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

    private IEnumerable<IBuiltInTypeNameHeuristic> GetBuiltInTypeNameHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<IBuiltInTypeNameHeuristic>()
                    .Where(h => !this.options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<IBuiltInTypeNameHeuristic>()
                    .Where(h => !h.IsOptional && !this.options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name);
    }

    private IEnumerable<INameHeuristic> GetOrderedNameBeforeSubschemaHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicBeforeSubschema>()
                    .Where(h => !this.options.DisabledNamingHeuristics
                    .Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicBeforeSubschema>()
                    .Where(h => !h.IsOptional && !this.options.DisabledNamingHeuristics
                    .Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name);
    }

    private IEnumerable<INameHeuristic> GetOrderedNameAfterSubschemaHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !this.options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name)
                : this.nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !h.IsOptional && !this.options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name);
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
        GeneratedTypeAccessibility defaultAccessibility = GeneratedTypeAccessibility.Public)
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
        internal HashSet<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics is string[] n ? [.. n] : [];

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
        /// Try to get the namespace for the base URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="namespaceMap">The namespace map to search.</param>
        /// <param name="ns">The resulting namespace.</param>
        /// <returns><see langword="true"/> if the namespace was provided.</returns>
        /// <remarks>
        /// This method first tries an exact match, then falls back to prefix matching
        /// to find the longest matching base URI.
        /// </remarks>
        public static bool TryGetNamespace(JsonReference baseUri, FrozenDictionary<string, string> namespaceMap, [NotNullWhen(true)] out string? ns)
        {
            if (!baseUri.HasAbsoluteUri)
            {
                ns = null;
                return false;
            }

            string uriString = baseUri.Uri.ToString();

            // First try an exact match
            if (namespaceMap.TryGetValue(uriString, out ns))
            {
                return true;
            }

            // Then try prefix matching - find the longest matching base URI
            string? bestMatch = null;
            string? bestNamespace = null;

            foreach (KeyValuePair<string, string> kvp in namespaceMap)
            {
                if (uriString.StartsWith(kvp.Key, StringComparison.Ordinal) &&
                    (bestMatch is null || kvp.Key.Length > bestMatch.Length))
                {
                    bestMatch = kvp.Key;
                    bestNamespace = kvp.Value;
                }
            }

            if (bestNamespace is not null)
            {
                ns = bestNamespace;
                return true;
            }

            ns = null;
            return false;
        }

        /// <summary>
        /// Gets the namespace for the base URI.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration for which to get the namespace.</param>
        /// <param name="dynamic">The dynamic source of the type declaration if available.</param>
        /// <returns>The namespace.</returns>
        internal string GetNamespace(TypeDeclaration typeDeclaration, TypeDeclaration? dynamic = null)
        {
            if (dynamic is TypeDeclaration d && this.TryGetNamespace(d.LocatedSchema.Location, out string? ns))
            {
                return ns;
            }

            if (!this.TryGetNamespace(typeDeclaration.LocatedSchema.Location, out ns))
            {
                ns = this.DefaultNamespace;
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
            return this.namedTypeMap.TryGetValue(reference, out typeName);
        }

        /// <summary>
        /// Try to get the namespace for the base URI.
        /// </summary>
        /// <param name="baseUri">The base URI.</param>
        /// <param name="ns">The resulting namespace.</param>
        /// <returns><see langword="true"/> if the namespace was provided.</returns>
        private bool TryGetNamespace(JsonReference baseUri, [NotNullWhen(true)] out string? ns)
        {
            return TryGetNamespace(baseUri, this.namespaceMap, out ns);
        }
    }
}