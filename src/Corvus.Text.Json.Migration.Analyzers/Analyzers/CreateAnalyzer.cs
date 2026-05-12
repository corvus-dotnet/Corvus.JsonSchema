// <copyright file="CreateAnalyzer.cs" company="Endjin Limited">
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
/// CVJ013/CVJ014/CVJ015: Detects V4 static factory methods Create, FromItems, and FromValues.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CreateAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.CreateMigration,
            DiagnosticDescriptors.FromItemsMigration,
            DiagnosticDescriptors.FromValuesMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
        {
            string methodName = memberAccess.Name.Identifier.Text;

            DiagnosticDescriptor? descriptor = methodName switch
            {
                "Create" => DiagnosticDescriptors.CreateMigration,
                "FromItems" => DiagnosticDescriptors.FromItemsMigration,
                "FromValues" => DiagnosticDescriptors.FromValuesMigration,
                _ => null,
            };

            if (descriptor is not null)
            {
                ISymbol? symbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
                if (symbol is IMethodSymbol methodSymbol)
                {
                    if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(methodSymbol.ContainingType, context.SemanticModel.Compilation))
                    {
                        return;
                    }
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        descriptor,
                        memberAccess.Name.GetLocation()));
            }
        }
    }
}