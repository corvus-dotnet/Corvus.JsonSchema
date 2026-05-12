// <copyright file="FromJsonAnalyzer.cs" company="Endjin Limited">
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
/// CVJ006: Detects V4 <c>.FromJson(...)</c> or <c>T.FromJson(...)</c> method invocations
/// that should be replaced with <c>.From(...)</c> or <c>T.From(...)</c> in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class FromJsonAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.FromJsonMigration);

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

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.ValueText == "FromJson")
        {
            // Resolve the method symbol to find its containing type
            ISymbol? methodSymbol = context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol;
            if (methodSymbol is IMethodSymbol method)
            {
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(method.ContainingType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }
            else
            {
                // Fall back to checking the receiver expression type
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.FromJsonMigration,
                invocation.GetLocation()));
        }
    }
}