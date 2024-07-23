// <copyright file="PatternPropertyDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A regular expression and a schema.
/// </summary>
public sealed class PatternPropertyDeclaration(IObjectPatternPropertyValidationKeyword keyword, TypeDeclaration patternPropertyType)
{
    /// <summary>
    /// Gets the keyword for the pattern property.
    /// </summary>
    public IObjectPatternPropertyValidationKeyword Keyword { get; } = keyword;

    /// <summary>
    /// Gets the reduced type declaration for the pattern property.
    /// </summary>
    public TypeDeclaration ReducedPatternPropertyType { get; } = patternPropertyType.ReducedTypeDeclaration().ReducedType;

    /// <summary>
    /// Gets the unreduced pattern property type.
    /// </summary>
    public TypeDeclaration UnreducedPatternPropertyType { get; } = patternPropertyType;

    /// <summary>
    /// Gets the reduced path modifier for the pattern property.
    /// </summary>
    public string KeywordPathModifier { get; } = keyword.GetPathModifier(patternPropertyType.ReducedTypeDeclaration());
}