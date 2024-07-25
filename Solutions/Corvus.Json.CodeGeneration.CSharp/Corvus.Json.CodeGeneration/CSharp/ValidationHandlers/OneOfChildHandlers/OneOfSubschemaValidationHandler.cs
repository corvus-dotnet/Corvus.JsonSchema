// <copyright file="OneOfSubschemaValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A oneOf subschema validation handler.
/// </summary>
public class OneOfSubschemaValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="OneOfSubschemaValidationHandler"/>.
    /// </summary>
    public static OneOfSubschemaValidationHandler Instance { get; } = new();

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
        if (typeDeclaration.OneOfCompositionTypes() is IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> subschemaDictionary)
        {
            foreach (IOneOfSubschemaValidationKeyword keyword in subschemaDictionary.Keys)
            {
                string localMethodName = generator.GetUniqueMethodNameInScope(keyword.Keyword, prefix: "Validate");

                generator
                    .AppendSeparatorLine()
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

                IReadOnlyCollection<TypeDeclaration> subschemaTypes = subschemaDictionary[keyword];
                int i = 0;

                string foundValidName = generator.GetUniqueVariableNameInScope("FoundValid", prefix: keyword.Keyword);
                generator
                    .AppendLineIndent("int ", foundValidName, " = 0;");

                foreach (TypeDeclaration subschemaType in subschemaTypes)
                {
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
                        .PushIndent()
                        .AppendLineIndent(
                            "result = result.MergeChildContext(",
                            resultName,
                            ", level >= ValidationLevel.Verbose);")
                        .AppendLineIndent(foundValidName, "++;")
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
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent(
                                "result.PushValidationLocationProperty(",
                                SymbolDisplay.FormatLiteral(keyword.Keyword, true),
                                ");")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (", foundValidName, " == 1)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: true, keyword, "result", "validated against the schema.")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else if (", foundValidName, " > 1)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", "validated against more than 1 of the schema.")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.WithResult(isValid: false);")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", "did not validate against any of the schema.")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLineIndent("else")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("result = result.WithResult(isValid: false);")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (level >= ValidationLevel.Basic)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("result.PopLocation();")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("return result;")
                    .PopIndent()
                    .AppendLineIndent("}");
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