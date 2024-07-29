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
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
            .BeginReservedMethodDeclaration(
                "public static",
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
        if (typeDeclaration.ExplicitFormat() is not string explicitFormat)
        {
            return generator;
        }

        if (FormatProviderRegistry.Instance.FormatProviders.GetExpectedValueKind(explicitFormat) is not JsonValueKind expectedValueKind)
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

        if (typeDeclaration.IsFormatAssertion() || typeDeclaration.AlwaysAssertFormat())
        {
            FormatProviderRegistry.Instance.FormatProviders.AppendFormatAssertion(generator, explicitFormat, "value", "validationContext");
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
            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the keyword is not asserted.");
        }

        static void AppendUnknownFormat(CodeGenerator generator, string format)
        {
            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the format is not recognized.");
        }

        static void AppendIgnoredValueKind(CodeGenerator generator, string format, JsonValueKind expectedValueKind)
        {
            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the value '{{valueKind}}' is not '")
                .Append(expectedValueKind)
                .Append("'.");
        }

        static CodeGenerator AppendUnkownFormat(CodeGenerator generator, TypeDeclaration typeDeclaration, string explicitFormat)
        {
            generator
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext unknownResult = validationContext;");

            IEnumerable<IFormatProviderKeyword> unknownKeywords = typeDeclaration.Keywords().OfType<IFormatProviderKeyword>();

            foreach (IFormatProviderKeyword keyword in unknownKeywords)
            {
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