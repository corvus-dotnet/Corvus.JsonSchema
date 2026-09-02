// <copyright file="HoistedAllOfPropertyValidationHandler.cs" company="Endjin Limited">
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
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// Handles property validation for allOf branches that have been hoisted into the parent's
/// single ObjectEnumerator loop, avoiding redundant property enumerations.
/// </summary>
internal sealed class HoistedAllOfPropertyValidationHandler : IChildObjectPropertyValidationHandler2, IJsonSchemaClassSetup
{
    internal const string HoistedBranchMetadataKey = "HoistedAllOfPropertyValidationHandler.BranchMetadata";
    internal const string UnifiedMapMetadataKey = "HoistedAllOfPropertyValidationHandler.UnifiedMap";
    internal const string StandaloneMapBuiltKey = "HoistedAllOfPropertyValidationHandler.StandaloneMapBuilt";
    internal const int MinHoistedPropertiesForMap = 4;

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static HoistedAllOfPropertyValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority => ValidationPriorities.AfterComposition + 50;

    /// <inheritdoc/>
    public bool WillEmitCodeFor(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? _);
    }

    /// <inheritdoc/>
    public bool RequiresPropertyNameAsString(TypeDeclaration typeDeclaration) => WillEmitCodeFor(typeDeclaration);

    private const string ClassSetupEmittedKey = "HoistedAllOfPropertyValidationHandler.ClassSetupEmitted";

    /// <inheritdoc/>
    public CodeGenerator AppendJsonSchemaClassSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        List<HoistedBranchMetadata>? branchMetadataList = DetectAndStoreHoistableBranches(generator, typeDeclaration);
        if (branchMetadataList is null || branchMetadataList.Count == 0)
        {
            return generator;
        }

        // Guard against emitting static fields twice when called from both
        // CompositionAllOfValidationHandler and ObjectValidationHandler.
        if (typeDeclaration.TryGetMetadata(ClassSetupEmittedKey, out bool emitted) && emitted)
        {
            return generator;
        }

        typeDeclaration.SetMetadata(ClassSetupEmittedKey, true);

        foreach (HoistedBranchMetadata branchMeta in branchMetadataList)
        {
            foreach (HoistedPropertyMetadata propMeta in branchMeta.Properties)
            {
                string evaluationPath = SymbolDisplay.FormatLiteral(propMeta.KeywordPathModifier, true);
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "private static readonly JsonSchemaPathProvider ",
                        propMeta.EvalPathName, " = static (buffer, out written) => JsonSchemaEvaluation.TryCopyPath(",
                        evaluationPath,
                        "u8, buffer, out written);");
            }

            foreach (HoistedRequiredMetadata reqMeta in branchMeta.RequiredProperties)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "private static readonly JsonSchemaMessageProvider<int> ",
                        reqMeta.PresentMessageName,
                        " = static (_, buffer, out written) => JsonSchemaEvaluation.RequiredPropertyPresent(",
                        SymbolDisplay.FormatLiteral(reqMeta.JsonPropertyName, true),
                        "u8, buffer, out written);")
                    .AppendLineIndent(
                        "private static readonly JsonSchemaMessageProvider<int> ",
                        reqMeta.NotPresentMessageName,
                        " = static (_, buffer, out written) => JsonSchemaEvaluation.RequiredPropertyNotPresent(",
                        SymbolDisplay.FormatLiteral(reqMeta.JsonPropertyName, true),
                        "u8, buffer, out written);")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private const int ", reqMeta.OffsetName, " = ", reqMeta.Offset.ToString(), ";")
#if NET
                    .AppendLineIndent("private const uint ", reqMeta.BitName, " = 0b", reqMeta.BitValue.ToString("b32"), ";");
#else
                    .AppendLineIndent("private const uint ", reqMeta.BitName, " = ", reqMeta.BitValue.ToString(), ";");
