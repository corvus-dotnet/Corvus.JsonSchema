// <copyright file="UnevaluatedPropertyValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// An unevaluated property validation handler.
/// </summary>
public class UnevaluatedPropertyValidationHandler : IChildObjectPropertyValidationHandler2, IJsonSchemaClassSetup
{
    private const string LocalEvaluatedSchemaEvaluationPathKey = "UnevaluatedPropertyValidationHandler_LocalEvaluatedSchemaEvaluationPath";
    private const string LocalAndAppliedEvaluatedSchemaEvaluationPathKey = "UnevaluatedPropertyValidationHandler_LocalAndAppliedEvaluatedSchemaEvaluationPath";

    /// <summary>
    /// Gets the singleton instance of the <see cref="UnevaluatedPropertyValidationHandler"/>.
    /// </summary>
    public static UnevaluatedPropertyValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Last; // We must be last.

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) => throw new NotImplementedException();

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localEvaluatedProperty)
        {
            AppendLocalEvaluatedProperty(generator, typeDeclaration, localEvaluatedProperty);
        }

        if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedEvaluatedProperty)
        {
            AppendLocalAndAppliedEvaluatedProperty(generator, typeDeclaration, localAndAppliedEvaluatedProperty);
        }

        return generator;

        static void AppendLocalEvaluatedProperty(CodeGenerator generator, TypeDeclaration typeDeclaration, FallbackObjectPropertyType fallbackProperty)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            if (!typeDeclaration.TryGetMetadata(LocalEvaluatedSchemaEvaluationPathKey, out string? schemaEvaluationPathProviderName) || schemaEvaluationPathProviderName is null)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (!context.HasLocalEvaluatedProperty(objectValidation_propertyCount))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendFallbackValidation(fallbackProperty, schemaEvaluationPathProviderName)
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendLocalAndAppliedEvaluatedProperty(CodeGenerator generator, TypeDeclaration typeDeclaration, FallbackObjectPropertyType fallbackProperty)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            if (!typeDeclaration.TryGetMetadata(LocalAndAppliedEvaluatedSchemaEvaluationPathKey, out string? schemaEvaluationPathProviderName) || schemaEvaluationPathProviderName is null)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (!context.HasLocalOrAppliedEvaluatedProperty(objectValidation_propertyCount))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendFallbackValidation(fallbackProperty, schemaEvaluationPathProviderName)
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => true;

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localEvaluatedProperty)
        {
            string evaluationPathProperty = generator.GetUniqueStaticReadOnlyPropertyNameInScope(localEvaluatedProperty.Keyword.Keyword, suffix: "SchemaEvaluationPath");
            string keywordPathProperty = SymbolDisplay.FormatLiteral(localEvaluatedProperty.KeywordPathModifier, true);
            typeDeclaration.SetMetadata(LocalEvaluatedSchemaEvaluationPathKey, evaluationPathProperty);
            generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    evaluationPathProperty,
                    " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(", keywordPathProperty, "u8, buffer, out written);");
        }

        if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedEvaluatedProperty)
        {
            string evaluationPathProperty = generator.GetUniqueStaticReadOnlyPropertyNameInScope(localAndAppliedEvaluatedProperty.Keyword.Keyword, suffix: "SchemaEvaluationPath");
            string keywordPathProperty = SymbolDisplay.FormatLiteral(localAndAppliedEvaluatedProperty.KeywordPathModifier, true);
            typeDeclaration.SetMetadata(LocalAndAppliedEvaluatedSchemaEvaluationPathKey, evaluationPathProperty);
            generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    evaluationPathProperty,
                    " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(", keywordPathProperty, "u8, buffer, out written);");
        }

        return generator;
    }

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is not null || typeDeclaration.LocalEvaluatedPropertyType() is not null;
}

file static class UnevaluatedPropertyValidationExtensions
{
    public static CodeGenerator AppendFallbackValidation(this CodeGenerator generator, FallbackObjectPropertyType fallbackProperty, string schemaEvaluationPathProviderName)
    {
        string keywordAsQuotedString = SymbolDisplay.FormatLiteral(fallbackProperty.Keyword.Keyword, true);

        string propertyClassName = fallbackProperty.ReducedType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(propertyClassName);

        string childContextName = generator.GetUniqueVariableNameInScope("childContext");

        return generator
            .AppendLineIndent("JsonSchemaContext ", childContextName, " = ", propertyClassName, ".", jsonSchemaClassName, ".PushChildContextUnescaped(")
            .PushIndent()
                .AppendLineIndent("parentDocument,")
                .AppendLineIndent("objectValidation_currentIndex,")
                .AppendLineIndent("ref context,")
                .AppendLineIndent("objectValidation_unescapedPropertyName.Span,")
                .AppendLineIndent("evaluationPath: ", schemaEvaluationPathProviderName, ");")
            .PopIndent()
            .AppendSeparatorLine()
            .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, objectValidation_currentIndex, ref ", childContextName, ");")
            .AppendSeparatorLine()
            .AppendLineIndent("if (!", childContextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.CommitChildContext(false, ref ", childContextName, ");")
                .AppendLineIndent("context.EvaluatedKeyword(false, messageProvider: JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema, ", keywordAsQuotedString, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.CommitChildContext(true, ref ", childContextName, ");")
                .AppendLineIndent("context.AddLocalEvaluatedProperty(objectValidation_propertyCount);")
                .AppendLineIndent("context.EvaluatedKeyword(true, messageProvider: JsonSchemaEvaluation.ExpectedPropertyMatchesFallbackSchema, ", keywordAsQuotedString, "u8);")
            .PopIndent()
            .AppendLineIndent("}");
    }
}