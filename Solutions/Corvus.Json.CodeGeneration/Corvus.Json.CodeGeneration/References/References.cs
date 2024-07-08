// <copyright file="References.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;
using System.Web;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for <see cref="IReferenceKeyword"/> implementations.
/// </summary>
public static class References
{
    /// <summary>
    /// Determines if the type declaration has recursive references.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns><see langword="true"/> if the type declaration contains one or more recursive references.</returns>
    public static bool HasRecursiveReferences(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        HashSet<TypeDeclaration> visitedTypes = [];
        return HasRecursiveReferences(typeBuilderContext, typeDeclaration, visitedTypes);
    }

    /// <summary>
    /// Gets the dynamic reference for a type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration for which to get dynamic references.</param>
    /// <returns>The unique set of dynamic references.</returns>
    public static IReadOnlyCollection<string> GetDynamicReferences(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        HashSet<string> result = [];
        HashSet<TypeDeclaration> visitedTypes = [];
        GetDynamicReferences(typeBuilderContext, typeDeclaration, result, visitedTypes);
        return result;
    }

    /// <summary>
    /// Resolves a reference and builds its type declarations.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="referenceValue">The reference value.</param>
    /// <returns>A <see cref="ValueTask"/> which completes when the reference is resolved.</returns>
    public static async ValueTask ResolveStandardReference(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, string referenceValue)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        (JsonReference baseSchemaForReferenceLocation, LocatedSchema baseSchemaForReference) =
            await typeBuilderContext.SchemaRegistry.ResolveBaseReference(
                typeBuilderContext.Scope.Location,
                typeBuilderContext.Scope.LocatedSchema,
                reference);

