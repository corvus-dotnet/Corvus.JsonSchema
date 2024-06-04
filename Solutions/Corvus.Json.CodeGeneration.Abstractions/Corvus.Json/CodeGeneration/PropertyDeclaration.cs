// <copyright file="PropertyDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// A property declaration in a <see cref="TypeDeclaration"/>.
/// </summary>
public class PropertyDeclaration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PropertyDeclaration"/> class.
    /// </summary>
    /// <param name="type">The type of the property.</param>
    /// <param name="jsonPropertyName">The json property name.</param>
    /// <param name="isRequired">Whether the property is required by default.</param>
    /// <param name="isInLocalScope">Whether the property is in the local scope.</param>
    /// <param name="hasDefaultValue">Determines whether this property has a default value.</param>
    /// <param name="defaultValue">Gets the raw string value for the default value, or null if there is no default value.</param>
    /// <param name="xmlDocumentationRemarks">Gets the formatted documentation for the property.</param>
    /// <param name="isDeprecated">Determines whether this property is deprecated.</param>
    public PropertyDeclaration(TypeDeclaration type, string jsonPropertyName, bool isRequired, bool isInLocalScope, bool hasDefaultValue, string? defaultValue, string? xmlDocumentationRemarks, bool isDeprecated)
    {
        this.Type = type;
        this.JsonPropertyName = jsonPropertyName;
        this.IsRequired = isRequired;
        this.IsDefinedInLocalScope = isInLocalScope;
        this.DefaultValue = defaultValue;
        this.HasDefaultValue = hasDefaultValue;
        this.XmlDocumentationRemarks = xmlDocumentationRemarks;
        this.IsDeprecated = isDeprecated;
    }

    /// <summary>
    /// Gets the type of the property.
    /// </summary>
    public TypeDeclaration Type { get; }

    /// <summary>
    /// Gets the json property name of the property.
    /// </summary>
    public string JsonPropertyName { get; }

    /// <summary>
    /// Gets a value indicating whether this property is required.
    /// </summary>
    public bool IsRequired { get; }

    /// <summary>
    /// Gets a value indicating whether this property is defined in the local scope.
    /// </summary>
    /// <remarks>If true, then this property is defined in the current schema. If false, it
    /// has been derived from a merged type.</remarks>
    public bool IsDefinedInLocalScope { get; }

    /// <summary>
    /// Gets or sets the dotnet property name.
    /// </summary>
    public string? DotnetPropertyName { get; set; }

    /// <summary>
    /// Gets the dotnet parameter name.
    /// </summary>
    public string? DotnetParameterName => this.DotnetPropertyName is string dnpn ? Formatting.ToCamelCaseWithReservedWords(dnpn).ToString() : null;

    /// <summary>
    /// Gets the constructor parameter name for this property.
    /// </summary>
    public string? ConstructorParameterName => this.DotnetPropertyName is string dnpn ? char.ToLower(dnpn[0]) + dnpn[1..] : null;

    /// <summary>
    /// Gets a value indicating whether this property has a default value.
    /// </summary>
    public bool HasDefaultValue { get; }

    /// <summary>
    /// Gets the default value for the property.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Gets a value indicating whether this property is deprecated.
    /// </summary>
    public bool IsDeprecated { get; }

    /// <summary>
    /// Gets a value indicating whether this property has formatted XML documentation remarks.
    /// </summary>
    public bool HasXmlDocumentationRemarks => !string.IsNullOrWhiteSpace(this.XmlDocumentationRemarks);

    /// <summary>
    /// Gets the formatted XML documentation remarks for the property declaration.
    /// </summary>
    /// <remarks>
    /// If present, this may be embedded between &lt;remarks&gt; and &lt;/remarks&gt; elements in the generated code.
    /// </remarks>
    public string? XmlDocumentationRemarks { get; }

    /// <summary>
    /// Construct a copy with the specified <see cref="IsRequired"/> value.
    /// </summary>
    /// <param name="isRequired">Whether the property is required.</param>
    /// <returns>The new instance with isRequired set.</returns>
    public PropertyDeclaration WithRequired(bool isRequired)
    {
        return new PropertyDeclaration(this.Type, this.JsonPropertyName, isRequired, this.IsDefinedInLocalScope, this.HasDefaultValue, this.DefaultValue, this.XmlDocumentationRemarks, this.IsDeprecated);
    }

    /// <summary>
    /// Construct a copy with the specified <see cref="IsDeprecated"/> value.
    /// </summary>
    /// <param name="isDeprecated">Whether the property is deprecated.</param>
    /// <returns>The new instance with isRequired set.</returns>
    public PropertyDeclaration WithDeprecated(bool isDeprecated)
    {
        return new PropertyDeclaration(this.Type, this.JsonPropertyName, this.IsRequired, this.IsDefinedInLocalScope, this.HasDefaultValue, this.DefaultValue, this.XmlDocumentationRemarks, isDeprecated);
    }

    /// <summary>
    /// Construct a copy with the specified <see cref="XmlDocumentationRemarks"/> value.
    /// </summary>
    /// <param name="xmlDocumentationRemarks">Gets the formatted documentation for the property.</param>
    /// <returns>The new instance with isRequired set.</returns>
    public PropertyDeclaration WithXmlDocumentationRemarks(string? xmlDocumentationRemarks)
    {
        return new PropertyDeclaration(this.Type, this.JsonPropertyName, this.IsRequired, this.IsDefinedInLocalScope, this.HasDefaultValue, this.DefaultValue, xmlDocumentationRemarks, this.IsDeprecated);
    }
}