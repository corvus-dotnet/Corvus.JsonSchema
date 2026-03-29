// <copyright file="JsonSchemaEvaluation.Composition.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
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
    public static readonly JsonSchemaMessageProvider MatchedMoreThanOneSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedMoreThanOneSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedNoSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedNoSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when all schemas match in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAllSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedAllSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when all schemas do not match in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAllSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchAllSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAtLeastOneSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedAtLeastOneSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one schema matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedExactlyOneSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedExactlyOneSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no schemas matched in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAtLeastOneSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchAtLeastOneSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when at least one constant value matches in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedAtLeastOneConstantValue = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedAtLeastOneConstantValue.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when no constant values matched in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchAtLeastOneConstantValue = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchAtLeastOneConstantValue.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value (correctly) did not match a not schema in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchNotSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchNotSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value (incorrectly) matched a not schema in a composition constraint.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedNotSchema = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedNotSchema.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches a binary or ternary if to go on to match a then clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedIfForThen = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedIfForThen.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value did not match the then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchThen = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchThen.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches the corresponding then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedThen = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedThen.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value does not match a ternary if and so goes on to match an else clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedIfForElse = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedIfForElse.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value does not match a ternary if and then did not match the corresponding else clause.
    /// </summary>
    public static readonly JsonSchemaMessageProvider DidNotMatchElse = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_DidNotMatchElse.AsSpan(), buffer, out written);

    /// <summary>
    /// Message provider for validation errors when a value matches the corresponding then clause for a binary or ternary if.
    /// </summary>
    public static readonly JsonSchemaMessageProvider MatchedElse = static (buffer, out written) => JsonReaderHelper.TryGetUtf8FromText(SR.JsonSchema_MatchedElse.AsSpan(), buffer, out written);
}