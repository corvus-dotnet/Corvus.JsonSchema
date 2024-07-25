// <copyright file="JsonSchemaRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Loads and manages schema with Vocabulary support.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="JsonSchemaRegistry"/> class.
/// </remarks>
/// <param name="documentResolver">The document resolver used to provide input documents.</param>
/// <param name="vocabularyRegistry">The vocabulary registry used to provide vocabularies.</param>
public class JsonSchemaRegistry(IDocumentResolver documentResolver, VocabularyRegistry vocabularyRegistry)
{
    private static readonly JsonReference DefaultAbsoluteLocation = new("https://endjin.com");
    private readonly Dictionary<string, LocatedSchema> locatedSchema = [];

    /// <summary>
    /// Walk a JSON document and build a schema map.
    /// </summary>
    /// <param name="jsonSchemaPath">The path to the JSON schema root document.</param>
    /// <param name="ambientVocabulary">The ambient vocabulary to use if the document's vocabulary cannot be analysed.</param>
    /// <param name="rebaseAsRoot">Whether to rebase this path as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langoword="true"/>, then references in this document should be taken as if the fragment was the root of a document.</param>
    /// <returns>A <see cref="ValueTask"/> which, when complete, provides the root scope for the document (which may be a generated $ref for a path with a fragment), and the base reference to the document containing the root element.</returns>
    /// <remarks><paramref name="jsonSchemaPath"/> must point to a root scope. If it has a pointer into the document, then <paramref name="rebaseAsRoot"/> must be true.</remarks>
    public async ValueTask<(JsonReference RootUri, JsonReference BaseReference)> RegisterBaseSchema(JsonReference jsonSchemaPath, IVocabulary ambientVocabulary, bool rebaseAsRoot = false)
    {
        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(jsonSchemaPath, out string? result))
        {
            jsonSchemaPath = new(result);
        }

        JsonReference basePath = jsonSchemaPath.WithFragment(string.Empty);

        if (basePath.Uri.StartsWith(DefaultAbsoluteLocation.Uri))
        {
            basePath = new JsonReference(jsonSchemaPath.Uri[DefaultAbsoluteLocation.Uri.Length..], []);
        }

        // Load the document
        JsonElement? optionalDocumentRoot = await documentResolver.TryResolve(basePath).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to locate the root document at '{basePath}'");
        if (optionalDocumentRoot is not JsonElement documentRoot)
        {
            throw new InvalidOperationException($"Unable to resolve the document at {basePath}");
        }

        if (jsonSchemaPath.HasFragment)
        {
            return await HandleEmbeddedBaseSchema(vocabularyRegistry, jsonSchemaPath, ambientVocabulary, rebaseAsRoot, basePath, documentRoot).ConfigureAwait(false);
        }

