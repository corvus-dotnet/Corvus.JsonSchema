// <copyright file="ContainsValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ArrayChildHandlers;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A items validation handler.
/// </summary>
public class ContainsValidationHandler : IChildArrayItemValidationHandler2, IJsonSchemaClassSetup
{
    private const string ValidationConfigurationKey = "ContainsValidationHandler_ValidationConfiguration";

    /// <summary>
    /// Gets the singleton instance of the <see cref="ContainsValidationHandler"/>.
    /// </summary>
    public static ContainsValidationHandler Instance { get; } = CreateDefaultInstance();

    private static ContainsValidationHandler CreateDefaultInstance()
    {
        return new();
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 1;

    public uint ItemHandlerPriority { get; } = ValidationPriorities.Composition;

    private class ValidationConfiguration
    {
        public ValidationConfiguration(string keyword, ArrayItemsTypeDeclaration arrayItemsTypeDeclaration, string containsEvaluationPathProperty, List<ContainsOperator> containsOperators)
        {
            Keyword = keyword;
            ArrayItemsTypeDeclaration = arrayItemsTypeDeclaration;
            ContainsEvaluationPathProperty = containsEvaluationPathProperty;
            ContainsOperators = containsOperators;
        }

        public ArrayItemsTypeDeclaration ArrayItemsTypeDeclaration { get; }

        public string ContainsEvaluationPathProperty { get; }

        public List<ContainsOperator> ContainsOperators { get; }

        public string Keyword { get; }

        public string ContainsCountVariableName { get => field ?? throw new InvalidOperationException("You must set the contains count variable name."); internal set; }
    }

    private class ContainsOperator
    {
        public int ConstantValue { get; internal set; }

        public Operator Operator { get; internal set; }
    }

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        string? containsEvaluationPathProperty = null;
        List<ContainsOperator> containsOperators = [];

        IArrayContainsValidationKeyword? keywordOrDefault = typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().FirstOrDefault();
        if (keywordOrDefault is IArrayContainsValidationKeyword containsKeyword)
        {
            if (containsKeyword.TryGetContainsItemType(typeDeclaration, out ArrayItemsTypeDeclaration? containsItemsTypeDeclaration))
            {
                containsEvaluationPathProperty = generator.GetPropertyNameInScope("SchemaEvaluationPath", prefix: containsKeyword.Keyword);
                generator
                    .AppendLineIndent(
                        "private static readonly JsonSchemaPathProvider<int> ",
                        containsEvaluationPathProperty, " = static (_, buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                        SymbolDisplay.FormatLiteral(containsItemsTypeDeclaration.Keyword.GetPathModifier(containsItemsTypeDeclaration), true),
                        "u8, buffer, out written);");

                foreach (IArrayContainsCountConstantValidationKeyword keyword in typeDeclaration.Keywords().OfType<IArrayContainsCountConstantValidationKeyword>())
                {
                    if (keyword.TryGetOperator(typeDeclaration, out Operator op) &&
                        keyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) &&
                        constants?.SingleOrDefault() is JsonElement constant &&
                        constant.ValueKind == JsonValueKind.Number)
                    {
                        if (!constant.TryGetInt32(out int value))
                        {
                            if (!constant.TryGetDouble(out double constantValueAsDouble))
                            {
                                throw new InvalidOperationException($"Expected constant value to be a number that can be represented as an int or double. Actual value: {constant}");
                            }

                            value = (int)constantValueAsDouble;
                        }

                        containsOperators.Add(new ContainsOperator { ConstantValue = value, Operator = op });
                    }
                }

                if (!containsOperators.Any(c => c.Operator is Operator.GreaterThan or Operator.GreaterThanOrEquals))
                {
                    containsOperators.Add(new ContainsOperator { ConstantValue = 0, Operator = Operator.GreaterThan });
                }

                typeDeclaration.SetMetadata(ValidationConfigurationKey, new ValidationConfiguration(containsKeyword.Keyword, containsItemsTypeDeclaration, containsEvaluationPathProperty, containsOperators));
            }
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

        validationConfiguration.ContainsCountVariableName = generator.GetUniqueVariableNameInScope("containsHandler_containsCount");

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("int ", validationConfiguration.ContainsCountVariableName, " = 0;");
    }

    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        foreach (ContainsOperator containsOperator in validationConfiguration.ContainsOperators)
        {
            generator
                .AppendStandardContainsCountOperator(
                    typeDeclaration,
                    validationConfiguration.Keyword,
                    validationConfiguration.ContainsCountVariableName,
                    containsOperator.ConstantValue,
                    containsOperator.Operator);
        }

        return generator;
    }

    public CodeGenerator AppendArrayItemValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(ValidationConfigurationKey, out ValidationConfiguration? validationConfiguration))
        {
            return generator;
        }

        string containsTypeName = validationConfiguration.ArrayItemsTypeDeclaration.ReducedType.FullyQualifiedDotnetTypeName();
        string containsJsonSchemaClassName = generator.JsonSchemaClassName(containsTypeName);
        string containsChildContextName = generator.GetUniqueVariableNameInScope("childContext");

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("JsonSchemaContext ", containsChildContextName, " = ", containsTypeName, ".", containsJsonSchemaClassName, ".PushChildContext(")
            .PushIndent()
                .AppendLineIndent("parentDocument,")
                .AppendLineIndent("arrayValidation_currentIndex,")
                .AppendLineIndent("ref context,")
                .AppendLineIndent("providerContext: arrayValidation_itemCount,")
                .AppendLineIndent("schemaEvaluationPath: ", validationConfiguration.ContainsEvaluationPathProperty, ",")
                .AppendLineIndent("documentEvaluationPath: JsonSchemaEvaluation.ItemIndex);")
            .PopIndent()
            .AppendSeparatorLine()
            .AppendLineIndent(containsTypeName, ".", containsJsonSchemaClassName, ".Evaluate(parentDocument, arrayValidation_currentIndex, ref ", containsChildContextName, ");")
            .AppendLineIndent("if (!", containsChildContextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.CommitChildContext(true, ref ", containsChildContextName, ");")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent(validationConfiguration.ContainsCountVariableName, "++;")
                .AppendLineIndent("context.CommitChildContext(true, ref ", containsChildContextName, ");")
                .AppendLineIndent("context.AddLocalEvaluatedItem(arrayValidation_itemCount);")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.Keywords().OfType<IArrayContainsValidationKeyword>().Any();
}

public static class ContainsValidationExtensions
{
    public static CodeGenerator AppendStandardContainsCountOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, string keyword, string containsCountVariableName, int constantValue, Operator op)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string expected = constantValue.ToString();
        string operatorFunction = op switch
        {
            Operator.Equals => "JsonSchemaEvaluation.MatchContainsCountEquals",
            Operator.NotEquals => "JsonSchemaEvaluation.MatchContainsCountNotEquals",
            Operator.LessThan => "JsonSchemaEvaluation.MatchContainsCountLessThan",
            Operator.LessThanOrEquals => "JsonSchemaEvaluation.MatchContainsCountLessThanOrEquals",
            Operator.GreaterThan => "JsonSchemaEvaluation.MatchContainsCountGreaterThan",
            Operator.GreaterThanOrEquals => "JsonSchemaEvaluation.MatchContainsCountGreaterThanOrEquals",
            _ => throw new InvalidOperationException($"Unsupported operator: {op}")
        };

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent(
                operatorFunction, "(",
                expected,
                ", ", containsCountVariableName, ", ",
                SymbolDisplay.FormatLiteral(keyword, true), "u8, ref context);");
    }
}