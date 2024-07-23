// <copyright file="PatternPropertiesValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A properties property validation handler.
/// </summary>
public class PatternPropertiesValidationHandler : IChildObjectPropertyValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PatternPropertiesValidationHandler"/>.
    /// </summary>
    public static PatternPropertiesValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

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
        if (typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("propertyNameAsString ??= property.Name.GetString();");

            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternProperties.Values)
            {
                int index = 1;
                bool hasIndex = patternPropertyCollection.Count > 1;
                foreach (PatternPropertyDeclaration patternProperty in patternPropertyCollection)
                {
                    AppendPatternPropertyValidation(generator, patternProperty, hasIndex ? index : null);
                    ++index;
                }
            }
        }

        return generator;

        static void AppendPatternPropertyValidation(CodeGenerator generator, PatternPropertyDeclaration property, int? index)
        {
            string regexAccessor =
                generator.GetStaticReadOnlyFieldNameInScope(
                    property.Keyword.Keyword,
                    rootScope: generator.ValidationClassScope(),
                    suffix: index?.ToString());

            generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "if (",
                    regexAccessor,
                    ".IsMatch(propertyNameAsString))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("result = result.WithLocalProperty(propertyCount);")
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifierAndProperty(new JsonReference(",
                            SymbolDisplay.FormatLiteral(property.KeywordPathModifier, true),
                            "), propertyNameAsString);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("result = property.Value.As<", property.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName(), ">().Validate(result, level);")
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
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}