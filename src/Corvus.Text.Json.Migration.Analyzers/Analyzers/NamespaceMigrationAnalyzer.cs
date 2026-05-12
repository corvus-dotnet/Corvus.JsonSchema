// <copyright file="NamespaceMigrationAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// Detects <c>using Corvus.Json;</c> and <c>using Corvus.Json.*;</c> directives
/// that should be migrated to <c>Corvus.Text.Json</c> namespaces in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class NamespaceMigrationAnalyzer : DiagnosticAnalyzer
{
    private const string CorvusJsonPrefix = "Corvus.Json";

    // V4-only namespaces that have no V5 equivalent and should not be flagged.
    private static readonly ImmutableHashSet<string> s_excludedPrefixes = ImmutableHashSet.Create(
        "Corvus.Json.CodeGeneration");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.NamespaceMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeUsingDirective, SyntaxKind.UsingDirective);
    }

    private static void AnalyzeUsingDirective(SyntaxNodeAnalysisContext context)
    {
        var usingDirective = (UsingDirectiveSyntax)context.Node;

        // Ignore using aliases (e.g. using X = Corvus.Json;) and static usings.
        if (usingDirective.Alias is not null || usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword))
        {
            return;
        }

        NameSyntax? name = usingDirective.Name;
        if (name is null)
        {
            return;
        }

        string nameText = name.ToString();

        // Must start with "Corvus.Json" (exact match or followed by '.').
        if (!nameText.Equals(CorvusJsonPrefix, System.StringComparison.Ordinal) &&
            !nameText.StartsWith(CorvusJsonPrefix + ".", System.StringComparison.Ordinal))
        {
            return;
        }

        // Skip V4-only namespaces that won't exist in V5.
        foreach (string excluded in s_excludedPrefixes)
        {
            if (nameText.Equals(excluded, System.StringComparison.Ordinal) ||
                nameText.StartsWith(excluded + ".", System.StringComparison.Ordinal))
            {
                return;
            }
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.NamespaceMigration,
                usingDirective.GetLocation(),
                nameText));
    }
}