        IVocabulary vocabulary = await vocabularyRegistry.AnalyseSchema(documentRoot).ConfigureAwait(false) ?? ambientVocabulary;
        if (!vocabulary.ValidateSchemaInstance(documentRoot))
        {
            // This is not a valid schema overall, so this must be an island in the schema
            basePath = jsonSchemaPath;
            if (await documentResolver.TryResolve(basePath).ConfigureAwait(false) is JsonElement island)
            {
                // We've loaded a new document, so we need to see if we need to override the vocabulary.
                vocabulary = await vocabularyRegistry.AnalyseSchema(island).ConfigureAwait(false) ?? vocabulary;
                documentRoot = island;

                if (!vocabulary.ValidateSchemaInstance(documentRoot))
                {
                    throw new InvalidOperationException($"Expected to find a valid schema island at {jsonSchemaPath}, according to the vocabulary {vocabulary.Uri}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unable to resolve a JSON at {jsonSchemaPath}");
            }
        }

        return (this.AddSchemaAndSubschema(basePath, documentRoot, vocabulary), basePath);

        async ValueTask<JsonReference> AddSchemaForUpdatedPathAndElement(JsonReference jsonSchemaPath, JsonElement newBase, IVocabulary vocabulary)
        {
            return await AddSchemaForUpdatedPathAndDocument(jsonSchemaPath, GetDocumentFrom(newBase), vocabulary).ConfigureAwait(false);
        }

        async ValueTask<JsonReference> AddSchemaForUpdatedPathAndDocument(JsonReference jsonSchemaPath, JsonDocument newBase, IVocabulary vocabulary)
        {
            documentResolver.AddDocument(jsonSchemaPath, newBase);
            JsonElement? resolvedBaseOptional = await documentResolver.TryResolve(jsonSchemaPath).ConfigureAwait(false) ?? throw new InvalidOperationException($"Expected to find a rebased schema at {jsonSchemaPath}");
            if (resolvedBaseOptional is not JsonElement resolvedBase)
            {
                throw new InvalidOperationException($"Unable to find the JSON schema at '{jsonSchemaPath}'.");
            }

            if (!vocabulary.ValidateSchemaInstance(resolvedBase))
            {
                throw new InvalidOperationException($"The JSON schema at '{jsonSchemaPath}' was not valid, according to the vocabulary {vocabulary.Uri}.");
            }

            return this.AddSchemaAndSubschema(jsonSchemaPath, resolvedBase, vocabulary);
        }

        static JsonDocument GetDocumentFrom(JsonElement documentRoot)
        {
            return JsonDocument.Parse(documentRoot.GetRawText());
        }

        async ValueTask<(JsonReference RootUri, JsonReference BaseReference)> HandleEmbeddedBaseSchema(VocabularyRegistry vocabularyRegistry, JsonReference jsonSchemaPath, IVocabulary ambientVocabulary, bool rebaseAsRoot, JsonReference basePath, JsonElement documentRoot)
        {
            JsonElement newBase = JsonPointerUtilities.ResolvePointer(documentRoot, jsonSchemaPath.Fragment);
            IVocabulary referencedVocab = await vocabularyRegistry.AnalyseSchema(newBase).ConfigureAwait(false) ?? ambientVocabulary;

            if (rebaseAsRoot)
            {
                // Switch the root to be an absolute URI
                jsonSchemaPath = DefaultAbsoluteLocation.Apply(new JsonReference($"{Guid.NewGuid()}/Schema"));

                // And add the document back to the document resolver against that root URI
                JsonReference docref = await AddSchemaForUpdatedPathAndElement(jsonSchemaPath, newBase, referencedVocab).ConfigureAwait(false);
                return (docref, basePath);
            }
            else
            {
                // This is not a root path, so we need to construct a JSON document that references the root path instead.
                // This will not actually be constructed, as it will be resolved to the reference type instead.
                // It allows us to indirect through this reference as if it were a "root" type.
                JsonDocument referenceSchema = referencedVocab.BuildReferenceSchemaInstance(jsonSchemaPath)
                    ?? throw new InvalidOperationException("The vocabulary does not support referencing");
                jsonSchemaPath = DefaultAbsoluteLocation.Apply(new JsonReference($"{Guid.NewGuid()}/Schema"));
                JsonReference docref = await AddSchemaForUpdatedPathAndDocument(jsonSchemaPath, referenceSchema, referencedVocab).ConfigureAwait(false);
                return (docref, basePath);
            }
        }
    }