        // Now, figure out the pointer or anchor if we have one
        JsonReference schemaForRefPointer;

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;
        bool referenceHasAnchor = Anchors.IsAnchor(fragmentWithoutLeadingHash);
        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = Anchors.GetLocationAndPointerForAnchor(baseSchemaForReferenceLocation, baseSchemaForReference, fragmentWithoutLeadingHash);
        }
        else
        {
            // The fragment is a reference
            (baseSchemaForReferenceLocation, schemaForRefPointer) = References.GetPointerForReference(typeBuilderContext.SchemaRegistry, baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        typeBuilderContext.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
        typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath, subschemaTypeDeclaration);
        typeBuilderContext.LeaveScope();
    }

    /// <summary>
    /// Resolves a recursive reference and builds its type declarations.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="referenceValue">The reference value.</param>
    /// <returns>A <see cref="ValueTask"/> which completes when the reference is resolved.</returns>
    public static async ValueTask ResolveRecursiveReference(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, string referenceValue)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        (JsonReference baseSchemaForReferenceLocation, LocatedSchema baseSchemaForReference) =
            await typeBuilderContext.SchemaRegistry.ResolveBaseReference(
                typeBuilderContext.Scope.Location,
                typeBuilderContext.Scope.LocatedSchema,
                reference);

        // Now, figure out the pointer or anchor if we have one
        JsonReference schemaForRefPointer;

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;
        bool referenceHasAnchor = Anchors.IsAnchor(fragmentWithoutLeadingHash);

        bool hasRecursiveAnchor =
            reference.HasFragment &&
            Anchors.IsRecursiveAnchor(baseSchemaForReference);

        if (hasRecursiveAnchor && Anchors.TryGetScopeForFirstRecursiveAnchor(typeBuilderContext, out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!typeBuilderContext.SchemaRegistry.TryGetLocatedSchema(baseSchemaForReferenceLocation, out LocatedSchema? recursiveBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = recursiveBaseSchema;
        }

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = Anchors.GetLocationAndPointerForAnchor(baseSchemaForReferenceLocation, baseSchemaForReference, fragmentWithoutLeadingHash);
        }
        else
        {
            // The fragment is a reference
            (baseSchemaForReferenceLocation, schemaForRefPointer) = References.GetPointerForReference(typeBuilderContext.SchemaRegistry, baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        typeBuilderContext.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
        typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath, subschemaTypeDeclaration);
        typeBuilderContext.LeaveScope();
    }

    /// <summary>
    /// Resolves a dynamic reference and builds its type declarations.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="subschemaPath">The subschema path.</param>
    /// <param name="referenceValue">The reference value.</param>
    /// <returns>A <see cref="ValueTask"/> which completes when the reference is resolved.</returns>
    public static async ValueTask ResolveDynamicReference(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, JsonReference subschemaPath, string referenceValue)
    {
        JsonReference reference = new(HttpUtility.UrlDecode(referenceValue));

        (JsonReference baseSchemaForReferenceLocation, LocatedSchema baseSchemaForReference) =
            await typeBuilderContext.SchemaRegistry.ResolveBaseReference(
                typeBuilderContext.Scope.Location,
                typeBuilderContext.Scope.LocatedSchema,
                reference);

        // Now, figure out the pointer or anchor if we have one
        JsonReference schemaForRefPointer;

        string fragmentWithoutLeadingHash = reference.HasFragment ? reference.Fragment[1..].ToString() : string.Empty;
        bool referenceHasAnchor = Anchors.IsAnchor(fragmentWithoutLeadingHash);

        bool hasDynamicAnchor =
            reference.HasFragment &&
            Anchors.HasDynamicAnchor(baseSchemaForReference, fragmentWithoutLeadingHash);

        if (hasDynamicAnchor &&
            Anchors.TryGetScopeForFirstDynamicAnchor(
                typeBuilderContext,
                fragmentWithoutLeadingHash,
                out JsonReference? baseScopeLocation))
        {
            baseSchemaForReferenceLocation = baseScopeLocation.Value;
            if (!typeBuilderContext.SchemaRegistry.TryGetLocatedSchema(baseSchemaForReferenceLocation, out LocatedSchema? dynamicBaseSchema))
            {
                throw new InvalidOperationException($"Unable to resolve the base schema at '{baseSchemaForReferenceLocation}'");
            }

            baseSchemaForReference = dynamicBaseSchema;
        }

        if (referenceHasAnchor)
        {
            // The fragment is an anchor
            (baseSchemaForReferenceLocation, schemaForRefPointer) = Anchors.GetLocationAndPointerForAnchor(baseSchemaForReferenceLocation, baseSchemaForReference, fragmentWithoutLeadingHash);
        }
        else
        {
            // The fragment is a reference
            (baseSchemaForReferenceLocation, schemaForRefPointer) = References.GetPointerForReference(typeBuilderContext.SchemaRegistry, baseSchemaForReference, baseSchemaForReferenceLocation, reference);
        }

        typeBuilderContext.EnterReferenceScope(baseSchemaForReferenceLocation, baseSchemaForReference, schemaForRefPointer);
        TypeDeclaration subschemaTypeDeclaration = await typeBuilderContext.BuildTypeDeclarationForCurrentScope().ConfigureAwait(false);
        typeDeclaration.AddSubschemaTypeDeclaration(subschemaPath, subschemaTypeDeclaration);
        typeBuilderContext.LeaveScope();
    }

    /// <summary>
    /// Gets the pointer for the base location and pointer for a reference.
    /// </summary>
    /// <param name="schemaRegistry">The schema registry.</param>
    /// <param name="baseSchemaForReference">The base schema for the reference.</param>
    /// <param name="baseSchemaForReferenceLocation">The base schema location for the reference.</param>
    /// <param name="reference">The reference.</param>
    /// <returns>The location and pointer for the reference, as applied to the base schema location.</returns>
    /// <exception cref="InvalidOperationException">It was not possible to resolve the schema from the base element using the reference fragment.</exception>
    public static (JsonReference Location, JsonReference Pointer) GetPointerForReference(JsonSchemaRegistry schemaRegistry, LocatedSchema baseSchemaForReference, JsonReference baseSchemaForReferenceLocation, JsonReference reference)
    {
        JsonReference schemaForRefPointer;
        if (reference.HasFragment)
        {
            // If we've already located it, this must be the thing.
            if (schemaRegistry.TryGetLocatedSchema(new JsonReference(baseSchemaForReferenceLocation.Uri, reference.Fragment), out _))
            {
                schemaForRefPointer = new JsonReference([], reference.Fragment);
            }
            else
            {
                // Resolve the pointer, and add the type
                if (TryResolvePointer(schemaRegistry, baseSchemaForReferenceLocation, baseSchemaForReference, reference.Fragment, out (JsonReference Location, JsonReference Pointer)? result))
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

    private static void GetDynamicReferences(TypeBuilderContext typeBuilderContext, TypeDeclaration type, HashSet<string> result, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return;
        }

        visitedTypes.Add(type);

        var dynamicRefKeywords = type.LocatedSchema.Vocabulary.Keywords.OfType<IDynamicReferenceKeyword>().ToDictionary(k => (string)new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Keyword), v => v);

        foreach (KeyValuePair<string, TypeDeclaration> prop in type.SubschemaTypeDeclarations)
        {
            if (dynamicRefKeywords.TryGetValue(prop.Key, out IDynamicReferenceKeyword? refKeyword))
            {
                if (type.LocatedSchema.Schema.TryGetProperty(refKeyword.KeywordUtf8, out JsonElement value))
                {
                    string refValue = value.GetString() ?? string.Empty;
                    var reference = new JsonReference(refValue);
                    if (reference.HasFragment)
                    {
#if NET8_0_OR_GREATER
                        ReadOnlySpan<char> fragmentWithoutLeadingHash = reference.Fragment[1..];
                        if (CommonPatterns.AnchorPattern.IsMatch(fragmentWithoutLeadingHash))
                        {
                            result.Add(fragmentWithoutLeadingHash.ToString());
                        }
#else
                        string fragmentWithoutLeadingHash = reference.Fragment[1..].ToString();
                        if (CommonPatterns.AnchorPattern.IsMatch(fragmentWithoutLeadingHash))
                        {
                            result.Add(fragmentWithoutLeadingHash);
                        }
#endif
                    }
                }
            }

            GetDynamicReferences(typeBuilderContext, prop.Value, result, visitedTypes);
        }
    }

    private static bool HasRecursiveReferences(TypeBuilderContext typeBuilderContext, TypeDeclaration type, HashSet<TypeDeclaration> visitedTypes)
    {
        if (visitedTypes.Contains(type))
        {
            return false;
        }

        visitedTypes.Add(type);

        var recursiveRefKeywords = type.LocatedSchema.Vocabulary.Keywords.OfType<IRecursiveReferenceKeyword>().ToDictionary(k => (string)new JsonReference("#").AppendUnencodedPropertyNameToFragment(k.Keyword), v => v);

        foreach (KeyValuePair<string, TypeDeclaration> prop in type.SubschemaTypeDeclarations)
        {
            if (recursiveRefKeywords.TryGetValue(prop.Key, out IRecursiveReferenceKeyword? refKeyword))
            {
                if (type.LocatedSchema.Schema.TryGetProperty(refKeyword.KeywordUtf8, out _))
                {
                    return true;
                }
            }

            if (HasRecursiveReferences(typeBuilderContext, prop.Value, visitedTypes))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryResolvePointer(
        JsonSchemaRegistry schemaRegistry,
        JsonReference baseSchemaForReferenceLocation,
        LocatedSchema baseSchemaFoReference,
        ReadOnlySpan<char> fragment,
        [NotNullWhen(true)] out (JsonReference Location, JsonReference Pointer)? result)
    {
        JsonElement rootElement = baseSchemaFoReference.Schema;
        LocatedSchema currentSchema = baseSchemaFoReference;
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
#if NET8_0_OR_GREATER
            int written = JsonPointerUtilities.DecodePointer(segment, decodedSegment);
            currentBuilder.Append(decodedSegment[..written]);
#else
            int written = JsonPointerUtilities.DecodePointer(segment.AsSpan(), decodedSegment);
            currentBuilder.Append(decodedSegment[..written].ToString());
#endif
            if (schemaRegistry.TryGetLocatedSchema(baseSchemaForReferenceLocation.WithFragment(currentBuilder.ToString()), out LocatedSchema? locatedSchema))
            {
                failed = false;
                currentSchema = locatedSchema;

                if (Scope.ShouldEnterScope(locatedSchema.Schema, locatedSchema.Vocabulary, out string? value))
                {
                    // Update the base location and element for the found schema;
                    baseSchemaForReferenceLocation = baseSchemaForReferenceLocation.Apply(new JsonReference(value));
                    rootElement = locatedSchema.Schema;
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
                var pointerRef = new JsonReference([], fragment);
                JsonReference location = baseSchemaForReferenceLocation.Apply(pointerRef);
                schemaRegistry.AddSchemaAndSubschema(location, resolvedElement.Value, currentSchema.Vocabulary);
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
    }
}