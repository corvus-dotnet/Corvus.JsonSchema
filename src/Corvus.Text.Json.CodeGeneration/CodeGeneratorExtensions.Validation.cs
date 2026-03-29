// <copyright file="CodeGeneratorExtensions.Validation.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.Internal;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generation extensions for JSON Schema related functionality.
/// </summary>
internal static partial class CodeGenerationExtensions
{
    private const string NormalizedJsonNumberAppendedKey = "NormalizedJsonNumberAppended";
    private const string NormalizedJsonNumberAppendedInScopeKey = "NormalizedJsonNumberAppendedInScope";
    private const string GetRawSimpleValueAppendedKey = "GetRawSimpleValueAppended";
    private const string GetRawSimpleValueAppendedInScopeKey = "GetRawSimpleValueAppendedInScope";
    private const string UnescapedUtf8JsonStringAppendedKey = "UnescapedUtf8JsonStringAppended";
    private const string UnescapedUtf8JsonStringAppendedInScopeKey = "UnescapedUtf8JsonStringAppendedInScope";
    private const string StringLengthAppendedKey = "StringLengthAppended";
    private const string StringLengthAppendedInScopeKey = "StringLengthAppendedInScope";
    private const string EnumStringSetFieldNameKeyPrefix = "AnyOfConstValidationHandler.EnumStringSetFieldName.";
    private const string OneOfDiscriminatorMapFieldNameKeyPrefix = "OneOfDiscriminator.EnumStringMapFieldName.";
    private const string OneOfDiscriminatorPropertyNameKeyPrefix = "OneOfDiscriminator.PropertyName.";
    private const string OneOfDiscriminatorValuesKeyPrefix = "OneOfDiscriminator.Values.";
    private const string AnyOfDiscriminatorMapFieldNameKeyPrefix = "AnyOfDiscriminator.EnumStringMapFieldName.";
    private const string AnyOfDiscriminatorPropertyNameKeyPrefix = "AnyOfDiscriminator.PropertyName.";
    private const string AnyOfDiscriminatorValuesKeyPrefix = "AnyOfDiscriminator.Values.";
    private const string OneOfDiscriminatorValueKindKeyPrefix = "OneOfDiscriminator.ValueKind.";
    private const string AnyOfDiscriminatorValueKindKeyPrefix = "AnyOfDiscriminator.ValueKind.";
    private const string HoistedAllOfBranchesKeyPrefix = "HoistedAllOf.Branches.";
    private const int MinEnumValuesForHashSet = 3;

    public static CodeGenerator AppendNormalizedJsonNumberIfNotAppended(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool includeTokenTypeCheck = true)
    {
        if (typeDeclaration.TryGetMetadata(NormalizedJsonNumberAppendedKey, out bool? _))
        {
            return generator;
        }

        typeDeclaration.SetMetadata(NormalizedJsonNumberAppendedKey, true);
        typeDeclaration.SetMetadata(NormalizedJsonNumberAppendedInScopeKey, generator.FullyQualifiedScope);

        return generator
            .AppendGetRawSimpleValueIfNotAppended(typeDeclaration, includeTokenTypeCheck)
            .AppendSeparatorLine()
            .ReserveName("isNegative")
            .ReserveName("integral")
            .ReserveName("fractional")
            .ReserveName("exponent")
            .AppendLineIndent("JsonElementHelpers.TryParseNumber(rawSimpleValue.Span, out bool isNegative,out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);");
    }

    public static CodeGenerator AppendStringLengthIfNotAppended(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool includeTokenTypeCheck = true)
    {
        if (typeDeclaration.TryGetMetadata(StringLengthAppendedKey, out bool? _))
        {
            return generator;
        }

        typeDeclaration.SetMetadata(StringLengthAppendedKey, true);
        typeDeclaration.SetMetadata(StringLengthAppendedInScopeKey, generator.FullyQualifiedScope);

        return generator
            .AppendUnescapedUtf8JsonStringIfNotAppended(typeDeclaration, includeTokenTypeCheck)
            .AppendSeparatorLine()
            .ReserveName("stringLength")
            .AppendLineIndent("int stringLength = JsonElementHelpers.CountRunes(unescapedUtf8JsonString.Span);");
    }

    public static CodeGenerator PopStringLengthIfAppendedInScope(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(StringLengthAppendedInScopeKey, out string? scope)
            && scope == generator.FullyQualifiedScope)
        {
            typeDeclaration.RemoveMetadata(StringLengthAppendedInScopeKey);
            typeDeclaration.RemoveMetadata(StringLengthAppendedKey);
        }

