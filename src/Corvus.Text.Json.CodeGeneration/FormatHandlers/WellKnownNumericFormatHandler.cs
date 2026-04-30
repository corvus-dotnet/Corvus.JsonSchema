// <copyright file="WellKnownNumericFormatHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

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
    public bool AppendFormatAssertion(
        CodeGenerator generator,
        string format,
        string formatKeywordProviderExpression,
        string isNegativeIdentifier,
        string integralIdentifier,
        string fractionalIdentifier,
        string exponentIdentifier,
        string validationContextIdentifier)
    {
        switch (format)
        {
            case "byte":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchByte(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "uint16":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt16(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "uint32":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt32(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "uint64":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt64(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "uint128":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt128(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "sbyte":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchSByte(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "int16":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchInt16(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "int32":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchInt32(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "int64":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt64(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "int128":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchUInt128(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "half":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchHalf(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "single":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchSingle(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "double":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchDouble(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            case "decimal":
                generator.AppendIndent(
                    "JsonSchemaEvaluation.MatchDecimal(",
                    isNegativeIdentifier, ", ",
                    integralIdentifier, ", ",
                    fractionalIdentifier, ", ",
                    exponentIdentifier, ", ",
                    formatKeywordProviderExpression, ", ",
                    "ref ", validationContextIdentifier, ")");
                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public JsonTokenType? GetExpectedTokenType(string format)
    {
        if (IsIntegerFormat(format) || IsFloatingPointFormat(format))
        {
            return JsonTokenType.Number;
        }

        return null;
    }

    /// <inheritdoc/>
    public bool AppendFormatSourceConstructors(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConstructorParameters)
    {
        switch (format)
        {
            case "byte":
                if (seenConstructorParameters.Add("byte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(byte value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "uint16":
                if (seenConstructorParameters.Add("ushort"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(ushort value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "uint32":
                if (seenConstructorParameters.Add("uint"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(uint value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "uint64":
                if (seenConstructorParameters.Add("ulong"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(ulong value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "uint128":
                if (seenConstructorParameters.Add("UInt128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("private Source(UInt128 value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written)); _kind = Kind.NumericSimpleType; }")
                        .AppendLine("#endif");
                }

                return true;

            case "sbyte":
                if (seenConstructorParameters.Add("sbyte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(sbyte value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "int16":
                if (seenConstructorParameters.Add("short"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(short value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "int32":
                if (seenConstructorParameters.Add("int"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(int value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "int64":
                if (seenConstructorParameters.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(long value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "int128":
                if (seenConstructorParameters.Add("Int128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("private Source(Int128 value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written)); _kind = Kind.NumericSimpleType; }")
                        .AppendLine("#endif");
                }

                return true;

            case "half":
                if (seenConstructorParameters.Add("Half"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("private Source(Half value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => v.TryFormat(buffer, out written)); _kind = Kind.NumericSimpleType; }")
                        .AppendLine("#endif");
                }

                return true;

            case "single":
                if (seenConstructorParameters.Add("float"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(float value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "double":
                if (seenConstructorParameters.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(double value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            case "decimal":
                if (seenConstructorParameters.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private Source(decimal value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (v, buffer, out written) => Utf8Formatter.TryFormat(v, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                }

                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    private static bool IsIntegerFormat(string format)
    {
        return format switch
        {
            "byte" => true,
            "uint16" => true,
            "uint32" => true,
            "uint64" => true,
            "uint128" => true,
            "sbyte" => true,
            "int16" => true,
            "int32" => true,
            "int64" => true,
            "int128" => true,
            _ => false,
        };
    }

    /// <inheritdoc/>
    private static bool IsFloatingPointFormat(string format)
    {
        return format switch
        {
            "half" => true,
            "single" => true,
            "double" => true,
            "decimal" => true,
            _ => false,
        };
    }

    /// <inheritdoc/>
    public bool TryGetNumericTypeName(string format, [NotNullWhen(true)] out string? typeName, out bool isNetOnly, out string? netStandardFallback)
    {
        switch (format)
        {
            case "double":
                typeName = "double";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "decimal":
                typeName = "decimal";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "half":
                typeName = "Half";
                netStandardFallback = "double";
                isNetOnly = true;
                return true;

            case "single":
                typeName = "float";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "byte":
                typeName = "byte";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "int16":
                typeName = "short";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "int32":
                typeName = "int";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "int64":
                typeName = "long";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "int128":
                typeName = "Int128";
                netStandardFallback = "long";
                isNetOnly = true;
                return true;

            case "sbyte":
                typeName = "sbyte";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "uint16":
                typeName = "ushort";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "uint32":
                typeName = "uint";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "uint64":
                typeName = "ulong";
                netStandardFallback = null;
                isNetOnly = false;
                return true;

            case "uint128":
                typeName = "UInt128";
                netStandardFallback = "ulong";
                isNetOnly = true;
                return true;

            default:
                typeName = null;
                netStandardFallback = null;
                isNetOnly = false;
                return false;
        }

        ;
    }

    /// <summary>
    /// Appends format-specific source conversion operators to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <param name="seenConversionOperators">The set of conversion operators that have already been generated.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public bool AppendFormatSourceConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators)
    {
        switch (format)
        {
            case "byte":
                if (seenConversionOperators.Add("byte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(byte value) => new (value);");
                }

                return true;

            case "uint16":
                if (seenConversionOperators.Add("ushort"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(ushort value) => new (value);");
                }

                return true;

            case "uint32":
                if (seenConversionOperators.Add("uint"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(uint value) => new (value);");
                }

                return true;

            case "uint64":
                if (seenConversionOperators.Add("ulong"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(ulong value) => new (value);");
                }

                return true;

            case "uint128":
                if (seenConversionOperators.Add("UInt128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(UInt128 value) => new (value);")
                        .AppendLine("#endif");
                }

                return true;

            case "sbyte":
                if (seenConversionOperators.Add("sbyte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(sbyte value) => new (value);");
                }

                return true;

            case "int16":
                if (seenConversionOperators.Add("short"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(short value) => new (value);");
                }

                return true;

            case "int32":
                if (seenConversionOperators.Add("int"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(int value) => new (value);");
                }

                return true;

            case "int64":
                if (seenConversionOperators.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(long value) => new (value);");
                }

                return true;

            case "int128":
                if (seenConversionOperators.Add("Int128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(Int128 value) => new (value);")
                        .AppendLine("#endif");
                }

                return true;

            case "half":
                if (seenConversionOperators.Add("Half"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(Half value) => new (value);")
                        .AppendLine("#endif");
                }

                return true;

            case "single":
                if (seenConversionOperators.Add("float"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(float value) => new (value);");
                }

                return true;

            case "double":
                if (seenConversionOperators.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(double value) => new (value);");
                }

                return true;

            case "decimal":
                if (seenConversionOperators.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static implicit operator Source(decimal value) => new (value);");
                }

                return true;

            default:
                return false;
        }
    }

    public bool AppendFormatConversionOperators(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators, bool forMutable, bool useExplicit = false)
    {
        string typeName = forMutable ? "Mutable" : typeDeclaration.DotnetTypeName();
        string operatorKind = useExplicit ? "explicit" : "implicit";

        switch (format)
        {
            case "byte":
                if (seenConversionOperators.Add("byte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator byte(", typeName, " value) => value._parent.TryGetValue(value._idx, out byte result) ? result : throw new FormatException();");
                }

                return true;

            case "uint16":
                if (seenConversionOperators.Add("ushort"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator ushort(", typeName, " value) => value._parent.TryGetValue(value._idx, out ushort result) ? result : throw new FormatException();");
                }

                return true;

            case "uint32":
                if (seenConversionOperators.Add("uint"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator uint(", typeName, " value) => value._parent.TryGetValue(value._idx, out uint result) ? result : throw new FormatException();");
                }

                return true;

            case "uint64":
                if (seenConversionOperators.Add("ulong"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator ulong(", typeName, " value) => value._parent.TryGetValue(value._idx, out ulong result) ? result : throw new FormatException();");
                }

                return true;

            case "uint128":
                if (seenConversionOperators.Add("UInt128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator UInt128(", typeName, " value) => value._parent.TryGetValue(value._idx, out UInt128 result) ? result : throw new FormatException();")
                        .AppendLine("#endif");
                }

                return true;

            case "sbyte":
                if (seenConversionOperators.Add("sbyte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator sbyte(", typeName, " value) => value._parent.TryGetValue(value._idx, out sbyte result) ? result : throw new FormatException();");
                }

                return true;

            case "int16":
                if (seenConversionOperators.Add("short"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator short(", typeName, " value) => value._parent.TryGetValue(value._idx, out short result) ? result : throw new FormatException();");
                }

                return true;

            case "int32":
                if (seenConversionOperators.Add("int"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator int(", typeName, " value) => value._parent.TryGetValue(value._idx, out int result) ? result : throw new FormatException();");
                }

                return true;

            case "int64":
                if (seenConversionOperators.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator long(", typeName, " value) => value._parent.TryGetValue(value._idx, out long result) ? result : throw new FormatException();");
                }

                return true;

            case "int128":
                if (seenConversionOperators.Add("Int128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator Int128(", typeName, " value) => value._parent.TryGetValue(value._idx, out Int128 result) ? result : throw new FormatException();")
                        .AppendLine("#endif");
                }

                return true;

            case "half":
                if (seenConversionOperators.Add("Half"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator Half(", typeName, " value) => value._parent.TryGetValue(value._idx, out Half result) ? result : throw new FormatException();")
                        .AppendLine("#endif");
                }

                return true;

            case "single":
                if (seenConversionOperators.Add("float"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator float(", typeName, " value) => value._parent.TryGetValue(value._idx, out float result) ? result : throw new FormatException();");
                }

                return true;

            case "double":
                if (seenConversionOperators.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator double(", typeName, " value) => value._parent.TryGetValue(value._idx, out double result) ? result : throw new FormatException();");
                }

                return true;

            case "decimal":
                if (seenConversionOperators.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static ", operatorKind, " operator decimal(", typeName, " value) => value._parent.TryGetValue(value._idx, out decimal result) ? result : throw new FormatException();");
                }

                return true;

            default:
                return false;
        }
    }

    public bool AppendFormatValueGetters(CodeGenerator generator, TypeDeclaration typeDeclaration, string format, HashSet<string> seenConversionOperators)
    {
        switch (format)
        {
            case "byte":
                if (seenConversionOperators.Add("byte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out byte value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "uint16":
                if (seenConversionOperators.Add("ushort"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out ushort value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "uint32":
                if (seenConversionOperators.Add("uint"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out uint value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "uint64":
                if (seenConversionOperators.Add("ulong"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out ulong value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "uint128":
                if (seenConversionOperators.Add("UInt128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out UInt128 value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                return true;

            case "sbyte":
                if (seenConversionOperators.Add("sbyte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out sbyte value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "int16":
                if (seenConversionOperators.Add("short"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out short value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "int32":
                if (seenConversionOperators.Add("int"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out int value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "int64":
                if (seenConversionOperators.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out long value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "int128":
                if (seenConversionOperators.Add("Int128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Int128 value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                return true;

            case "half":
                if (seenConversionOperators.Add("Half"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Half value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                return true;

            case "single":
                if (seenConversionOperators.Add("float"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out float value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "double":
                if (seenConversionOperators.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out double value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            case "decimal":
                if (seenConversionOperators.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out decimal value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                return true;

            default:
                return false;
        }
    }

    /// <inheritdoc/>
    public bool TryGetSimpleTypeNameSuffix(string format, [NotNullWhen(true)] out string? suffix)
    {
        suffix = format switch
        {
            "byte" => "Byte",
            "sbyte" => "SByte",
            "int16" => "Int16",
            "int32" => "Int32",
            "int64" => "Int64",
            "int128" => "Int128",
            "uint16" => "UInt16",
            "uint32" => "UInt32",
            "uint64" => "UInt64",
            "uint128" => "UInt128",
            "half" => "Half",
            "single" => "Single",
            "double" => "Double",
            "decimal" => "Decimal",
            _ => null,
        };

        return suffix is not null;
    }
}