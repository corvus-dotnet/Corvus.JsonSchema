// <copyright file="JsonSchemaTypeBuilder.References.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;
using Microsoft.CodeAnalysis;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Walks a JSON schema and builds a type map of it.
/// </summary>
public partial class JsonSchemaTypeBuilder
{
    private static readonly Regex AnchorPattern = new("^[A-Za-z][-A-Za-z0-9.:_]*$", RegexOptions.Compiled, TimeSpan.FromSeconds(3));

    private static (JsonReference Location, JsonReference Pointer) GetLocationAndPointerForAnchor(LocatedSchema baseSchemaForReference, string anchor, JsonReference baseSchemaForReferenceLocation)
    {
        JsonReference schemaForRefPointer;
        if (baseSchemaForReference.TryGetAnchor(anchor, out Anchor? registeredAnchor))
        {
            LocatedSchema schemaForRef = registeredAnchor.Schema;

            // Figure out a base schema location from the location of the anchored schema.
            baseSchemaForReferenceLocation = schemaForRef.Location.WithFragment(string.Empty);
            schemaForRefPointer = new JsonReference(ReadOnlySpan<char>.Empty, schemaForRef.Location.Fragment);
        }
        else
        {
            throw new InvalidOperationException($"Unable to find the anchor '{anchor}' in the schema at location '{baseSchemaForReferenceLocation}'");
        }

        return (baseSchemaForReferenceLocation, schemaForRefPointer);
    }

    private bool TryGetNewDynamicScope(TypeDeclaration existingTypeDeclaration, WalkContext context, [NotNullWhen(true)] out JsonReference? dynamicScope)
    {
        HashSet<string> dynamicReferences = this.GetDynamicReferences(existingTypeDeclaration);

        if (dynamicReferences.Count == 0)
        {
            dynamicScope = null;
            return false;
        }

        foreach (string dynamicAnchor in dynamicReferences)
        {
            if (context.TryGetScopeForFirstDynamicAnchor(dynamicAnchor, out JsonReference? baseScopeLocation))
            {
                // We have found a new dynamic anchor in the containing scope, so we cannot share a type
                // declaration with the previous instance.
                if (context.TryGetPreviousScope(out JsonReference? location) && location == baseScopeLocation)
                {
                    dynamicScope = location;
                    return true;
                }
            }
        }

        dynamicScope = null;
        return false;
    }

    private bool TryGetNewRecursiveScope(TypeDeclaration existingTypeDeclaration, WalkContext context, [NotNullWhen(true)] out JsonReference? recursiveScope)
    {
        bool hasRecursiveReferences = this.HasRecursiveReferences(existingTypeDeclaration);

        if (!hasRecursiveReferences)
        {
            recursiveScope = null;
            return false;
        }

        if (context.TryGetScopeForFirstRecursiveAnchor(out JsonReference? baseScopeLocation) && existingTypeDeclaration.RecursiveScope != baseScopeLocation)
        {
            recursiveScope = baseScopeLocation;
            return true;
        }

        recursiveScope = null;
        return false;
    }

    private bool HasRecursiveReferences(TypeDeclaration typeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypes = new();
        return this.HasRecursiveReferences(typeDeclaration, visitedTypes);
    }

    private HashSet<string> GetDynamicReferences(TypeDeclaration typeDeclaration)
    {
        HashSet<string> result = new();
        HashSet<TypeDeclaration> visitedTypes = new();
        this.GetDynamicReferences(typeDeclaration, result, visitedTypes);
        return result;
    }

    private bool HasRecursiveReferences(TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return false;
        }

        visitedTypes.Add(type);

