// <copyright file="ValidationCodeGeneratorExtensions.Constants.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    private static CodeGenerator AppendValidationConstantFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationConstants() is IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]> constants)
        {
            AppendValidationConstantFields(generator, constants);
        }

        return generator;
    }

    private static CodeGenerator AppendStringValidationConstantProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationConstants() is IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]> constants)
        {
            AppendStringValidationConstantProperties(generator, constants);
        }

        return generator;
    }

    private static CodeGenerator AppendRegexValidationFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationRegularExpressions() is IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> regexes)
        {
            // Ensure we have a got a stable ordering of the keywords.
            foreach (KeyValuePair<IValidationRegexProviderKeyword, IReadOnlyList<string>> constant in regexes.OrderBy(k => k.Key.Keyword))
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (constant.Value.Count > 0)
                {
                    generator.AppendSeparatorLine();
                    if (constant.Value.Count == 1)
                    {
                        generator.AppendRegexValidationField(constant.Key, null);
                    }
                    else
                    {
                        int i = 1;
                        foreach (string value in constant.Value)
                        {
                            generator.AppendRegexValidationField(constant.Key, i);
                            i++;
                        }
                    }
                }
            }
        }

        return generator;
    }

    private static CodeGenerator AppendRegexValidationFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationRegularExpressions() is IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> regexes)
        {
            // Ensure we have a got a stable ordering of the keywords.
            foreach (KeyValuePair<IValidationRegexProviderKeyword, IReadOnlyList<string>> constant in regexes.OrderBy(k => k.Key.Keyword))
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (constant.Value.Count == 1)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendRegexValidationFactoryMethod(constant.Key, null, constant.Value[0]);
                }
                else
                {
                    int i = 1;
                    foreach (string value in constant.Value)
                    {
                        if (i == 1)
                        {
                            generator.AppendSeparatorLine();
                        }

                        generator.AppendRegexValidationFactoryMethod(constant.Key, i, value);
                        i++;
                    }
                }
            }
        }

        return generator;
    }

    private static CodeGenerator AppendNumberValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly BinaryJsonNumber ")
            .Append(memberName)
            .Append(" = new(")
            .AppendNumericLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendIntegerValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly long ")
            .Append(memberName)
            .Append(" = ")
            .AppendIntegerLiteral(value)
            .AppendLine(";");

        return generator;
    }

    private static CodeGenerator AppendStringValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.String, "The value must be a string.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly JsonString ")
            .Append(memberName)
            .Append(" = JsonString.ParseValue(")
            .AppendQuotedStringLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendNullValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Null, "The value must be null.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly JsonNull ")
            .Append(memberName)
            .AppendLine(" = JsonNull.Null;");

        return generator;
    }

    private static CodeGenerator AppendBooleanValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False, "The value must be a boolean.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly JsonBoolean ")
            .Append(memberName)
            .Append(" = JsonBoolean.ParseValue(")
            .AppendSerializedBooleanLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendObjectValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Object, "The value must be an object.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly JsonObject ")
            .Append(memberName)
            .Append(" = JsonObject.ParseValue(")
            .AppendSerializedObjectStringLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendArrayValidationConstantField(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Array, "The value must be an array.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly JsonArray ")
            .Append(memberName)
            .Append(" = JsonArray.ParseValue(")
            .AppendSerializedArrayStringLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendStringValidationConstantProperty(this CodeGenerator generator, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.String, "The value must be a string.");

        string memberName = generator.GetPropertyNameInScope(keyword.Keyword, suffix: index?.ToString());

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static ReadOnlySpan<byte> ")
            .Append(memberName)
            .Append("Utf8")
            .Append(" => ")
            .AppendQuotedStringLiteral(value)
            .AppendLine("u8;");

        return generator;
    }

    private static CodeGenerator AppendRegexValidationField(this CodeGenerator generator, IKeyword keyword, int? index)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string? suffix = index?.ToString();
        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: suffix);
        string methodName = generator.GetMethodNameInScope(keyword.Keyword, prefix: "Create", suffix: suffix);

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A regular expression for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly Regex ")
            .Append(memberName)
            .Append(" = ")
            .Append(methodName)
            .AppendLine("();");

        return generator;
    }

    private static CodeGenerator AppendRegexValidationFactoryMethod(this CodeGenerator generator, IKeyword keyword, int? index, string value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string memberName = generator.GetMethodNameInScope(keyword.Keyword, prefix: "Create", suffix: index?.ToString());

        // TODO: Figure out how to get the SourceGenerator to run when generating code
        // in the specs.
        return generator
                .AppendLine("#if NET8_0_OR_GREATER && !SPECFLOW_BUILD")
                    .AppendIndent("[GeneratedRegex(")
                    .Append(SymbolDisplay.FormatLiteral(value, true))
                    .AppendLine(")]")
                    .AppendIndent("private static partial Regex ")
                    .Append(memberName)
                    .AppendLine("();")
                .AppendLine("#else")
                .AppendIndent("private static Regex ")
                .Append(memberName)
                .Append("() => new(")
                .AppendQuotedStringLiteral(value)
                .AppendLine(", RegexOptions.Compiled);")
                .AppendLine("#endif");
    }

    private static CodeGenerator AppendValidationConstantFields(CodeGenerator generator, IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]> constants)
    {
        // Ensure we have a got a stable ordering of the keywords.
        foreach (KeyValuePair<IValidationConstantProviderKeyword, JsonElement[]> constant in constants.OrderBy(k => k.Key.Keyword))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            int count = constant.Value.Length;

            if (count > 0)
            {
                generator.AppendSeparatorLine();

                int? i = count == 1 ? null : 1;
                foreach (JsonElement value in constant.Value)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    switch (value.ValueKind)
                    {
                        case JsonValueKind.Array:
                            generator.AppendArrayValidationConstantField(constant.Key, i, value);
                            break;

                        case JsonValueKind.Null:
                            generator.AppendNullValidationConstantField(constant.Key, i, value);
                            break;

                        case JsonValueKind.String:
                            generator.AppendStringValidationConstantField(constant.Key, i, value);
                            break;

                        case JsonValueKind.Number:
                            if (constant.Key is IIntegerConstantValidationKeyword)
                            {
                                generator.AppendIntegerValidationConstantField(constant.Key, i, value);
                            }
                            else
                            {
                                generator.AppendNumberValidationConstantField(constant.Key, i, value);
                            }

                            break;

                        case JsonValueKind.Object:
                            generator.AppendObjectValidationConstantField(constant.Key, i, value);
                            break;

                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            generator.AppendBooleanValidationConstantField(constant.Key, i, value);
                            break;

                        default:
                            break;
                    }

                    if (count > 1)
                    {
                        i++;
                    }
                }
            }
        }

        return generator;
    }

    private static CodeGenerator AppendStringValidationConstantProperties(CodeGenerator generator, IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]> constants)
    {
        // Ensure we have a got a stable ordering of the keywords.
        foreach (KeyValuePair<IValidationConstantProviderKeyword, JsonElement[]> constant in constants.OrderBy(k => k.Key.Keyword))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            int count = constant.Value.Length;

            if (count > 0)
            {
                generator.AppendSeparatorLine();

                int i = 1;
                foreach (JsonElement value in constant.Value)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    if (value.ValueKind == JsonValueKind.String)
                    {
                        if (count == 1)
                        {
                            return generator.AppendStringValidationConstantProperty(constant.Key, null, value);
                        }
                        else
                        {
                            generator.AppendStringValidationConstantProperty(constant.Key, i, value);
                        }

                        if (count == i)
                        {
                            return generator;
                        }
                    }

                    // We need to ensure that we get the correct index in the array, so we always increment even if we omitted this particular
                    // value (in the event that values of different types are interleaved in the array).
                    i++;
                }
            }
        }

        return generator;
    }
}