        return generator
            .PopUnescapedUtf8JsonStringIfAppendedInScope(typeDeclaration);
    }

    public static CodeGenerator AppendUnescapedUtf8JsonStringIfNotAppended(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool includeTokenTypeCheck = true)
    {
        if (typeDeclaration.TryGetMetadata(UnescapedUtf8JsonStringAppendedKey, out bool? _))
        {
            // If the variable was declared in the current scope, it is still
            // accessible — skip re-declaration.
            if (typeDeclaration.TryGetMetadata(UnescapedUtf8JsonStringAppendedInScopeKey, out string? scope)
                && scope == generator.FullyQualifiedScope)
            {
                return generator;
            }

            // The variable was declared in a different scope that may no longer
            // be active (e.g. a type-check else clause that has since closed).
            // Clear the stale metadata so we can re-declare in the current scope.
            typeDeclaration.RemoveMetadata(UnescapedUtf8JsonStringAppendedKey);
            typeDeclaration.RemoveMetadata(UnescapedUtf8JsonStringAppendedInScopeKey);
        }

        typeDeclaration.SetMetadata(UnescapedUtf8JsonStringAppendedKey, true);
        typeDeclaration.SetMetadata(UnescapedUtf8JsonStringAppendedInScopeKey, generator.FullyQualifiedScope);

        generator
            .AppendSeparatorLine()
            .ReserveName("unescapedUtf8JsonString");

        if (includeTokenTypeCheck)
        {
            generator
                .AppendLineIndent("using UnescapedUtf8JsonString unescapedUtf8JsonString = tokenType is JsonTokenType.String ? parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String) : default;");
        }
        else
        {
            generator
                .AppendLineIndent("using UnescapedUtf8JsonString unescapedUtf8JsonString = parentDocument.GetUtf8JsonString(parentIndex, JsonTokenType.String);");
        }

        return generator;
    }

    public static CodeGenerator PopUnescapedUtf8JsonStringIfAppendedInScope(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(UnescapedUtf8JsonStringAppendedInScopeKey, out string? scope)
            && scope == generator.FullyQualifiedScope)
        {
            typeDeclaration.RemoveMetadata(UnescapedUtf8JsonStringAppendedInScopeKey);
            typeDeclaration.RemoveMetadata(UnescapedUtf8JsonStringAppendedKey);
        }

        return generator;
    }

    public static CodeGenerator PopNormalizedJsonNumberIfAppendedInScope(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(NormalizedJsonNumberAppendedInScopeKey, out string? scope)
            && scope == generator.FullyQualifiedScope)
        {
            typeDeclaration.RemoveMetadata(NormalizedJsonNumberAppendedInScopeKey);
            typeDeclaration.RemoveMetadata(NormalizedJsonNumberAppendedKey);
        }

        return generator
            .PopGetRawSimpleValueIfAppendedInScope(typeDeclaration);
    }

    public static CodeGenerator AppendGetRawSimpleValueIfNotAppended(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool includeTokenTypeCheck = true)
    {
        if (typeDeclaration.TryGetMetadata(GetRawSimpleValueAppendedKey, out bool? _))
        {
            return generator;
        }

        typeDeclaration.SetMetadata(GetRawSimpleValueAppendedKey, true);
        typeDeclaration.SetMetadata(GetRawSimpleValueAppendedInScopeKey, generator.FullyQualifiedScope);

        generator
            .AppendSeparatorLine()
            .ReserveName("rawSimpleValue");

        if (includeTokenTypeCheck)
        {
            generator
                .AppendLineIndent("ReadOnlyMemory<byte> rawSimpleValue = tokenType is JsonTokenType.Number or JsonTokenType.String ? parentDocument.GetRawSimpleValue(parentIndex) : default;");
        }
        else
        {
            generator
                .AppendLineIndent("ReadOnlyMemory<byte> rawSimpleValue = parentDocument.GetRawSimpleValue(parentIndex);");
        }

        return generator;
    }

    public static CodeGenerator PopGetRawSimpleValueIfAppendedInScope(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(GetRawSimpleValueAppendedInScopeKey, out string? scope)
            && scope == generator.FullyQualifiedScope)
        {
            typeDeclaration.RemoveMetadata(GetRawSimpleValueAppendedKey);
            typeDeclaration.RemoveMetadata(GetRawSimpleValueAppendedInScopeKey);
        }

        return generator;
    }

    /// <summary>
    /// Appends the code to shortcut the return from validation if there is no collector.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="contextName">The name to use for the context (defaults to 'context').</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNoCollectorShortcutReturn(this CodeGenerator generator, string contextName = "context")
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (!", contextName, ".HasCollector)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the code to shortcut the return from validation if there is no collector.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="contextName">The name to use for the context (defaults to 'context').</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNoCollectorBooleanFalseShortcutReturn(this CodeGenerator generator, string contextName = "context")
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (!", contextName, ".HasCollector)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent(contextName, ".EvaluatedBooleanSchema(false);")
                .AppendLineIndent("return;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the code to shortcut the return from validation if there is no collector.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="contextName">The name to use for the context (defaults to 'context').</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNoCollectorNoMatchShortcutReturn(this CodeGenerator generator, string contextName = "context")
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (!", contextName, ".HasCollector && !", contextName, ".IsMatch)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the code to shortcut the return from validation if there is no collector.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="contextName">The name to use for the context (defaults to 'context').</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendConditionalNoCollectorNoMatchShortcutReturn(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeywordValidationHandler handler, string contextName = "context")
    {
        bool hasMoreHandlers = typeDeclaration
            .OrderedValidationHandlers(generator.LanguageProvider)
            .TakeWhile(h => h != handler)
            .Skip(1)
            .Any();

        if (hasMoreHandlers)
        {
            return AppendNoCollectorNoMatchShortcutReturn(generator, contextName);
        }

        return generator;
    }

    /// <summary>
    /// Prepend validation setup code for children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PrependChildValidationSetup(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority <= parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            child.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Append validation setup code for children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendChildValidationSetup(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority > parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            child.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Prepend validation code for appropriate children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PrependChildValidationCode(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool appendShortcut = false;

        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority <= parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            int initialLength = generator.Length;

            if (appendShortcut)
            {
                generator.AppendNoCollectorNoMatchShortcutReturn();
            }

            int length = generator.Length;

            child.AppendValidationCode(generator, typeDeclaration);

            if (length != generator.Length)
            {
                appendShortcut = true;
            }
            else
            {
                // Trim off the shortcut we appended
                // if we didn't append any validation code
                generator.Length = initialLength;
            }
        }

        return generator;
    }

    /// <summary>
    /// Append validation code for appropriate children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendChildValidationCode(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        bool appendShortcut = false;

        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority > parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            int initialLength = generator.Length;

            if (appendShortcut)
            {
                generator.AppendNoCollectorNoMatchShortcutReturn();
            }

            int length = generator.Length;

            child.AppendValidationCode(generator, typeDeclaration);

            if (length != generator.Length)
            {
                appendShortcut = true;
            }
            else
            {
                // Trim off the shortcut we appended
                // if we didn't append any validation code
                generator.Length = initialLength;
            }
        }

        return generator;
    }

    /// <summary>
    /// Appends the contants nested class containing property name constants.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property names class.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendConstantsClass(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.ValidationConstants() is not IReadOnlyDictionary<IValidationConstantProviderKeyword, JsonElement[]> constants
            || constants.Count == 0)
        {
            return generator;
        }

        var requiredConstants = constants.Where(k => !IsNotRequiredInConstantsClass(k.Key)).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        if (requiredConstants.Count == 0)
        {
            return generator;
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Provides accesors for enumerated values")
            .AppendLineIndent("/// </summary>")
            .BeginPrivateStaticClassDeclaration(generator.ConstantsClassName());

        foreach (KeyValuePair<IValidationConstantProviderKeyword, JsonElement[]> constant in requiredConstants.OrderBy(k => k.Key.Keyword))
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
                            generator.AppendArrayValidationConstantField(typeDeclaration, constant.Key, i, value);
                            break;

                        case JsonValueKind.Null:
                            generator.AppendNullValidationConstantField(typeDeclaration, constant.Key, i, value);
                            break;

                        case JsonValueKind.String:
                            generator.AppendStringValidationConstantField(typeDeclaration, constant.Key, i, value);
                            break;

                        case JsonValueKind.Number:
                            generator.AppendNumberValidationConstantField(typeDeclaration, constant.Key, i, value);
                            break;

                        case JsonValueKind.Object:
                            generator.AppendObjectValidationConstantField(typeDeclaration, constant.Key, i, value);
                            break;

                        case JsonValueKind.True:
                        case JsonValueKind.False:
                            generator.AppendBooleanValidationConstantField(typeDeclaration, constant.Key, i, value);
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

        return generator
            .EndClassStructOrEnumDeclaration();
    }

    /// <summary>
    /// Appends a public <c>EnumValues</c> class containing named properties for each constant
    /// defined by any-of constant validation keywords (e.g. <c>enum</c>).
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEnumValuesClass(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.AnyOfConstantValues() is not IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> anyOfConstants
            || anyOfConstants.Count == 0)
        {
            return generator;
        }

        // Only emit if we actually have values with derivable names.
        bool hasNameableValues = false;
        foreach (KeyValuePair<IAnyOfConstantValidationKeyword, JsonElement[]> kvp in anyOfConstants)
        {
            if (kvp.Value.Length > 0)
            {
                hasNameableValues = true;
                break;
            }
        }

        if (!hasNameableValues)
        {
            return generator;
        }

        string constantsClassName = generator.ConstantsClassName();
        string constantsScope = generator.ConstantsScope();
        string enumValuesScope = generator.EnumValuesScope();
        string dotnetTypeName = typeDeclaration.DotnetTypeName();

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Provides named constants for enum values.")
            .AppendLineIndent("/// </summary>")
            .BeginPublicStaticClassDeclaration(generator.EnumValuesClassName());

        foreach (KeyValuePair<IAnyOfConstantValidationKeyword, JsonElement[]> kvp in anyOfConstants.OrderBy(k => k.Key.Keyword))
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            JsonElement[] values = kvp.Value;
            int count = values.Length;
            if (count == 0)
            {
                continue;
            }

            string keywordName = kvp.Key.Keyword;
            bool addSuffix = count > 1;

            int elementIndex = 1;
            foreach (JsonElement value in values)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                string? suffix = addSuffix ? elementIndex.ToString() : null;

                AppendEnumValueProperty(generator, typeDeclaration, value, keywordName, suffix, constantsClassName, constantsScope, enumValuesScope, dotnetTypeName);

                elementIndex++;
            }
        }

        return generator
            .EndClassStructOrEnumDeclaration();

        static void AppendEnumValueProperty(
            CodeGenerator generator,
            TypeDeclaration typeDeclaration,
            JsonElement value,
            string keywordName,
            string? suffix,
            string constantsClassName,
            string constantsScope,
            string enumValuesScope,
            string dotnetTypeName)
        {
            // Compute the Constants field names by looking up names in the Constants scope.
            string utf8FieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);

            switch (value.ValueKind)
            {
                case JsonValueKind.String:
                {
                    string jsonFieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: $"Json{suffix}");
                    string propertyBaseName = value.GetString()!;
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(propertyBaseName, rootScope: enumValuesScope);
                    string utf8PropertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(propertyName, rootScope: enumValuesScope, suffix: "Utf8");

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the string ", SymbolDisplay.FormatLiteral(propertyBaseName, true))
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", jsonFieldName, ";");

                    generator
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the string ", SymbolDisplay.FormatLiteral(propertyBaseName, true))
                        .AppendLineIndent("/// as a UTF8 byte array.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ReadOnlySpan<byte> ")
                        .Append(utf8PropertyName)
                        .AppendLine(" => ", constantsClassName, ".", utf8FieldName, ";");
                }

                break;

                case JsonValueKind.Number:
                {
                    string jsonFieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: $"Json{suffix}");
                    string rawText = value.GetRawText();
                    string propertyBaseName = rawText.Replace(".", "Point").Replace("-", "Minus");
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(propertyBaseName, rootScope: enumValuesScope, prefix: "Number");

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the number ", rawText)
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", jsonFieldName, ";");
                }

                break;

                case JsonValueKind.True:
                {
                    string fieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("True", rootScope: enumValuesScope);

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the boolean <c>true</c>")
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", fieldName, ";");
                }

                break;

                case JsonValueKind.False:
                {
                    string fieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("False", rootScope: enumValuesScope);

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the boolean <c>false</c>")
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", fieldName, ";");
                }

                break;

                case JsonValueKind.Null:
                {
                    string fieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("Null", rootScope: enumValuesScope);

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the <c>null</c> value")
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", fieldName, ";");
                }

                break;

                case JsonValueKind.Array:
                case JsonValueKind.Object:
                {
                    string fieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);
                    string propertyBaseName = value.ValueKind == JsonValueKind.Array ? "ArrayValue" : "ObjectValue";
                    string propertyName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(propertyBaseName, rootScope: enumValuesScope, suffix: suffix);

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("/// <summary>")
                        .AppendLineIndent("/// Gets the ", value.ValueKind == JsonValueKind.Array ? "array" : "object", " value")
                        .AppendLineIndent("/// as a <see cref=\"", dotnetTypeName, "\"/>.")
                        .AppendLineIndent("/// </summary>")
                        .AppendIndent("public static ")
                        .Append(dotnetTypeName)
                        .Append(" ")
                        .Append(propertyName)
                        .AppendLine(" { get; } = ", constantsClassName, ".", fieldName, ";");
                }

                break;

                default:
                    break;
            }
        }
    }

    private static bool IsNotRequiredInConstantsClass(IValidationConstantProviderKeyword key)
    {
        // We do not require the various numeric constants for validation.
        return key is
            INumberConstantValidationKeyword or IIntegerConstantValidationKeyword or
            IStringLengthConstantValidationKeyword or
            IPropertyCountConstantValidationKeyword or
            IArrayLengthConstantValidationKeyword or IArrayContainsCountConstantValidationKeyword;
    }

    public static CodeGenerator AppendIgnoredCoreTypeStringFormatKeywords(
    this CodeGenerator generator,
    TypeDeclaration typeDeclaration,
    string ignoredMessageProviderName)
    {
        return AppendIgnoredCoreTypeFormatKeywords(generator, typeDeclaration, ignoredMessageProviderName, CoreTypes.String);
    }

    public static CodeGenerator AppendIgnoredCoreTypeNumberFormatKeywords(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string ignoredMessageProviderName)
    {
        return generator
            .AppendIgnoredCoreTypeFormatKeywords(typeDeclaration, ignoredMessageProviderName, CoreTypes.Number | CoreTypes.Integer);
    }

    public static CodeGenerator ElseAppendIgnoredCoreTypeStringFormatKeywords(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string ignoredMessageProviderName)
    {
        return AppendIgnoredCoreTypeFormatKeywords(generator, typeDeclaration, ignoredMessageProviderName, CoreTypes.String, includeElse: true);
    }

    public static CodeGenerator ElseAppendIgnoredCoreTypeNumberFormatKeywords(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string ignoredMessageProviderName)
    {
        return generator
            .AppendIgnoredCoreTypeFormatKeywords(typeDeclaration, ignoredMessageProviderName, CoreTypes.Number | CoreTypes.Integer, includeElse: true);
    }

    public static CodeGenerator AppendIgnoredCoreTypeFormatKeywords(this CodeGenerator generator, TypeDeclaration typeDeclaration, string ignoredMessageProviderName, CoreTypes coreType, bool includeElse = false)
    {
        IEnumerable<IFormatProviderKeyword> ignoredKeywords =
            typeDeclaration
                .Keywords()
                .OfType<IFormatProviderKeyword>();

        bool hasElse = false;

        foreach (IFormatProviderKeyword keyword in ignoredKeywords)
        {
            if (((keyword.ImpliesCoreTypes(typeDeclaration) & coreType) != 0))
            {
                typeDeclaration.AddIgnoredKeyword(keyword);

                if (includeElse && !hasElse)
                {
                    hasElse = true;
                    generator
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent();
                }

                generator
                    .AppendLineIndent("context.IgnoredKeyword(", ignoredMessageProviderName, ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8);");
            }
        }

        if (hasElse)
        {
            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    public static CodeGenerator AppendIgnoredCoreTypeKeywords<T>(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string ignoredMessageProviderName)
            where T : IValidationKeyword
    {
        IEnumerable<T> keywordsToIgnore =
            typeDeclaration
                .Keywords()
                .OfType<T>();

        foreach (T keyword in keywordsToIgnore)
        {
            if (typeDeclaration.AddIgnoredKeyword(keyword))
            {
                generator
                    .AppendLineIndent("context.IgnoredKeyword(", ignoredMessageProviderName, ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8);");
            }
        }

        return generator;
    }

    public static bool HasIgnoredCoreTypeKeywords<T>(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return typeDeclaration
        .Keywords()
        .OfType<T>()
        .Any();
    }

    public static CodeGenerator TryAppendIgnoredCoreTypeKeywords<T>(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string ignoredMessageProviderName,
        Action<CodeGenerator, TypeDeclaration>? preAppendAction,
        ref bool appended)
        where T : IValidationKeyword
    {
        IEnumerable<T> keywordsToIgnore =
            typeDeclaration
                .Keywords()
                .OfType<T>();

        foreach (T keyword in keywordsToIgnore)
        {
            if (typeDeclaration.AddIgnoredKeyword(keyword))
            {
                if (!appended)
                {
                    preAppendAction?.Invoke(generator, typeDeclaration);
                }

                generator
                    .AppendLineIndent("context.IgnoredKeyword(", ignoredMessageProviderName, ", ", SymbolDisplay.FormatLiteral(keyword.Keyword, true), "u8);");
                appended |= true;
            }
        }

        return generator;
    }

    private static CodeGenerator AppendArrayValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
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
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .Append(" = ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(".ParseValue(")
            .AppendSerializedArrayStringLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendObjectValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
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
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .Append(" = ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(".ParseValue(")
            .AppendSerializedObjectStringLiteral(value)
            .AppendLine(");");

        return generator;
    }

    private static CodeGenerator AppendNullValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
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
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .AppendLine(" = ParsedJsonDocument<", typeDeclaration.DotnetTypeName(), ">.Null;");

        return generator;
    }

    private static CodeGenerator AppendBooleanValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
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
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .AppendLine(" = ParsedJsonDocument<", typeDeclaration.DotnetTypeName(), ">.", value.ValueKind == JsonValueKind.True ? "True" : "False", ";");

        return generator;
    }

    private static CodeGenerator AppendStringValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.String, "The value must be a string.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());
        string jsonMemberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: $"Json{index?.ToString()}");

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly byte[] ")
            .Append(memberName)
            .AppendLine(" = ", SymbolDisplay.FormatLiteral(value.GetString()!, true), "u8.ToArray();");

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(jsonMemberName)
            .AppendLine(" = ParsedJsonDocument<", typeDeclaration.DotnetTypeName(), ">.StringConstant([..", SymbolDisplay.FormatLiteral(value.GetRawText(), true), "u8]);");

        return generator;
    }

    private static CodeGenerator AppendNumberValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString());
        string jsonMemberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: $"Json{index?.ToString()}");

