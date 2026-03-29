// <copyright file="SimpleCoreTypeNameHeuristic.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.Keywords;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// A name heuristic that detects simple core-type-only schemas (type + optional format,
/// no other constraining keywords) and maps them to shared global type names.
/// </summary>
/// <remarks>
/// <para>
/// This heuristic reduces the number of generated types by consolidating trivial schemas
/// like <c>{"type": "string"}</c> or <c>{"type": "string", "format": "uuid"}</c> into
/// shared types such as <c>JsonString</c> or <c>JsonUuid</c>, generated once in the
/// root namespace.
/// </para>
/// <para>
/// When format is present but not asserted (neither <see cref="TypeDeclarationExtensions.AlwaysAssertFormat"/>
/// nor vocabulary-level assertion), the suffix <c>NotAsserted</c> is appended to the name
/// (e.g., <c>JsonUuidNotAsserted</c>).
/// </para>
/// <para>
/// Format-to-type-name mappings are delegated to the registered
/// <see cref="IFormatHandler"/> implementations via
/// <see cref="IFormatHandler.TryGetSimpleTypeNameSuffix"/>.
/// </para>
/// </remarks>
internal sealed class SimpleCoreTypeNameHeuristic : IBuiltInTypeNameHeuristic
{
    private static readonly Dictionary<CoreTypes, string> CoreTypeNames = new()
    {
        [CoreTypes.String] = "String",
        [CoreTypes.Number] = "Number",
        [CoreTypes.Number | CoreTypes.Integer] = "Number",
        [CoreTypes.Integer] = "Integer",
        [CoreTypes.Boolean] = "Boolean",
        [CoreTypes.Null] = "Null",
    };

    private readonly ConcurrentDictionary<string, TypeDeclaration> firstSeen = new();
    private readonly string defaultNamespace;

    /// <summary>
    /// Initializes a new instance of the <see cref="SimpleCoreTypeNameHeuristic"/> class.
    /// </summary>
    /// <param name="options">The language provider options.</param>
    public SimpleCoreTypeNameHeuristic(CSharpLanguageProvider.Options options)
    {
        defaultNamespace = options.DefaultNamespace;
    }

    /// <inheritdoc/>
    public bool IsOptional => false;

    /// <inheritdoc/>
    public uint Priority => 2;

    /// <inheritdoc/>
    public bool TryGetName(ILanguageProvider languageProvider, TypeDeclaration typeDeclaration, JsonReferenceBuilder reference, Span<char> typeNameBuffer, out int written)
    {
        written = 0;

        if (!IsSimpleCoreTypeOnly(typeDeclaration))
        {
            return false;
        }

        string canonicalName = GetCanonicalName(typeDeclaration);

        typeDeclaration.SetDotnetTypeName(canonicalName);
        typeDeclaration.SetDotnetNamespace(defaultNamespace);

        if (firstSeen.TryAdd(canonicalName, typeDeclaration))
        {
            // First instance: mark as global simple type so GenerateCodeFor
            // processes it in a dedicated loop (since ShouldGenerate returns false
            // for DoNotGenerate types, the framework won't include it in the
            // main enumerable).
            typeDeclaration.SetIsGlobalSimpleType(true);
        }

        // Return true for ALL instances — framework calls SetDoNotGenerate + SetParent(null).
        // First instance generates via the dedicated global simple type loop in GenerateCodeFor.
        return true;
    }

    /// <summary>
    /// Gets the first-seen type declarations for each canonical name.
    /// These are the types that should be generated once in the root namespace.
    /// </summary>
    /// <returns>The collection of first-seen global simple type declarations.</returns>
    public IEnumerable<TypeDeclaration> GetFirstSeenTypes()
    {
        return firstSeen.Values;
    }

    /// <summary>
    /// Determines whether the given type declaration represents a simple core-type-only schema.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to check.</param>
    /// <returns><see langword="true"/> if the schema has only type (and optionally format) keywords.</returns>
    private static bool IsSimpleCoreTypeOnly(TypeDeclaration typeDeclaration)
    {
        CoreTypes coreTypes = typeDeclaration.ImpliedCoreTypes();

        if (!CoreTypeNames.ContainsKey(coreTypes))
        {
            return false;
        }

        LocatedSchema locatedSchema = typeDeclaration.LocatedSchema;

        if (locatedSchema.IsBooleanSchema)
        {
            return false;
        }

        // Check for sibling-hiding keywords (e.g. $ref in draft 4-7).
        IKeyword? hidesSiblingsKeyword = locatedSchema.Vocabulary.Keywords
            .FirstOrDefault(k => k is IHidesSiblingsKeyword && locatedSchema.Schema.HasKeyword(k));

        if (hidesSiblingsKeyword is IKeyword k)
        {
            return k.CanReduce(locatedSchema.Schema);
        }

        // All keywords that are NOT core-type and NOT format-provider must be reducible.
        return locatedSchema.Vocabulary.Keywords
            .Where(k => k is not ICoreTypeValidationKeyword && k is not IFormatProviderKeyword && locatedSchema.Schema.HasKeyword(k))
            .All(k => k.CanReduce(locatedSchema.Schema));
    }

    /// <summary>
    /// Gets the canonical name for the given simple core-type type declaration.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>The canonical name (e.g., <c>JsonString</c>, <c>JsonUuid</c>, <c>JsonUuidNotAsserted</c>).</returns>
    private static string GetCanonicalName(TypeDeclaration typeDeclaration)
    {
        string? format = typeDeclaration.Format();

        string suffix;
        bool hasFormat;

        if (format is string f)
        {
            hasFormat = true;
            suffix = GetFormatSuffix(f);
        }
        else
        {
            hasFormat = false;
            suffix = CoreTypeNames[typeDeclaration.ImpliedCoreTypes()];
        }

        bool isAsserted = !hasFormat || typeDeclaration.IsFormatAssertion() || typeDeclaration.AlwaysAssertFormat();

        return isAsserted ? $"Json{suffix}" : $"Json{suffix}NotAsserted";
    }

    /// <summary>
    /// Gets the PascalCase suffix for the given format string, delegating to
    /// the registered format handlers.
    /// </summary>
    /// <param name="format">The format string.</param>
    /// <returns>The PascalCase suffix.</returns>
    private static string GetFormatSuffix(string format)
    {
        if (FormatHandlerRegistry.Instance.FormatHandlers.TryGetSimpleTypeNameSuffix(format, out string? suffix))
        {
            return suffix;
        }

        // Fall back to PascalCase conversion for unknown formats.
        Span<char> buffer = stackalloc char[Formatting.MaxIdentifierLength];
        format.AsSpan().CopyTo(buffer);
        int length = Formatting.ToPascalCase(buffer[..format.Length]);
        return buffer[..length].ToString();
    }
}