// <copyright file="AnyOfSubschemaValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// An any-of subschema validation handler.
/// </summary>
public class AnyOfSubschemaValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="AnyOfSubschemaValidationHandler"/>.
    /// </summary>
    public static AnyOfSubschemaValidationHandler Instance { get; } = new();

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

        if (typeDeclaration.AnyOfCompositionTypes() is IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> subschemaDictionary)
        {
            foreach (IAnyOfSubschemaValidationKeyword keyword in subschemaDictionary.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                string localMethodName = generator.GetUniqueMethodNameInScope(keyword.Keyword, prefix: "Validate");

                generator
                    .AppendSeparatorLine();

                if (subschemaDictionary.Count > 1)
                {
                    generator
                        .AppendLineIndent("result = ", localMethodName, "(value, result, level);")
                        .AppendLineIndent("if (!result.IsValid && level == ValidationLevel.Flag)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return result;")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "static ValidationContext ",
                            localMethodName,
                            "(in ",
                            typeDeclaration.DotnetTypeName(),
                            " value, in ValidationContext validationContext, ValidationLevel level)")
                        .AppendLineIndent("{")
                        .PushIndent()
                        .AppendLineIndent("ValidationContext result = validationContext;");
                }

                IReadOnlyCollection<TypeDeclaration> subschemaTypes = subschemaDictionary[keyword];
                int i = 0;

                string foundValidName = generator.GetUniqueVariableNameInScope("FoundValid", prefix: keyword.Keyword);
                generator
                    .AppendLineIndent("bool ", foundValidName, " = false;");

                foreach (TypeDeclaration subschemaType in subschemaTypes)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    ReducedTypeDeclaration reducedType = subschemaType.ReducedTypeDeclaration();
                    string pathModifier = keyword.GetPathModifier(reducedType, i);
                    string contextName = generator.GetUniqueVariableNameInScope("ChildContext", prefix: keyword.Keyword, suffix: i.ToString());
                    string resultName = generator.GetUniqueVariableNameInScope("Result", prefix: keyword.Keyword, suffix: i.ToString());
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("ValidationContext ", contextName, " = validationContext.CreateChildContext();")
                        .AppendLineIndent("if (level > ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent(
                                contextName,
                                " = ",
                                contextName,
                                ".PushValidationLocationReducedPathModifier(new(",
                                SymbolDisplay.FormatLiteral(pathModifier, true),
                                "));")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "ValidationContext ",
                            resultName,
                            " = value.As<",
                            reducedType.ReducedType.FullyQualifiedDotnetTypeName(),
                            ">().Validate(",
                            contextName,
                            ", level);")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (", resultName, ".IsValid)")
                        .AppendLineIndent("{")
                        .PushIndent();

                    if (!typeDeclaration.RequiresItemsEvaluationTracking() &&
                        !typeDeclaration.RequiresPropertyEvaluationTracking())
                    {
                        generator
                            .AppendLineIndent("if (level == ValidationLevel.Flag)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("return result;")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent(
                                    "result = result.MergeChildContext(",
                                    resultName,
                                    ", level >= ValidationLevel.Verbose);")
                                .AppendLineIndent(foundValidName, " = true;")
                            .PopIndent()
                            .AppendLineIndent("}");
                    }
                    else
                    {
                        generator
                            .AppendLineIndent(
                                "result = result.MergeChildContext(",
                                resultName,
                                ", level >= ValidationLevel.Verbose);")
                            .AppendLineIndent(foundValidName, " = true;");
                    }

                    generator
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                            .AppendLineIndent("{")
                            .PushIndent()
                            .AppendLineIndent("result = result.MergeResults(result.IsValid, level, ", resultName, ");")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}");
                    i++;
                }

                generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (", foundValidName, ")")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: true, keyword, "result", "validated against the schema.", withKeyword: true)
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level == ValidationLevel.Flag)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.WithResult(isValid: false);")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", "did not validate against the schema.", withKeyword: true)
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine();

                if (subschemaDictionary.Count > 1)
                {
                    generator
                         .AppendLineIndent("return result;")
                         .PopIndent()
                         .AppendLineIndent("}");
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