#if BUILDING_SOURCE_GENERATOR
        JsonElementHelpers.ParseNumber(Encoding.UTF8.GetBytes(value.GetRawText()), out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
#else
        JsonElementHelpers.ParseNumber(JsonMarshal.GetRawUtf8Value(value), out bool isNegative, out ReadOnlySpan<byte> integral, out ReadOnlySpan<byte> fractional, out int exponent);
#endif
        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly NormalizedJsonNumber ")
            .Append(memberName)
            .AppendLine(" = new(", isNegative ? "true" : "false", ", [..\"", Encoding.UTF8.GetString(integral.ToArray()), "\"u8], [..\"", Encoding.UTF8.GetString(fractional.ToArray()), "\"u8], ", exponent.ToString(), ");");

        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(jsonMemberName)
            .AppendLine(" = ParsedJsonDocument<", typeDeclaration.DotnetTypeName(), ">.NumberConstant([..", SymbolDisplay.FormatLiteral(value.GetRawText(), true), "u8]);");

        return generator;
    }

    /// <summary>
    /// Emits a static <c>EnumStringMap</c> field for oneOf discriminator-based dispatch
    /// when a discriminator property has been detected for the given type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the fields.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// <para>
    /// This is called at JsonSchema class scope so the emitted field is a class-level static.
    /// The field name, discriminator property name, and discriminator values are stored in type
    /// metadata so that the oneOf validation handler can reference them.
    /// </para>
    /// </remarks>
    public static CodeGenerator AppendOneOfDiscriminatorMapFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.OneOfCompositionTypes() is not { } oneOf)
        {
            return generator;
        }

        foreach (KeyValuePair<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> kvp in oneOf)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            IOneOfSubschemaValidationKeyword keyword = kvp.Key;
            IReadOnlyCollection<TypeDeclaration> subschemaTypes = kvp.Value;

            if (!TryGetOneOfDiscriminator(subschemaTypes, out string? discriminatorPropertyName, out List<(string Value, int BranchIndex)>? discriminatorValues, out JsonValueKind discriminatorValueKind))
            {
                continue;
            }

            // Store discriminator metadata for the validation handler to use
            typeDeclaration.SetMetadata(OneOfDiscriminatorPropertyNameKeyPrefix + keyword.Keyword, discriminatorPropertyName);
            typeDeclaration.SetMetadata(OneOfDiscriminatorValuesKeyPrefix + keyword.Keyword, discriminatorValues);
            typeDeclaration.SetMetadata(OneOfDiscriminatorValueKindKeyPrefix + keyword.Keyword, discriminatorValueKind);

            // Only emit a hash map field when there are enough string branches to justify it.
            // Numeric discriminators use sequential comparison (CompareNormalizedJsonNumbers).
            if (discriminatorValueKind == JsonValueKind.String && discriminatorValues.Count > MinEnumValuesForHashSet)
            {
                string fieldName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("OneOfDiscriminatorMap");
                string builderName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("BuildOneOfDiscriminatorMap");

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private static EnumStringMap ", builderName, "()")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return new EnumStringMap([")
                        .PushIndent();

                foreach ((string value, _) in discriminatorValues)
                {
                    string quotedValue = SymbolDisplay.FormatLiteral(value, true);
                    generator
                        .AppendLineIndent("static () => ", quotedValue, "u8,");
                }

                generator
                        .PopIndent()
                        .AppendLineIndent("]);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private static EnumStringMap ", fieldName, " { get; } = ", builderName, "();");

                typeDeclaration.SetMetadata(OneOfDiscriminatorMapFieldNameKeyPrefix + keyword.Keyword, fieldName);
            }
        }

        return generator;
    }

    /// <summary>
    /// Tries to get the discriminator metadata for a oneOf keyword.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keywordName">The keyword name (e.g. "oneOf").</param>
    /// <param name="discriminatorPropertyName">When successful, the JSON property name of the discriminator.</param>
    /// <param name="discriminatorValues">When successful, the list of (value, branchIndex) pairs.</param>
    /// <param name="discriminatorValueKind">When successful, the <see cref="JsonValueKind"/> of the discriminator values.</param>
    /// <param name="mapFieldName">When successful and a hash map was emitted, the field name; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if discriminator metadata was found; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetOneOfDiscriminatorMetadata(
        this TypeDeclaration typeDeclaration,
        string keywordName,
        [NotNullWhen(true)] out string? discriminatorPropertyName,
        [NotNullWhen(true)] out List<(string Value, int BranchIndex)>? discriminatorValues,
        out JsonValueKind discriminatorValueKind,
        out string? mapFieldName)
    {
        discriminatorPropertyName = null;
        discriminatorValues = null;
        discriminatorValueKind = default;
        mapFieldName = null;

        if (!typeDeclaration.TryGetMetadata(OneOfDiscriminatorPropertyNameKeyPrefix + keywordName, out string? propName) ||
            propName is null ||
            !typeDeclaration.TryGetMetadata(OneOfDiscriminatorValuesKeyPrefix + keywordName, out List<(string Value, int BranchIndex)>? values) ||
            values is null)
        {
            return false;
        }

        discriminatorPropertyName = propName;
        discriminatorValues = values;
        typeDeclaration.TryGetMetadata(OneOfDiscriminatorValueKindKeyPrefix + keywordName, out discriminatorValueKind);
        typeDeclaration.TryGetMetadata(OneOfDiscriminatorMapFieldNameKeyPrefix + keywordName, out mapFieldName);
        return true;
    }

    /// <summary>
    /// Emits static <see cref="EnumStringMap"/> fields for anyOf discriminator-based dispatch.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the fields.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAnyOfDiscriminatorMapFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.AnyOfCompositionTypes() is not { } anyOf)
        {
            return generator;
        }

        foreach (KeyValuePair<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> kvp in anyOf)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            IAnyOfSubschemaValidationKeyword keyword = kvp.Key;
            IReadOnlyCollection<TypeDeclaration> subschemaTypes = kvp.Value;

            if (!TryGetOneOfDiscriminator(subschemaTypes, out string? discriminatorPropertyName, out List<(string Value, int BranchIndex)>? discriminatorValues, out JsonValueKind discriminatorValueKind, requireRequired: false, allowPartial: true))
            {
                continue;
            }

            typeDeclaration.SetMetadata(AnyOfDiscriminatorPropertyNameKeyPrefix + keyword.Keyword, discriminatorPropertyName);
            typeDeclaration.SetMetadata(AnyOfDiscriminatorValuesKeyPrefix + keyword.Keyword, discriminatorValues);
            typeDeclaration.SetMetadata(AnyOfDiscriminatorValueKindKeyPrefix + keyword.Keyword, discriminatorValueKind);

            // Only emit a hash map field when there are enough string branches to justify it.
            // Numeric discriminators use sequential comparison (CompareNormalizedJsonNumbers).
            if (discriminatorValueKind == JsonValueKind.String && discriminatorValues.Count > MinEnumValuesForHashSet)
            {
                string fieldName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("AnyOfDiscriminatorMap");
                string builderName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("BuildAnyOfDiscriminatorMap");

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private static EnumStringMap ", builderName, "()")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return new EnumStringMap([")
                        .PushIndent();

                foreach ((string value, _) in discriminatorValues)
                {
                    string quotedValue = SymbolDisplay.FormatLiteral(value, true);
                    generator
                        .AppendLineIndent("static () => ", quotedValue, "u8,");
                }

                generator
                        .PopIndent()
                        .AppendLineIndent("]);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private static EnumStringMap ", fieldName, " { get; } = ", builderName, "();");

                typeDeclaration.SetMetadata(AnyOfDiscriminatorMapFieldNameKeyPrefix + keyword.Keyword, fieldName);
            }
        }

        return generator;
    }

    /// <summary>
    /// Tries to get the discriminator metadata for an anyOf keyword.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keywordName">The keyword name (e.g. "anyOf").</param>
    /// <param name="discriminatorPropertyName">When successful, the JSON property name of the discriminator.</param>
    /// <param name="discriminatorValues">When successful, the list of (value, branchIndex) pairs.</param>
    /// <param name="discriminatorValueKind">When successful, the <see cref="JsonValueKind"/> of the discriminator values.</param>
    /// <param name="mapFieldName">When successful and a hash map was emitted, the field name; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if discriminator metadata was found; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetAnyOfDiscriminatorMetadata(
        this TypeDeclaration typeDeclaration,
        string keywordName,
        [NotNullWhen(true)] out string? discriminatorPropertyName,
        [NotNullWhen(true)] out List<(string Value, int BranchIndex)>? discriminatorValues,
        out JsonValueKind discriminatorValueKind,
        out string? mapFieldName)
    {
        discriminatorPropertyName = null;
        discriminatorValues = null;
        discriminatorValueKind = default;
        mapFieldName = null;

        if (!typeDeclaration.TryGetMetadata(AnyOfDiscriminatorPropertyNameKeyPrefix + keywordName, out string? propName) ||
            propName is null ||
            !typeDeclaration.TryGetMetadata(AnyOfDiscriminatorValuesKeyPrefix + keywordName, out List<(string Value, int BranchIndex)>? values) ||
            values is null)
        {
            return false;
        }

        discriminatorPropertyName = propName;
        discriminatorValues = values;
        typeDeclaration.TryGetMetadata(AnyOfDiscriminatorValueKindKeyPrefix + keywordName, out discriminatorValueKind);
        typeDeclaration.TryGetMetadata(AnyOfDiscriminatorMapFieldNameKeyPrefix + keywordName, out mapFieldName);
        return true;
    }

    /// <summary>
    /// Emits static <see cref="EnumStringSet"/> fields for any-of constant validation keywords
    /// that have more than <see cref="MinEnumValuesForHashSet"/> string enum values.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the fields.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is called at JsonSchema class scope so the emitted fields are class-level statics.
    /// The field names are stored in type metadata so that the validation code can reference them.
    /// </remarks>
    public static CodeGenerator AppendEnumStringSetFields(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (typeDeclaration.AnyOfConstantValues() is not IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> constDictionary)
        {
            return generator;
        }

        foreach (KeyValuePair<IAnyOfConstantValidationKeyword, JsonElement[]> entry in constDictionary)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            IAnyOfConstantValidationKeyword keyword = entry.Key;
            JsonElement[] elements = entry.Value;

            JsonElement[] stringValues = elements.Where(e => e.ValueKind == JsonValueKind.String).ToArray();

            if (stringValues.Length <= MinEnumValuesForHashSet)
            {
                continue;
            }

            string fieldName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("EnumStringSet");
            string builderName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("BuildEnumStringSet");

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("private static EnumStringSet ", builderName, "()")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new EnumStringSet([")
                    .PushIndent();

            foreach (JsonElement value in stringValues)
            {
                string quotedValue = SymbolDisplay.FormatLiteral(value.GetString()!, true);
                generator
                    .AppendLineIndent("static () => ", quotedValue, "u8,");
            }

            generator
                    .PopIndent()
                    .AppendLineIndent("]);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("private static EnumStringSet ", fieldName, " { get; } = ", builderName, "();");

            typeDeclaration.SetMetadata(EnumStringSetFieldNameKeyPrefix + keyword.Keyword, fieldName);
        }

        return generator;
    }

    /// <summary>
    /// Tries to get the name of the <see cref="EnumStringSet"/> field that was emitted for the
    /// given keyword, if one was generated.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="keywordName">The keyword name (e.g. "enum").</param>
    /// <param name="fieldName">When this method returns <see langword="true"/>, contains the field name.</param>
    /// <returns><see langword="true"/> if a hash set field was emitted; otherwise <see langword="false"/>.</returns>
    public static bool TryGetEnumStringSetFieldName(this TypeDeclaration typeDeclaration, string keywordName, [NotNullWhen(true)] out string? fieldName)
    {
        return typeDeclaration.TryGetMetadata(EnumStringSetFieldNameKeyPrefix + keywordName, out fieldName);
    }

    /// <summary>
    /// Tries to detect a discriminator property across oneOf/anyOf subschemas.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A discriminator is a property that has a single string constant value
    /// (either <c>const: "X"</c> or <c>enum: ["X"]</c>) with distinct values
    /// across branches.
    /// </para>
    /// <para>
    /// When <paramref name="allowPartial"/> is <see langword="false"/> (the default,
    /// used for oneOf), the discriminator must be present in ALL branches.
    /// When <paramref name="allowPartial"/> is <see langword="true"/> (used for anyOf),
    /// branches that lack the discriminator property are skipped — the fast-path
    /// default case will fall through to sequential evaluation for such branches.
    /// </para>
    /// <para>
    /// The method tries each branch as a potential seed for candidate property names,
    /// so it works even when the first branch has no properties (e.g. <c>type: boolean</c>).
    /// </para>
    /// </remarks>
    /// <param name="subschemaTypes">The oneOf/anyOf branch type declarations.</param>
    /// <param name="discriminatorPropertyName">When successful, the JSON property name of the discriminator.</param>
    /// <param name="discriminatorValues">When successful, a list of (utf8Value, branchIndex) pairs for discriminated branches.</param>
    /// <param name="discriminatorValueKind">When successful, the <see cref="JsonValueKind"/> of the discriminator values (all values share the same kind).</param>
    /// <param name="requireRequired">When <see langword="true"/>, only required properties are considered.</param>
    /// <param name="allowPartial">When <see langword="true"/>, branches without the discriminator property are skipped instead of failing.</param>
    /// <returns><see langword="true"/> if a discriminator was detected; otherwise, <see langword="false"/>.</returns>
    public static bool TryGetOneOfDiscriminator(
        IReadOnlyCollection<TypeDeclaration> subschemaTypes,
        [NotNullWhen(true)] out string? discriminatorPropertyName,
        [NotNullWhen(true)] out List<(string Value, int BranchIndex)>? discriminatorValues,
        out JsonValueKind discriminatorValueKind,
        bool requireRequired = true,
        bool allowPartial = false)
    {
        discriminatorPropertyName = null;
        discriminatorValues = null;
        discriminatorValueKind = default;

        if (subschemaTypes.Count < 2)
        {
            return false;
        }

        TypeDeclaration[] branches = subschemaTypes.ToArray();

        // Try each branch as a seed for candidate discriminator properties.
        // This handles cases where early branches have no properties (e.g. type: boolean).
        for (int seedIdx = 0; seedIdx < branches.Length; seedIdx++)
        {
            IReadOnlyList<PropertyDeclaration> seedProps = branches[seedIdx].PropertyDeclarations;

            foreach (PropertyDeclaration candidateProp in seedProps)
            {
                if (requireRequired && candidateProp.RequiredOrOptional == RequiredOrOptional.Optional)
                {
                    continue;
                }

                string candidateName = candidateProp.JsonPropertyName;

                if (!TryGetSingleConstant(candidateProp, out string? seedValue, out JsonValueKind seedKind))
                {
                    continue;
                }

                var values = new List<(string Value, int BranchIndex)>(branches.Length)
                {
                    (seedValue, seedIdx),
                };

                HashSet<string> seenValues = [seedValue];
                bool isViable = true;

                for (int i = 0; i < branches.Length; i++)
                {
                    if (i == seedIdx)
                    {
                        continue;
                    }

                    PropertyDeclaration? matchingProp = FindPropertyByName(branches[i].PropertyDeclarations, candidateName);

                    if (matchingProp is null)
                    {
                        if (allowPartial)
                        {
                            // Branch doesn't have the discriminator property — skip it.
                            // The fast-path default case will fall through to sequential evaluation.
                            continue;
                        }

                        isViable = false;
                        break;
                    }

                    if ((requireRequired && matchingProp.RequiredOrOptional == RequiredOrOptional.Optional) ||
                        !TryGetSingleConstant(matchingProp, out string? branchValue, out JsonValueKind branchKind) ||
                        branchKind != seedKind ||
                        !seenValues.Add(branchValue))
                    {
                        isViable = false;
                        break;
                    }

                    values.Add((branchValue, i));
                }

                if (isViable && values.Count >= 2)
                {
                    discriminatorPropertyName = candidateName;
                    discriminatorValues = values;
                    discriminatorValueKind = seedKind;
                    return true;
                }
            }
        }

        return false;
    }

    private static bool TryGetSingleConstant(PropertyDeclaration prop, [NotNullWhen(true)] out string? value, out JsonValueKind constKind)
    {
        value = null;
        constKind = default;
        TypeDeclaration propType = prop.UnreducedPropertyType;

        // Check const keyword first
        JsonElement constValue = propType.SingleConstantValue();
        if (constValue.ValueKind == JsonValueKind.String)
        {
            value = constValue.GetString()!;
            constKind = JsonValueKind.String;
            return true;
        }

        if (constValue.ValueKind == JsonValueKind.Number)
        {
            value = constValue.GetRawText();
            constKind = JsonValueKind.Number;
            return true;
        }

        // Check enum keyword for single-value string or number enum (e.g. enum: ["Point"] or enum: [1])
        if (propType.AnyOfConstantValues() is IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> constDict)
        {
            foreach (KeyValuePair<IAnyOfConstantValidationKeyword, JsonElement[]> entry in constDict)
            {
                if (entry.Value.Length != 1)
                {
                    continue;
                }

                JsonElement singleElement = entry.Value[0];
                if (singleElement.ValueKind == JsonValueKind.String)
                {
                    value = singleElement.GetString()!;
                    constKind = JsonValueKind.String;
                    return true;
                }

                if (singleElement.ValueKind == JsonValueKind.Number)
                {
                    value = singleElement.GetRawText();
                    constKind = JsonValueKind.Number;
                    return true;
                }
            }
        }

        return false;
    }

    private static PropertyDeclaration? FindPropertyByName(IReadOnlyList<PropertyDeclaration> properties, string jsonPropertyName)
    {
        foreach (PropertyDeclaration prop in properties)
        {
            if (prop.JsonPropertyName == jsonPropertyName)
            {
                return prop;
            }
        }

        return null;
    }

    /// <summary>
    /// Determines whether a composition branch's validation can be hoisted into a parent's
    /// property enumeration loop instead of being called as an opaque <c>Evaluate()</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A type declaration is hoistable if its validation consists entirely of:
    /// <list type="bullet">
    ///   <item>Core type checks (must allow object)</item>
    ///   <item>Object property validation keywords (properties, patternProperties,
    ///         additionalProperties, propertyNames, required, minProperties, maxProperties,
    ///         dependentRequired, dependentSchemas)</item>
    ///   <item>Composition keywords (allOf, anyOf, oneOf) where ALL sub-branches are
    ///         themselves hoistable (recursive check)</item>
    /// </list>
    /// </para>
    /// <para>
    /// Keywords that block hoisting include: const, enum, string, number, array, format,
    /// if/then/else, and not.
    /// </para>
    /// </remarks>
    /// <param name="typeDeclaration">The type declaration to check.</param>
    /// <returns><see langword="true"/> if the type can be hoisted; otherwise, <see langword="false"/>.</returns>
    public static bool IsHoistableObjectSubschema(TypeDeclaration typeDeclaration)
    {
        return IsHoistableObjectSubschemaCore(typeDeclaration, []);
    }

    private static bool IsHoistableObjectSubschemaCore(TypeDeclaration typeDeclaration, HashSet<TypeDeclaration> visited)
    {
        // Guard against circular references
        if (!visited.Add(typeDeclaration))
        {
            return true;
        }

        // Follow reduction (e.g. $ref targets)
        ReducedTypeDeclaration reduced = typeDeclaration.ReducedTypeDeclaration();
        TypeDeclaration effectiveType = reduced.ReducedType;

        // If the effective type is different, check it instead (but keep our visited set)
        if (!ReferenceEquals(effectiveType, typeDeclaration))
        {
            if (!visited.Add(effectiveType))
            {
                return true;
            }
        }

        // The type must allow object (it may also allow other types, but we only hoist
        // object property evaluation — the type check itself is handled by the parent)
        CoreTypes allowed = effectiveType.AllowedCoreTypes();
        if (allowed != CoreTypes.None && (allowed & CoreTypes.Object) == 0)
        {
            // This type explicitly disallows object — it can't be property-hoisted
            return false;
        }

        // Check all validation keywords — only leaf property schemas are hoistable.
        // We require that every validation keyword is one of: type check, properties, or required.
        // Composition keywords (allOf, anyOf, oneOf, $ref) are NOT hoistable because
        // the deepest composition evaluation must occur first with contexts committed
        // bottom-up. We only hoist flat property+required schemas into the parent's loop.
        IReadOnlyCollection<IValidationKeyword> keywords = effectiveType.ValidationKeywords();
        bool hasPropertyKeyword = false;
        foreach (IValidationKeyword keyword in keywords)
        {
            if (keyword is ICoreTypeValidationKeyword)
            {
                // Type checks are hoistable (shared with parent)
                continue;
            }

            if (keyword is IObjectPropertyValidationKeyword)
            {
                hasPropertyKeyword = true;
                continue;
            }

            if (keyword is IObjectRequiredPropertyValidationKeyword)
            {
                // Properties and required keywords are hoistable into the parent's property loop
                continue;
            }

            // Any other validation keyword blocks hoisting:
            // - IObjectValidationKeyword (additionalProperties, patternProperties, etc.)
            // - Composition keywords (allOf, anyOf, oneOf, $ref)
            // - Constraint keywords (string, number, array, format, const, enum, etc.)
            return false;
        }

        // Must have at least one properties keyword — boolean schemas and empty schemas
        // have nothing to hoist and must be evaluated normally.
        return hasPropertyKeyword;
    }

    /// <summary>
    /// Information about an allOf branch that has been hoisted into the parent's property loop.
    /// </summary>
    public readonly struct HoistedAllOfBranchInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="HoistedAllOfBranchInfo"/> struct.
        /// </summary>
        public HoistedAllOfBranchInfo(
            int branchIndex,
            TypeDeclaration subschemaType,
            TypeDeclaration reducedType,
            string targetTypeName,
            string jsonSchemaClassName,
            string evalPathPropertyName)
        {
            BranchIndex = branchIndex;
            SubschemaType = subschemaType;
            ReducedType = reducedType;
            TargetTypeName = targetTypeName;
            JsonSchemaClassName = jsonSchemaClassName;
            EvalPathPropertyName = evalPathPropertyName;
        }

        /// <summary>Gets the index of the branch in the allOf composition.</summary>
        public int BranchIndex { get; }

        /// <summary>Gets the original (unreduced) type declaration of the branch.</summary>
        public TypeDeclaration SubschemaType { get; }

        /// <summary>Gets the reduced type declaration of the branch.</summary>
        public TypeDeclaration ReducedType { get; }

        /// <summary>Gets the fully qualified .NET type name of the reduced type.</summary>
        public string TargetTypeName { get; }

        /// <summary>Gets the name of the JsonSchema class for the reduced type.</summary>
        public string JsonSchemaClassName { get; }

        /// <summary>Gets the name of the evaluation path property in the parent scope.</summary>
        public string EvalPathPropertyName { get; }
    }

    /// <summary>
    /// Stores hoisted allOf branch info as metadata on the type declaration.
    /// </summary>
    public static void SetHoistedAllOfBranches(
        TypeDeclaration typeDeclaration,
        string keywordName,
        List<HoistedAllOfBranchInfo> branches)
    {
        typeDeclaration.SetMetadata(HoistedAllOfBranchesKeyPrefix + keywordName, branches);
    }

    /// <summary>
    /// Tries to retrieve hoisted allOf branch info from the type declaration's metadata.
    /// </summary>
    public static bool TryGetHoistedAllOfBranches(
        TypeDeclaration typeDeclaration,
        string keywordName,
        [NotNullWhen(true)] out List<HoistedAllOfBranchInfo>? branches)
    {
        if (typeDeclaration.TryGetMetadata(HoistedAllOfBranchesKeyPrefix + keywordName, out branches) &&
            branches is not null)
        {
            return true;
        }

        branches = null;
        return false;
    }
}