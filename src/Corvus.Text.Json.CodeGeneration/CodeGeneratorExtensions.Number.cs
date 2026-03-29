// <copyright file="CodeGeneratorExtensions.Number.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.Internal;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions providing general code generation utilities.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <inheritdoc/>
    public static CodeGenerator AppendNumericConstant(this CodeGenerator generator, string baseName, JsonElement constantValue)
    {
        Debug.Assert(constantValue.ValueKind == JsonValueKind.Number);

        string isNegativeField = generator.GetUniqueStaticReadOnlyFieldNameInScope(baseName, suffix: "IsNegative");
        string integralProperty = generator.GetUniqueStaticReadOnlyPropertyNameInScope(baseName, suffix: "Integral");
        string fractionalProperty = generator.GetUniqueStaticReadOnlyPropertyNameInScope(baseName, suffix: "Fractional");
        string exponentField = generator.GetUniqueStaticReadOnlyFieldNameInScope(baseName, suffix: "Exponent");

        // Get the normalized JSON number for the constant
#if BUILDING_SOURCE_GENERATOR
        ReadOnlySpan<byte> number = Encoding.UTF8.GetBytes(constantValue.GetRawText());
#else
        ReadOnlySpan<byte> number = JsonMarshal.GetRawUtf8Value(constantValue);
#endif

        JsonElementHelpers.ParseNumber(number, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        string normalizedNumberProperty = generator.GetStaticReadOnlyPropertyNameInScope(baseName);
        generator.AppendLineIndent("private const bool ", isNegativeField, " = ", isNegative ? "true" : "false", ";");
        generator.AppendLineIndent("private static ", integralProperty, " => \"", Formatting.GetTextFromUtf8(integral), "\"u8;");
        generator.AppendLineIndent("private static ", fractionalProperty, " => \"", Formatting.GetTextFromUtf8(fractional), "\"u8;");
        generator.AppendLineIndent("private const int ", exponentField, " = ", exponent.ToString(), ";");

        generator.AppendLineIndent("private static NormalizedJsonNumber", normalizedNumberProperty, " => new NormalizedJsonNumber(", isNegativeField, ", ", integralProperty, ", ", fractionalProperty, ", ", exponentField, ");");

        return generator;
    }

    /// <summary>
    /// Appends the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the operator.</param>
    /// <param name="op">The operator to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendOperator(this CodeGenerator generator, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        switch (op)
        {
            case Operator.Equals:
                return generator.Append("==");
            case Operator.NotEquals:
                return generator.Append("!=");
            case Operator.LessThan:
                return generator.Append("<");
            case Operator.LessThanOrEquals:
                return generator.Append("<=");
            case Operator.GreaterThan:
                return generator.Append(">");
            case Operator.GreaterThanOrEquals:
                return generator.Append(">=");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }
}