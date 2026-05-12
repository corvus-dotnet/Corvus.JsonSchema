// <copyright file="RequiredValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A required property validation handler.
/// </summary>
public class RequiredValidationHandler : IChildObjectPropertyValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="RequiredValidationHandler"/>.
    /// </summary>
    public static RequiredValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitRequiredProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            foreach (PropertyDeclaration property in properties)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (property.RequiredKeyword is not IObjectRequiredPropertyValidationKeyword keyword)
                {
                    continue;
                }

                Debug.Assert(property.Keyword is not null, "The property keyword must be set for required property validation.");

                string requiredName = GetHasSeenVariableName(generator, property);
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            SymbolDisplay.FormatLiteral(keyword.GetPathModifier(property), true),
                            "));")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!", requiredName, ")")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, property.Keyword, "result", g => AppendErrorText(g, property), false)
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ValidationContext.InvalidContext;")
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, property.Keyword, "result", g => AppendValidText(g, property), false)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;

        static void AppendErrorText(CodeGenerator generator, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("the required property '")
                .Append(SymbolDisplay.FormatLiteral(property.JsonPropertyName, true).Trim('"'))
                .Append("' was not present.");
        }

        static void AppendValidText(CodeGenerator generator, PropertyDeclaration property)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("the required property '")
                .Append(SymbolDisplay.FormatLiteral(property.JsonPropertyName, true).Trim('"'))
                .Append("' was present.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitRequiredProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            generator
                .AppendSeparatorLine();

            foreach (PropertyDeclaration property in properties)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                string requiredName = GetHasSeenVariableName(generator, property);
                generator
                    .AppendLineIndent("bool ", requiredName, " = false;");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <summary>
    /// Gets the required property hasSeen variable name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="property">The property declaration.</param>
    /// <returns>The required property hasSeen variable name.</returns>
    internal static string GetHasSeenVariableName(CodeGenerator generator, PropertyDeclaration property)
    {
        return generator.GetVariableNameInScope(property.DotnetPropertyName(), prefix: "hasSeen");
    }
}