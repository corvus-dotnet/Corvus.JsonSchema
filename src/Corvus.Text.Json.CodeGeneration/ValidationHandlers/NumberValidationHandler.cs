// <copyright file="NumberValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.NumberChildHandlers;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IFormatValidationKeyword"/> capability.
/// </summary>
internal sealed class NumberValidationHandler : TypeSensitiveKeywordValidationHandlerBase, INumberKeywordValidationHandler
{
    private NumberValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="NumberValidationHandler"/>.
    /// </summary>
    public static NumberValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Default;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendNumberValidationSetup()
             .AppendChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority);
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IReadOnlyCollection<IChildValidationHandler> childHandlers = ChildHandlers;

        generator
            .AppendNumberValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority, validateOnly);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is INumberValidationKeyword;
    }

    private static NumberValidationHandler CreateDefault()
    {
        var result = new NumberValidationHandler();
        result
            .RegisterChildHandlers(
                NumberRangeValidationHandler.Instance);
        return result;
    }
}

file static class NumberValidationHandlerExtensions
{
    public static CodeGenerator AppendNumberValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendNumberValidation(
        this CodeGenerator generator,
        IKeywordValidationHandler parentHandler,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> childHandlers,
        uint validationPriority,
        bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!validateOnly)
        {
            generator
                .AppendSeparatorLine()
                .AppendStartTokenTypeCheck(typeDeclaration)
                .PushMemberScope("tokenTypeCheck", ScopeType.Method);
        }

        generator
            .PrependChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        if (!validateOnly)
        {
            // We only pop if we were in our own scope
            generator
                .PopNormalizedJsonNumberIfAppendedInScope(typeDeclaration)
                .PopMemberScope()
                .AppendEndTokenTypeCheck(typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendStartTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.Number)")
            .AppendLineIndent("{")
            .PushIndent();
    }

    private static CodeGenerator AppendEndTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .PopIndent()
            .AppendLineIndent("}");

        bool appended = false;

        generator
            .TryAppendIgnoredCoreTypeKeywords<INumberValidationKeyword>(
                typeDeclaration,
                "JsonSchemaEvaluation.IgnoredNotTypeNumber",
                static (g, _) =>
                {
                    g
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent();
                },
                ref appended);

        if (appended)
        {
            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }
}