#endif
            }

            foreach (KeyValuePair<int, string> bitmask in branchMeta.RequiredBitmasks)
            {
                List<string> bitNames = branchMeta.RequiredProperties
                    .Where(r => r.Offset == bitmask.Key)
                    .Select(r => r.BitName)
                    .ToList();

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private const uint ", bitmask.Value, " =")
                    .PushIndent()
                    .AppendIndent(string.Join(" | ", bitNames))
                    .AppendLine(";")
                    .PopIndent();
            }
        }

        // For standalone case (no parent ObjectValidationHandler), emit the map directly.
        // For parent-hosted case, PropertiesValidationHandler will build the unified map.
        bool parentHasObjectValidation = typeDeclaration.ValidationKeywords()
            .Any(k => k is IObjectValidationKeyword);

        if (!parentHasObjectValidation)
        {
            int totalHoistedProperties = branchMetadataList.Sum(b => b.Properties.Count);
            if (totalHoistedProperties >= MinHoistedPropertiesForMap)
            {
                UnifiedMapInfo plan = BuildDispatchPlan(branchMetadataList, localEntries: null);
                EmitPropertyIndexMap(generator, plan, "TryGetHoistedPropertyIndex");
                typeDeclaration.SetMetadata(StandaloneMapBuiltKey, plan);
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? branchMetadataList) ||
            branchMetadataList is null)
        {
            return generator;
        }

        foreach (HoistedBranchMetadata branchMeta in branchMetadataList)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("JsonSchemaContext ", branchMeta.ContextName, " =")
                .PushIndent()
                .AppendLineIndent(
                    branchMeta.TargetTypeName, ".", branchMeta.JsonSchemaClassName,
                    ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ",
                    branchMeta.EvalPathPropertyName, ");")
                .PopIndent();

            if (branchMeta.RequiredIntCount > 0)
            {
                generator
                    .AppendLineIndent("Span<uint> ", branchMeta.RequiredBitsName, " = stackalloc uint[", branchMeta.RequiredIntCount.ToString(), "];");
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendObjectPropertyValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? branchMetadataList) ||
            branchMetadataList is null)
        {
            return generator;
        }

        // If a unified map was built (by PropertiesValidationHandler for parent-hosted case),
        // emit the unified switch covering both local and hoisted properties.
        if (typeDeclaration.TryGetMetadata(UnifiedMapMetadataKey, out UnifiedMapInfo? unifiedMap) && unifiedMap is not null)
        {
            return EmitUnifiedSwitch(generator, typeDeclaration, unifiedMap);
        }

        // Otherwise, emit SequenceEqual chains (no map, or below threshold).
        foreach (HoistedBranchMetadata branchMeta in branchMetadataList)
        {
            if (branchMeta.Properties.Count == 0)
            {
                continue;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("// Hoisted allOf[", branchMeta.BranchIndex.ToString(), "] property matching");

            bool first = true;
            foreach (HoistedPropertyMetadata propMeta in branchMeta.Properties)
            {
                EmitSequenceEqualPropertyMatch(generator, branchMeta, propMeta, ref first);
            }
        }

        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? branchMetadataList) ||
            branchMetadataList is null)
        {
            return generator;
        }

        // Branch contexts are pushed in forward order in AppendValidationSetup ([b0, b1, ... bN]),
        // so their child contexts must be committed in reverse order ([bN, ... b1, b0]) to satisfy
        // the results collector's LIFO context-sequence invariant. Branches are contiguous per
        // keyword in branchMetadataList, so reversing both the keyword-group order and the
        // within-group order reproduces the exact reverse of the push order. All other per-branch
        // work (composedIsMatch accumulation, ApplyEvaluated, required checks) is order-independent.
        foreach (IGrouping<string, HoistedBranchMetadata> keywordGroup in branchMetadataList.GroupBy(b => b.KeywordName).Reverse())
        {
            string keywordName = keywordGroup.Key;

            if (!typeDeclaration.TryGetMetadata($"HoistedAllOf.ComposedIsMatchName.{keywordName}", out string? composedIsMatchName) ||
                composedIsMatchName is null)
            {
                continue;
            }

            foreach (HoistedBranchMetadata branchMeta in keywordGroup.Reverse())
            {
                EmitRequiredChecks(generator, branchMeta);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(composedIsMatchName, " = ", composedIsMatchName, " && ", branchMeta.ContextName, ".IsMatch;")
                    .AppendLineIndent("context.ApplyEvaluated(ref ", branchMeta.ContextName, ");")
                    .AppendLineIndent("context.CommitChildContext(", branchMeta.ContextName, ".IsMatch, ref ", branchMeta.ContextName, ");");
            }

            string formattedKeyword = SymbolDisplay.FormatLiteral(keywordName, true);
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("context.EvaluatedKeyword(", composedIsMatchName, ", ", composedIsMatchName, "  ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, ", formattedKeyword, "u8);");
        }

        return generator;
    }

    /// <summary>
    /// Emits a complete standalone ObjectEnumerator loop with hoisted property matching
    /// for the case when the parent type has no <see cref="IObjectValidationKeyword"/> keywords
    /// (and thus <see cref="ObjectValidationHandler"/> will not activate).
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <param name="composedIsMatchName">The name of the composed isMatch variable from the allOf handler.</param>
    /// <param name="keywordName">The keyword name for the allOf group.</param>
    /// <returns>The code generator for chaining.</returns>
    internal static CodeGenerator AppendStandaloneObjectLoop(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string composedIsMatchName,
        string keywordName)
    {
        if (!typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? branchMetadataList) ||
            branchMetadataList is null)
        {
            return generator;
        }

        List<HoistedBranchMetadata> keywordBranches = branchMetadataList
            .Where(b => b.KeywordName == keywordName)
            .ToList();

        if (keywordBranches.Count == 0)
        {
            return generator;
        }

        // Emit validation setup: child contexts and required bit spans
        foreach (HoistedBranchMetadata branchMeta in keywordBranches)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("JsonSchemaContext ", branchMeta.ContextName, " =")
                .PushIndent()
                .AppendLineIndent(
                    branchMeta.TargetTypeName, ".", branchMeta.JsonSchemaClassName,
                    ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ",
                    branchMeta.EvalPathPropertyName, ");")
                .PopIndent();

            if (branchMeta.RequiredIntCount > 0)
            {
                generator
                    .AppendLineIndent("Span<uint> ", branchMeta.RequiredBitsName, " = stackalloc uint[", branchMeta.RequiredIntCount.ToString(), "];");
            }
        }

        // Emit the ObjectEnumerator loop
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (parentDocument.GetJsonTokenType(parentIndex) == JsonTokenType.StartObject)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("int objectValidation_propertyCount = 0;")
                .AppendSeparatorLine()
                .AppendLineIndent("var objectValidation_enumerator = new ObjectEnumerator(parentDocument, parentIndex);")
                .AppendLineIndent("while (objectValidation_enumerator.MoveNext())")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("int objectValidation_currentIndex = objectValidation_enumerator.CurrentIndex;")
                    .AppendLineIndent("using UnescapedUtf8JsonString objectValidation_unescapedPropertyName = parentDocument.GetPropertyNameUnescaped(objectValidation_currentIndex);");

        // Emit per-property matching for each branch
        bool useMap = typeDeclaration.TryGetMetadata(StandaloneMapBuiltKey, out UnifiedMapInfo? standalonePlan) && standalonePlan is not null;

        if (useMap)
        {
            // Map-based dispatch. The map is shared across every keyword group's loop, so each
            // loop's switch carries only the map indices whose sites belong to its own group; a
            // name owned solely by another group falls through this switch and is handled by
            // that group's loop.
            string matchIndexVar = generator.GetUniqueVariableNameInScope("matchIndex");
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if (TryGetHoistedPropertyIndex(objectValidation_unescapedPropertyName.Span, out MatchIndex? ", matchIndexVar, "))")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("switch (", matchIndexVar, "!.Value)")
                    .AppendLineIndent("{")
                    .PushIndent();

            foreach (UnifiedMapEntry entry in standalonePlan!.Entries)
            {
                bool caseOpened = false;
                foreach ((HoistedBranchMetadata branchMeta, HoistedPropertyMetadata propMeta) in entry.HoistedSites)
                {
                    if (branchMeta.KeywordName != keywordName)
                    {
                        continue;
                    }

                    if (!caseOpened)
                    {
                        generator.AppendLineIndent("case ", entry.MapIndex.ToString(), ":");
                        generator.PushIndent();
                        caseOpened = true;
                    }

                    EmitHoistedPropertySwitchCaseBody(generator, branchMeta, propMeta);
                }

                if (caseOpened)
                {
                    generator
                        .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            generator
                    .PopIndent()
                    .AppendLineIndent("}")
                .PopIndent()
                .AppendLineIndent("}");
        }
        else
        {
            // SequenceEqual chains (below threshold)
            foreach (HoistedBranchMetadata branchMeta in keywordBranches)
            {
                if (branchMeta.Properties.Count == 0)
                {
                    continue;
                }

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("// Hoisted allOf[", branchMeta.BranchIndex.ToString(), "] property matching");

                bool first = true;
                foreach (HoistedPropertyMetadata propMeta in branchMeta.Properties)
                {
                    EmitSequenceEqualPropertyMatch(generator, branchMeta, propMeta, ref first);
                }
            }
        }

        // Close while loop and object type check
        generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("objectValidation_propertyCount++;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        // Post-loop: required checks, ApplyEvaluated, CommitChildContext, EvaluatedKeyword.
        // Contexts were pushed in forward order above, so commit them in reverse to satisfy the
        // results collector's LIFO context-sequence invariant (see AppendValidationCode).
        foreach (HoistedBranchMetadata branchMeta in Enumerable.Reverse(keywordBranches))
        {
            EmitRequiredChecks(generator, branchMeta);

            generator
                .AppendSeparatorLine()
                .AppendLineIndent(composedIsMatchName, " = ", composedIsMatchName, " && ", branchMeta.ContextName, ".IsMatch;")
                .AppendLineIndent("context.ApplyEvaluated(ref ", branchMeta.ContextName, ");")
                .AppendLineIndent("context.CommitChildContext(", branchMeta.ContextName, ".IsMatch, ref ", branchMeta.ContextName, ");");
        }

        string formattedKeyword = SymbolDisplay.FormatLiteral(keywordName, true);
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("context.EvaluatedKeyword(", composedIsMatchName, ", ", composedIsMatchName, "  ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, ", formattedKeyword, "u8);");

        return generator;
    }

    /// <summary>
    /// Builds the dispatch plan shared by the property index map and every switch that consumes
    /// it: one entry per distinct JSON property name, carrying the local property site (if any)
    /// and every hoisted branch site for that name. A name declared both locally and in one or
    /// more hoisted branches gets a single map index whose case executes all of its sites; the
    /// map lookup can only ever resolve a name to one index, so per-site entries would leave all
    /// but one site unreachable.
    /// </summary>
    /// <param name="hoistedBranches">The hoisted branch metadata.</param>
    /// <param name="localEntries">Optional local property entries for the unified map (null for standalone).</param>
    /// <returns>The dispatch plan.</returns>
    internal static UnifiedMapInfo BuildDispatchPlan(
        List<HoistedBranchMetadata> hoistedBranches,
        List<UnifiedMapLocalEntry>? localEntries)
    {
        List<UnifiedMapEntry> entries = [];
        Dictionary<string, UnifiedMapEntry> entriesByName = new(StringComparer.Ordinal);

        if (localEntries is not null)
        {
            foreach (UnifiedMapLocalEntry local in localEntries)
            {
                UnifiedMapEntry entry = new(entries.Count, local.JsonPropertyName, local, []);
                entries.Add(entry);
                entriesByName.Add(local.JsonPropertyName, entry);
            }
        }

        foreach (HoistedBranchMetadata branchMeta in hoistedBranches)
        {
            foreach (HoistedPropertyMetadata propMeta in branchMeta.Properties)
            {
                if (!entriesByName.TryGetValue(propMeta.JsonPropertyName, out UnifiedMapEntry? entry))
                {
                    entry = new(entries.Count, propMeta.JsonPropertyName, null, []);
                    entries.Add(entry);
                    entriesByName.Add(propMeta.JsonPropertyName, entry);
                }

                entry.HoistedSites.Add((branchMeta, propMeta));
            }
        }

        return new UnifiedMapInfo(entries);
    }

    /// <summary>
    /// Emits the property index map (PropertySchemaMatchers&lt;MatchIndex&gt;) as static class members.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="plan">The dispatch plan built by <see cref="BuildDispatchPlan"/>.</param>
    /// <param name="tryGetName">The name of the lookup method to emit.</param>
    internal static void EmitPropertyIndexMap(
        CodeGenerator generator,
        UnifiedMapInfo plan,
        string tryGetName)
    {
        string jsonPropertyNamesClassName = generator.JsonPropertyNamesClassName();
        string matchersName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("HoistedMatchers");
        string builderName = generator.GetUniqueStaticReadOnlyPropertyNameInScope("HoistedMatchersBuilder");

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("private static PropertySchemaMatchers<MatchIndex> ", builderName, "()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new PropertySchemaMatchers<MatchIndex>([")
                .PushIndent();

        foreach (UnifiedMapEntry entry in plan.Entries)
        {
            if (entry.Local is UnifiedMapLocalEntry local)
            {
                generator
                    .AppendLineIndent("(static () => ", jsonPropertyNamesClassName, ".", local.PropertyDotnetName, "Utf8, new MatchIndex(", entry.MapIndex.ToString(), ")),");
            }
            else
            {
                string propertyJsonName = SymbolDisplay.FormatLiteral(entry.JsonPropertyName, true);
                generator
                    .AppendLineIndent("(static () => ", propertyJsonName, "u8, new MatchIndex(", entry.MapIndex.ToString(), ")),");
            }
        }

        generator
                .PopIndent()
                .AppendLineIndent("]);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("private static PropertySchemaMatchers<MatchIndex> ", matchersName, " { get; } = ", builderName, "();")
            .AppendSeparatorLine()
            .ReserveName(tryGetName)
            .AppendLineIndent("private static bool ", tryGetName, "(ReadOnlySpan<byte> span,")
            .AppendLine("#if NET")
            .AppendLineIndent("[NotNullWhen(true)]")
            .AppendLine("#endif")
            .AppendLineIndent("out MatchIndex? index)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return ", matchersName, ".TryGetNamedMatcher(span, out index);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Emits the body of a switch case for a hoisted property (used by both standalone and unified switch).
    /// </summary>
    internal static void EmitHoistedPropertySwitchCaseBody(
        CodeGenerator generator,
        HoistedBranchMetadata branchMeta,
        HoistedPropertyMetadata propMeta)
    {
        string propertyJsonName = SymbolDisplay.FormatLiteral(propMeta.JsonPropertyName, true);
        string childContextName = generator.GetUniqueVariableNameInScope("hoistedChildContext");

        generator
            .AppendLineIndent(branchMeta.ContextName, ".AddLocalEvaluatedProperty(objectValidation_propertyCount);")
            .AppendLineIndent("context.AddAppliedEvaluatedProperty(objectValidation_propertyCount);");

        HoistedRequiredMetadata? reqMeta = branchMeta.RequiredProperties
            .FirstOrDefault(r => r.JsonPropertyName == propMeta.JsonPropertyName);
        if (reqMeta is not null)
        {
            generator
                .AppendLineIndent(branchMeta.RequiredBitsName, "[", reqMeta.OffsetName, "] |= ", reqMeta.BitName, ";");
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("JsonSchemaContext ", childContextName, " =")
            .PushIndent()
                .AppendLineIndent(propMeta.ReducedTypeName, ".", propMeta.JsonSchemaClassName, ".PushChildContextUnescaped(")
                .PushIndent()
                    .AppendLineIndent("parentDocument,")
                    .AppendLineIndent("objectValidation_currentIndex,")
                    .AppendLineIndent("ref ", branchMeta.ContextName, ",")
                    .AppendLineIndent(propertyJsonName, "u8,")
                    .AppendLineIndent("evaluationPath: ", propMeta.EvalPathName, ");")
                .PopIndent()
            .PopIndent()
            .AppendSeparatorLine()
            .AppendLineIndent(propMeta.ReducedTypeName, ".", propMeta.JsonSchemaClassName, ".Evaluate(parentDocument, objectValidation_currentIndex, ref ", childContextName, ");")
            .AppendLineIndent(branchMeta.ContextName, ".CommitChildContext(", childContextName, ".IsMatch, ref ", childContextName, ");");
    }

    /// <summary>
    /// Emits a SequenceEqual-based property match for a single hoisted property.
    /// </summary>
    private static void EmitSequenceEqualPropertyMatch(
        CodeGenerator generator,
        HoistedBranchMetadata branchMeta,
        HoistedPropertyMetadata propMeta,
        ref bool first)
    {
        string propertyJsonName = SymbolDisplay.FormatLiteral(propMeta.JsonPropertyName, true);

        if (first)
        {
            generator.AppendLineIndent("if (objectValidation_unescapedPropertyName.Span.SequenceEqual(", propertyJsonName, "u8))");
            first = false;
        }
        else
        {
            generator.AppendLineIndent("else if (objectValidation_unescapedPropertyName.Span.SequenceEqual(", propertyJsonName, "u8))");
        }

        generator
            .AppendLineIndent("{")
            .PushIndent();

        EmitHoistedPropertySwitchCaseBody(generator, branchMeta, propMeta);

        generator
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Emits the unified switch covering both local and hoisted properties for the parent-hosted case.
    /// A plan entry whose name has both a local site and hoisted sites gets one case that executes
    /// the local match first and then every hoisted branch body.
    /// </summary>
    private static CodeGenerator EmitUnifiedSwitch(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        UnifiedMapInfo unifiedMap)
    {
        string matchIndexVar = generator.GetUniqueVariableNameInScope("matchIndex");
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("if (TryGetUnifiedPropertyIndex(objectValidation_unescapedPropertyName.Span, out MatchIndex? ", matchIndexVar, "))")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("switch (", matchIndexVar, "!.Value)")
                .AppendLineIndent("{")
                .PushIndent();

        foreach (UnifiedMapEntry entry in unifiedMap.Entries)
        {
            generator.AppendLineIndent("case ", entry.MapIndex.ToString(), ":");
            generator.PushIndent();

            if (entry.Local is UnifiedMapLocalEntry local)
            {
                PropertiesValidationHandler.AppendLocalPropertyDirectCall(generator, typeDeclaration, local.MethodName);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (!context.HasCollector && !context.IsMatch)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine();
            }

            foreach ((HoistedBranchMetadata branchMeta, HoistedPropertyMetadata propMeta) in entry.HoistedSites)
            {
                EmitHoistedPropertySwitchCaseBody(generator, branchMeta, propMeta);
            }

            generator
                .AppendLineIndent("break;")
                .PopIndent();
        }

        generator
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    private static List<HoistedBranchMetadata>? DetectAndStoreHoistableBranches(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // Idempotent: if detection already ran (e.g. from CompositionAllOfValidationHandler
        // and then again from ObjectValidationHandler), return the cached result.
        if (typeDeclaration.TryGetMetadata(HoistedBranchMetadataKey, out List<HoistedBranchMetadata>? cached))
        {
            return cached;
        }

        if (typeDeclaration.AllOfCompositionTypes() is not IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> subschemaDictionary)
        {
            return null;
        }

        List<HoistedBranchMetadata>? result = null;

        // Branch-scoped identifiers are derived from the per-keyword branch index alone, so two
        // keyword groups (e.g. $ref and allOf) can both own a branch 0; track the slots handed
        // out so a later group's branch gets a disambiguated slot instead of a colliding one.
        HashSet<string> usedBranchSlots = new(StringComparer.Ordinal);

        foreach (IAllOfSubschemaValidationKeyword keyword in subschemaDictionary.Keys)
        {
            IReadOnlyCollection<TypeDeclaration> subschemaTypes = subschemaDictionary[keyword];
            int i = 0;
            foreach (TypeDeclaration subschemaType in subschemaTypes)
            {
                if (CodeGenerationExtensions.IsHoistableObjectSubschema(subschemaType))
                {
                    ReducedTypeDeclaration reducedType = subschemaType.ReducedTypeDeclaration();
                    string evalPathPropName = generator.GetPropertyNameInScope($"{keyword.Keyword}{i}SchemaEvaluationPath");
                    string targetTypeName = reducedType.ReducedType.FullyQualifiedDotnetTypeName();
                    string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);

                    HoistedBranchMetadata branchMeta = BuildBranchMetadata(
                        generator, keyword.Keyword, i, reducedType.ReducedType,
                        targetTypeName, jsonSchemaClassName, evalPathPropName, usedBranchSlots);

                    result ??= [];
                    result.Add(branchMeta);

                    CodeGenerationExtensions.HoistedAllOfBranchInfo branchInfo = new(
                        i, subschemaType, reducedType.ReducedType,
                        targetTypeName, jsonSchemaClassName, evalPathPropName);

                    if (!CodeGenerationExtensions.TryGetHoistedAllOfBranches(typeDeclaration, keyword.Keyword, out List<CodeGenerationExtensions.HoistedAllOfBranchInfo>? branches))
                    {
                        branches = [];
                        CodeGenerationExtensions.SetHoistedAllOfBranches(typeDeclaration, keyword.Keyword, branches);
                    }

                    branches.Add(branchInfo);

                    if (!typeDeclaration.TryGetMetadata("HoistedAllOf.Keywords", out List<string>? keywordNames))
                    {
                        keywordNames = [];
                        typeDeclaration.SetMetadata("HoistedAllOf.Keywords", keywordNames);
                    }

                    if (!keywordNames.Contains(keyword.Keyword))
                    {
                        keywordNames.Add(keyword.Keyword);
                    }
                }

                i++;
            }
        }

        if (result is not null)
        {
            typeDeclaration.SetMetadata(HoistedBranchMetadataKey, result);
        }

        return result;
    }

    private static HoistedBranchMetadata BuildBranchMetadata(
        CodeGenerator generator,
        string keywordName,
        int branchIndex,
        TypeDeclaration reducedType,
        string targetTypeName,
        string jsonSchemaClassName,
        string evalPathPropertyName,
        HashSet<string> usedBranchSlots)
    {
        string branchSlot = $"hoistedAllOf{branchIndex}";
        int slotDisambiguator = 1;
        while (!usedBranchSlots.Add(branchSlot))
        {
            branchSlot = $"hoistedAllOf{branchIndex}_{slotDisambiguator++}";
        }

        string contextName = $"{branchSlot}_context";
        string requiredBitsName = $"{branchSlot}_requiredBits";

        List<HoistedPropertyMetadata> properties = [];
        foreach (PropertyDeclaration property in reducedType.PropertyDeclarations)
        {
            if (property.LocalOrComposed != LocalOrComposed.Local || property.Keyword is not IObjectPropertyValidationKeyword)
            {
                continue;
            }

            string propEvalPathName = generator.GetUniqueStaticReadOnlyPropertyNameInScope($"HoistedAllOf{branchIndex}_{property.DotnetPropertyName()}SchemaEvaluationPath");
            string propReducedTypeName = property.ReducedPropertyType.FullyQualifiedDotnetTypeName();
            string propJsonSchemaClassName = generator.JsonSchemaClassName(propReducedTypeName);

            properties.Add(new HoistedPropertyMetadata(
                property.JsonPropertyName,
                property.DotnetPropertyName(),
                propEvalPathName,
                propReducedTypeName,
                propJsonSchemaClassName,
                property.KeywordPathModifier));
        }

        List<HoistedRequiredMetadata> requiredProperties = [];
        Dictionary<int, string> requiredBitmasks = [];
        int currentOffset = 0;
        int currentBitShift = 0;

        foreach (PropertyDeclaration property in reducedType.PropertyDeclarations)
        {
            if (property.RequiredOrOptional != RequiredOrOptional.Required || property.LocalOrComposed != LocalOrComposed.Local)
            {
                continue;
            }

            uint bitValue = 1U << currentBitShift;
            string bitName = generator.GetUniqueStaticReadOnlyPropertyNameInScope($"HoistedAllOf{branchIndex}_RequiredBitFor", suffix: property.JsonPropertyName, rootScope: generator.JsonSchemaClassScope());
            string offsetName = generator.GetUniqueStaticReadOnlyPropertyNameInScope($"HoistedAllOf{branchIndex}_RequiredOffsetFor", suffix: property.JsonPropertyName, rootScope: generator.JsonSchemaClassScope());
            string presentName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(property.JsonPropertyName, prefix: $"HoistedAllOf{branchIndex}_RequiredProperty", suffix: "Present");
            string notPresentName = generator.GetUniqueStaticReadOnlyPropertyNameInScope(property.JsonPropertyName, prefix: $"HoistedAllOf{branchIndex}_RequiredProperty", suffix: "NotPresent");

            string requiredKeyword = property.RequiredKeyword is IKeyword k ? k.Keyword : "required";

            requiredProperties.Add(new HoistedRequiredMetadata(
                property.JsonPropertyName,
                currentOffset, bitValue,
                bitName, offsetName,
                presentName, notPresentName,
                requiredKeyword));

            if (!requiredBitmasks.ContainsKey(currentOffset))
            {
                string bitmaskName = generator.GetUniqueStaticReadOnlyPropertyNameInScope($"HoistedAllOf{branchIndex}_RequiredBitMask{currentOffset}", rootScope: generator.JsonSchemaClassScope());
                requiredBitmasks[currentOffset] = bitmaskName;
            }

            currentBitShift++;
            if (currentBitShift == 32)
            {
                currentBitShift = 0;
                currentOffset++;
            }
        }

        int requiredIntCount = requiredProperties.Count > 0
            ? (int)Math.Ceiling(requiredProperties.Count / 32.0)
            : 0;

        return new HoistedBranchMetadata(
            keywordName, branchIndex,
            targetTypeName, jsonSchemaClassName, evalPathPropertyName,
            contextName, requiredBitsName, requiredIntCount,
            properties, requiredProperties, requiredBitmasks);
    }

    private static void EmitRequiredChecks(CodeGenerator generator, HoistedBranchMetadata branchMeta)
    {
        if (branchMeta.RequiredProperties.Count == 0)
        {
            return;
        }

        foreach (KeyValuePair<int, string> bitmask in branchMeta.RequiredBitmasks)
        {
            List<HoistedRequiredMetadata> offsetProps = branchMeta.RequiredProperties
                .Where(r => r.Offset == bitmask.Key)
                .ToList();

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("if ((~(", branchMeta.RequiredBitsName, "[", bitmask.Key.ToString(), "]) & ", bitmask.Value, ") == 0)")
                .AppendLineIndent("{")
                .PushIndent();

            if (branchMeta.RequiredProperties.Count > 2)
            {
                generator
                    .AppendLineIndent("if (", branchMeta.ContextName, ".HasCollector)")
                    .AppendLineIndent("{")
                    .PushIndent();
            }

            int index = 0;
            foreach (HoistedRequiredMetadata reqMeta in offsetProps)
            {
                string requiredKeyword = SymbolDisplay.FormatLiteral(reqMeta.RequiredKeywordName, true);
                generator
                    .AppendLineIndent(branchMeta.ContextName, ".EvaluatedKeywordForProperty(true, ", index.ToString(), ", ", reqMeta.PresentMessageName, ", ", SymbolDisplay.FormatLiteral(reqMeta.JsonPropertyName, true), "u8, ", requiredKeyword, "u8);");
                index++;
            }

            if (branchMeta.RequiredProperties.Count > 2)
            {
                generator
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else if (!", branchMeta.ContextName, ".HasCollector)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent(branchMeta.ContextName, ".EvaluatedBooleanSchema(false);")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLineIndent("else")
                .AppendLineIndent("{")
                .PushIndent();

            index = 0;
            foreach (HoistedRequiredMetadata reqMeta in offsetProps)
            {
                string requiredKeyword = SymbolDisplay.FormatLiteral(reqMeta.RequiredKeywordName, true);
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if ((", branchMeta.RequiredBitsName, "[", reqMeta.OffsetName, "] & ", reqMeta.BitName, ") == 0)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(branchMeta.ContextName, ".EvaluatedKeywordForProperty(false, ", index.ToString(), ", ", reqMeta.NotPresentMessageName, ", ", SymbolDisplay.FormatLiteral(reqMeta.JsonPropertyName, true), "u8, ", requiredKeyword, "u8);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendLineIndent("else")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent(branchMeta.ContextName, ".EvaluatedKeywordForProperty(true, ", index.ToString(), ", ", reqMeta.PresentMessageName, ", ", SymbolDisplay.FormatLiteral(reqMeta.JsonPropertyName, true), "u8, ", requiredKeyword, "u8);")
                    .PopIndent()
                    .AppendLineIndent("}");
                index++;
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    internal sealed class HoistedBranchMetadata
    {
        public HoistedBranchMetadata(
            string keywordName, int branchIndex,
            string targetTypeName, string jsonSchemaClassName, string evalPathPropertyName,
            string contextName, string requiredBitsName, int requiredIntCount,
            List<HoistedPropertyMetadata> properties,
            List<HoistedRequiredMetadata> requiredProperties,
            Dictionary<int, string> requiredBitmasks)
        {
            KeywordName = keywordName;
            BranchIndex = branchIndex;
            TargetTypeName = targetTypeName;
            JsonSchemaClassName = jsonSchemaClassName;
            EvalPathPropertyName = evalPathPropertyName;
            ContextName = contextName;
            RequiredBitsName = requiredBitsName;
            RequiredIntCount = requiredIntCount;
            Properties = properties;
            RequiredProperties = requiredProperties;
            RequiredBitmasks = requiredBitmasks;
        }

        public string KeywordName { get; }

        public int BranchIndex { get; }

        public string TargetTypeName { get; }

        public string JsonSchemaClassName { get; }

        public string EvalPathPropertyName { get; }

        public string ContextName { get; }

        public string RequiredBitsName { get; }

        public int RequiredIntCount { get; }

        public List<HoistedPropertyMetadata> Properties { get; }

        public List<HoistedRequiredMetadata> RequiredProperties { get; }

        public Dictionary<int, string> RequiredBitmasks { get; }
    }

    internal sealed class HoistedPropertyMetadata
    {
        public HoistedPropertyMetadata(
            string jsonPropertyName, string dotnetPropertyName,
            string evalPathName, string reducedTypeName, string jsonSchemaClassName,
            string keywordPathModifier)
        {
            JsonPropertyName = jsonPropertyName;
            DotnetPropertyName = dotnetPropertyName;
            EvalPathName = evalPathName;
            ReducedTypeName = reducedTypeName;
            JsonSchemaClassName = jsonSchemaClassName;
            KeywordPathModifier = keywordPathModifier;
        }

        public string JsonPropertyName { get; }

        public string DotnetPropertyName { get; }

        public string EvalPathName { get; }

        public string ReducedTypeName { get; }

        public string JsonSchemaClassName { get; }

        public string KeywordPathModifier { get; }
    }

    internal sealed class HoistedRequiredMetadata
    {
        public HoistedRequiredMetadata(
            string jsonPropertyName,
            int offset, uint bitValue,
            string bitName, string offsetName,
            string presentMessageName, string notPresentMessageName,
            string requiredKeywordName)
        {
            JsonPropertyName = jsonPropertyName;
            Offset = offset;
            BitValue = bitValue;
            BitName = bitName;
            OffsetName = offsetName;
            PresentMessageName = presentMessageName;
            NotPresentMessageName = notPresentMessageName;
            RequiredKeywordName = requiredKeywordName;
        }

        public string JsonPropertyName { get; }

        public int Offset { get; }

        public uint BitValue { get; }

        public string BitName { get; }

        public string OffsetName { get; }

        public string PresentMessageName { get; }

        public string NotPresentMessageName { get; }

        public string RequiredKeywordName { get; }
    }

    internal sealed class UnifiedMapInfo
    {
        public UnifiedMapInfo(List<UnifiedMapEntry> entries)
        {
            Entries = entries;
        }

        public List<UnifiedMapEntry> Entries { get; }
    }

    internal sealed class UnifiedMapEntry
    {
        public UnifiedMapEntry(int mapIndex, string jsonPropertyName, UnifiedMapLocalEntry? local, List<(HoistedBranchMetadata Branch, HoistedPropertyMetadata Property)> hoistedSites)
        {
            MapIndex = mapIndex;
            JsonPropertyName = jsonPropertyName;
            Local = local;
            HoistedSites = hoistedSites;
        }

        public int MapIndex { get; }

        public string JsonPropertyName { get; }

        public UnifiedMapLocalEntry? Local { get; }

        public List<(HoistedBranchMetadata Branch, HoistedPropertyMetadata Property)> HoistedSites { get; }
    }

    internal sealed class UnifiedMapLocalEntry
    {
        public UnifiedMapLocalEntry(string methodName, string propertyDotnetName, string jsonPropertyName)
        {
            MethodName = methodName;
            PropertyDotnetName = propertyDotnetName;
            JsonPropertyName = jsonPropertyName;
        }

        public string MethodName { get; }

        public string PropertyDotnetName { get; }

        public string JsonPropertyName { get; }
    }
}