        var recursiveRefKeywords = this.JsonSchemaConfiguration.RefKeywords.Where(k => k.RefKind == RefKind.RecursiveRef).ToDictionary(k => (string)new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Name), v => v);

        foreach (KeyValuePair<string, TypeDeclaration> prop in type.RefResolvablePropertyDeclarations)
        {
            if (recursiveRefKeywords.TryGetValue(prop.Key, out RefKeyword? refKeyword))
            {
                if (type.LocatedSchema.Schema.TryGetProperty(refKeyword.Name, out JsonAny value))
                {
                    return true;
                }
            }

            if (this.HasRecursiveReferences(prop.Value, visitedTypes))
            {
                return true;
            }
        }

        return false;
    }

    private void GetDynamicReferences(TypeDeclaration type, HashSet<string> result, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        var dynamicRefKeywords = this.JsonSchemaConfiguration.RefKeywords.Where(k => k.RefKind == RefKind.DynamicRef).ToDictionary(k => (string)new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Name), v => v);

        foreach (KeyValuePair<string, TypeDeclaration> prop in type.RefResolvablePropertyDeclarations)
        {
            if (dynamicRefKeywords.TryGetValue(prop.Key, out RefKeyword? refKeyword))
            {
                if (type.LocatedSchema.Schema.TryGetProperty(refKeyword.Name, out JsonAny value))
                {
                    var reference = new JsonReference(value);
                    if (reference.HasFragment)
                    {
                        ReadOnlySpan<char> fragmentWithoutLeadingHash = reference.Fragment[1..];
                        if (AnchorPattern.IsMatch(fragmentWithoutLeadingHash))
                        {
                            result.Add(fragmentWithoutLeadingHash.ToString());
                        }
                    }
                }
            }

            this.GetDynamicReferences(prop.Value, result, visitedTypes);
        }
    }

    private async Task AddSubschemaForRefKeywords(WalkContext context, TypeDeclaration typeDeclaration)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        foreach (RefKeyword keyword in this.JsonSchemaConfiguration.RefKeywords)
        {
            if (schema.Schema.TryGetProperty(keyword.Name, out JsonAny value))
            {
                context.EnterSubschemaScopeForUnencodedPropertyName(keyword.Name);
                JsonReference subschemaPath = new JsonReference("#").AppendUnencodedPropertyNameToFragment(keyword.Name);

                switch (keyword.RefKind)
                {
                    case RefKind.Ref:
                        await this.AddSubschemaForRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefKind.DynamicRef:
                        await this.AddSubschemaForDynamicRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefKind.RecursiveRef:
                        await this.AddSubschemaForRecursiveRef(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    default:
                        throw new InvalidOperationException($"Unknown reference kind '{Enum.GetName(keyword.RefKind)}' at '{context.SubschemaLocation}'");
                }

                context.LeaveScope();

                // We can return immediately because there can only be one ref-type keyword in a valid schema, and we've already validated the schema on the way in.
                return;
            }
        }
    }

    private async Task AddSubschemaForRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base schema against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        // Now, figure out the pointer or anchor if we have one
        JsonReference schemaForRefPointer;

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);
        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            (baseSchemaForReferenceLocation, schemaForRefPointer) = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private async Task AddSubschemaForDynamicRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base schema against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;

        // We've reached the first landing spot. Does it define a dynamic anchor?
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);

        bool hasDynamicAnchor =
            referenceHasAnchor &&
            baseSchemaForReference.TryGetAnchor(fragmentWithoutLeadingHash, out Anchor? registeredAnchor) &&
            registeredAnchor.IsDynamic;

        // It does, so look in the scopes for the first dynamic anchor
        if (hasDynamicAnchor && context.TryGetScopeForFirstDynamicAnchor(fragmentWithoutLeadingHash, out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!this.schemaRegistry.TryGetValue(baseSchemaForReferenceLocation, out LocatedSchema? dynamicBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the dynamic base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = dynamicBaseSchema;
        }

        JsonReference schemaForRefPointer;

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            (baseSchemaForReferenceLocation, schemaForRefPointer) = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private async Task AddSubschemaForRecursiveRef(JsonReference subschemaPath, JsonAny referenceValue, WalkContext context, TypeDeclaration typeDeclaration)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        LocatedSchema baseSchemaForReference;
        JsonReference baseSchemaForReferenceLocation;

        // First, we need to find the base type against which we are resolving pointers/anchors
        if (reference.HasUri)
        {
            if (reference.HasAbsoluteUri)
            {
                // Find the base schema, ignoring the fragment
                baseSchemaForReferenceLocation = reference.WithFragment(string.Empty);
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
            else
            {
                // Apply to the parent scope, ignoring the fragment
                baseSchemaForReferenceLocation = context.Scope.Location.Apply(reference.WithFragment(string.Empty));
                baseSchemaForReference = await this.ResolveBaseReference(baseSchemaForReferenceLocation).ConfigureAwait(false) ?? throw new InvalidOperationException($"Unable to load the schema at location '{baseSchemaForReferenceLocation}'");
            }
        }
        else
        {
            baseSchemaForReferenceLocation = context.Scope.Location;
            baseSchemaForReference = context.Scope.Schema;
        }

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;

        // We've reached the first landing spot. Does it define a dynamic anchor?
        bool referenceHasAnchor = !string.IsNullOrEmpty(fragmentWithoutLeadingHash) && AnchorPattern.IsMatch(fragmentWithoutLeadingHash);

        bool hasRecursiveAnchor =
            reference.HasFragment &&
            baseSchemaForReference.IsRecursiveAnchor;

        // It does, so look in the scopes for the first dynamic anchor
        if (hasRecursiveAnchor && context.TryGetScopeForFirstRecursiveAnchor(out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!this.schemaRegistry.TryGetValue(baseSchemaForReferenceLocation, out LocatedSchema? dynamicBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the dynamic base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = dynamicBaseSchema;
        }

        JsonReference schemaForRefPointer;

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = GetLocationAndPointerForAnchor(baseSchemaForReference, fragmentWithoutLeadingHash, baseSchemaForReferenceLocation);
        }
        else
        {
            (baseSchemaForReferenceLocation, schemaForRefPointer) = this.GetPointerForReference(baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        context.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
        context.LeaveScope();
    }

    private (JsonReference Location, JsonReference Pointer) GetPointerForReference(LocatedSchema baseSchemaForReference, JsonReference baseSchemaForReferenceLocation, JsonReference reference)
    {
        JsonReference schemaForRefPointer;
        if (reference.HasFragment)
        {
            // If we've already located it, this must be the thing.
            if (this.schemaRegistry.TryGetValue(new JsonReference(baseSchemaForReferenceLocation.Uri, reference.Fragment), out _))
            {
                schemaForRefPointer = new JsonReference(ReadOnlySpan<char>.Empty, reference.Fragment);
            }
            else
            {
                // Resolve the pointer, and add the type
                if (this.TryResolvePointer(baseSchemaForReferenceLocation, baseSchemaForReference.Schema.AsJsonElement, reference.Fragment, out (JsonReference Location, JsonReference Pointer)? result))
                {
                    return result.Value;
                }

                throw new InvalidOperationException($"Unable to resolve the schema from the base element '{baseSchemaForReferenceLocation}' with the pointer '{reference.Fragment.ToString()}'");
            }
        }
        else
        {
            schemaForRefPointer = new JsonReference("#");
        }

        return (baseSchemaForReferenceLocation, schemaForRefPointer);
    }

    private bool TryResolvePointer(JsonReference baseSchemaForReferenceLocation, JsonElement rootElement, ReadOnlySpan<char> fragment, [NotNullWhen(true)] out (JsonReference Location, JsonReference Pointer)? result)
    {
        string[] segments = fragment.ToString().Split('/');
        var currentBuilder = new StringBuilder();
        bool failed = false;
        foreach (string segment in segments)
        {
            if (currentBuilder.Length > 0)
            {
                currentBuilder.Append('/');
            }

            Span<char> decodedSegment = new char[segment.Length];
            int written = JsonPointerUtilities.DecodePointer(segment, decodedSegment);
            currentBuilder.Append(decodedSegment[..written]);
            if (this.schemaRegistry.TryGetValue(baseSchemaForReferenceLocation.WithFragment(currentBuilder.ToString()), out LocatedSchema? locatedSchema))
            {
                failed = false;
                if (!RefMatters(locatedSchema.Schema) && locatedSchema.Schema.TryGetProperty(this.JsonSchemaConfiguration.IdKeyword, out JsonAny value))
                {
                    // Update the base location and element for the found schema;
                    baseSchemaForReferenceLocation = baseSchemaForReferenceLocation.Apply(new JsonReference(value));
                    rootElement = locatedSchema.Schema.AsJsonElement;
                    currentBuilder.Clear();
                    currentBuilder.Append('#');
                }
            }
            else
            {
                failed = true;
            }
        }

        if (failed)
        {
            if (JsonPointerUtilities.TryResolvePointer(rootElement, fragment, out JsonElement? resolvedElement))
            {
                var pointerRef = new JsonReference(ReadOnlySpan<char>.Empty, fragment);
                JsonReference location = baseSchemaForReferenceLocation.Apply(pointerRef);
                this.schemaRegistry.AddSchemaAndSubschema(location, JsonAny.FromJson(resolvedElement.Value));
                result = (baseSchemaForReferenceLocation, pointerRef);
                return true;
            }
        }
        else
        {
            result = (baseSchemaForReferenceLocation, new JsonReference(currentBuilder.ToString()));
            return true;
        }

        result = null;
        return false;

        bool RefMatters(JsonAny schema)
        {
            return (this.JsonSchemaConfiguration.ValidatingAs & ValidationSemantics.Pre201909) != 0 && schema.HasProperty(this.JsonSchemaConfiguration.RefKeyword);
        }
    }

    private async Task<LocatedSchema?> ResolveBaseReference(JsonReference baseSchemaForReferenceLocation)
    {
        if (!this.schemaRegistry.TryGetValue(baseSchemaForReferenceLocation, out LocatedSchema? baseReferenceSchema))
        {
            JsonReference registeredSchemaReference = await this.schemaRegistry.RegisterDocumentSchema(baseSchemaForReferenceLocation).ConfigureAwait(false);
            if (!this.schemaRegistry.TryGetValue(registeredSchemaReference, out baseReferenceSchema))
            {
                return null;
            }
        }

        return baseReferenceSchema;
    }

    private async Task AddSubschemaForRefResolvableKeywords(WalkContext context, TypeDeclaration typeDeclaration)
    {
        LocatedSchema schema = typeDeclaration.LocatedSchema;

        foreach (RefResolvableKeyword keyword in this.JsonSchemaConfiguration.RefResolvableKeywords)
        {
            if (this.JsonSchemaConfiguration.DefinitionKeywords.Contains(keyword.Name))
            {
                // Don't build the type for a "definitions" types unless/until we actually use them, typically by reference.
                continue;
            }

            if (schema.Schema.TryGetProperty(keyword.Name, out JsonAny value))
            {
                context.EnterSubschemaScopeForUnencodedPropertyName(keyword.Name);

                // This is the path to the subschema for the property accessed at the keyword, from the parent subschema.
                JsonReference subschemaPath = new JsonReference("#").AppendUnencodedPropertyNameToFragment(keyword.Name);

                switch (keyword.RefResolvablePropertyKind)
                {
                    case RefResolvablePropertyKind.ArrayOfSchema:
                        await this.AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.MapOfSchema:
                        await this.AddSubschemaFromMapOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.MapOfSchemaIfValueIsSchemaLike:
                        await this.AddSubschemaFromMapOfSchemaIfValueIsSchemaLikeForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.Schema:
                        await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.SchemaIfValueIsSchemaLike:
                        await this.AddSubschemaFromSchemaIfValueIsSchemaLikeForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                    case RefResolvablePropertyKind.SchemaOrArrayOfSchema:
                        await this.AddSubschemaFromSchemaOrArrayOfSchemaForRefResolvableKeyword(subschemaPath, value, context, typeDeclaration).ConfigureAwait(false);
                        break;
                }

                context.LeaveScope();
            }
        }
    }

    private async Task AddSubschemaFromSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (!this.JsonSchemaConfiguration.ValidateSchema(schema))
        {
            throw new InvalidOperationException($"The schema at {context.SubschemaLocation} was not valid.");
        }

        // Build the subschema type declaration.
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

        // And add it to our ref resolvables.
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    private async Task AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny array, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (array.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The value at {context.SubschemaLocation} was not an array.");
        }

        int index = 0;
        foreach (JsonAny item in array.EnumerateArray())
        {
            context.EnterSubschemaScopeForArrayIndex(index);

            // Build the subschema type declaration.
            TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

            // And add it to our ref resolvables.
            typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath.AppendArrayIndexToFragment(index), subschemaTypeDeclaration);

            ++index;
            context.LeaveScope();
        }
    }

    private async Task AddSubschemaFromMapOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny map, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (map.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The value at {context.SubschemaLocation} was not an object.");
        }

        foreach (JsonObjectProperty item in map.EnumerateObject())
        {
            context.EnterSubschemaScopeForUnencodedPropertyName(item.Name);
            await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath.AppendUnencodedPropertyNameToFragment(item.Name), item.Value, context, typeDeclaration).ConfigureAwait(false);
            context.LeaveScope();
        }
    }

    private async Task AddSubschemaFromMapOfSchemaIfValueIsSchemaLikeForRefResolvableKeyword(JsonReference subschemaPath, JsonAny map, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (map.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException($"The value at {context.SubschemaLocation} was not an object.");
        }

        foreach (JsonObjectProperty item in map.EnumerateObject())
        {
            if (item.ValueKind == JsonValueKind.Object || item.ValueKind == JsonValueKind.True || item.ValueKind == JsonValueKind.False)
            {
                context.EnterSubschemaScopeForUnencodedPropertyName(item.Name);
                await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath.AppendUnencodedPropertyNameToFragment(item.Name), item.Value, context, typeDeclaration).ConfigureAwait(false);
                context.LeaveScope();
            }
        }
    }

    private async Task AddSubschemaFromSchemaIfValueIsSchemaLikeForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (!this.JsonSchemaConfiguration.ValidateSchema(schema))
        {
            // If our schema isn't a valid schema, we just ignore it.
            return;
        }

        // Build the subschema type declaration.
        TypeDeclaration subschemaTypeDeclaration = await this.BuildTypeDeclarationFor(context).ConfigureAwait(false);

        // And add it to our ref resolvables.
        typeDeclaration.AddRefResolvablePropertyDeclaration(subschemaPath, subschemaTypeDeclaration);
    }

    private async Task AddSubschemaFromSchemaOrArrayOfSchemaForRefResolvableKeyword(JsonReference subschemaPath, JsonAny schema, WalkContext context, TypeDeclaration typeDeclaration)
    {
        if (schema.ValueKind == JsonValueKind.Array)
        {
            await this.AddSubschemaFromArrayOfSchemaForRefResolvableKeyword(subschemaPath, schema, context, typeDeclaration).ConfigureAwait(false);
        }
        else
        {
            await this.AddSubschemaFromSchemaForRefResolvableKeyword(subschemaPath, schema, context, typeDeclaration).ConfigureAwait(false);
        }
    }
}