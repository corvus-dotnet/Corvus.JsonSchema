// <copyright file="ValidationCodeGeneratorExtensions.String.cs" company="Endjin Limited">
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
    /// Append a validation method for string types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires string validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendStringValidation(
        this CodeGenerator generator,
        string methodName,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// String validation.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value to validate.</param>")
            .AppendLineIndent("/// <param name=\"valueKind\">The <see cref=\"JsonValueKind\" /> of the value to validate.</param>")
            .AppendLineIndent("/// <param name=\"validationContext\">The current validation context.</param>")
            .AppendLineIndent("/// <param name=\"level\">The current validation level.</param>")
            .AppendLineIndent("/// <returns>The resulting validation context after validation.</returns>")
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .BeginReservedMethodDeclaration(
                "internal static",
                "ValidationContext",
                methodName,
                new("in", typeDeclaration.DotnetTypeName(), "value"),
                ("JsonValueKind", "valueKind"),
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
                .ReserveName("result")
                .ReserveName("isValid")
            .AppendStringValidation(typeDeclaration, children)
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendStringValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendLineIndent("if (valueKind != JsonValueKind.String)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

        foreach (IStringValidationKeyword keyword in typeDeclaration.Keywords().OfType<IStringValidationKeyword>())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", "ignored because the value is not a string", withKeyword: true);
        }

        generator
                .AppendSeparatorLine()
                .AppendLineIndent("return ignoredResult;")
                .PopIndent()
                .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("return validationContext;")
            .PopIndent()
            .AppendLineIndent("}");

        bool requiresValueValidation = typeDeclaration.RequiresStringValueValidation();

        string valueAccessor = (typeDeclaration.AllowedCoreTypes() & CoreTypes.String) != 0 ? "value" : "value.AsString";

        if (requiresValueValidation)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("ValidationContext result = validationContext;")
                .AppendLineIndent(valueAccessor, ".TryGetValue(StringValidator, new Corvus.Json.Validate.ValidationContextWrapper(result, level), out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("return result;")
                .AppendLine()
                .AppendLineIndent("static bool StringValidator(ReadOnlySpan<char> input, in Corvus.Json.Validate.ValidationContextWrapper context, out ValidationContext result)")
                .AppendLineIndent("{")
                .PushIndent();

            if (typeDeclaration.RequiresStringLength())
            {
                generator
                    .AppendLineIndent("int length = Corvus.Json.Validate.CountRunes(input);");
            }

            generator
                .AppendLineIndent("result = context.Context;");

            foreach (IChildValidationHandler child in children)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                child.AppendValidationCode(generator, typeDeclaration);
            }

            return generator
                .AppendSeparatorLine()
                .AppendLineIndent("return true;")
                .PopIndent()
                .AppendLineIndent("}");
        }
        else
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("return validationContext;");
        }

        return generator;
    }
}