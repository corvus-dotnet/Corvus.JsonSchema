// <copyright file="JsonDocumentParseAnalyzer.cs" company="Endjin Limited">
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
/// CVJ008: Detects <c>JsonDocument.Parse(...)</c> calls that should be replaced
/// with <c>ParsedJsonDocument&lt;JsonElement&gt;.Parse(...)</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class JsonDocumentParseAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.JsonDocumentParseMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        if (memberAccess.Name.Identifier.Text != "Parse")
        {
            return;
        }

        // Extract the rightmost identifier from the receiver expression.
        // Handles both `JsonDocument.Parse(...)` and `System.Text.Json.JsonDocument.Parse(...)`.
        string? receiverName = GetRightmostIdentifier(memberAccess.Expression);
        if (receiverName != "JsonDocument")
        {
            return;
        }

        // Verify via semantic model that this is System.Text.Json.JsonDocument.
        ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
        if (symbol is IMethodSymbol methodSymbol)
        {
            string containingType = methodSymbol.ContainingType.ToDisplayString();
            if (containingType != "System.Text.Json.JsonDocument")
            {
                return;
            }
        }
        else if (symbol is not null)
        {
            // Resolved to something unexpected — skip.
            return;
        }

        // symbol is null means unresolved — still report based on syntax match.
        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.JsonDocumentParseMigration,
                invocation.GetLocation()));
    }

    private static string? GetRightmostIdentifier(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax id => id.Identifier.Text,
            MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
            AliasQualifiedNameSyntax aq => aq.Name.Identifier.Text,
            _ => null,
        };
    }
}