// <copyright file="TypeDeclaration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.JsonSchema.Draft6;

namespace Corvus.Json.CodeGeneration.Draft6;

/// <summary>
/// A type declaration based on a schema.
/// </summary>
public class TypeDeclaration : TypeDeclaration<Schema, PropertyDeclaration, TypeDeclaration>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
    /// </summary>
    /// <param name="absoluteLocation">The canonical location of the type declaration.</param>
    /// <param name="lexicalLocation">The lexical location of the type declaration.</param>
    /// <param name="schema">The schema with which this type declaration is associated.</param>
    public TypeDeclaration(string absoluteLocation, string lexicalLocation, Schema schema)
        : base(absoluteLocation, lexicalLocation, schema)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class.
    /// </summary>
    /// <param name="location">The canonical location of the type declaration.</param>
    /// <param name="schema">The schema with which this type declaration is associated.</param>
    public TypeDeclaration(string location, Schema schema)
        : base(location, schema)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class for a built-in type.
    /// </summary>
    /// <param name="schema">The schema for the built-in type.</param>
    public TypeDeclaration(Schema schema)
        : base(schema)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeDeclaration"/> class for a built-in type.
    /// </summary>
    /// <param name="ns">The namespace for the built-in type.</param>
    /// <param name="typename">The typename for the built-in type.</param>
    public TypeDeclaration(string? ns, string typename)
        : base(ns, typename)
    {
    }

    /// <inheritdoc/>
    protected override bool SchemaIsBuiltInType => this.Schema.IsBuiltInType();

    /// <inheritdoc/>
    protected override string SchemaSimpleType => this.Schema.Type.AsSimpleTypesEntity;

    /// <inheritdoc/>
    protected override string? SchemaFormat => this.Schema.Format.AsOptionalString();

    /// <inheritdoc/>
    protected override bool SchemaIsEmpty => this.Schema.IsEmpty();

    /// <inheritdoc/>
    protected override bool SchemaIsTrue => this.Schema.ValueKind == System.Text.Json.JsonValueKind.True;

    /// <inheritdoc/>
    protected override bool SchemaIsFalse => this.Schema.ValueKind == System.Text.Json.JsonValueKind.False;

    /// <inheritdoc/>
    protected override string? SchemaContentEncoding => this.Schema.ContentEncoding.AsOptionalString();

    /// <inheritdoc/>
    protected override string? SchemaContentMediaType => this.Schema.ContentMediaType.AsOptionalString();

    /// <inheritdoc/>
    protected override bool SchemaHasEnum => this.Schema.Enum.IsNotUndefined();

    /// <inheritdoc/>
    protected override bool SchemaIsExplicitArrayType => this.Schema.IsExplicitArrayType();

    /// <inheritdoc/>
    protected override bool SchemaIsSimpleType => this.Schema.IsSimpleType();
}