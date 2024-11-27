// <copyright file="ValidationCodeGeneratorExtensions.Const.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers;
#endif

using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for required core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires type validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    public static CodeGenerator AppendConstValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Constant value validation.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value to validate.</param>")
            .AppendLineIndent("/// <param name=\"validationContext\">The current validation context.</param>")
            .AppendLineIndent("/// <param name=\"level\">The current validation level.</param>")
            .AppendLineIndent("/// <returns>The resulting validation context after validation.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .BeginReservedMethodDeclaration(
                "internal static",
                "ValidationContext",
                methodName,
                new("in", typeDeclaration.DotnetTypeName(), "value"),
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
            .PrependChildValidationCode(typeDeclaration, children, parentHandlerPriority)
            .AppendConstValidation(typeDeclaration)
            .AppendChildValidationCode(typeDeclaration, children, parentHandlerPriority)
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendConstValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        var keywords =
            typeDeclaration.Keywords()
                .OfType<ISingleConstantValidationKeyword>()
                .ToList();

        if (keywords.Count > 1)
        {
            generator
                .ReserveName("result")
                .AppendLineIndent("ValidationContext result = validationContext;");
        }

        foreach (ISingleConstantValidationKeyword keyword in keywords)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            string constField = generator.GetPropertyNameInScope(keyword.Keyword, rootScope: generator.ValidationClassScope());

            string realisedMethodName = generator.GetUniqueMethodNameInScope(keyword.Keyword, prefix: "Validate");

            keyword.TryGetConstantValue(typeDeclaration, out JsonElement constantValue);

            StringBuilder builder = new(constantValue.GetRawText());
            builder
                .Replace("{", "{{")
                .Replace("}", "}}");

            string constantValueRawString = builder.ToString();

            generator
                .AppendSeparatorLine();

            if (keywords.Count == 1)
            {
                generator
                    .AppendLineIndent("return ", realisedMethodName, "(value, validationContext, level);");
            }
            else
            {
                generator

                    .AppendLineIndent("result = ", realisedMethodName, "(value, result, level);")
                    .AppendLineIndent("if (level == ValidationLevel.Flag && !result.IsValid)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            string pathModifier = SymbolDisplay.FormatLiteral(keyword.GetPathModifier(), true);

            generator
                .AppendLine()
                .BeginLocalMethodDeclaration(
                    "static",
                    "ValidationContext",
                    realisedMethodName,
                    new("in", typeDeclaration.DotnetTypeName(), "value"),
                    ("in ValidationContext", "validationContext"),
                    ("ValidationLevel", "level", "ValidationLevel.Flag"))
                    .ReserveName("result")
                    .AppendBlockIndent("ValidationContext result = validationContext;")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (value.Equals(", generator.ValidationClassName(), ".", constField, "))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendKeywordValidationResult(isValid: true, keyword, "result", g => AppendValidText(g, keyword, constantValueRawString), useInterpolatedString: true, reducedPathModifier: pathModifier)
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (level == ValidationLevel.Flag)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return ValidationContext.InvalidContext;")
                    .PopIndent()
                    .AppendLineIndent("}")

                    .AppendLineIndent("else if (level >= ValidationLevel.Detailed)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => AppendDetailedInvalidText(g, keyword, constantValueRawString), useInterpolatedString: true, reducedPathModifier: pathModifier)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendKeywordValidationResult(isValid: false, keyword, "result", g => AppendInvalidText(g, keyword, constantValueRawString), reducedPathModifier: pathModifier)
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return result;")
                .EndMethodDeclaration();
        }

        return generator;

        static void AppendValidText(CodeGenerator generator, ISingleConstantValidationKeyword keyword, string constantValue)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("the value '{value}' matched '")
                .Append(SymbolDisplay.FormatLiteral(constantValue, true).Trim('"'))
                .Append("'.");
        }

        static void AppendDetailedInvalidText(CodeGenerator generator, ISingleConstantValidationKeyword keyword, string constantValue)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("the value '{value}' did not match '")
                .Append(SymbolDisplay.FormatLiteral(constantValue, true).Trim('"'))
                .Append("'.");
        }

        static void AppendInvalidText(CodeGenerator generator, ISingleConstantValidationKeyword keyword, string constantValue)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("the value did not match '")
                .Append(SymbolDisplay.FormatLiteral(constantValue, true).Trim('"'))
                .Append("'.");
        }
    }
}