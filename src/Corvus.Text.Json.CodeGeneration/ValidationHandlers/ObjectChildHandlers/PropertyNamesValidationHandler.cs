// <copyright file="PropertyNamesValidationHandler.cs" company="Endjin Limited">
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
/// A property name validation handler.
/// </summary>
public class PropertyNamesValidationHandler : IChildObjectPropertyValidationHandler2, IJsonSchemaClassSetup
{
    private const string EvaluationPathPropertyKey = "PropertyNameValidationHandler_EvaluationPathProperty";

    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertyNamesValidationHandler"/>.
    /// </summary>
    public static PropertyNamesValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 10; // We want to go immediately after the higher priority items as we are very cheap

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
        if (typeDeclaration.TryGetMetadata(EvaluationPathPropertyKey, out string? evaluationPathProperty) &&
            evaluationPathProperty is not null &&
            typeDeclaration.PropertyNamesSubschemaType() is SingleSubschemaKeywordTypeDeclaration propertyNameType)
        {
            string keywordString = SymbolDisplay.FormatLiteral(propertyNameType.Keyword.Keyword, true);
            string propertyClassName = propertyNameType.ReducedType.FullyQualifiedDotnetTypeName();
            string jsonSchemaClassName = generator.JsonSchemaClassName(propertyClassName);
            string childContextName = generator.GetUniqueVariableNameInScope("childContext");
            string fixedJsonStringName = generator.GetUniqueVariableNameInScope("fixedJsonString");

            generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "using (var ", fixedJsonStringName, " = FixedStringJsonDocument<",
                    propertyClassName,
                    ">.Parse(parentDocument.GetPropertyNameRaw(objectValidation_currentIndex, true), parentDocument.ValueIsEscaped(objectValidation_currentIndex, true)))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("JsonSchemaContext ", childContextName, " =")
                    .PushIndent()
                        .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".PushChildContext(")
                        .PushIndent()
                            .AppendLineIndent(fixedJsonStringName, ",")
                            .AppendLineIndent("0,")
                            .AppendLineIndent("ref context,")
                            .AppendLineIndent("schemaEvaluationPath: ", evaluationPathProperty, ");")
                        .PopIndent()
                    .PopIndent()
                    .AppendSeparatorLine()
                    .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".Evaluate(", fixedJsonStringName, ", 0, ref ", childContextName, ");")
                    .AppendLineIndent("context.CommitChildContext(", childContextName, ".IsMatch, ref ", childContextName, ");")
                    .AppendNoCollectorNoMatchShortcutReturn()
                    .AppendSeparatorLine()
                    .AppendLineIndent("context.EvaluatedKeyword(context.IsMatch, messageProvider: JsonSchemaEvaluation.ExpectedPropertyNameMatchesSchema, ", keywordString, "u8);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => false;

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.PropertyNamesSubschemaType() is not null;

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.PropertyNamesSubschemaType() is SingleSubschemaKeywordTypeDeclaration propertyNameType)
        {
            string evaluationPathProperty = generator.GetPropertyNameInScope(propertyNameType.Keyword.Keyword, suffix: "SchemaEvaluationPath");
            typeDeclaration.SetMetadata(EvaluationPathPropertyKey, evaluationPathProperty);
            return generator
                .AppendSeparatorLine()
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    evaluationPathProperty,
                    " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(", SymbolDisplay.FormatLiteral(propertyNameType.KeywordPathModifier, true), "u8, buffer, out written);");
        }

        return generator;
    }
}

public static class PropertyNameValidationExtensions;