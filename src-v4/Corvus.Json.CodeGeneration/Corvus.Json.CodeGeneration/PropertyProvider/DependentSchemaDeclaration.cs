// <copyright file="DependentSchemaDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A propety name and a schema to apply to the object if the property is present..
/// </summary>
/// <param name="keyword">The keyword that produced the declaration.</param>
/// <param name="jsonPropertyName">The property name that is present if the schema applies.</param>
/// <param name="dependentSchemaType">The type of the dependent schema.</param>
public sealed class DependentSchemaDeclaration(IObjectPropertyDependentSchemasValidationKeyword keyword, string jsonPropertyName, TypeDeclaration dependentSchemaType)
{
    /// <summary>
    /// Gets the keyword for the dependent schema property.
    /// </summary>
    public IObjectPropertyDependentSchemasValidationKeyword Keyword { get; } = keyword;

    /// <summary>
    /// Gets the reduced type declaration for the dependent schema property.
    /// </summary>
    public TypeDeclaration ReducedDepdendentSchemaType { get; } = dependentSchemaType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the unreduced dependent schema property type.
    /// </summary>
    public TypeDeclaration UnreducedDepdendentSchemaType { get; } = dependentSchemaType;

    /// <summary>
    /// Gets the JSON property name.
    /// </summary>
    public string JsonPropertyName { get; } = jsonPropertyName;

    /// <summary>
    /// Gets the reduced path modifier for the dependent schema property.
    /// </summary>
    public string KeywordPathModifier { get; } = keyword.GetPathModifier(dependentSchemaType.ReducedTypeDeclaration(), jsonPropertyName);
}