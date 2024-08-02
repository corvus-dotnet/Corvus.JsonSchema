// <copyright file="JsonSchemaTypeBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
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
    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = [];
    private readonly JsonSchemaRegistry schemaRegistry = new(documentResolver, vocabularyRegistry);
    private readonly HashSet<TypeDeclaration> propertiesCollected = [];

    /// <summary>
    /// Walk a JSON document to build a JSON schema type declaration.
    /// </summary>
    /// <param name="documentPath">The path to the root of the json-schema document.</param>
    /// <param name="fallbackVocabulary">The default vocabulary to use if one cannot be analysed.</param>
    /// <param name="rebaseAsRoot">Whether to rebase the <paramref name="documentPath"/> as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langword="true"/>, then references in this document should be taken as if the fragment was the root of a document. This will effectively generate a custom $id for the root scope.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>This method may be called multiple times to build up a set of related types, perhaps from multiple fragments of a single document, or a family of related documents.</para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="GenerateCodeUsing(ILanguageProvider, TypeDeclaration[])"/> to generate the code for each language you need to support.</para>
    /// <para>Note: this requires the <see cref="IDocumentResolver"/> to be pre-configured with all the files required by the type build.
    /// If the <see cref="ValueTask{JsonElement}"/> returned by <see cref="IDocumentResolver.TryResolve(JsonReference)"/> is not
    /// completed immediately on return, it will throw an invalid operation exception.</para>
    /// </remarks>
    public TypeDeclaration AddTypeDeclarations(
        JsonReference documentPath,
        IVocabulary fallbackVocabulary,
        bool rebaseAsRoot = false)
    {
        ValueTask<TypeDeclaration> task =
            this.AddTypeDeclarationsAsync(documentPath, fallbackVocabulary, rebaseAsRoot);

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
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>This method may be called multiple times to build up a set of related types, perhaps from multiple fragments of a single document, or a family of related documents.</para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="GenerateCodeUsing(ILanguageProvider, TypeDeclaration[])"/> to generate the code for each language you need to support.</para>
    /// </remarks>
    public async ValueTask<TypeDeclaration> AddTypeDeclarationsAsync(JsonReference documentPath, IVocabulary fallbackVocabulary, bool rebaseAsRoot = false)
    {
        // First we do a document "load" - this enables us to build the map of the schema, anchors etc.
        (JsonReference schemaLocation, JsonReference baseLocation) = await this.schemaRegistry.RegisterBaseSchema(documentPath, fallbackVocabulary, rebaseAsRoot).ConfigureAwait(false);

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

        TypeDeclaration rootTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);

        this.CollectProperties(rootTypeDeclaration);

        return rootTypeDeclaration;
    }

    /// <summary>
    /// Generates code for the types using the given language provider.
    /// </summary>
    /// <param name="languageProvider">The <see cref="ILanguageProvider"/> for which to generate code.</param>
    /// <param name="rootTypeDeclarations">The root type declarations for which to generate types.</param>
    /// <returns>The <see cref="GeneratedCodeFile"/> collection.</returns>
    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeUsing(ILanguageProvider languageProvider, params TypeDeclaration[] rootTypeDeclarations)
    {
        IReadOnlyList<TypeDeclaration> candidateTypesToGenerate = GetCandidateTypesToGenerate(rootTypeDeclarations);

        MarkNonGeneratedTypes(languageProvider, rootTypeDeclarations);

        // A language provider can opt out of being hierarchical by not implementing
        // the IHierarchicalLanguageProvider interface. In that case, we don't need to set parents.
        if (languageProvider is IHierarchicalLanguageProvider hierarchicalProvider)
        {
            this.SetParents(hierarchicalProvider, rootTypeDeclarations);
        }

        SetNames(languageProvider, rootTypeDeclarations);

        IEnumerable<TypeDeclaration> typeDeclarations = candidateTypesToGenerate.Where(languageProvider.ShouldGenerate);

        return languageProvider.GenerateCodeFor(typeDeclarations);
    }

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
    /// <returns>A set of types that need to be built.</returns>
    /// <remarks>This will then be further filtered by the <see cref="ILanguageProvider"/> to eliminate built-in types.</remarks>
    private static IReadOnlyList<TypeDeclaration> GetCandidateTypesToGenerate(TypeDeclaration[] rootTypeDeclarations)
    {
        HashSet<TypeDeclaration> typesToGenerate = [];
        foreach (TypeDeclaration rootTypeDeclaration in rootTypeDeclarations)
        {
            GetTypesToGenerateCore(rootTypeDeclaration, typesToGenerate);
        }

        return [.. typesToGenerate.OrderBy(t => t.LocatedSchema.Location)];

        static void GetTypesToGenerateCore(TypeDeclaration type, HashSet<TypeDeclaration> typesToGenerate)
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
                    GetTypesToGenerateCore(child, typesToGenerate);
                }
            }
            else
            {
                ReducedTypeDeclaration reducedTypeDeclaration = type.ReducedTypeDeclaration();
                GetTypesToGenerateCore(reducedTypeDeclaration.ReducedType, typesToGenerate);
            }
        }
    }

    private static void MarkNonGeneratedTypes(ILanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations)
    {
        HashSet<TypeDeclaration> visitedTypes = [];

        // Deal with the well-known type declarations first.
        IdentifyNonGeneratedTypes(WellKnownTypeDeclarations.JsonAny, visitedTypes);
        IdentifyNonGeneratedTypes(WellKnownTypeDeclarations.JsonNotAny, visitedTypes);

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            IdentifyNonGeneratedTypes(type, visitedTypes);
        }

        void IdentifyNonGeneratedTypes(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations)
        {
            // Quit early if we are already visiting the type declaration.
            if (visitedTypeDeclarations.Contains(typeDeclaration))
            {
                return;
            }

            // Tell ourselves early that we have visited this type declaration already.
            visitedTypeDeclarations.Add(typeDeclaration);

            // We only set a name for ourselves if we cannot be reduced.
            if (!typeDeclaration.CanReduce())
            {
                // Set the name for this type
                languageProvider.IdentifyNonGeneratedType(typeDeclaration);
            }

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.SubschemaTypeDeclarations.Values.OrderBy(t => t.LocatedSchema.Location.ToString()))
            {
                IdentifyNonGeneratedTypes(child, visitedTypeDeclarations);
            }
        }
    }

    private static void SetNames(ILanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations)
    {
        HashSet<TypeDeclaration> visitedTypes = [];

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetNamesBeforeSubschema(type, visitedTypes);
        }

        visitedTypes.Clear();

        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetNamesAfterSubschema(type, visitedTypes);
        }

        void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations)
        {
            // Quit early if we are already visiting the type declaration.
            if (visitedTypeDeclarations.Contains(typeDeclaration))
            {
                return;
            }

            // Tell ourselves early that we have visited this type declaration already.
            visitedTypeDeclarations.Add(typeDeclaration);

            // We only set a name for ourselves if we cannot be reduced.
            if (typeDeclaration.CanReduce())
            {
                typeDeclaration = typeDeclaration.ReducedTypeDeclaration().ReducedType;
                SetNamesBeforeSubschema(typeDeclaration, visitedTypeDeclarations);
                return;
            }

            languageProvider.SetNamesBeforeSubschema(typeDeclaration, "Entity");

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.SubschemaTypeDeclarations.Values
                        .Select(s => s.ReducedTypeDeclaration().ReducedType)
                        .OrderBy(t => t.LocatedSchema.Location.ToString()))
            {
                SetNamesBeforeSubschema(child, visitedTypeDeclarations);
            }
        }

        void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visitedTypeDeclarations)
        {
            // Quit early if we are already visiting the type declaration.
            if (visitedTypeDeclarations.Contains(typeDeclaration))
            {
                return;
            }

            // Tell ourselves early that we have visited this type declaration already.
            visitedTypeDeclarations.Add(typeDeclaration);

            // We only set a name for ourselves if we cannot be reduced.
            if (!typeDeclaration.CanReduce())
            {
                // Set the name for this type
                languageProvider.SetNamesAfterSubschema(typeDeclaration);
            }

            // Then set the names for the subschema it requires.
            foreach (TypeDeclaration child in typeDeclaration.SubschemaTypeDeclarations.Values
                            .Select(s => s.ReducedTypeDeclaration().ReducedType)
                            .OrderBy(t => t.LocatedSchema.Location.ToString()))
            {
                SetNamesAfterSubschema(child, visitedTypeDeclarations);
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

    private void CollectProperties(TypeDeclaration typeDeclaration)
    {
        // Collect properties for the type itself.
        if (PropertyProvider.CollectProperties(typeDeclaration, typeDeclaration, this.propertiesCollected, false))
        {
            // Then each of its subschema type declarations.
            foreach (TypeDeclaration subType in typeDeclaration.SubschemaTypeDeclarations.Values)
            {
                this.CollectProperties(subType);
            }
        }
    }

    private void SetParents(IHierarchicalLanguageProvider languageProvider, TypeDeclaration[] rootTypeDeclarations)
    {
        HashSet<TypeDeclaration> visitedTypes = [];
        foreach (TypeDeclaration type in rootTypeDeclarations)
        {
            SetParentsCore(type, visitedTypes);
        }

        void SetParentsCore(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
        {
            if (visitedTypes.Contains(type))
            {
                return;
            }

            visitedTypes.Add(type);

            SetParent(type);
            foreach (TypeDeclaration child in type.SubschemaTypeDeclarations.Values)
            {
                SetParentsCore(child, visitedTypes);
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