// <copyright file="CSharpLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
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
    public static string GetFullyQualifiedDotnetTypeName(GeneratedCodeFile generatedCodeFile)
    {
        return GetFullyQualifiedDotnetTypeName(generatedCodeFile.TypeDeclaration);
    }

    /// <summary>
    /// Gets the .NET type name for the <see cref="GeneratedCodeFile"/>.
    /// </summary>
    /// <param name="generatedCodeFile">The generated code file.</param>
    /// <returns>The .NET type name.</returns>
    public static string GetDotnetTypeName(GeneratedCodeFile generatedCodeFile)
    {
        return GetDotnetTypeName(generatedCodeFile.TypeDeclaration);
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

    /// <inheritdoc/>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations)
    {
        CodeGenerator generator = new(this);

        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
            if (generator.TryBeginTypeDeclaration(typeDeclaration))
            {
                foreach (ICodeFileBuilder codeFileBuilder in this.codeFileBuilderRegistry.RegisteredBuilders)
                {
                    codeFileBuilder.EmitFile(generator, typeDeclaration);
                }

                generator.EndTypeDeclaration(typeDeclaration);
            }
        }

        return generator.GetGeneratedCodeFiles(t => new(t.FullyQualifiedDotnetTypeName(), ".cs"));
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
    public void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName)
    {
        // We've already set the dotnet type name.
        if (typeDeclaration.HasDotnetTypeName())
        {
            return;
        }

        string ns = this.options.GetNamespace(typeDeclaration);

        var reference = JsonReferenceBuilder.From(typeDeclaration.LocatedSchema.Location);

        if (reference.HasQuery)
        {
            // Remove the query.
            reference = new JsonReferenceBuilder(reference.Scheme, reference.Authority, reference.Path, [], reference.Fragment);
        }

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        SetTypeNameWithKeywordHeuristics(
            typeDeclaration,
            reference,
            typeNameBuffer,
            ns,
            fallbackName,
            this.GetOrderedNameBeforeSubschemaHeuristics());
    }

    /// <inheritdoc/>
    public void SetNamesAfterSubschema(TypeDeclaration typeDeclaration)
    {
        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];
        UpdateTypeNameWithKeywordHeuristics(
            typeDeclaration,
            reference,
            typeNameBuffer,
            this.GetOrderedNameAfterSubschemaHeuristics());
    }

    /// <inheritdoc/>
    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.Children();
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
            BinaryIfValidationHandler.Instance,
            CompositionAllOfValidationHandler.Instance,
            CompositionAnyOfValidationHandler.Instance,
            CompositionNotValidationHandler.Instance,
            CompositionOneOfValidationHandler.Instance,
            FormatValidationHandler.Instance,
            NumberValidationHandler.Instance,
            ObjectValidationHandler.Instance,
            StringValidationHandler.Instance,
            TernaryIfValidationHandler.Instance,
            TypeValidationHandler.Instance);

        languageProvider.RegisterNameHeuristics(
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

        return languageProvider;
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

    private static void SetTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        string ns,
        string fallbackName,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(typeDeclaration, reference, typeNameBuffer, out int written))
            {
                if (heuristic is IBuiltInTypeNameHeuristic)
                {
                    typeDeclaration.SetDoNotGenerate();
                }
                else
                {
                    // The namespace will have been set by the heuristic for a built-in type.
                    typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                    typeDeclaration.SetDotnetNamespace(ns);
                }

                return;
            }
        }

        typeDeclaration.SetDotnetTypeName(Formatting.FormatTypeNameComponent(typeDeclaration, fallbackName.AsSpan(), typeNameBuffer).ToString());
        typeDeclaration.SetDotnetNamespace(ns);
    }

    private static void UpdateTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(typeDeclaration, reference, typeNameBuffer, out int written))
            {
                typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                return;
            }
        }
    }

    private IEnumerable<INameHeuristic> GetOrderedNameBeforeSubschemaHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics.OfType<INameHeuristicBeforeSubschema>().OrderBy(h => h.Priority)
                : this.nameHeuristicRegistry.RegisteredHeuristics.OfType<INameHeuristicBeforeSubschema>().Where(h => !h.IsOptional).OrderBy(h => h.Priority);
    }

    private IEnumerable<INameHeuristic> GetOrderedNameAfterSubschemaHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics.OfType<INameHeuristicAfterSubschema>().OrderBy(h => h.Priority)
                : this.nameHeuristicRegistry.RegisteredHeuristics.OfType<INameHeuristicAfterSubschema>().Where(h => !h.IsOptional).OrderBy(h => h.Priority);
    }

    /// <summary>
    /// An explicit name for a type at a given reference.
    /// </summary>
    /// <param name="reference">The reference to the schema.</param>
    /// <param name="dotnetTypeName">The dotnet type name to use.</param>
    public readonly struct NamedType(JsonReference reference, string dotnetTypeName)
    {
        /// <summary>
        /// Gets the reference to schema with an explicit name.
        /// </summary>
        internal string Reference { get; } = reference;

        /// <summary>
        /// Gets the dotnet type name.
        /// </summary>
        internal string DotnetTypeName { get; } = dotnetTypeName;
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
    /// <param name="useTypeNameKeywordHeuristics">Indicates whether to use newer type name heuristics based on keyword inspection.</param>
    public class Options(string defaultNamespace, NamedType[]? namedTypes = null, Namespace[]? namespaces = null, bool useTypeNameKeywordHeuristics = true)
    {
        private readonly FrozenDictionary<string, string> namedTypeMap = namedTypes?.ToFrozenDictionary(kvp => kvp.Reference, kvp => kvp.DotnetTypeName) ?? FrozenDictionary<string, string>.Empty;
        private readonly FrozenDictionary<string, string> namespaceMap = namespaces?.ToFrozenDictionary(kvp => kvp.BaseUri, kvp => kvp.DotnetNamespace) ?? FrozenDictionary<string, string>.Empty;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new("GeneratedCode", [], [], useTypeNameKeywordHeuristics: true);

        /// <summary>
        /// Gets the root namespace for code generation.
        /// </summary>
        internal string DefaultNamespace { get; } = defaultNamespace;

        /// <summary>
        /// Gets a value indicating whether to use newer type naming heuristics.
        /// </summary>
        internal bool UseOptionalNameHeuristics { get; } = useTypeNameKeywordHeuristics;

        /// <summary>
        /// Gets the namespace for the base URI.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration for which to get the namespace.</param>
        /// <returns>The namespace.</returns>
        internal string GetNamespace(TypeDeclaration typeDeclaration)
        {
            if (!this.TryGetNamespace(typeDeclaration.LocatedSchema.Location, out string? ns))
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
        internal bool TryGetTypeName(string reference, [NotNullWhen(true)] out string? typeName)
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
            if (!baseUri.HasAbsoluteUri)
            {
                ns = null;
                return false;
            }

            return this.namespaceMap.TryGetValue(baseUri.Uri.ToString(), out ns);
        }
    }
}