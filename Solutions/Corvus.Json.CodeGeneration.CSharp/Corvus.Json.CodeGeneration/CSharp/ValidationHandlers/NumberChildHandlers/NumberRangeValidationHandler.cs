// <copyright file="NumberRangeValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A string length validation handler.
/// </summary>
public class NumberRangeValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="NumberRangeValidationHandler"/>.
    /// </summary>
    public static NumberRangeValidationHandler Instance { get; } = new();

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

        foreach (INumberConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<INumberConstantValidationKeyword>())
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

            if (op == Operator.MultipleOf)
            {
                AppendMultipleOf(generator, keyword, memberName);
            }
            else
            {
                AppendStandardOperator(generator, keyword, op, memberName);
            }

            generator
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationProperty(",
                            SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                            ");")
                        .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, op, memberName), useInterpolatedString: true)
                        .AppendLineIndent("result = result.PopLocation();")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
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
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, op, memberName), useInterpolatedString: true)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else if (level >= ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetSimplifiedInvalidMessage(g, op), useInterpolatedString: false)
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
                .AppendLineIndent("}");
        }

        return generator;

        static void GetValidMessage(CodeGenerator generator, Operator op, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("{value} ")
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
                .Append("{value} ")
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
                .Append(" the required value.");
        }

        static void AppendMultipleOf(CodeGenerator generator, INumberConstantValidationKeyword keyword, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (value.HasJsonElementBacking")
                .PushIndent()
                .AppendLineIndent("? BinaryJsonNumber.IsMultipleOf(value.AsJsonElement, ", memberName, ")")
                .AppendLineIndent(": value.AsBinaryJsonNumber.IsMultipleOf(", memberName, "))")
                .PopIndent();
        }

        static void AppendStandardOperator(CodeGenerator generator, INumberConstantValidationKeyword keyword, Operator op, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()

                .AppendLineIndent("if ((value.HasJsonElementBacking")
                .PushIndent()
                .AppendLineIndent("? BinaryJsonNumber.Compare(value.AsJsonElement, ", memberName, ")")
                .AppendIndent(": BinaryJsonNumber.Compare(value.AsBinaryJsonNumber, ", memberName, "))")
                .PopIndent()
                .AppendOperator(op)
                .Append(" 0")
                .AppendLine(")");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}