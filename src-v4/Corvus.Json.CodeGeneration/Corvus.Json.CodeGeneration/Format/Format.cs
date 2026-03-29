// <copyright file="Format.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration;

/// <summary>
/// Gets the <see cref="CoreTypes"/> for a supported format.
/// </summary>
public static class Format
{
    /// <summary>
    /// Get the core types for the given format string.
    /// </summary>
    /// <param name="formatValue">The value of the format string.</param>
    /// <returns>The <see cref="CoreTypes"/> corresponding to the format string, or <see cref="CoreTypes.None"/> if none specified.</returns>
    public static CoreTypes GetCoreTypesFor(in JsonElement formatValue)
    {
        if (formatValue.ValueKind != JsonValueKind.String)
        {
            return CoreTypes.None;
        }

        if (formatValue.ValueEquals("byte"u8) ||
            formatValue.ValueEquals("uint16"u8) ||
            formatValue.ValueEquals("uint32"u8) ||
            formatValue.ValueEquals("uint64"u8) ||
            formatValue.ValueEquals("uint128"u8) ||
            formatValue.ValueEquals("sbyte"u8) ||
            formatValue.ValueEquals("int16"u8) ||
            formatValue.ValueEquals("int32"u8) ||
            formatValue.ValueEquals("int64"u8) ||
            formatValue.ValueEquals("int128"u8))
        {
            return CoreTypes.Integer;
        }

        if (formatValue.ValueEquals("half"u8) ||
            formatValue.ValueEquals("single"u8) ||
            formatValue.ValueEquals("double"u8) ||
            formatValue.ValueEquals("decimal"u8))
        {
            return CoreTypes.Number;
        }

        if (formatValue.ValueEquals("date"u8) ||
            formatValue.ValueEquals("date-time"u8) ||
            formatValue.ValueEquals("time"u8) ||
            formatValue.ValueEquals("duration"u8) ||
            formatValue.ValueEquals("email"u8) ||
            formatValue.ValueEquals("idn-email"u8) ||
            formatValue.ValueEquals("hostname"u8) ||
            formatValue.ValueEquals("idn-hostname"u8) ||
            formatValue.ValueEquals("ipv4"u8) ||
            formatValue.ValueEquals("ipv6"u8) ||
            formatValue.ValueEquals("uuid"u8) ||
            formatValue.ValueEquals("uri"u8) ||
            formatValue.ValueEquals("uri-template"u8) ||
            formatValue.ValueEquals("uri-reference"u8) ||
            formatValue.ValueEquals("iri"u8) ||
            formatValue.ValueEquals("iri-reference"u8) ||
            formatValue.ValueEquals("json-pointer"u8) ||
            formatValue.ValueEquals("relative-json-pointer"u8) ||
            formatValue.ValueEquals("regex"u8))
        {
            return CoreTypes.String;
        }

        return CoreTypes.None;
    }
}