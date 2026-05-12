// <copyright file="WriteToAnalyzer.cs" company="Endjin Limited">
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
/// CVJ016: Detects WriteTo() calls that need to use the V5 Utf8JsonWriter type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class WriteToAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.WriteToMigration);

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

        if (invocation.Expression is MemberAccessExpressionSyntax memberAccess &&
            memberAccess.Name.Identifier.Text == "WriteTo")
        {
            if (memberAccess.Expression is { } receiverExpression)
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(receiverExpression, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            // Check that the first argument is System.Text.Json.Utf8JsonWriter
            if (invocation.ArgumentList.Arguments.Count > 0)
            {
                ExpressionSyntax firstArgExpr = invocation.ArgumentList.Arguments[0].Expression;
                ITypeSymbol? argType = context.SemanticModel.GetTypeInfo(firstArgExpr, context.CancellationToken).Type;
                if (!V4TypeHelper.IsSystemTextJsonUtf8JsonWriter(argType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.WriteToMigration,
                    memberAccess.Name.GetLocation()));
        }
    }
}