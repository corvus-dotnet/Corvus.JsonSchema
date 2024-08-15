// <copyright file="ContainsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A validation handler for a contains count.
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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IArrayContainsValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().FirstOrDefault();
        if (keywordOrDefault is IArrayContainsValidationKeyword containsKeyword)
        {
            if (!generator.TryPeekMetadata(ContainsCountKey, out string? containsCountName) || containsCountName is null)
            {
                Debug.Fail("Expected to find a contains count variable name.");
            }

            bool foundMinimumRangeValidation = false;

            foreach (IArrayContainsCountConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayContainsCountConstantValidationKeyword>())
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (!keyword.TryGetOperator(typeDeclaration, out Operator op) || op == Operator.None)
                {
                    continue;
                }

                if (op == Operator.GreaterThan || op == Operator.GreaterThanOrEquals)
                {
                    foundMinimumRangeValidation = true;
                }

                // Gets the field name for the validation constant.
                string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationProperty(",
                            SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                            ");")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendIndent("if (")
                    .Append(containsCountName)
                    .Append(' ')
                    .AppendOperator(op)
                    .Append(' ')
                    .Append(memberName)
                    .AppendLine(")")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, op, containsCountName, memberName), useInterpolatedString: true)
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Detailed)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, op, containsCountName, memberName), useInterpolatedString: true)
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

            if (!foundMinimumRangeValidation)
            {
                generator
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationProperty(",
                            SymbolDisplay.FormatLiteral(containsKeyword.Keyword, true),
                            ");")
                    .PopIndent()
                    .AppendLineIndent("}")
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
                                containsKeyword,
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
                    .AppendLineIndent("}")
                    .AppendLineIndent("else if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, containsKeyword, "result", "contained at least one item.")
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
        }

        return generator;

        static void GetValidMessage(CodeGenerator generator, Operator op, string containsCountName, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("count of {")
                .Append(containsCountName)
                .Append("} was ")
                .AppendTextForOperator(op)
                .Append(" {")
                .Append(memberName)
                .Append("}");
        }

        static void GetInvalidMessage(CodeGenerator generator, Operator op, string containsCountName, string memberName)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("count of {")
                .Append(containsCountName)
                .Append("} was ")
                .AppendTextForInverseOperator(op)
                .Append(" {")
                .Append(memberName)
                .Append("}");
        }

        static void GetSimplifiedInvalidMessage(CodeGenerator generator, Operator op)
        {
            generator
                .AppendTextForInverseOperator(op)
                .Append(" the required count.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

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
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IArrayContainsValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().FirstOrDefault();
        if (keywordOrDefault is IArrayContainsValidationKeyword keyword)
        {
            if (generator.TryPeekMetadata(ContainsCountKey, out string? containsCountName))
            {
                if (keyword.TryGetContainsItemType(typeDeclaration, out ArrayItemsTypeDeclaration? containsType))
                {
                    // We don't need to push the reduced path modifier, because
                    // the contains result does not get applied back to the parent.
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("ValidationContext containsResult = result.CreateChildContext();")
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