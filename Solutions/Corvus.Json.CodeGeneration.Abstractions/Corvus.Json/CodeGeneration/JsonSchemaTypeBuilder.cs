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
public partial class JsonSchemaTypeBuilder
{
    private static readonly JsonReference BuiltInsLocation = new("https://github.com/Corvus-dotnet/Corvus/tree/master/schema/builtins");

    private readonly Dictionary<string, TypeDeclaration> locatedTypeDeclarations = new();
    private readonly JsonSchemaRegistry schemaRegistry;
    private readonly IDocumentResolver documentResolver;
    private readonly IPropertyBuilder propertyBuilder;

    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaTypeBuilder"/> class.
    /// </summary>
    /// <param name="documentResolver">The document resolver to use.</param>
    public JsonSchemaTypeBuilder(IDocumentResolver documentResolver)
    {
        this.schemaRegistry = new(documentResolver, this.JsonSchemaConfiguration);
        this.documentResolver = documentResolver;
        this.propertyBuilder = new PropertyBuilder(this);
    }

    /// <summary>
    /// Gets the JsonSchemaConfiguration.
    /// </summary>
    public JsonSchemaConfiguration JsonSchemaConfiguration { get; } = new JsonSchemaConfiguration();

    /// <summary>
    /// Walk a JSON document to build a JSON schema type declaration.
    /// </summary>
    /// <param name="documentPath">The path to the root of the json-schema document.</param>
    /// <param name="rootNamespace">The root namespace in which to generate types.</param>
    /// <param name="rebaseAsRoot">Whether to rebase the <paramref name="documentPath"/> as a root document. This should only be done for a JSON schema island in a larger non-schema document.
    /// If <see langword="true"/>, then references in this document should be taken as if the fragment was the root of a document. This will effectively generate a custom $id for the root scope.</param>
    /// <param name="baseUriToNamespaceMap">An optional map of base URIs in the document to namespaces in which to generate the types.</param>
    /// <param name="rootTypeName">An optional explicit type name for the root element.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the requested type declaration.</returns>
    /// <remarks>
    /// <para>This method may be called multiple times to build up a set of related types, perhaps from multiple fragments of a single document, or a family of related documents.</para>
    /// <para>Any re-used schema will (if possible) be reduced to the same type, to build a single coherent type system.</para>
    /// <para>Once you have finished adding types, call <see cref="TypeDeclaration.GetTypesToGenerate()"/> to retrieve the set of types that need to be built for each root type you wish to build.</para>
    /// </remarks>
    public async Task<TypeDeclaration?> AddTypeDeclarationsFor(JsonReference documentPath, string rootNamespace, bool rebaseAsRoot = false, ImmutableDictionary<string, string>? baseUriToNamespaceMap = null, string? rootTypeName = null)
    {
        // First we do a document "load" - this enables us to build the map of the schema, anchors etc.
        JsonReference scope = await this.schemaRegistry.RegisterDocumentSchema(documentPath, rebaseAsRoot).ConfigureAwait(false);

        // Then we do a second "contextual" pass over the loaded schema from the root location. This enables
        // us to build correct dynamic references.
        TypeDeclaration rootTypeDeclaration = await this.BuildTypeDeclarationFor(this.schemaRegistry.GetLocatedSchema(scope)).ConfigureAwait(false);

        // Then we reduce the reducible chains of references to the final target type.
        rootTypeDeclaration = this.ReduceTypeDeclarations(rootTypeDeclaration);

        // Set up the built in types before we set the parent type names.
        this.SetBuiltInTypeNamesAndNamespaces(rootTypeDeclaration);

        // Then, set the parent type names.
        SetParents(rootTypeDeclaration);

        // Finally, we figure out which are built in types, and which need new types and namespaces for the custom types.
        this.SetTypeNamesAndNamespaces(rootTypeDeclaration, rootNamespace, baseUriToNamespaceMap, rootTypeName);

        // Next, we walk the tree finding and building properties from the properties themselves, and any conditionally applied subschema.
        this.FindAndBuildPropertiesCore(rootTypeDeclaration);

        return rootTypeDeclaration;
    }

