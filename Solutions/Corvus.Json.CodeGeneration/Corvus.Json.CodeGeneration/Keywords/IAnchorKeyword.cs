// <copyright file="IAnchorKeyword.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// An anchor keyword that is capable of providing a scope location
/// for an anchor.
/// </summary>
public interface IAnchorKeyword : IKeyword
{
    /// <summary>
    /// Add an anchor to the schema registry for the given schema.
    /// </summary>
    /// <param name="jsonSchemaRegistry">The <see cref="JsonSchemaRegistry"/>.</param>
    /// <param name="schema">The schema to which to add the anchor.</param>
    /// <param name="currentLocation">The current location.</param>
    void AddAnchor(JsonSchemaRegistry jsonSchemaRegistry, JsonElement schema, JsonReference currentLocation);

    /// <summary>
    /// Applies the anchor scope to a type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration to which to apply the scope.</param>
    /// <param name="existingTypeDeclaration">The existing type declaration which corresponds to this type declaration.</param>
    /// <param name="result">The result of processing the scope.</param>
    /// <returns><see langword="true"/> if the keyword handled the scope.</returns>
    bool TryApplyScopeToExistingType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration, TypeDeclaration existingTypeDeclaration, [NotNullWhen(true)] out Anchors.ApplyScopeResult result);

    /// <summary>
    /// Applies the anchor scope to a new type declaration.
    /// </summary>
    /// <param name="typeBuilderContext">The current type builder context.</param>
    /// <param name="typeDeclaration">The type declaration to which to apply the scope.</param>
    /// <returns><see langword="true"/> if the anchor handled the scope.</returns>
    bool TryApplyScopeToNewType(TypeBuilderContext typeBuilderContext, TypeDeclaration typeDeclaration);
}