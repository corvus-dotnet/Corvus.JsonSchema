// <copyright file="JsonSchemaEvaluation.Composition.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
namespace Corvus.Text.Json.Internal;

/// <summary>
/// Support for JSON Schema matching implementations.
/// </summary>
public static partial class JsonSchemaEvaluation
{
    /// <summary>
    /// Message provider for validation errors when more than one schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedMoreThanOneSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedMoreThanOneSchema ??= Utf8(SR.JsonSchema_MatchedMoreThanOneSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedNoSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedNoSchema ??= Utf8(SR.JsonSchema_MatchedNoSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when all schemas match in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAllSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedAllSchema ??= Utf8(SR.JsonSchema_MatchedAllSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when all schemas do not match in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAllSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchAllSchema ??= Utf8(SR.JsonSchema_DidNotMatchAllSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAtLeastOneSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedAtLeastOneSchema ??= Utf8(SR.JsonSchema_MatchedAtLeastOneSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedExactlyOneSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedExactlyOneSchema ??= Utf8(SR.JsonSchema_MatchedExactlyOneSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no schemas matched in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAtLeastOneSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchAtLeastOneSchema ??= Utf8(SR.JsonSchema_DidNotMatchAtLeastOneSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one constant value matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAtLeastOneConstantValue = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedAtLeastOneConstantValue ??= Utf8(SR.JsonSchema_MatchedAtLeastOneConstantValue), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no constant values matched in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAtLeastOneConstantValue = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchAtLeastOneConstantValue ??= Utf8(SR.JsonSchema_DidNotMatchAtLeastOneConstantValue), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value (correctly) did not match a not schema in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchNotSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchNotSchema ??= Utf8(SR.JsonSchema_DidNotMatchNotSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value (incorrectly) matched a not schema in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedNotSchema = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedNotSchema ??= Utf8(SR.JsonSchema_MatchedNotSchema), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches a binary or ternary if to go on to match a then clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedIfForThen = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedIfForThen ??= Utf8(SR.JsonSchema_MatchedIfForThen), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value did not match the then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchThen = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchThen ??= Utf8(SR.JsonSchema_DidNotMatchThen), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches the corresponding then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedThen = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedThen ??= Utf8(SR.JsonSchema_MatchedThen), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value does not match a ternary if and so goes on to match an else clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedIfForElse = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedIfForElse ??= Utf8(SR.JsonSchema_MatchedIfForElse), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value does not match a ternary if and then did not match the corresponding else clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchElse = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_DidNotMatchElse ??= Utf8(SR.JsonSchema_DidNotMatchElse), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches the corresponding then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedElse = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_MatchedElse ??= Utf8(SR.JsonSchema_MatchedElse), buffer, out written);

    /// <summary>
    /// Message provider for an ignored then keyword when no if keyword is present.
    /// </summary>
    public static readonly JsonSchemaMessageProvider ThenWithoutIf = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ThenWithoutIf ??= Utf8(SR.JsonSchema_ThenWithoutIf), buffer, out written);

    /// <summary>
    /// Message provider for an ignored else keyword when no if keyword is present.
    /// </summary>
    public static readonly JsonSchemaMessageProvider ElseWithoutIf = static (buffer, out written) => TryCopyUtf8(s_JsonSchema_ElseWithoutIf ??= Utf8(SR.JsonSchema_ElseWithoutIf), buffer, out written);
}