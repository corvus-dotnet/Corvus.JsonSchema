// <copyright file="CodeGeneratorExtensions.JsonSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generation extensions for JSON Schema related functionality.
/// </summary>
internal static partial class CodeGenerationExtensions
{
    private const string ArrayBuilderClassBaseName = "ArrayBuilder";
    private const string BuilderClassBaseName = "Builder";
    private const string BuilderClassNameKey = "CSharp_JsonSchema_BuilderClassNameKey";
    private const string JsonPropertyNamesClassBaseName = "JsonPropertyNames";
    private const string JsonPropertyNamesClassNameKey = "CSharp_JsonSchema_JsonPropertyNamesClassNameKey";
    private const string JsonPropertyNamesEscapedClassBaseName = "JsonPropertyNamesEscaped";
    private const string JsonPropertyNamesEscapedClassNameKey = "CSharp_JsonSchema_JsonPropertyNamesEscapedClassNameKey";
    private const string JsonPropertyNamesPrebakedClassBaseName = "JsonPropertyNamesPrebaked";
    private const string JsonPropertyNamesPrebakedClassNameKey = "CSharp_JsonSchema_JsonPropertyNamesPrebakedClassNameKey";
    private const string JsonSchemaClassBaseName = "JsonSchema";
    private const string JsonSchemaClassNameKey = "CSharp_JsonSchema_JsonSchemaClassNameKey";
    private const string ObjectBuilderClassBaseName = "ObjectBuilder";
    private const string SourceClassBaseName = "Source";
    private const string SourceClassNameKey = "CSharp_JsonSchema_SourceClassNameKey";
    private const string ConstantsClassBaseName = "Constants";
    private const string ConstantsClassNameKey = "CSharp_JsonSchema_ConstantsClassNameKey";
    private const string EnumValuesClassBaseName = "EnumValues";
    private const string EnumValuesClassNameKey = "CSharp_JsonSchema_EnumValuesClassNameKey";
    private const string MutableClassBaseName = "Mutable";
    private const string MutableClassNameKey = "CSharp_JsonSchema_MutableClassNameKey";

    private static readonly System.Text.RegularExpressions.Regex PrefixPattern =
        new(@"^\^([a-zA-Z0-9\-_/@.]+)(\.\*)?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RangePattern =
        new(@"^\^\.\{([0-9]+),([0-9]+)\}\$$", System.Text.RegularExpressions.RegexOptions.Compiled);

    /// <summary>
    /// Classifies a regular expression pattern for potential inline code generation
    /// instead of emitting a full <see cref="System.Text.RegularExpressions.Regex"/> object.
    /// </summary>
    /// <param name="pattern">The raw regex pattern string.</param>
    /// <returns>The classification of the pattern.</returns>
    internal static RegexPatternCategory ClassifyRegexPattern(string pattern)
    {
        if (pattern is ".*" or "^.*$" or "^(.*)$" or "(.*)" or "[\\s\\S]*" or "^[\\s\\S]*$")
        {
            return RegexPatternCategory.Noop;
        }

        if (pattern is ".+" or "^.+$" or "^(.+)$" or "(.+)" or ".")
        {
            return RegexPatternCategory.NonEmpty;
        }

        if (PrefixPattern.IsMatch(pattern))
        {
            return RegexPatternCategory.Prefix;
        }

        if (RangePattern.IsMatch(pattern))
        {
            return RegexPatternCategory.Range;
        }

        return RegexPatternCategory.FullRegex;
    }

    /// <summary>
    /// Extracts the literal prefix from a prefix-category regex pattern.
    /// </summary>
    /// <param name="pattern">A pattern previously classified as <see cref="RegexPatternCategory.Prefix"/>.</param>
    /// <returns>The literal prefix string.</returns>
    internal static string ExtractRegexPrefix(string pattern)
    {
        System.Text.RegularExpressions.Match match = PrefixPattern.Match(pattern);
        return match.Groups[1].Value;
    }

    /// <summary>
    /// Extracts the minimum and maximum length from a range-category regex pattern.
    /// </summary>
    /// <param name="pattern">A pattern previously classified as <see cref="RegexPatternCategory.Range"/>.</param>
    /// <returns>A tuple of (minimum, maximum) length values.</returns>
    internal static (int Min, int Max) ExtractRegexRange(string pattern)
    {
        System.Text.RegularExpressions.Match match = RangePattern.Match(pattern);
        return (int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture), int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Gets a value indicating whether the type's <c>JsonSchema</c> class needs regular-expression fields for its
    /// pattern properties (only full regexes need a field; prefix, range and non-empty patterns are matched inline).
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns><see langword="true"/> if at least one pattern property regex field is emitted.</returns>
    public static bool HasPatternPropertyRegexFields(this TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.ValidationRegularExpressions() is IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> regexes)
        {
            foreach (KeyValuePair<IValidationRegexProviderKeyword, IReadOnlyList<string>> regex in regexes)
            {
                if (regex.Key is IObjectPatternPropertyValidationKeyword)
                {
                    foreach (string value in regex.Value)
                    {
                        if (ClassifyRegexPattern(value) == RegexPatternCategory.FullRegex)
                        {
                            return true;
                        }
                    }
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Appends the static regular-expression fields backing the generated pattern-property helpers
    /// (<c>MatchesPattern…</c>, <c>TryAsPattern…</c>). Validation itself runs in the runtime evaluator, so only the
    /// pattern-property regexes are emitted.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPatternPropertyRegexFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator.AppendPatternPropertyRegexMembers(typeDeclaration, static (g, keyword, index, _) => g.AppendRegexValidationField(keyword, index));
    }

    /// <summary>
    /// Appends the factory methods for the fields emitted by <see cref="AppendPatternPropertyRegexFields"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPatternPropertyRegexFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator.AppendPatternPropertyRegexMembers(typeDeclaration, static (g, keyword, index, value) => g.AppendRegexValidationFactoryMethod(keyword, index, value));
    }

    private static CodeGenerator AppendPatternPropertyRegexMembers(this CodeGenerator generator, TypeDeclaration typeDeclaration, Func<CodeGenerator, IKeyword, int?, string, CodeGenerator> append)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationRegularExpressions() is IReadOnlyDictionary<IValidationRegexProviderKeyword, IReadOnlyList<string>> regexes)
        {
            // Ensure we have a got a stable ordering of the keywords.
            foreach (KeyValuePair<IValidationRegexProviderKeyword, IReadOnlyList<string>> constant in regexes.OrderBy(k => k.Key.Keyword, StringComparer.Ordinal))
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                if (constant.Key is not IObjectPatternPropertyValidationKeyword || constant.Value.Count == 0)
                {
                    continue;
                }

                // The index suffix matches the pattern-property helpers: none for a single pattern, 1-based otherwise.
                bool hasIndex = constant.Value.Count > 1;
                bool needsSeparator = true;
                int i = 1;
                foreach (string value in constant.Value)
                {
                    if (ClassifyRegexPattern(value) == RegexPatternCategory.FullRegex)
                    {
                        if (needsSeparator)
                        {
                            generator.AppendSeparatorLine();
                            needsSeparator = false;
                        }

                        append(generator, constant.Key, hasIndex ? i : null, value);
                    }

                    i++;
                }
            }
        }

        return generator;
    }

