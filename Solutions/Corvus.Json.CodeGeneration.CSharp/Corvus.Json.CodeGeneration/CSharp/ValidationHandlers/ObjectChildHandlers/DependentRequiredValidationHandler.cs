// <copyright file="DependentRequiredValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A dependent required property validation handler.
/// </summary>
public class DependentRequiredValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="DependentRequiredValidationHandler"/>.
    /// </summary>
    public static DependentRequiredValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.DependentRequired() is IReadOnlyDictionary<IObjectDependentRequiredValidationKeyword, IReadOnlyCollection<DependentRequiredDeclaration>> properties)
        {
            foreach (IObjectDependentRequiredValidationKeyword keyword in properties.Keys)
            {
                foreach (DependentRequiredDeclaration dependentRequired in properties[keyword])
                {
                    string propertyNameAsQuotedString = SymbolDisplay.FormatLiteral(dependentRequired.JsonPropertyName, true);

                    generator
                        .AppendLineIndent("if (value.HasJsonElementBacking)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (value.HasProperty(", propertyNameAsQuotedString, "u8))")
                            .AppendLineIndent("{")
                            .PushIndent();

                    int i = 0;
                    foreach (string dependency in dependentRequired.Dependencies)
                    {
                        string quotedDependency = SymbolDisplay.FormatLiteral(dependency, true);
                        string quotedPathModifier = SymbolDisplay.FormatLiteral(keyword.GetPathModifier(dependentRequired, i), true);
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (!value.HasProperty(", quotedDependency, "u8))");

                        AppendBody(generator, keyword, propertyNameAsQuotedString, quotedDependency, quotedPathModifier);

                        i++;
                    }

                    generator
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (value.HasProperty(", propertyNameAsQuotedString, "))")
                            .AppendLineIndent("{")
                            .PushIndent();

                    i = 0;
                    foreach (string dependency in dependentRequired.Dependencies)
                    {
                        string quotedDependency = SymbolDisplay.FormatLiteral(dependency, true);
                        string quotedPathModifier = SymbolDisplay.FormatLiteral(keyword.GetPathModifier(dependentRequired, i), true);
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (!value.HasProperty(", quotedDependency, "))");

                        AppendBody(generator, keyword, propertyNameAsQuotedString, quotedDependency, quotedPathModifier);

                        i++;
                    }

                    generator
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }
        }

        return generator;

        static void AppendErrorText(CodeGenerator generator, string quotedPropertyName, string quotedDependency)
        {
            generator
                .Append("the property '")
                .Append(quotedPropertyName.Trim('"'))
                .Append("' required the dependent property '")
                .Append(quotedDependency.Trim('"'))
                .Append("' which was not present.");
        }

        static void AppendValidText(CodeGenerator generator, string quotedPropertyName, string quotedDependency)
        {
            generator
                .Append("the property '")
                .Append(quotedPropertyName.Trim('"'))
                .Append("' required the dependent property '")
                .Append(quotedDependency.Trim('"'))
                .Append("' which was present.");
        }

        static void AppendBody(CodeGenerator generator, IObjectDependentRequiredValidationKeyword keyword, string propertyNameAsQuotedString, string quotedDependency, string quotedPathModifier)
        {
            generator
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            quotedPathModifier,
                            "));")
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => AppendErrorText(g, propertyNameAsQuotedString, quotedDependency))
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return result.WithResult(isValid: false);")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            quotedPathModifier,
                            "));")
                        .AppendKeywordValidationResult(isValid: true, keyword, "result", g => AppendValidText(g, propertyNameAsQuotedString, quotedDependency))
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}