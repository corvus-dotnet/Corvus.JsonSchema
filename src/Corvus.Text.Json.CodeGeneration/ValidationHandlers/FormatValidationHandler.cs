// <copyright file="FormatValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IFormatValidationKeyword"/> capability.
/// </summary>
internal sealed class FormatValidationHandler : TypeSensitiveKeywordValidationHandlerBase, IFormatKeywordValidationHandler
{
    private FormatValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="FormatValidationHandler"/>.
    /// </summary>
    public static FormatValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Default;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendFormatValidationSetup()
             .AppendChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority);
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IKeyword keyword = typeDeclaration.Keywords().OfType<IFormatProviderKeyword>().First();

        IReadOnlyCollection<IChildValidationHandler> childHandlers = ChildHandlers;

        generator
            .AppendFormatValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority, keyword, typeDeclaration.AllowedCoreTypes(), validateOnly);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        // This covers both FormatProvider and FormatValidation
        return keyword is IFormatProviderKeyword;
    }

    /// <inheritdoc/>
    public bool HandlesCoreTypes(TypeDeclaration typeDeclaration, CoreTypes coreTypes)
    {
        if (typeDeclaration.ExplicitFormat() is not string explicitFormat)
        {
            return false;
        }

        if (FormatHandlerRegistry.Instance.FormatHandlers.GetExpectedTokenType(explicitFormat) is not JsonTokenType expectedTokenType)
        {
            return false;
        }

        return
            (expectedTokenType == JsonTokenType.Number && ((coreTypes & CoreTypes.Number | CoreTypes.Integer) != 0)) ||
            (expectedTokenType == JsonTokenType.String && ((coreTypes & CoreTypes.String) != 0));
    }
}

file static class FormatValidationHandlerExtensions
{
    public static CodeGenerator AppendFormatValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendFormatValidation(
        this CodeGenerator generator,
        IKeywordValidationHandler parentHandler,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> childHandlers,
        uint validationPriority,
        IKeyword keyword,
        CoreTypes allowedTypes,
        bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ExplicitFormat() is not string explicitFormat)
        {
            return generator;
        }

        if (FormatHandlerRegistry.Instance.FormatHandlers.GetExpectedTokenType(explicitFormat) is not JsonTokenType expectedTokenType)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("context.IgnoredKeyword(", "JsonSchemaEvaluation.IgnoredUnrecognizedFormat", ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8);");
            return generator;
        }

        Debug.Assert(expectedTokenType is JsonTokenType.String or JsonTokenType.Number);

        if (!validateOnly)
        {
            generator
                .AppendSeparatorLine()
                .AppendStartTokenTypeCheck(typeDeclaration, explicitFormat, expectedTokenType)
                .PushMemberScope("tokenTypeCheck", ScopeType.Method);
        }

        generator
            .PrependChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        if (typeDeclaration.IsFormatAssertion() || typeDeclaration.AlwaysAssertFormat())
        {
            if (expectedTokenType is JsonTokenType.String)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, includeTokenTypeCheck: false);
                FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatAssertion(generator, explicitFormat, $"{SymbolDisplay.FormatLiteral(keyword.Keyword, true)}u8", "unescapedUtf8JsonString.Span", "context");
                generator.AppendLine(";");
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendNormalizedJsonNumberIfNotAppended(typeDeclaration, includeTokenTypeCheck: false);
                FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatAssertion(generator, explicitFormat, $"{SymbolDisplay.FormatLiteral(keyword.Keyword, true)}u8", "isNegative", "integral", "fractional", "exponent", "context");
                generator.AppendLine(";");
            }

            generator
                .AppendConditionalNoCollectorNoMatchShortcutReturn(typeDeclaration, parentHandler);
        }
        else
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("context.IgnoredKeyword(", "JsonSchemaEvaluation.IgnoredFormatNotAsserted", ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8);");
        }

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        if (!validateOnly)
        {
            // We only pop if we were in our own scope
            generator
                .PopNormalizedJsonNumberIfAppendedInScope(typeDeclaration)
                .PopUnescapedUtf8JsonStringIfAppendedInScope(typeDeclaration)
                .PopMemberScope()
                .AppendEndTokenTypeCheck(typeDeclaration, explicitFormat, expectedTokenType);
        }

        return generator;
    }

    private static CodeGenerator AppendStartTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration, string format, JsonTokenType expectedTokenType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.", expectedTokenType.ToString(), ")")
            .AppendLineIndent("{")
            .PushIndent();
    }

    private static CodeGenerator AppendEndTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration, string format, JsonTokenType expectedTokenType)
    {
        generator
            .PopIndent()
            .AppendLineIndent("}");

        if (expectedTokenType == JsonTokenType.String)
        {
            generator
                .ElseAppendIgnoredCoreTypeStringFormatKeywords(typeDeclaration, "JsonSchemaEvaluation.IgnoredNotTypeString");
        }
        else
        {
            generator
                .ElseAppendIgnoredCoreTypeNumberFormatKeywords(typeDeclaration, "JsonSchemaEvaluation.IgnoredNotTypeNumber");
        }

        return generator;
    }
}