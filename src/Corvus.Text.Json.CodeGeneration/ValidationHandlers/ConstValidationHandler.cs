// <copyright file="ConstValidationHandler.cs" company="Endjin Limited">
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
using System.Runtime.InteropServices;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.Internal;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IConstValidationKeyword"/> capability.
/// </summary>
internal sealed class ConstValidationHandler : KeywordValidationHandlerBase
{
    private ConstValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="ConstValidationHandler"/>.
    /// </summary>
    public static ConstValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Default;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendConstValidationSetup()
             .AppendChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority);
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IReadOnlyCollection<IChildValidationHandler> childHandlers = ChildHandlers;

        generator
            .AppendConstValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is ISingleConstantValidationKeyword;
    }

    private static ConstValidationHandler CreateDefault()
    {
        return new ConstValidationHandler();
    }
}

file static class ConstValidationHandlerExtensions
{
    public static CodeGenerator AppendConstValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendConstValidation(
        this CodeGenerator generator,
        IKeywordValidationHandler parentHandler,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> childHandlers,
        uint validationPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .PrependChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        ISingleConstantValidationKeyword keyword = typeDeclaration.Keywords().OfType<ISingleConstantValidationKeyword>().Single();
        keyword.TryGetConstantValue(typeDeclaration, out JsonElement constantValue);
        Debug.Assert(constantValue.ValueKind != JsonValueKind.Undefined);

        switch (constantValue.ValueKind)
        {
            case JsonValueKind.String:
                generator
                    .AppendStringConstantValidation(typeDeclaration, keyword, constantValue);
                break;
            case JsonValueKind.Number:
                generator
                    .AppendNumberConstantValidation(typeDeclaration, keyword, constantValue);
                break;
            case JsonValueKind.True:
                generator
                    .AppendBooleanConstantValidation(typeDeclaration, keyword, true);
                break;
            case JsonValueKind.False:
                generator
                    .AppendBooleanConstantValidation(typeDeclaration, keyword, false);
                break;
            case JsonValueKind.Null:
                generator
                    .AppendNullConstantValidation(typeDeclaration, keyword);
                break;
            case JsonValueKind.Object:
                generator
                    .AppendComplexValueConstantValidation(typeDeclaration, keyword, constantValue);
                break;
            case JsonValueKind.Array:
                generator
                    .AppendComplexValueConstantValidation(typeDeclaration, keyword, constantValue);
                break;
            default:
                throw new InvalidOperationException("Usupported constant value.");
        }

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        return generator;
    }

    private static CodeGenerator AppendStringConstantValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, ISingleConstantValidationKeyword keyword, JsonElement constantValue)
    {
        string quotedStringValue = SymbolDisplay.FormatLiteral(constantValue.GetString()!, true);

        string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.String)")
            .PushMemberScope("constantValidation", ScopeType.Method)
            .AppendLineIndent("{")
            .PushIndent()
                .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, false)
                .AppendLineIndent(
                    "JsonSchemaEvaluation.MatchStringConstantValue(unescapedUtf8JsonString.Span, ",
                    quotedStringValue, "u8, ", quotedStringValue, ", ",
                    formattedKeyword, "u8, ref context);")
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(false, ", quotedStringValue, ", messageProvider: JsonSchemaEvaluation.ExpectedStringEquals, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
        return generator;
    }

    private static CodeGenerator AppendNumberConstantValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, ISingleConstantValidationKeyword keyword, JsonElement constantValue)
    {
#if BUILDING_SOURCE_GENERATOR
        ReadOnlySpan<byte> rawValue = Encoding.UTF8.GetBytes(constantValue.GetRawText());
#else
        ReadOnlySpan<byte> rawValue = JsonMarshal.GetRawUtf8Value(constantValue);
#endif

        JsonElementHelpers.ParseNumber(rawValue, out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);

        string isNegativeString = isNegative ? "true" : "false";
        string integralString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(integral), true);
        string fractionalString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(fractional), true);
        string exponentString = exponent.ToString();
        string rawValueString = SymbolDisplay.FormatLiteral(Formatting.GetTextFromUtf8(rawValue), true);

        string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.Number)")
            .PushMemberScope("constantValidation", ScopeType.Method)
            .AppendLineIndent("{")
            .PushIndent()
                .AppendNormalizedJsonNumberIfNotAppended(typeDeclaration, false)
                .AppendLineIndent(
                    "JsonSchemaEvaluation.MatchEquals(isNegative, integral, fractional, exponent, ",
                    isNegativeString, ", ",
                    integralString, "u8, ",
                    fractionalString, "u8, ",
                    exponentString, ", ",
                    rawValueString, ", ",
                    formattedKeyword, "u8, ref context);")
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(false, ", rawValueString, ", messageProvider: JsonSchemaEvaluation.ExpectedEquals, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendBooleanConstantValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, ISingleConstantValidationKeyword keyword, bool expectation)
    {
        string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.", expectation ? "True" : "False", ")")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedBoolean", expectation ? "True" : "False", ", ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedBoolean", expectation ? "True" : "False", ", ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
        return generator;
    }

    private static CodeGenerator AppendNullConstantValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, ISingleConstantValidationKeyword keyword)
    {
        string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.Null)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedNull, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedNull, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
        return generator;
    }

    private static CodeGenerator AppendComplexValueConstantValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, ISingleConstantValidationKeyword keyword, JsonElement constantValue)
    {
        Debug.Assert(constantValue.ValueKind is JsonValueKind.Object or JsonValueKind.Array);

        string quotedConstantValue = SymbolDisplay.FormatLiteral(constantValue.GetRawText(), true);

        string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);

        string constPropertyName = generator.GetPropertyNameInScope(keyword.Keyword, rootScope: generator.ConstantsScope());

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (JsonElementHelpers.DeepEqualsNoParentDocumentCheck(", generator.ConstantsClassName(), ".", constPropertyName, ", tokenType, parentDocument, parentIndex))")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(true, ", quotedConstantValue, ", messageProvider: JsonSchemaEvaluation.ExpectedConstant, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.EvaluatedKeyword(false, ", quotedConstantValue, ", messageProvider: JsonSchemaEvaluation.ExpectedConstant, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
        return generator;
    }
}