// <copyright file="AsAccessorAnalyzer.cs" company="Endjin Limited">
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
/// CVJ010: Detects V4 As* accessor patterns (AsString, AsNumber, etc.) that do not exist in V5.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AsAccessorAnalyzer : DiagnosticAnalyzer
{
    private static readonly ImmutableHashSet<string> s_asAccessors = ImmutableHashSet.Create(
        "AsString",
        "AsNumber",
        "AsObject",
        "AsArray",
        "AsBoolean",
        "AsAny",
        "AsJsonElement");

    /// <inheritdoc/>
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(DiagnosticDescriptors.AsAccessorMigration);

    /// <inheritdoc/>
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(
            AnalyzeMemberAccess,
            SyntaxKind.SimpleMemberAccessExpression);
    }

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var memberAccess = (MemberAccessExpressionSyntax)context.Node;
        string name = memberAccess.Name.Identifier.Text;

        if (s_asAccessors.Contains(name))
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
                    DiagnosticDescriptors.AsAccessorMigration,
                    memberAccess.Name.GetLocation(),
                    name));
        }
    }
}