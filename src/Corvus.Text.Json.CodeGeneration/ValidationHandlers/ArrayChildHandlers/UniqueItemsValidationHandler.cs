// <copyright file="UniqueItemsValidationHandler.cs" company="Endjin Limited">
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
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A items validation handler.
/// </summary>
public class UniqueItemsValidationHandler : IChildArrayItemValidationHandler2, IJsonSchemaClassSetup
{
    private const string ValidationConfigurationKey = "UniqueItemsValidationHandler_ValidationConfiguration";

    /// <summary>
    /// Gets the singleton instance of the <see cref="UniqueItemsValidationHandler"/>.
    /// </summary>
    public static UniqueItemsValidationHandler Instance { get; } = CreateDefaultInstance();

    private static UniqueItemsValidationHandler CreateDefaultInstance()
    {
        return new();
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 1;

    public uint ItemHandlerPriority { get; } = ValidationPriorities.Composition;

    private class ValidationConfiguration
    {
        public ValidationConfiguration(string keyword)
        {
            Keyword = keyword;
        }

        public string Keyword { get; }

        public string HasUniqueItemsVariableName { get => field ?? throw new InvalidOperationException("You must set the uniqueItems count variable name."); internal set; }

        public string UniqueItemsHashSetVariableName { get => field ?? throw new InvalidOperationException("You must set the uniqueItems hash set variable name."); internal set; }
    }

    private class UniqueItemsOperator
    {
        public int ConstantValue { get; internal set; }

        public Operator Operator { get; internal set; }
    }

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        List<UniqueItemsOperator> uniqueItemsOperators = [];

        IUniqueItemsArrayValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IUniqueItemsArrayValidationKeyword>().FirstOrDefault(k => k.RequiresUniqueItems(typeDeclaration));
        if (keywordOrDefault is IUniqueItemsArrayValidationKeyword uniqueItemsKeyword)
        {
            typeDeclaration.SetMetadata(ValidationConfigurationKey, new ValidationConfiguration(uniqueItemsKeyword.Keyword));
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { return generator; }

    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        validationConfiguration.HasUniqueItemsVariableName = generator.GetUniqueVariableNameInScope("uniqueItemsHandlerHasUniqueItems");
        validationConfiguration.UniqueItemsHashSetVariableName = generator.GetUniqueVariableNameInScope("uniqueItemsHandlerHashSet");

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("bool ", validationConfiguration.HasUniqueItemsVariableName, " = true;")
            .AppendLineIndent(
                "using UniqueItemsHashSet ",
                validationConfiguration.UniqueItemsHashSetVariableName,
                " = new UniqueItemsHashSet(parentDocument, parentDocument.GetArrayLength(parentIndex), stackalloc int[UniqueItemsHashSet.StackAllocBucketSize], stackalloc byte[UniqueItemsHashSet.StackAllocEntrySize]);");
    }

    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("context.EvaluatedKeyword(", validationConfiguration.HasUniqueItemsVariableName, ", messageProvider: JsonSchemaEvaluation.ExpectedUniqueItems, ", SymbolDisplay.FormatLiteral(validationConfiguration.Keyword, true), "u8);");
        return generator;
    }

    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (", validationConfiguration.HasUniqueItemsVariableName, " && !", validationConfiguration.UniqueItemsHashSetVariableName, ".AddItemIfNotExists(arrayValidation_currentIndex))")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent(validationConfiguration.HasUniqueItemsVariableName, " = false;")
                .AppendNoCollectorBooleanFalseShortcutReturn()
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.Keywords().OfType<IUniqueItemsArrayValidationKeyword>().Any(k => k.RequiresUniqueItems(typeDeclaration));
}