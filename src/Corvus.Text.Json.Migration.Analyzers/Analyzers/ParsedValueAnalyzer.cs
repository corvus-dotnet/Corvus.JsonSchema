// <copyright file="ParsedValueAnalyzer.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Immutable;
using System.Linq;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Corvus.Text.Json.Migration.Analyzers;

/// <summary>
/// CVJ002: Detects uses of <c>ParsedValue&lt;T&gt;</c> type name and <c>.Instance</c> property
/// access on <c>ParsedValue&lt;T&gt;</c> instances that should be migrated to
/// <c>ParsedJsonDocument&lt;T&gt;</c> and <c>.RootElement</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ParsedValueAnalyzer : DiagnosticAnalyzer
{
    private const string ParsedValueTypeName = "ParsedValue";
    private const string FullTypeName = "Corvus.Json.ParsedValue";
    private const string InstancePropertyName = "Instance";

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.ParsedValueMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeGenericName, SyntaxKind.GenericName);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeGenericName(SyntaxNodeAnalysisContext context)
    {
        var genericName = (GenericNameSyntax)context.Node;

        if (genericName.Identifier.Text != ParsedValueTypeName)
        {
            return;
        }

        // Try to verify via the semantic model that this is Corvus.Json.ParsedValue<T>.
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(genericName, context.CancellationToken);
        ISymbol? symbol = symbolInfo.Symbol
            ?? (symbolInfo.CandidateSymbols.Length > 0 ? symbolInfo.CandidateSymbols[0] : null);

        if (symbol is not null)
        {
            INamedTypeSymbol? namedType = symbol as INamedTypeSymbol
                ?? (symbol as IMethodSymbol)?.ContainingType;

            if (namedType is not null)
            {
                string fullName = namedType.OriginalDefinition.ToDisplayString();
                if (!fullName.StartsWith(FullTypeName, System.StringComparison.Ordinal))
                {
                    return;
                }
            }
            else
            {
                // Symbol resolved but isn't a type we recognize — skip.
                return;
            }
        }

        // If symbol is null (unresolved type — e.g. namespace already changed),
        // fall through and report based on the syntax name match alone.
        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.ParsedValueMigration,
                genericName.GetLocation()));
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (memberAccess.Name.Identifier.Text != InstancePropertyName)
        {
            return;
        }

        // Try to check that the expression type is ParsedValue<T>.
        TypeInfo typeInfo = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken);
        ITypeSymbol? type = typeInfo.Type;

        if (type is INamedTypeSymbol namedType)
        {
            string fullName = namedType.OriginalDefinition.ToDisplayString();
            if (!fullName.StartsWith(FullTypeName, System.StringComparison.Ordinal))
            {
                return;
            }
        }
        else if (type is IErrorTypeSymbol || type is null)
        {
            // Type is unresolved (e.g. namespace already changed). Fall back to
            // checking if the receiver expression's syntax looks like ParsedValue<T>.
            if (!IsParsedValueSyntax(memberAccess.Expression))
            {
                return;
            }
        }
        else
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                DiagnosticDescriptors.ParsedValueMigration,
                memberAccess.Name.GetLocation()));
    }

    private static bool IsParsedValueSyntax(ExpressionSyntax expression)
    {
        // Walk through the expression to find a GenericNameSyntax with identifier "ParsedValue".
        return expression is IdentifierNameSyntax { Identifier.Text: ParsedValueTypeName }
            || expression.DescendantNodesAndSelf()
                .OfType<GenericNameSyntax>()
                .Any(g => g.Identifier.Text == ParsedValueTypeName);
    }
}