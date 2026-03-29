// <copyright file="DependentSchemasChildHandler.cs" company="Endjin Limited">
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

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// Implements dependent schema handlers.
/// </summary>
internal class DependentSchemasChildHandler : INamedPropertyChildHandler
{
    internal const string EvaluationPathPropertiesKey = "DependentSchemasChildHandler_EvaluationPathProperties";
    internal const string DependentSchemaByPropertyNameKey = "DependentSchemasChildHandler_DependentSchemaByPropertyNameKey";

    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertiesValidationHandler"/>.
    /// </summary>
    public static DependentSchemasChildHandler Instance { get; } = CreateDefaultInstance();

    private static DependentSchemasChildHandler CreateDefaultInstance()
    {
        return new();
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority => ValidationPriorities.AfterComposition + 1000; // We are comparatively expensive, so we should go later

    /// <inheritdoc/>
    public bool EvaluatesProperty(PropertyDeclaration property) => false;

    /// <inheritdoc/>
    public bool AppendJsonSchemaClassSetupForProperty(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property)
    {
        if (!typeDeclaration.TryGetMetadata(DependentSchemaByPropertyNameKey, out Dictionary<string, DependentSchemaDeclaration>? declarationsByPropertyName) ||
            declarationsByPropertyName is null)
        {
            return false;
        }

        return declarationsByPropertyName.ContainsKey(property.JsonPropertyName);
    }

    /// <inheritdoc/>
    public void AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property)
    {
        if (!typeDeclaration.TryGetMetadata(DependentSchemaByPropertyNameKey, out Dictionary<string, DependentSchemaDeclaration>? declarationsByPropertyName) ||
            declarationsByPropertyName is null ||
            !declarationsByPropertyName.TryGetValue(property.JsonPropertyName, out DependentSchemaDeclaration? declaration)
            || declaration is null)
        {
            return;
        }

        string keywordAsQuotedString = SymbolDisplay.FormatLiteral(declaration.Keyword.Keyword, true);
        string propertyClassName = declaration.ReducedDepdendentSchemaType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(propertyClassName);
        string childContextName = generator.GetUniqueVariableNameInScope("childContext");
        string schemaEvaluationPathProviderName = GetSchemaEvaluationProviderName(typeDeclaration, declaration);
        string quotedPropertyName = SymbolDisplay.FormatLiteral(declaration.JsonPropertyName, true);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("JsonSchemaContext ", childContextName, " = ", propertyClassName, ".", jsonSchemaClassName, ".PushChildContext(")
            .PushIndent()
                .AppendLineIndent("parentDocument,")
                .AppendLineIndent("depdendentSchemasChildHandler_propertyParentDocumentIndex,")
                .AppendLineIndent("ref context,")
                .AppendLineIndent("schemaEvaluationPath: ", schemaEvaluationPathProviderName, ");")
            .PopIndent()
            .AppendSeparatorLine()
            .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, depdendentSchemasChildHandler_propertyParentDocumentIndex, ref ", childContextName, ");")
            .AppendSeparatorLine()
            .AppendLineIndent("if (!", childContextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.CommitChildContext(false, ref ", childContextName, ");")
                .AppendLineIndent("context.EvaluatedKeyword(false, ", quotedPropertyName, ", messageProvider: JsonSchemaEvaluation.ExpectedMatchesDependentSchema, ", keywordAsQuotedString, "u8);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLineIndent("else")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("context.ApplyEvaluated(ref ", childContextName, ");")
                .AppendLineIndent("context.CommitChildContext(true, ref ", childContextName, ");")
                .AppendLineIndent("context.EvaluatedKeyword(true, ", quotedPropertyName, ", messageProvider: JsonSchemaEvaluation.ExpectedMatchesDependentSchema, ", keywordAsQuotedString, "u8);")
            .PopIndent()
            .AppendLineIndent("}");

        static string GetSchemaEvaluationProviderName(TypeDeclaration typeDeclaration, DependentSchemaDeclaration declaration)
        {
            if (typeDeclaration.TryGetMetadata(EvaluationPathPropertiesKey, out Dictionary<DependentSchemaDeclaration, string>? evaluationPathProperties))
            {
                if (evaluationPathProperties.TryGetValue(declaration, out string? providerName))
                {
                    return providerName;
                }
            }

            throw new InvalidOperationException("Unable to find schema evaluation path provider for dependent schema.");
        }
    }

    /// <inheritdoc/>
    public void AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public void AppendValidatorArguments(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(DependentSchemaByPropertyNameKey, out Dictionary<string, DependentSchemaDeclaration>? declarationsByPropertyName) ||
            declarationsByPropertyName is null)
        {
            return;
        }

        // Pass in the parent document index
        generator
            .Append(", parentIndex");
    }

    /// <inheritdoc/>
    public void BeginJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        Dictionary<string, DependentSchemaDeclaration> declarationsByPropertyName = new(StringComparer.Ordinal);

        if (typeDeclaration.DependentSchemasSubschemaTypes()
           is IReadOnlyDictionary<IObjectPropertyDependentSchemasValidationKeyword, IReadOnlyCollection<DependentSchemaDeclaration>> dependentSchemas)
        {
            Dictionary<DependentSchemaDeclaration, string> evaluationPathProperties = [];

            foreach (IObjectPropertyDependentSchemasValidationKeyword keyword in dependentSchemas.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                int i = 0;

                foreach (DependentSchemaDeclaration dependentSchema in dependentSchemas[keyword])
                {
                    if (generator.IsCancellationRequested)
                    {
                        return;
                    }

                    string evaluationPathProperty = generator.GetUniqueStaticReadOnlyPropertyNameInScope($"{dependentSchema.Keyword.Keyword}{i++}", suffix: "SchemaEvaluationPath");
                    string keywordPathProperty = SymbolDisplay.FormatLiteral(dependentSchema.KeywordPathModifier, true);
                    evaluationPathProperties.Add(dependentSchema, evaluationPathProperty);
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "private static readonly JsonSchemaPathProvider ",
                            evaluationPathProperty,
                            " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(", keywordPathProperty, "u8, buffer, out written);");

                    declarationsByPropertyName.Add(dependentSchema.JsonPropertyName, dependentSchema);
                }
            }

            typeDeclaration.SetMetadata(EvaluationPathPropertiesKey, evaluationPathProperties);
            typeDeclaration.SetMetadata(DependentSchemaByPropertyNameKey, declarationsByPropertyName);
        }
    }

    /// <inheritdoc/>
    public void EndJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public IEnumerable<ObjectPropertyValidatorParameter> GetNamedPropertyValidatorParameters(TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(DependentSchemaByPropertyNameKey, out Dictionary<string, DependentSchemaDeclaration>? declarationsByPropertyName) ||
            declarationsByPropertyName is null)
        {
            return [];
        }

        return [
            new("int", "depdendentSchemasChildHandler_propertyParentDocumentIndex")
            ];
    }

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.DependentSchemasSubschemaTypes()?.Any() ?? false;

    public void AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { }
}