// <copyright file="CoreTypeAnalyzer.cs" company="Endjin Limited">
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
/// CVJ007: Detects V4 core type names (<c>JsonAny</c>, <c>JsonObject</c>, <c>JsonArray</c>,
/// <c>JsonString</c>, <c>JsonNumber</c>, <c>JsonBoolean</c>, <c>JsonNull</c>) that should
/// be replaced with <c>JsonElement</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CoreTypeAnalyzer : DiagnosticAnalyzer
{
    private const string CorvusJsonNamespace = "Corvus.Json";

    private static readonly ImmutableHashSet<string> s_v4TypedCoreTypeNames = ImmutableHashSet.Create(
        "JsonObject",
        "JsonArray",
        "JsonString",
        "JsonNumber",
        "JsonBoolean",
        "JsonNull");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.CoreTypeMigration, DiagnosticDescriptors.TypedCoreMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeIdentifierName, SyntaxKind.IdentifierName);
    }

    private static void AnalyzeIdentifierName(SyntaxNodeAnalysisContext context)
    {
        var identifierName = (IdentifierNameSyntax)context.Node;
        string identifierText = identifierName.Identifier.Text;

        if (identifierText != "JsonAny" && !s_v4TypedCoreTypeNames.Contains(identifierText))
        {
            return;
        }

        // Don't fire inside using directives — that's CVJ001's job.
        if (identifierName.FirstAncestorOrSelf<UsingDirectiveSyntax>() is not null)
        {
            return;
        }

        DiagnosticDescriptor descriptor = identifierText == "JsonAny"
            ? DiagnosticDescriptors.CoreTypeMigration
            : DiagnosticDescriptors.TypedCoreMigration;

        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(identifierName, context.CancellationToken);
        ISymbol? symbol = symbolInfo.Symbol;

        if (symbol is null)
        {
            // Check if this is an unresolved error type (e.g. namespace already changed but type not yet renamed).
            ITypeSymbol? typeInfo = context.SemanticModel.GetTypeInfo(identifierName, context.CancellationToken).Type;
            if (typeInfo is IErrorTypeSymbol)
            {
                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor,
                        identifierName.GetLocation(),
                        identifierText));
            }

            return;
        }

        // Only report if the symbol resolves to a type in the Corvus.Json namespace.
        INamespaceSymbol? containingNamespace = symbol is ITypeSymbol typeSymbol
            ? typeSymbol.ContainingNamespace
            : symbol.ContainingType?.ContainingNamespace;

        if (containingNamespace?.ToDisplayString() == CorvusJsonNamespace)
        {
            context.ReportDiagnostic(
                Diagnostic.Create(
                    descriptor,
                    identifierName.GetLocation(),
                    identifierText));
        }
    }
}