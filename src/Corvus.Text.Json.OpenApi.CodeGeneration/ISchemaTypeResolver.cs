// <copyright file="ISchemaTypeResolver.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Resolves a <see cref="SchemaRef"/> to the language-specific type name that the code
/// generator emits for that schema.
/// </summary>
/// <remarks>
/// <para>
/// The default C# implementation (<see cref="DefaultSchemaTypeResolver"/>) maps a schema
/// pointer to the fully qualified .NET type name produced by the JSON Schema model generator.
/// A future TypeScript emitter can supply its own implementation that returns the TypeScript
/// final name instead.
/// </para>
/// </remarks>
public interface ISchemaTypeResolver
{
    /// <summary>
    /// Resolves the type name for a schema.
    /// </summary>
    /// <param name="schema">
    /// The schema reference, or <see langword="null"/> when the location has no schema.
    /// </param>
    /// <returns>
    /// The language-specific type name for the schema, or the fallback type name when the
    /// reference is <see langword="null"/> or unknown.
    /// </returns>
    string ResolveTypeName(SchemaRef? schema);

    /// <summary>
    /// Resolves the element type name for the items of an array or stream schema.
    /// </summary>
    /// <param name="schema">
    /// The schema reference identifying the element schema, or <see langword="null"/> when
    /// the location has no schema.
    /// </param>
    /// <returns>
    /// The language-specific type name for the element schema, or the fallback type name when
    /// the reference is <see langword="null"/> or unknown.
    /// </returns>
    string ResolveElementTypeName(SchemaRef? schema);
}