    private static CodeGenerator AppendRegexValidationField(this CodeGenerator generator, IKeyword keyword, int? index)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string? suffix = index?.ToString(CultureInfo.InvariantCulture);
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

        string translatedValue = EcmaRegexTranslator.TranslateOrFallback(value);
        string memberName = generator.GetMethodNameInScope(keyword.Keyword, prefix: "Create", suffix: index?.ToString(CultureInfo.InvariantCulture));

        return generator
#if BUILDING_SOURCE_GENERATOR
                .AppendIndent("private static Regex ")
                .Append(memberName)
                .Append("() => new(")
                .Append(SymbolDisplay.FormatLiteral(translatedValue, true))
                .AppendLine(", RegexOptions.Compiled);");
#else
                .AppendLine("#if NET8_0_OR_GREATER && !DYNAMIC_BUILD")
                .AppendIndent("[GeneratedRegex(")
                .Append(SymbolDisplay.FormatLiteral(translatedValue, true))
                .AppendLine(")]")
                .AppendIndent("private static partial Regex ")
                .Append(memberName)
                .AppendLine("();")
            .AppendLine("#else")
            .AppendIndent("private static Regex ")
            .Append(memberName)
            .Append("() => new(")
            .Append(SymbolDisplay.FormatLiteral(translatedValue, true))
            .AppendLine(", RegexOptions.Compiled);")
            .AppendLine("#endif");
#endif
    }

    /// <summary>
    /// Appends the static JsonSchema Evaluate method, which evaluates the instance against this type's entry point
    /// of the assembly's schema evaluation program.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to generate the evaluation method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendRuntimeProgramEvaluateMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        var provider = (CSharpLanguageProvider)generator.LanguageProvider;
        int entry = provider.GetProgramEntry(typeDeclaration);

        generator
            .ReserveName("Evaluator")
            .ReserveName("Evaluate")
            .AppendSeparatorLine();

        if (entry < 0)
        {
            // No schema location is known for this type (a synthetic type); it accepts any instance.
            return generator
                .AppendLineIndent("private static readonly global::Corvus.Text.Json.RuntimeEvaluator.JsonSchemaEvaluator? Evaluator = null;")
                .AppendSeparatorLine()
                .BeginMethodDeclaration(
                    visibilityAndModifiers: "internal static",
                    returnType: "bool",
                    methodName: "Evaluate",
                    parameters: [
                        ("IJsonDocument", "parentDocument"),
                        ("int", "parentIndex"),
                        ("IJsonSchemaResultsCollector?", "resultsCollector", "null")
                    ])
                    .AppendLineIndent("return true;")
                .EndMethodDeclaration();
        }

        return generator
            .AppendLineIndent(
                "private static readonly global::Corvus.Text.Json.RuntimeEvaluator.JsonSchemaEvaluator Evaluator = ",
                provider.ProgramClassReference,
                ".Entry(",
                entry.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ");")
            .AppendSeparatorLine()
            .BeginMethodDeclaration(
                visibilityAndModifiers: "internal static",
                returnType: "bool",
                methodName: "Evaluate",
                parameters: [
                    ("IJsonDocument", "parentDocument"),
                    ("int", "parentIndex"),
                    ("IJsonSchemaResultsCollector?", "resultsCollector", "null")
                ])
                .AppendLineIndent("return Evaluator.Evaluate(parentDocument, parentIndex, resultsCollector);")
            .EndMethodDeclaration();
    }

    /// <summary>
    /// Appends an EvaluateSchema method to the generated type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEvaluateSchemaMethod(this CodeGenerator generator)
    {
        return generator
            .ReserveName("EvaluateSchema")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                $$"""
                /// <inheritdoc cref="global::Corvus.Text.Json.JsonElement.EvaluateSchema(IJsonSchemaResultsCollector)"/>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null)
                {
                    return {{generator.JsonSchemaClassName()}}.Evaluate(_parent, _idx, resultsCollector);
                }
                """);
    }

    /// <summary>
    /// Gets the ArrayBuilder class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string ArrayBuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(ArrayBuilderClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the ArrayBuilder class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string ArrayBuilderClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
            value is (string _, string arrayClassName, string _, string _))
        {
            return arrayClassName;
        }

        throw new InvalidOperationException(SR.ArrayBuilderClassNameNotCreated);
    }

    /// <summary>
    /// Gets the Builder class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string BuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(BuilderClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the Builder class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string BuilderClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
            value is (string className, string _, string _, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.BuilderClassNameNotCreated);
    }

    /// <summary>
    /// Gets the Builder class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string BuilderScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
            value is (string _, string _, string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.BuilderClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the JsonPropertyNames class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string JsonPropertyNamesClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.JsonPropertyNamesClassNameNotCreated);
    }

    /// <summary>
    /// Gets the JsonPropertyNamesEscaped class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string JsonPropertyNamesEscapedClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.JsonPropertyNamesEscapedClassNameNotCreated);
    }

    /// <summary>
    /// Gets the JsonPropertyNamesEscaped class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string JsonPropertyNamesEscapedScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.JsonPropertyNamesEscapedClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the JsonPropertyNamesPrebaked class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string JsonPropertyNamesPrebakedClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesPrebakedClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.JsonPropertyNamesPrebakedClassNameNotCreated);
    }

    /// <summary>
    /// Gets the JsonPropertyNames class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string JsonPropertyNamesScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.JsonPropertyNamesClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the JsonSchema class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The validation class name.</returns>
    public static string JsonSchemaClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.JsonSchemaClassNameNotCreated);
    }

    /// <summary>
    /// Gets the JsonSchema class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string JsonSchemaClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(JsonSchemaClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the JsonSchema class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified validation class scope.</returns>
    public static string JsonSchemaClassScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.JsonSchemaClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the ObjectBuilder class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string ObjectBuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(ObjectBuilderClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the ArrayBuilder class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string ObjectBuilderClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
            value is (string _, string _, string objectClassName, string _))
        {
            return objectClassName;
        }

        throw new InvalidOperationException(SR.ObjectBuilderClassNameNotCreated);
    }

    /// <summary>
    /// Remove the scoped Builder class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopBuilderClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(BuilderClassNameKey);
    }

    /// <summary>
    /// Remove the scoped json property names class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopJsonPropertyNamesClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(JsonPropertyNamesClassNameKey);
    }

    /// <summary>
    /// Remove the scoped escaped json property names class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopJsonPropertyNamesEscapedClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(JsonPropertyNamesEscapedClassNameKey);
    }

    /// <summary>
    /// Remove the scoped prebaked json property names class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopJsonPropertyNamesPrebakedClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(JsonPropertyNamesPrebakedClassNameKey);
    }

    /// <summary>
    /// Remove the scoped json schema class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopJsonSchemaClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(JsonSchemaClassNameKey);
    }

    /// <summary>
    /// Remove the Source class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopSourceClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(SourceClassNameKey);
    }

    /// <summary>
    /// Remove the Constants class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopConstantsClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(ConstantsClassNameKey);
    }

    /// <summary>
    /// Remove the EnumValues class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopEnumValuesClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(EnumValuesClassNameKey);
    }

    /// <summary>
    /// Remove the Mutable class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopMutableClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(MutableClassNameKey);
    }

    /// <summary>
    /// Make the Builder class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushBuilderClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string) _))
        {
            return generator;
        }

        string builderClass = generator.GetTypeNameInScope(BuilderClassBaseName);
        string arrayBuilderClass = generator.GetTypeNameInScope(ArrayBuilderClassBaseName);
        string objectBuilderClass = generator.GetTypeNameInScope(ObjectBuilderClassBaseName);
        return generator
            .PushMetadata(BuilderClassNameKey, (builderClass, arrayBuilderClass, objectBuilderClass, generator.GetChildScope(builderClass, null)));
    }

    /// <summary>
    /// Make the json property names class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushJsonPropertyNamesClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesClassBaseName);
        return generator
            .PushMetadata(JsonPropertyNamesClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
    }

    /// <summary>
    /// Make the escaped json property names class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushJsonPropertyNamesEscapedClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesEscapedClassBaseName);
        return generator
            .PushMetadata(JsonPropertyNamesEscapedClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
    }

    /// <summary>
    /// Make the prebaked json property names class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushJsonPropertyNamesPrebakedClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(JsonPropertyNamesPrebakedClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesPrebakedClassBaseName);
        return generator
            .PushMetadata(JsonPropertyNamesPrebakedClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
    }

    /// <summary>
    /// Make the scoped json schema class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PushJsonSchemaClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string) _))
        {
            string jsonSchemaClassName = generator.GetTypeNameInScope(JsonSchemaClassBaseName);
            return generator
                .PushMetadata(JsonSchemaClassNameKey, (jsonSchemaClassName, generator.GetChildScope(jsonSchemaClassName, null)));
        }

        return generator;
    }

    /// <summary>
    /// Make the Source class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushSourceClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string builderClass = generator.GetTypeNameInScope(SourceClassBaseName);
        return generator
            .PushMetadata(SourceClassNameKey, (builderClass, generator.GetChildScope(builderClass, null)));
    }

    /// <summary>
    /// Make the Enumeration class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushConstantsClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(ConstantsClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string constantsClass = generator.GetTypeNameInScope(ConstantsClassBaseName);
        return generator
            .PushMetadata(ConstantsClassNameKey, (constantsClass, generator.GetChildScope(constantsClass, null)));
    }

    /// <summary>
    /// Make the EnumValues class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushEnumValuesClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(EnumValuesClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string enumValuesClass = generator.GetTypeNameInScope(EnumValuesClassBaseName);
        return generator
            .PushMetadata(EnumValuesClassNameKey, (enumValuesClass, generator.GetChildScope(enumValuesClass, null)));
    }

    /// <summary>
    /// Make the Source class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushMutableClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(MutableClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string builderClass = generator.GetTypeNameInScope(MutableClassBaseName);
        return generator
            .PushMetadata(MutableClassNameKey, (builderClass, generator.GetChildScope(builderClass, null)));
    }

    /// <summary>
    /// Gets the Source class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string SourceClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(SourceClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the Source class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string SourceClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.SourceClassNameNotCreated);
    }

    /// <summary>
    /// Gets the Source class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string SourceScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.SourceClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the Constants class name for a particular type name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
    /// <returns>The class name.</returns>
    public static string ConstantsClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
    {
        return generator.GetTypeNameInScope(ConstantsClassBaseName, rootScope: fullyQualifiedTypeName);
    }

    /// <summary>
    /// Gets the ambient Mutable class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string MutableClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(MutableClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.MutableClassNameNotCreated);
    }

    /// <summary>
    /// Gets the Mutable class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string MutableScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(MutableClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.MutableClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the ambient Constants class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string ConstantsClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ConstantsClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.ConstantsClassNameNotCreated);
    }

    /// <summary>
    /// Gets the Constants class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string ConstantsScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ConstantsClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.ConstantsClassScopeNotCreated);
    }

    /// <summary>
    /// Gets the ambient EnumValues class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The class name.</returns>
    public static string EnumValuesClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(EnumValuesClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException(SR.EnumValuesClassNameNotCreated);
    }

    /// <summary>
    /// Gets the EnumValues class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified class scope.</returns>
    public static string EnumValuesScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(EnumValuesClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.EnumValuesClassScopeNotCreated);
    }
}