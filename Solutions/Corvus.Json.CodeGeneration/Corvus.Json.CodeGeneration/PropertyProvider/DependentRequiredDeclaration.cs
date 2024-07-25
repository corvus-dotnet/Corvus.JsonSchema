// <copyright file="DependentRequiredDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A propety name and a required to apply to the object if the property is present..
/// </summary>
/// <param name="keyword">The keyword that produced the declaration.</param>
/// <param name="jsonPropertyName">The property name that is present if the required applies.</param>
/// <param name="dependencies">The required property names.</param>
public sealed class DependentRequiredDeclaration(IObjectDependentRequiredValidationKeyword keyword, string jsonPropertyName, IReadOnlyCollection<string> dependencies)
{
    /// <summary>
    /// Gets the keyword for the dependent required property.
    /// </summary>
    public IObjectDependentRequiredValidationKeyword Keyword { get; } = keyword;

    /// <summary>
    /// Gets the JSON property name.
    /// </summary>
    public string JsonPropertyName { get; } = jsonPropertyName;

    /// <summary>
    /// Gets the required property names.
    /// </summary>
    public IReadOnlyCollection<string> Dependencies { get; } = dependencies;
}