    /// <summary>
    /// Gets the validation semantics for the document at the given location.
    /// </summary>
    /// <param name="reference">The reference to the document.</param>
    /// <param name="rebaseToRootPath">Whether we are rebasing the element to a root path.</param>
    /// <returns>A <see cref="Task{ValidationSemantics}"/> that, when complete, provides the validation semantics for the reference.</returns>
    public async Task<ValidationSemantics> GetValidationSemantics(JsonReference reference, bool rebaseToRootPath)
    {
        if (!rebaseToRootPath)
        {
            reference = reference.WithFragment(string.Empty);
        }

        JsonElement? element = await this.documentResolver.TryResolve(reference).ConfigureAwait(false);

        if (element is JsonElement e)
        {
            if (e.TryGetProperty(this.JsonSchemaConfiguration.SchemaKeyword, out JsonElement value) && value.ValueKind == JsonValueKind.String)
            {
                string? schemaValue = value.GetString();
                if (schemaValue is string sv)
                {
                    if (sv == "http://json-schema.org/draft-06/schema" || sv == "http://json-schema.org/draft-06/schema#")
                    {
                        return ValidationSemantics.Draft6;
                    }

                    if (sv == "http://json-schema.org/draft-07/schema" || sv == "http://json-schema.org/draft-07/schema#")
                    {
                        return ValidationSemantics.Draft7;
                    }

                    if (sv == "https://json-schema.org/draft/2019-09/schema")
                    {
                        return ValidationSemantics.Draft201909;
                    }

                    if (sv == "https://json-schema.org/draft/2020-12/schema")
                    {
                        return ValidationSemantics.Draft202012;
                    }
                }
            }
        }

        return ValidationSemantics.Unknown;
    }

    /// <summary>
    /// Adds a virtual document to the document resolver.
    /// </summary>
    /// <param name="path">The virtual path.</param>
    /// <param name="jsonDocument">The document to add.</param>
    public void AddDocument(string path, JsonDocument jsonDocument)
    {
        this.documentResolver.AddDocument(path, jsonDocument);
    }

    /// <summary>
    /// Replaces a located type declaration.
    /// </summary>
    /// <param name="location">The location for the replacement.</param>
    /// <param name="type">The type to replace.</param>
    /// <remarks>
    /// This is used by the <see cref="WalkContext"/> to replace the dynamic types when the scope is popped.
    /// </remarks>
    internal void ReplaceLocatedTypeDeclaration(JsonReference location, TypeDeclaration type)
    {
        this.locatedTypeDeclarations.Remove(location);
        this.locatedTypeDeclarations.Add(location, type);
    }

    private static void SetParentsCore(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        type.SetParent();
        foreach (TypeDeclaration child in type.RefResolvablePropertyDeclarations.Values)
        {
            SetParentsCore(child, visitedTypes);
        }
    }

    private static void SetParents(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypes = new();
        SetParentsCore(rootTypeDeclaration, visitedTypes);
    }

    private void FindAndBuildPropertiesCore(TypeDeclaration rootTypeDeclaration)
    {
        HashSet<TypeDeclaration> typesVisitedForBuild = new();

        this.FindAndBuildPropertiesCore(rootTypeDeclaration, typesVisitedForBuild);

        typesVisitedForBuild.Clear();
        this.SetDotnetPropertyNames(rootTypeDeclaration, typesVisitedForBuild);
    }

    private void FindAndBuildPropertiesCore(TypeDeclaration type, HashSet<TypeDeclaration> typesVisitedForBuild)
    {
        if (typesVisitedForBuild.Contains(type))
        {
            return;
        }

        typesVisitedForBuild.Add(type);

        HashSet<TypeDeclaration> typesVisited = new();

        this.JsonSchemaConfiguration.FindAndBuildPropertiesAdapter(this.propertyBuilder, type, type, typesVisited, false);

        foreach (TypeDeclaration subtype in type.RefResolvablePropertyDeclarations.Values)
        {
            this.FindAndBuildPropertiesCore(subtype, typesVisitedForBuild);
        }
    }

