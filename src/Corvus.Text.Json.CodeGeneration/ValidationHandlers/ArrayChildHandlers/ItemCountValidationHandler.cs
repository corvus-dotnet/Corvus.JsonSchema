// <copyright file="ItemCountValidationHandler.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ArrayChildHandlers;

/// <summary>
/// An array item count validation handler.
/// </summary>
public class ItemCountValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="ItemCountValidationHandler"/>.
    /// </summary>
    public static ItemCountValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 1; // We want to go immediately after the higher priority items as we are very cheap

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool requiresShortCut = false;

        foreach (IArrayLengthConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayLengthConstantValidationKeyword>())
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
                AppendStandardItemCountOperator(typeDeclaration, keyword, op);

            requiresShortCut = true;
        }

        return generator;
    }
}

public static class ItemCountValidationExtensions
{
    public static CodeGenerator AppendStandardItemCountOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, IArrayLengthConstantValidationKeyword keyword, Operator op)
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
            Operator.Equals => "JsonSchemaEvaluation.MatchItemCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchItemCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchItemCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchItemCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchItemCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchItemCountGreaterThanOrEquals",
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent(
                operatorFunction, "(",
                expected,
                ", arrayValidation_itemCount, ",
                SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
    }
}