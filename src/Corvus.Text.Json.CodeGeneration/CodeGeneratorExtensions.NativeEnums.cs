// <copyright file="CodeGeneratorExtensions.NativeEnums.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json.CodeGeneration;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generation extensions for native C# enum emission (issue #948).
/// </summary>
internal static partial class CodeGenerationExtensions
{
    private const string KnownValuesEnumBaseName = "KnownValues";
    private const string KnownValuesEnumNameKey = "CSharp_JsonSchema_KnownValuesEnumNameKey";
    private const string KnownValuesMembersKey = "CSharp_LanguageProvider_KnownValuesMembers";

    /// <summary>
    /// Make the KnownValues enum name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushKnownValuesEnumNameAndScope(this CodeGenerator generator)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (generator.TryPeekMetadata(KnownValuesEnumNameKey, out (string, string) _))
        {
            return generator;
        }

        string knownValuesEnum = generator.GetTypeNameInScope(KnownValuesEnumBaseName);
        return generator
            .PushMetadata(KnownValuesEnumNameKey, (knownValuesEnum, generator.GetChildScope(knownValuesEnum, null)));
    }

    /// <summary>
    /// Remove the KnownValues enum name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopKnownValuesEnumNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(KnownValuesEnumNameKey);
    }

    /// <summary>
    /// Gets the ambient KnownValues enum name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The enum name.</returns>
    public static string KnownValuesEnumName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(KnownValuesEnumNameKey, out (string, string)? value) &&
            value is (string enumName, string _))
        {
            return enumName;
        }

        throw new InvalidOperationException(SR.KnownValuesEnumNameNotCreated);
    }

    /// <summary>
    /// Gets the KnownValues enum scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified enum scope.</returns>
    public static string KnownValuesEnumScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(KnownValuesEnumNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException(SR.KnownValuesEnumScopeNotCreated);
    }

    /// <summary>
    /// Appends a public nested native C# enum whose members correspond to the constants defined
    /// by any-of constant validation keywords (e.g. <c>enum</c>), for a pure string enum.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKnownValuesEnum(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.HasNativeStringEnum())
        {
            return generator;
        }

        IReadOnlyList<KnownValuesMember> members = GetOrBuildKnownValuesMembers(generator, typeDeclaration);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// A native enum for the well-known values of this type.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <remarks>")
            .AppendLineIndent("/// Member ordinals follow the schema declaration order. Inserting or reordering values")
            .AppendLineIndent("/// in the schema renumbers the ordinals, so do not persist their integer values.")
            .AppendLineIndent("/// </remarks>")
            .BeginEnum(GeneratedTypeAccessibility.Public, generator.KnownValuesEnumName());

        int ordinal = 0;
        foreach (KnownValuesMember member in members)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Corresponds to the JSON string ", SymbolDisplay.FormatLiteral(member.JsonString, true), ".")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent(member.MemberName, " = ", ordinal.ToString(), ",");

            ordinal++;
        }

        return generator
            .EndClassStructOrEnumDeclaration();
    }

    /// <summary>
    /// Appends the conversions between the containing type and its nested KnownValues enum.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="forMutable">If <see langword="true"/>, emit for the mutable variant of the type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKnownValuesConversions(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.HasNativeStringEnum())
        {
            return generator;
        }

        IReadOnlyList<KnownValuesMember> members = GetOrBuildKnownValuesMembers(generator, typeDeclaration);
        string enumName = generator.KnownValuesEnumName();
        string constantsClassName = generator.ConstantsClassName();
        string targetTypeName = forMutable ? generator.MutableClassName() : typeDeclaration.DotnetTypeName();

        if (!forMutable)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Converts a <see cref=\"", enumName, "\"/> to an instance of this type.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The well-known value from which to convert.</param>")
                .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value was not a defined member of the <see cref=\"", enumName, "\"/> enumeration.</exception>")
                .AppendLineIndent("public static implicit operator ", targetTypeName, "(", enumName, " value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return value switch")
                    .AppendLineIndent("{")
                    .PushIndent();

            foreach (KnownValuesMember member in members)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                generator
                    .AppendLineIndent(enumName, ".", member.MemberName, " => ", constantsClassName, ".", member.JsonFieldName, ",");
            }

            generator
                    .AppendLineIndent("_ => throw new InvalidOperationException(),")
                    .PopIndent()
                    .AppendLineIndent("};")
                .PopIndent()
                .AppendLineIndent("}");
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Converts the value to its <see cref=\"", enumName, "\"/> equivalent.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendLineIndent("/// <exception cref=\"InvalidOperationException\">The value did not match a well-known value.</exception>")
            .AppendLineIndent("public static implicit operator ", enumName, "(", targetTypeName, " value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (value.TryGetKnownValue(out ", enumName, " result))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return result;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("throw new InvalidOperationException();")
            .PopIndent()
            .AppendLineIndent("}");

        generator
            .ReserveNameIfNotReserved("TryGetKnownValue")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Tries to get the <see cref=\"", enumName, "\"/> equivalent of this value.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"result\">The corresponding well-known value, or the default if this value did not match one.</param>")
            .AppendLineIndent("/// <returns><see langword=\"true\"/> if the value matched a well-known value.</returns>")
            .AppendLineIndent("public bool TryGetKnownValue(out ", enumName, " result)")
            .AppendLineIndent("{")
            .PushIndent();

        foreach (KnownValuesMember member in members)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (this.ValueEquals(", constantsClassName, ".", member.Utf8FieldName, "))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("result = ", enumName, ".", member.MemberName, ";")
                    .AppendLineIndent("return true;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static IReadOnlyList<KnownValuesMember> GetOrBuildKnownValuesMembers(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(KnownValuesMembersKey, out IReadOnlyList<KnownValuesMember>? existingMembers) &&
            existingMembers is not null)
        {
            return existingMembers;
        }

        string knownValuesScope = generator.KnownValuesEnumScope();
        string constantsScope = generator.ConstantsScope();
        List<KnownValuesMember> members = [];

        if (typeDeclaration.AnyOfConstantValues() is IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> anyOfConstants)
        {
            foreach (KeyValuePair<IAnyOfConstantValidationKeyword, JsonElement[]> kvp in anyOfConstants.OrderBy(k => k.Key.Keyword))
            {
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
                    string? suffix = addSuffix ? elementIndex.ToString() : null;
                    string jsonString = value.GetString()!;
                    string memberName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(jsonString, rootScope: knownValuesScope);
                    string utf8FieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: suffix);
                    string jsonFieldName = generator.GetStaticReadOnlyFieldNameInScope(keywordName, rootScope: constantsScope, suffix: $"Json{suffix}");
                    members.Add(new(memberName, jsonString, utf8FieldName, jsonFieldName));
                    elementIndex++;
                }
            }
        }

        typeDeclaration.SetMetadata(KnownValuesMembersKey, (IReadOnlyList<KnownValuesMember>)members);
        return members;
    }

    /// <summary>
    /// Describes one member of a nested native KnownValues enum: its C# member name, the JSON
    /// string it corresponds to, and the Constants field names holding the UTF-8 bytes and the
    /// pre-parsed typed constant for that string.
    /// </summary>
    private sealed class KnownValuesMember(string memberName, string jsonString, string utf8FieldName, string jsonFieldName)
    {
        public string MemberName { get; } = memberName;

        public string JsonString { get; } = jsonString;

        public string Utf8FieldName { get; } = utf8FieldName;

        public string JsonFieldName { get; } = jsonFieldName;
    }
}