    private async Task<TypeDeclaration> BuildTypeDeclarationFor(LocatedSchema baseSchema)
    {
        WalkContext context = new(this, baseSchema);
        return await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);
    }

    private async Task<TypeDeclaration> BuildTypeDeclarationFor(WalkContext context)
    {
        if (!this.schemaRegistry.TryGetValue(context.SubschemaLocation, out LocatedSchema? schema))
        {
            throw new InvalidOperationException($"Unable to find the schema at ${context.SubschemaLocation}");
        }

        if (TryGetBooleanSchemaTypeDeclaration(schema, out TypeDeclaration? booleanTypeDeclaration))
        {
            if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? existingBooleanTypeDeclaration))
            {
                return existingBooleanTypeDeclaration;
            }

            this.locatedTypeDeclarations.Add(context.SubschemaLocation, booleanTypeDeclaration);
            return booleanTypeDeclaration;
        }

        // Create a type declaration for this location
        TypeDeclaration typeDeclaration = new(this, schema);

        // Check to see if we have a recursive scope
        if (context.TryGetScopeForFirstRecursiveAnchor(out JsonReference? baseScopeLocation))
        {
            // Set the recursive scope if we have one, for the root entity.
            typeDeclaration.SetRecursiveScope(baseScopeLocation.Value);
        }

        // Are we already building the type here?
        if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? existingTypeDeclaration))
        {
            // We need to determine if it has a dynamic reference to a dynamic anchor
            // owned by this type
            if (this.TryGetNewDynamicScope(existingTypeDeclaration, context, out JsonReference? dynamicScope))
            {
                // We remove the existing one and replace it, for this context.
                context.ReplaceDeclarationInScope(context.SubschemaLocation, existingTypeDeclaration);
                this.locatedTypeDeclarations.Remove(context.SubschemaLocation);

                // Update the dynamic location
                typeDeclaration.UpdateDynamicLocation(dynamicScope.Value);

                if (this.locatedTypeDeclarations.TryGetValue(typeDeclaration.LocatedSchema.Location, out TypeDeclaration? existingDynamicDeclaration))
                {
                    // If we already exist in the dynamic location
                    // Add it to the current locatedTypeDeclarations for this subschema, and return it.
                    this.locatedTypeDeclarations.Add(context.SubschemaLocation, existingDynamicDeclaration);
                    return existingDynamicDeclaration;
                }

                this.schemaRegistry.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration.LocatedSchema);
                this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
                this.locatedTypeDeclarations.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration);
            }
            else if (this.TryGetNewRecursiveScope(existingTypeDeclaration, context, out JsonReference? recursiveScope))
            {
                // We remove the existing one and replace it, for this context.
                if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? previousRecursiveDefinition))
                {
                    context.ReplaceDeclarationInScope(context.SubschemaLocation, previousRecursiveDefinition);
                    this.locatedTypeDeclarations.Remove(context.SubschemaLocation);
                }

                // Update the recursive location
                typeDeclaration.UpdateRecursiveLocation(recursiveScope.Value);

                if (this.locatedTypeDeclarations.TryGetValue(typeDeclaration.LocatedSchema.Location, out TypeDeclaration? existingRecursiveDeclaration))
                {
                    // If we already exist in the dynamic location
                    // Add it to the current locatedTypeDeclarations for this subschema, and return it.
                    this.locatedTypeDeclarations.Add(context.SubschemaLocation, existingRecursiveDeclaration);
                    return existingRecursiveDeclaration;
                }

                this.schemaRegistry.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration.LocatedSchema);
                this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
                this.locatedTypeDeclarations.Add(typeDeclaration.LocatedSchema.Location, typeDeclaration);
            }
            else
            {
                // We can just use the existing type.
                return existingTypeDeclaration;
            }
        }
        else
        {
            this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
        }

        // Capture the original location before potentially entering a dynamic scope.
        JsonReference currentLocation = context.SubschemaLocation;

        // Try to enter the dynamic scope of the ID - note that this *replaces*
        // the current scope, so we will be automatically popping it when we get done.
        bool enteredDynamicScope = false;

        try
        {
            if (this.TryEnterDynamicScope(context, schema))
            {
                enteredDynamicScope = true;
                if (currentLocation != context.SubschemaLocation)
                {
                    // We have already built this in a preceding reference, at the dynamic location, so we need to replace our "local" version of the type
                    // and then return the version we were already building/had already built.
                    if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? alreadyBuildingDynamicTypeDeclaration))
                    {
                        if (alreadyBuildingDynamicTypeDeclaration.LocatedSchema.Location == typeDeclaration.LocatedSchema.Location)
                        {
                            this.locatedTypeDeclarations.Remove(currentLocation);
                            this.locatedTypeDeclarations.Add(currentLocation, alreadyBuildingDynamicTypeDeclaration);
                            return alreadyBuildingDynamicTypeDeclaration;
                        }
                        else
                        {
                            if (this.locatedTypeDeclarations.TryGetValue(context.SubschemaLocation, out TypeDeclaration? previousDefinition))
                            {
                                context.ReplaceDeclarationInScope(context.SubschemaLocation, previousDefinition);
                                this.locatedTypeDeclarations.Remove(context.SubschemaLocation);
                            }
                        }
                    }

                    this.locatedTypeDeclarations.Add(context.SubschemaLocation, typeDeclaration);
                }
            }

            await this.AddSubschemaForRefKeywords(context, typeDeclaration).ConfigureAwait(false);
            await this.AddSubschemaForRefResolvableKeywords(context, typeDeclaration).ConfigureAwait(false);

            return typeDeclaration;
        }
        finally
        {
            if (enteredDynamicScope)
            {
                context.LeaveScope();
            }
        }

        bool TryGetBooleanSchemaTypeDeclaration(LocatedSchema schema, [NotNullWhen(true)] out TypeDeclaration? booleanTypeDeclaration)
        {
            if (schema.Schema.ValueKind == JsonValueKind.True)
            {
                // We neeed to return a type declaration for JsonAny.
                booleanTypeDeclaration = this.propertyBuilder.AnyTypeDeclarationInstance;
                return true;
            }

            if (schema.Schema.ValueKind == JsonValueKind.False)
            {
                // We neeed to return a type declaration for JsonNotAny.
                booleanTypeDeclaration = this.propertyBuilder.NotAnyTypeDeclarationInstance;
                return true;
            }

            booleanTypeDeclaration = null;
            return false;
        }
    }

    private bool TryEnterDynamicScope(WalkContext context, LocatedSchema schema)
    {
        if (!schema.Schema.AsObject.TryGetProperty(this.JsonSchemaConfiguration.IdKeyword, out JsonString idValue))
        {
            return false;
        }

        if ((this.JsonSchemaConfiguration.ValidatingAs & ValidationSemantics.Pre201909) != 0 && HasRefKeyword(schema))
        {
            // Ignore ID if we are pre-draft2019-09 semantics and a reference keyword is present.
            return false;
        }

        string idv = idValue;
        if (context.Scope.Location.Uri.EndsWith(idv))
        {
            // Ignore ID if we were already directly in the dynamic scope.
            return false;
        }

        context.EnterDynamicScope(context.Scope.Location.Apply(new JsonReference(idv)), schema);
        return true;

        bool HasRefKeyword(LocatedSchema subschema)
        {
            foreach (RefKeyword keyword in this.JsonSchemaConfiguration.RefKeywords)
            {
                if (subschema.Schema.AsObject.HasProperty(keyword.Name))
                {
                    return true;
                }
            }

            return false;
        }
    }

    private class PropertyBuilder : IPropertyBuilder
    {
        private readonly JsonSchemaTypeBuilder builder;

        public PropertyBuilder(JsonSchemaTypeBuilder builder)
        {
            this.builder = builder;
            this.AnyTypeDeclarationInstance =
                new(this.builder, new LocatedSchema(BuiltInsLocation, new(true)))
                {
                    IsBuiltInType = true,
                };
            this.AnyTypeDeclarationInstance.SetDotnetTypeName(BuiltInTypes.AnyTypeDeclaration.Type);
            this.AnyTypeDeclarationInstance.SetNamespace(BuiltInTypes.AnyTypeDeclaration.Ns);
            this.NotAnyTypeDeclarationInstance =
                new(this.builder, new LocatedSchema(BuiltInsLocation, new(false)))
                {
                    IsBuiltInType = true,
                };
            this.NotAnyTypeDeclarationInstance.SetDotnetTypeName(BuiltInTypes.NotAnyTypeDeclaration.Type);
            this.NotAnyTypeDeclarationInstance.SetNamespace(BuiltInTypes.NotAnyTypeDeclaration.Ns);
        }

        /// <summary>
        /// Gets a type declaration to use as the JSON "any" type instance.
        /// </summary>
        public TypeDeclaration AnyTypeDeclarationInstance { get; }

        /// <summary>
        /// Gets a type declaration to use as the JSON "not any" type instance.
        /// </summary>
        public TypeDeclaration NotAnyTypeDeclarationInstance { get; }

        /// <summary>
        /// Find and build properties for the given types.
        /// </summary>
        /// <param name="source">The source from which to find the properties.</param>
        /// <param name="target">The target to which to add the properties.</param>
        /// <param name="typesVisited">The types we have already visited to find properties.</param>
        /// <param name="treatRequiredAsOptional">Whether to treat required properties as optional when adding.</param>
        public void FindAndBuildProperties(TypeDeclaration source, TypeDeclaration target, HashSet<TypeDeclaration> typesVisited, bool treatRequiredAsOptional)
        {
            this.builder.JsonSchemaConfiguration.FindAndBuildPropertiesAdapter(this, source, target, typesVisited, treatRequiredAsOptional);
        }
    }
}