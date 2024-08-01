// <copyright file="ValidationCodeGeneratorExtensions.Not.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers;
#endif

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
    public static CodeGenerator AppendNotValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Not validation.")
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
            .AppendNotValidation(typeDeclaration)
            .AppendChildValidationCode(typeDeclaration, children, parentHandlerPriority)
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendNotValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        var keywords =
            typeDeclaration.Keywords()
                .OfType<INotValidationKeyword>()
                .ToList();

        if (keywords.Count > 1)
        {
            generator
                .ReserveName("result")
                .AppendLineIndent("ValidationContext result = validationContext;");
        }

        foreach (INotValidationKeyword keyword in keywords)
        {
            if (keyword.TryGetNotType(typeDeclaration, out ReducedTypeDeclaration? value) &&
                value is ReducedTypeDeclaration notType)
            {
                string notField = generator.GetPropertyNameInScope(keyword.Keyword, rootScope: generator.ValidationClassScope());
                string realisedMethodName = generator.GetUniqueMethodNameInScope(keyword.Keyword, prefix: "Validate");
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

                string pathModifier = keyword.GetPathModifier(notType);

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
                        .AppendLineIndent("ValidationContext result = validationContext;")
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (level > ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.PushValidationLocationReducedPathModifier(new(",
                                    SymbolDisplay.FormatLiteral(pathModifier, true),
                                    "));")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendSeparatorLine()

                            .AppendSeparatorLine()
                            .AppendLineIndent(
                                "ValidationContext compositionResult = value.As<",
                                notType.ReducedType.FullyQualifiedDotnetTypeName(),
                                ">().Validate(result.CreateChildContext(), level);")

                            .AppendLineIndent("if (compositionResult.IsValid)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                                .AppendLineIndent("{")
                                .PushIndent()
                                    .AppendLineIndent("result = validationContext.MergeResults(false, level, compositionResult);")
                                    .AppendKeywordValidationResult(isValid: false, keyword, "result", "incorrectly validated successfully against the schema")
                                .PopIndent()
                                .AppendLineIndent("}")
                                .AppendLineIndent("else")
                                .AppendLineIndent("{")
                                .PushIndent()
                                    .AppendLineIndent("result = validationContext.WithResult(isValid: false);")
                                .PopIndent()
                                .AppendLineIndent("}")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else if (level >= ValidationLevel.Basic)")
                                .AppendLineIndent("{")
                                .PushIndent()
                                    .AppendLineIndent("result = result.MergeResults(result.IsValid, level, compositionResult);")
                                .PopIndent()
                                .AppendLineIndent("}")
                            .AppendSeparatorLine()
                            .AppendLineIndent("if (level > ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.PopLocation();")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("return result;")
                    .EndMethodDeclaration();
            }
        }

        return generator;
    }
}