// <copyright file="WellKnownNumericFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Handlers for well-known numeric formats.
/// </summary>
public class WellKnownNumericFormatHandler : INumberFormatHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="WellKnownNumericFormatHandler"/>.
    /// </summary>
    public static WellKnownNumericFormatHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint Priority => 100_000;

    /// <inheritdoc/>
    public bool AppendFormatAssertion(CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier, bool includeType, IKeyword? typeKeyword, IKeyword? formatKeyword, bool returnFromMethod)
    {
        string validator = includeType ? "Validate" : "ValidateWithoutCoreType";

        string typeKeywordDisplay = typeKeyword is IKeyword t ? SymbolDisplay.FormatLiteral(t.Keyword, true) : "null";
        string formatKeywordDisplay = formatKeyword is IKeyword f ? SymbolDisplay.FormatLiteral(f.Keyword, true) : "null";

        string keywordParameters =
            includeType
                ? $"{typeKeywordDisplay}, {formatKeywordDisplay}"
                : formatKeywordDisplay;

        switch (format)
        {
            case "byte":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeByte(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;

            case "uint16":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeUInt16(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "uint32":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeUInt32(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "uint64":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeUInt64(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "uint128":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeUInt128(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "sbyte":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeSByte(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "int16":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeInt16(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "int32":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeInt32(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "int64":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeInt64(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "int128":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeInt128(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "half":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeHalf(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "single":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeSingle(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "double":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeDouble(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
                return true;
            case "decimal":
                generator.AppendLineIndent(
                    returnFromMethod ? "return" : validationContextIdentifier,
                    returnFromMethod ? string.Empty : " = ",
                    " Corvus.Json.",
                    validator,
                    ".TypeDecimal(",
                    valueIdentifier,
                    ", ",
                    validationContextIdentifier,
                    ", level, ",
                    keywordParameters,
                    ");");
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

    /// <inheritdoc/>
    public bool AppendFormatConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicStaticProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPublicMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPrivateStaticMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatPrivateMethods(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatEqualsTBody(CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
    {
        return false;
    }

    /// <inheritdoc/>
    public bool AppendFormatConstant(CodeGenerator generator, ITypedValidationConstantProviderKeyword keyword, string format, string fieldName, JsonElement constantValue)
    {
        return false;
    }
}