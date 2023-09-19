// <copyright file="JsonSchemaRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Handles loading and parsing Json Schema files.
/// </summary>
internal class JsonSchemaRegistry
{
    private static readonly JsonReference DefaultAbsoluteLocation = new("https://endjin.com/");
    private readonly Dictionary<string, LocatedSchema> locatedSchema = new();
    private readonly IDocumentResolver documentResolver;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaTypeBuilder"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver to use.</param>
    /// <param name="jsonSchemaConfiguration">The JSON schema configuration.</param>
    public JsonSchemaRegistry(IDocumentResolver documentResolver, JsonSchemaConfiguration jsonSchemaConfiguration)
    {
        this.documentResolver = documentResolver;
        this.JsonSchemaConfiguration = jsonSchemaConfiguration;
    }

    /// <summary>
    /// Gets the JsonSchemaConfiguration for the registry.
    /// </summary>
    public JsonSchemaConfiguration JsonSchemaConfiguration { get; }

    /// <summary>
    /// Walk a JSON document and build a schema map.
    /// </summary>
    /// <param name="jsonSchemaPath">The path to the JSON schema root document.</param>
    /// <param name="rebaseAsRoot">Whether to rebase this path as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langoword="true"/>, then references in this document should be taken as if the fragment was the root of a document.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the base URI for the document.</returns>
    /// <remarks><paramref name="jsonSchemaPath"/> must point to a root scope. If it has a pointer into the document, then <paramref name="rebaseAsRoot"/> must be true.</remarks>
    public async Task<JsonReference> RegisterDocumentSchema(JsonReference jsonSchemaPath, bool rebaseAsRoot = false)
    {
        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(jsonSchemaPath, out string? result))
        {
            jsonSchemaPath = new(result);
        }

        JsonReference basePath = jsonSchemaPath.WithFragment(string.Empty);

        if (basePath.Uri.StartsWith(DefaultAbsoluteLocation.Uri))
        {
            basePath = new JsonReference(jsonSchemaPath.Uri[DefaultAbsoluteLocation.Uri.Length..], ReadOnlySpan<char>.Empty);
        }

        JsonElement? optionalDocumentRoot = await this.documentResolver.TryResolve(basePath).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to locate the root document at '{basePath}'");
        JsonElement documentRoot = optionalDocumentRoot.Value;

        if (jsonSchemaPath.HasFragment)
        {
            if (rebaseAsRoot)
            {
                // Switch the root to be an absolute URI
                JsonElement newBase = JsonPointerUtilities.ResolvePointer(documentRoot, jsonSchemaPath.Fragment);
                jsonSchemaPath = DefaultAbsoluteLocation.Apply(new JsonReference($"{Guid.NewGuid()}/Schema"));

                // And add the document back to the document resolver against that root URI
                return await AddSchemaForUpdatedPathAndElement(jsonSchemaPath, newBase).ConfigureAwait(false);
            }
            else
            {
                // This is not a root path, so we need to construct a JSON document that references the root path instead.
                // This will not actually be constructed, as it will be resolved to the reference type instead.
                // It allows us to indirect through this reference as if it were a "root" type.
                var referenceSchema = JsonObject.FromProperties(("$ref", (string)jsonSchemaPath));
                jsonSchemaPath = DefaultAbsoluteLocation.Apply(new JsonReference($"{Guid.NewGuid()}/Schema"));
                return await AddSchemaForUpdatedPathAndElement(jsonSchemaPath, referenceSchema.AsJsonElement).ConfigureAwait(false);
            }
        }

        var baseSchema = JsonAny.FromJson(documentRoot);

        if (!this.JsonSchemaConfiguration.ValidateSchema(baseSchema))
        {
            // This is not a valid schema overall, so this must be an island in the schema
            basePath = jsonSchemaPath;
            if (await this.documentResolver.TryResolve(basePath).ConfigureAwait(false) is JsonElement island)
            {
                baseSchema = JsonAny.FromJson(island);
                if (!this.JsonSchemaConfiguration.ValidateSchema(baseSchema))
                {
                    throw new InvalidOperationException($"Expected to find a valid schema island at {jsonSchemaPath}");
                }
            }
            else
            {
                throw new InvalidOperationException($"Unable to resolve a JSON at {jsonSchemaPath}");
            }
        }

