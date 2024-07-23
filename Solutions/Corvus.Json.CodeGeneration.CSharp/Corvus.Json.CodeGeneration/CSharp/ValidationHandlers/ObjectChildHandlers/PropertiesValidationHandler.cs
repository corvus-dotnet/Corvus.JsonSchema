// <copyright file="PropertiesValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A properties property validation handler.
/// </summary>
public class PropertiesValidationHandler : IChildObjectPropertyValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertiesValidationHandler"/>.
    /// </summary>
    public static PropertiesValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Last;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ExplicitProperties() is IReadOnlyCollection<PropertyDeclaration> properties)
        {
            generator
                .AppendSeparatorLine();

            bool first = true;
            foreach (PropertyDeclaration property in properties)
            {
                if (first)
                {
                    first = false;
                    generator.AppendIndent(string.Empty);
                }
                else
                {
                    generator.AppendIndent("else ");
                }

                AppendPropertyValidation(generator, property);
            }

            if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localEvaluatedProperty)
            {
                bool enumeratorIsCorrectType = localEvaluatedProperty.ReducedType.LocatedSchema.Location == typeDeclaration.FallbackObjectPropertyType()?.ReducedType.LocatedSchema.Location;
                AppendLocalEvaluatedProperty(generator, localEvaluatedProperty, enumeratorIsCorrectType);
            }

            if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedEvaluatedProperty)
            {
                bool enumeratorIsCorrectType = localAndAppliedEvaluatedProperty.ReducedType.LocatedSchema.Location == typeDeclaration.FallbackObjectPropertyType()?.ReducedType.LocatedSchema.Location;
                AppendLocalAndAppliedEvaluatedProperty(generator, localAndAppliedEvaluatedProperty, enumeratorIsCorrectType);
            }
        }

        return generator;

        static void AppendPropertyValidation(CodeGenerator generator, PropertyDeclaration property)
        {
            generator
                .AppendLine(
                    "if ((property.NamePrefersUtf8 && property.NameEquals(",
                    generator.JsonPropertyNamesClassName(),
                    ".",
                    property.DotnetPropertyName(),
                    "Utf8)) ||")
                .PushIndent()
                .AppendLineIndent(
                    "(!property.NamePrefersUtf8 && property.NameEquals(",
                    generator.JsonPropertyNamesClassName(),
                    ".",
                    property.DotnetPropertyName(),
                    ")))")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("result = result.WithLocalProperty(propertyCount);")
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifierAndProperty(new(",
                            SymbolDisplay.FormatLiteral(property.KeywordPathModifier, true),
                            "), ",
                            generator.JsonPropertyNamesClassName(),
                            ".",
                            property.DotnetPropertyName(),
                            ");")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("ValidationContext propertyResult = property.Value.As<", property.ReducedPropertyType.FullyQualifiedDotnetTypeName(), ">().Validate(result.CreateChildContext(), level);")
                    .AppendLineIndent("result = result.MergeResults(propertyResult.IsValid, level, propertyResult);")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level == ValidationLevel.Flag && !result.IsValid)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendLocalEvaluatedProperty(CodeGenerator generator, FallbackObjectPropertyType fallbackProperty, bool enumeratorIsCorrectType)
        {
            generator
                .AppendLineIndent("if (!result.HasEvaluatedLocalProperty(propertyCount))")
                .AppendLineIndent("{")
                .PushIndent();

            AppendFallbackPropertyValidation(generator, fallbackProperty, enumeratorIsCorrectType);

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendLocalAndAppliedEvaluatedProperty(CodeGenerator generator, FallbackObjectPropertyType fallbackProperty, bool enumeratorIsCorrectType)
        {
            generator
                .AppendLineIndent("if (!result.HasEvaluatedLocalOrAppliedProperty(propertyCount))")
                .AppendLineIndent("{")
                .PushIndent();

            AppendFallbackPropertyValidation(generator, fallbackProperty, enumeratorIsCorrectType);

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendFallbackPropertyValidation(CodeGenerator generator, FallbackObjectPropertyType fallbackPropertyType, bool enumeratorIsCorrectType)
        {
            generator
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("string localEvaluatedPropertyName = (propertyNameAsString ??= property.Name.GetString());");

            if (fallbackPropertyType.ReducedPathModifier.Fragment.Length > 1)
            {
                generator
                            .AppendLineIndent(
                                "result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference(",
                                SymbolDisplay.FormatLiteral(fallbackPropertyType.KeywordPathModifier, true),
                                ").AppendUnencodedPropertyNameToFragment(localEvaluatedPropertyName).AppendFragment(new(",
                                SymbolDisplay.FormatLiteral(fallbackPropertyType.ReducedPathModifier, true),
                                ")), localEvaluatedPropertyName);");
            }
            else
            {
                generator
                            .AppendLineIndent(
                                "result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference(",
                                SymbolDisplay.FormatLiteral(fallbackPropertyType.KeywordPathModifier, true),
                                ").AppendUnencodedPropertyNameToFragment(localEvaluatedPropertyName), localEvaluatedPropertyName);");
            }

            generator
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();

            if (!fallbackPropertyType.ReducedType.IsJsonAnyType())
            {
                if (enumeratorIsCorrectType)
                {
                    generator
                        .AppendLineIndent("result = property.Value.Validate(result, level);");
                }
                else
                {
                    generator
                        .AppendLineIndent("result = property.Value.As<", fallbackPropertyType.ReducedType.FullyQualifiedDotnetTypeName(), ">().Validate(result, level);");
                }

                generator
                    .AppendLineIndent("if (level == ValidationLevel.Flag && !result.IsValid)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("result = result.WithLocalProperty(propertyCount);");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}