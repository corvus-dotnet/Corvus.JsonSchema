// <copyright file="StringRegularExpressionValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.StringChildHandlers;

/// <summary>
/// A string regular expression validation handler.
/// </summary>
public class StringRegularExpressionValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="StringRegularExpressionValidationHandler"/>.
    /// </summary>
    public static StringRegularExpressionValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool requiresShortCut = false;

        foreach (IStringRegexValidationProviderKeyword keyword in typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>())
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

            generator.
                AppendStringRegularExpressionMatch(typeDeclaration, keyword);

            requiresShortCut = true;
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // Not expected to be called
        throw new InvalidOperationException();
    }
}

public static class StringRegularExpressionValidationExtensions
{
    public static CodeGenerator AppendStringRegularExpressionMatch(this CodeGenerator generator, TypeDeclaration typeDeclaration, IStringRegexValidationProviderKeyword keyword)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string> expressions))
        {
            throw new InvalidOperationException("Unable to get validation constants for keyword.");
        }

        Debug.Assert(expressions.Count == 1, "Expected exactly one regular expression for keyword.");

        string rawPattern = expressions[0];
        string regularExpression = SymbolDisplay.FormatLiteral(rawPattern, true);
        string keywordLiteral = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
        RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(rawPattern);

        switch (category)
        {
            case RegexPatternCategory.Noop:
                return generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "JsonSchemaEvaluation.MatchNoopRegularExpression(",
                        regularExpression, ", ",
                        keywordLiteral, "u8, ref context);");

            case RegexPatternCategory.NonEmpty:
                return generator
                    .AppendSeparatorLine()
                    .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, false)
                    .AppendLineIndent(
                        "JsonSchemaEvaluation.MatchNonEmptyRegularExpression(unescapedUtf8JsonString.Span, ",
                        regularExpression, ", ",
                        keywordLiteral, "u8, ref context);");

            case RegexPatternCategory.Prefix:
            {
                string prefix = CodeGenerationExtensions.ExtractRegexPrefix(rawPattern);
                string prefixLiteral = SymbolDisplay.FormatLiteral(prefix, true);
                return generator
                    .AppendSeparatorLine()
                    .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, false)
                    .AppendLineIndent(
                        "JsonSchemaEvaluation.MatchPrefixRegularExpression(unescapedUtf8JsonString.Span, ",
                        prefixLiteral, "u8, ",
                        regularExpression, ", ",
                        keywordLiteral, "u8, ref context);");
            }

            case RegexPatternCategory.Range:
            {
                (int min, int max) = CodeGenerationExtensions.ExtractRegexRange(rawPattern);
                return generator
                    .AppendSeparatorLine()
                    .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, false)
                    .AppendLineIndent(
                        "JsonSchemaEvaluation.MatchRangeRegularExpression(unescapedUtf8JsonString.Span, ",
                        min.ToString(), ", ", max.ToString(), ", ",
                        regularExpression, ", ",
                        keywordLiteral, "u8, ref context);");
            }

            default:
            {
                string regularExpressionMemberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword);

                return generator
                    .AppendSeparatorLine()
                    .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, false)
                    .AppendLineIndent(
                        "JsonSchemaEvaluation.MatchRegularExpression(unescapedUtf8JsonString.Span, ",
                        regularExpressionMemberName, ",",
                        regularExpression, ", ",
                        keywordLiteral, "u8, ref context);");
            }
        }
    }
}