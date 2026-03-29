// <copyright file="CompositionNotValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="ICompositionNotValidationKeyword"/> capability.
/// </summary>
internal sealed class CompositionNotValidationHandler : KeywordValidationHandlerBase
{
    private CompositionNotValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="CompositionNotValidationHandler"/>.
    /// </summary>
    public static CompositionNotValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Composition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendCompositionNotValidationSetup()
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
            .AppendCompositionNotValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is INotValidationKeyword;
    }

    private static CompositionNotValidationHandler CreateDefault()
    {
        return new CompositionNotValidationHandler();
    }
}

file static class CompositionNotValidationHandlerExtensions
{
    public static CodeGenerator AppendCompositionNotValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendCompositionNotValidation(
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

        IEnumerable<INotValidationKeyword> keywords =
            typeDeclaration.Keywords()
                .OfType<INotValidationKeyword>();

        bool requiresShortCut = false;

        foreach (INotValidationKeyword keyword in keywords)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (keyword.TryGetNotType(typeDeclaration, out ReducedTypeDeclaration? value) &&
                value is ReducedTypeDeclaration notType)
            {
                if (requiresShortCut)
                {
                    generator
                        .AppendNoCollectorNoMatchShortcutReturn();
                }

                string localContextName = generator.GetUniqueVariableNameInScope("Context", prefix: keyword.Keyword);

                string evaluationPathProperty = generator.GetPropertyNameInScope($"{keyword.Keyword}SchemaEvaluationPath");
                string targetTypeName = notType.ReducedType.FullyQualifiedDotnetTypeName();
                string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("JsonSchemaContext ", localContextName, " =")
                    .PushIndent()
                    .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ", evaluationPathProperty, ");")
                    .PopIndent()
                    .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentIndex, ref ", localContextName, ");");

                string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (", localContextName, ".IsMatch)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("context.CommitChildContext(false, ref ", localContextName, ");")
                        .AppendLineIndent("context.EvaluatedKeyword(false, JsonSchemaEvaluation.MatchedNotSchema, ", formattedKeyword, "u8);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("context.CommitChildContext(true, ref ", localContextName, ");")
                        .AppendLineIndent("context.EvaluatedKeyword(true, JsonSchemaEvaluation.DidNotMatchNotSchema, ", formattedKeyword, "u8);")
                    .PopIndent()
                    .AppendLineIndent("}");

                requiresShortCut = true;
            }
        }

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        return generator;
    }
}