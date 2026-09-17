// <copyright file="JsonSchemaTypeBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Walks a JSON schema and builds a type map of it.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JsonSchemaTypeBuilder"/> class.
/// </remarks>
/// <param name="documentResolver">The document resolver to use.</param>
/// <param name="vocabularyRegistry">The vocabulary registry.</param>
public class JsonSchemaTypeBuilder(
    IDocumentResolver documentResolver,
    VocabularyRegistry vocabularyRegistry)
{
    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = new(StringComparer.Ordinal);
    private readonly JsonSchemaRegistry schemaRegistry = new(documentResolver, vocabularyRegistry);
    private readonly HashSet<TypeDeclaration> propertiesCollected = [];
    private IVocabulary? lastFallbackVocabulary;

    /// <summary>
    /// Walk a JSON document to build a JSON schema type declaration.
    /// </summary>
    /// <param name="documentPath">The path to the root of the json-schema document.</param>
    /// <param name="fallbackVocabulary">The default vocabulary to use if one cannot be analysed.</param>
    /// <param name="rebaseAsRoot">
    /// Whether to rebase the <paramref name="documentPath"/> as a root document.
    /// This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langword="true"/>, then references in this document should be taken as if the fragment was the root of a document.
    /// This will effectively generate a custom $id for the root scope.
    /// </param>
    /// <param name="cancellationToken">A cancellation token to abandon generation.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>
    /// This method may be called multiple times to build up a set of related types,
    /// perhaps from multiple fragments of a single document, or a family of related documents.
    /// </para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="GenerateCodeUsing(ILanguageProvider, CancellationToken, TypeDeclaration[])"/> to generate the code for each language you need to support.</para>
    /// <para>Note: this requires the <see cref="IDocumentResolver"/> to be pre-configured with all the files required by the type build.
    /// If the <see cref="ValueTask{JsonElement}"/> returned by <see cref="IDocumentResolver.TryResolve(JsonReference)"/> is not
    /// completed immediately on return, it will throw an invalid operation exception.</para>
    /// </remarks>
    public TypeDeclaration AddTypeDeclarations(
        JsonReference documentPath,
        IVocabulary fallbackVocabulary,
        bool rebaseAsRoot = false,
        CancellationToken? cancellationToken = null)
    {
        this.lastFallbackVocabulary = fallbackVocabulary;
        ValueTask<TypeDeclaration> task = this.AddTypeDeclarationsAsync(documentPath, fallbackVocabulary, rebaseAsRoot, cancellationToken);

        if (!task.IsCompleted)
        {
            throw new InvalidOperationException("An async operation occurred on the synchronous code path. Was the IDocumentResolver pre-propulated with all required files?");
        }

        return task.Result;
    }

    /// <summary>
    /// Walk a JSON document to build a JSON schema type declaration.
    /// </summary>
    /// <param name="documentPath">The path to the root of the json-schema document.</param>
    /// <param name="fallbackVocabulary">The default vocabulary to use if one cannot be analysed.</param>
    /// <param name="rebaseAsRoot">Whether to rebase the <paramref name="documentPath"/> as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langword="true"/>, then references in this document should be taken as if the fragment was the root of a document. This will effectively generate a custom $id for the root scope.</param>
    /// <param name="cancellationToken">A cancellation token to abandon generation.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>This method may be called multiple times to build up a set of related types, perhaps from multiple fragments of a single document, or a family of related documents.</para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="GenerateCodeUsing(ILanguageProvider, CancellationToken, TypeDeclaration[])"/> to generate the code for each language you need to support.</para>
    /// </remarks>
    public async ValueTask<TypeDeclaration> AddTypeDeclarationsAsync(
        JsonReference documentPath,
        IVocabulary fallbackVocabulary,
        bool rebaseAsRoot = false,
        CancellationToken? cancellationToken = null)
    {
        this.lastFallbackVocabulary = fallbackVocabulary;
        CancellationToken ct = cancellationToken ?? CancellationToken.None;

        // First we do a document "load" - this enables us to build the map of the schema, anchors etc.
        (JsonReference schemaLocation, JsonReference baseLocation) = await this.schemaRegistry.RegisterBaseSchema(documentPath, fallbackVocabulary, rebaseAsRoot, ct);

        if (ct.IsCancellationRequested)
        {
            return WellKnownTypeDeclarations.JsonAny;
        }

        if (!baseLocation.HasUri)
        {
            // Move down to the base location.
            baseLocation = baseLocation.Apply(new("."));
        }

        // Then we do a second "contextual" pass over the loaded schema from the root location. This enables
        // us to build correct dynamic references.
        if (!this.schemaRegistry.TryGetLocatedSchema(schemaLocation, out LocatedSchema? schema))
        {
            throw new InvalidOperationException($"Unable to find the schema in the registry at {schemaLocation}");
        }

        // We give the type builder context the shared schema registry,
        // the schema we are building, and the map of located type declarations.
        // This allows us to accumulate a shared set of types based on multiple schema sharing the same common
        // schema.
        TypeBuilderContext typeBuilderContext = new(this.schemaRegistry, this.locatedTypeDeclarations, schema, baseLocation);

        TypeDeclaration rootTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope(ct);

        if (ct.IsCancellationRequested)
        {
            return WellKnownTypeDeclarations.JsonAny;
        }

        this.CollectProperties(rootTypeDeclaration, ct);

        if (ct.IsCancellationRequested)
        {
            return WellKnownTypeDeclarations.JsonAny;
        }

        return rootTypeDeclaration;
    }

    /// <summary>
    /// Generates code for the types using the given language provider.
    /// </summary>
    /// <param name="languageProvider">The <see cref="ILanguageProvider"/> for which to generate code.</param>
    /// <param name="rootTypeDeclarations">The root type declarations for which to generate types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The <see cref="GeneratedCodeFile"/> collection.</returns>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeUsing(ILanguageProvider languageProvider, IEnumerable<TypeDeclaration> rootTypeDeclarations, CancellationToken cancellationToken)
    {
        return this.GenerateCodeUsing(languageProvider, cancellationToken, rootTypeDeclarations.ToArray());
    }

    /// <summary>
    /// Generates code for the root type declarations, handing each file to the sink as it is completed when the
    /// provider streams (<see cref="IStreamingLanguageProvider"/>), and feeding the sink from the collection otherwise.
    /// </summary>
    /// <param name="languageProvider">The language provider.</param>
    /// <param name="rootTypeDeclarations">The root type declarations.</param>
    /// <param name="sink">The sink that receives each file.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public void GenerateCodeUsing(ILanguageProvider languageProvider, IEnumerable<TypeDeclaration> rootTypeDeclarations, IGeneratedCodeFileSink sink, CancellationToken cancellationToken)
    {
        IEnumerable<TypeDeclaration> typeDeclarations = this.PrepareGeneration(languageProvider, rootTypeDeclarations.ToArray(), cancellationToken);
        if (languageProvider is IStreamingLanguageProvider streamingProvider)
        {
            streamingProvider.GenerateCodeFor(typeDeclarations, sink, cancellationToken);
            return;
        }

        foreach (GeneratedCodeFile file in languageProvider.GenerateCodeFor(typeDeclarations, cancellationToken))
        {
            sink.Add(file);
        }
    }

    /// <summary>
    /// Generates code for the types using the given language provider.
    /// </summary>
    /// <param name="languageProvider">The <see cref="ILanguageProvider"/> for which to generate code.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <param name="rootTypeDeclarations">The root type declarations for which to generate types.</param>
    /// <returns>The <see cref="GeneratedCodeFile"/> collection.</returns>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeUsing(ILanguageProvider languageProvider, CancellationToken cancellationToken, params TypeDeclaration[] rootTypeDeclarations)
    {
        return languageProvider.GenerateCodeFor(this.PrepareGeneration(languageProvider, rootTypeDeclarations, cancellationToken), cancellationToken);
    }

    // The passes every generation runs before a provider emits: ordering comparer, candidates, non-generated
    // types, parents, names, and the schema program set-up.
    private IEnumerable<TypeDeclaration> PrepareGeneration(ILanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations, CancellationToken cancellationToken)
    {
        // The names that reach generated code (properties, subschemas, documentation keywords) are ordered with the
        // type declaration's comparer, so the provider's comparer is applied to every declaration this builder has built
        // before anything is ordered. The process-wide shared declarations have no properties or subschemas to order.
        IComparer<string> orderingComparer = languageProvider is IOrderingLanguageProvider orderingProvider
            ? orderingProvider.OrderingComparer
            : Comparer<string>.Default;
        foreach (TypeDeclaration typeDeclaration in this.locatedTypeDeclarations.Values)
        {
            if (!typeDeclaration.IsShared)
            {
                typeDeclaration.SetOrderingComparer(orderingComparer);
            }
        }

        IReadOnlyList<TypeDeclaration> candidateTypesToGenerate = GetCandidateTypesToGenerate(rootTypeDeclarations, cancellationToken);

        MarkNonGeneratedTypes(languageProvider, rootTypeDeclarations, cancellationToken);

        // A language provider can opt out of being hierarchical by not implementing
        // the IHierarchicalLanguageProvider interface. In that case, we don't need to set parents.
        if (languageProvider is IHierarchicalLanguageProvider hierarchicalProvider)
        {
            this.SetParents(hierarchicalProvider, rootTypeDeclarations, cancellationToken);
        }

        SetNames(languageProvider, rootTypeDeclarations, cancellationToken);

        IEnumerable<TypeDeclaration> typeDeclarations = candidateTypesToGenerate.Where(languageProvider.ShouldGenerate);

        if (languageProvider is ISchemaProgramLanguageProvider programProvider)
        {
            programProvider.SetProgramRootTypes(rootTypeDeclarations);
            if (programProvider is ISchemaProgramUtf8LanguageProvider utf8ProgramProvider)
            {
                utf8ProgramProvider.SetSchemaDocuments(this.GetSchemaDocumentsUtf8(), this.lastFallbackVocabulary?.Uri);
            }
            else
            {
                programProvider.SetSchemaDocuments(this.GetSchemaDocuments(), this.lastFallbackVocabulary?.Uri);
            }
        }

        return typeDeclarations;
    }

    /// <summary>
    /// Gets the text of every root document the builder has loaded, keyed by root document URI, in first-seen order.
    /// </summary>
    /// <returns>The documents.</returns>
    public IReadOnlyList<KeyValuePair<string, string>> GetSchemaDocuments()
    {
        List<KeyValuePair<string, string>> result = [];
        foreach ((string uri, JsonElement element) in this.CollectSchemaDocumentElements())
        {
            result.Add(new KeyValuePair<string, string>(uri, element.GetRawText()));
        }

        return result;
    }

    /// <summary>
    /// Gets the UTF-8 JSON text of every root document the builder has loaded, keyed by root document URI, in
    /// first-seen order: the same documents as <see cref="GetSchemaDocuments"/>, without a string copy.
    /// </summary>
    /// <returns>The documents.</returns>
    public IReadOnlyList<KeyValuePair<string, ReadOnlyMemory<byte>>> GetSchemaDocumentsUtf8()
    {
        List<KeyValuePair<string, ReadOnlyMemory<byte>>> result = [];
        foreach ((string uri, JsonElement element) in this.CollectSchemaDocumentElements())
        {
#if NET9_0_OR_GREATER
            // The element's own UTF-8 text, copied once out of the document's buffer.
            result.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(uri, System.Runtime.InteropServices.JsonMarshal.GetRawUtf8Value(element).ToArray()));
#else
            result.Add(new KeyValuePair<string, ReadOnlyMemory<byte>>(uri, System.Text.Encoding.UTF8.GetBytes(element.GetRawText())));
#endif
        }

        return result;
    }

    private List<(string Uri, JsonElement Element)> CollectSchemaDocumentElements()
    {
        List<(string Uri, JsonElement Element)> result = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (LocatedSchema located in this.schemaRegistry.LocatedSchemas)
        {
            string uri = located.RootDocumentUri;
            if (uri.Length == 0 || !seen.Add(uri))
            {
                continue;
            }

            // A rebased island or a synthetic $ref root is a virtual resource of the registry, not a resolver document.
            JsonElement? root = this.schemaRegistry.TryGetVirtualResource(uri.AsSpan(), out JsonElement virtualRoot)
                ? virtualRoot
                : documentResolver.TryResolve(new JsonReference(uri)).AsTask().GetAwaiter().GetResult();
            if (root is JsonElement element)
            {
                result.Add((uri, element));
                AddCustomMetaschemas(element);
            }
        }

        return result;

        // A schema whose $schema is not one of the standard metaschemas needs that metaschema at evaluation time to
        // discover its $vocabulary; the standard ones are built into the evaluator.
        void AddCustomMetaschemas(JsonElement schema)
        {
            while (schema.ValueKind == JsonValueKind.Object &&
                   schema.TryGetProperty("$schema", out JsonElement metaschemaElement) &&
                   metaschemaElement.ValueKind == JsonValueKind.String &&
                   metaschemaElement.GetString() is string metaschemaUri)
            {
                int fragment = metaschemaUri.IndexOf('#');
                if (fragment >= 0)
                {
                    metaschemaUri = metaschemaUri.Substring(0, fragment);
                }

                if (metaschemaUri.Length == 0 ||
                    metaschemaUri.StartsWith("http://json-schema.org/", StringComparison.Ordinal) ||
                    metaschemaUri.StartsWith("https://json-schema.org/", StringComparison.Ordinal) ||
                    !seen.Add(metaschemaUri))
                {
                    return;
                }

                JsonElement? metaschema = documentResolver.TryResolve(new JsonReference(metaschemaUri)).AsTask().GetAwaiter().GetResult();
                if (metaschema is not JsonElement metaschemaRoot)
                {
                    return;
                }

                result.Add((metaschemaUri, metaschemaRoot));
                schema = metaschemaRoot;
            }
        }
    }

    /// <summary>
    /// Gets every schema the builder has registered, including those that reduce away during type generation.
    /// </summary>
    public IEnumerable<LocatedSchema> LocatedSchemas => this.schemaRegistry.LocatedSchemas;

    /// <summary>
    /// Add a document to the document resolver for the type builder.
    /// </summary>
    /// <param name="uri">The uri for the document.</param>
    /// <param name="jsonDocument">The document to add at the path.</param>
    public void AddDocument(string uri, JsonDocument jsonDocument)
    {
        documentResolver.AddDocument(uri, jsonDocument);
    }

    /// <summary>
    /// Gets the candidate set of types to build, given we start at the given root type declaration(s).
    /// </summary>
    /// <param name="rootTypeDeclarations">The root type declarations for which to generate types.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A set of types that need to be built.</returns>
    /// <remarks>This will then be further filtered by the <see cref="ILanguageProvider"/> to eliminate built-in types.</remarks>
    private static IReadOnlyList<TypeDeclaration> GetCandidateTypesToGenerate(TypeDeclaration[] rootTypeDeclarations, CancellationToken cancellationToken)
    {
        HashSet<TypeDeclaration> typesToGenerate = [];
        foreach (TypeDeclaration rootTypeDeclaration in rootTypeDeclarations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Array.Empty<TypeDeclaration>();
            }

            GetTypesToGenerateCore(rootTypeDeclaration, typesToGenerate, cancellationToken);
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return Array.Empty<TypeDeclaration>();
        }

        return [.. typesToGenerate.OrderBy(t => t.LocatedSchema.Location)];

        static void GetTypesToGenerateCore(TypeDeclaration type, HashSet<TypeDeclaration> typesToGenerate, CancellationToken cancellationToken)
        {
            if (typesToGenerate.Contains(type))
            {
                return;
            }

            if (!type.CanReduce())
            {
                // We only add ourselves if we can't be reduced.
                typesToGenerate.Add(type);
                foreach (TypeDeclaration child in type.SubschemaTypeDeclarations.Values)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    GetTypesToGenerateCore(child, typesToGenerate, cancellationToken);
                }
            }
            else
            {
                ReducedTypeDeclaration reducedTypeDeclaration = type.ReducedTypeDeclaration();
                GetTypesToGenerateCore(reducedTypeDeclaration.ReducedType, typesToGenerate, cancellationToken);
            }
        }
    }

    private static void MarkNonGeneratedTypes(ILanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations, CancellationToken cancellationToken)
    {
        HashSet<TypeDeclaration> visitedTypes = [];

        // Deal with the well-known type declarations first.
        IdentifyNonGeneratedTypes(WellKnownTypeDeclarations.JsonAny, visitedTypes, cancellationToken);
        IdentifyNonGeneratedTypes(WellKnownTypeDeclarations.JsonNotAny, visitedTypes, cancellationToken);

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            IdentifyNonGeneratedTypes(type, visitedTypes, cancellationToken);
        }

        void IdentifyNonGeneratedTypes(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations, CancellationToken cancellationToken)
        {
            // Quit early if we are already visiting the type declaration.
            if (!visitedTypeDeclarations.Add(typeDeclaration))
            {
                return;
            }

            // We only set a name for ourselves if we cannot be reduced.
            if (!typeDeclaration.CanReduce())
            {
                // Set the name for this type
                languageProvider.IdentifyNonGeneratedType(typeDeclaration, cancellationToken);
            }

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.OrderedSubschemaTypeDeclarations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                IdentifyNonGeneratedTypes(child, visitedTypeDeclarations, cancellationToken);
            }
        }
    }

    private static void SetNames(ILanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations, CancellationToken cancellationToken)
    {
        HashSet<TypeDeclaration> visitedTypes = [];

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetNamesBeforeSubschema(type, visitedTypes, cancellationToken);
        }

        visitedTypes.Clear();

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetNamesAfterSubschema(type, visitedTypes, cancellationToken);
        }

        void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations, CancellationToken cancellationToken)
        {
            // Quit early if we are already visiting the type declaration.
            if (!visitedTypeDeclarations.Add(typeDeclaration))
            {
                return;
            }

            // We only set a name for ourselves if we cannot be reduced.
            if (typeDeclaration.CanReduce())
            {
                typeDeclaration = typeDeclaration.ReducedTypeDeclaration().ReducedType;
                SetNamesBeforeSubschema(typeDeclaration, visitedTypeDeclarations, cancellationToken);
                return;
            }

            languageProvider.SetNamesBeforeSubschema(typeDeclaration, "Entity", cancellationToken);

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.OrderedSubschemaTypeDeclarations)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                TypeDeclaration reducedChild = child.CanReduce() ? child.ReducedTypeDeclaration().ReducedType : child;
                SetNamesBeforeSubschema(reducedChild, visitedTypeDeclarations, cancellationToken);
            }
        }

        void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations, CancellationToken cancellationToken)
        {
            // Quit early if we are already visiting the type declaration.
            if (!visitedTypeDeclarations.Add(typeDeclaration))
            {
                return;
            }

            // We only set a name for ourselves if we cannot be reduced.
            if (!typeDeclaration.CanReduce())
            {
                // Set the name for this type
                languageProvider.SetNamesAfterSubschema(typeDeclaration, visitedTypeDeclarations, cancellationToken);
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.OrderedSubschemaTypeDeclarations)
            {
                TypeDeclaration reducedChild = child.CanReduce() ? child.ReducedTypeDeclaration().ReducedType : child;
                SetNamesAfterSubschema(reducedChild, visitedTypeDeclarations, cancellationToken);
            }
        }
    }

    /// <summary>
    /// Gets the fully-reduced type declaration for the given location.
    /// </summary>
    /// <param name="currentLocation">The location for which to get the reduced type declaration.</param>
    /// <param name="reducedTypeDeclaration">The reduced type declaration.</param>
    /// <returns><see langword="true"/>if a type declaration could be found for the given location.</returns>
    private bool TryGetReducedTypeDeclarationFor(
        JsonReference currentLocation,
        [NotNullWhen(true)] out ReducedTypeDeclaration reducedTypeDeclaration)
    {
        if (this.locatedTypeDeclarations.TryGetValue(currentLocation, out TypeDeclaration? baseTypeDeclaration))
        {
            reducedTypeDeclaration = baseTypeDeclaration.ReducedTypeDeclaration();
            return true;
        }

        reducedTypeDeclaration = default;
        return false;
    }

    private void CollectProperties(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        // Collect properties for the type itself.
        if (PropertyProvider.CollectProperties(typeDeclaration, typeDeclaration, this.propertiesCollected, false, cancellationToken))
        {
            // Then each of its subschema type declarations.
            foreach (TypeDeclaration subType in typeDeclaration.SubschemaTypeDeclarations.Values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                this.CollectProperties(subType, cancellationToken);
            }
        }
    }

    private void SetParents(IHierarchicalLanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations, CancellationToken cancellationToken)
    {
        HashSet<TypeDeclaration> visitedTypes = [];
        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetParentsCore(type, visitedTypes, cancellationToken);
        }

        void SetParentsCore(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes, CancellationToken cancellationToken)
        {
            if (!visitedTypes.Add(type))
            {
                return;
            }

            SetParent(type);
            foreach (TypeDeclaration child in type.SubschemaTypeDeclarations.Values)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                SetParentsCore(child, visitedTypes, cancellationToken);
            }

            void SetParent(TypeDeclaration type)
            {
                if (!languageProvider.ShouldGenerate(type))
                {
                    languageProvider.SetParent(type, null);
                    return;
                }

                JsonReference currentLocation = type.LocatedSchema.Location;
                JsonReferenceBuilder builder = currentLocation.AsBuilder();
                if (builder.HasQuery)
                {
                    // We were created in a dynamic scope, so our parent will be that dynamic scope.
                    currentLocation = new JsonReference(Uri.UnescapeDataString(builder.Query[(builder.Query.IndexOf('=') + 1)..].ToString()));

                    if (this.TryGetReducedTypeDeclarationFor(currentLocation, out ReducedTypeDeclaration reducedTypeDeclaration))
                    {
                        if (reducedTypeDeclaration.ReducedType.Equals(type) || !languageProvider.ShouldGenerate(reducedTypeDeclaration.ReducedType))
                        {
                            languageProvider.SetParent(type, null);
                            return;
                        }

                        languageProvider.SetParent(type, reducedTypeDeclaration.ReducedType);
                        return;
                    }
                }

                while (true)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        return;
                    }

                    if (!currentLocation.HasFragment)
                    {
                        // We have reached the root of a dynamic scope, and not found anything, so that's that.
                        languageProvider.SetParent(type, null);
                        return;
                    }

                    currentLocation = StepBackOneFragment(currentLocation);

                    if (this.TryGetReducedTypeDeclarationFor(currentLocation, out ReducedTypeDeclaration reducedTypeDeclaration))
                    {
                        if (reducedTypeDeclaration.ReducedType.Equals(type) || !languageProvider.ShouldGenerate(reducedTypeDeclaration.ReducedType))
                        {
                            languageProvider.SetParent(type, null);
                            return;
                        }

                        languageProvider.SetParent(type, reducedTypeDeclaration.ReducedType);
                        return;
                    }
                }

                static JsonReference StepBackOneFragment(JsonReference reference)
                {
                    if (!reference.HasFragment)
                    {
                        return reference;
                    }

                    ReadOnlySpan<char> fragment = reference.Fragment;
                    int lastSlash = fragment.LastIndexOf('/');
                    if (lastSlash <= 0)
                    {
                        return reference.WithFragment(string.Empty);
                    }

                    if (fragment[lastSlash - 1] == '#')
                    {
                        lastSlash--;
                    }

                    if (lastSlash <= 0)
                    {
                        return reference.WithFragment(string.Empty);
                    }

                    fragment = fragment[..lastSlash];
                    return new JsonReference(reference.Uri, fragment);
                }
            }
        }
    }
}