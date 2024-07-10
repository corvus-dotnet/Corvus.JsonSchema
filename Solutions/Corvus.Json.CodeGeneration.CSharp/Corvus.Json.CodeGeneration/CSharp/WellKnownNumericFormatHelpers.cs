// <copyright file="WellKnownNumericFormatHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Helpers for well-known numeric formats.
/// </summary>
public static class WellKnownNumericFormatHelpers
{
    /// <summary>
    /// Gets the .NET BCL type name for the given C# numeric langword or type.
    /// </summary>
    /// <param name="langword">The .NET numeric langword.</param>
    /// <returns>The JSON string form suffix (e.g. <see langword="long"/> becomes <c>Int64</c>.</returns>
    public static string GetDotnetTypeNameForCSharpLangwordOrTypeName(string langword)
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
            _ => langword,
        };
    }

    /// <summary>
    /// Gets the JSON type for the given numeric format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    public static string GetDotnetTypeNameFor(string format)
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
            "half" => "JsonHalf",
            "single" => "JsonSingle",
            "double" => "JsonDouble",
            "decimal" => "JsonDecimal",
            _ => "JsonNumber",
        };
    }

    /// <summary>
    /// Gets the JSON type for the given numeric format.
    /// </summary>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    public static string GetIntegerDotnetTypeNameFor(string format)
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
            _ => "JsonInteger",
        };
    }
}