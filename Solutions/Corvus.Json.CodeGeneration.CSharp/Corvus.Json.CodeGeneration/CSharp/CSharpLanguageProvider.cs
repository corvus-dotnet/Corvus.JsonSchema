// <copyright file="CSharpLanguageProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Frozen;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
        // We've already set the .NET type name.
        if (typeDeclaration.HasDotnetTypeName())
        {
            return;
        }

        string ns = this.options.GetNamespace(typeDeclaration);

        JsonReferenceBuilder reference = GetReferenceWithoutQuery(typeDeclaration);

        if (this.options.TryGetTypeName(reference.ToString(), out NamedType typeName))
        {
            typeDeclaration.SetDotnetTypeName(typeName.DotnetTypeName);

            if (typeName.DotnetNamespace is string nsOverride)
            {
                typeDeclaration.SetDotnetNamespace(ns);
                typeDeclaration.SetParent(null);
            }
            else
            {
                typeDeclaration.SetDotnetNamespace(ns);
            }

            return;
        }

        Span<char> typeNameBuffer = stackalloc char[Formatting.MaxIdentifierLength];

        this.SetTypeNameWithKeywordHeuristics(
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
        this.UpdateTypeNameWithKeywordHeuristics(
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

        languageProvider.RegisterNameCollisionResolvers(
            DefaultNameCollisionResolver.Instance);

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

    private void UpdateTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(this, typeDeclaration, reference, typeNameBuffer, out int written))
            {
                typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                break;
            }
        }

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
    }

    private void SetTypeNameWithKeywordHeuristics(
        TypeDeclaration typeDeclaration,
        JsonReferenceBuilder reference,
        Span<char> typeNameBuffer,
        string ns,
        string fallbackName,
        IEnumerable<INameHeuristic> nameHeuristics)
    {
        foreach (INameHeuristic heuristic in nameHeuristics)
        {
            if (heuristic.TryGetName(this, typeDeclaration, reference, typeNameBuffer, out int written))
            {
                if (heuristic is IBuiltInTypeNameHeuristic)
                {
                    typeDeclaration.SetDoNotGenerate();
                }
                else
                {
                    written = this.FixTypeNameForCollisionWithParent(typeDeclaration, typeNameBuffer, written);
                    typeDeclaration.SetDotnetTypeName(typeNameBuffer[..written].ToString());
                    typeDeclaration.SetDotnetNamespace(ns);
                }

                return;
            }
        }

        typeDeclaration.SetDotnetTypeName(Formatting.FormatTypeNameComponent(typeDeclaration, fallbackName.AsSpan(), typeNameBuffer).ToString());
        typeDeclaration.SetDotnetNamespace(ns);
    }

    private int FixTypeNameForCollisionWithParent(TypeDeclaration typeDeclaration, Span<char> typeNameBuffer, int written)
    {
        if (typeDeclaration.Parent() is TypeDeclaration parent &&
            !typeDeclaration.IsInDefinitionsContainer() &&
            parent.TryGetDotnetTypeName(out string? name))
        {
            foreach (INameCollisionResolver resolver in this.nameCollisionResolverRegistry.RegisteredCollisionResolvers)
            {
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
    public readonly struct NamedType(JsonReference reference, string dotnetTypeName, string? dotnetNamespace = null)
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
    public class Options(string defaultNamespace, NamedType[]? namedTypes = null, Namespace[]? namespaces = null, bool useOptionalNameHeuristics = true, bool alwaysAssertFormat = true, bool optionalAsNullable = false, string[]? disabledNamingHeuristics = null)
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
        /// Gets a value indicating whether to always assert the format validation, regardless of the vocabularyy.
        /// </summary>
        internal bool AlwaysAssertFormat { get; } = alwaysAssertFormat;

        /// <summary>
        /// Gets a value indicating whether to generate nullable types for optional parameters.
        /// </summary>
        internal bool OptionalAsNullable { get; } = optionalAsNullable;

        /// <summary>
        /// Gets the array of disabled naming heuristics.
        /// </summary>
        internal HashSet<string> DisabledNamingHeuristics { get; } = disabledNamingHeuristics is string[] n ? [..n] : [];

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
            if (!baseUri.HasAbsoluteUri)
            {
                ns = null;
                return false;
            }

            return this.namespaceMap.TryGetValue(baseUri.Uri.ToString(), out ns);
        }
    }
}