// <copyright file="AnyOfConstValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// An any-of const validation handler.
/// </summary>
public class AnyOfConstValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="AnyOfConstValidationHandler"/>.
    /// </summary>
    public static AnyOfConstValidationHandler Instance { get; } = new();

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
        if (typeDeclaration.AnyOfConstantValues() is IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> constDictionary)
        {
            foreach (IAnyOfConstantValidationKeyword keyword in constDictionary.Keys)
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

                IReadOnlyCollection<JsonElement> constValues = constDictionary[keyword];

                string foundValidName = generator.GetUniqueVariableNameInScope("FoundValid", prefix: keyword.Keyword);
                generator
                    .AppendLineIndent("bool ", foundValidName, " = false;");

                int count = constValues.Count;
                for (int i = 1; i <= count; ++i)
                {
                    string pathModifier = keyword.GetPathModifier(i);
                    string constField =
                        generator.GetPropertyNameInScope(
                            keyword.Keyword,
                            rootScope: generator.ValidationClassScope(),
                            suffix: count > 1 ? i.ToString() : null);

                    if (i > 1)
                    {
                        generator
                            .AppendLineIndent("if (!", foundValidName, ")")
                            .AppendLineIndent("{")
                            .PushIndent();
                    }

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent(foundValidName, " = value.Equals(", generator.ValidationClassName(), ".", constField, ");");

                    if (i > 1)
                    {
                        generator
                            .PopIndent()
                            .AppendLineIndent("}");
                    }
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
                        .AppendLineIndent("if (", foundValidName, ")")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("if (level >= ValidationLevel.Verbose)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendKeywordValidationResult(isValid: true, keyword, "result", "validated against the enumeration.")
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
                                .AppendKeywordValidationResult(isValid: false, keyword, "result", "did not validate against the enumeration.")
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