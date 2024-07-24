// <copyright file="UniqueItemsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class UniqueItemsValidationHandler : IChildArrayItemValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="UniqueItemsValidationHandler"/>.
    /// </summary>
    public static UniqueItemsValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.First;

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
    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        IUniqueItemsArrayValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IUniqueItemsArrayValidationKeyword>().FirstOrDefault(k => k.RequiresUniqueItems(typeDeclaration));
        if (keywordOrDefault is IUniqueItemsArrayValidationKeyword keyword)
        {
            if (keyword.RequiresUniqueItems(typeDeclaration))
            {
                string innerIndexName = generator.GetUniqueVariableNameInScope("innerIndex");
                string innerEnumeratorName = generator.GetUniqueVariableNameInScope("innerEnumerator");
                generator
                    .AppendSeparatorLine()
                    .AppendArrayEnumerator(typeDeclaration, innerEnumeratorName)
                    .AppendIndent("int ")
                    .Append(innerIndexName)
                    .AppendLine(" = -1;")
                    .AppendIndent("while (")
                    .Append(innerIndexName)
                    .AppendLine(" < length && ")
                    .Append(innerEnumeratorName)
                    .AppendLine(".MoveNext())")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent(innerIndexName)
                        .AppendLine("++;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendIndent("bool foundDuplicate = false;")
                    .AppendIndent("while (")
                    .Append(innerEnumeratorName)
                    .AppendLine(".MoveNext())")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent("if (")
                        .Append(innerEnumeratorName)
                        .Append(".Current.Equals(arrayEnumerator.Current))")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("foundDuplicate = true;")
                            .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.PushValidationLocationProperty(",
                                    SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                                    ");")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (level >= ValidationLevel.Detailed)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", g => DetailedMessage(g, innerIndexName), useInterpolatedString: true)
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", "duplicate items were found.")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("return result.WithResult(isValid: false);")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.PopLocation();")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!foundDuplicate && level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationProperty(",
                            SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                            ");")
                            .AppendKeywordValidationResult(isValid: true, keyword, "result", "no duplicate items found.")
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
            else
            {
                generator
                    .AppendLineIndent(
                        "result = result.PushValidationLocationProperty(",
                        SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                        ");")
                    .AppendKeywordValidationResult(isValid: true, keyword, "result", "ignored because unique items were not required.")
                    .AppendLineIndent("result = result.PopLocation();");
            }
        }

        return generator;

        static void DetailedMessage(CodeGenerator generator, string innerIndexName)
        {
            generator
                .Append("duplicate items were found at indices ")
                .Append(innerIndexName)
                .Append(" and {length}.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}