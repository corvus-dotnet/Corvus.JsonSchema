// <copyright file="StringValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.StringChildHandlers;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IStringValidationKeyword"/> capability.
/// </summary>
internal sealed class StringValidationHandler : TypeSensitiveKeywordValidationHandlerBase, IStringKeywordValidationHandler
{
    private StringValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="StringValidationHandler"/>.
    /// </summary>
    public static StringValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.Default;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // If we require string value validation, then we need to run the type validation after all the string value validation handlers have run, so that we can ignore the type validation if any of those handlers are present.
        return generator
             .PrependChildValidationSetup(typeDeclaration, ChildHandlers, ValidationHandlerPriority)
             .AppendStringValidationSetup()
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
            .AppendStringValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority, validateOnly);

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is IStringValidationKeyword;
    }

    private static StringValidationHandler CreateDefault()
    {
        var result = new StringValidationHandler();
        result
            .RegisterChildHandlers(
                StringRegularExpressionValidationHandler.Instance,
                StringLengthValidationHandler.Instance);
        return result;
    }
}

file static class StringValidationHandlerExtensions
{
    public static CodeGenerator AppendStringValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendStringValidation(
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
                .PopStringLengthIfAppendedInScope(typeDeclaration)
                .PopMemberScope()
                .AppendEndTokenTypeCheck(typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendStartTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.String)")
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
            .TryAppendIgnoredCoreTypeKeywords<IStringValidationKeyword>(
                typeDeclaration,
                "JsonSchemaEvaluation.IgnoredNotTypeString",
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