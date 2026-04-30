// <copyright file="DiagnosticDescriptors.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Microsoft.CodeAnalysis;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// Diagnostic descriptors for the Corvus.Text.Json production analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "Performance";
    private const string UsageCategory = "Usage";
    private const string ReliabilityCategory = "Reliability";
    private const string HelpLinkBase = "https://corvus-text-json.dev/docs/analyzers.html";

    /// <summary>
    /// CTJ001: Prefer UTF-8 string literal.
    /// </summary>
    public static readonly DiagnosticDescriptor PreferUtf8StringLiteral = new(
        id: "CTJ001",
        title: "Prefer UTF-8 string literal",
        messageFormat: "Use \"{0}\"u8 instead of \"{0}\" — a ReadOnlySpan<byte> overload is available",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj001-prefer-utf-8-string-literal");

    /// <summary>
    /// CTJ002: Unnecessary conversion to .NET type.
    /// </summary>
    public static readonly DiagnosticDescriptor UnnecessaryConversion = new(
        id: "CTJ002",
        title: "Unnecessary conversion to .NET type",
        messageFormat: "Unnecessary conversion to '{0}' — the original type converts implicitly to the target parameter type",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj002-unnecessary-conversion-to.net-type");

    /// <summary>
    /// CTJ003: Match lambda should be static.
    /// </summary>
    public static readonly DiagnosticDescriptor MatchLambdaShouldBeStatic = new(
        id: "CTJ003",
        title: "Match lambda should be static",
        messageFormat: "Lambda passed to Match is not static — {0}",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj003-match-lambda-should-be-static");

    /// <summary>
    /// CTJ004: Missing dispose on ParsedJsonDocument.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDisposeOnParsedJsonDocument = new(
        id: "CTJ004",
        title: "Missing dispose on ParsedJsonDocument",
        messageFormat: "'{0}' is disposable and leaks pooled memory if not disposed — add 'using' or call Dispose()",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj004-missing-dispose-on-parsedjsondocument");

    /// <summary>
    /// CTJ005: Missing dispose on JsonWorkspace.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDisposeOnJsonWorkspace = new(
        id: "CTJ005",
        title: "Missing dispose on JsonWorkspace",
        messageFormat: "'{0}' is disposable and leaks pooled memory if not disposed — add 'using' or call Dispose()",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj005-missing-dispose-on-jsonworkspace");

    /// <summary>
    /// CTJ006: Missing dispose on JsonDocumentBuilder.
    /// </summary>
    public static readonly DiagnosticDescriptor MissingDisposeOnJsonDocumentBuilder = new(
        id: "CTJ006",
        title: "Missing dispose on JsonDocumentBuilder",
        messageFormat: "'{0}' is disposable and leaks pooled memory if not disposed — add 'using' or call Dispose()",
        category: ReliabilityCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj006-missing-dispose-on-jsondocumentbuilder");

    /// <summary>
    /// CTJ007: Ignored schema validation result.
    /// </summary>
    public static readonly DiagnosticDescriptor IgnoredSchemaValidationResult = new(
        id: "CTJ007",
        title: "EvaluateSchema() result is discarded",
        messageFormat: "The return value of EvaluateSchema() is not used — validation has no effect if the result is discarded",
        category: UsageCategory,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj007-ignored-schema-validation-result");

    /// <summary>
    /// CTJ008: Prefer non-allocating property name accessors.
    /// </summary>
    public static readonly DiagnosticDescriptor PreferNonAllocatingPropertyName = new(
        id: "CTJ008",
        title: "Prefer NameEquals over Name for comparisons",
        messageFormat: "Use NameEquals(\"{0}\"u8) instead of comparing Name — avoids allocating a string",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj008-prefer-non-allocating-property-name-accessors");

    /// <summary>
    /// CTJ009: Prefer renting Utf8JsonWriter from workspace.
    /// </summary>
    public static readonly DiagnosticDescriptor PreferRentedWriter = new(
        id: "CTJ009",
        title: "Prefer renting Utf8JsonWriter from workspace",
        messageFormat: "A JsonWorkspace is in scope — use workspace.RentWriter() or workspace.RentWriterAndBuffer() instead of allocating a new Utf8JsonWriter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj009-prefer-renting-utf8jsonwriter-from-workspace");

    /// <summary>
    /// CTJ010: Prefer ReadOnlyMemory/Span-based Parse overload.
    /// </summary>
    public static readonly DiagnosticDescriptor PreferMemoryParse = new(
        id: "CTJ010",
        title: "Prefer ReadOnlyMemory<byte> or UTF-8 literal for Parse",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#ctj010-prefer-readonlymemoryspan-based-parse-overload");
}