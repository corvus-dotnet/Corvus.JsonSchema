// <copyright file="ItemsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Diagnostics;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ArrayChildHandlers;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A items validation handler.
/// </summary>
public class ItemsValidationHandler : IChildArrayItemValidationHandler2, IJsonSchemaClassSetup
{
    private const string ValidationConfigurationKey = "ItemsValidationHandler_ValidationConfiguration";

    /// <summary>
    /// Gets the singleton instance of the <see cref="ItemsValidationHandler"/>.
    /// </summary>
    public static ItemsValidationHandler Instance { get; } = CreateDefaultInstance();

    private static ItemsValidationHandler CreateDefaultInstance()
    {
        return new();
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 1;

    public uint ItemHandlerPriority => ValidationHandlerPriority;

    private class ValidationConfiguration
    {
        public ValidationConfiguration(string? tupleEvaluationPathProperty, string? unevaluatedItemsEvaluationPathProperty, string? nonTupleEvaluationPathProperty)
        {
            TupleEvaluationPathProperty = tupleEvaluationPathProperty;
            UnevaluatedItemsEvaluationPathProperty = unevaluatedItemsEvaluationPathProperty;
            NonTupleEvaluationPathProperty = nonTupleEvaluationPathProperty;
        }

        public string? TupleEvaluationPathProperty { get; }

        public string? UnevaluatedItemsEvaluationPathProperty { get; }

        public string? NonTupleEvaluationPathProperty { get; }
    }

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        string? tupleEvaluationPathProperty = null;
        string? unevaluatedItemsEvaluationPathProperty = null;
        string? nonTupleEvaluationPathProperty = null;

        if (typeDeclaration.ExplicitTupleType() is TupleTypeDeclaration ttd)
        {
            tupleEvaluationPathProperty = generator.GetPropertyNameInScope("SchemaEvaluationPath", prefix: ttd.Keyword.Keyword);
            generator
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    tupleEvaluationPathProperty, " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                    SymbolDisplay.FormatLiteral(ttd.Keyword.Keyword, true),
                    "u8, buffer, out written);");
        }

