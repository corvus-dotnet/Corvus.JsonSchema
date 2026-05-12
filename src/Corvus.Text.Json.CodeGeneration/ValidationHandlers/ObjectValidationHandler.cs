// <copyright file="ObjectValidationHandler.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers;

/// <summary>
/// A validation handler for <see cref="IObjectValidationKeyword"/> capability.
/// </summary>
internal sealed class ObjectValidationHandler : TypeSensitiveKeywordValidationHandlerBase, IObjectKeywordValidationHandler, IJsonSchemaClassSetup
{
    private ObjectValidationHandler()
    {
    }

    /// <summary>
    /// Gets a singleton instance of the <see cref="ObjectValidationHandler"/>.
    /// </summary>
    public static ObjectValidationHandler Instance { get; } = CreateDefault();

    /// <inheritdoc/>
    public override uint ValidationHandlerPriority => ValidationPriorities.AfterComposition;

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
             .AppendObjectValidationSetup();
    }

    /// <inheritdoc/>
    public override CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration, bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        IReadOnlyCollection<IChildValidationHandler> childHandlers = ChildHandlers;

        generator
            .AppendObjectValidation(this, typeDeclaration, childHandlers, ValidationHandlerPriority, validateOnly);

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IChildValidationHandler childHandler in ChildHandlers)
        {
            if (childHandler is IJsonSchemaClassSetup classSetup)
            {
                classSetup.AppendJsonSchemaClassSetup(generator, typeDeclaration);
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public override bool HandlesKeyword(IKeyword keyword)
    {
        return keyword is IObjectValidationKeyword;
    }

    private static ObjectValidationHandler CreateDefault()
    {
        var result = new ObjectValidationHandler();
        result
            .RegisterChildHandlers(
                HoistedAllOfPropertyValidationHandler.Instance,
                PropertyCountValidationHandler.Instance,
                PropertiesValidationHandler.Instance,
                PropertyNamesValidationHandler.Instance,
                PatternPropertiesValidationHandler.Instance,
                UnevaluatedPropertyValidationHandler.Instance);
        return result;
    }
}

file static class ObjectValidationHandlerExtensions
{
    public static CodeGenerator AppendObjectValidationSetup(this CodeGenerator generator)
    {
        return generator;
    }

    public static CodeGenerator AppendObjectValidation(
        this CodeGenerator generator,
        IKeywordValidationHandler parentHandler,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> childHandlers,
        uint validationPriority,
        bool validateOnly)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!validateOnly)
        {
            generator
                .AppendSeparatorLine()
                .AppendStartTokenTypeCheck(typeDeclaration)
                .PushMemberScope("tokenTypeCheck", ScopeType.Method);
        }

        generator
            .AppendSeparatorLine();

        foreach (IChildValidationHandler child in childHandlers)
        {
            child.AppendValidationSetup(generator, typeDeclaration);
        }

        generator
            .PrependChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        bool requiresObjectEnumeration = childHandlers
                    .OfType<IChildObjectPropertyValidationHandler2>()
                    .Any(child => child.WillEmitCodeFor(typeDeclaration));

        if (requiresObjectEnumeration ||
            typeDeclaration.RequiresPropertyCount())
        {
            generator.ReserveName("objectValidation_propertyCount");

            if (requiresObjectEnumeration)
            {
                generator.AppendLineIndent("int objectValidation_propertyCount = 0;");
            }
            else
            {
                generator.AppendLineIndent("int objectValidation_propertyCount = parentDocument.GetPropertyCount(parentIndex);");
            }
        }

        if (requiresObjectEnumeration)
        {
            generator
                .AppendSeparatorLine()
                .ReserveName("objectValidation_enumerator")
                .AppendLineIndent("var objectValidation_enumerator = new ObjectEnumerator(parentDocument, parentIndex);")
                .AppendLineIndent("while (objectValidation_enumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .ReserveName("objectValidation_currentIndex")
                    .AppendLineIndent("int objectValidation_currentIndex = objectValidation_enumerator.CurrentIndex;");

            if (childHandlers
                    .OfType<IChildObjectPropertyValidationHandler>()
                    .Any(child => child.RequiresPropertyNameAsString(typeDeclaration)))
            {
                generator
                    .ReserveName("objectValidation_unescapedPropertyName")
                    .AppendLineIndent("using UnescapedUtf8JsonString objectValidation_unescapedPropertyName = parentDocument.GetPropertyNameUnescaped(objectValidation_currentIndex);");
            }

            // Determine if we can wrap pattern/fallback handlers in an else clause
            // after the named property matcher. This is valid when:
            // 1. A named property handler will emit a matcher block
            // 2. Pattern/fallback handlers will emit code
            // 3. No named property matches any patternProperties regex
            // 4. PropertyNames handler won't emit code (it must run for ALL properties)
            bool useElseClause = CanUseNamedPropertyElseClause(typeDeclaration, childHandlers);

            if (useElseClause)
            {
                // Emit named property handlers first (only PropertiesValidationHandler;
                // HoistedAllOfPropertyValidationHandler is excluded by CanUseNamedPropertyElseClause)
                foreach (IChildObjectPropertyValidationHandler child in childHandlers.OfType<IChildObjectPropertyValidationHandler>())
                {
                    if (child is PropertiesValidationHandler)
                    {
                        child.AppendObjectPropertyValidationCode(generator, typeDeclaration);
                    }
                }

                // Wrap remaining handlers in else clause
                generator
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent();

                foreach (IChildObjectPropertyValidationHandler child in childHandlers.OfType<IChildObjectPropertyValidationHandler>())
                {
                    if (child is not PropertiesValidationHandler)
                    {
                        child.AppendObjectPropertyValidationCode(generator, typeDeclaration);
                    }
                }

                generator
                    .PopIndent()
                    .AppendLineIndent("}");
            }
            else
            {
                foreach (IChildObjectPropertyValidationHandler child in childHandlers.OfType<IChildObjectPropertyValidationHandler>())
                {
                    child.AppendObjectPropertyValidationCode(generator, typeDeclaration);
                }
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("objectValidation_propertyCount++;")
                .PopIndent()
                .AppendLineIndent("}");
        }

        generator
            .AppendChildValidationCode(typeDeclaration, childHandlers, validationPriority);

        if (!validateOnly)
        {
            // We only pop if we were in our own scope
            generator
                .PopMemberScope()
                .AppendEndTokenTypeCheck(typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendStartTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (tokenType == JsonTokenType.StartObject)")
            .AppendLineIndent("{")
            .PushIndent();
    }

    private static CodeGenerator AppendEndTokenTypeCheck(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .PopIndent()
            .AppendLineIndent("}");

        bool appended = false;

        generator
            .TryAppendIgnoredCoreTypeKeywords<IObjectValidationKeyword>(
                typeDeclaration,
                "JsonSchemaEvaluation.IgnoredNotTypeObject",
                static (g, _) =>
                {
                    g
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent();
                },
                ref appended);

        if (appended)
        {
            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    /// <summary>
    /// Determines whether the named property matcher block can use an else clause to skip
    /// patternProperties and additionalProperties/unevaluatedProperties checks for matched properties.
    /// </summary>
    /// <remarks>
    /// This is safe when:
    /// 1. A named property handler (Properties or HoistedAllOf) will emit a matcher block
    /// 2. At least one pattern/fallback handler will emit code
    /// 3. No named property matches any patternProperties regex (checked at codegen time)
    /// 4. PropertyNames handler won't emit code (it must validate ALL properties)
    /// </remarks>
    private static bool CanUseNamedPropertyElseClause(TypeDeclaration typeDeclaration, IReadOnlyCollection<IChildValidationHandler> childHandlers)
    {
        // The named property map (TryGetNamedMatcher) includes entries from multiple keywords:
        // 'properties', 'dependentSchemas', 'required', etc. Only entries from the 'properties'
        // keyword (IObjectPropertyValidationKeyword) call AddLocalEvaluatedProperty in their
        // Match method. Entries from other keywords (e.g., dependentSchemas just evaluates the
        // dependent schema, required just sets a bit) do NOT mark the property as locally evaluated.
        //
        // The else clause skips fallback checks (additionalProperties/unevaluatedProperties) for
        // ALL matched properties. This is only correct when every match also marks the property
        // as locally evaluated. If non-IObjectPropertyValidationKeyword entries exist, the else
        // clause would skip fallback checks for properties that haven't been locally evaluated.
        //
        // Note: dependentSchemas entries use LocalOrComposed.Composed and keyword=null,
        // so we must check ALL property declarations, not just Local ones.
        if (typeDeclaration.PropertyDeclarations
            .Any(p => p.Keyword is not IObjectPropertyValidationKeyword))
        {
            return false;
        }

        // When HoistedAllOfPropertyValidationHandler is active, it creates a unified property map
        // that includes BOTH local properties AND allOf-hoisted properties. The else clause would
        // incorrectly skip additionalProperties checks for hoisted properties, which are NOT in
        // the local properties keyword and should still be checked by additionalProperties.
        if (childHandlers
            .OfType<HoistedAllOfPropertyValidationHandler>()
            .Any(child => child.WillEmitCodeFor(typeDeclaration)))
        {
            return false;
        }

        // Check if PropertiesValidationHandler will emit a named matcher
        bool hasNamedPropertyMatcher = childHandlers
            .OfType<PropertiesValidationHandler>()
            .Any(child => child.WillEmitCodeFor(typeDeclaration));

        if (!hasNamedPropertyMatcher)
        {
            return false;
        }

        // Check if pattern/fallback handlers will emit code (otherwise else clause is pointless)
        bool hasUnmatchedHandlers = childHandlers
            .OfType<IChildObjectPropertyValidationHandler2>()
            .Any(child => child is PatternPropertiesValidationHandler or UnevaluatedPropertyValidationHandler
                && child.WillEmitCodeFor(typeDeclaration));

        if (!hasUnmatchedHandlers)
        {
            return false;
        }

        // PropertyNames must validate ALL properties — can't skip it for matched ones
        if (childHandlers
            .OfType<PropertyNamesValidationHandler>()
            .Any(child => child.WillEmitCodeFor(typeDeclaration)))
        {
            return false;
        }

        // Check overlap: does any named property match any patternProperties regex?
        if (typeDeclaration.PatternProperties() is IReadOnlyDictionary<IObjectPatternPropertyValidationKeyword, IReadOnlyCollection<PatternPropertyDeclaration>> patternProperties)
        {
            IEnumerable<string> namedPropertyNames = typeDeclaration.PropertyDeclarations
                .Where(p => p.LocalOrComposed == LocalOrComposed.Local && p.Keyword is IObjectPropertyValidationKeyword)
                .Select(p => p.JsonPropertyName);

            foreach (IReadOnlyCollection<PatternPropertyDeclaration> patternPropertyCollection in patternProperties.Values)
            {
                foreach (PatternPropertyDeclaration patternProperty in patternPropertyCollection)
                {
                    if (AnyNamedPropertyMatchesPattern(namedPropertyNames, patternProperty.Pattern))
                    {
                        return false;
                    }
                }
            }
        }

        return true;
    }

    /// <summary>
    /// Checks whether any of the given property names would match a patternProperties regex.
    /// Uses <see cref="CodeGenerationExtensions.ClassifyRegexPattern"/> for fast detection of
    /// common patterns before falling back to full regex evaluation.
    /// </summary>
    private static bool AnyNamedPropertyMatchesPattern(IEnumerable<string> propertyNames, string pattern)
    {
        RegexPatternCategory category = CodeGenerationExtensions.ClassifyRegexPattern(pattern);

        switch (category)
        {
            case RegexPatternCategory.Noop:
                // .* matches everything — overlap is guaranteed
                return true;

            case RegexPatternCategory.NonEmpty:
                // .+ matches any non-empty string — all property names are non-empty
                return true;

            case RegexPatternCategory.Prefix:
                string prefix = CodeGenerationExtensions.ExtractRegexPrefix(pattern);
                return propertyNames.Any(name => name.StartsWith(prefix, StringComparison.Ordinal));

            case RegexPatternCategory.Range:
                (int min, int max) = CodeGenerationExtensions.ExtractRegexRange(pattern);
                return propertyNames.Any(name => name.Length >= min && name.Length <= max);

            default:
                // Full regex — evaluate against each property name
                try
                {
                    Regex regex = new(pattern);
                    return propertyNames.Any(name => regex.IsMatch(name));
                }
                catch
                {
                    // If the regex is invalid, assume overlap for safety
                    return true;
                }
        }
    }
}