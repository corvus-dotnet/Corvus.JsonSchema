// <copyright file="StringLengthValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class StringLengthValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="StringLengthValidationHandler"/>.
    /// </summary>
    public static StringLengthValidationHandler Instance { get; } = new();

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

        foreach (IStringLengthConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IStringLengthConstantValidationKeyword>())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
            {
                continue;
            }

            // Gets the field name for the validation constant.
            string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword);

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
                .AppendIndent("if (length ")
                .AppendOperator(op)
                .Append(' ')
                .Append(memberName)
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (context.Level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, op, memberName), useInterpolatedString: true)
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
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, op, memberName), useInterpolatedString: true)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else if (context.Level >= ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetSimplifiedInvalidMessage(g, op), useInterpolatedString: false)
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
                .AppendLineIndent("}")
;
        }

        return generator;

        static void GetValidMessage(CodeGenerator generator, Operator op, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("{input.ToString()} of {length} ")
                .AppendTextForOperator(op)
                .Append(" {")
                .Append(memberName)
                .Append("}");
        }

        static void GetInvalidMessage(CodeGenerator generator, Operator op, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("{input.ToString()} of {length} ")
                .AppendTextForInverseOperator(op)
                .Append(" {")
                .Append(memberName)
                .Append("}");
        }

        static void GetSimplifiedInvalidMessage(CodeGenerator generator, Operator op)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendTextForInverseOperator(op)
                .Append(" the required length.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}