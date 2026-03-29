// <copyright file="PropertyCountValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A property count validation handler.
/// </summary>
public class PropertyCountValidationHandler : IChildArrayItemValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertyCountValidationHandler"/>.
    /// </summary>
    public static PropertyCountValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IPropertyCountConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IPropertyCountConstantValidationKeyword>())
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
                .AppendIndent("if (propertyCount ")
                .AppendOperator(op)
                .Append(' ')
                .Append(memberName)
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: true, keyword, "result", g => GetValidMessage(g, op, memberName), useInterpolatedString: true, withKeyword: true)
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
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetInvalidMessage(g, op, memberName), useInterpolatedString: true, withKeyword: true)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else if (level >= ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => GetSimplifiedInvalidMessage(g, op), useInterpolatedString: false, withKeyword: true)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return ValidationContext.InvalidContext;")
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
                .Append("property count {propertyCount} ")
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
                .Append("property count {propertyCount} ")
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
                .Append(" the required count.");
        }
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}