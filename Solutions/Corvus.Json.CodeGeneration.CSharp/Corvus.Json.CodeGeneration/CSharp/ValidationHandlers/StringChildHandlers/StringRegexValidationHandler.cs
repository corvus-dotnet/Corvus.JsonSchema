﻿// <copyright file="StringRegexValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class StringRegexValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="StringRegexValidationHandler"/>.
    /// </summary>
    public static StringRegexValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
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

        foreach (IStringRegexValidationProviderKeyword keyword in typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            // Gets the field name for the validation constant.
            string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword);
            if (keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? regularExpressions))
            {
                string regularExpression = regularExpressions[0];
                string quotedReducedPathModifier = SymbolDisplay.FormatLiteral(keyword.GetPathModifier(), true);

                generator
                    .AppendSeparatorLine()
                    .AppendIndent("if (")
                    .Append(memberName)
                    .AppendLine(".IsMatch(input))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (context.Level == ValidationLevel.Verbose)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, regularExpression), useInterpolatedString: true, reducedPathModifier: quotedReducedPathModifier)
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (context.Level == ValidationLevel.Flag)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = context.Context.WithResult(isValid: false);")
                            .AppendLineIndent("return true;")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else if (context.Level >= ValidationLevel.Detailed)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, regularExpression), useInterpolatedString: true, reducedPathModifier: quotedReducedPathModifier)
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetSimplifiedInvalidMessage(g, regularExpression), useInterpolatedString: false, reducedPathModifier: quotedReducedPathModifier)
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;

        static void GetValidMessage(CodeGenerator generator, string expression)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("{input.ToString()} matched '")
                .Append(FormatRegularExpression(expression))
                .Append("'");
        }

        static void GetInvalidMessage(CodeGenerator generator, string expression)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("{input.ToString()} did not match '")
                .Append(FormatRegularExpression(expression))
                .Append("'");
        }

        static void GetSimplifiedInvalidMessage(CodeGenerator generator, string expression)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("The value did not match '")
                .Append(FormatRegularExpression(expression))
                .Append("'");
        }

        static string FormatRegularExpression(string expression)
        {
            return SymbolDisplay.FormatLiteral(expression, true)[1..^1].Replace("{", "{{").Replace("}", "}}");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}