// <copyright file="MiscPatternAnalyzer.cs" company="Endjin Limited">
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
/// CVJ018/CVJ019: Detects miscellaneous V4 patterns including TryGetString
/// and backing model APIs.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MiscPatternAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> s_backingModelMembers = ImmutableHashSet.Create(
        "HasJsonElementBacking",
        "HasDotnetBacking",
        "AsDotnetBackedValue",
        "AsJsonElementBackedValue");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.TryGetStringMigration,
            DiagnosticDescriptors.BackingModelMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeInvocation,
            SyntaxKind.InvocationExpression);
        context.RegisterSyntaxNodeAction(
            AnalyzeMemberAccess,
            SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
        {
            return;
        }

        string methodName = memberAccess.Name.Identifier.Text;

        // CVJ018: TryGetString
        if (methodName == "TryGetString")
        {
            if (memberAccess.Expression is { } tryGetReceiver)
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(tryGetReceiver, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.TryGetStringMigration,
                    memberAccess.Name.GetLocation()));
            return;
        }

        // CVJ019: Backing model method calls (AsDotnetBackedValue, AsJsonElementBackedValue)
        if (s_backingModelMembers.Contains(methodName))
        {
            if (memberAccess.Expression is { } backingReceiver)
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(backingReceiver, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.BackingModelMigration,
                    memberAccess.Name.GetLocation(),
                    methodName));
        }
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;

        // Skip if this member access is already the expression of an invocation
        // (those are handled in AnalyzeInvocation to avoid duplicate diagnostics)
        if (memberAccess.Parent is InvocationExpressionSyntax)
        {
            return;
        }

        string name = memberAccess.Name.Identifier.Text;

        // CVJ019: Backing model property access (HasJsonElementBacking, HasDotnetBacking)
        if (s_backingModelMembers.Contains(name))
        {
            if (memberAccess.Expression is { } receiverExpression)
            {
                ITypeSymbol? receiverType = context.SemanticModel.GetTypeInfo(receiverExpression, context.CancellationToken).Type;
                if (!V4TypeHelper.ImplementsIJsonValueOrUnresolved(receiverType, context.SemanticModel.Compilation))
                {
                    return;
                }
            }

            context.ReportDiagnostic(
                Diagnostic.Create(
                    DiagnosticDescriptors.BackingModelMigration,
                    memberAccess.Name.GetLocation(),
                    name));
        }
    }
}