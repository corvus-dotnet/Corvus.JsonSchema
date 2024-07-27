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
    public bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier)
    {
        switch (format)
        {
            case "byte":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeByte(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;

            case "uint16":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUInt16(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uint32":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUInt32(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uint64":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUInt64(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "uint128":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeUInt128(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "sbyte":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeSByte(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "int16":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeInt16(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "int32":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeInt32(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "int64":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeInt64(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "int128":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeInt128(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "half":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeHalf(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "single":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeSingle(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "double":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeDouble(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            case "decimal":
                generator.AppendLineIndent(
                    "return Corvus.Json.Validate.TypeDecimal(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level);");
                return true;
            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public string? GetTypeNameForNumericLangwordOrTypeName(string langword)
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
    public string? GetCorvusJsonTypeNameFor(string format)
    {
        return this.GetIntegerCorvusJsonTypeNameFor(format) ??
               this.GetFloatCorvusJsonTypeNameFor(format);
    }

    /// <inheritdoc/>
    public string? GetIntegerCorvusJsonTypeNameFor(string format)
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
    public string? GetFloatCorvusJsonTypeNameFor(string format)
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
        if (this.GetCorvusJsonTypeNameFor(format) is not null)
        {
            return JsonValueKind.Number;
        }

        return null;
    }
}