    /// <summary>
    /// Adds the subschema to the registry at the given location, and walks the schema, adding its
    /// subschema relative to that location.
    /// </summary>
    /// <param name="currentLocation">The location at which to add the schema.</param>
    /// <param name="schema">The schema to add.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    /// <returns>A reference to the located schema.</returns>
    /// <exception cref="InvalidOperationException">The schema could not be registered.</exception>
    public JsonReference AddSchemaAndSubschema(JsonReference currentLocation, JsonElement schema, IVocabulary vocabulary)
    {
        // First, add the element at the current location.
        bool leavingEarlyBecauseTheLocatedSchemaHasAlreadyBeenRegistered = false;

        currentLocation = MakeAbsolute(currentLocation);

        if (!this.TryAddLocatedSchema(currentLocation, schema, vocabulary))
        {
            // We've already registered this schema, so we are going to leave early.
            // But we have to resolve a change of scope, so we will continue to do that.
            leavingEarlyBecauseTheLocatedSchemaHasAlreadyBeenRegistered = true;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            // We are a boolean schema, so we can just leave.
            return currentLocation;
        }

        CustomKeywords.ApplyBeforeScope(this, schema, currentLocation, vocabulary);

        if (Scope.ShouldEnterScope(schema, vocabulary, out string? scopeName))
        {
            if (leavingEarlyBecauseTheLocatedSchemaHasAlreadyBeenRegistered)
            {
                currentLocation = currentLocation.Apply(new JsonReference(scopeName));
            }
            else
            {
                CustomKeywords.ApplyBeforeEnteringScope(this, schema, currentLocation, vocabulary);
                currentLocation = this.EnterScope(currentLocation, scopeName);
                CustomKeywords.ApplyAfterEnteringScope(this, schema, currentLocation, vocabulary);
            }
        }

        if (leavingEarlyBecauseTheLocatedSchemaHasAlreadyBeenRegistered)
        {
            // Having (possibly) updated the current location based on a change of scope, we can now leave.
            return currentLocation;
        }

        CustomKeywords.ApplyBeforeAnchors(this, schema, currentLocation, vocabulary);

        Anchors.AddAnchors(this, schema, currentLocation, vocabulary);

        CustomKeywords.ApplyBeforeSubschemas(this, schema, currentLocation, vocabulary);

        Subschemas.RegisterLocalSubschemas(this, schema, currentLocation, vocabulary);

        CustomKeywords.ApplyAfterSubschemas(this, schema, currentLocation, vocabulary);

        return currentLocation;
    }

    /// <summary>
    /// Tries to get the located schema for the given scope.
    /// </summary>
    /// <param name="location">The Location for which to find the schema.</param>
    /// <param name="schema">The schema found at the location.</param>
    /// <returns><see langword="true"/> when the schema is found.</returns>
    public bool TryGetLocatedSchema(JsonReference location, [NotNullWhen(true)] out LocatedSchema? schema)
    {
        return this.locatedSchema.TryGetValue(location, out schema);
    }

    /// <summary>
    /// Resolves a base reference, registering any newly discovered schema if necessary.
    /// </summary>
    /// <param name="baseSchemaForReferenceLocation">The base schema location.</param>
    /// <param name="vocabulary">The ambient vocabulary.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> which, when complete, provides the located schema
    /// or <see langword="null"/> if no schema could be located.</returns>
    public async ValueTask<LocatedSchema?> ResolveBaseReference(JsonReference baseSchemaForReferenceLocation, IVocabulary vocabulary)
    {
        if (!this.TryGetLocatedSchema(baseSchemaForReferenceLocation, out LocatedSchema? baseReferenceSchema))
        {
            (JsonReference registeredSchemaReference, _) = await this.RegisterBaseSchema(baseSchemaForReferenceLocation, vocabulary).ConfigureAwait(false);
            if (!this.TryGetLocatedSchema(registeredSchemaReference, out baseReferenceSchema))
            {
                return null;
            }
        }

        return baseReferenceSchema;
    }

    /// <summary>
    /// Add a located schema to the registry.
    /// </summary>
    /// <param name="location">The location at which to add the schema.</param>
    /// <param name="schema">The schema to add.</param>
    /// <param name="vocabulary">The vocabulary for the schema.</param>
    /// <returns><see langword="true"/> if the schema was added.</returns>
    public bool TryAddLocatedSchema(JsonReference location, JsonElement schema, IVocabulary vocabulary)
    {
        location = MakeAbsolute(location);
        return this.TryAddLocatedSchema(location, new(location, schema, vocabulary));
    }

