// <copyright file="ValidationCodeGeneratorExtensions.Format.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Buffers;
#endif

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    /// <summary>
    /// Append a validation method for format types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="methodName">The name of the validation method.</param>
    /// <param name="typeDeclaration">The type declaration which requires numeric validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFormatValidation(
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
            .AppendLineIndent("/// Numeric and string format validation.")
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
            .AppendFormatValidation(typeDeclaration, children)
            .EndMethodDeclaration();
    }

    private static CodeGenerator AppendFormatValidation(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitFormat() is not string explicitFormat)
        {
            return generator
                .AppendLineIndent("return validationContext;");
        }

        if (FormatHandlerRegistry.Instance.FormatHandlers.GetExpectedValueKind(explicitFormat) is not JsonValueKind expectedValueKind)
        {
            return AppendUnkownFormat(generator, typeDeclaration, explicitFormat);
        }

        AppendValueKindCheck(generator, typeDeclaration, explicitFormat, expectedValueKind);

        IReadOnlyCollection<IFormatProviderKeyword> keywords =
            typeDeclaration.AlwaysAssertFormat()
                ? typeDeclaration.Keywords().OfType<IFormatProviderKeyword>().ToList()
                : typeDeclaration.Keywords().OfType<IFormatValidationKeyword>().ToList();

        generator
            .AppendSeparatorLine();

        // Let the children have their go, then work through the standard
        foreach (IChildValidationHandler child in children)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            child.AppendValidationCode(generator, typeDeclaration);
        }

        if (typeDeclaration.IsFormatAssertion() || typeDeclaration.AlwaysAssertFormat())
        {
            FormatHandlerRegistry.Instance.FormatHandlers.AppendFormatAssertion(generator, explicitFormat, "value", "validationContext", null, keywords.FirstOrDefault(), returnFromMethod: true);
        }
        else
        {
            generator
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent("ValidationContext result = validationContext;");

            foreach (IFormatProviderKeyword keyword in keywords)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                generator
                    .AppendKeywordValidationResult(isValid: true, keyword, "result", g => AppendIgnoredFormat(g, explicitFormat), useInterpolatedString: true);
            }

            generator
                 .AppendLineIndent("return result;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return validationContext;");
        }

        return generator;

        static void AppendIgnoredFormat(CodeGenerator generator, string format)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the keyword is not asserted.");
        }

        static void AppendUnknownFormat(CodeGenerator generator, string format)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the format is not recognized.");
        }

        static void AppendIgnoredValueKind(CodeGenerator generator, string format, JsonValueKind expectedValueKind)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the value is of kind '{valueKind}' not '")
                .Append(expectedValueKind)
                .Append("'.");
        }

        static CodeGenerator AppendUnkownFormat(CodeGenerator generator, TypeDeclaration typeDeclaration, string explicitFormat)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext unknownResult = validationContext;");

            IEnumerable<IFormatProviderKeyword> unknownKeywords = typeDeclaration.Keywords().OfType<IFormatProviderKeyword>();

            foreach (IFormatProviderKeyword keyword in unknownKeywords)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                generator
                    .AppendKeywordValidationResult(isValid: true, keyword, "unknownResult", g => AppendUnknownFormat(g, explicitFormat));
            }

            return generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return unknownResult;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return validationContext;");
        }

        static CodeGenerator AppendValueKindCheck(CodeGenerator generator, TypeDeclaration typeDeclaration, string explicitFormat, JsonValueKind expectedValueKind)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendLineIndent("if (valueKind != JsonValueKind.", expectedValueKind.ToString(), ")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

            foreach (IFormatProviderKeyword keyword in typeDeclaration.Keywords().OfType<IFormatProviderKeyword>())
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                generator
                    .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", g => AppendIgnoredValueKind(g, explicitFormat, expectedValueKind), useInterpolatedString: true);
            }

            return generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return ignoredResult;")
                    .PopIndent()
                    .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return validationContext;")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }
}