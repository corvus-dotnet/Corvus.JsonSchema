// <copyright file="AllOfSubschemaValidationHandler.cs" company="Endjin Limited">
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
using Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.AllOfChildHandlers;

/// <summary>
/// A validation handler for all-of subschema semantics.
/// </summary>
public class AllOfSubschemaValidationHandler : IChildValidationHandler
{
    /// <summary>
    /// Gets the singleton instance of the <see cref="AllOfSubschemaValidationHandler"/>.
    /// </summary>
    public static AllOfSubschemaValidationHandler Instance { get; } = new();

    /// <inheritdoc/>
    public uint ValidationHandlerPriority { get; } = ValidationPriorities.Default;

    /// <inheritdoc/>
    public CodeGenerator AppendValidationSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator;
    }

    /// <inheritdoc/>
    public CodeGenerator AppendValidationCode(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        // Determine if the parent type has its own object validation keywords.
        // If it does, ObjectValidationHandler will create the property loop and the
        // HoistedAllOfPropertyValidationHandler child will emit the hoisted code there.
        // If not, we must emit a standalone object loop here in the composition phase.
        bool parentHasObjectValidation = typeDeclaration.ValidationKeywords()
            .Any(k => k is IObjectValidationKeyword);

        bool requiresShortCut = false;

        if (typeDeclaration.AllOfCompositionTypes() is IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> subschemaDictionary)
        {
            foreach (IAllOfSubschemaValidationKeyword keyword in subschemaDictionary.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                string composedIsMatchName = generator.GetUniqueVariableNameInScope("ComposedIsMatch", prefix: keyword.Keyword);

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("bool ", composedIsMatchName, " = true;");

                // Check if the hoisted handler detected any hoistable branches for this keyword
                bool hasHoistedBranches = CodeGenerationExtensions.TryGetHoistedAllOfBranches(
                    typeDeclaration, keyword.Keyword, out List<CodeGenerationExtensions.HoistedAllOfBranchInfo>? hoistedBranches);

                IReadOnlyCollection<TypeDeclaration> subschemaTypes = subschemaDictionary[keyword];
                int totalBranches = subschemaTypes.Count;
                int hoistedCount = hasHoistedBranches ? hoistedBranches!.Count : 0;
                bool allBranchesHoisted = hasHoistedBranches && hoistedCount == totalBranches;

                int i = 0;
                foreach (TypeDeclaration subschemaType in subschemaTypes)
                {
                    if (generator.IsCancellationRequested)
                    {
                        return generator;
                    }

                    // Skip branches that were hoisted by the HoistedAllOfPropertyValidationHandler
                    if (hasHoistedBranches && hoistedBranches!.Any(b => b.BranchIndex == i))
                    {
                        i++;
                        continue;
                    }

                    if (requiresShortCut)
                    {
                        generator
                            .AppendNoCollectorNoMatchShortcutReturn();
                    }

                    ReducedTypeDeclaration reducedType = subschemaType.ReducedTypeDeclaration();
                    string localContextName = generator.GetUniqueVariableNameInScope("Context", prefix: keyword.Keyword, suffix: i.ToString());

                    string evaluationPathProperty = generator.GetPropertyNameInScope($"{keyword.Keyword}{i}SchemaEvaluationPath");
                    string targetTypeName = reducedType.ReducedType.FullyQualifiedDotnetTypeName();
                    string jsonSchemaClassName = generator.JsonSchemaClassName(targetTypeName);
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("JsonSchemaContext ", localContextName, " =")
                        .PushIndent()
                        .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".PushChildContext(parentDocument, parentIndex, ref context, schemaEvaluationPath: ", evaluationPathProperty, ");")
                        .PopIndent()
                        .AppendLineIndent(targetTypeName, ".", jsonSchemaClassName, ".Evaluate(parentDocument, parentIndex, ref ", localContextName, ");")
                        .AppendLineIndent(composedIsMatchName, " = ", composedIsMatchName, " && ", localContextName, ".IsMatch;")
                        .AppendLineIndent("context.ApplyEvaluated(ref ", localContextName, ");")
                        .AppendLineIndent("context.CommitChildContext(", localContextName, ".IsMatch, ref ", localContextName, ");");

                    requiresShortCut = true;
                    i++;
                }

                if (hasHoistedBranches && !parentHasObjectValidation)
                {
                    // Standalone case: no ObjectValidationHandler will run, so emit the full
                    // object loop with hoisted property matching here in the composition phase.
                    HoistedAllOfPropertyValidationHandler.AppendStandaloneObjectLoop(
                        generator, typeDeclaration, composedIsMatchName, keyword.Keyword);
                }
                else if (hasHoistedBranches)
                {
                    // Store the composedIsMatch variable name for the hoisted handler to use post-loop
                    typeDeclaration.SetMetadata($"HoistedAllOf.ComposedIsMatchName.{keyword.Keyword}", composedIsMatchName);

                    // Defer EvaluatedKeyword — the HoistedAllOfPropertyValidationHandler will emit it post-loop
                }
                else
                {
                    string formattedKeyword = SymbolDisplay.FormatLiteral(keyword.Keyword, true);
                    generator
                        .AppendLineIndent("context.EvaluatedKeyword(", composedIsMatchName, ", ", composedIsMatchName, "  ? JsonSchemaEvaluation.MatchedAllSchema : JsonSchemaEvaluation.DidNotMatchAllSchema, ", formattedKeyword, "u8);");
                }
            }
        }

        return generator;
    }

    public CodeGenerator AppendValidateMethodSetup(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        // Not expected to be called
        throw new InvalidOperationException();
    }
}