        if (typeDeclaration.ExplicitUnevaluatedItemsType() is ArrayItemsTypeDeclaration aitd)
        {
            unevaluatedItemsEvaluationPathProperty = generator.GetPropertyNameInScope("SchemaEvaluationPath", prefix: ((IKeyword)aitd.Keyword).Keyword);
            generator
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    unevaluatedItemsEvaluationPathProperty, " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                    SymbolDisplay.FormatLiteral(aitd.Keyword.GetPathModifier(aitd), true),
                    "u8, buffer, out written);");
        }

        if (typeDeclaration.ExplicitNonTupleItemsType() is ArrayItemsTypeDeclaration nonTupleItemsTypeDeclaration)
        {
            nonTupleEvaluationPathProperty = generator.GetPropertyNameInScope("SchemaEvaluationPath", prefix: ((IKeyword)nonTupleItemsTypeDeclaration.Keyword).Keyword);
            generator
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    nonTupleEvaluationPathProperty, " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                    SymbolDisplay.FormatLiteral(nonTupleItemsTypeDeclaration.Keyword.GetPathModifier(nonTupleItemsTypeDeclaration), true),
                    "u8, buffer, out written);");
        }

        if (tupleEvaluationPathProperty is not null ||
            unevaluatedItemsEvaluationPathProperty is not null ||
            nonTupleEvaluationPathProperty is not null)
        {
            typeDeclaration.SetMetadata(ValidationConfigurationKey, new ValidationConfiguration(tupleEvaluationPathProperty, unevaluatedItemsEvaluationPathProperty, nonTupleEvaluationPathProperty));
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { return generator; }

    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        bool hasTupleType = false;
        bool hasNonTupleItemsType = typeDeclaration.ExplicitNonTupleItemsType() is not null;
        bool hasExplicitUnevaluatedItemsType = typeDeclaration.ExplicitUnevaluatedItemsType() is not null;
        if (typeDeclaration.ExplicitTupleType() is TupleTypeDeclaration tupleTypeDeclaration)
        {
            hasTupleType = true;
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("switch (arrayValidation_itemCount)")
                .AppendLineIndent("{")
                .PushIndent();

            int index = 0;
            foreach (ReducedTypeDeclaration tuple in tupleTypeDeclaration.ItemsTypes)
            {
                string tupleTypeName = tuple.ReducedType.FullyQualifiedDotnetTypeName();
                string tupleJsonSchemaClassName = generator.JsonSchemaClassName(tupleTypeName);
                string tupleChildContextName = generator.GetUniqueVariableNameInScope("childContext");
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("case ", index.ToString(), ":")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("JsonSchemaContext ", tupleChildContextName, " = ", tupleTypeName, ".", tupleJsonSchemaClassName, ".PushChildContext(")
                        .PushIndent()
                            .AppendLineIndent("parentDocument,")
                            .AppendLineIndent("arrayValidation_currentIndex,")
                            .AppendLineIndent("ref context,")
                            .AppendLineIndent("itemIndex: arrayValidation_itemCount,")
                            .AppendLineIndent("evaluationPath: ", validationConfiguration.TupleEvaluationPathProperty!, ");")
                        .PopIndent()
                        .AppendSeparatorLine()
                        .AppendLineIndent(tupleTypeName, ".", tupleJsonSchemaClassName, ".Evaluate(parentDocument, arrayValidation_currentIndex, ref ", tupleChildContextName, ");")
                        .AppendLineIndent("if (!", tupleChildContextName, ".IsMatch)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("context.CommitChildContext(false, ref ", tupleChildContextName, ");")
                            .AppendNoCollectorShortcutReturn()
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("context.CommitChildContext(true, ref ", tupleChildContextName, ");")
                            .AppendLineIndent("context.AddLocalEvaluatedItem(arrayValidation_itemCount);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}");

                index++;
            }

            if (hasNonTupleItemsType || hasExplicitUnevaluatedItemsType)
            {
                generator
                    .AppendLineIndent("default:")
                    .AppendLineIndent("{")
                    .PushIndent();
            }
        }

        if (hasNonTupleItemsType)
        {
            ArrayItemsTypeDeclaration nonTuple = typeDeclaration.ExplicitNonTupleItemsType()!;
            Debug.Assert(nonTuple is not null);

            string nonTupleTypeName = nonTuple.ReducedType.FullyQualifiedDotnetTypeName();
            string nonTupleJsonSchemaClassName = generator.JsonSchemaClassName(nonTupleTypeName);
            string nonTupleChildContextName = generator.GetUniqueVariableNameInScope("childContext");
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("JsonSchemaContext ", nonTupleChildContextName, " = ", nonTupleTypeName, ".", nonTupleJsonSchemaClassName, ".PushChildContext(")
                .PushIndent()
                    .AppendLineIndent("parentDocument,")
                    .AppendLineIndent("arrayValidation_currentIndex,")
                    .AppendLineIndent("ref context,")
                    .AppendLineIndent("itemIndex: arrayValidation_itemCount,")
                    .AppendLineIndent("evaluationPath: ", validationConfiguration.NonTupleEvaluationPathProperty!, ");")
                .PopIndent()
                .AppendSeparatorLine()
                .AppendLineIndent(nonTupleTypeName, ".", nonTupleJsonSchemaClassName, ".Evaluate(parentDocument, arrayValidation_currentIndex, ref ", nonTupleChildContextName, ");")
                .AppendLineIndent("if (!", nonTupleChildContextName, ".IsMatch)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("context.CommitChildContext(false, ref ", nonTupleChildContextName, ");")
                    .AppendNoCollectorShortcutReturn()
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("context.CommitChildContext(true, ref ", nonTupleChildContextName, ");")
                    .AppendLineIndent("context.AddLocalEvaluatedItem(arrayValidation_itemCount);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        if (hasExplicitUnevaluatedItemsType)
        {
            ArrayItemsTypeDeclaration unevaluatedType = typeDeclaration.ExplicitUnevaluatedItemsType()!;
            Debug.Assert(unevaluatedType is not null);

            string unevaluatedTypeName = unevaluatedType.ReducedType.FullyQualifiedDotnetTypeName();
            string unevaluatedJsonSchemaClassName = generator.JsonSchemaClassName(unevaluatedTypeName);
            string unevaluatedChildContextName = generator.GetUniqueVariableNameInScope("childContext");
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (!context.HasLocalOrAppliedEvaluatedItem(arrayValidation_itemCount))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("JsonSchemaContext ", unevaluatedChildContextName, " = ", unevaluatedTypeName, ".", unevaluatedJsonSchemaClassName, ".PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("arrayValidation_currentIndex,")
                    .AppendLineIndent("ref context,")
                        .AppendLineIndent("itemIndex: arrayValidation_itemCount,")
                        .AppendLineIndent("evaluationPath: ", validationConfiguration.UnevaluatedItemsEvaluationPathProperty!, ");")
                    .PopIndent()
                    .AppendSeparatorLine()
                    .AppendLineIndent(unevaluatedTypeName, ".", unevaluatedJsonSchemaClassName, ".Evaluate(parentDocument, arrayValidation_currentIndex, ref ", unevaluatedChildContextName, ");")
                    .AppendLineIndent("if (!", unevaluatedChildContextName, ".IsMatch)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("context.CommitChildContext(false, ref ", unevaluatedChildContextName, ");")
                        .AppendNoCollectorShortcutReturn()
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("context.CommitChildContext(true, ref ", unevaluatedChildContextName, ");")
                        .AppendLineIndent("context.AddLocalEvaluatedItem(arrayValidation_itemCount);")
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");
        }

        if (hasTupleType)
        {
            if (hasNonTupleItemsType || hasExplicitUnevaluatedItemsType)
            {
                generator
                            .AppendLineIndent("break;")
                        .PopIndent()
                        .AppendLineIndent("}");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) =>
        typeDeclaration.ExplicitTupleType() is not null ||
        typeDeclaration.ExplicitNonTupleItemsType() is not null ||
        typeDeclaration.ExplicitUnevaluatedItemsType() is not null;
}