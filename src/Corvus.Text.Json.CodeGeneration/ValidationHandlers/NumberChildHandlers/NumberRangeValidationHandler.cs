// <copyright file="NumberRangeValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

#if !NET
using System.Collections.Generic;
#endif
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.Internal;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.NumberChildHandlers;

/// <summary>
/// A number range validation handler.
/// </summary>
public class NumberRangeValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="NumberRangeValidationHandler"/>.
    /// </summary>
    public static NumberRangeValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool requiresShortCut = false;

        foreach (INumberConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<INumberConstantValidationKeyword>())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (requiresShortCut)
            {
                generator
                    .AppendNoCollectorNoMatchShortcutReturn();
            }

            if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
            {
                continue;
            }

            if (op == Operator.MultipleOf)
            {
                generator
                    .AppendMultipleOf(typeDeclaration, keyword);
            }
            else
            {
                generator
                    .AppendStandardOperator(typeDeclaration, keyword, op);
            }

            requiresShortCut = true;
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // Not expected to be called
        throw new InvalidOperationException();
    }
}

public static class NumberValidationExtensions
{
    public static CodeGenerator AppendStandardOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, INumberConstantValidationKeyword keyword, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[] constants))
        {
            throw new InvalidOperationException("Unable to get validation constants for keyword.");
        }

        Debug.Assert(constants.Length == 1, "Expected exactly one validation constant for keyword.");

#if BUILDING_SOURCE_GENERATOR
        ReadOnlySpan<byte> rawValue = Encoding.UTF8.GetBytes(constants[0].GetRawText());
#else
        ReadOnlySpan<byte> rawValue = JsonMarshal.GetRawUtf8Value(constants[0]);
#endif

        JsonElementHelpers.ParseNumber(rawValue, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        string isNegativeString = isNegative ? "true" : "false";
        string integralString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(integral), true);
        string fractionalString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(fractional), true);
        string exponentString = exponent.ToString();
        string rawValueString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(rawValue), true);
        string operatorFunction = op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchGreaterThanOrEquals",
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

        return generator
            .AppendSeparatorLine()
            .AppendNormalizedJsonNumberIfNotAppended(typeDeclaration, false)
            .AppendLineIndent(
                operatorFunction,
                "(isNegative, integral, fractional, exponent, ",
                isNegativeString, ", ",
                integralString, "u8, ",
                fractionalString, "u8, ",
                exponentString, ", ",
                rawValueString, ", ",
                SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
    }

    public static CodeGenerator AppendMultipleOf(this CodeGenerator generator, TypeDeclaration typeDeclaration, INumberConstantValidationKeyword keyword)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[] constants))
        {
            throw new InvalidOperationException("Unable to get validation constants for multipleOf-type keyword.");
        }

        Debug.Assert(constants.Length == 1, "Expected exactly one validation constant for multipleOf-type keyword.");

#if BUILDING_SOURCE_GENERATOR
        ReadOnlySpan<byte> rawValue = Encoding.UTF8.GetBytes(constants[0].GetRawText());
#else
        ReadOnlySpan<byte> rawValue = JsonMarshal.GetRawUtf8Value(constants[0]);
#endif
        JsonElementHelpers.ParseNumber(rawValue, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        string divisor = $"{Formatting.GetTextFromUtf8(integral)}{Formatting.GetTextFromUtf8(fractional)}";
        string divisorValue = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(rawValue), true);

        // Does the integral/fractional part represent a value that can be validated as a UInt64 without loss of precision?
        if (IsUInt64(isNegative, integral, fractional, 0))
        {
            return generator
                .AppendSeparatorLine()
                .AppendNormalizedJsonNumberIfNotAppended(typeDeclaration, false)
                .AppendLineIndent("JsonSchemaEvaluation.MatchMultipleOf(integral, fractional, exponent, ", divisor, ", ", exponent.ToString(), ", ", divisorValue, ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
        }
        else
        {
            // We pay the allocations if you've forced a BigInteger divisor.
            return generator
                .AppendSeparatorLine()
                .AppendNormalizedJsonNumberIfNotAppended(typeDeclaration, false)
                .AppendLineIndent("JsonSchemaEvaluation.MatchMultipleOf(integral, fractional, exponent, BigInteger.Parse(\"", divisor, "\"), ", exponent.ToString(), ", ", divisorValue, ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
        }
    }

    /// <summary>
    /// Determines if a normalized JSON Number fits in a UInt64.
    /// </summary>
    /// <param name="isNegative">Indicates whether the number is negative.</param>
    /// <param name="integral">The integral part of the number.</param>
    /// <param name="fractional">The fractional part of the number.</param>
    /// <param name="exponent">The exponent of the number.</param>
    /// <param name="keyword">The keyword being evaluated.</param>
    /// <param name="context">The schema validation context.</param>
    /// <returns><see langword="true"/> if the number is a valid UInt64; otherwise, <see langword="false"/>.</returns>
    private static bool IsUInt64(bool isNegative, ReadOnlySpan<byte> integral, ReadOnlySpan<byte> fractional, int exponent)
    {
        const int MaximumUInt64Exponent = 0;
        const bool MaximumUInt64IsNegative = false;
        const int MinimumUInt64Exponent = 0;
        const bool MinimumUInt64IsNegative = false;

        ReadOnlySpan<byte> maximumUInt64Fractional = ""u8;
        ReadOnlySpan<byte> maximumUInt64Integral = "18446744073709551615"u8;
        ReadOnlySpan<byte> minimumUInt64Fractional = ""u8;
        ReadOnlySpan<byte> minimumUInt64Integral = ""u8;

        if (exponent != 0)
        {
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MinimumUInt64IsNegative,
            minimumUInt64Integral,
            minimumUInt64Fractional,
            MinimumUInt64Exponent) < 0)
        {
            return false;
        }

        if (JsonElementHelpers.CompareNormalizedJsonNumbers(
            isNegative,
            integral,
            fractional,
            exponent,
            MaximumUInt64IsNegative,
            maximumUInt64Integral,
            maximumUInt64Fractional,
            MaximumUInt64Exponent) > 0)
        {
            return false;
        }

        return true;
    }
}