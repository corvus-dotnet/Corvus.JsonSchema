// <copyright file="CodeGeneratorExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.Markdown;

/// <summary>
/// Extensions for the <see cref="MarkdownLanguageProvider"/>.
/// </summary>
public static class CodeGeneratorExtensions
{
    /// <summary>
    /// Append the title of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendTitle(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendLine("# ", typeDeclaration.LocatedSchema.Location.ToString());
    }

    /// <summary>
    /// Appends information from analysing the core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendArrayAnalysis(typeDeclaration)
            .AppendObjectAnalysis(typeDeclaration)
            .AppendStringAnalysis(typeDeclaration)
            .AppendNumberAnalysis(typeDeclaration)
            .AppendCompositionAnalysis(typeDeclaration);
    }

    /// <summary>
    /// Appends information from analysing an array type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendArrayAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <summary>
    /// Appends information from analysing an object type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendObjectAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <summary>
    /// Appends information from analysing a string type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendStringAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        HashSet<IKeyword> visitedKeywords = [];

        return generator
            .AppendStringLengthAnalysis(typeDeclaration, visitedKeywords)
            .AppendPatternAnalysis(typeDeclaration, visitedKeywords);
    }

    /// <summary>
    /// Appends information from analysing a number type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendNumberAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <summary>
    /// Appends information from analysing a number type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendCompositionAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <summary>
    /// Appends the information about core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendCoreTypes(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator.AppendIndent("**Core types: **");
        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypesOrAny();
        CoreTypes allowedCoreTypes = typeDeclaration.AllowedCoreTypes();

        // If any allowed core types are specified, limit the implied core types to those allowed.
        if ((allowedCoreTypes & CoreTypes.Any) != 0)
        {
            impliedCoreTypes = impliedCoreTypes & allowedCoreTypes;
        }

        bool isFirst = true;

        if ((impliedCoreTypes & CoreTypes.Object) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Object) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.Object,
                    "object",
                    "https://json-schema.org/understanding-json-schema/reference/object");
        }

        if ((impliedCoreTypes & CoreTypes.Array) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Array) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.Array,
                    "array",
                    "https://json-schema.org/understanding-json-schema/reference/array");
        }

        if ((impliedCoreTypes & CoreTypes.String) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.String) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.String,
                    "string",
                    "https://json-schema.org/understanding-json-schema/reference/string");
        }

        if ((impliedCoreTypes & CoreTypes.Number) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Number) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.Number,
                    "number",
                    "https://json-schema.org/understanding-json-schema/reference/number");
        }

        if ((impliedCoreTypes & CoreTypes.Integer) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Integer) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.Integer,
                    "integer",
                    "https://json-schema.org/understanding-json-schema/reference/integer");
        }

        if ((impliedCoreTypes & CoreTypes.Boolean) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Boolean) != 0;
            isFirst =
                generator.AppendCoreType(
                    isRequired,
                    isFirst,
                    CoreTypes.Boolean,
                    "boolean",
                    "https://json-schema.org/understanding-json-schema/reference/boolean");
        }

        if ((impliedCoreTypes & CoreTypes.Null) != 0)
        {
            bool isRequired = (allowedCoreTypes & CoreTypes.Null) != 0;
            generator.AppendCoreType(
                isRequired,
                isFirst,
                CoreTypes.Null,
                "null",
                "https://json-schema.org/understanding-json-schema/reference/null");
        }

        return generator;
    }

    /// <summary>
    /// Append the documentation text for the type declaration.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator after the operation has completed.</returns>
    public static CodeGenerator AppendDocumentation(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        AdditionalSchemaDocumentation.Details? details = typeDeclaration.GetDocumentationDetails("#"u8);

        if (typeDeclaration.ShortDocumentation() is string shortDocumentation)
        {
            generator
                .AppendLine()
                .AppendBlockIndent(shortDocumentation);
        }

        if (details?.Title is JsonString title)
        {
            generator
                .AppendLine()
                .AppendBlockIndent((string)title);
        }

        if (typeDeclaration.LongDocumentation() is string longDocumentation)
        {
            generator
                .AppendLine()
                .AppendBlockIndent(longDocumentation);
        }

        if (details?.Description is JsonString description)
        {
            generator
                .AppendLine()
                .AppendBlockIndent((string)description);
        }

        return generator;
    }

    private static CodeGenerator AppendOperator(this CodeGenerator generator, Operator op)
    {
        return op switch
        {
            Operator.Equals => generator.Append(" == "),
            Operator.NotEquals => generator.Append(" != "),
            Operator.LessThan => generator.Append(" < "),
            Operator.LessThanOrEquals => generator.Append(" <= "),
            Operator.GreaterThan => generator.Append(" > "),
            Operator.GreaterThanOrEquals => generator.Append(" >= "),
            Operator.MultipleOf => generator.Append(" is multiple of "),
            Operator.None => throw new InvalidOperationException("The operator 'None' is not permitted."),
            _ => generator,
        };
    }

    private static CodeGenerator AppendSectionTitle(this CodeGenerator generator, string title)
    {
        return generator
            .AppendLineIndent("## ", title);
    }

    private static CodeGenerator AppendSubsectionTitle(this CodeGenerator generator, string title)
    {
        return generator
            .AppendLineIndent("### ", title);
    }

    private static CodeGenerator AppendBlockIndent(this CodeGenerator generator, string text, string? prefix = null)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(text, false);
        bool isFirst = true;
        foreach (string line in lines)
        {
            if (!isFirst)
            {
                generator.AppendLine();
            }
            else
            {
                isFirst = false;
            }

            if (prefix is string p)
            {
                generator
                    .AppendIndent(p);
            }

            generator
                .AppendLineIndent(line);
        }

        return generator;
    }

    private static string[] NormalizeAndSplitBlockIntoLines(string block, bool removeBlankLines = false)
    {
        string normalizedBlock = block.Replace("\r\n", "\n");
        string[] lines = normalizedBlock.Split(['\n'], removeBlankLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
        return lines;
    }

    private static bool AppendCoreType(this CodeGenerator generator, bool isRequired, bool isFirst, CoreTypes coreType, string coreTypeName, string documentationLink)
    {
        if (isFirst)
        {
            isFirst = false;
        }
        else
        {
            generator.Append(", ");
        }

        if (!isRequired)
        {
            generator
                .Append("*");
        }

        generator
            .Append("[")
            .Append(coreTypeName)
            .Append("](")
            .Append(documentationLink);

        if (!isRequired)
        {
            generator
                .Append(" \"implied\")*");
        }
        else
        {
            generator
                .Append(")");
        }

        return isFirst;
    }

    private static CodeGenerator AppendStringLengthAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration, HashSet<IKeyword> visitedKeywords)
    {
        foreach (IStringLengthConstantValidationKeyword stringLengthValidationKeyword in typeDeclaration.Keywords().OfType<IStringLengthConstantValidationKeyword>())
        {
            visitedKeywords.Add(stringLengthValidationKeyword);

            if (!stringLengthValidationKeyword.TryGetOperator(typeDeclaration, out Operator op))
            {
                throw new InvalidOperationException($"The string length keyword {stringLengthValidationKeyword.Keyword} did not have an operator");
            }

            if (!stringLengthValidationKeyword.TryGetValidationConstants(typeDeclaration, out JsonElement[]? constants) ||
                constants?.Length != 1 ||
                constants[0].ValueKind != JsonValueKind.Number)
            {
                throw new InvalidOperationException($"The string length keyword {stringLengthValidationKeyword.Keyword} did not have a single numeric constant value.");
            }

            string pathModifier = stringLengthValidationKeyword.GetPathModifier();

            generator
                .AppendDocumentationDetails(typeDeclaration, pathModifier)
                .Append("length ")
                .AppendOperator(op)
                .Append(constants[0].GetRawText())
                .AppendLine(" *", pathModifier, "*");
        }

        return generator;
    }

    private static CodeGenerator AppendPatternAnalysis(this CodeGenerator generator, TypeDeclaration typeDeclaration, HashSet<IKeyword> visitedKeywords)
    {
        foreach (IStringRegexValidationProviderKeyword stringRegexValidationKeyword in typeDeclaration.Keywords().OfType<IStringRegexValidationProviderKeyword>())
        {
            visitedKeywords.Add(stringRegexValidationKeyword);

            if (!stringRegexValidationKeyword.TryGetValidationRegularExpressions(typeDeclaration, out IReadOnlyList<string>? regexes) ||
                regexes?.Count != 1)
            {
                throw new InvalidOperationException($"The string regex keyword {stringRegexValidationKeyword.Keyword} did not have a single regular expression value.");
            }

            string pathModifier = stringRegexValidationKeyword.GetPathModifier();

            generator
                .AppendDocumentationDetails(typeDeclaration, pathModifier)
                .Append("Regular expression: ")
                .Append(regexes[0])
                .AppendLine(" *", pathModifier, "*");
        }

        return generator;
    }

    private static CodeGenerator AppendDocumentationDetails(this CodeGenerator generator, TypeDeclaration typeDeclaration, string pathModifier)
    {
        if (typeDeclaration.GetDocumentationDetails(pathModifier) is AdditionalSchemaDocumentation.Details details)
        {
            if (details.Title is JsonString title)
            {
                generator
                    .AppendSubsectionTitle((string)title);
            }

            if (details.Description is JsonString description)
            {
                generator
                    .AppendBlockIndent((string)description);
            }
        }

        return generator;
    }
}