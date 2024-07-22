// <copyright file="WellKnownNumericFormatProvider.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Helpers for well-known numeric formats.
/// </summary>
public class WellKnownNumericFormatProvider : INumberFormatProvider
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="WellKnownNumericFormatProvider"/>.
    /// </summary>
    public static WellKnownNumericFormatProvider Instance { get; } = new();

    /// <inheritdoc/>
    public string? GetDotnetTypeNameForCSharpNumericLangwordOrTypeName(string langword)
    {
        return langword switch
        {
            "byte" => "Byte",
            "decimal" => "Decimal",
            "double" => "Double",
            "short" => "Int16",
            "int" => "Int32",
            "long" => "Int64",
            "Int128" => "Int128",
            "sbyte" => "SByte",
            "Half" => "Half",
            "float" => "Single",
            "ushort" => "UInt16",
            "uint" => "UInt32",
            "ulong" => "UInt64",
            "UInt128" => "UInt128",
            _ => null,
        };
    }

    /// <inheritdoc/>
    public string? GetDotnetTypeNameFor(string format)
    {
        return this.GetIntegerDotnetTypeNameFor(format) ??
               this.GetFloatDotnetTypeNameFor(format);
    }

    /// <inheritdoc/>
    public string? GetIntegerDotnetTypeNameFor(string format)
    {
        return format switch
        {
            "byte" => "JsonByte",
            "uint16" => "JsonUInt16",
            "uint32" => "JsonUInt32",
            "uint64" => "JsonUInt64",
            "uint128" => "JsonUInt128",
            "sbyte" => "JsonSByte",
            "int16" => "JsonInt16",
            "int32" => "JsonInt32",
            "int64" => "JsonInt64",
            "int128" => "JsonInt128",
            _ => null,
        };
    }

    /// <inheritdoc/>
    public string? GetFloatDotnetTypeNameFor(string format)
    {
        return format switch
        {
            "half" => "JsonHalf",
            "single" => "JsonSingle",
            "double" => "JsonDouble",
            "decimal" => "JsonDecimal",
            _ => null,
        };
    }

    /// <inheritdoc/>
    public JsonValueKind? GetExpectedValueKind(string format)
    {
        if (this.GetDotnetTypeNameFor(format) is not null)
        {
            return JsonValueKind.Number;
        }

        return null;
    }
}