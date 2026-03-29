// <copyright file="StringLengthValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.StringChildHandlers;

/// <summary>
/// A string length validation handler.
/// </summary>
public class StringLengthValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="StringLengthValidationHandler"/>.
    /// </summary>
    public static StringLengthValidationHandler Instance { get; } = new();

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

        foreach (IStringLengthConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IStringLengthConstantValidationKeyword>())
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

            generator.
                AppendStandardStringLengthOperator(typeDeclaration, keyword, op);

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

public static class StringLengthValidationExtensions
{
    public static CodeGenerator AppendStandardStringLengthOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, IStringLengthConstantValidationKeyword keyword, Operator op)
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

        int rawValue = (int)constants[0].GetDecimal();

        string expected = rawValue.ToString();
        string operatorFunction = op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchLengthEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchLengthNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchLengthLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchLengthLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchLengthGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchLengthGreaterThanOrEquals",
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

        return generator
            .AppendSeparatorLine()
            .AppendStringLengthIfNotAppended(typeDeclaration, false)
            .AppendLineIndent(
                operatorFunction, "(",
                expected, ",",
                "stringLength, ",
                SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
    }
}