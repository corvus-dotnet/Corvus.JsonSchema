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
        if (typeDeclaration.ExplicitRequiredProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            foreach (PropertyDeclaration property in properties)
            {
                // Don't bother with properties that have default values.
                if (property.ReducedPropertyType.HasDefaultValue())
                {
                    continue;
                }

                Debug.Assert(property.Keyword is not null, "The property keyword must be set for required property validation.");

                string requiredName = GetRequiredName(generator, property);
                generator
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
                            .AppendLineIndent("return result.WithResult(isValid: false);")
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;

        static void AppendErrorText(CodeGenerator generator, PropertyDeclaration property)
        {
            generator
                .Append("the required property '")
                .Append(SymbolDisplay.FormatLiteral(property.JsonPropertyName, true).Trim('"'))
                .Append("' was not present.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ExplicitRequiredProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            generator
                .AppendSeparatorLine();

            foreach (PropertyDeclaration property in properties)
            {
                // Don't bother with properties that have default values.
                if (property.ReducedPropertyType.HasDefaultValue())
                {
                    continue;
                }

                string requiredName = GetRequiredName(generator, property);
                generator
                    .AppendLineIndent("bool ", requiredName, " = false;");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ExplicitRequiredProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            generator
                .AppendSeparatorLine();

            bool first = true;
            foreach (PropertyDeclaration property in properties)
            {
                // As above, skip the properties that have default values.
                if (property.ReducedPropertyType.HasDefaultValue())
                {
                    continue;
                }

                if (first)
                {
                    first = false;
                    generator.AppendIndent(string.Empty);
                }
                else
                {
                    generator.AppendIndent("else ");
                }

                string requiredName = GetRequiredName(generator, property);

                generator
                    .AppendLine(
                        "if ((value.HasJsonElementBacking && property.NameEquals(",
                        generator.JsonPropertyNamesClassName(),
                        ".",
                        property.DotnetPropertyName(),
                        "Utf8)) ||")
                    .PushIndent()
                    .AppendLineIndent(
                        "(!value.HasJsonElementBacking && property.NameEquals(",
                        generator.JsonPropertyNamesClassName(),
                        ".",
                        property.DotnetPropertyName(),
                        ")))")
                    .PopIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                    .AppendLineIndent(requiredName, " = true;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    private static string GetRequiredName(CodeGenerator generator, PropertyDeclaration property)
    {
        return generator.GetVariableNameInScope(property.DotnetPropertyName(), prefix: "hasSeen");
    }
}