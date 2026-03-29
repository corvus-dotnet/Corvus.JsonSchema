// <copyright file="PropertySubschemaChildHandler.cs" company="Endjin Limited">
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

internal class PropertySubschemaChildHandler : INamedPropertyChildHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="PropertiesValidationHandler"/>.
    /// </summary>
    public static PropertySubschemaChildHandler Instance { get; } = CreateDefaultInstance();

    private static PropertySubschemaChildHandler CreateDefaultInstance()
    {
        return new();
    }

    /// <inheritdoc/>
    public uint ValidationHandlerPriority => ValidationPriorities.AfterComposition + 1000; // We are comparatively expensive, so we should go later

    /// <inheritdoc/>
    public bool EvaluatesProperty(PropertyDeclaration property) => property.LocalOrComposed == LocalOrComposed.Local && property.Keyword is IObjectPropertyValidationKeyword;

    /// <inheritdoc/>
    public bool AppendJsonSchemaClassSetupForProperty(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property)
    {
        if (property.LocalOrComposed == LocalOrComposed.Local && property.Keyword is IObjectPropertyValidationKeyword)
        {
            string evaluationPathProperty = generator.GetPropertyNameInScope($"{property.DotnetPropertyName()}SchemaEvaluationPath");
            string evaluationPath = SymbolDisplay.FormatLiteral(property.KeywordPathModifier, true);
            generator
                .AppendLineIndent(
                    "private static readonly JsonSchemaPathProvider ",
                    evaluationPathProperty, " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                    evaluationPath,
                    "u8, buffer, out written);");

            return true;
        }

        return false;
    }

    /// <inheritdoc/>
    public void AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, PropertyDeclaration property)
    {
        if (property.LocalOrComposed != LocalOrComposed.Local || property.Keyword is not IObjectPropertyValidationKeyword)
        {
            return;
        }

        string propertyName = property.DotnetPropertyName();
        string propertyClassName = property.ReducedPropertyType.FullyQualifiedDotnetTypeName();
        string jsonSchemaClassName = generator.JsonSchemaClassName(propertyClassName);
        string evaluationPathProperty = generator.GetPropertyNameInScope($"{property.DotnetPropertyName()}SchemaEvaluationPath");
        string jsonPropertyNamesClassName = generator.JsonPropertyNamesClassName();
        string childContextName = generator.GetUniqueVariableNameInScope("childContext");

        generator
        .AppendLineIndent("JsonSchemaContext ", childContextName, " =")
        .PushIndent()
            .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".PushChildContextUnescaped(")
            .PushIndent()
                .AppendLineIndent("parentDocument,")
                .AppendLineIndent("parentDocumentIndex,")
                .AppendLineIndent("ref context,")
                .AppendLineIndent(jsonPropertyNamesClassName, ".", propertyName, "Utf8,")
                .AppendLineIndent("evaluationPath: ", evaluationPathProperty, ");")
            .PopIndent()
        .PopIndent()
        .AppendSeparatorLine()
        .AppendLineIndent(propertyClassName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentDocumentIndex, ref ", childContextName, ");")
        .AppendLineIndent("context.CommitChildContext(", childContextName, ".IsMatch, ref ", childContextName, ");");
    }

    /// <inheritdoc/>
    public void AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public void AppendValidatorArguments(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public void BeginJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public void EndJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { }

    /// <inheritdoc/>
    public IEnumerable<ObjectPropertyValidatorParameter> GetNamedPropertyValidatorParameters(TypeDeclaration typeDeclaration) => [];

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration) => typeDeclaration.ExplicitProperties()?.Any(p => p.LocalOrComposed == LocalOrComposed.Local && p.Keyword is IObjectPropertyValidationKeyword) ?? false;

    public void AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration) { }
}