    /// <summary>
    /// Add a located schema to the registry.
    /// </summary>
    /// <param name="location">The location at which to add the schema.</param>
    /// <param name="schema">The located schema to add.</param>
    /// <returns><see langword="true"/> if the schema was added.</returns>
    /// <remarks>Note that the location may not be the same as the location of the <see cref="LocatedSchema"/> if this is
    /// being added based on an anchor or similar.</remarks>
    public bool TryAddLocatedSchema(JsonReference location, LocatedSchema schema)
    {
#if NET8_0_OR_GREATER
        return this.locatedSchema.TryAdd(location, schema);
#else
        string l = location;
        if (this.locatedSchema.ContainsKey(l))
        {
            return false;
        }

        this.locatedSchema.Add(l, schema);
        return true;
#endif
    }

    /// <summary>
    /// Try to get the subschema and its base schema for a given location.
    /// </summary>
    /// <param name="location">The location for which to get the schema.</param>
    /// <param name="baseSchema">The base schema.</param>
    /// <param name="subschema">The subschema.</param>
    /// <returns><see langword="true"/> if the schema and its base schema could be found.</returns>
    public bool TryGetSchemaAndBaseForLocation(JsonReference location, [NotNullWhen(true)] out LocatedSchema? baseSchema, [NotNullWhen(true)] out LocatedSchema? subschema)
    {
        // Go back up to root
        JsonReference baseLocation = MakeAbsolute(location.WithFragment(string.Empty));
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(baseLocation, out baseSchema))
        {
            subschema = null;
            return false;
        }

        return this.locatedSchema.TryGetValue(location, out subschema);
    }

    /// <summary>
    /// Try to resolve the reference to a base.
    /// </summary>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="currentSchema">The current schema.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> which completes once the base is resolved.</returns>
    public async ValueTask<(JsonReference BaseSchemaForReferenceLocation, LocatedSchema BaseSchemaForReference)> ResolveBaseReference(JsonReference currentLocation, LocatedSchema currentSchema, JsonReference reference)
    {
        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base schema against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation, currentSchema.Vocabulary).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = currentLocation.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation, currentSchema.Vocabulary).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = currentLocation;
            baseSchemaForReference = currentSchema;
        }

        return new(baseSchemaForReferenceLocation, baseSchemaForReference);
    }

    /// <summary>
    /// Enter a new scope.
    /// </summary>
    /// <param name="previousLocation">The previous scope location.</param>
    /// <param name="scopeName">The new scope name.</param>
    /// <exception cref="InvalidOperationException">No schema was found for the previous location.</exception>
    /// <returns>The new scope location.</returns>
    public JsonReference EnterScope(JsonReference previousLocation, string scopeName)
    {
        if (!this.TryGetLocatedSchema(previousLocation, out LocatedSchema? previousSchema))
        {
            throw new InvalidOperationException($"The previously registered schema for '{previousLocation}' was not found.");
        }

        JsonReference currentLocation = previousLocation.Apply(new JsonReference(scopeName));

        if (currentLocation.HasFragment)
        {
            this.TryAddLocatedSchema(currentLocation, previousSchema);
            string anchorName = currentLocation.Fragment[1..].ToString();
            if (this.TryGetSchemaAndBaseForLocation(
                currentLocation,
                out LocatedSchema? baseSchema,
                out LocatedSchema? anchoredSchema))
            {
                // We add this scope as a named anchor for our base schema.
                baseSchema.AddOrUpdateLocatedAnchor(new NamedLocatedAnchor(anchorName, anchoredSchema));
            }
        }
        else
        {
            this.TryAddLocatedSchema(currentLocation, previousSchema);
        }

        return currentLocation;
    }

    private static JsonReference MakeAbsolute(JsonReference location)
    {
        if (location.HasAbsoluteUri)
        {
            return location;
        }

        return DefaultAbsoluteLocation.Apply(location);
    }
}