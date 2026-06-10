// <copyright file="DiagnosticDescriptors.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Microsoft.CodeAnalysis;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Diagnostic descriptors for the V4 to V5 migration analyzers.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "Corvus.Json.Migration";
    private const string HelpLinkBase = "https://corvus-oss.org/Corvus.JsonSchema/docs/migration-analyzers.html";

    // Tier 1: Automatable

    /// <summary>
    /// CVJ001: using Corvus.Json to using Corvus.Text.Json.
    /// </summary>
    public static readonly DiagnosticDescriptor NamespaceMigration = new(
        id: "CVJ001",
        title: "Migrate namespace to Corvus.Text.Json",
        messageFormat: "Replace 'using {0}' with the corresponding Corvus.Text.Json namespace",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj001-migrate-namespace");

    /// <summary>
    /// CVJ002: ParsedValue to ParsedJsonDocument.
    /// </summary>
    public static readonly DiagnosticDescriptor ParsedValueMigration = new(
        id: "CVJ002",
        title: "Migrate ParsedValue to ParsedJsonDocument",
        messageFormat: "Replace 'ParsedValue<T>' with 'ParsedJsonDocument<T>' and '.Instance' with '.RootElement'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj002-migrate-parsedvaluet-to-parsedjsondocumentt");

    /// <summary>
    /// CVJ003: IsValid or Validate to EvaluateSchema.
    /// </summary>
    public static readonly DiagnosticDescriptor ValidateMigration = new(
        id: "CVJ003",
        title: "Migrate validation to EvaluateSchema",
        messageFormat: "{0}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj003-migrate-validation-to-evaluateschema");

    /// <summary>
    /// CVJ004: As of T to T.From.
    /// </summary>
    public static readonly DiagnosticDescriptor AsGenericMigration = new(
        id: "CVJ004",
        title: "Migrate As<T>() to T.From()",
        messageFormat: "Replace 'As<{0}>()' with '{0}.From(value)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj004-migrate-ast-to-tfrom");

    /// <summary>
    /// CVJ005: Count property to GetPropertyCount method.
    /// </summary>
    public static readonly DiagnosticDescriptor CountMigration = new(
        id: "CVJ005",
        title: "Migrate Count to GetPropertyCount()",
        messageFormat: "Replace '.Count' with '.GetPropertyCount()'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj005-migrate-count-to-getpropertycount");

    /// <summary>
    /// CVJ006: FromJson to From.
    /// </summary>
    public static readonly DiagnosticDescriptor FromJsonMigration = new(
        id: "CVJ006",
        title: "Migrate FromJson() to From()",
        messageFormat: "Replace 'FromJson()' with 'From()'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj006-migrate-fromjson-to-from");

    /// <summary>
    /// CVJ007: V4 core types to JsonElement.
    /// </summary>
    public static readonly DiagnosticDescriptor CoreTypeMigration = new(
        id: "CVJ007",
        title: "Migrate V4 core type to JsonElement",
        messageFormat: "Replace V4 type '{0}' with 'JsonElement'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj007-migrate-v4-core-type-to-jsonelement");

    /// <summary>
    /// CVJ008: JsonDocument.Parse to ParsedJsonDocument.
    /// </summary>
    public static readonly DiagnosticDescriptor JsonDocumentParseMigration = new(
        id: "CVJ008",
        title: "Migrate JsonDocument.Parse to ParsedJsonDocument<T>.Parse",
        messageFormat: "Replace 'JsonDocument.Parse(...)' with 'ParsedJsonDocument<JsonElement>.Parse(...)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj008-migrate-jsondocumentparse-to-parsedjsondocumenttparse");

    /// <summary>
    /// CVJ009: V4 typed core types may have generated V5 equivalents.
    /// </summary>
    public static readonly DiagnosticDescriptor TypedCoreMigration = new(
        id: "CVJ009",
        title: "V4 typed core type may need replacement",
        messageFormat: "V4 type '{0}' should be replaced with the project-local generated equivalent or 'JsonElement' in V5",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj009-v4-typed-core-type-may-need-replacement");

    // Tier 2: Guidance-only

    /// <summary>
    /// CVJ010: As accessors removed in V5.
    /// </summary>
    public static readonly DiagnosticDescriptor AsAccessorMigration = new(
        id: "CVJ010",
        title: "V4 As* accessors removed in V5",
        messageFormat: "V4 accessor '.{0}' does not exist in V5; use explicit casts or direct accessors instead",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj010-v4-as-accessors-removed");

    /// <summary>
    /// CVJ011: V4 immutable mutation should use V5 mutable builder.
    /// </summary>
    public static readonly DiagnosticDescriptor WithMutationMigration = new(
        id: "CVJ011",
        title: "V4 immutable mutation replaced by mutable builder in V5",
        messageFormat: "V4 immutable '.{0}(...)' returns a new value. In V5, use 'element.BuildDocument(workspace)' to get a mutable builder, then call '.{1}(...)' on the Mutable element.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj011-v4-immutable-mutation-replaced-by-mutable-builder");

    /// <summary>
    /// CVJ012: Functional array operations to mutable builder.
    /// </summary>
    public static readonly DiagnosticDescriptor FunctionalArrayMigration = new(
        id: "CVJ012",
        title: "V4 functional array ops replaced in V5",
        messageFormat: "V4 array method '.{0}(...)' should be replaced with mutable builder '.{1}(...)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj012-v4-functional-array-operations");

    /// <summary>
    /// CVJ013: Create to CreateBuilder.
    /// </summary>
    public static readonly DiagnosticDescriptor CreateMigration = new(
        id: "CVJ013",
        title: "V4 Create() replaced by CreateBuilder() in V5",
        messageFormat: "V4 method '.Create(...)' should be replaced with 'CreateBuilder(workspace, ...)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj013-v4-create-replaced-by-createbuilder-or-build");

    /// <summary>
    /// CVJ014: FromItems to Build pattern.
    /// </summary>
    public static readonly DiagnosticDescriptor FromItemsMigration = new(
        id: "CVJ014",
        title: "V4 FromItems() replaced by Build pattern in V5",
        messageFormat: "V4 method '.FromItems(...)' should be replaced with 'CreateBuilder(workspace, Build(...))'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj014-v4-fromitems-replaced-by-build-pattern");

    /// <summary>
    /// CVJ015: FromValues to CreateBuilder.
    /// </summary>
    public static readonly DiagnosticDescriptor FromValuesMigration = new(
        id: "CVJ015",
        title: "V4 FromValues() replaced by CreateBuilder() in V5",
        messageFormat: "V4 method '.FromValues(...)' should be replaced with 'CreateBuilder(workspace, span)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj015-v4-fromvalues-replaced-by-createbuilder");

    /// <summary>
    /// CVJ016: WriteTo uses different writer type in V5.
    /// </summary>
    public static readonly DiagnosticDescriptor WriteToMigration = new(
        id: "CVJ016",
        title: "V5 uses Corvus.Text.Json.Utf8JsonWriter",
        messageFormat: "V5 WriteTo() requires Corvus.Text.Json.Utf8JsonWriter not System.Text.Json.Utf8JsonWriter",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj016-v5-uses-corvustextjsonutf8jsonwriter");

    // CVJ017: Removed — [JsonConverter(typeof(JsonValueConverter<T>))] is on V4 generated code,
    // which is replaced when the V5 generator takes over.

    /// <summary>
    /// CVJ018: TryGetString to TryGetValue.
    /// </summary>
    public static readonly DiagnosticDescriptor TryGetStringMigration = new(
        id: "CVJ018",
        title: "V4 TryGetString() replaced by TryGetValue() in V5",
        messageFormat: "V4 method '.TryGetString(out string)' should be replaced with '.TryGetValue(out string)'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj018-migrate-trygetstring-to-trygetvalue");

    /// <summary>
    /// CVJ019: V4 backing model APIs removed in V5.
    /// </summary>
    public static readonly DiagnosticDescriptor BackingModelMigration = new(
        id: "CVJ019",
        title: "V4 backing model APIs removed in V5",
        messageFormat: "V4 backing model API '.{0}' does not exist in V5 which uses a single document-index model",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj019-v4-backing-model-apis-removed");

    // CVJ020: Removed — V5 has the same null/undefined extensions as V4, no migration needed.

    /// <summary>
    /// CVJ021: Nested With*() reconstruction can use V5 deep property setter.
    /// </summary>
    public static readonly DiagnosticDescriptor DeepMutationMigration = new(
        id: "CVJ021",
        title: "V4 nested With*() chain can use V5 deep property setter via builder",
        messageFormat: "V4 nested reconstruction '{0}' can be simplified in V5 to '{1}' on a mutable builder",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj021-nested-mutation-chain-can-use-deep-property-setter");

    /// <summary>
    /// CVJ025: V4 package reference should be replaced with V5 equivalent.
    /// </summary>
    public static readonly DiagnosticDescriptor PackageReferenceMigration = new(
        id: "CVJ025",
        title: "Replace V4 package reference with V5 equivalent",
        messageFormat: "Replace V4 assembly '{0}' with the '{1}' NuGet package",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        helpLinkUri: HelpLinkBase + "#cvj025-replace-v4-package-reference",
        customTags: new[] { WellKnownDiagnosticTags.CompilationEnd });
}