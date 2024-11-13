// <copyright file="PropertyNamesValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A property name validation handler.
/// </summary>
public class PropertyNamesValidationHandler : IChildObjectPropertyValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertyNamesValidationHandler"/>.
    /// </summary>
    public static PropertyNamesValidationHandler Instance { get; } = new();

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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.PropertyNamesSubschemaType() is SingleSubschemaKeywordTypeDeclaration propertyNameType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (level > ValidationLevel.Basic)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        "result = result.PushValidationLocationReducedPathModifier(new(",
                        SymbolDisplay.FormatLiteral(propertyNameType.KeywordPathModifier, true),
                        "));")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "result = new ",
                    propertyNameType.ReducedType.FullyQualifiedDotnetTypeName(),
                    "(propertyNameAsString ??= property.Name.GetString()).Validate(result, level);")
                .AppendLineIndent("if (level == ValidationLevel.Flag && !result.IsValid)")
                .AppendLineIndent("{")
                .PushIndent()
                        .AppendLineIndent("return result;")
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

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => typeDeclaration.PropertyNamesSubschemaType() is not null;

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