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
            return generator;
        }

        if (!typeDeclaration.AlwaysAssertFormat())
        {
            var nonAssertedKeywords = typeDeclaration.Keywords().OfType<IFormatProviderKeyword>().Where(k => k is not IFormatValidationKeyword).ToList();
            if (nonAssertedKeywords.Count > 0)
            {
                generator
                    .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

                foreach (IFormatProviderKeyword keyword in nonAssertedKeywords)
                {
                    generator
                        .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", g => AppendIgnoredFormat(g, explicitFormat), useInterpolatedString: true);
                }

                generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("return ignoredResult;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        generator
            .AppendLineIndent("if (valueKind != JsonValueKind.", expectedValueKind.ToString(), ")")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (level == ValidationLevel.Verbose)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("ValidationContext ignoredResult = validationContext;");

        IEnumerable<IFormatProviderKeyword> keywords =
            typeDeclaration.AlwaysAssertFormat()
                ? typeDeclaration.Keywords().OfType<IFormatProviderKeyword>()
                : typeDeclaration.Keywords().OfType<IFormatValidationKeyword>();

        foreach (IFormatProviderKeyword keyword in keywords)
        {
            generator
                .AppendKeywordValidationResult(isValid: true, keyword, "ignoredResult", g => AppendIgnoredValueKind(g, explicitFormat, expectedValueKind), useInterpolatedString: true);
        }

        generator
                .AppendSeparatorLine()
                .AppendLineIndent("return ignoredResult;")
                .PopIndent()
                .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("return validationContext;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();

        generator
            .AppendLineIndent("ValidationContext result = validationContext;");

        // TODO - do the asssertion here
        foreach (IChildValidationHandler child in children)
        {
            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator
            .AppendLineIndent("return result;");

        static void AppendIgnoredFormat(CodeGenerator generator, string format)
        {
            generator
                .Append("ignored '")
                .Append(format)
                .Append("' because the keyword is not asserted.");
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
    }
}