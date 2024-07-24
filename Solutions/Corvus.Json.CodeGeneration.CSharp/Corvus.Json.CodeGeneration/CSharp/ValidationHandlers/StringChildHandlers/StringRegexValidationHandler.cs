// <copyright file="StringRegexValidationHandler.cs" company="Endjin Limited">
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
        foreach (IStringRegexValidationProviderKeyword keyword in typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>())
        {
            // Gets the field name for the validation constant.
            string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword);
            if (keyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? regularExpressions))
            {
                string regularExpression = regularExpressions[0];

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (context.Level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            SymbolDisplay.FormatLiteral(keyword.GetPathModifier(), true),
                            "));")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendIndent("if (")
                    .Append(memberName)
                    .AppendLine(".IsMatch(input))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (context.Level == ValidationLevel.Verbose)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, regularExpression), useInterpolatedString: true)
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (context.Level >= ValidationLevel.Detailed)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, regularExpression), useInterpolatedString: true)
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else if (context.Level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetSimplifiedInvalidMessage(g, regularExpression), useInterpolatedString: false)
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = context.Context.WithResult(isValid: false);")
                            .AppendLineIndent("return true;")
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (context.Level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;

        static void GetValidMessage(CodeGenerator generator, string expression)
        {
            generator
                .Append("{input.ToString()} matched ")
                .Append(" '")
                .Append(SymbolDisplay.FormatLiteral(expression, false))
                .Append("'");
        }

        static void GetInvalidMessage(CodeGenerator generator, string expression)
        {
            generator
                .Append("{input.ToString()} did not match ")
                .Append(" '")
                .Append(SymbolDisplay.FormatLiteral(expression, false))
                .Append("'");
        }

        static void GetSimplifiedInvalidMessage(CodeGenerator generator, string expression)
        {
            generator
                .Append("The value did not match ")
                .Append(" '")
                .Append(SymbolDisplay.FormatLiteral(expression, false))
                .Append("'");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}