// <copyright file="PropertyCountValidationHandler.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A property count validation handler.
/// </summary>
public class PropertyCountValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertyCountValidationHandler"/>.
    /// </summary>
    public static PropertyCountValidationHandler Instance { get; } = new();

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

        foreach (IPropertyCountConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IPropertyCountConstantValidationKeyword>())
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
                AppendStandardPropertyCountOperator(typeDeclaration, keyword, op);

            requiresShortCut = true;
        }

        return generator;
    }
}

public static class PropertyCountValidationExtensions
{
    public static CodeGenerator AppendStandardPropertyCountOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, IPropertyCountConstantValidationKeyword keyword, Operator op)
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
            Operator.Equals => "JsonSchemaEvaluation.MatchPropertyCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchPropertyCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchPropertyCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchPropertyCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchPropertyCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchPropertyCountGreaterThanOrEquals",
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent(
                operatorFunction, "(",
                expected, ",",
                "objectValidation_propertyCount, ",
                SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8, ref context);");
    }
}