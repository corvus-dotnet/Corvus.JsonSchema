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
#if DEBUG
        Dictionary<string, TypeDeclaration> namesSeen = [];
#endif
        CodeGenerator generator = new(this);

        foreach (TypeDeclaration typeDeclaration in typeDeclarations)
        {
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
            typeDeclaration.SetCSharpOptions(this.options);

            if (generator.TryBeginTypeDeclaration(typeDeclaration))
            {
                foreach (ICodeFileBuilder codeFileBuilder in this.codeFileBuilderRegistry.RegisteredBuilders)
                {
                    codeFileBuilder.EmitFile(generator, typeDeclaration);
                }

                generator.EndTypeDeclaration(typeDeclaration);
            }
        }

        return generator.GetGeneratedCodeFiles(t => new(t.DotnetTypeNameWithoutNamespace(), ".cs"));
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
    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.HasDotnetTypeName())
        {
            return;
        }

        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        SetTypeNameWithKeywordHeuristics(
            this,
            typeDeclaration,
            reference,
            typeNameBuffer,
            this.GetBuiltInTypeNameHeuristics());
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
        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        if (this.options.TryGetTypeName(reference.ToString(), out string? typeName))
        {
            typeDeclaration.SetDotnetTypeName(typeName);
            typeDeclaration.SetDotnetNamespace(ns);

            return;
        }

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        SetTypeNameWithKeywordHeuristics(
            this,
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
            this,
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

        return languageProvider;
    }

    private static void SetTypeNameWithKeywordHeuristics(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<IBuiltInTypeNameHeuristic> nameHeuristics)
    {
        foreach (IBuiltInTypeNameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(languageProvider, typeDeclaration, reference, typeNameBuffer, out int _))
            {
                typeDeclaration.SetDoNotGenerate();
                return;
            }
        }
    }

    private static void SetTypeNameWithKeywordHeuristics(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        string ns,
        string fallbackName,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(languageProvider, typeDeclaration, reference, typeNameBuffer, out int written))
            {
                if (heuristic is IBuiltInTypeNameHeuristic)
                {
                    typeDeclaration.SetDoNotGenerate();
                }
                else
                {
                    written = FixTypeNameForCollisionWithParent(typeDeclaration, typeNameBuffer, written);
                    typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                    typeDeclaration.SetDotnetNamespace(ns);
                }

                return;
            }
        }

        typeDeclaration.SetDotnetTypeName(Formatting.FormatTypeNameComponent(typeDeclaration, fallbackName.AsSpan(), typeNameBuffer).ToString());
        typeDeclaration.SetDotnetNamespace(ns);
    }

    private static int FixTypeNameForCollisionWithParent(TypeDeclaration typeDeclaration, Span<char> typeNameBuffer, int written)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent && !typeDeclaration.IsInDefinitionsContainer() && parent.TryGetDotnetTypeName(out string? name))
        {
            ReadOnlySpan<char> parentSpan = name.AsSpan();

            // Capture the reference outside the loop so we can work through it.
            JsonReference updatedReference = typeDeclaration.LocatedSchema.Location.MoveToParentFragment();
            Span<char> trimmedStringBuffer = stackalloc char[typeNameBuffer.Length];

            for (int index = 1; parent.DotnetTypeName().AsSpan().Equals(typeNameBuffer[..written], StringComparison.Ordinal) || NameMatchesChildren(parent, typeDeclaration, typeNameBuffer[..written]); index++)
            {
                int trimmedStringLength = written;
                while (trimmedStringLength > 1 && typeNameBuffer[trimmedStringLength - 1] >= '0' && typeNameBuffer[trimmedStringLength - 1] <= '9')
                {
                    trimmedStringLength--;
                }

                typeNameBuffer[..trimmedStringLength].CopyTo(trimmedStringBuffer);

                ReadOnlySpan<char> trimmedString = trimmedStringBuffer[..trimmedStringLength];

                while (updatedReference.HasFragment && IsPropertySubschemaKeywordFragment(typeDeclaration, updatedReference.Fragment))
                {
                    updatedReference = updatedReference.MoveToParentFragment();
                }

                int slashIndex = 0;

                if (updatedReference.HasFragment && (slashIndex = updatedReference.Fragment.LastIndexOf('/')) >= 0 && slashIndex < updatedReference.Fragment.Length - 1)
                {
                    ReadOnlySpan<char> previousNode = updatedReference.Fragment[(slashIndex + 1)..];
                    previousNode.CopyTo(typeNameBuffer);
                    written = Formatting.ToPascalCase(typeNameBuffer[..previousNode.Length]);
                    trimmedString.CopyTo(typeNameBuffer[previousNode.Length..]);
                    written += trimmedString.Length;
                }
                else if (!trimmedString.Equals(parent.DotnetTypeName().AsSpan(), StringComparison.Ordinal))
                {
                    parentSpan.CopyTo(typeNameBuffer);
                    trimmedString.CopyTo(typeNameBuffer[parentSpan.Length..]);
                    written = parentSpan.Length + trimmedString.Length;
                    index--;
                }
                else
                {
                    trimmedString.CopyTo(typeNameBuffer);
                    written = trimmedStringLength;
                    written += Formatting.ApplySuffix(index, typeNameBuffer[trimmedStringLength..]);
                }
            }
        }

        return written;
    }

    private static bool IsPropertySubschemaKeywordFragment(TypeDeclaration typeDeclaration, ReadOnlySpan<char> fragment)
    {
        foreach (IPropertySubchemaProviderKeyword keyword in typeDeclaration.LocatedSchema.Vocabulary.Keywords.OfType<IPropertySubchemaProviderKeyword>())
        {
            if (keyword.Keyword.AsSpan().Equals(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static TypeDeclaration? FindMatchingChild(TypeDeclaration parent, TypeDeclaration typeDeclaration, ReadOnlySpan<char> span)
    {
        foreach (TypeDeclaration child in parent.Children())
        {
            TypeDeclaration reducedChild = child.ReducedTypeDeclaration().ReducedType;
            if (reducedChild != typeDeclaration && reducedChild.TryGetDotnetTypeName(out string? typeName) && typeName.AsSpan().Equals(span, StringComparison.Ordinal))
            {
                return reducedChild;
            }
        }

        return null;
    }

    private static bool NameMatchesChildren(TypeDeclaration parent, TypeDeclaration typeDeclaration, ReadOnlySpan<char> span)
    {
        return FindMatchingChild(parent, typeDeclaration, span) is not null;
    }

    private static void UpdateTypeNameWithKeywordHeuristics(
        CSharpLanguageProvider languageProvider,
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(languageProvider, typeDeclaration, reference, typeNameBuffer, out int written))
            {
                typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                break;
            }
        }

        if (typeDeclaration.Parent() is TypeDeclaration parent &&
            FindMatchingChild(parent, typeDeclaration, typeDeclaration.DotnetTypeName().AsSpan()) is TypeDeclaration child
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
    }

    private IEnumerable<IBuiltInTypeNameHeuristic> GetBuiltInTypeNameHeuristics()
    {
        return
            this.options.UseOptionalNameHeuristics
                ? this.nameHeuristicRegistry.RegisteredHeuristics.OfType<IBuiltInTypeNameHeuristic>().OrderBy(h => h.Priority)
                : this.nameHeuristicRegistry.RegisteredHeuristics.OfType<IBuiltInTypeNameHeuristic>().Where(h => !h.IsOptional).OrderBy(h => h.Priority);
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
    /// <param name="alwaysAssertFormat">If true, then Format will always be treated as a validation assertion keyword, regardless of the vocabulary.</param>
    public class Options(string defaultNamespace, NamedType[]? namedTypes = null, Namespace[]? namespaces = null, bool useTypeNameKeywordHeuristics = true, bool alwaysAssertFormat = true)
    {
        private readonly FrozenDictionary<string, string> namedTypeMap = namedTypes?.ToFrozenDictionary(kvp => kvp.Reference, kvp => kvp.DotnetTypeName) ?? FrozenDictionary<string, string>.Empty;
        private readonly FrozenDictionary<string, string> namespaceMap = namespaces?.ToFrozenDictionary(kvp => kvp.BaseUri, kvp => kvp.DotnetNamespace) ?? FrozenDictionary<string, string>.Empty;

        /// <summary>
        /// Gets the default options.
        /// </summary>
        public static Options Default { get; } = new("GeneratedCode", [], [], useTypeNameKeywordHeuristics: true, alwaysAssertFormat: true);

        /// <summary>
        /// Gets the root namespace for code generation.
        /// </summary>
        internal string DefaultNamespace { get; } = defaultNamespace;

        /// <summary>
        /// Gets a value indicating whether to use newer type naming heuristics.
        /// </summary>
        internal bool UseOptionalNameHeuristics { get; } = useTypeNameKeywordHeuristics;

        /// <summary>
        /// Gets a value indicating whether to always assert the format validation, regardless of the vocabularyy.
        /// </summary>
        internal bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

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