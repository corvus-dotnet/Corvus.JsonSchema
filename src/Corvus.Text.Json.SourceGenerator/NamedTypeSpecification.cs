// <copyright file="NamedTypeSpecification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.SourceGeneratorTools;

/// <summary>
/// A type name requested for a schema location by a generation specification.
/// </summary>
/// <param name="reference">The canonical schema location for the type.</param>
/// <param name="dotnetTypeName">The .NET type name.</param>
/// <param name="dotnetNamespace">The .NET namespace.</param>
/// <param name="accessibility">The accessibility for the type.</param>
public readonly struct NamedTypeSpecification(JsonReference reference, string dotnetTypeName, string? dotnetNamespace = null, GeneratedTypeAccessibility? accessibility = null)
{
    /// <summary>
    /// Gets the canonical schema location for the type.
    /// </summary>
    public JsonReference Reference { get; } = reference;

    /// <summary>
    /// Gets the .NET type name.
    /// </summary>
    public string DotnetTypeName { get; } = dotnetTypeName;

    /// <summary>
    /// Gets the .NET namespace.
    /// </summary>
    public string? DotnetNamespace { get; } = dotnetNamespace;

    /// <summary>
    /// Gets the accessibility for the type.
    /// </summary>
    public GeneratedTypeAccessibility? Accessibility { get; } = accessibility;
}