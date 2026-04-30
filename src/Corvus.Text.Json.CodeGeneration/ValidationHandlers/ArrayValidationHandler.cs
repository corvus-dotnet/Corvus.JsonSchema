// <copyright file="ArrayValidationHandler.cs" company="Endjin Limited">
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
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ArrayChildHandlers;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IArrayValidationKeyword"/> capability.
/// </summary>
internal sealed class ArrayValidationHandler : TypeSensitiveKeywordValidationHandlerBase, IArrayKeywordValidationHandler, IJsonSchemaClassSetup
{
    private ArrayValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="ArrayValidationHandler"/>.
    /// </summary>
    public static ArrayValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
             .AppendArrayValidationSetup();
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
            .AppendArrayValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority, validateOnly);

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IChildValidationHandler childHandler in ChildHandlers)
        {
            if (childHandler is IJsonSchemaClassSetup classSetup)
            {
                classSetup.AppendJsonSchemaClassSetup(generator, typeDeclaration);
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is IArrayValidationKeyword;
    }

    private static ArrayValidationHandler CreateDefault()
    {
        var result = new ArrayValidationHandler();
        result
            .RegisterChildHandlers(
                ItemCountValidationHandler.Instance,
                ItemsValidationHandler.Instance,
                ContainsValidationHandler.Instance,
                UniqueItemsValidationHandler.Instance);
        return result;
    }
}

file static class ArrayValidationHandlerExtensions
{
    public static CodeGenerator AppendArrayValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendArrayValidation(
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
            .AppendSeparatorLine();

        foreach (IChildValidationHandler child in childHandlers)
        {
            child.AppendValidationSetup(generator, typeDeclaration);
        }

        generator
            .PrependChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        bool requiresArrayEnumeration = childHandlers
                    .OfType<IChildArrayItemValidationHandler2>()
                    .Any(child => child.WillEmitCodeFor(typeDeclaration));

        if (requiresArrayEnumeration ||
            typeDeclaration.RequiresArrayLength())
        {
            generator.ReserveName("arrayValidation_itemCount");

            if (requiresArrayEnumeration)
            {
                generator.AppendLineIndent("int arrayValidation_itemCount = 0;");
            }
            else
            {
                generator.AppendLineIndent("int arrayValidation_itemCount = parentDocument.GetArrayLength(parentIndex);");
            }
        }

        if (requiresArrayEnumeration)
        {
            generator
                .AppendSeparatorLine()
                .ReserveName("arrayValidation_enumerator")
                .AppendLineIndent("var arrayValidation_enumerator = new ArrayEnumerator(parentDocument, parentIndex);")
                .AppendLineIndent("while (arrayValidation_enumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .ReserveName("arrayValidation_currentIndex")
                    .AppendLineIndent("int arrayValidation_currentIndex = arrayValidation_enumerator.CurrentIndex;");

            foreach (IChildArrayItemValidationHandler child in childHandlers.OfType<IChildArrayItemValidationHandler>().OrderBy(c => c is IChildArrayItemValidationHandler2 c2 ? c2.ItemHandlerPriority : c.ValidationHandlerPriority))
            {
                child.AppendArrayItemValidationCode(generator, typeDeclaration);
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("arrayValidation_itemCount++;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        if (!validateOnly)
        {
            // We only pop if we were in our own scope
            generator
                .PopMemberScope()
                .AppendEndTokenTypeCheck(typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendStartTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.StartArray)")
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
            .TryAppendIgnoredCoreTypeKeywords<IArrayValidationKeyword>(
                typeDeclaration,
                "JsonSchemaEvaluation.IgnoredNotTypeArray",
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