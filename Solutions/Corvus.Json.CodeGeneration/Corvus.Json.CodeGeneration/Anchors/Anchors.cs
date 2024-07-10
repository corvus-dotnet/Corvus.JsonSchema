// <copyright file="Anchors.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for working with anchors.
/// </summary>
public static class Anchors
{
    /// <summary>
    /// Try to get the anchor keyword for a schema.
    /// </summary>
    /// <param name="schema">The schema for which to get the anchor keyword.</param>
    /// <param name="vocabulary">The vocabulary containing the keywords.</param>
    /// <param name="anchorKeyword">The resulting anchor keyword.</param>
    /// <returns><see langword="true"/> if an anchor keyword is found.</returns>
    /// <remraks>Note that this will find the first anchor keyword if multiple anchor keywords are set.</remraks>
    public static bool TryGetAnchorKeyword(in JsonElement schema, IVocabulary vocabulary, out IAnchorKeyword? anchorKeyword)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            anchorKeyword = null;
            return false;
        }

        foreach (IAnchorKeyword keyword in vocabulary.Keywords.OfType<IAnchorKeyword>())
        {
            if (schema.HasKeyword(keyword))
            {
                anchorKeyword = keyword;
                return true;
            }
        }

        anchorKeyword = null;
        return false;
    }

    /// <summary>
    /// Add anchors to the base schema for the given schema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schema">The anchoring schema.</param>
    /// <param name="currentLocation">The current location.</param>
    /// <param name="vocabulary">The current vocabulary.</param>
    public static void AddAnchors(JsonSchemaRegistry jsonSchemaRegistry, in JsonElement schema, in JsonReference currentLocation, IVocabulary vocabulary)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        foreach (IAnchorKeyword keyword in vocabulary.Keywords.OfType<IAnchorKeyword>())
        {
            keyword.AddAnchor(jsonSchemaRegistry, schema, currentLocation);
        }
    }

    /// <summary>
    /// Applies the anchor scope to a type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration to which to apply the scope.</param>
    /// <param name="existingTypeDeclaration">The existing type declaration which corresponds to this type declaration.</param>
    /// <returns>The result of processing the scope.</returns>
    public static ApplyScopeResult ApplyScopeToExistingType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, TypeDeclaration existingTypeDeclaration)
    {
        foreach (IAnchorKeyword keyword in existingTypeDeclaration.Keywords().OfType<IAnchorKeyword>())
        {
            if (keyword.TryApplyScopeToExistingType(typeBuilderContext, typeDeclaration, existingTypeDeclaration, out ApplyScopeResult result))
            {
                return result;
            }
        }

        return new(typeDeclaration, true);
    }

    /// <summary>
    /// Applies the anchor scope to a new type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration to which to apply the scope.</param>
    public static void ApplyScopeToNewType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration)
    {
        foreach (IAnchorKeyword keyword in typeDeclaration.Keywords().OfType<IAnchorKeyword>())
        {
            if (keyword.TryApplyScopeToNewType(typeBuilderContext, typeDeclaration))
            {
                break;
            }
        }
    }

    /// <summary>
    /// Gets the location and pointer for the named anchor.
    /// </summary>
    /// <param name="baseSchemaForReferenceLocation">The base schema reference location.</param>
    /// <param name="baseSchemaForReference">The base schema for reference.</param>
    /// <param name="anchorName">The name of the anchor.</param>
    /// <returns>The location and point for the named anchor.</returns>
    /// <exception cref="InvalidOperationException">No anchor was found at the location.</exception>
    public static (JsonReference Location, JsonReference Pointer) GetLocationAndPointerForAnchor(JsonReference baseSchemaForReferenceLocation, LocatedSchema baseSchemaForReference, string anchorName)
    {
        JsonReference schemaForRefPointer;
        if (baseSchemaForReference.LocatedAnchors.OfType<NamedLocatedAnchor>().SingleOrDefault(a => a.Name == anchorName) is NamedLocatedAnchor registeredAnchor)
        {
            LocatedSchema schemaForRef = registeredAnchor.Schema;

            // Figure out a base schema location from the location of the anchored schema.
            baseSchemaForReferenceLocation = schemaForRef.Location.WithFragment(string.Empty);
            schemaForRefPointer = new JsonReference([], schemaForRef.Location.Fragment);
        }
        else
        {
            throw new InvalidOperationException($"Unable to find the anchor '{anchorName}' in the schema at location '{baseSchemaForReferenceLocation}'");
        }

        return (baseSchemaForReferenceLocation, schemaForRefPointer);
    }

    /// <summary>
    /// Gets a value indicating whether an string is a valid anchor.
    /// </summary>
    /// <param name="anchorString">The candidate anchor string.</param>
    /// <returns><see langword="true"/> if the candidate string is a valid anchor.</returns>
    public static bool IsAnchor(string anchorString)
    {
        return !string.IsNullOrEmpty(anchorString) && CommonPatterns.AnchorPattern.IsMatch(anchorString);
    }

    /// <summary>
    /// Determines if the base schema is a recursive anchor.
    /// </summary>
    /// <param name="baseSchema">The base schema to test.</param>
    /// <returns><see langword="true"/> if the base schema is a recursive anchor.</returns>
    public static bool IsRecursiveAnchor(LocatedSchema baseSchema)
    {
        return baseSchema.Vocabulary.Keywords.OfType<IRecursiveAnchorKeyword>().Any(a => a.IsRecursiveAnchor(baseSchema.Schema));
    }

    /// <summary>
    /// Determines if the base schema is a recursive anchor.
    /// </summary>
    /// <param name="baseSchema">The base schema to test.</param>
    /// <param name="name">The name of the anchor.</param>
    /// <returns><see langword="true"/> if the base schema is a recursive anchor.</returns>
    public static bool HasDynamicAnchor(LocatedSchema baseSchema, string name)
    {
        return baseSchema.LocatedAnchors.OfType<DynamicLocatedAnchor>().Any(k => k.Name == name);
    }

    /// <summary>
    /// Try to get the base scope location for the first recursive anchor.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="baseScopeLocation">The base scope location.</param>
    /// <returns><see langword="true"/> if a base scope location could be found.</returns>
    public static bool TryGetScopeForFirstRecursiveAnchor(TypeBuilderContext typeBuilderContext, [NotNullWhen(true)] out JsonReference? baseScopeLocation)
    {
        foreach (IRecursiveAnchorKeyword keyword in typeBuilderContext.Scope.LocatedSchema.Vocabulary.Keywords.OfType<IRecursiveAnchorKeyword>())
        {
            if (keyword.TryGetScopeForFirstRecursiveAnchor(typeBuilderContext, out baseScopeLocation))
            {
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }

    /// <summary>
    /// Finds the scope containing the first dynamic anchor that corresponds to the given anchor name.
    /// </summary>
    /// <param name="typeBuilderContext">The type builder context.</param>
    /// <param name="anchor">The anchor name.</param>
    /// <param name="baseScopeLocation">The base scope location in which the anchor was found.</param>
    /// <returns><see langword="true"/> if a dynamic anchor was found that matches the name, otherwise <see langword="false"/>.</returns>
    public static bool TryGetScopeForFirstDynamicAnchor(TypeBuilderContext typeBuilderContext, string anchor, [NotNullWhen(true)] out JsonReference? baseScopeLocation)
    {
        JsonSchemaScope? foundScope = null;

        foreach (JsonSchemaScope scope in typeBuilderContext.ReversedStack)
        {
            // Ignore consecutive identical scopes
            if (foundScope is JsonSchemaScope fs && fs.Location == scope.Location)
            {
                continue;
            }

            foundScope = scope;

            if (Anchors.HasDynamicAnchor(scope.LocatedSchema, anchor))
            {
                baseScopeLocation = scope.Location;
                return true;
            }
        }

        baseScopeLocation = null;
        return false;
    }

    /// <summary>
    /// The result of applying the scope to a type declaration.
    /// </summary>
    /// <remarks>
    /// Initializes a new instance of the <see cref="ApplyScopeResult"/> struct.
    /// </remarks>
    /// <param name="typeDeclaration">The updated type declaration.</param>
    /// <param name="isCompleteTypeDeclaration">If the type declaration is complete and requires no further processing.</param>
    public readonly struct ApplyScopeResult(TypeDeclaration typeDeclaration, bool isCompleteTypeDeclaration)
    {
        /// <summary>
        /// Gets the type declaration.
        /// </summary>
        public TypeDeclaration TypeDeclaration { get; } = typeDeclaration;

        /// <summary>
        /// Gets a value indicating whether this type declaration is complete
        /// and requires no further processing.
        /// </summary>
        public bool IsCompleteTypeDeclaration { get; } = isCompleteTypeDeclaration;
    }
}