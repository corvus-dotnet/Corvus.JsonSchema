// <copyright file="ValidationCodeGeneratorExtensions.If.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for if-then-(else) composition.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires allOf validation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendIfValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// If/then/else composition validation.")
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
                .ReserveName("result")
                .ReserveName("isValid")
                .AppendBlockIndent(
                """
                ValidationContext result = validationContext;
                """)
            .AppendIfValidation(typeDeclaration)
            .AppendSeparatorLine()
            .AppendLineIndent("return result;")
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendIfValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.IfSubschemaType() is SingleSubschemaKeywordTypeDeclaration ifType)
        {
            AppendIf(generator, ifType);

            if (typeDeclaration.ThenSubschemaType() is SingleSubschemaKeywordTypeDeclaration thenType)
            {
                AppendThen(generator, thenType);
            }

            if (typeDeclaration.ElseSubschemaType() is SingleSubschemaKeywordTypeDeclaration elseType)
            {
                AppendElse(generator, elseType);
            }
        }

        return generator;

        static void AppendElse(CodeGenerator generator, SingleSubschemaKeywordTypeDeclaration elseType)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (!ifResult.IsValid)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            SymbolDisplay.FormatLiteral(elseType.KeywordPathModifier, true),
                            "));")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "ValidationContext elseResult = value.As<",
                        elseType.ReducedType.FullyQualifiedDotnetTypeName(),
                        ">().Validate(validationContext.CreateChildContext(), level);")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!elseResult.IsValid)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = validationContext.MergeResults(false, level, ifResult, elseResult);")
                            .AppendKeywordValidationResult(isValid: false, elseType.Keyword, "result", "failed to validate against the else schema")
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
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.MergeChildContext(elseResult, true);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.MergeChildContext(elseResult, false);")
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
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendThen(CodeGenerator generator, SingleSubschemaKeywordTypeDeclaration thenType)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (ifResult.IsValid)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level > ValidationLevel.Basic)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(
                            "result = result.PushValidationLocationReducedPathModifier(new(",
                            SymbolDisplay.FormatLiteral(thenType.KeywordPathModifier, true),
                            "));")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "ValidationContext thenResult = value.As<",
                        thenType.ReducedType.FullyQualifiedDotnetTypeName(),
                        ">().Validate(validationContext.CreateChildContext(), level);")
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!thenResult.IsValid)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = validationContext.MergeResults(false, level, ifResult, thenResult);")
                            .AppendKeywordValidationResult(isValid: false, thenType.Keyword, "result", "failed to validate against the then schema")
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
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.MergeChildContext(thenResult, true);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.MergeChildContext(thenResult, false);")
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
                    .AppendLineIndent("}")
                    .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendIf(CodeGenerator generator, SingleSubschemaKeywordTypeDeclaration ifType)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (level > ValidationLevel.Basic)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(
                        "result = result.PushValidationLocationReducedPathModifier(new(",
                        SymbolDisplay.FormatLiteral(ifType.KeywordPathModifier, true),
                        "));")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "ValidationContext ifResult = value.As<",
                    ifType.ReducedType.FullyQualifiedDotnetTypeName(),
                    ">().Validate(validationContext.CreateChildContext(), level);")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    if (!ifResult.IsValid)
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = validationContext.MergeResults(true, level, ifResult);
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Verbose)
                        {
                            result = result.MergeChildContext(ifResult, true);
                        }
                        else
                        {
                            result = result.MergeChildContext(ifResult, false);
                        }
                    }
                    """)
                .AppendSeparatorLine()
                .AppendLineIndent("if (level > ValidationLevel.Basic)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("result = result.PopLocation();")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }
}