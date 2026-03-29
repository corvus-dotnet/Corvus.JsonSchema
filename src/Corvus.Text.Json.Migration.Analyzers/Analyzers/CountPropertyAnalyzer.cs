// <copyright file="CountPropertyAnalyzer.cs" company="Endjin Limited">
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
/// CVJ005: Detects V4 <c>.Count</c> property access that should be replaced
/// with <c>.GetPropertyCount()</c> in V5.
/// </summary>
/// <remarks>
/// This is a syntax-only check on member access named "Count". It may produce
/// false positives, which is acceptable for a migration tool.
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CountPropertyAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(DiagnosticDescriptors.CountMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        if (memberAccess.Name.Identifier.ValueText == "Count" &&
            memberAccess.Parent is not InvocationExpressionSyntax)
        {
            ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(memberAccess.Expression, context.CancellationToken).Type;
            if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
            {
                return;
            }

            // This is a property-style access (.Count), not a method call (.Count(...))
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.CountMigration,
                memberAccess.Name.GetLocation()));
        }
    }
}