        return this.AddSchemaAndSubschema(basePath, baseSchema);

        async Task<JsonReference> AddSchemaForUpdatedPathAndElement(JsonReference jsonSchemaPath, JsonElement newBase)
        {
            this.documentResolver.AddDocument(jsonSchemaPath, GetDocumentFrom(newBase));
            JsonElement? resolvedBase = await this.documentResolver.TryResolve(jsonSchemaPath).ConfigureAwait(false) ?? throw new InvalidOperationException($"Expected to find a rebased schema at {jsonSchemaPath}");
            var rebasedSchema = JsonAny.FromJson(resolvedBase.Value);
            if (!this.JsonSchemaConfiguration.ValidateSchema(rebasedSchema))
            {
                throw new InvalidOperationException($"The document at '{jsonSchemaPath}' is not a valid schema.");
            }

            return this.AddSchemaAndSubschema(jsonSchemaPath, rebasedSchema);
        }
    }

    /// <summary>
    /// Adds the subschema to the registry at the given location, and walks the schema, adding its
    /// subschema relative to that location.
    /// </summary>
    /// <param name="currentLocation">The location at which to add the schema.</param>
    /// <param name="schema">The schema to add.</param>
    /// <returns>A reference to the located schema.</returns>
    /// <exception cref="InvalidOperationException">The schema could not be registered.</exception>
    public JsonReference AddSchemaAndSubschema(JsonReference currentLocation, JsonAny schema)
    {
        // First, add the element at the current location.
        bool leavingEarly = false;

        currentLocation = MakeAbsolute(currentLocation);

        if (!this.AddLocatedSchema(currentLocation, schema))
        {
            // We've already registered this schema, so we are going to leave early.
            // But we have to resolve the currentLocation for the ID (if present and applicable)
            leavingEarly = true;
        }

        if (schema.ValueKind != JsonValueKind.Object)
        {
            // We are a boolean schema, so we can just leave.
            return currentLocation;
        }

        JsonObject schemaObject = schema.AsObject;

        if (!RefMatters(schemaObject) && schemaObject.TryGetProperty(this.JsonSchemaConfiguration.IdKeyword, out JsonAny id))
        {
            JsonReference previousLocation = currentLocation;
            currentLocation = currentLocation.Apply(new JsonReference((string)id.AsString));

            // We skip adding if we are leaving early
            if (!leavingEarly)
            {
                if (currentLocation.HasFragment)
                {
                    if (!this.locatedSchema.TryGetValue(previousLocation, out LocatedSchema? previousSchema))
                    {
                        throw new InvalidOperationException($"The previously registered schema for '{previousLocation}' was not found.");
                    }

                    this.locatedSchema.TryAdd(currentLocation, previousSchema);

                    // Update the location to reflect the ID
                    ////previousSchema.Location = currentLocation;

                    this.AddNamedAnchor(currentLocation, currentLocation.Fragment[1..].ToString());
                }
                else
                {
                    if (!this.locatedSchema.TryGetValue(previousLocation, out LocatedSchema? previousSchema))
                    {
                        throw new InvalidOperationException($"The previously registered schema for '{previousLocation}' was not found.");
                    }

                    this.locatedSchema.TryAdd(currentLocation, previousSchema);
                }
            }
        }

        // Having (possibly) updated the current location for the ID we can now leave.
        if (leavingEarly)
        {
            return currentLocation;
        }

        AddAnchors(currentLocation, schemaObject);
        AddSubschemas(currentLocation, schemaObject);

        return currentLocation;

        bool RefMatters(JsonObject schema)
        {
            return (this.JsonSchemaConfiguration.ValidatingAs & ValidationSemantics.Pre201909) != 0 && schema.HasProperty(this.JsonSchemaConfiguration.RefKeyword);
        }

        void AddAnchors(JsonReference currentLocation, JsonObject schema)
        {
            foreach (AnchorKeyword anchorKeyword in this.JsonSchemaConfiguration.AnchorKeywords)
            {
                if (schema.AsObject.TryGetProperty(anchorKeyword.Name, out JsonAny value))
                {
                    if (value.ValueKind == JsonValueKind.String)
                    {
                        string valueString = (string)value.AsString;
                        this.AddNamedAnchor(currentLocation, valueString);
                        if (anchorKeyword.IsDynamic)
                        {
                            this.AddDynamicAnchor(currentLocation, valueString);
                        }
                    }

                    if (anchorKeyword.IsRecursive && value.ValueKind == JsonValueKind.True)
                    {
                        this.SetRecursiveAnchor(currentLocation);
                    }
                }
            }
        }

        void AddSubschemas(JsonReference currentLocation, JsonObject schema)
        {
            foreach (RefResolvableKeyword keyword in this.JsonSchemaConfiguration.RefResolvableKeywords)
            {
                if (schema.TryGetProperty(keyword.Name, out JsonAny value))
                {
                    AddSubschemasForKeyword(keyword, value, currentLocation);
                }
            }
        }

        void AddSubschemasForKeyword(RefResolvableKeyword keyword, JsonAny value, JsonReference currentLocation)
        {
            switch (keyword.RefResolvablePropertyKind)
            {
                case RefResolvablePropertyKind.Schema:
                    AddSubschemasForSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.ArrayOfSchema:
                    AddSubschemasForArrayOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.SchemaOrArrayOfSchema:
                    AddSubschemasForSchemaOrArrayOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.MapOfSchema:
                    AddSubschemasForMapOfSchemaProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.MapOfSchemaIfValueIsSchemaLike:
                    AddSubschemasForMapOfSchemaIfValueIsSchemaLikeProperty(keyword.Name, value, currentLocation);
                    break;
                case RefResolvablePropertyKind.SchemaIfValueIsSchemaLike:
                    AddSubschemasForSchemaIfValueIsASchemaLikeKindProperty(keyword.Name, value, currentLocation);
                    break;
                default:
                    throw new InvalidOperationException($"Unrecognized property ref resolvable property kind: {Enum.GetName(keyword.RefResolvablePropertyKind)}");
            }
        }

        void AddSubschemasForSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

            if (!this.JsonSchemaConfiguration.ValidateSchema(value))
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a schema object.");
            }

            this.AddSchemaAndSubschema(propertyLocation, value);
        }

        void AddSubschemasForArrayOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be an array of schema objects.");
            }

            int index = 0;
            foreach (JsonAny subschema in value.AsArray.EnumerateArray())
            {
                JsonReference subschemaLocation = propertyLocation.AppendArrayIndexToFragment(index);
                this.AddSchemaAndSubschema(subschemaLocation, subschema);
                ++index;
            }
        }

        void AddSubschemasForSchemaOrArrayOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            if (value.ValueKind == JsonValueKind.Object || value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False)
            {
                AddSubschemasForSchemaProperty(propertyName, value, currentLocation);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                AddSubschemasForArrayOfSchemaProperty(propertyName, value.AsArray, currentLocation);
            }
            else
            {
                JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be either a schema object, or an array of schema objects.");
            }
        }

        void AddSubschemasForMapOfSchemaIfValueIsSchemaLikeProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a map of schema objects.");
            }

            int index = 0;
            foreach (JsonObjectProperty property in value.AsObject.EnumerateObject())
            {
                if (property.ValueKind == JsonValueKind.Object || property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
                {
                    JsonReference subschemaLocation = propertyLocation.AppendUnencodedPropertyNameToFragment(property.Name.GetString());
                    this.AddSchemaAndSubschema(subschemaLocation, property.Value);
                    ++index;
                }
            }
        }

        void AddSubschemasForMapOfSchemaProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);
            if (value.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"The property at {propertyLocation} was expected to be a map of schema objects.");
            }

            int index = 0;
            foreach (JsonObjectProperty property in value.AsObject.EnumerateObject())
            {
                JsonReference subschemaLocation = propertyLocation.AppendUnencodedPropertyNameToFragment(property.Name.GetString());
                this.AddSchemaAndSubschema(subschemaLocation, property.Value);
                ++index;
            }
        }

        void AddSubschemasForSchemaIfValueIsASchemaLikeKindProperty(string propertyName, JsonAny value, JsonReference currentLocation)
        {
            JsonReference propertyLocation = currentLocation.AppendUnencodedPropertyNameToFragment(propertyName);

            if (value.ValueKind != JsonValueKind.Object && value.ValueKind != JsonValueKind.False && value.ValueKind != JsonValueKind.True)
            {
                // If we are not an object, that's OK - we just ignore it in this case.
                return;
            }

            this.AddSchemaAndSubschema(propertyLocation, value);
        }
    }

    /// <summary>
    /// Add the located schema at the given location.
    /// </summary>
    /// <param name="location">The location at which to add the located schema.</param>
    /// <param name="locatedSchema">The located schema to add.</param>
    public void Add(JsonReference location, LocatedSchema locatedSchema)
    {
        this.locatedSchema.Add(location, locatedSchema);
    }

    /// <summary>
    /// Gets the located schema for the given location.
    /// </summary>
    /// <param name="location">The location for which to retrieve the schema.</param>
    /// <returns>The <see cref="LocatedSchema"/> for the given scope.</returns>
    /// <exception cref="KeyNotFoundException">No schema was registered at the given location.</exception>
    public LocatedSchema GetLocatedSchema(JsonReference location)
    {
        return this.locatedSchema[location];
    }

    /// <summary>
    /// Tries to get the located schema for the given scope.
    /// </summary>
    /// <param name="location">The Location for which to find the schema.</param>
    /// <param name="schema">The schema found at the locatio.</param>
    /// <returns><see langword="true"/> when the schema is found.</returns>
    public bool TryGetValue(JsonReference location, [NotNullWhen(true)] out LocatedSchema? schema)
    {
        return this.locatedSchema.TryGetValue(location, out schema);
    }

    private static JsonDocument GetDocumentFrom(JsonElement documentRoot)
    {
        ArrayBufferWriter<byte> abw = new();
        using Utf8JsonWriter writer = new(abw);
        documentRoot.WriteTo(writer);
        writer.Flush();
        return JsonDocument.Parse(abw.WrittenMemory);
    }

    private static JsonReference MakeAbsolute(JsonReference location)
    {
        if (location.HasAbsoluteUri)
        {
            return location;
        }

        return DefaultAbsoluteLocation.Apply(location);
    }

    private bool AddLocatedSchema(JsonReference location, JsonAny schema)
    {
        location = MakeAbsolute(location);
        return this.locatedSchema.TryAdd(location, new(location, schema));
    }

    private void AddNamedAnchor(JsonReference location, string anchorName)
    {
        // Go back up to root
        JsonReference rootLocation = MakeAbsolute(location.WithFragment(string.Empty));
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(rootLocation, out LocatedSchema? rootSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for '{rootLocation}' while trying to add anchors for '{location}'");
        }

        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        rootSchema.TryAddAnchor(anchorName, anchoredSchema);
    }

    private void AddDynamicAnchor(JsonReference location, string anchorName)
    {
        // Go back up to root
        JsonReference rootLocation = MakeAbsolute(location.WithFragment(string.Empty));
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(rootLocation, out LocatedSchema? rootSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for '{rootLocation}' while trying to add anchors for '{location}'");
        }

        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        rootSchema.AddOrUpdateDynamicAnchor(anchorName, anchoredSchema);
    }

    private void SetRecursiveAnchor(JsonReference location)
    {
        location = MakeAbsolute(location);
        if (!this.locatedSchema.TryGetValue(location, out LocatedSchema? anchoredSchema))
        {
            throw new InvalidOperationException($"No schema has been registered for anchor at '{location}");
        }

        anchoredSchema.IsRecursiveAnchor = true;
    }
}