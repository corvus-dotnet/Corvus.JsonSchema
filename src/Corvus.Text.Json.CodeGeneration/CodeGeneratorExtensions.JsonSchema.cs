// <copyright file="CodeGeneratorExtensions.JsonSchema.cs" company="Endjin Limited">
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
using Corvus.Text.Json.CodeGeneration.ValidationHandlers;
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

    public static CodeGenerator AppendPushChildContextMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string useEvaluatedItems = typeDeclaration.RequiresItemsEvaluationTracking() ? "true" : "false";
        string useEvaluatedProperties = typeDeclaration.RequiresPropertyEvaluationTracking() ? "true" : "false";

        generator
            .ReserveName("PushChildContext")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Push the current context as a child context for schema evaluation.
                /// </summary>
                /// <typeparam name="TContext">The type of the context to be passed to the path providers.</typeparam>
                /// <param name="parentDocument">The parent document of the instance for which to push the child context.</param>
                /// <param name="parentDocumentIndex">The index in the parent document of the instance for which to push the child context.</param>
                /// <param name="context">The current evaluation context.</param>
                /// <param name="providerContext">The context to be passed to the path providers.</param>
                /// <param name="schemaEvaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
                /// <param name="documentEvaluationPath">The (optional) path in the document being evaluated in the child context.</param>
                /// <returns>The child context.</returns>
                """)
            .AppendLineIndent("internal static JsonSchemaContext PushChildContext<TContext>(")
            .PushIndent()
                .AppendLineIndent("IJsonDocument parentDocument,")
                .AppendLineIndent("int parentDocumentIndex,")
                .AppendLineIndent("ref JsonSchemaContext context,")
                .AppendLineIndent("TContext providerContext,")
                .AppendLineIndent("JsonSchemaPathProvider<TContext>? schemaEvaluationPath = null,")
                .AppendLineIndent("JsonSchemaPathProvider<TContext>? documentEvaluationPath = null)")
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendLineIndent("context.PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("parentDocumentIndex,")
                        .AppendLineIndent("useEvaluatedItems: ", useEvaluatedItems, ",")
                        .AppendLineIndent("useEvaluatedProperties: ", useEvaluatedProperties, ",")
                        .AppendLineIndent("evaluationPath: schemaEvaluationPath,")
                        .AppendLineIndent("documentEvaluationPath: documentEvaluationPath,")
                        .AppendLineIndent("providerContext: providerContext);")
                    .PopIndent()
                .PopIndent()
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Push the current context as a child context for schema evaluation.
                /// </summary>
                /// <param name="parentDocument">The parent document of the instance for which to push the child context.</param>
                /// <param name="parentDocumentIndex">The index in the parent document of the instance for which to push the child context.</param>
                /// <param name="context">The current evaluation context.</param>
                /// <param name="schemaEvaluationPath">The (optional) path to the schema being evaluated in the child context.</param>
                /// <param name="documentEvaluationPath">The (optional) path in the document being evaluated in the child context.</param>
                /// <returns>The child context.</returns>
                """)
            .AppendLineIndent("internal static JsonSchemaContext PushChildContext(")
            .PushIndent()
                .AppendLineIndent("IJsonDocument parentDocument,")
                .AppendLineIndent("int parentDocumentIndex,")
                .AppendLineIndent("ref JsonSchemaContext context,")
                .AppendLineIndent("JsonSchemaPathProvider? schemaEvaluationPath = null,")
                .AppendLineIndent("JsonSchemaPathProvider? documentEvaluationPath = null)")
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendLineIndent("context.PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("parentDocumentIndex,")
                        .AppendLineIndent("useEvaluatedItems: ", useEvaluatedItems, ",")
                        .AppendLineIndent("useEvaluatedProperties: ", useEvaluatedProperties, ",")
                        .AppendLineIndent("evaluationPath: schemaEvaluationPath,")
                        .AppendLineIndent("documentEvaluationPath: documentEvaluationPath);")
                    .PopIndent()
                .PopIndent()
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Push the current context as a child context for schema evaluation of a property.
                /// </summary>
                /// <param name="parentDocument">The parent document of the instance for which to push the child context.</param>
                /// <param name="parentDocumentIndex">The index in the parent document of the instance for which to push the child context.</param>
                /// <param name="context">The current evaluation context.</param>
                /// <param name="propertyName">The name of the property </param>
                /// <param name="evaluationPath">The (optional) reduced evaluation path in the child context.</param>
                /// <returns>The child context.</returns>
                """)
            .AppendLineIndent("internal static JsonSchemaContext PushChildContext(")
            .PushIndent()
                .AppendLineIndent("IJsonDocument parentDocument,")
                .AppendLineIndent("int parentDocumentIndex,")
                .AppendLineIndent("ref JsonSchemaContext context,")
                .AppendLineIndent("ReadOnlySpan<byte> propertyName,")
                .AppendLineIndent("JsonSchemaPathProvider? evaluationPath = null)")
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendLineIndent("context.PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("parentDocumentIndex,")
                        .AppendLineIndent("useEvaluatedItems: ", useEvaluatedItems, ",")
                        .AppendLineIndent("useEvaluatedProperties: ", useEvaluatedProperties, ",")
                        .AppendLineIndent("propertyName,")
                        .AppendLineIndent("evaluationPath: evaluationPath,")
                        .AppendLineIndent("schemaEvaluationPath: SchemaLocationProvider);")
                    .PopIndent()
                .PopIndent()
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Push the current context as a child context for schema evaluation of a property where the property name is known to be unescaped.
                /// </summary>
                /// <param name="parentDocument">The parent document of the instance for which to push the child context.</param>
                /// <param name="parentDocumentIndex">The index in the parent document of the instance for which to push the child context.</param>
                /// <param name="context">The current evaluation context.</param>
                /// <param name="propertyName">The name of the property </param>
                /// <param name="evaluationPath">The (optional) reduced evaluation path in the child context.</param>
                /// <returns>The child context.</returns>
                """)
            .AppendLineIndent("internal static JsonSchemaContext PushChildContextUnescaped(")
            .PushIndent()
                .AppendLineIndent("IJsonDocument parentDocument,")
                .AppendLineIndent("int parentDocumentIndex,")
                .AppendLineIndent("ref JsonSchemaContext context,")
                .AppendLineIndent("ReadOnlySpan<byte> propertyName,")
                .AppendLineIndent("JsonSchemaPathProvider? evaluationPath = null)")
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendLineIndent("context.PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("parentDocumentIndex,")
                        .AppendLineIndent("useEvaluatedItems: ", useEvaluatedItems, ",")
                        .AppendLineIndent("useEvaluatedProperties: ", useEvaluatedProperties, ",")
                        .AppendLineIndent("propertyName,")
                        .AppendLineIndent("evaluationPath: evaluationPath,")
                        .AppendLineIndent("schemaEvaluationPath: SchemaLocationProvider);")
                    .PopIndent()
                .PopIndent()
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Push the current context as a child context for schema evaluation of an array item.
                /// </summary>
                /// <param name="parentDocument">The parent document of the instance for which to push the child context.</param>
                /// <param name="parentDocumentIndex">The index in the parent document of the instance for which to push the child context.</param>
                /// <param name="context">The current evaluation context.</param>
                /// <param name="itemIndex">The index of the item in the array.</param>
                /// <param name="evaluationPath">The (optional) reduced evaluation path in the child context.</param>
                /// <returns>The child context.</returns>
                """)
            .AppendLineIndent("internal static JsonSchemaContext PushChildContext(")
            .PushIndent()
                .AppendLineIndent("IJsonDocument parentDocument,")
                .AppendLineIndent("int parentDocumentIndex,")
                .AppendLineIndent("ref JsonSchemaContext context,")
                .AppendLineIndent("int itemIndex,")
                .AppendLineIndent("JsonSchemaPathProvider? evaluationPath = null)")
            .PopIndent()
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendLineIndent("context.PushChildContext(")
                    .PushIndent()
                        .AppendLineIndent("parentDocument,")
                        .AppendLineIndent("parentDocumentIndex,")
                        .AppendLineIndent("useEvaluatedItems: ", useEvaluatedItems, ",")
                        .AppendLineIndent("useEvaluatedProperties: ", useEvaluatedProperties, ",")
                        .AppendLineIndent("itemIndex,")
                        .AppendLineIndent("evaluationPath: evaluationPath,")
                        .AppendLineIndent("schemaEvaluationPath: SchemaLocationProvider);")
                    .PopIndent()
                .PopIndent()
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    private static readonly System.Text.RegularExpressions.Regex PrefixPattern =
        new(@"^\^([a-zA-Z0-9\-_/@.]+)(\.\*)?$", System.Text.RegularExpressions.RegexOptions.Compiled);

    private static readonly System.Text.RegularExpressions.Regex RangePattern =
        new(@"^\^\.\{(\d+),(\d+)\}\$$", System.Text.RegularExpressions.RegexOptions.Compiled);

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
        return (int.Parse(match.Groups[1].Value), int.Parse(match.Groups[2].Value));
    }

    public static CodeGenerator AppendRegexValidationFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
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
                    if (constant.Value.Count == 1)
                    {
                        if (ClassifyRegexPattern(constant.Value[0]) == RegexPatternCategory.FullRegex)
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendRegexValidationField(constant.Key, null);
                        }
                    }
                    else
                    {
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

                                generator.AppendRegexValidationField(constant.Key, i);
                            }

                            i++;
                        }
                    }
                }
            }
        }

        return generator;
    }

    public static CodeGenerator AppendRegexValidationFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
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
                    if (ClassifyRegexPattern(constant.Value[0]) == RegexPatternCategory.FullRegex)
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendRegexValidationFactoryMethod(constant.Key, null, constant.Value[0]);
                    }
                }
                else
                {
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

                            generator.AppendRegexValidationFactoryMethod(constant.Key, i, value);
                        }

                        i++;
                    }
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

        return generator
#if BUILDING_SOURCE_GENERATOR
                .AppendIndent("private static Regex ")
                .Append(memberName)
                .Append("() => new(")
                .Append(SymbolDisplay.FormatLiteral(value, true))
                .AppendLine(", RegexOptions.Compiled);");
#else
                .AppendLine("#if NET8_0_OR_GREATER && !DYNAMIC_BUILD")
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
            .Append(SymbolDisplay.FormatLiteral(value, true))
            .AppendLine(", RegexOptions.Compiled);")
            .AppendLine("#endif");
#endif
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
                /// <summary>
                /// Evaluate this instance against the JSON Schema for this type.
                /// </summary>
                /// <params name="resultsCollector">The (optional) results collector.</params>
                /// <returns><see langword="true" /> if the instance evaluates against the schema.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null)
                {
                    return {{generator.JsonSchemaClassName()}}.Evaluate(_parent, _idx, resultsCollector);
                }
                """);
    }

    /// <summary>
    /// Appends a static JsonSchema Evaluate method to the generated type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to generate the evaluation method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonSchemaEvaluateMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .ReserveName("Evaluate")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Applies the JSON schema semantics defined by this type to the instance determined by the given document and index.
                /// </summary>
                /// <param name="parentDocument">The parent document.</param>
                /// <param name="parentIndex">The parent index.</param>
                /// <param name="context">A reference to the validation context, configured with the appropriate values.</param>
                """)
            .BeginReservedMethodDeclaration(
                visibilityAndModifiers: "internal static",
                returnType: "void",
                methodName: "Evaluate",
                parameters: [
                    ("IJsonDocument", "parentDocument"),
                    ("int", "parentIndex"),
                    ("ref JsonSchemaContext", "context")
                ])
                .ConditionallyAppend(typeDeclaration.RequiresJsonTokenType(),
                g => g
                    .ReserveName("tokenType")
                    .AppendLineIndent("JsonTokenType tokenType = parentDocument.GetJsonTokenType(parentIndex);"))
                .AppendSeparatorLine()
                .AppendLineIndent("// You're not allowed to ask about non-value-like entities")
                .AppendLineIndent("Debug.Assert(parentDocument.GetJsonTokenType(parentIndex) is not")
                .PushIndent()
                    .AppendLineIndent("(JsonTokenType.None or")
                    .AppendLineIndent("JsonTokenType.EndObject or")
                    .AppendLineIndent("JsonTokenType.EndArray));")
                .PopIndent();

        // Append any setup code for each handler at the top of the method
        foreach (KeywordValidationHandlerBase handler in typeDeclaration.OrderedValidationHandlers<KeywordValidationHandlerBase>(generator.LanguageProvider))
        {
            handler.AppendValidationSetup(generator, typeDeclaration);
        }

        // Then append the actual validation code beneath
        bool needsShortcut = false;
        foreach (KeywordValidationHandlerBase handler in typeDeclaration.OrderedValidationHandlers<KeywordValidationHandlerBase>(generator.LanguageProvider))
        {
            int originalLength = generator.Length;

            if (needsShortcut)
            {
                generator.AppendNoCollectorNoMatchShortcutReturn();
            }

            int length = generator.Length;

            typeDeclaration.ExecuteValidationHandler(handler, k => k.AppendValidationCode(generator, typeDeclaration));

            if (length != generator.Length)
            {
                needsShortcut = true;
            }
            else
            {
                // Revert anything we added as a result of applying a shortcut.
                generator.Length = originalLength;
            }
        }

        generator
            .EndMethodDeclaration()
            .AppendSeparatorLine();

        // Now append the utility wrapper that reutrn the boolean result derived from the context.
        return generator
            .BeginMethodDeclaration(
                visibilityAndModifiers: "internal static",
                returnType: "bool",
                methodName: "Evaluate",
                parameters: [
                    ("IJsonDocument", "parentDocument"),
                    ("int", "parentIndex"),
                    ("IJsonSchemaResultsCollector?", "resultsCollector", "null")
                ])
                .AppendLineIndent("JsonSchemaContext context = JsonSchemaContext.BeginContext(")
                .AppendLineIndent("parentDocument,")
                .AppendLineIndent("parentIndex,")
                .AppendLineIndent("usingEvaluatedItems: ", typeDeclaration.ExplicitUnevaluatedItemsType() is not null ? "true" : "false", ",")
                .AppendLineIndent("usingEvaluatedProperties: ", typeDeclaration.LocalEvaluatedPropertyType() is not null || typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is not null ? "true" : "false", ",")
                .AppendLineIndent("resultsCollector: resultsCollector);")
                .AppendSeparatorLine()
                .AppendLineIndent("try")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("Evaluate(parentDocument, parentIndex, ref context);")
                    .AppendLineIndent("context.EndContext();")
                    .AppendLineIndent("return context.IsMatch;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("finally")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent("context.Dispose();")
                .PopIndent()
                .AppendLineIndent("}")
            .EndMethodDeclaration();
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

        throw new InvalidOperationException("The ArrayBuilder class name has not been created.");
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

        throw new InvalidOperationException("The Builder class name has not been created.");
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

        throw new InvalidOperationException("The Builder class scope  has not been created.");
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

        throw new InvalidOperationException("The JsonPropertyNames class name has not been created.");
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

        throw new InvalidOperationException("The JsonPropertyNamesEscaped class name has not been created.");
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

        throw new InvalidOperationException("The JsonPropertyNamesEscaped class scope  has not been created.");
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

        throw new InvalidOperationException("The JsonPropertyNames class scope  has not been created.");
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

        throw new InvalidOperationException("The JSON Schema class name has not been created.");
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

        throw new InvalidOperationException("The JSON Schema class scope  has not been created.");
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

        throw new InvalidOperationException("The ObjectBuilder class name has not been created.");
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

        throw new InvalidOperationException("The Source class name has not been created.");
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

        throw new InvalidOperationException("The Source class scope  has not been created.");
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

        throw new InvalidOperationException("The Mutable class name has not been created.");
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

        throw new InvalidOperationException("The Mutable class scope  has not been created.");
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

        throw new InvalidOperationException("The Constants class name has not been created.");
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

        throw new InvalidOperationException("The Constants class scope  has not been created.");
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

        throw new InvalidOperationException("The EnumValues class name has not been created.");
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

        throw new InvalidOperationException("The EnumValues class scope has not been created.");
    }
}