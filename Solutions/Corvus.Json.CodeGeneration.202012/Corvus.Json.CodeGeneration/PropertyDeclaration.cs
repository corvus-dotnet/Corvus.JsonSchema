// <copyright file="PropertyDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.JsonSchema.Draft202012;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A property declaration in a <see cref="TypeDeclaration"/>.
/// </summary>
public class PropertyDeclaration : PropertyDeclaration<Schema, PropertyDeclaration, TypeDeclaration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyDeclaration"/> class.
    /// </summary>
    /// <param name="type">The type of the property.</param>
    /// <param name="jsonPropertyName">The json property name.</param>
    /// <param name="isRequired">Whether the property is required by default.</param>
    /// <param name="isInLocalScope">Whether the property is in the local scope.</param>
    public PropertyDeclaration(TypeDeclaration type, string jsonPropertyName, bool isRequired, bool isInLocalScope)
        : base(type, jsonPropertyName, isRequired, isInLocalScope)
    {
    }

    /// <summary>
    /// Gets a value indicating whether this property has a default value.
    /// </summary>
    public bool HasDefaultValue => this.Type.Schema.Default.IsNotUndefined();

    /// <summary>
    /// Gets the default value for the property.
    /// </summary>
    public string? DefaultValue => this.Type.Schema.Default is JsonAny def ? def.ToString() : default;

    /// <inheritdoc/>
    public override PropertyDeclaration WithRequired(bool isRequired)
    {
        return new PropertyDeclaration(this.Type, this.JsonPropertyName, isRequired, this.IsDefinedInLocalScope);
    }
}