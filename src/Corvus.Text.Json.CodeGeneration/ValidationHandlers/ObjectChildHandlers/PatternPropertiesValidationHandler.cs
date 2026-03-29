// <copyright file="PatternPropertiesValidationHandler.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A pattern-properties validation handler.
/// </summary>
public class PatternPropertiesValidationHandler : IChildObjectPropertyValidationHandler2, IJsonSchemaClassSetup
{
    private const string EvaluationPathPropertiesKey = "PatternPropertiesValidationHandler_EvaluationPathProperties";

    /// <summary>
    /// Gets the singleton instance of the <see cref="PatternPropertiesValidationHandler"/>.
    /// </summary>
    public static PatternPropertiesValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.AfterComposition + 100; // We are not so cheap!

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
        if (typeDeclaration.TryGetMetadata(EvaluationPathPropertiesKey, out Dictionary<string, List<string>>? evaluationPathProperties) &&
            evaluationPathProperties is not null &&
            typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternProperties.Values)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                int index = 1;
                bool hasIndex = patternPropertyCollection.Count > 1;
                foreach (PatternPropertyDeclaration patternProperty in patternPropertyCollection)
                {
                    generator
                        .AppendPatternPropertyValidation(typeDeclaration, patternProperty, hasIndex ? index : null, evaluationPathProperties);
                    ++index;
                }
            }
        }

        return generator;
    }

    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => typeDeclaration.PatternProperties() is not null;

    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            Dictionary<string, List<string>> evaluationPathProperties = [];

            foreach (KeyValuePair<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternPropertyForKeyword in patternProperties)
            {
                int index = 1;
                int count = patternPropertyForKeyword.Value.Count;
                foreach (PatternPropertyDeclaration value in patternPropertyForKeyword.Value)
                {
                    string schemaPath = value.KeywordPathModifier;
                    string evaluationPathProperty = generator.GetPropertyNameInScope($"{value.Keyword.Keyword}{(count > 1 ? index.ToString() : "")}SchemaEvaluationPath");
                    index++;
                    AddEvaluationPathProperty(evaluationPathProperties, value.Keyword.Keyword, evaluationPathProperty);
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "private static readonly JsonSchemaPathProvider ",
                            evaluationPathProperty,
                            " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(", SymbolDisplay.FormatLiteral(value.KeywordPathModifier, true), "u8, buffer, out written);");
                }
            }

            typeDeclaration.SetMetadata(EvaluationPathPropertiesKey, evaluationPathProperties);
        }

        return generator;

        static void AddEvaluationPathProperty(Dictionary<string, List<string>> evaluationPathProperties, string keyword, string evaluationPathProperty)
        {
            if (!evaluationPathProperties.TryGetValue(keyword, out List<string>? propertiesForKeyword))
            {
                propertiesForKeyword = [];
                evaluationPathProperties.Add(keyword, propertiesForKeyword);
            }

            propertiesForKeyword.Add(evaluationPathProperty);
        }
    }

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.PatternProperties() is not null;
}

file static class PatternPropertiesValidationExtensions
{
    public static CodeGenerator AppendPatternPropertyValidation(this CodeGenerator generator, TypeDeclaration typeDeclaration, PatternPropertyDeclaration property, int? index, Dictionary<string, List<string>> evaluationPathProperties)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string keywordString = SymbolDisplay.FormatLiteral(property.Keyword.Keyword, true);
        string propertyClassName = property.ReducedPatternPropertyType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(propertyClassName);
        string childContextName = generator.GetUniqueVariableNameInScope("childContext");
        string pattern = SymbolDisplay.FormatLiteral(property.Pattern, true);

        string evaluationPathProperty = evaluationPathProperties[property.Keyword.Keyword][index.HasValue ? index.Value - 1 : 0];

        RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(property.Pattern);

        generator
            .AppendSeparatorLine();

        if (category == RegexPatternCategory.Noop)
        {
            // Noop patterns always match — emit body without an if-guard.
            generator
                .AppendLineIndent("// Pattern ", pattern, " always matches.")
                .AppendPatternPropertyValidationBody(
                    propertyClassName, jsonSchemaClassName, childContextName, pattern, keywordString, evaluationPathProperty);
        }
        else
        {
            string condition = BuildPatternPropertyCondition(generator, property, index, category);

            generator
                .AppendLineIndent("if (", condition, ")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendPatternPropertyValidationBody(
                        propertyClassName, jsonSchemaClassName, childContextName, pattern, keywordString, evaluationPathProperty)
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendPatternPropertyValidationBody(
        this CodeGenerator generator,
        string propertyClassName,
        string jsonSchemaClassName,
        string childContextName,
        string pattern,
        string keywordString,
        string evaluationPathProperty)
    {
        return generator
            .AppendLineIndent("context.AddLocalEvaluatedProperty(objectValidation_propertyCount);")
            .AppendLineIndent("JsonSchemaContext ", childContextName, " =")
            .PushIndent()
                .AppendLineIndent("PushChildContextUnescaped(")
                .PushIndent()
                    .AppendLineIndent("parentDocument,")
                    .AppendLineIndent("objectValidation_currentIndex,")
                    .AppendLineIndent("ref context,")
                    .AppendLineIndent("objectValidation_unescapedPropertyName.Span,")
                    .AppendLineIndent("evaluationPath: ", evaluationPathProperty, ");")
                .PopIndent()
            .PopIndent()
            .AppendSeparatorLine()
            .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, objectValidation_currentIndex, ref ", childContextName, ");")
            .AppendLineIndent("context.EvaluatedKeyword(context.IsMatch, ", pattern, ", messageProvider: JsonSchemaEvaluation.ExpectedMatchPatternPropertySchema, ", keywordString, "u8);")
            .AppendLineIndent("context.CommitChildContext(", childContextName, ".IsMatch, ref ", childContextName, ");");
    }

    private static string BuildPatternPropertyCondition(CodeGenerator generator, PatternPropertyDeclaration property, int? index, RegexPatternCategory category)
    {
        switch (category)
        {
            case RegexPatternCategory.NonEmpty:
                return "objectValidation_unescapedPropertyName.Span.Length > 0";

            case RegexPatternCategory.Prefix:
            {
                string prefix = CodeGenerationExtensions.ExtractRegexPrefix(property.Pattern);
                string prefixLiteral = SymbolDisplay.FormatLiteral(prefix, true);
                return $"objectValidation_unescapedPropertyName.Span.StartsWith({prefixLiteral}u8)";
            }

            case RegexPatternCategory.Range:
            {
                (int min, int max) = CodeGenerationExtensions.ExtractRegexRange(property.Pattern);
                return $"JsonSchemaEvaluation.MatchRangeRegularExpression(objectValidation_unescapedPropertyName.Span, {min}, {max})";
            }

            default:
            {
                string regexAccessor =
                    generator.GetStaticReadOnlyFieldNameInScope(
                        property.Keyword.Keyword,
                        rootScope: generator.JsonSchemaClassScope(),
                        suffix: index?.ToString());
                return $"JsonSchemaEvaluation.MatchRegularExpression(objectValidation_unescapedPropertyName.Span, {regexAccessor})";
            }
        }
    }
}