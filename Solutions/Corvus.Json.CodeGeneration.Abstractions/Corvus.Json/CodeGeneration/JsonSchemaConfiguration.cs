// <copyright file="JsonSchemaConfiguration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Configuration for a json schema dialect.
/// </summary>
public class JsonSchemaConfiguration
{
    /// <summary>
    /// Gets or sets the ID keyword for the schema model.
    /// </summary>
    public string IdKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the items keyword for the schema model.
    /// </summary>
    public string ItemsKeyword { get; set; } = string.Empty;

    /// <summary>
    /// Gets the (required, non-dynamic) reference keyword for the schema model.
    /// </summary>
    public string RefKeyword => this.RefKeywords.Single(k => k.RefKind == RefKind.Ref).Name;

    /// <summary>
    /// Gets or sets the schema keyword for the schema model.
    /// </summary>
    public string SchemaKeyword { get; set; } = "$schema"; // We default to $schema so we can inspect a json-schema document and figure it out in the "we are not yet set" case

    /// <summary>
    /// Gets or sets the reference keywords for the schema model.
    /// </summary>
    public ImmutableArray<RefKeyword> RefKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the anchor keywords for the schema model.
    /// </summary>
    public ImmutableArray<AnchorKeyword> AnchorKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the ref-resolvable keywords for the schema model.
    /// </summary>
    public ImmutableArray<RefResolvableKeyword> RefResolvableKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of non-reducing keywords for the schema model.
    /// </summary>
    /// <remarks>
    /// These are the keywords that, if placed alongside a reference keyword, prevent the local type from being reduced to the referenced type.
    /// </remarks>
    public ImmutableHashSet<string> IrreducibleKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of definition keywords for the schema model.
    /// </summary>
    /// <remarks>
    /// These are the keywords that, while they are ref resolvable, do not contribute directly to a type declaration.
    /// </remarks>
    public ImmutableHashSet<string> DefinitionKeywords { get; set; } = [];

    /// <summary>
    /// Gets or sets the list of words reserved by the generator, which may not be used as names.
    /// </summary>
    public ImmutableHashSet<string> GeneratorReservedWords { get; set; } = [];

    /// <summary>
    /// Gets or sets the validation semantic model to use.
    /// </summary>
    public ValidationSemantics ValidatingAs { get; set; } = ValidationSemantics.Unknown;

    /// <summary>
    /// Gets or sets a predicate that validates the schema.
    /// </summary>
    public Predicate<JsonAny> ValidateSchema { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a predicate that indicates whether the given schema is an explicit array type.
    /// </summary>
    public Predicate<JsonAny> IsExplicitArrayType { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a predicate that indicates whether the given schema is a simple type.
    /// </summary>
    public Predicate<JsonAny> IsSimpleType { get; set; } = static _ => false;

    /// <summary>
    /// Gets or sets a function to get the built-in type name for a schema with particular validation semantics.
    /// </summary>
    public Func<JsonAny, ValidationSemantics, (string Ns, string TypeName)?> GetBuiltInTypeName { get; set; } = static (_, _) => null;

    /// <summary>
    /// Gets or sets a function to build dotnet properties for given type declaration.
    /// </summary>
    public Action<IPropertyBuilder, TypeDeclaration, TypeDeclaration, HashSet<TypeDeclaration>, bool> FindAndBuildPropertiesAdapter { get; set; } = static (_, _, _, _, _) => { };
}