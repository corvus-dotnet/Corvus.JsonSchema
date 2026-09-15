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
using System.Globalization;
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

                string? suffix = addSuffix ? elementIndex.ToString(CultureInfo.InvariantCulture) : null;

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

    private static CodeGenerator AppendArrayValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Array, "The value must be an array.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));

        // The constant is materialised by calling the generated type's own ParseValue, which
        // is emitted with [Obsolete] to steer consumers towards pooled parsing. This use is
        // intentional (a process-lifetime static cannot use a pooled parse, and the
        // ParsedJsonDocument constants only represent scalar values), so suppress the
        // obsolete diagnostic in the generated code.
        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("#pragma warning disable CS0618 // Type or member is obsolete")
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .Append(" = ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(".ParseValue(")
            .AppendSerializedArrayStringLiteral(value)
            .AppendLine(");")
            .AppendLineIndent("#pragma warning restore CS0618");

        return generator;
    }

    private static CodeGenerator AppendObjectValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Object, "The value must be an object.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));

        // The constant is materialised by calling the generated type's own ParseValue, which
        // is emitted with [Obsolete] to steer consumers towards pooled parsing. This use is
        // intentional (a process-lifetime static cannot use a pooled parse, and the
        // ParsedJsonDocument constants only represent scalar values), so suppress the
        // obsolete diagnostic in the generated code.
        generator
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A constant for the <c>", keyword.Keyword, "</c> keyword.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("#pragma warning disable CS0618 // Type or member is obsolete")
            .AppendIndent("public static readonly ", typeDeclaration.DotnetTypeName(), " ")
            .Append(memberName)
            .Append(" = ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(".ParseValue(")
            .AppendSerializedObjectStringLiteral(value)
            .AppendLine(");")
            .AppendLineIndent("#pragma warning restore CS0618");

        return generator;
    }

    private static CodeGenerator AppendNullValidationConstantField(this CodeGenerator generator, TypeDeclaration typeDeclaration, IKeyword keyword, int? index, in JsonElement value)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        Debug.Assert(value.ValueKind == JsonValueKind.Null, "The value must be null.");

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));

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

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));

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

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));
        string jsonMemberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: $"Json{index?.ToString(CultureInfo.InvariantCulture)}");

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

        string memberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: index?.ToString(CultureInfo.InvariantCulture));
        string jsonMemberName = generator.GetStaticReadOnlyFieldNameInScope(keyword.Keyword, suffix: $"Json{index?.ToString(CultureInfo.InvariantCulture)}");

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
            .AppendLine(" = new(", isNegative ? "true" : "false", ", [..\"", Encoding.UTF8.GetString(integral.ToArray()), "\"u8], [..\"", Encoding.UTF8.GetString(fractional.ToArray()), "\"u8], ", exponent.ToString(CultureInfo.InvariantCulture), ");");

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
}