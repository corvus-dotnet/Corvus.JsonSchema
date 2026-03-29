// <copyright file="TernaryIfValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="ITernaryIfValidationKeyword"/> capability.
/// </summary>
internal sealed class TernaryIfValidationHandler : KeywordValidationHandlerBase
{
    private TernaryIfValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="TernaryIfValidationHandler"/>.
    /// </summary>
    public static TernaryIfValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendTernaryIfValidationSetup()
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
            .AppendTernaryIfValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is ITernaryIfValidationKeyword;
    }

    private static TernaryIfValidationHandler CreateDefault()
    {
        return new TernaryIfValidationHandler();
    }
}

file static class TernaryIfValidationHandlerExtensions
{
    public static CodeGenerator AppendTernaryIfValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendTernaryIfValidation(
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

        if (typeDeclaration.IfSubschemaType() is SingleSubschemaKeywordTypeDeclaration ifType)
        {
            generator.AppendIfThenElse(typeDeclaration, ifType);
        }
        else
        {
            SingleSubschemaKeywordTypeDeclaration? thenType = typeDeclaration.ThenSubschemaType();
            SingleSubschemaKeywordTypeDeclaration? elseType = typeDeclaration.ElseSubschemaType();

            if (thenType is not null)
            {
                string formattedKeyword = SymbolDisplay.FormatLiteral(thenType.Keyword.Keyword, true);

                generator
                    .AppendLineIndent("context.IgnoredKeyword(JsonSchemaEvaluation.ThenWithoutIf, ", formattedKeyword, "u8);");
            }

            if (elseType is not null)
            {
                string formattedKeyword = SymbolDisplay.FormatLiteral(thenType.Keyword.Keyword, true);

                generator
                    .AppendLineIndent("context.IgnoredKeyword(JsonSchemaEvaluation.ElseeWithoutIf, ", formattedKeyword, "u8);");
            }
        }

        generator
                .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        return generator;
    }

    private static CodeGenerator AppendIfThenElse(this CodeGenerator generator, TypeDeclaration typeDeclaration, SingleSubschemaKeywordTypeDeclaration ifType)
    {
        string ifContextName = generator.GetUniqueVariableNameInScope("Context", prefix: "if");
        string evaluationPathProperty = generator.GetPropertyNameInScope($"{ifType.Keyword.Keyword}SchemaEvaluationPath");
        string targetTypeName = ifType.ReducedType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);

        SingleSubschemaKeywordTypeDeclaration? thenType = typeDeclaration.ThenSubschemaType();
        SingleSubschemaKeywordTypeDeclaration? elseType = typeDeclaration.ElseSubschemaType();
        string formattedKeyword = SymbolDisplay.FormatLiteral(ifType.Keyword.Keyword, true);

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("JsonSchemaContext ", ifContextName, " =")
            .PushIndent()
            .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ", evaluationPathProperty, ");")
            .PopIndent()
            .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentIndex, ref ", ifContextName, ");")
            .AppendSeparatorLine()
            .AppendLineIndent("if (", ifContextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.ApplyEvaluated(ref ", ifContextName, ");")
                .ConditionallyAppend(thenType is not null, g => g.AppendThen(thenType!, ifContextName))
                .ConditionallyAppend(thenType is null, g => g.AppendLineIndent("context.CommitChildContext(true, ref ", ifContextName, ");"))
                .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedIfForThen, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .ConditionallyAppend(elseType is not null, g => g.AppendElse(elseType!, ifContextName))
                .ConditionallyAppend(elseType is null, g => g.AppendLineIndent("context.CommitChildContext(true, ref ", ifContextName, ");"))
                .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedIfForElse, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();
    }

    private static CodeGenerator AppendThen(this CodeGenerator generator, SingleSubschemaKeywordTypeDeclaration thenType, string ifContextName)
    {
        string thenContextName = generator.GetUniqueVariableNameInScope("Context", prefix: "then");
        string evaluationPathProperty = generator.GetPropertyNameInScope($"{thenType.Keyword.Keyword}SchemaEvaluationPath");
        string targetTypeName = thenType.ReducedType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);

        string formattedKeyword = SymbolDisplay.FormatLiteral(thenType.Keyword.Keyword, true);

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("JsonSchemaContext ", thenContextName, " =")
            .PushIndent()
            .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ", evaluationPathProperty, ");")
            .PopIndent()
            .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentIndex, ref ", thenContextName, ");")
            .AppendSeparatorLine()
            .AppendLineIndent("if (", thenContextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.ApplyEvaluated(ref ", thenContextName, ");")
                .AppendLineIndent("context.CommitChildContext(true, ref ", thenContextName, ");")
                .AppendLineIndent("context.CommitChildContext(true, ref ", ifContextName, ");")
                .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedThen, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.CommitChildContext(false, ref ", thenContextName, ");")
                .AppendLineIndent("context.CommitChildContext(false, ref ", ifContextName, ");")
                .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.DidNotMatchThen, ", formattedKeyword, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine();
    }

    private static CodeGenerator AppendElse(this CodeGenerator generator, SingleSubschemaKeywordTypeDeclaration elseType, string ifContextName)
    {
        {
            string elseContextName = generator.GetUniqueVariableNameInScope("Context", prefix: "else");
            string evaluationPathProperty = generator.GetPropertyNameInScope($"{elseType.Keyword.Keyword}SchemaEvaluationPath");
            string targetTypeName = elseType.ReducedType.FullyQualifiedDotnetTypeName();
            string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);

            string formattedKeyword = SymbolDisplay.FormatLiteral(elseType.Keyword.Keyword, true);

            return generator
                .AppendSeparatorLine()
                .AppendLineIndent("JsonSchemaContext ", elseContextName, " =")
                .PushIndent()
                .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ", evaluationPathProperty, ");")
                .PopIndent()
                .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentIndex, ref ", elseContextName, ");")
                .AppendSeparatorLine()
                .AppendLineIndent("if (", elseContextName, ".IsMatch)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("context.ApplyEvaluated(ref ", elseContextName, ");")
                    .AppendLineIndent("context.CommitChildContext(true, ref ", elseContextName, ");")
                    .AppendLineIndent("context.CommitChildContext(true, ref ", ifContextName, ");")
                    .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.MatchedElse, ", formattedKeyword, "u8);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("context.CommitChildContext(false, ref ", elseContextName, ");")
                    .AppendLineIndent("context.CommitChildContext(false, ref ", ifContextName, ");")
                    .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.DidNotMatchElse, ", formattedKeyword, "u8);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine();
        }
    }
}