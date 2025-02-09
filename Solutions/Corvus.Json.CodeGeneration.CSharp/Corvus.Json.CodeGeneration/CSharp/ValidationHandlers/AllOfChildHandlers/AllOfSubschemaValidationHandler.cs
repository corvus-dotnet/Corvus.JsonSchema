﻿// <copyright file="AllOfSubschemaValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// An all-of subschema validation handler.
/// </summary>
public class AllOfSubschemaValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="AllOfSubschemaValidationHandler"/>.
    /// </summary>
    public static AllOfSubschemaValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.AllOfCompositionTypes() is IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> subschemaDictionary)
        {
            foreach (IAllOfSubschemaValidationKeyword keyword in subschemaDictionary.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                IReadOnlyCollection<TypeDeclaration> subschemaTypes = subschemaDictionary[keyword];
                int i = 0;
                foreach (TypeDeclaration subschemaType in subschemaTypes)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    ReducedTypeDeclaration reducedType = subschemaType.ReducedTypeDeclaration();
                    string pathModifier = keyword.GetPathModifier(reducedType, i);
                    string resultName = generator.GetUniqueVariableNameInScope("Result", prefix: keyword.Keyword, suffix: i.ToString());
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("ValidationContext ", resultName, " = childContextBase.CreateChildContext();")
                        .AppendLineIndent("if (level > ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent(
                                resultName,
                                " = ",
                                resultName,
                                ".PushValidationLocationReducedPathModifier(new(",
                                SymbolDisplay.FormatLiteral(pathModifier, true),
                                "));")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            resultName,
                            " = value.As<",
                            reducedType.ReducedType.FullyQualifiedDotnetTypeName(),
                            ">().Validate(",
                            resultName,
                            ", level);")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (!", resultName, ".IsValid)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.MergeChildContext(",
                                    resultName,
                                    ", true).PushValidationLocationProperty(",
                                    SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                                    ").WithResult(isValid: false, \"Validation - ",
                                    keyword.Keyword,
                                    " failed to validate against the schema.\").PopLocation();")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.MergeChildContext(",
                                    resultName,
                                    ", false).WithResult(isValid: false);")
                                .AppendLineIndent("return result;")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result = result.MergeChildContext(", resultName, ", level >= ValidationLevel.Detailed);")
                        .PopIndent()
                        .AppendLineIndent("}");
                    i++;
                }
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }
}