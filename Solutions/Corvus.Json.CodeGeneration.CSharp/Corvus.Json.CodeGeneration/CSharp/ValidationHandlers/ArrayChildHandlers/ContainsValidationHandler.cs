// <copyright file="ContainsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class ContainsValidationHandler : IChildArrayItemValidationHandler
{
    private const string ContainsCountKey = "Contains_containsCount";

    /// <summary>
    /// Gets the singleton instance of the <see cref="ContainsValidationHandler"/>.
    /// </summary>
    public static ContainsValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.First;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        IArrayContainsValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().FirstOrDefault();
        if (keywordOrDefault is IArrayContainsValidationKeyword keyword)
        {
            if (generator.TryPeekMetadata(ContainsCountKey, out string? containsCountName))
            {
                generator
                    .AppendSeparatorLine()
                    .AppendIndent("if (")
                    .Append(containsCountName)
                    .AppendLine(" == 0)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(
                                isValid: false,
                                keyword,
                                "result",
                                "no items found matching the required schema.")
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
            else
            {
                Debug.Fail($"{ContainsCountKey} was not available.");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().Any())
        {
            string containsCountName = generator.GetUniqueVariableNameInScope("containsCount");
            generator
                .PushMetadata(ContainsCountKey, containsCountName)
                .AppendSeparatorLine()
                .AppendIndent("int ")
                .Append(containsCountName)
                .AppendLine(" = 0;");
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        IArrayContainsValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().FirstOrDefault();
        if (keywordOrDefault is IArrayContainsValidationKeyword keyword)
        {
            if (generator.TryPeekMetadata(ContainsCountKey, out string? containsCountName))
            {
                if (keyword.TryGetContainsItemType(typeDeclaration, out ArrayItemsTypeDeclaration? containsType))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("ValidationContext containsResult = result.CreateChildContext();");

                    if (containsType.ReducedPathModifier.Fragment.Length > 1)
                    {
                        // We don't need to pop the reduced path modifier, because we are not reusing the contains result.
                        generator
                            .AppendIndent("containsResult = containsResult.PushValidationLocationReducedPathModifier(new(")
                            .Append(SymbolDisplay.FormatLiteral(containsType.ReducedPathModifier, true))
                            .AppendLine("));");
                    }

                    generator
                        .AppendIndent("containsResult = arrayEnumerator.Current.As<")
                        .Append(containsType.ReducedType.FullyQualifiedDotnetTypeName())
                        .AppendLine(">().Validate(containsResult, level);")
                        .AppendLineIndent("if (containsResult.IsValid)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.WithLocalItemIndex(length);")
                            .AppendIndent(containsCountName)
                            .AppendLine("++;")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }
            else
            {
                Debug.Fail($"{ContainsCountKey} was not available.");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}