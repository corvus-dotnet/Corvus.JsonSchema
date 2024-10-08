// <copyright file="Types.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Helpers for type handling.
/// </summary>
public static class Types
{
    /// <summary>
    /// Get the core types for a string-like type value.
    /// </summary>
    /// <param name="typeValue">The string-like type value.</param>
    /// <returns>The <see cref="CoreTypes"/> represented by the type value.</returns>
    public static CoreTypes GetCoreTypesFor(JsonElement typeValue)
    {
        Debug.Assert(typeValue.ValueKind == JsonValueKind.String, "The typeValue must be a string.");

        if (typeValue.ValueEquals("null"u8))
        {
            return CoreTypes.Null;
        }

        if (typeValue.ValueEquals("boolean"u8))
        {
            return CoreTypes.Boolean;
        }

        if (typeValue.ValueEquals("string"u8))
        {
            return CoreTypes.String;
        }

        if (typeValue.ValueEquals("array"u8))
        {
            return CoreTypes.Array;
        }

        if (typeValue.ValueEquals("object"u8))
        {
            return CoreTypes.Object;
        }

        if (typeValue.ValueEquals("number"u8))
        {
            return CoreTypes.Number;
        }

        if (typeValue.ValueEquals("integer"u8))
        {
            return CoreTypes.Integer;
        }

        return CoreTypes.None;
    }
}