// <copyright file="JsonSchemaScope.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Holds the information about a JsonSchema scope.
/// </summary>
/// <param name="Location">The root location of the scope.</param>
/// <param name="Pointer">The pointer to the current subschema in the scope.</param>
/// <param name="Schema">The schema associated with the root location of the scope.</param>
/// <param name="IsDynamicScope">Whether it is a dynamic scope.</param>
public record struct JsonSchemaScope(JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope)
{
    /// <summary>
    /// Tuple conversion operator.
    /// </summary>
    /// <param name="value">The <see cref="JsonSchemaScope"/> to convert to a tuple.</param>
    public static implicit operator (JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope)(JsonSchemaScope value)
    {
        return (value.Location, value.Pointer, value.Schema, value.IsDynamicScope);
    }

    /// <summary>
    /// Tuple conversion operator.
    /// </summary>
    /// <param name="value">The tuple to convert to the <see cref="JsonSchemaScope"/>.</param>
    public static implicit operator JsonSchemaScope((JsonReference Location, JsonReference Pointer, LocatedSchema Schema, bool IsDynamicScope) value)
    {
        return new JsonSchemaScope(value.Location, value.Pointer, value.Schema, value.IsDynamicScope);
    }
}