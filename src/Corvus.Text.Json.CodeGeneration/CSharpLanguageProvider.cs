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
public class CSharpLanguageProvider : IHierarchicalLanguageProvider, ISchemaProgramUtf8LanguageProvider, IOrderingLanguageProvider
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
    private IReadOnlyList<TypeDeclaration>? programRootTypes;
    private IReadOnlyList<KeyValuePair<string, string>> schemaDocuments = [];
    private IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>>? utf8SchemaDocuments;
    private string? fallbackVocabularyUri;
    private readonly List<(string RootDocumentUri, string RootDocumentPointer)> programEntries = [];
    private readonly Dictionary<(string, string), int> programEntryIndex = new();
    private readonly Dictionary<string, List<TypeDeclaration>> namedTypesByFullyQualifiedName = new(StringComparer.Ordinal);
    private IEnumerable<TypeDeclaration>? namedTypesPass;

    private CSharpLanguageProvider(Options? options = null)
    {
        this.options = options ?? Options.Default;
    }

    /// <summary>
    /// Gets the default <see cref="CSharpLanguageProvider"/> instance.
    /// </summary>
    public static CSharpLanguageProvider Default { get; } = CreateDefaultCSharpLanguageProvider(null);

    /// <summary>
    /// Gets the comparer with which the names that reach generated code are ordered: <see cref="StringComparer.Ordinal"/>,
    /// so that the output does not depend on the culture of the generating process.
    /// </summary>
    public IComparer<string> OrderingComparer => StringComparer.Ordinal;

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

    /// <summary>Gets the name of the emitted schema evaluation program class.</summary>
    internal const string ProgramClassName = "CorvusJsonSchemaProgram";

    /// <summary>Gets the fully qualified reference to the program class for use in generated code.</summary>
    internal string ProgramClassReference => options.DefaultNamespace.Length == 0 ? "global::" + ProgramClassName : "global::" + options.DefaultNamespace + "." + ProgramClassName;

    /// <inheritdoc/>
    public void SetProgramRootTypes(IReadOnlyList<TypeDeclaration> rootTypes)
    {
        this.programRootTypes = rootTypes;
    }

    /// <inheritdoc/>
    public void SetSchemaDocuments(IReadOnlyList<KeyValuePair<string, string>> documents, string? fallbackVocabularyUri)
    {
        this.schemaDocuments = documents;
        this.utf8SchemaDocuments = null;
        this.fallbackVocabularyUri = fallbackVocabularyUri;
    }

    /// <inheritdoc/>
    public void SetSchemaDocuments(IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> documents, string? fallbackVocabularyUri)
    {
        this.utf8SchemaDocuments = documents;
        this.schemaDocuments = [];
        this.fallbackVocabularyUri = fallbackVocabularyUri;
    }

    /// <summary>
    /// Gets (registering on first use) the program entry point index for a type declaration's schema, or -1 when
    /// the type has no located schema document.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The entry point index.</returns>
    internal int GetProgramEntry(TypeDeclaration typeDeclaration)
    {
        LocatedSchema located = this.GetEntryType(typeDeclaration).LocatedSchema;
        if (located.RootDocumentUri.Length == 0)
        {
            return -1;
        }

        (string, string) key = (located.RootDocumentUri, located.RootDocumentPointer);
        if (!this.programEntryIndex.TryGetValue(key, out int index))
        {
            index = this.programEntries.Count;
            this.programEntries.Add(key);
            this.programEntryIndex.Add(key, index);
        }

        return index;
    }

    /// <summary>
    /// Gets the type whose schema location is the entry point for a generated type: the type itself when it is a
    /// requested root, else the first requested root that reduces to it (a root that is nothing but a <c>$ref</c>, say),
    /// so that evaluation starts at the schema as written, with its dynamic scope, rather than at the reduced target.
    /// </summary>
    private TypeDeclaration GetEntryType(TypeDeclaration typeDeclaration)
    {
        if (this.programRootTypes is null)
        {
            return typeDeclaration;
        }

        foreach (TypeDeclaration root in this.programRootTypes)
        {
            if (ReferenceEquals(root, typeDeclaration))
            {
                return typeDeclaration;
            }
        }

        foreach (TypeDeclaration root in this.programRootTypes)
        {
            if (root.LocatedSchema.RootDocumentUri.Length > 0 &&
                ReferenceEquals(root.ReducedTypeDeclaration().ReducedType, typeDeclaration))
            {
                return root;
            }
        }

        return typeDeclaration;
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
            .ThenBy(n => n.Name, StringComparer.Ordinal);
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

            if (options.EmitUnions && typeDeclarations.Any(t => t.UnionCaseTypes() is not null))
            {
                result.Add(new GeneratedCodeFile($"{Formatting.UnionAttributeFileName}{options.FileExtension}", UnionAttributePolyfill(options.LineEndSequence)));
            }
        }

        // Standalone evaluators are thin entry points into the program. Use the original (unreduced) root
        // types stored via SetEvaluatorRootTypes so that the entry point is the schema as written.
        if (generateEvaluator && this.evaluatorRootTypes is not null)
        {
            foreach (TypeDeclaration rootType in this.evaluatorRootTypes)
            {
                int entry = this.GetProgramEntry(rootType);
                if (entry >= 0)
                {
                    result.Add(RuntimeProgramGenerator.GenerateStandaloneEvaluator(
                        options.GetNamespace(rootType),
                        RuntimeProgramGenerator.GetEvaluatorClassName(rootType),
                        this.ProgramClassReference,
                        entry,
                        options.FileExtension,
                        options.LineEndSequence));
                }
                else if (rootType.LocatedSchema.IsBooleanSchema)
                {
                    // A boolean root reduces to the built-in any/not-any type, which has no document of its own
                    // and therefore no program entry; the shim compiles the constant schema itself.
                    result.Add(RuntimeProgramGenerator.GenerateBooleanStandaloneEvaluator(
                        options.GetNamespace(rootType),
                        RuntimeProgramGenerator.GetEvaluatorClassName(rootType),
                        rootType.LocatedSchema.Schema.ValueKind == System.Text.Json.JsonValueKind.True,
                        options.FileExtension,
                        options.LineEndSequence));
                }
            }
        }

        if (this.programEntries.Count > 0)
        {
            IEnumerable<string> documentUris = this.utf8SchemaDocuments is { } utf8Documents
                ? utf8Documents.Select(d => d.Key)
                : this.schemaDocuments.Select(d => d.Key);
            IReadOnlyDictionary<string, string> keys = RuntimeProgramGenerator.MapDocumentKeys(
                documentUris.Concat(this.programEntries.Select(e => e.RootDocumentUri)));
            List<RuntimeProgramGenerator.SchemaDocumentSource> documents = [];
            if (this.utf8SchemaDocuments is { } utf8SchemaDocumentList)
            {
                // The documents stay UTF-8 from the type builder to the program compiler.
                foreach (KeyValuePair<string, ReadOnlyMemory<byte>> document in utf8SchemaDocumentList)
                {
                    documents.Add(new RuntimeProgramGenerator.SchemaDocumentSource(keys[document.Key], RuntimeProgramGenerator.MapReferenceDocument(document.Value, keys)));
                }
            }
            else
            {
                foreach (KeyValuePair<string, string> document in this.schemaDocuments)
                {
                    documents.Add(new RuntimeProgramGenerator.SchemaDocumentSource(keys[document.Key], RuntimeProgramGenerator.MapReferenceDocument(document.Value, keys)));
                }
            }

            List<string> entryPoints = [];
            foreach ((string rootDocumentUri, string rootDocumentPointer) in this.programEntries)
            {
                entryPoints.Add(keys[rootDocumentUri] + "#" + rootDocumentPointer);
            }

            string rootDocumentKey = documents.Count > 0 ? documents[0].Key : keys[this.programEntries[0].RootDocumentUri];
            string dialect = RuntimeProgramGenerator.DialectFor(this.fallbackVocabularyUri);
            List<KeyValuePair<string, string>> formatModes = options.FormatModeOverrides.OrderBy(m => m.Key, StringComparer.Ordinal).Select(m => new KeyValuePair<string, string>(m.Key, m.Value.ToString())).ToList();
            SchemaProgramImage? image = options.ProgramCompiler?.Invoke(
                this.utf8SchemaDocuments is not null
                    ? SchemaProgramSource.FromUtf8(
                        documents.Select(d => new KeyValuePair<string, ReadOnlyMemory<byte>>(d.Key, d.Utf8Json)).ToList(),
                        rootDocumentKey,
                        entryPoints,
                        dialect,
                        options.AlwaysAssertFormat,
                        formatModes)
                    : new SchemaProgramSource(
                        documents.Select(d => new KeyValuePair<string, string>(d.Key, d.Json)).ToList(),
                        rootDocumentKey,
                        entryPoints,
                        dialect,
                        options.AlwaysAssertFormat,
                        formatModes));
            result.Add(RuntimeProgramGenerator.Generate(
                options.DefaultNamespace,
                ProgramClassName,
                documents,
                rootDocumentKey,
                entryPoints,
                dialect,
                options.AlwaysAssertFormat,
                formatModes,
                options.FileExtension,
                options.LineEndSequence,
                image));
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
                targetType.SetCSharpOptions(options);
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

        fullyQualifiedName = options.DefaultNamespace.Length == 0 ? name : $"{options.DefaultNamespace}.{name}";
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
                    "global::Corvus.Text.Json.Internal");

            if (options.DefaultNamespace.Length != 0)
            {
                rootNamespaceGenerator.AppendLineIndent("namespace " + options.DefaultNamespace, ";");
            }
        }

        emitter(rootNamespaceGenerator, name);
        return fullyQualifiedName;
    }

    /// <summary>
    /// The <c>[Union]</c> attribute the generated union types carry, for target frameworks whose runtime does not
    /// define it. The C# compiler recognises the attribute by name, so an internal copy in the generated assembly
    /// is enough; a project that already brings its own copy defines
    /// <c>CORVUS_TEXT_JSON_NO_UNION_ATTRIBUTE_POLYFILL</c> to leave this one out.
    /// </summary>
    private static string UnionAttributePolyfill(string lineEndSequence)
    {
        const string text = """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------

            #if !NET11_0_OR_GREATER && !CORVUS_TEXT_JSON_NO_UNION_ATTRIBUTE_POLYFILL
            namespace System.Runtime.CompilerServices
            {
                /// <summary>
                /// Marks a type as a C# union type. This is the attribute the .NET 11 runtime defines, supplied here for
                /// earlier target frameworks so that the C# 15 compiler treats the generated composition types as unions.
                /// </summary>
                [global::System.AttributeUsage(global::System.AttributeTargets.Class | global::System.AttributeTargets.Struct, AllowMultiple = false, Inherited = false)]
                [global::System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
                internal sealed class UnionAttribute : global::System.Attribute
                {
                }
            }
            #endif

            """;

        return lineEndSequence == "\n" ? text.Replace("\r\n", "\n") : text.Replace("\r\n", "\n").Replace("\n", lineEndSequence);
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

        // The declarations named so far in this naming pass are indexed by the fully-qualified
        // name they had when they were named, instead of scanning all of them for every type.
        // This gives the same answer as the scan: a declaration is only renamed while it is the
        // one being named (the collision loop below), and only its own rename invalidates its
        // cached fully-qualified name, so an indexed name stays current; the candidates found
        // through the index are still checked against the live predicate.
        if (!ReferenceEquals(this.namedTypesPass, existingDeclarations))
        {
            this.namedTypesPass = existingDeclarations;
            this.namedTypesByFullyQualifiedName.Clear();
        }

        // And now resolve any matching fully-qualified names.
        // This handles definitions containers (the original case) and also inline schemas
        // at different locations that derive the same type name from their structure — e.g.
        // multiple OpenAPI response schemas with identical inline objects.
        if (!typeDeclaration.DoNotGenerate())
        {
            int index = 1;
            while (this.HasNamedTypeCollision(typeDeclaration, fqdtn))
            {
                typeDeclaration.SetDotnetTypeName($"{baseName}{index++}");
                fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();
            }
        }

        if (typeDeclaration.HasDotnetTypeName())
        {
            if (!this.namedTypesByFullyQualifiedName.TryGetValue(fqdtn, out List<TypeDeclaration>? named))
            {
                named = [];
                this.namedTypesByFullyQualifiedName.Add(fqdtn, named);
            }

            named.Add(typeDeclaration);
        }
    }

    private bool HasNamedTypeCollision(TypeDeclaration typeDeclaration, string fqdtn)
    {
        if (this.namedTypesByFullyQualifiedName.TryGetValue(fqdtn, out List<TypeDeclaration>? candidates))
        {
            foreach (TypeDeclaration t in candidates)
            {
                if (t != typeDeclaration && !t.DoNotGenerate() && t.HasDotnetTypeName() && t.FullyQualifiedDotnetTypeName() == fqdtn)
                {
                    return true;
                }
            }
        }

        return false;
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
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<IBuiltInTypeNameHeuristic>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)).ToArray();
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
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicBeforeSubschema>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics
                    .Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)).ToArray();
    }

    private IReadOnlyList<INameHeuristic> GetOrderedNameAfterSubschemaHeuristics()
    {
        return cachedNameAfterSubschemaHeuristics ??= (
            options.UseOptionalNameHeuristics
                ? nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)
                : nameHeuristicRegistry.RegisteredHeuristics
                    .OfType<INameHeuristicAfterSubschema>()
                    .Where(h => !h.IsOptional && !options.DisabledNamingHeuristics.Contains(h.GetType().Name))
                    .OrderBy(h => h.Priority)
                    .ThenBy(h => h.GetType().Name, StringComparer.Ordinal)).ToArray();
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
    /// <param name="codeGenerationMode">The code generation mode to use.</param>
    /// <param name="excludeNonNullDefaulted">If true (and <paramref name="optionalAsNullable"/> is true), then optional properties that declare a non-null <c>default</c> are generated as non-nullable types.</param>
    /// <param name="buildParametersThreshold">The maximum estimated number of captured value slots an object type's <c>Build(...)</c> property-parameter overload may hold before it is omitted (and callers fall back to the delegate/context <c>Build</c> form). See <see cref="DefaultBuildParametersThreshold"/>.</param>
    /// <param name="formatModeOverrides">Per-format assertion mode overrides, keyed by format name (e.g. <c>date-time</c>). An override takes precedence over both the vocabulary's format-assertion behaviour and <paramref name="alwaysAssertFormat"/>.</param>
    /// <param name="emitUnions">If true (the default), a type whose schema is a <c>oneOf</c> or <c>anyOf</c> composition is also a C# union: <c>switch</c> and <c>is</c> patterns over its branch types work with the C# 15 compiler (the .NET 11 SDK).</param>
    /// <param name="emitNativeStringEnums">If true (the default), a pure string-enum schema additionally generates a nested native C# enum with conversions.</param>
    /// <param name="emitNativeFlagsEnums">If true (the default), an object schema whose declared properties are all boolean additionally generates a nested native C# <c>[Flags]</c> enum with conversions.</param>
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
        CodeGenerationMode codeGenerationMode = CodeGenerationMode.TypeGeneration,
        bool excludeNonNullDefaulted = false,
        int buildParametersThreshold = 32,
        IReadOnlyDictionary<string, FormatAssertionMode>? formatModeOverrides = null,
        bool emitNativeStringEnums = true,
        bool emitNativeFlagsEnums = true,
        SchemaProgramCompiler? programCompiler = null,
        bool emitUnions = true)
    {
        internal bool EmitUnions { get; } = emitUnions;

        /// <summary>
        /// Gets the ahead-of-time program compiler, or <see langword="null"/> to emit the schema documents and compile
        /// at first use.
        /// </summary>
        internal SchemaProgramCompiler? ProgramCompiler { get; } = programCompiler;

        /// <summary>
        /// The default value for <see cref="BuildParametersThreshold"/>.
        /// </summary>
        /// <remarks>
        /// This bounds the estimated stack footprint that the property-parameter <c>Build(...)</c>
        /// convenience overload adds to an object type's <c>Source</c> ref struct, by capping the
        /// total number of captured value slots (transitively, counting nested emitting object
        /// properties) the overload may hold. Object types whose estimate exceeds this value omit
        /// the overload and keep the delegate/context <c>Build</c> form.
        /// <para>
        /// Must be kept in sync with the literal default of the <c>buildParametersThreshold</c>
        /// constructor parameter (a primary-constructor default cannot reference this constant).
        /// </para>
        /// </remarks>
        public const int DefaultBuildParametersThreshold = 32;

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
        /// Gets the per-format assertion mode overrides, keyed by format name (e.g. <c>date-time</c>).
        /// </summary>
        /// <remarks>
        /// An override takes precedence over both the vocabulary's format-assertion behaviour
        /// and <see cref="AlwaysAssertFormat"/>.
        /// </remarks>
        internal FrozenDictionary<string, FormatAssertionMode> FormatModeOverrides { get; } =
            formatModeOverrides is { Count: > 0 }
                ? formatModeOverrides.ToFrozenDictionary(StringComparer.Ordinal)
                : FrozenDictionary<string, FormatAssertionMode>.Empty;

        /// <summary>
        /// Gets a value indicating whether to generate nullable types for optional parameters.
        /// </summary>
        internal bool OptionalAsNullable { get; } = optionalAsNullable;

        /// <summary>
        /// Gets a value indicating whether optional properties that declare a non-null
        /// <c>default</c> are generated as non-nullable types when <see cref="OptionalAsNullable"/> is set.
        /// </summary>
        internal bool ExcludeNonNullDefaulted { get; } = excludeNonNullDefaulted;

        /// <summary>
        /// Gets the maximum estimated number of captured value slots an object type's
        /// <c>Build(...)</c> property-parameter overload may hold before it is omitted.
        /// </summary>
        /// <remarks>
        /// See <see cref="DefaultBuildParametersThreshold"/> for the meaning of the estimate.
        /// </remarks>
        internal int BuildParametersThreshold { get; } = buildParametersThreshold;

        /// <summary>
        /// Gets a value indicating whether a pure string-enum schema additionally generates
        /// a nested native C# enum with conversions.
        /// </summary>
        internal bool EmitNativeStringEnums { get; } = emitNativeStringEnums;

        /// <summary>
        /// Gets a value indicating whether an object schema whose declared properties are all
        /// boolean additionally generates a nested native C# <c>[Flags]</c> enum with conversions.
        /// </summary>
        internal bool EmitNativeFlagsEnums { get; } = emitNativeFlagsEnums;

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