// <copyright file="JsonSchemaScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Holds the information about a JsonSchema scope.
/// </summary>
/// <param name="Location">The root location of the scope.</param>
/// <param name="Pointer">The pointer to the current subschema in the scope.</param>
/// <param name="Schema">The schema associated with the root location of the scope.</param>
/// <param name="IsDynamicScope">Whether it is a dynamic scope.</param>
/// <param name="ReplacedDynamicTypes">The dynamic types that were replaced in the scope.</param>
public record struct JsonSchemaScope(JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope, ImmutableList<(JsonReference Location, TypeDeclaration Type)> ReplacedDynamicTypes)
{
    /// <summary>
    /// Tuple conversion operator.
    /// </summary>
    /// <param name="value">The <see cref="JsonSchemaScope"/> to convert to a tuple.</param>
    public static implicit operator (JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope, ImmutableList<(JsonReference Location, TypeDeclaration Type)> ReplacedDynamicTypes)(JsonSchemaScope value)
    {
        return (value.Location, value.Pointer, value.Schema, value.IsDynamicScope, value.ReplacedDynamicTypes);
    }

    /// <summary>
    /// Tuple conversion operator.
    /// </summary>
    /// <param name="value">The tuple to convert to the <see cref="JsonSchemaScope"/>.</param>
    public static implicit operator JsonSchemaScope((JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope, ImmutableList<(JsonReference Location, TypeDeclaration Type)> ReplacedDynamicTypes) value)
    {
        return new JsonSchemaScope(value.Location, value.Pointer, value.Schema, value.IsDynamicScope, value.ReplacedDynamicTypes);
    }
}